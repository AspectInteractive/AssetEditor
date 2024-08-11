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
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public interface ISpriteSequence
	{
		string Name { get; }
		int Length { get; }
		int Facings { get; }
		int Tick { get; }
		int ZOffset { get; }
		int ShadowZOffset { get; }
		Rectangle Bounds { get; }
		bool IgnoreWorldTint { get; }
		float Scale { get; }
		void ResolveSprites(SpriteCache cache);
		Sprite GetSprite(int frame);
		Sprite GetSprite(int frame, WAngle facing);
		(Sprite Sprite, WAngle Rotation) GetSpriteWithRotation(int frame, WAngle facing);
		Sprite GetShadow(int frame, WAngle facing);
		float GetAlpha(int frame);
	}

	public interface ISpriteSequenceLoader
	{
		int BgraSheetSize { get; }
		int IndexedSheetSize { get; }
		Dictionary<string, ISpriteSequence> ParseSequences(ModData modData, string tileSet, SpriteCache cache, MiniYamlNode node);
	}

	public sealed class SequenceSet : IDisposable
	{
		readonly ModData modData;
		readonly string tileSet;
		IReadOnlyDictionary<string, Dictionary<string, ISpriteSequence>> images;
		public SpriteCache SpriteCache { get; }
		readonly MiniYaml additionalSequences; // for reloading

		public SequenceSet(IReadOnlyFileSystem fileSystem, ModData modData, string tileSet, MiniYaml additionalSequences)
		{
			this.modData = modData;
			this.tileSet = tileSet;
			SpriteCache = new SpriteCache(fileSystem, modData.SpriteLoaders, modData.SpriteSequenceLoader.BgraSheetSize, modData.SpriteSequenceLoader.IndexedSheetSize);
			using (new Support.PerfTimer("LoadSequences"))
				images = Load(fileSystem, additionalSequences);
			this.additionalSequences = additionalSequences;
		}

		public ISpriteSequence GetSequence(string image, string sequence)
		{
			if (!images.TryGetValue(image, out var sequences))
				throw new InvalidOperationException($"Image `{image}` does not have any sequences defined.");

			if (!sequences.TryGetValue(sequence, out var seq))
				throw new InvalidOperationException($"Image `{image}` does not have a sequence named `{sequence}`.");

			return seq;
		}

		public IEnumerable<string> Images => images.Keys;

		public bool HasSequence(string image, string sequence)
		{
			if (!images.TryGetValue(image, out var sequences))
				throw new InvalidOperationException($"Image `{image}` does not have any sequences defined.");

			return sequences.ContainsKey(sequence);
		}

		public IEnumerable<string> Sequences(string image)
		{
			if (!images.TryGetValue(image, out var sequences))
				throw new InvalidOperationException($"Image `{image}` does not have any sequences defined.");

			return sequences.Keys;
		}

		/// <summary>
		/// Reloads one or more sequences matching the key name provided from the existing sequences files.
		/// </summary>
		/// <param name="fileSystem">The filesystem to use.</param>
		/// <param name="sequenceKey">The name / key of the sequence as it is defined in one of the sequence files.</param>
		/// <param name="newAddSequences">Any new additional sequences specified.</param>
		public void ReloadSequenceSetFromNode(IReadOnlyFileSystem fileSystem, string sequenceKey, MiniYaml newAddSequences = null)
		{
			Dictionary<string, Dictionary<string, ISpriteSequence>> newImages;
			var nodes = MiniYaml.Load(fileSystem, modData.Manifest.Sequences, additionalSequences);
			var matchingSequenceNodes = nodes.Where(n => n.Key == sequenceKey).ToList();

			if (matchingSequenceNodes.Count == 0)
				return;

			newAddSequences ??= additionalSequences;
			newImages = LoadNode(fileSystem, matchingSequenceNodes, newAddSequences);
			images = newImages;
			LoadSprites(false);
		}

		/// <summary>
		/// Reloads a single sequence file.
		/// </summary>
		/// <param name="fileSystem">The filesystem to use.</param>
		/// <param name="sequencesFile">A sequences file.</param>
		/// <param name="newAddSequences">Any new additional sequences specified.</param>
		public void ReloadSequenceSetFromFiles(IReadOnlyFileSystem fileSystem, string sequencesFile, MiniYaml newAddSequences = null)
		{
			Dictionary<string, Dictionary<string, ISpriteSequence>> newImages;
			newAddSequences ??= additionalSequences;
			newImages = Load(fileSystem, sequencesFile, newAddSequences);
			images = newImages;
			LoadSprites(false);
		}

		/// <summary>
		/// Reloads one or more sequence files.
		/// </summary>
		/// <param name="fileSystem">The filesystem to use.</param>
		/// <param name="sequencesFiles">a list of one or more sequences files.</param>
		/// <param name="newAddSequences">Any new additional sequences specified.</param>
		public void ReloadSequenceSetFromFiles(IReadOnlyFileSystem fileSystem, string[] sequencesFiles = null, MiniYaml newAddSequences = null)
		{
			Dictionary<string, Dictionary<string, ISpriteSequence>> newImages;

			if (sequencesFiles == null || sequencesFiles.Length == 0)
			{
				TextNotificationsManager.Debug("No sequence file specified, reloading all sequence files.");
				sequencesFiles = modData.Manifest.Sequences;
			}
			else
				TextNotificationsManager.Debug($"Reloading sequence files: {string.Join(", ", sequencesFiles)}");

			newAddSequences ??= additionalSequences;
			newImages = Load(fileSystem, sequencesFiles, newAddSequences);
			images = newImages;
			LoadSprites(false);
		}

		public Dictionary<string, Dictionary<string, ISpriteSequence>> Load(IReadOnlyFileSystem fileSystem, string sequencesFile, MiniYaml additionalSequences)
		{
			return Load(fileSystem, new string[] { sequencesFile }, additionalSequences);
		}

		public Dictionary<string, Dictionary<string, ISpriteSequence>> Load(IReadOnlyFileSystem fileSystem, MiniYaml additionalSequences)
		{
			return Load(fileSystem, modData.Manifest.Sequences, additionalSequences);
		}

		/// <summary>
		/// Loads a list of sequence files.
		/// </summary>
		/// <param name="fileSystem">The filesystem to use.</param>
		/// <param name="sequencesFiles">a list of one or more sequences files.</param>
		/// <param name="additionalSequences">Any additional sequences specified.</param>
		/// <returns>A dictionary containing all images.</returns>
		public Dictionary<string, Dictionary<string, ISpriteSequence>> Load(IReadOnlyFileSystem fileSystem, string[] sequencesFiles, MiniYaml additionalSequences)
		{
			var nodes = MiniYaml.Load(fileSystem, sequencesFiles, additionalSequences);
			Dictionary<string, Dictionary<string, ISpriteSequence>> newImages;
			if (images == null)
				newImages = new Dictionary<string, Dictionary<string, ISpriteSequence>>();
			else
				newImages = (Dictionary<string, Dictionary<string, ISpriteSequence>>)images;

			foreach (var node in nodes)
			{
				// Nodes starting with ^ are inheritable but never loaded directly
				if (node.Key.StartsWith(ActorInfo.AbstractActorPrefix))
					continue;

				newImages[node.Key] = modData.SpriteSequenceLoader.ParseSequences(modData, tileSet, SpriteCache, node);
			}

			return newImages;
		}

		/// <summary>
		/// Loads a set of sequence nodes. Requires images to have already been loaded.
		/// </summary>
		/// <param name="fileSystem">The filesystem to use.</param>
		/// <param name="nodes">a list of nodes from one or more sequences files.</param>
		/// <param name="additionalSequences">Any additional sequences specified.</param>
		/// <returns>A dictionary containing all images.</returns>
		public Dictionary<string, Dictionary<string, ISpriteSequence>> LoadNode(IReadOnlyFileSystem fileSystem,
			List<MiniYamlNode> nodes, MiniYaml additionalSequences)
		{
			var newImages = (Dictionary<string, Dictionary<string, ISpriteSequence>>)images;

			// Nodes starting with ^ are inheritable but never loaded directly
			foreach (var node in nodes)
			{
				if (node.Key.StartsWith(ActorInfo.AbstractActorPrefix))
					return newImages;

				newImages[node.Key] = modData.SpriteSequenceLoader.ParseSequences(modData, tileSet, SpriteCache, node);
			}

			return newImages;
		}

		public void LoadSprites(bool showLoadScreen = true)
		{
			SpriteCache.LoadReservations(modData, showLoadScreen);
			foreach (var sequences in images.Values)
				foreach (var sequence in sequences)
					sequence.Value.ResolveSprites(SpriteCache);
		}

		public void Dispose()
		{
			SpriteCache.Dispose();
		}
	}
}
