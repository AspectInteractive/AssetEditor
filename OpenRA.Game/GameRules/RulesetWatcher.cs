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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.GameRules
{
	public sealed class RulesetWatcher : IDisposable
	{
		static readonly StringComparer FileNameComparer = Platform.CurrentPlatform == PlatformType.Windows
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;

		static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(100);

		readonly object syncLock = new();

		readonly World world;
		readonly ModData modData;
		readonly IReadOnlyDictionary<string, string> watchFiles;
		readonly HashSet<string> fileQueue = new(FileNameComparer);
		readonly FileSystemWatcher watcher;
		bool isDisposed;

		CancellationTokenSource debounceCts;

		public RulesetWatcher(World world, ModData modData)
		{
			this.world = world;
			this.modData = modData;

			var dict = new Dictionary<string, string>(FileNameComparer);
			foreach (var file in modData.Manifest.Rules
				.Concat(modData.Manifest.Weapons)
				.Concat(modData.Manifest.Sequences))
			{
				string filename;
				using (var stream = modData.DefaultFileSystem.Open(file))
					filename = (stream as FileStream)?.Name;

				if (string.IsNullOrEmpty(filename))
					continue;

				var fullPath = Path.GetFullPath(filename);
				dict.Add(fullPath, file);
			}

			watchFiles = dict.ToImmutableDictionary(FileNameComparer);

			watcher = new FileSystemWatcher(modData.Manifest.Package.Name)
			{
				IncludeSubdirectories = true
			};
			watcher.Changed += FileChanged;

			foreach (var file in watchFiles.Keys)
				watcher.Filters.Add(Path.GetFileName(file));

			watcher.EnableRaisingEvents = true;
		}

		void FileChanged(object sender, FileSystemEventArgs e)
		{
			if (!watchFiles.ContainsKey(e.FullPath))
				return;

			lock (syncLock)
			{
				if (isDisposed)
					return;

				fileQueue.Add(e.FullPath);

				if (debounceCts != null)
				{
					debounceCts.Cancel();
					debounceCts.Dispose();
				}

				// Since CancellationTokenSource is already thread-safe, let's leverage that for a debounce mechanism:
				// Taking a local copy of CancellationToken from current CancellationTokenSource makes sure that current Task can be cancelled
				// by the next thread. Also, we cannot store CancellationTokenSource, since after it is disposed, CancellationToken cannot be accessed anymore.
				// Superfluous CancellationTokenSource will be disposed in ToggleWatching() or Dispose().
				debounceCts = new CancellationTokenSource();
				var localToken = debounceCts.Token;

				Task.Run(async () =>
				{
					await Task.Delay(DebounceInterval, localToken);

					List<string> changedFiles;
					lock (syncLock)
					{
						changedFiles = fileQueue.ToList();
						fileQueue.Clear();
					}

					Game.RunAfterTick(() => DoRulesetReload(changedFiles));
				});
			}
		}

		void DoRulesetReload(ICollection<string> files)
		{
			lock (syncLock)
			{
				if (isDisposed || files.Count == 0)
					return;
			}

			var modFsFilenames = files.Select(f => watchFiles[f]).ToHashSet(FileNameComparer);

			var defaultRules = world.Map.Rules;
			var rulesFiles = FindModFiles(modData.Manifest.Rules, modFsFilenames).ToArray();
			var weaponFiles = FindModFiles(modData.Manifest.Weapons, modFsFilenames).ToArray();
			var sequenceFiles = FindModFiles(modData.Manifest.Sequences, modFsFilenames).ToArray();

			if (rulesFiles.Length > 0)
				TryExecute(() => defaultRules.LoadActorTraitsFromRuleFile(world, modData, rulesFiles));
			if (weaponFiles.Length > 0)
				TryExecute(() => defaultRules.LoadWeaponsFromFile(world, modData, weaponFiles));
			if (sequenceFiles.Length > 0)
				TryExecute(() => world.Map.Sequences.ReloadSequenceSetFromFiles(modData.DefaultFileSystem, sequenceFiles));

			if (Game.Settings.Debug.RecreateActorsAfterRulesetReload)
				world.RecreateActors();

			static IEnumerable<string> FindModFiles(IEnumerable<string> allFiles, ISet<string> findFiles)
				=> allFiles.Where(findFiles.Contains);

			static void TryExecute(Action action, int attempts = 2)
			{
				for (var i = 0; i < attempts; i++)
				{
					try
					{
						action();

						return;
					}
					catch (IOException)
					{
						// IO errors are not critical, we should be able to retry them a few times (and even swallow them, if they keep occurring)
						// TODO: error handling should be in each respective code that reloads ruleset/sequences/etc.
					}
				}
			}
		}

		public void Dispose()
		{
			if (isDisposed)
				return;

			lock (syncLock)
			{
				if (isDisposed)
					return;

				debounceCts?.Dispose();
				fileQueue.Clear();

				isDisposed = true;

				watcher.Changed -= FileChanged;
				watcher.Dispose();
			}
		}
	}
}
