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
        private long _bytes = -1;
        private RankingSettlement _last;
        private List<CustomMusicScorePlayHistoryRecord> _timeline = new();

        internal HistoryPage ReadPage(int offset, int limit)
        {
            if (offset < 0 || limit <= 0) throw new ArgumentOutOfRangeException();
            EnsureLoaded();
            int start = Math.Min(offset, _timeline.Count);
            return new HistoryPage(_timeline.Count,
                _timeline.GetRange(start, Math.Min(limit, _timeline.Count - start)).AsReadOnly());
        }

        internal void TransferPendingTo(RankingHistory next)
        {
            foreach (var pair in _pending) next._pending[pair.Key] = pair.Value;
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
                    return existing;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                summary = _library.Append(_path, play);
                _bytes = new FileInfo(_path).Length;
                _ids.Add(play.RecordId);
                int index = _timeline.BinarySearch(play, Comparer<CustomMusicScorePlayHistoryRecord>.Create(CompareTime));
                _timeline.Insert(index < 0 ? ~index : index, play);
                _pending.Remove(play.RecordId);
                _last = _board.Evaluate(play, HistorySaveState.Saved);
                return _last;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Ranking] " + e.Message);
                _bytes = -1; // Re-read first: an interrupted write might already contain the whole record.
                if (exclusion.HasValue) return _board.Evaluate(play, HistorySaveState.NotRecorded, exclusion);
                _pending[play.RecordId] = play;
                return _board.Evaluate(play, HistorySaveState.SaveFailed);
            }
        }

        internal RankingSettlement Retry(string playId, out CustomMusicScorePlayHistorySummary summary)
        {
            summary = null;
            if (_pending.TryGetValue(playId, out var play)) return Submit(play, null, out summary);
            EnsureLoaded();
            return _last?.PlayId == playId ? _last : _ids.Contains(playId) ? Replay(playId) : null;
        }

        private void EnsureLoaded()
        {
            long bytes = File.Exists(_path) ? new FileInfo(_path).Length : 0;
            if (_bytes == bytes) return;
            var board = new Best30Index();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            RankingSettlement last = null;
            var timeline = new List<CustomMusicScorePlayHistoryRecord>();
            foreach (var record in Records())
            {
                if (!string.IsNullOrWhiteSpace(record.RecordId) && !ids.Add(record.RecordId)) continue;
                timeline.Add(record);
                if (CanRank(record)) last = board.Evaluate(record, HistorySaveState.Saved);
            }
            timeline.Sort(CompareTime);
            _timeline = timeline;
            _board = board;
            _ids.Clear();
            _ids.UnionWith(ids);
            _last = last;
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
            foreach (string line in File.ReadLines(_path))
            {
                CustomMusicScorePlayHistoryRecord record;
                try { record = JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(line); }
                catch (JsonException) { continue; } // A truncated final line cannot invalidate earlier plays.
                if (record != null) yield return record;
            }
        }

        private static bool CanRank(CustomMusicScorePlayHistoryRecord record) =>
            record.SchemaVersion == 3 && !string.IsNullOrWhiteSpace(record.RecordId);

        private static int CompareTime(CustomMusicScorePlayHistoryRecord a, CustomMusicScorePlayHistoryRecord b)
        {
            int order = string.CompareOrdinal(b.PlayedAtUtc, a.PlayedAtUtc);
            return order != 0 ? order : string.CompareOrdinal(a.RecordId, b.RecordId);
        }
    }
}
