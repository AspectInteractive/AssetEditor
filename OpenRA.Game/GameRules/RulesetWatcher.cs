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

		volatile bool isEnabled;
		bool isDisposed;

		CancellationTokenSource debounceCts;

		public bool IsEnabled => isEnabled;

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
			{
				watcher.Filters.Add(Path.GetFileName(file));
			}
		}

		public void ToggleWatching(bool toggleValue)
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(RulesetWatcher));

			lock (syncLock)
			{
				if (isDisposed)
					throw new ObjectDisposedException(nameof(RulesetWatcher));

				watcher.EnableRaisingEvents = toggleValue;

				isEnabled = toggleValue;

				if (!toggleValue)
				{
					debounceCts?.Dispose();
					fileQueue.Clear();
				}
			}
		}

		void FileChanged(object sender, FileSystemEventArgs e)
		{
			if (!watchFiles.ContainsKey(e.FullPath))
				return;

			lock (syncLock)
			{
				if (!isEnabled || isDisposed)
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
				if (isDisposed || !isEnabled || files.Count == 0)
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

				isEnabled = false;
				isDisposed = true;

				watcher.Changed -= FileChanged;
				watcher.Dispose();
			}
		}
	}
}
