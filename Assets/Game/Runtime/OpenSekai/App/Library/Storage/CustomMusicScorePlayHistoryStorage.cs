using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Sekai.Core.Live;
using UnityEngine;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using CriWare;
using Sekai;

namespace OpenSekai.App.Library
{
	public static class CustomMusicScorePlayHistoryStorage
	{
		private static readonly PlayHistorySummaryCache _cache = new PlayHistorySummaryCache();

		/// <summary>
		/// Raised after a run was appended, with its score key and that package's updated summary, so
		/// an open library page can redraw just that package instead of reloading the history.
		/// </summary>
		public static event Action<string, CustomMusicScorePlayHistorySummary> Recorded;

		public static void AppendLiveResult(FreeLiveBootData bootData, LiveScore score, long musicTimeMs, long musicLengthMs)
		{
			AppendLiveResult(bootData, score, musicTimeMs, musicLengthMs, null, null, 0);
		}

		/// <summary>
		/// Records one finished run. The extra arguments keep the same JSONL schema and only add
		/// fields, so older readers keep working: chartHash identifies the source chart content,
		/// rulesVersion marks the judgement/scoring rules, sessionId links to one live session.
		/// </summary>
		public static void AppendLiveResult(
			FreeLiveBootData bootData, LiveScore score, long musicTimeMs, long musicLengthMs,
			string sessionId, string chartHash, int rulesVersion)
		{
			if (bootData == null || bootData.IsAuto || bootData.IsCustomMusicScore != true || bootData.MusicData?.IsTestPlay == true)
			{
				return;
			}

			if (string.IsNullOrEmpty(bootData.CustomMusicScoreKey))
			{
				// Without the content hash the run could not be shown for any package.
				return;
			}

			CustomMusicScorePlayHistorySummary summary;
			string scoreKey;
			try
			{
				Directory.CreateDirectory(CustomMusicScoreStorage.RootDirectory);
				// The manifest as the library caches it; only a package outside the library is read from disk.
				SongPackage package = SongPackageManager.Shared.FindCachedItem(bootData.CustomMusicScorePath)?.Entry
					?? SongPackageStorage.LoadEntry(bootData.CustomMusicScorePath);
				CustomMusicScoreManifest manifest = CustomMusicScoreEntry.FromPackage(package)?.Manifest;
				CustomMusicScorePlayHistoryRecord record = CreateRecord(bootData, manifest, score, musicTimeMs, musicLengthMs,
					sessionId, chartHash, rulesVersion);
				scoreKey = record.ScoreKey;
				summary = _cache.Append(CustomMusicScoreStorage.PlayHistoryPath, record);
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("Failed to append custom music score play history. path:{0} error:{1}", CustomMusicScoreStorage.PlayHistoryPath, exception.Message);
				return;
			}

			if (summary != null)
			{
				Recorded?.Invoke(scoreKey, summary);
			}
		}

		/// <summary>
		/// One summary per score key (the content hash of the package's score file; records
		/// without one are ignored): the best run, the latest play time and the play count. Served
		/// from the cache, which a launch fills from the saved summary snapshot; new runs are folded in
		/// as they are appended. Queries expose a read-only view of the cached summaries.
		/// </summary>
		public static IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> LoadSummaries()
		{
			try
			{
				return _cache.Load(CustomMusicScoreStorage.PlayHistoryPath);
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("Failed to summarize custom music score play history. path:{0} error:{1}", CustomMusicScoreStorage.PlayHistoryPath, exception.Message);
				return new System.Collections.ObjectModel.ReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary>(
					new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal));
			}
		}

		/// <summary>Folds the whole history file again and saves a fresh snapshot: the refresh button's resync.</summary>
		public static IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> ReloadSummaries()
		{
			try
			{
				return _cache.Reload(CustomMusicScoreStorage.PlayHistoryPath);
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("Failed to reload custom music score play history. path:{0} error:{1}", CustomMusicScoreStorage.PlayHistoryPath, exception.Message);
				return LoadSummaries();
			}
		}

		internal static Dictionary<string, CustomMusicScorePlayHistorySummary> Summarize(IEnumerable<string> lines)
		{
			var summaries = new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
			foreach (string line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				CustomMusicScorePlayHistoryRecord record;
				try
				{
					record = JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(line);
				}
				catch (JsonException)
				{
					// One damaged line (a write cut short) must not hide every other record.
					continue;
				}

				Fold(summaries, record);
			}

			return summaries;
		}

		/// <summary>
		/// Adds one run to its package's summary: it becomes the best only when it beats the current
		/// best, the last-played time moves forward and the play count goes up. Null when the record
		/// has no score key.
		/// </summary>
		internal static CustomMusicScorePlayHistorySummary Fold(
			Dictionary<string, CustomMusicScorePlayHistorySummary> summaries, CustomMusicScorePlayHistoryRecord record)
		{
			if (record == null || string.IsNullOrEmpty(record.ScoreKey))
			{
				return null;
			}

			summaries.TryGetValue(record.ScoreKey, out var previous);
			var best = previous?.Best;
			string lastPlayed = previous?.LastPlayedAtUtc ?? string.Empty;
			var summary = new CustomMusicScorePlayHistorySummary(
				best == null || IsBetterResult(record, best) ? record : best,
				string.CompareOrdinal(record.PlayedAtUtc, lastPlayed) > 0 ? record.PlayedAtUtc : lastPlayed,
				(previous?.PlayCount ?? 0) + 1);
			summaries[record.ScoreKey] = summary;

			return summary;
		}

		private static bool IsBetterResult(CustomMusicScorePlayHistoryRecord current, CustomMusicScorePlayHistoryRecord best)
		{
			int comparison = current.TotalScore.CompareTo(best.TotalScore);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.MaxCombo.CompareTo(best.MaxCombo);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.PerfectCount.CompareTo(best.PerfectCount);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.MissCount.CompareTo(best.MissCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			comparison = current.BadCount.CompareTo(best.BadCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			comparison = current.GoodCount.CompareTo(best.GoodCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			return string.CompareOrdinal(current.PlayedAtUtc, best.PlayedAtUtc) > 0;
		}

		private static CustomMusicScorePlayHistoryRecord CreateRecord(FreeLiveBootData bootData, CustomMusicScoreManifest manifest, LiveScore score,
			long musicTimeMs, long musicLengthMs, string sessionId, string chartHash, int rulesVersion)
		{
			bool isAllPerfect = score.totalComboCount > 0
				&& score.totalComboCount == score.perfectCount
				&& score.greatCount == 0
				&& score.goodCount == 0
				&& score.badCount == 0
				&& score.missCount == 0;
			bool isFullCombo = score.totalComboCount > 0 && score.maxCombo >= score.totalComboCount;
			string clearType = isAllPerfect ? "all_perfect" : isFullCombo ? "full_combo" : score.life > 0 ? "clear" : "life_zero";

			return new CustomMusicScorePlayHistoryRecord
			{
				SchemaVersion = 2,
				SessionId = sessionId ?? string.Empty,
				ChartHash = chartHash ?? string.Empty,
				RulesVersion = rulesVersion,
				RecordId = Guid.NewGuid().ToString("N"),
				PlayedAtUtc = DateTime.UtcNow.ToString("O"),
				AppVersion = Application.version,
				LivePlayMode = bootData.LivePlayMode.ToString(),
				ScoreKey = bootData.CustomMusicScoreKey,
				CustomMusicScoreId = bootData.CustomMusicScoreId,
				CustomMusicScorePath = bootData.CustomMusicScorePath ?? string.Empty,
				Title = bootData.MusicData?.Music?.title ?? manifest?.title ?? string.Empty,
				ScoreTitle = bootData.CustomMusicScoreTitle ?? manifest?.scoreTitle ?? string.Empty,
				AuthorName = bootData.CustomMusicScoreAuthorName ?? manifest?.userName ?? string.Empty,
				Difficulty = bootData.MusicData?.DifficultyString ?? manifest?.musicDifficultyType ?? string.Empty,
				PlayLevel = bootData.MusicData?.PlayLevel ?? manifest?.playLevel ?? 0,
				ScoreFileName = manifest?.scoreFileName ?? string.Empty,
				AudioFileName = manifest?.audioFileName ?? string.Empty,
				JacketFileName = manifest?.jacketFileName ?? string.Empty,
				MusicTimeMs = musicTimeMs,
				MusicLengthMs = musicLengthMs,
				TotalScore = score.totalScore,
				TechnicalScore = score.technicalScore,
				Rank = score.rank.ToString(),
				Life = score.life,
				ClearType = clearType,
				IsFullCombo = isFullCombo,
				IsAllPerfect = isAllPerfect,
				MaxCombo = score.maxCombo,
				TotalComboCount = score.totalComboCount,
				JustPerfectCount = score.justPerfectCount,
				PerfectCount = score.perfectCount,
				GreatCount = score.greatCount,
				GoodCount = score.goodCount,
				BadCount = score.badCount,
				MissCount = score.missCount,
				FastCount = score.fastCount,
				LateCount = score.lateCount,
				FlickMissCount = score.flickCount,
				AutoCount = score.autoCount,
				NoteSpeed = bootData.LiveSettingData?.NoteSpeed ?? 0f,
				TimingAdjust = bootData.LiveSettingData?.TimingAdjustData ?? 0f,
				NoteSkinIndex = bootData.LiveSettingData?.NoteSkinIndex ?? 0
			};
		}
	}

	/// <summary>What the play history says about one package: its best run and how recently it was played.</summary>
	public sealed class CustomMusicScorePlayHistorySummary
	{
		internal CustomMusicScorePlayHistorySummary(CustomMusicScorePlayHistoryRecord best, string lastPlayed, int plays)
		{
			Best = best;
			LastPlayedAtUtc = lastPlayed;
			PlayCount = plays;
		}

		public CustomMusicScorePlayHistoryRecord Best { get; }

		/// <summary>ISO 8601 UTC time of the latest run; empty when no record carries a time.</summary>
		public string LastPlayedAtUtc { get; }

		public int PlayCount { get; }
	}

	public sealed class CustomMusicScorePlayHistoryRecord
	{
		[JsonProperty("schemaVersion")]
		public int SchemaVersion { get; init; }

		[JsonProperty("recordId")]
		public string RecordId { get; init; }

		/// <summary>Hash of the source chart file content; empty for records written before it existed.</summary>
		[JsonProperty("chartHash")]
		public string ChartHash { get; init; }

		/// <summary>Judgement/scoring rules that produced this record; 0 means legacy unknown.</summary>
		[JsonProperty("rulesVersion")]
		public int RulesVersion { get; init; }

		/// <summary>Live session that produced this record; empty for legacy records.</summary>
		[JsonProperty("sessionId")]
		public string SessionId { get; init; }

		/// <summary>
		/// Content hash of the package's score file when the run was played; the key
		/// the library groups runs under. Empty for records written before it existed (not shown).
		/// </summary>
		[JsonProperty("scoreKey")]
		public string ScoreKey { get; init; }

		[JsonProperty("playedAtUtc")]
		public string PlayedAtUtc { get; init; }

		[JsonProperty("appVersion")]
		public string AppVersion { get; init; }

		[JsonProperty("livePlayMode")]
		public string LivePlayMode { get; init; }

		[JsonProperty("customMusicScoreId")]
		public string CustomMusicScoreId { get; init; }

		[JsonProperty("customMusicScorePath")]
		public string CustomMusicScorePath { get; init; }

		[JsonProperty("title")]
		public string Title { get; init; }

		[JsonProperty("scoreTitle")]
		public string ScoreTitle { get; init; }

		[JsonProperty("authorName")]
		public string AuthorName { get; init; }

		[JsonProperty("difficulty")]
		public string Difficulty { get; init; }

		[JsonProperty("playLevel")]
		public int PlayLevel { get; init; }

		[JsonProperty("scoreFileName")]
		public string ScoreFileName { get; init; }

		[JsonProperty("audioFileName")]
		public string AudioFileName { get; init; }

		[JsonProperty("jacketFileName")]
		public string JacketFileName { get; init; }

		[JsonProperty("musicTimeMs")]
		public long MusicTimeMs { get; init; }

		[JsonProperty("musicLengthMs")]
		public long MusicLengthMs { get; init; }

		[JsonProperty("totalScore")]
		public int TotalScore { get; init; }

		[JsonProperty("technicalScore")]
		public int TechnicalScore { get; init; }

		[JsonProperty("rank")]
		public string Rank { get; init; }

		[JsonProperty("life")]
		public int Life { get; init; }

		[JsonProperty("clearType")]
		public string ClearType { get; init; }

		[JsonProperty("isFullCombo")]
		public bool IsFullCombo { get; init; }

		[JsonProperty("isAllPerfect")]
		public bool IsAllPerfect { get; init; }

		[JsonProperty("maxCombo")]
		public int MaxCombo { get; init; }

		[JsonProperty("totalComboCount")]
		public int TotalComboCount { get; init; }

		[JsonProperty("justPerfectCount")]
		public int JustPerfectCount { get; init; }

		[JsonProperty("perfectCount")]
		public int PerfectCount { get; init; }

		[JsonProperty("greatCount")]
		public int GreatCount { get; init; }

		[JsonProperty("goodCount")]
		public int GoodCount { get; init; }

		[JsonProperty("badCount")]
		public int BadCount { get; init; }

		[JsonProperty("missCount")]
		public int MissCount { get; init; }

		[JsonProperty("fastCount")]
		public int FastCount { get; init; }

		[JsonProperty("lateCount")]
		public int LateCount { get; init; }

		[JsonProperty("flickMissCount")]
		public int FlickMissCount { get; init; }

		[JsonProperty("autoCount")]
		public int AutoCount { get; init; }

		[JsonProperty("noteSpeed")]
		public float NoteSpeed { get; init; }

		[JsonProperty("timingAdjust")]
		public float TimingAdjust { get; init; }

		[JsonProperty("noteSkinIndex")]
		public int NoteSkinIndex { get; init; }
	}
}
