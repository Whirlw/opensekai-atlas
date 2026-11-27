using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.Library
{
    // Calls from the live, page and file picker are on Unity's main thread.
    internal sealed class RankingHistory
    {
        private readonly string _path;
        private readonly PlayHistorySummaryCache _library;
        private Best30Index _board = new();
        private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CustomMusicScorePlayHistoryRecord> _pending = new(StringComparer.Ordinal);
        private long _playCoins;
        internal long PlayCoins
        {
            get { EnsureLoaded(); return _playCoins; }
        }

        private long _bytes = -1;
        private RankingSettlement _last;
        private List<CustomMusicScorePlayHistoryRecord> _timeline = new();
        internal Func<DanArchive> DanSource { private get; set; }
        internal Func<AchievementClaims> ClaimsSource { private get; set; }
        private AchievementIndex _achievements;
        internal event Action Appended;

        private AchievementIndex Achievements => _achievements ??= AchievementIndex.Build(_timeline, DanSource?.Invoke(), ClaimsSource?.Invoke()?.RetryEvents);

        internal IReadOnlyList<AchievementProgress> ReadAchievements()
        {
            EnsureLoaded();
            return Achievements.Snapshot();
        }

        internal IReadOnlyList<AchievementProgress> ReadLegacyAchievements()
        {
            EnsureLoaded();
            return Achievements.LegacySnapshot();
        }

        internal void InvalidateAchievements() => _achievements = null;

        internal void AppendAchievement(AchievementEvent contribution)
        {
            if (_achievements != null && !_achievements.TryAppend(contribution)) InvalidateAchievements();
        }

        private RankingSettlement WithReached(RankingSettlement receipt)
        {
            var ids = Achievements.ForPlay(receipt.PlayId);
            if (ids.Count > 0 && ClaimsSource != null) ids = ClaimsSource().WithoutUnlocks(ids);
            return ids.Count == 0 ? receipt : new RankingSettlement(receipt.PlayId, receipt.ScoreKey, receipt.Eligibility,
                receipt.SaveState, receipt.Award, receipt.BasePoints, receipt.Before, receipt.After, ids, receipt.CoinReward);
        }

        internal bool Contains(CustomMusicScorePlayHistoryRecord record)
        {
            EnsureLoaded();
            return _timeline.Contains(record);
        }

        internal HistoryPage ReadPage(int offset, int limit)
        {
            if (offset < 0 || limit <= 0) throw new ArgumentOutOfRangeException();
            EnsureLoaded();
            int start = Math.Min(offset, _timeline.Count);
            return new HistoryPage(_timeline.Count,
                _timeline.GetRange(start, Math.Min(limit, _timeline.Count - start)).AsReadOnly());
        }

        internal void TransferPendingTo(RankingHistory next, string deletedId = null)
        {
            foreach (var pair in _pending)
                if (pair.Key != deletedId) next._pending[pair.Key] = pair.Value;
        }

        internal RankingHistory(string path, PlayHistorySummaryCache library)
        {
            _path = path;
            _library = library;
        }

        internal Best30Snapshot Read()
        {
            EnsureLoaded();
            return _board.Snapshot();
        }

        internal RankingSettlement Submit(CustomMusicScorePlayHistoryRecord play, RankingEligibility? exclusion,
            out CustomMusicScorePlayHistorySummary summary)
        {
            summary = null;
            try
            {
                EnsureLoaded();
                if (exclusion.HasValue)
                    return _board.Evaluate(play, HistorySaveState.NotRecorded, exclusion);
                if (_ids.Contains(play.RecordId))
                {
                    var existing = _last?.PlayId == play.RecordId ? _last : Replay(play.RecordId);
                    _pending.Remove(play.RecordId);
                    return WithReached(existing);
                }

                // Load existing progress before the write; a failed append cannot award anything.
                _ = Achievements;
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                summary = _library.Append(_path, play);
                _bytes = new FileInfo(_path).Length;
                _ids.Add(play.RecordId);
                int index = _timeline.BinarySearch(play, Comparer<CustomMusicScorePlayHistoryRecord>.Create(CompareTime));
                _timeline.Insert(index < 0 ? ~index : index, play);
                _pending.Remove(play.RecordId);
                _last = _board.Evaluate(play, HistorySaveState.Saved);
                _playCoins += _last.CoinReward;
                AppendAchievement(AchievementEvent.FromPlay(play));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Ranking] " + e.Message);
                _bytes = -1; // Re-read first: an interrupted write might already contain the whole record.
                if (exclusion.HasValue) return _board.Evaluate(play, HistorySaveState.NotRecorded, exclusion);
                _pending[play.RecordId] = play;
                return _board.Evaluate(play, HistorySaveState.SaveFailed);
            }
            var saved = WithReached(_last);
            Appended?.Invoke();
            return saved;
        }

        internal RankingSettlement Retry(string playId, out CustomMusicScorePlayHistorySummary summary)
        {
            summary = null;
            if (_pending.TryGetValue(playId, out var play)) return Submit(play, null, out summary);
            EnsureLoaded();
            var receipt = _last?.PlayId == playId ? _last : _ids.Contains(playId) ? Replay(playId) : null;
            return receipt == null ? null : WithReached(receipt);
        }

        private void EnsureLoaded()
        {
            long bytes = File.Exists(_path) ? new FileInfo(_path).Length : 0;
            if (_bytes == bytes) return;
            var board = new Best30Index();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            RankingSettlement last = null;
            long playCoins = 0;
            var timeline = new List<CustomMusicScorePlayHistoryRecord>();
            foreach (var record in Records())
            {
                if (!string.IsNullOrWhiteSpace(record.RecordId) && !ids.Add(record.RecordId)) continue;
                timeline.Add(record);
                if (CanRank(record))
                {
                    last = board.Evaluate(record, HistorySaveState.Saved);
                    playCoins += last.CoinReward;
                }
            }
            timeline.Sort(CompareTime);
            _timeline = timeline;
            InvalidateAchievements();
            _board = board;
            _ids.Clear();
            _ids.UnionWith(ids);
            _last = last;
            _playCoins = playCoins;
            _bytes = bytes;
        }

        // An explicit retry of an older committed play returns its original before/after standings.
        private RankingSettlement Replay(string playId)
        {
            var board = new Best30Index();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in Records())
            {
                if (!ids.Add(record.RecordId)) continue;
                if (!CanRank(record) && record.RecordId != playId) continue;
                var receipt = board.Evaluate(record, HistorySaveState.Saved);
                if (record.RecordId == playId) return receipt;
            }
            return null;
        }

        private IEnumerable<CustomMusicScorePlayHistoryRecord> Records()
        {
            if (!File.Exists(_path)) yield break;
            int lineNumber = 0;
            foreach (string line in File.ReadLines(_path))
            {
                lineNumber++;
                CustomMusicScorePlayHistoryRecord record;
                try { record = JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(line); }
                catch (JsonException) { continue; } // A truncated final line cannot invalidate earlier plays.
                if (record == null) continue;
                record.SourceLine = lineNumber;
                yield return record;
            }
        }

        private static bool CanRank(CustomMusicScorePlayHistoryRecord record) =>
            record.SchemaVersion == 3 && !string.IsNullOrWhiteSpace(record.RecordId);

        private static int CompareTime(CustomMusicScorePlayHistoryRecord a, CustomMusicScorePlayHistoryRecord b)
        {
            int order = b.PlayedAtTicks.CompareTo(a.PlayedAtTicks);
            return order != 0 ? order : string.CompareOrdinal(a.RecordId, b.RecordId);
        }
    }
}
