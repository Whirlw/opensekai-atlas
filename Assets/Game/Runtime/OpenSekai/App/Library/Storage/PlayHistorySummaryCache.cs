using OpenSekai.Shared.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using OpenSekai.Infrastructure.Packages;
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
		private const int SNAPSHOT_VERSION = 2;

		private Dictionary<string, CustomMusicScorePlayHistorySummary> _summaries;
		private IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> _view;
		private string _path;
		private long _historyBytes;

		/// <summary>Where the snapshot for a history file lives: PlayHistory.jsonl → PlayHistory.summary.json.</summary>
		internal static string SnapshotPathOf(string historyPath)
		{
			return Path.ChangeExtension(historyPath, ".summary.json");
		}

		internal IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> Load(string historyPath)
		{
			if (_summaries != null && IsSameFile(historyPath))
			{
				return _view;
			}

			_historyBytes = HistoryBytes(historyPath);
			_summaries = ReadSnapshot(historyPath, _historyBytes) ?? Rebuild(historyPath, _historyBytes);
			_view = new ReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary>(_summaries);
			_path = historyPath;
			return _view;
		}

		/// <summary>Folds the whole history file again and replaces the snapshot: the refresh button's resync.</summary>
		internal IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> Reload(string historyPath)
		{
			_historyBytes = HistoryBytes(historyPath);
			_summaries = Rebuild(historyPath, _historyBytes);
			_view = new ReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary>(_summaries);
			_path = historyPath;
			return _view;
		}

		/// <summary>
		/// Appends one run to the history, folds it into its package's summary and saves the snapshot.
		/// Returns that package's summary after the fold.
		/// </summary>
		internal CustomMusicScorePlayHistorySummary Append(string historyPath, CustomMusicScorePlayHistoryRecord record)
		{
			Load(historyPath);
			if (_historyBytes != HistoryBytes(historyPath)) Reload(historyPath);
			using (var stream = new FileStream(historyPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
			{
				// An interrupted append can leave an incomplete JSON object without a final newline.
				// Keep the next completed record on a line the history reader can recover independently.
				bool needsSeparator = false;
				if (stream.Length > 0)
				{
					stream.Seek(-1, SeekOrigin.End);
					needsSeparator = stream.ReadByte() != '\n';
				}
				using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
				{
					if (needsSeparator) writer.WriteLine();
					writer.WriteLine(JsonConvert.SerializeObject(record));
				}
				_historyBytes = stream.Length;
			}
			CustomMusicScorePlayHistorySummary summary = CustomMusicScorePlayHistoryStorage.Fold(_summaries, record);
			WriteSnapshot(historyPath, _summaries, _historyBytes);
			return summary;
		}

		private bool IsSameFile(string path)
		{
			return string.Equals(_path, path, StringComparison.OrdinalIgnoreCase);
		}

		private static long HistoryBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0L;

		private static Dictionary<string, CustomMusicScorePlayHistorySummary> Rebuild(string historyPath, long historyBytes)
		{
			Dictionary<string, CustomMusicScorePlayHistorySummary> summaries = File.Exists(historyPath)
				? CustomMusicScorePlayHistoryStorage.Summarize(File.ReadLines(historyPath))
				: new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
			WriteSnapshot(historyPath, summaries, historyBytes);
			return summaries;
		}

		private static Dictionary<string, CustomMusicScorePlayHistorySummary> ReadSnapshot(string historyPath, long historyBytes)
		{
			string path = SnapshotPathOf(historyPath);
			if (!File.Exists(path))
			{
				return null;
			}

			try
			{
				Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(path));
				if (snapshot?.summaries == null || snapshot.version != SNAPSHOT_VERSION || snapshot.historyBytes != historyBytes)
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

					summaries[pair.Key] = new CustomMusicScorePlayHistorySummary(
						pair.Value.best, pair.Value.lastPlayedAtUtc ?? string.Empty, pair.Value.playCount);
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

		private static void WriteSnapshot(string historyPath, Dictionary<string, CustomMusicScorePlayHistorySummary> summaries, long historyBytes)
		{
			var snapshot = new Snapshot { version = SNAPSHOT_VERSION, historyBytes = historyBytes };
			foreach (KeyValuePair<string, CustomMusicScorePlayHistorySummary> pair in summaries)
			{
				snapshot.summaries[pair.Key] = new Entry
				{
					best = pair.Value.Best,
					lastPlayedAtUtc = pair.Value.LastPlayedAtUtc,
					playCount = pair.Value.PlayCount
				};
			}

			try
			{
				AtomicFile.WriteAllText(SnapshotPathOf(historyPath), JsonConvert.SerializeObject(snapshot));
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				// The history itself is already written; a rebuild recovers the snapshot.
				Debug.LogWarning("[Library] Could not save the play history summary: " + exception.Message);
			}
		}

		private sealed class Snapshot
		{
			public int version;
			public long historyBytes;
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
