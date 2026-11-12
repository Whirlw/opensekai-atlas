using System;
using System.IO;
using Newtonsoft.Json;
using Sekai.Core.Live;
using UnityEngine;
using CriWare;
using Sekai;

namespace Sekai.MusicScoreMaker.Common
{
	public static class CustomMusicScorePlayHistoryStorage
	{
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

			if (string.IsNullOrEmpty(bootData.CustomMusicScoreId))
			{
				return;
			}

			try
			{
				Directory.CreateDirectory(CustomMusicScoreStorage.RootDirectory);
				CustomMusicScoreEntry entry = CustomMusicScoreStorage.LoadEntry(bootData.CustomMusicScorePath);
				CustomMusicScoreManifest manifest = entry?.Manifest;
				CustomMusicScorePlayHistoryRecord record = CreateRecord(bootData, manifest, score, musicTimeMs, musicLengthMs);
				record.sessionId = sessionId ?? string.Empty;
				record.chartHash = chartHash ?? string.Empty;
				record.rulesVersion = rulesVersion;
				File.AppendAllText(CustomMusicScoreStorage.PlayHistoryPath, JsonConvert.SerializeObject(record) + Environment.NewLine);
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("Failed to append custom music score play history. path:{0} error:{1}", CustomMusicScoreStorage.PlayHistoryPath, exception.Message);
			}
		}

		public static bool TryLoadBestResult(string customMusicScoreId, out CustomMusicScorePlayHistoryRecord bestRecord)
		{
			bestRecord = null;
			if (string.IsNullOrEmpty(customMusicScoreId) || !File.Exists(CustomMusicScoreStorage.PlayHistoryPath))
			{
				return false;
			}

			try
			{
				foreach (string line in File.ReadLines(CustomMusicScoreStorage.PlayHistoryPath))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}

					CustomMusicScorePlayHistoryRecord record = JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(line);
					if (record == null || !string.Equals(record.customMusicScoreId, customMusicScoreId, StringComparison.Ordinal))
					{
						continue;
					}

					if (bestRecord == null || IsBetterResult(record, bestRecord))
					{
						bestRecord = record;
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("Failed to load custom music score play history. path:{0} error:{1}", CustomMusicScoreStorage.PlayHistoryPath, exception.Message);
				bestRecord = null;
			}

			return bestRecord != null;
		}

		private static bool IsBetterResult(CustomMusicScorePlayHistoryRecord current, CustomMusicScorePlayHistoryRecord best)
		{
			int comparison = current.totalScore.CompareTo(best.totalScore);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.maxCombo.CompareTo(best.maxCombo);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.perfectCount.CompareTo(best.perfectCount);
			if (comparison != 0)
			{
				return comparison > 0;
			}

			comparison = current.missCount.CompareTo(best.missCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			comparison = current.badCount.CompareTo(best.badCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			comparison = current.goodCount.CompareTo(best.goodCount);
			if (comparison != 0)
			{
				return comparison < 0;
			}

			return string.CompareOrdinal(current.playedAtUtc, best.playedAtUtc) > 0;
		}

		private static CustomMusicScorePlayHistoryRecord CreateRecord(FreeLiveBootData bootData, CustomMusicScoreManifest manifest, LiveScore score, long musicTimeMs, long musicLengthMs)
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
				schemaVersion = 1,
				recordId = Guid.NewGuid().ToString("N"),
				playedAtUtc = DateTime.UtcNow.ToString("O"),
				appVersion = Application.version,
				livePlayMode = bootData.LivePlayMode.ToString(),
				customMusicScoreId = bootData.CustomMusicScoreId,
				customMusicScorePath = bootData.CustomMusicScorePath ?? string.Empty,
				title = bootData.MusicData?.Music?.title ?? manifest?.title ?? string.Empty,
				scoreTitle = bootData.CustomMusicScoreTitle ?? manifest?.scoreTitle ?? string.Empty,
				authorName = bootData.CustomMusicScoreAuthorName ?? manifest?.userName ?? string.Empty,
				difficulty = bootData.MusicData?.DifficultyString ?? manifest?.musicDifficultyType ?? string.Empty,
				playLevel = bootData.MusicData?.PlayLevel ?? manifest?.playLevel ?? 0,
				scoreFileName = manifest?.scoreFileName ?? string.Empty,
				audioFileName = manifest?.audioFileName ?? string.Empty,
				jacketFileName = manifest?.jacketFileName ?? string.Empty,
				musicTimeMs = musicTimeMs,
				musicLengthMs = musicLengthMs,
				totalScore = score.totalScore,
				technicalScore = score.technicalScore,
				rank = score.rank.ToString(),
				life = score.life,
				clearType = clearType,
				isFullCombo = isFullCombo,
				isAllPerfect = isAllPerfect,
				maxCombo = score.maxCombo,
				totalComboCount = score.totalComboCount,
				justPerfectCount = score.justPerfectCount,
				perfectCount = score.perfectCount,
				greatCount = score.greatCount,
				goodCount = score.goodCount,
				badCount = score.badCount,
				missCount = score.missCount,
				fastCount = score.fastCount,
				lateCount = score.lateCount,
				flickMissCount = score.flickCount,
				autoCount = score.autoCount,
				noteSpeed = bootData.LiveSettingData?.NoteSpeed ?? 0f,
				timingAdjust = bootData.LiveSettingData?.TimingAdjustData ?? 0f,
				noteSkinIndex = bootData.LiveSettingData?.NoteSkinIndex ?? 0
			};
		}
	}

	public sealed class CustomMusicScorePlayHistoryRecord
	{
				private int _schemaVersion;
		public int schemaVersion
		{
		    get => _schemaVersion;
		    set => _schemaVersion = value;
		}
				private string _recordId;
		public string recordId
		{
		    get => _recordId;
		    set => _recordId = value;
		}
		/// <summary>Hash of the source chart file content; empty for records written before it existed.</summary>
				private string _chartHash;
		public string chartHash
		{
		    get => _chartHash;
		    set => _chartHash = value;
		}
		/// <summary>Judgement/scoring rules that produced this record; 0 means legacy unknown.</summary>
				private int _rulesVersion;
		public int rulesVersion
		{
		    get => _rulesVersion;
		    set => _rulesVersion = value;
		}
		/// <summary>Live session that produced this record; empty for legacy records.</summary>
				private string _sessionId;
		public string sessionId
		{
		    get => _sessionId;
		    set => _sessionId = value;
		}
				private string _playedAtUtc;
		public string playedAtUtc
		{
		    get => _playedAtUtc;
		    set => _playedAtUtc = value;
		}
				private string _appVersion;
		public string appVersion
		{
		    get => _appVersion;
		    set => _appVersion = value;
		}
				private string _livePlayMode;
		public string livePlayMode
		{
		    get => _livePlayMode;
		    set => _livePlayMode = value;
		}
				private string _customMusicScoreId;
		public string customMusicScoreId
		{
		    get => _customMusicScoreId;
		    set => _customMusicScoreId = value;
		}
				private string _customMusicScorePath;
		public string customMusicScorePath
		{
		    get => _customMusicScorePath;
		    set => _customMusicScorePath = value;
		}
				private string _title;
		public string title
		{
		    get => _title;
		    set => _title = value;
		}
				private string _scoreTitle;
		public string scoreTitle
		{
		    get => _scoreTitle;
		    set => _scoreTitle = value;
		}
				private string _authorName;
		public string authorName
		{
		    get => _authorName;
		    set => _authorName = value;
		}
				private string _difficulty;
		public string difficulty
		{
		    get => _difficulty;
		    set => _difficulty = value;
		}
				private int _playLevel;
		public int playLevel
		{
		    get => _playLevel;
		    set => _playLevel = value;
		}
				private string _scoreFileName;
		public string scoreFileName
		{
		    get => _scoreFileName;
		    set => _scoreFileName = value;
		}
				private string _audioFileName;
		public string audioFileName
		{
		    get => _audioFileName;
		    set => _audioFileName = value;
		}
				private string _jacketFileName;
		public string jacketFileName
		{
		    get => _jacketFileName;
		    set => _jacketFileName = value;
		}
				private long _musicTimeMs;
		public long musicTimeMs
		{
		    get => _musicTimeMs;
		    set => _musicTimeMs = value;
		}
				private long _musicLengthMs;
		public long musicLengthMs
		{
		    get => _musicLengthMs;
		    set => _musicLengthMs = value;
		}
				private int _totalScore;
		public int totalScore
		{
		    get => _totalScore;
		    set => _totalScore = value;
		}
				private int _technicalScore;
		public int technicalScore
		{
		    get => _technicalScore;
		    set => _technicalScore = value;
		}
				private string _rank;
		public string rank
		{
		    get => _rank;
		    set => _rank = value;
		}
				private int _life;
		public int life
		{
		    get => _life;
		    set => _life = value;
		}
				private string _clearType;
		public string clearType
		{
		    get => _clearType;
		    set => _clearType = value;
		}
				private bool _isFullCombo;
		public bool isFullCombo
		{
		    get => _isFullCombo;
		    set => _isFullCombo = value;
		}
				private bool _isAllPerfect;
		public bool isAllPerfect
		{
		    get => _isAllPerfect;
		    set => _isAllPerfect = value;
		}
				private int _maxCombo;
		public int maxCombo
		{
		    get => _maxCombo;
		    set => _maxCombo = value;
		}
				private int _totalComboCount;
		public int totalComboCount
		{
		    get => _totalComboCount;
		    set => _totalComboCount = value;
		}
				private int _justPerfectCount;
		public int justPerfectCount
		{
		    get => _justPerfectCount;
		    set => _justPerfectCount = value;
		}
				private int _perfectCount;
		public int perfectCount
		{
		    get => _perfectCount;
		    set => _perfectCount = value;
		}
				private int _greatCount;
		public int greatCount
		{
		    get => _greatCount;
		    set => _greatCount = value;
		}
				private int _goodCount;
		public int goodCount
		{
		    get => _goodCount;
		    set => _goodCount = value;
		}
				private int _badCount;
		public int badCount
		{
		    get => _badCount;
		    set => _badCount = value;
		}
				private int _missCount;
		public int missCount
		{
		    get => _missCount;
		    set => _missCount = value;
		}
				private int _fastCount;
		public int fastCount
		{
		    get => _fastCount;
		    set => _fastCount = value;
		}
				private int _lateCount;
		public int lateCount
		{
		    get => _lateCount;
		    set => _lateCount = value;
		}
				private int _flickMissCount;
		public int flickMissCount
		{
		    get => _flickMissCount;
		    set => _flickMissCount = value;
		}
				private int _autoCount;
		public int autoCount
		{
		    get => _autoCount;
		    set => _autoCount = value;
		}
				private float _noteSpeed;
		public float noteSpeed
		{
		    get => _noteSpeed;
		    set => _noteSpeed = value;
		}
				private float _timingAdjust;
		public float timingAdjust
		{
		    get => _timingAdjust;
		    set => _timingAdjust = value;
		}
				private int _noteSkinIndex;
		public int noteSkinIndex
		{
		    get => _noteSkinIndex;
		    set => _noteSkinIndex = value;
		}
	}
}
