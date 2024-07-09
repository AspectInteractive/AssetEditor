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
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenRA.FileSystem;
using OpenRA.GameRules;
using OpenRA.Traits;

namespace OpenRA
{
	public class Ruleset
	{
		public readonly ActorInfoDictionary Actors;
		public readonly IReadOnlyDictionary<string, WeaponInfo> Weapons;
		public readonly IReadOnlyDictionary<string, SoundInfo> Voices;
		public readonly IReadOnlyDictionary<string, SoundInfo> Notifications;
		public readonly IReadOnlyDictionary<string, MusicInfo> Music;
		public readonly ITerrainInfo TerrainInfo;
		public readonly IReadOnlyDictionary<string, MiniYamlNode> ModelSequences;
		public Dictionary<string, MiniYamlNode> UnresolvedRulesYamlDict;
		public List<MiniYamlNode> ResolvedRulesYaml;

		public Ruleset(
			IReadOnlyDictionary<string, ActorInfo> actors,
			IReadOnlyDictionary<string, WeaponInfo> weapons,
			IReadOnlyDictionary<string, SoundInfo> voices,
			IReadOnlyDictionary<string, SoundInfo> notifications,
			IReadOnlyDictionary<string, MusicInfo> music,
			ITerrainInfo terrainInfo,
			IReadOnlyDictionary<string, MiniYamlNode> modelSequences,
			Dictionary<string, MiniYamlNode> unresolvedRulesYamlDict,
			List<MiniYamlNode> resolvedRulesYaml)
		{
			Actors = new ActorInfoDictionary(actors);
			Weapons = weapons;
			Voices = voices;
			Notifications = notifications;
			Music = music;
			TerrainInfo = terrainInfo;
			ModelSequences = modelSequences;
			UnresolvedRulesYamlDict = unresolvedRulesYamlDict;
			ResolvedRulesYaml = resolvedRulesYaml;

			foreach (var a in Actors.Values)
			{
				a.RulesetLoaded(this, a);
				foreach (var t in a.TraitInfos<IRulesetLoaded>())
				{
					try
					{
						t.RulesetLoaded(this, a);
					}
					catch (YamlException e)
					{
						throw new YamlException($"Actor type {a.Name}: {e.Message}");
					}
				}
			}

			foreach (var weapon in Weapons)
			{
				if (weapon.Value.Projectile is IRulesetLoaded<WeaponInfo> projectileLoaded)
				{
					try
					{
						projectileLoaded.RulesetLoaded(this, weapon.Value);
					}
					catch (YamlException e)
					{
						throw new YamlException($"Projectile type {weapon.Key}: {e.Message}");
					}
				}

				foreach (var warhead in weapon.Value.Warheads)
				{
					if (warhead is IRulesetLoaded<WeaponInfo> cacher)
					{
						try
						{
							cacher.RulesetLoaded(this, weapon.Value);
						}
						catch (YamlException e)
						{
							throw new YamlException($"Weapon type {weapon.Key}: {e.Message}");
						}
					}
				}
			}
		}

		public Ruleset(
			IReadOnlyDictionary<string, ActorInfo> actors,
			IReadOnlyDictionary<string, WeaponInfo> weapons,
			IReadOnlyDictionary<string, SoundInfo> voices,
			IReadOnlyDictionary<string, SoundInfo> notifications,
			IReadOnlyDictionary<string, MusicInfo> music,
			ITerrainInfo terrainInfo,
			IReadOnlyDictionary<string, MiniYamlNode> modelSequences)
		: this(actors, weapons, voices, notifications, music, terrainInfo, modelSequences, null, null) { }

		public IEnumerable<KeyValuePair<string, MusicInfo>> InstalledMusic { get { return Music.Where(m => m.Value.Exists); } }

		static IReadOnlyDictionary<string, T> MergeOrDefault<T>(string name,
			IReadOnlyFileSystem fileSystem,
			IEnumerable<string> files,
			MiniYaml additional,
			IReadOnlyDictionary<string, T> defaults,
			Func<MiniYamlNode, T> makeObject,
			Func<MiniYamlNode, bool> filterNode = null)
		{
			if (additional == null && defaults != null)
				return defaults;

			IEnumerable<MiniYamlNode> yamlNodes = MiniYaml.Load(fileSystem, files, additional);

			// Optionally, the caller can filter out elements from the loaded set of nodes. Default behavior is unfiltered.
			if (filterNode != null)
				yamlNodes = yamlNodes.Where(k => !filterNode(k));

			return yamlNodes.ToDictionaryWithConflictLog(k => k.Key.ToLowerInvariant(), makeObject, "LoadFromManifest<" + name + ">");
		}

		public static void WriteUnresolvedRulesToText(IReadOnlyFileSystem fs, string[] ruleFiles, string outputFolder, bool deleteFirst = false)
		{
			if (deleteFirst)
				MiniYaml.DeleteAllFiles(outputFolder);

			MiniYaml.CreateFolder(outputFolder, "rules");

			foreach (var ruleFile in ruleFiles)
			{
				var rulesYamlNodes = MiniYaml.LoadWithoutInherits(fs, new List<string>() { ruleFile }, null);
				foreach (var ruleNode in rulesYamlNodes)
					MiniYaml.WriteNodeToText(outputFolder, ruleFile, ruleNode);
			}
		}

		public static MiniYaml ResolveIndividualNode(MiniYamlNode inputNode, List<MiniYamlNode> resolvedRulesYaml)
		{ return MiniYaml.AtomicMerge(inputNode, new List<IReadOnlyCollection<MiniYamlNode>>() { resolvedRulesYaml }); }

		public static void WriteResolvedRulesToText(IReadOnlyFileSystem fs, string[] ruleFiles,	string outputFolder, bool deleteFirst = false)
		{
			if (deleteFirst)
				MiniYaml.DeleteAllFiles(outputFolder);

			MiniYaml.CreateFolder(outputFolder, "rules");

			foreach (var ruleFile in ruleFiles)
			{
				var rulesYamlNodes = MiniYaml.Load(fs, ruleFiles, null);
				foreach (var ruleNode in rulesYamlNodes)
					MiniYaml.WriteNodeToText(outputFolder, ruleFile, ruleNode);
			}

		}

		public static string OutputYamlNodes(List<MiniYamlNode> nodes)
		{
			var output = "";
			foreach (var line in nodes.ToLines())
				output += line + "\n";
			return output;
		}

		public static List<MiniYamlNode> LoadFilteredYaml(IReadOnlyFileSystem fileSystem, List<MiniYamlNode> yamlNodes, Func<MiniYamlNode, bool> filterNode = null)
		{
			// Optionally, the caller can filter out elements from the loaded set of nodes. Default behavior is unfiltered.
			if (filterNode != null)
				yamlNodes = yamlNodes.Where(k => !filterNode(k)).ToList();

			return yamlNodes;
		}

		public static Dictionary<string, MiniYamlNode> LoadFilteredYamlToDictionary(IReadOnlyFileSystem fileSystem, List<MiniYamlNode> yamlNodes,
			string debugName, Func<MiniYamlNode, bool> filterNode = null)
		{
			return LoadFilteredYaml(fileSystem, yamlNodes, filterNode).ToDictionaryWithConflictLog(k => k.Key.ToLowerInvariant(), debugName, null, null);
		}

		public void LoadActorTraitsFromRulesActor(World world, ModData modData, string actorKey)
		{
			var yamlNodes = MiniYaml.LoadWithoutInherits(modData.DefaultFileSystem, modData.Manifest.Rules, null);
			static bool FilterNode(MiniYamlNode node) => node.Key.StartsWith(ActorInfo.AbstractActorPrefix);
			var actorUnresolvedRules = LoadFilteredYamlToDictionary(modData.DefaultFileSystem, yamlNodes, "UnresolvedRulesYaml", FilterNode)[actorKey.ToLowerInvariant()];

			var actor = Actors.FirstOrDefault(s => string.Equals(s.Key, actorKey, StringComparison.InvariantCultureIgnoreCase)).Value;

			Console.WriteLine($"Hot Reloading Found Actor: {actor.Name}");

			if (actor == null || actor.ActorUnresolvedRules == null || actor.ActorResolvedRules == null)
				return;

			// Partial templates are not allowed.
			if (actor.Name.Contains(ActorInfo.AbstractActorPrefix))
				return;

			var newActorUnresolvedRules = new MiniYamlNodeBuilder(actorUnresolvedRules);

			var matchingWorldActors = world.Actors.Where(a => a.Info.Name.ToLowerInvariant() == actorKey).ToList();
			matchingWorldActors.ForEach(a =>
				{
					//a.Info.ClearTraits();
					//a.DisposeTraits();
					actor.LoadTraits(modData.ObjectCreator, newActorUnresolvedRules, true);
					CallRulesetLoadedOnActors(actorKey);
					//a.LoadCachedTraits();
				});
			//matchingWorldActors.ForEach(a => a.LoadCachedTraits());
		}

		public void LoadActorTraitsFromRuleFile(World world, ModData modData, string ruleFile)
		{
			Console.WriteLine($"Hot Reloading Rule File: {ruleFile}");
			LoadActorTraitsFromRuleFile(world, modData, new string[] { ruleFile });
		}

		public void LoadActorTraitsFromRuleFile(World world, ModData modData, string[] ruleFiles = null)
		{
			if (ruleFiles == null || ruleFiles.Length == 0)
			{
				Console.WriteLine("No rule file specified, reloading all rule files.");
				ruleFiles = modData.Manifest.Rules;
			}

			var yamlNodes = MiniYaml.LoadWithoutInherits(modData.DefaultFileSystem, ruleFiles, null);

			static bool FilterNode(MiniYamlNode node) => node.Key.StartsWith(ActorInfo.AbstractActorPrefix);
			var unresolvedRules = LoadFilteredYamlToDictionary(modData.DefaultFileSystem, yamlNodes, "UnresolvedRulesYaml", FilterNode);

			var actorInfos = new List<ActorInfo>();
			foreach (var actorKey in unresolvedRules)
			{
				var actor = Actors.FirstOrDefault(s => string.Equals(s.Key, actorKey.Key, StringComparison.InvariantCultureIgnoreCase)).Value;

				Console.WriteLine($"Hot Reloading Found Actor: {actor.Name}");

				if (actor == null || actor.ActorUnresolvedRules == null || actor.ActorResolvedRules == null)
					continue;

				// Partial templates are not allowed.
				if (actor.Name.Contains(ActorInfo.AbstractActorPrefix))
					continue;

				var newActorUnresolvedRules = new MiniYamlNodeBuilder(unresolvedRules.FirstOrDefault(s => string.Equals(s.Key, actor.Name, StringComparison.InvariantCultureIgnoreCase)).Value);

				actor.LoadTraits(modData.ObjectCreator, newActorUnresolvedRules, true);
				actorInfos.Add(actor);
			}

			CallRulesetLoadedOnActorList(actorInfos);
			//world.Actors.Where(a => actorInfos.Select(i => i.Name).Contains(a.Info.Name))
			//	.ToList().ForEach(a => a.LoadCachedTraits());
		}

		public static Ruleset LoadDefaults(ModData modData)
		{
			var m = modData.Manifest;
			var fs = modData.DefaultFileSystem;

			Ruleset ruleset = null;
			void LoadRuleset()
			{
				bool FilterNode(MiniYamlNode node) => node.Key.StartsWith(ActorInfo.AbstractActorPrefix);
				var unresolvedRulesYaml = LoadFilteredYamlToDictionary(fs, MiniYaml.LoadWithoutInherits(fs, m.Rules, null), "UnresolvedRulesYaml", FilterNode);
				var resolvedRulesYaml = MiniYaml.Load(fs, m.Rules, null); // needs to not filter in order to include Inheritance nodes for AtomicMerge

				var actors = MergeOrDefault("Manifest,Rules", fs, m.Rules, null, null,
					k => new ActorInfo(modData.ObjectCreator, k.Key.ToLowerInvariant(), k),
					filterNode: n => n.Key.StartsWith(ActorInfo.AbstractActorPrefix));

				var weapons = MergeOrDefault("Manifest,Weapons", fs, m.Weapons, null, null,
					k => new WeaponInfo(k.Value));

				var voices = MergeOrDefault("Manifest,Voices", fs, m.Voices, null, null,
					k => new SoundInfo(k.Value));

				var notifications = MergeOrDefault("Manifest,Notifications", fs, m.Notifications, null, null,
					k => new SoundInfo(k.Value));

				var music = MergeOrDefault("Manifest,Music", fs, m.Music, null, null,
					k => new MusicInfo(k.Key, k.Value));

				var modelSequences = MergeOrDefault("Manifest,ModelSequences", fs, m.ModelSequences, null, null,
					k => k);

				// The default ruleset does not include a preferred tileset
				ruleset = new Ruleset(actors, weapons, voices, notifications, music, null, modelSequences,
					unresolvedRulesYaml, resolvedRulesYaml);
			}

			if (modData.IsOnMainThread)
			{
				modData.HandleLoadingProgress();

				var loader = new Task(LoadRuleset);
				loader.Start();

				// Animate the loadscreen while we wait
				while (!loader.Wait(40))
					modData.HandleLoadingProgress();
			}
			else
				LoadRuleset();

			return ruleset;
		}

		public void CallRulesetLoadedOnActors(string actorKey = null)
		{
			List<ActorInfo> actorInfos;

			if (actorKey != null)
				actorInfos = Actors.Values.Where(s => string.Equals(s.Name, actorKey, StringComparison.InvariantCultureIgnoreCase)).ToList();
			else
				actorInfos = Actors.Values.ToList();

			CallRulesetLoadedOnActorList(actorInfos);
		}

		public void CallRulesetLoadedOnActorList(List<ActorInfo> actorInfos)
		{
			foreach (var a in actorInfos)
			{
				a.RulesetLoaded(this, a);
				foreach (var t in a.TraitInfos<IRulesetLoaded>())
				{
					try
					{
						t.RulesetLoaded(this, a);
					}
					catch (YamlException e)
					{
						throw new YamlException($"Actor type {a.Name}: {e.Message}");
					}
				}
			}
		}

		public static Ruleset LoadDefaultsForTileSet(ModData modData, string tileSet)
		{
			var dr = modData.DefaultRules;
			var terrainInfo = modData.DefaultTerrainInfo[tileSet];

			return new Ruleset(dr.Actors, dr.Weapons, dr.Voices, dr.Notifications, dr.Music, terrainInfo, dr.ModelSequences);
		}

		public static Ruleset Load(ModData modData, IReadOnlyFileSystem fileSystem, string tileSet,
			MiniYaml mapRules, MiniYaml mapWeapons, MiniYaml mapVoices, MiniYaml mapNotifications,
			MiniYaml mapMusic, MiniYaml mapModelSequences)
		{
			var m = modData.Manifest;
			var dr = modData.DefaultRules;

			Ruleset ruleset = null;
			void LoadRuleset()
			{
				bool FilterNode(MiniYamlNode node) => node.Key.StartsWith(ActorInfo.AbstractActorPrefix);
				var unresolvedRulesYaml = LoadFilteredYamlToDictionary(fileSystem, MiniYaml.LoadWithoutInherits(fileSystem, m.Rules, null), "UnresolvedRulesYaml", FilterNode);
				var resolvedRulesYaml = MiniYaml.Load(fileSystem, m.Rules, null); // needs to not filter in order to include Inheritance nodes for AtomicMerge

				var actors = MergeOrDefault("Rules", fileSystem, m.Rules, mapRules, dr.Actors,
					k => new ActorInfo(modData.ObjectCreator, k.Key.ToLowerInvariant(), k),
					filterNode: n => n.Key.StartsWith(ActorInfo.AbstractActorPrefix));

				var weapons = MergeOrDefault("Weapons", fileSystem, m.Weapons, mapWeapons, dr.Weapons,
					k => new WeaponInfo(k.Value));

				var voices = MergeOrDefault("Voices", fileSystem, m.Voices, mapVoices, dr.Voices,
					k => new SoundInfo(k.Value));

				var notifications = MergeOrDefault("Notifications", fileSystem, m.Notifications, mapNotifications, dr.Notifications,
					k => new SoundInfo(k.Value));

				var music = MergeOrDefault("Music", fileSystem, m.Music, mapMusic, dr.Music,
					k => new MusicInfo(k.Key, k.Value));

				// TODO: Add support for merging custom terrain modifications
				var terrainInfo = modData.DefaultTerrainInfo[tileSet];

				var modelSequences = dr.ModelSequences;
				if (mapModelSequences != null)
					modelSequences = MergeOrDefault("ModelSequences", fileSystem, m.ModelSequences, mapModelSequences, dr.ModelSequences,
						k => k);

				ruleset = new Ruleset(actors, weapons, voices, notifications, music, terrainInfo, modelSequences,
					unresolvedRulesYaml, resolvedRulesYaml);
			}

			if (modData.IsOnMainThread)
			{
				modData.HandleLoadingProgress();

				var loader = new Task(LoadRuleset);
				loader.Start();

				// Animate the loadscreen while we wait
				while (!loader.Wait(40))
					modData.HandleLoadingProgress();
			}
			else
				LoadRuleset();

			return ruleset;
		}

		static bool AnyCustomYaml(MiniYaml yaml)
		{
			return yaml != null && (yaml.Value != null || yaml.Nodes.Length > 0);
		}

		static bool AnyFlaggedTraits(ModData modData, IEnumerable<MiniYamlNode> actors)
		{
			foreach (var actorNode in actors)
			{
				foreach (var traitNode in actorNode.Value.Nodes)
				{
					try
					{
						var traitName = traitNode.Key.Split('@')[0];
						var traitType = modData.ObjectCreator.FindType(traitName + "Info");
						if (traitType != null && traitType.GetInterface(nameof(ILobbyCustomRulesIgnore)) == null)
							return true;
					}
					catch (Exception ex)
					{
						Log.Write("debug", "Error in AnyFlaggedTraits\n" + ex.ToString());
					}
				}
			}

			return false;
		}

		public static bool DefinesUnsafeCustomRules(ModData modData, IReadOnlyFileSystem fileSystem,
			MiniYaml mapRules, MiniYaml mapWeapons, MiniYaml mapVoices, MiniYaml mapNotifications, MiniYaml mapSequences)
		{
			// Maps that define any weapon, voice, notification, or sequence overrides are always flagged
			if (AnyCustomYaml(mapWeapons) || AnyCustomYaml(mapVoices) || AnyCustomYaml(mapNotifications) || AnyCustomYaml(mapSequences))
				return true;

			// Any trait overrides that aren't explicitly whitelisted are flagged
			if (mapRules == null)
				return false;

			if (AnyFlaggedTraits(modData, mapRules.Nodes))
				return true;

			if (mapRules.Value != null)
			{
				var mapFiles = FieldLoader.GetValue<string[]>("value", mapRules.Value);
				foreach (var f in mapFiles)
					if (AnyFlaggedTraits(modData, MiniYaml.FromStream(fileSystem.Open(f), f)))
						return true;
			}

			return false;
		}
	}
}
