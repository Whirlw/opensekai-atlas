using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OpenSekai.App.Library
{
	/// <summary>
	/// The play-history summaries kept in memory for one history file. The file is read in full
	/// once; after that each appended run is folded into its own package's summary, so neither the
	/// library page nor a new run ever re-reads the whole history. The file's size and write time
	/// are remembered, and any change this cache did not make itself (another process, a restore)
	/// sends the next load back to the file.
	/// </summary>
	internal sealed class PlayHistorySummaryCache
	{
		private Dictionary<string, CustomMusicScorePlayHistorySummary> _summaries;
		private string _path;
		private Stamp _stamp;

		internal Dictionary<string, CustomMusicScorePlayHistorySummary> Load(string path)
		{
			Stamp current = Stamp.Of(path);
			if (_summaries != null && IsSameFile(path) && current.Equals(_stamp))
			{
				return _summaries;
			}

			_summaries = current.Exists
				? CustomMusicScorePlayHistoryStorage.Summarize(File.ReadLines(path))
				: new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
			_path = path;
			_stamp = current;
			return _summaries;
		}

		/// <summary>
		/// Appends one run to the file and folds it into the summaries, loading them first if this
		/// is the first use. Returns the run's package summary after the fold.
		/// </summary>
		internal CustomMusicScorePlayHistorySummary Append(string path, CustomMusicScorePlayHistoryRecord record)
		{
			// Bring the cache up to the file as it is before this line goes in, so the fold below
			// starts from the full history even when nothing has loaded it yet.
			Dictionary<string, CustomMusicScorePlayHistorySummary> summaries = Load(path);
			File.AppendAllText(path, JsonConvert.SerializeObject(record) + Environment.NewLine);
			CustomMusicScorePlayHistorySummary summary = CustomMusicScorePlayHistoryStorage.Fold(summaries, record);
			_stamp = Stamp.Of(path);
			return summary;
		}

		private bool IsSameFile(string path)
		{
			return string.Equals(_path, path, StringComparison.OrdinalIgnoreCase);
		}

		private readonly struct Stamp : IEquatable<Stamp>
		{
			private Stamp(bool exists, long length, long writeTicksUtc)
			{
				Exists = exists;
				Length = length;
				WriteTicksUtc = writeTicksUtc;
			}

			internal bool Exists { get; }
			private long Length { get; }
			private long WriteTicksUtc { get; }

			internal static Stamp Of(string path)
			{
				var info = new FileInfo(path);
				return info.Exists
					? new Stamp(true, info.Length, info.LastWriteTimeUtc.Ticks)
					: new Stamp(false, 0, 0);
			}

			public bool Equals(Stamp other)
			{
				return Exists == other.Exists && Length == other.Length && WriteTicksUtc == other.WriteTicksUtc;
			}
		}
	}
}
