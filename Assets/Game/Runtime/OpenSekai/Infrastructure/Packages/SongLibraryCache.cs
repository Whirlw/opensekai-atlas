using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Shared.Packages;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
	/// <summary>
	/// The library's package list, kept in memory and saved as one snapshot file in the library
	/// root. Every read is served from it: a launch reads the snapshot instead of the folders, and
	/// only a missing or unreadable snapshot (or <see cref="Rescan"/>, the refresh button) walks
	/// the package folders. Changes made through the app go straight in with <see cref="Track"/> and
	/// <see cref="Untrack"/>; anything changed behind the app's back stays as cached until a rescan.
	/// </summary>
	internal sealed class SongLibraryCache
	{
		internal const string FILE_NAME = "library_cache.json";
		private const string LEGACY_HASH_FILE_NAME = "package_hashes.json";
		private const int SNAPSHOT_VERSION = 2; // 2: the score key hashes the score file alone

		private readonly string _root;
		private Dictionary<string, Cached> _packages;
		private SongPackageItem[] _sorted;

		internal SongLibraryCache(string rootDirectory)
		{
			_root = Path.GetFullPath(rootDirectory);
		}

		internal string SnapshotPath => Path.Combine(_root, FILE_NAME);

		internal bool IsLoaded => _packages != null;

		/// <summary>Every cached package, newest first; loads the snapshot (or scans once) on first use.</summary>
		internal IReadOnlyList<SongPackageItem> Items()
		{
			EnsureLoaded();
			if (_sorted == null)
			{
				_sorted = _packages.Values
					.Select(cached => cached.Item)
					.OrderByDescending(item => item.LastWriteTime)
					.ThenBy(item => item.Entry.Manifest.scoreTitle, StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
			return _sorted;
		}

		/// <summary>
		/// Walks every package folder again and saves the result: the one way back in sync with
		/// files changed outside the app. Hashes are reused for packages whose files kept their size
		/// and write time.
		/// </summary>
		internal void Rescan()
		{
			Dictionary<string, Cached> previous = _packages ?? ReadSnapshot() ?? NewMap();
			var scanned = NewMap();
			if (Directory.Exists(_root))
			{
				foreach (string directory in Directory.GetDirectories(_root))
				{
					string key = KeyOf(directory);
					previous.TryGetValue(key, out Cached old);
					Cached cached = Scan(directory, old);
					if (cached != null)
					{
						scanned[key] = cached;
					}
				}
			}

			_packages = scanned;
			_sorted = null;
			Save();
		}

		/// <summary>Reads one package folder again and puts it in the cache (or drops it when it is no longer a package).</summary>
		internal SongPackageItem Track(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !IsInsideRoot(directory))
			{
				return null;
			}

			EnsureLoaded();
			string key = KeyOf(directory);
			_packages.TryGetValue(key, out Cached old);
			Cached cached = Scan(directory, old);
			if (cached != null)
			{
				_packages[key] = cached;
			}
			else
			{
				_packages.Remove(key);
			}

			_sorted = null;
			Save();
			return cached?.Item;
		}

		internal void Untrack(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !IsInsideRoot(directory) || !IsLoaded)
			{
				return;
			}

			if (_packages.Remove(KeyOf(directory)))
			{
				_sorted = null;
				Save();
			}
		}

		/// <summary>The cached package in this folder; null when it is not cached. Never loads or scans.</summary>
		internal SongPackageItem Find(string directory)
		{
			if (!IsLoaded || string.IsNullOrEmpty(directory) || !IsInsideRoot(directory))
			{
				return null;
			}

			return _packages.TryGetValue(KeyOf(directory), out Cached cached) ? cached.Item : null;
		}

		private void EnsureLoaded()
		{
			if (_packages != null)
			{
				return;
			}

			Dictionary<string, Cached> snapshot = ReadSnapshot();
			if (snapshot != null)
			{
				_packages = snapshot;
				_sorted = null;
				return;
			}

			Rescan();
		}

		private Cached Scan(string directory, Cached previous)
		{
			SongPackage entry = SongPackageStorage.LoadEntry(directory);
			if (entry == null)
			{
				return null;
			}

			// May rewrite the manifest; the key only depends on the score file, so it is unaffected.
			DateTimeOffset importedAt = EnsureImportedAt(entry);
			FileStamp scoreStamp = FileStamp.Of(entry.ScorePath);
			string scoreFileName = entry.Manifest.scoreFileName ?? string.Empty;
			string scoreKey = previous != null
				&& !string.IsNullOrEmpty(previous.ScoreKey)
				&& string.Equals(previous.ScoreFileName, scoreFileName, StringComparison.Ordinal)
				&& previous.ScoreStamp.Equals(scoreStamp)
					? previous.ScoreKey
					: SongPackageHash.Compute(entry.ScorePath);

			DateTime lastWriteTime = Directory.GetLastWriteTime(entry.RootDirectory);
			lastWriteTime = Later(lastWriteTime, entry.ManifestPath);
			lastWriteTime = Later(lastWriteTime, entry.ScorePath);
			lastWriteTime = Later(lastWriteTime, entry.VideoPath);

			var item = new SongPackageItem(
				entry,
				lastWriteTime,
				File.Exists(entry.ManifestPath),
				File.Exists(entry.ScorePath),
				File.Exists(entry.AudioPath),
				File.Exists(entry.JacketPath),
				importedAt,
				scoreKey);
			return new Cached(item, scoreStamp, scoreFileName);
		}

		private static DateTime Later(DateTime current, string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return current;
			}

			DateTime written = File.GetLastWriteTime(path);
			return written > current ? written : current;
		}

		private static DateTimeOffset EnsureImportedAt(SongPackage entry)
		{
			if (entry.Manifest.importedAtUtc is DateTimeOffset importedAt)
			{
				return importedAt;
			}

			importedAt = DateTimeOffset.UtcNow;
			try
			{
				JObject manifest = JObject.Parse(File.ReadAllText(entry.ManifestPath));
				manifest[nameof(SongPackageManifest.importedAtUtc)] = new JValue(importedAt);
				AtomicFile.WriteAllText(entry.ManifestPath, manifest.ToString(Formatting.Indented));
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
				|| exception is JsonException)
			{
				Debug.LogWarning("[Library] Could not save import time: " + exception.Message);
			}

			entry.Manifest.importedAtUtc = importedAt;
			return importedAt;
		}

		private Dictionary<string, Cached> ReadSnapshot()
		{
			string path = SnapshotPath;
			if (!File.Exists(path))
			{
				return null;
			}

			try
			{
				Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(path));
				if (snapshot?.packages == null || snapshot.version != SNAPSHOT_VERSION)
				{
					return null;
				}

				var map = NewMap();
				foreach (Record record in snapshot.packages)
				{
					if (record?.manifest == null || string.IsNullOrEmpty(record.folder))
					{
						continue;
					}

					record.manifest.Normalize();
					var entry = new SongPackage(Path.Combine(_root, record.folder), record.manifest);
					var item = new SongPackageItem(
						entry,
						new DateTime(record.lastWriteTicks, DateTimeKind.Local),
						record.hasManifest,
						record.hasScore,
						record.hasAudio,
						record.hasJacket,
						record.manifest.importedAtUtc ?? DateTimeOffset.MinValue,
						record.scoreKey);
					map[record.folder] = new Cached(item, record.scoreStamp, record.scoreFileName ?? string.Empty);
				}
				return map;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
				|| exception is JsonException)
			{
				// An unreadable snapshot only costs one scan.
				Debug.LogWarning("[Library] Could not read the library cache: " + exception.Message);
				return null;
			}
		}

		private void Save()
		{
			if (!Directory.Exists(_root))
			{
				return;
			}

			var snapshot = new Snapshot { version = SNAPSHOT_VERSION };
			foreach (KeyValuePair<string, Cached> pair in _packages)
			{
				SongPackageItem item = pair.Value.Item;
				snapshot.packages.Add(new Record
				{
					folder = pair.Key,
					manifest = item.Entry.Manifest,
					hasManifest = item.HasManifest,
					hasScore = item.HasScore,
					hasAudio = item.HasAudio,
					hasJacket = item.HasJacket,
					lastWriteTicks = item.LastWriteTime.Ticks,
					scoreKey = item.ScoreKey,
					scoreFileName = pair.Value.ScoreFileName,
					scoreStamp = pair.Value.ScoreStamp
				});
			}

			try
			{
				AtomicFile.WriteAllText(SnapshotPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
				string legacy = Path.Combine(_root, LEGACY_HASH_FILE_NAME);
				if (File.Exists(legacy))
				{
					File.Delete(legacy);
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogWarning("[Library] Could not save the library cache: " + exception.Message);
			}
		}

		private bool IsInsideRoot(string directory)
		{
			string parent = Path.GetDirectoryName(Trim(Path.GetFullPath(directory)));
			return string.Equals(parent, Trim(_root), StringComparison.OrdinalIgnoreCase);
		}

		private static string KeyOf(string directory)
		{
			return Path.GetFileName(Trim(Path.GetFullPath(directory)));
		}

		private static string Trim(string path)
		{
			return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		private static Dictionary<string, Cached> NewMap()
		{
			return new Dictionary<string, Cached>(StringComparer.OrdinalIgnoreCase);
		}

		private sealed class Cached
		{
			internal Cached(SongPackageItem item, FileStamp scoreStamp, string scoreFileName)
			{
				Item = item;
				ScoreStamp = scoreStamp;
				ScoreFileName = scoreFileName;
			}

			internal SongPackageItem Item { get; }
			internal FileStamp ScoreStamp { get; }
			internal string ScoreFileName { get; }
			internal string ScoreKey => Item.ScoreKey;
		}

		private sealed class Snapshot
		{
			public int version;
			public List<Record> packages = new List<Record>();
		}

		private sealed class Record
		{
			public string folder;
			public SongPackageManifest manifest;
			public bool hasManifest;
			public bool hasScore;
			public bool hasAudio;
			public bool hasJacket;
			public long lastWriteTicks;
			public string scoreKey;
			public string scoreFileName;
			public FileStamp scoreStamp;
		}
	}
}
