using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenSekai.App.Library
{
	/// <summary>
	/// The play-history summaries (best run, last played, play count per score key), kept in memory
	/// and saved next to the history as one snapshot. Every read is served from it: a launch reads
	/// the snapshot, and the full history is only folded again when there is no usable snapshot or
	/// on <see cref="Reload"/> (the refresh button). Each appended run is folded into its own
	/// package's summary and the snapshot is saved right after.
	/// </summary>
	internal sealed class PlayHistorySummaryCache
	{
		private const int SNAPSHOT_VERSION = 1;

		private Dictionary<string, CustomMusicScorePlayHistorySummary> _summaries;
		private string _path;

		/// <summary>Where the snapshot for a history file lives: PlayHistory.jsonl → PlayHistory.summary.json.</summary>
		internal static string SnapshotPathOf(string historyPath)
		{
			return Path.ChangeExtension(historyPath, ".summary.json");
		}

		internal Dictionary<string, CustomMusicScorePlayHistorySummary> Load(string historyPath)
		{
			if (_summaries != null && IsSameFile(historyPath))
			{
				return _summaries;
			}

			_summaries = ReadSnapshot(historyPath) ?? Rebuild(historyPath);
			_path = historyPath;
			return _summaries;
		}

		/// <summary>Folds the whole history file again and replaces the snapshot: the refresh button's resync.</summary>
		internal Dictionary<string, CustomMusicScorePlayHistorySummary> Reload(string historyPath)
		{
			_summaries = Rebuild(historyPath);
			_path = historyPath;
			return _summaries;
		}

		/// <summary>
		/// Appends one run to the history, folds it into its package's summary and saves the snapshot.
		/// Returns that package's summary after the fold.
		/// </summary>
		internal CustomMusicScorePlayHistorySummary Append(string historyPath, CustomMusicScorePlayHistoryRecord record)
		{
			Dictionary<string, CustomMusicScorePlayHistorySummary> summaries = Load(historyPath);
			File.AppendAllText(historyPath, JsonConvert.SerializeObject(record) + Environment.NewLine);
			CustomMusicScorePlayHistorySummary summary = CustomMusicScorePlayHistoryStorage.Fold(summaries, record);
			WriteSnapshot(historyPath, summaries);
			return summary;
		}

		private bool IsSameFile(string path)
		{
			return string.Equals(_path, path, StringComparison.OrdinalIgnoreCase);
		}

		private static Dictionary<string, CustomMusicScorePlayHistorySummary> Rebuild(string historyPath)
		{
			Dictionary<string, CustomMusicScorePlayHistorySummary> summaries = File.Exists(historyPath)
				? CustomMusicScorePlayHistoryStorage.Summarize(File.ReadLines(historyPath))
				: new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
			WriteSnapshot(historyPath, summaries);
			return summaries;
		}

		private static Dictionary<string, CustomMusicScorePlayHistorySummary> ReadSnapshot(string historyPath)
		{
			string path = SnapshotPathOf(historyPath);
			if (!File.Exists(path))
			{
				return null;
			}

			try
			{
				Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(path));
				if (snapshot?.summaries == null || snapshot.version != SNAPSHOT_VERSION)
				{
					return null;
				}

				var summaries = new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
				foreach (KeyValuePair<string, Entry> pair in snapshot.summaries)
				{
					if (string.IsNullOrEmpty(pair.Key) || pair.Value?.best == null)
					{
						continue;
					}

					summaries[pair.Key] = new CustomMusicScorePlayHistorySummary
					{
						Best = pair.Value.best,
						LastPlayedAtUtc = pair.Value.lastPlayedAtUtc ?? string.Empty,
						PlayCount = pair.Value.playCount
					};
				}
				return summaries;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
				|| exception is JsonException)
			{
				// An unreadable snapshot only costs one fold of the history.
				Debug.LogWarning("[Library] Could not read the play history summary: " + exception.Message);
				return null;
			}
		}

		private static void WriteSnapshot(string historyPath, Dictionary<string, CustomMusicScorePlayHistorySummary> summaries)
		{
			var snapshot = new Snapshot { version = SNAPSHOT_VERSION };
			foreach (KeyValuePair<string, CustomMusicScorePlayHistorySummary> pair in summaries)
			{
				snapshot.summaries[pair.Key] = new Entry
				{
					best = pair.Value.Best,
					lastPlayedAtUtc = pair.Value.LastPlayedAtUtc,
					playCount = pair.Value.PlayCount
				};
			}

			string path = SnapshotPathOf(historyPath);
			string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}
				File.WriteAllText(tempPath, JsonConvert.SerializeObject(snapshot));
				if (File.Exists(path))
				{
					File.Replace(tempPath, path, null);
				}
				else
				{
					File.Move(tempPath, path);
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				// The history itself is already written; a rebuild recovers the snapshot.
				Debug.LogWarning("[Library] Could not save the play history summary: " + exception.Message);
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}

		private sealed class Snapshot
		{
			public int version;
			public Dictionary<string, Entry> summaries = new Dictionary<string, Entry>(StringComparer.Ordinal);
		}

		private sealed class Entry
		{
			public CustomMusicScorePlayHistoryRecord best;
			public string lastPlayedAtUtc;
			public int playCount;
		}
	}
}
