#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.BZip2;
using OpenRA.FileSystem;
using OpenRA.Primitives;
using OpenRA.Traits;
using static System.Net.WebRequestMethods;

namespace OpenRA
{	
	/// <summary>
	/// A unit/building inside the game. Every rules starts with one and adds trait to it.
	/// </summary>
	public class ActorInfo : IRulesetLoaded
	{
		public const char AbstractActorPrefix = '^';
		public const char TraitInstanceSeparator = '@';

		/// <summary>
		/// The actor name can be anything, but the sprites used in the Render*: traits default to this one.
		/// If you add an ^ in front of the name, the engine will recognize this as a collection of traits
		/// that can be inherited by others (using Inherits:) and not a real unit.
		/// You can remove inherited traits by adding a - in front of them as in -TraitName: to inherit everything, but this trait.
		/// </summary>
		public readonly string Name;
		TypeDictionary traits = new();
		List<TraitInfo> constructOrderCache = null;
		public Ruleset Rules; // used for storing unresolvedRulesYaml and resolvedRulesYaml
		public MiniYamlNodeBuilder ActorResolvedRules; // used for storing unresolvedRulesYaml and resolvedRulesYaml
		public MiniYamlNodeBuilder ActorUnresolvedRules; // used for storing unresolvedRulesYaml and resolvedRulesYaml

		public enum RulesType
		{
			Resolved = 1,
			Unresolved = 2,
		}

		public ActorInfo(ObjectCreator creator, string name, MiniYamlNode node)
		{
			Name = name;
			LoadTraits(creator, node);
		}

		public ActorInfo(ObjectCreator creator, string name, MiniYamlNode node, Ruleset rules)
		{
			Name = name;
			Rules = rules;
			LoadTraits(creator, node);
		}

		public void LoadTraits(ObjectCreator creator, MiniYamlNodeBuilder node, bool clearAllFirst = false)
		{
			LoadTraits(creator, new MiniYamlNode(node), clearAllFirst);
		}

		public void LoadTraits(ObjectCreator creator, MiniYamlNode node, bool clearAllFirst = false)
		{
			//Console.WriteLine($"~~~ Loading Traits for Actor: {Name}, node: {node.Key} ~~~");
			//Console.WriteLine($"~ NEW YAML NODE ~\n{MiniYaml.GetNodeOutputString(node)}");
			MiniYaml yaml;
			if (Rules != null && Rules.ResolvedRulesYaml != null)
				yaml = Ruleset.ResolveIndividualNode(node, Rules.ResolvedRulesYaml);
			else
				yaml = node.Value;

			if (clearAllFirst)
				ClearTraits();

			try
			{
				foreach (var t in yaml.Nodes)
				{
					try
					{
						// HACK: The linter does not want to crash when a trait doesn't exist but only print an error instead
						// LoadTraitInfo will only return null to signal us to abort here if the linter is running
						var trait = LoadTraitInfo(creator, t.Key, t.Value);
						if (trait != null)
							traits.Add(trait);
						//Console.WriteLine($"Trait {trait} loaded.");
					}
					catch (FieldLoader.MissingFieldsException e)
					{
						throw new YamlException(e.Message);
					}
				}

				traits.TrimExcess();
			}
			catch (YamlException e)
			{
				throw new YamlException($"Error loading traits: {e.Message}");
			}
		}

		public ActorInfo(string name, params TraitInfo[] traitInfos)
		{
			Name = name;
			foreach (var t in traitInfos)
				traits.Add(t);
			traits.TrimExcess();
		}

		public ActorInfo(string name, MiniYamlNode actorResolvedRules, MiniYamlNode actorUnresolvedRules, params TraitInfo[] traitInfos)
		{
			Name = name;
			ActorResolvedRules = new MiniYamlNodeBuilder(actorResolvedRules);
			ActorUnresolvedRules = new MiniYamlNodeBuilder(actorUnresolvedRules);
			foreach (var t in traitInfos)
				traits.Add(t);
			traits.TrimExcess();
		}

		public void RulesetLoaded(Ruleset rules, ActorInfo info)
		{

			Rules = rules;
			Rules.UnresolvedRulesYamlDict.TryGetValue(Name.ToLowerInvariant(), out var actorUnresolvedRulesYaml);
			ActorUnresolvedRules = new MiniYamlNodeBuilder(actorUnresolvedRulesYaml);
			ActorResolvedRules = new MiniYamlNodeBuilder(Rules.ResolvedRulesYaml.
				FirstOrDefault(s => string.Equals(s.Key, Name, StringComparison.InvariantCultureIgnoreCase)));
		}

		public void ClearTraits()
		{
			traits = new(); // Incase we are reloading traits
			constructOrderCache = null;
		}

		public void EditTraitOrField(MiniYamlNodeBuilder node, string newValue)
		{
			// For testing only
			Console.WriteLine("~~~ BEFORE ~~~ \n" + MiniYaml.GetNodeOutputString(ActorUnresolvedRules));

			var newValueList = newValue.Split(':');
			var newValueTrait = newValueList[0];
			var newValueValue = newValueList[1].TrimStart();

			node.Key = newValueTrait;
			node.Value.Value = newValueValue;

			// For testing only
			Console.WriteLine("~~~ AFTER ~~~ \n" + MiniYaml.GetNodeOutputString(ActorUnresolvedRules));
		}

		public void EditTrait(ObjectCreator creator, string traitName, string newName, RulesType rulesType)
		{
			if (!Rules.Actors.Select(a => a.Value.TraitInfos<TraitInfo>().Select(t => t.GetType().Name == traitName)).Any())
				throw new YamlException($"Existing trait cannot be found: {traitName}");

			if (!Rules.Actors.Select(a => a.Value.TraitInfos<TraitInfo>().Select(t => t.GetType().Name == newName)).Any())
				throw new YamlException($"New trait cannot be found: {newName}");

			var matchingTraitNodes = new List<MiniYamlNodeBuilder>();
			MiniYamlNodeBuilder rulesForReloadingTraits = null;

			if (rulesType is RulesType.Resolved)
			{
				matchingTraitNodes = ActorResolvedRules.Value.Nodes.Where(t => t.Key + ":" == traitName).ToList();
				rulesForReloadingTraits = ActorResolvedRules;
			}
			else if (rulesType is RulesType.Unresolved)
			{
				matchingTraitNodes = ActorUnresolvedRules.Value.Nodes.Where(t => t.Key + ":" == traitName).ToList();
				rulesForReloadingTraits = ActorUnresolvedRules;
			}

			if (matchingTraitNodes.Count > 0)
			{
				foreach (var traitNode in matchingTraitNodes)
					traitNode.Key = newName;

				if (rulesForReloadingTraits != null)
					LoadTraits(creator, rulesForReloadingTraits, true);
			}
		}

		static TraitInfo LoadTraitInfo(ObjectCreator creator, string traitName, MiniYaml my)
		{
			if (!string.IsNullOrEmpty(my.Value))
				throw new YamlException($"Junk value `{my.Value}` on trait node {traitName}");

			// HACK: The linter does not want to crash when a trait doesn't exist but only print an error instead
			// ObjectCreator will only return null to signal us to abort here if the linter is running
			var traitInstance = traitName.Split(TraitInstanceSeparator);
			var info = creator.CreateObject<TraitInfo>(traitInstance[0] + "Info");
			if (info == null)
				return null;

			try
			{
				if (traitInstance.Length > 1)
					info.GetType().GetField(nameof(info.InstanceName)).SetValue(info, traitInstance[1]);

				FieldLoader.Load(info, my);
			}
			catch (FieldLoader.MissingFieldsException e)
			{
				var header = "Trait name " + traitName + ": " + (e.Missing.Length > 1 ? "Required properties missing" : "Required property missing");
				throw new FieldLoader.MissingFieldsException(e.Missing, header);
			}

			return info;
		}

		public IEnumerable<TraitInfo> TraitsInConstructOrder()
		{
			if (constructOrderCache != null)
				return constructOrderCache;

			var source = traits.WithInterface<TraitInfo>().Select(i => new
			{
				Trait = i,
				Type = i.GetType(),
				Dependencies = PrerequisitesOf(i).ToList(),
				OptionalDependencies = OptionalPrerequisitesOf(i).ToList()
			}).ToList();

			var resolved = source.Where(s => s.Dependencies.Count == 0 && s.OptionalDependencies.Count == 0).ToList();
			var unresolved = source.ToHashSet();
			unresolved.ExceptWith(resolved);

			static bool AreResolvable(Type a, Type b) => a.IsAssignableFrom(b);

			// This query detects which unresolved traits can be immediately resolved as all their direct dependencies are met.
			var more = unresolved.Where(u =>
				u.Dependencies.All(d => // To be resolvable, all dependencies must be satisfied according to the following conditions:
					resolved.Exists(r => AreResolvable(d, r.Type)) && // There must exist a resolved trait that meets the dependency.
					!unresolved.Any(u1 => AreResolvable(d, u1.Type))) && // All matching traits that meet this dependency must be resolved first.
				u.OptionalDependencies.All(d => // To be resolvable, all optional dependencies must be satisfied according to the following condition:
					!unresolved.Any(u1 => AreResolvable(d, u1.Type)))); // All matching traits that meet this optional dependencies must be resolved first.

			// Continue resolving traits as long as possible.
			// Each time we resolve some traits, this means dependencies for other traits may then be possible to satisfy in the next pass.
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
			var readyToResolve = more.ToList();
			while (readyToResolve.Count != 0)
			{
				resolved.AddRange(readyToResolve);
				unresolved.ExceptWith(readyToResolve);
				readyToResolve.Clear();
				readyToResolve.AddRange(more);
			}
#pragma warning restore CA1851

			if (unresolved.Count != 0)
			{
				var exceptionString = "ActorInfo(\"" + Name + "\") failed to initialize because of the following:\n";
				var missing = unresolved.SelectMany(u => u.Dependencies.Where(d => !source.Any(s => AreResolvable(d, s.Type)))).Distinct();

				exceptionString += "Missing:\n";
				foreach (var m in missing)
					exceptionString += m + " \n";

				exceptionString += "Unresolved:\n";
				foreach (var u in unresolved)
				{
					var deps = u.Dependencies.Where(d => !resolved.Exists(r => r.Type == d));
					var optDeps = u.OptionalDependencies.Where(d => !resolved.Exists(r => r.Type == d));
					var allDeps = string.Join(", ", deps.Select(o => o.ToString()).Concat(optDeps.Select(o => $"[{o}]")));
					exceptionString += $"{u.Type}: {{ {allDeps} }}\n";
				}

				throw new YamlException(exceptionString);
			}

			constructOrderCache = resolved.ConvertAll(r => r.Trait);
			return constructOrderCache;
		}

		public static IEnumerable<Type> PrerequisitesOf(TraitInfo info)
		{
			return info
				.GetType()
				.GetInterfaces()
				.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Requires<>))
				.Select(t => t.GetGenericArguments()[0]);
		}

		public static IEnumerable<Type> OptionalPrerequisitesOf(TraitInfo info)
		{
			return info
				.GetType()
				.GetInterfaces()
				.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(NotBefore<>))
				.Select(t => t.GetGenericArguments()[0]);
		}

		public bool HasTraitInfo<T>() where T : ITraitInfoInterface { return traits.Contains<T>(); }
		public T TraitInfo<T>() where T : ITraitInfoInterface { return traits.Get<T>(); }
		public T TraitInfoOrDefault<T>() where T : ITraitInfoInterface { return traits.GetOrDefault<T>(); }
		public IReadOnlyCollection<T> TraitInfos<T>() where T : ITraitInfoInterface { return traits.WithInterface<T>(); }

		public BitSet<TargetableType> GetAllTargetTypes()
		{
			// PERF: Avoid LINQ.
			var targetTypes = default(BitSet<TargetableType>);
			foreach (var targetable in TraitInfos<ITargetableInfo>())
				targetTypes = targetTypes.Union(targetable.GetTargetTypes());
			return targetTypes;
		}
	}
}
