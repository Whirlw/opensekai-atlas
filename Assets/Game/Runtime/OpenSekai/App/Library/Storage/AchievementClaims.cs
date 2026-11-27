using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OpenSekai.App.Library
{
    /// <summary>
    /// Permanent reward claims and the retry starts used to calculate progress.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class AchievementClaims
    {
        [JsonProperty("unlocked", Required = Required.Always)]
        private Dictionary<string, DateTimeOffset> _times = new(StringComparer.Ordinal);

        [JsonProperty("retryStarts")]
        private Dictionary<string, RetryStart> _retryStarts = new(StringComparer.Ordinal);

        internal sealed class RetryStart
        {
            [JsonProperty(Required = Required.Always)]
            public string ScoreKey;
            [JsonProperty(Required = Required.Always)]
            public string SourceId;
            [JsonProperty(Required = Required.Always)]
            public string Source;
            [JsonProperty(Required = Required.Always)]
            public DateTimeOffset Time;
        }

        internal long CoinRewards => _times.Keys.Concat(_times.Keys.SelectMany(Successors)).Distinct(StringComparer.Ordinal)
            .Sum(id => (long)(AchievementGoals.Find(id)?.CoinReward ?? 0));

        internal int Count => _times.Count;
        internal int RetryCount => _retryStarts.Count;
        internal IEnumerable<AchievementEvent> RetryEvents => _retryStarts.Select(pair => new AchievementEvent
        {
            Id = pair.Key, SongNumber = -1, RetryKey = pair.Value.ScoreKey, Time = pair.Value.Time
        });

        internal AchievementClaims WithRetry(string id, RetryStart start)
        {
            if (_retryStarts.ContainsKey(id)) return this;
            var next = Copy();
            next._retryStarts.Add(id, start);
            return next;
        }

        internal AchievementClaims AfterHistoryDeletion(CustomMusicScorePlayHistoryRecord deleted)
        {
            // A missing selection clears the journal. A selected legacy row can itself have no ID.
            if (deleted != null && (deleted.RecordId == null || !_retryStarts.ContainsKey(deleted.RecordId))) return this;
            var next = Copy();
            if (deleted == null) next._retryStarts.Clear();
            else next._retryStarts.Remove(deleted.RecordId);
            return next;
        }

        // Legacy keys keep their original medal slots and grant the corresponding modern goals.
        internal IReadOnlyList<AchievementProgress> Apply(RankingHistory history)
        {
            var legacy = LegacyRewards(history);
            var mapped = new Dictionary<string, AchievementProgress>(StringComparer.Ordinal);
            foreach (var old in legacy)
            {
                foreach (var id in Successors(old.Id))
                    if (!mapped.TryGetValue(id, out var prior) || prior.UnlockedAtUtc > old.UnlockedAtUtc) mapped[id] = old;
            }
            return Array.AsReadOnly(history.ReadAchievements().Select(goal =>
            {
                if (!mapped.TryGetValue(goal.Id, out var old)) return WithClaim(goal);
                var reached = goal.ReachedAtUtc.HasValue && goal.ReachedAtUtc < old.ReachedAtUtc ? goal.ReachedAtUtc : old.ReachedAtUtc;
                var supported = new AchievementProgress(goal.Goal, Math.Max(goal.Target, goal.Current), reached);
                return _times.TryGetValue(goal.Id, out var time) ? supported.WithClaim(time) : supported.WithClaim(old.UnlockedAtUtc);
            }).ToArray());
        }

        internal IReadOnlyList<string> WithoutUnlocks(IReadOnlyList<string> reached)
        {
            if (_times.Count == 0) return reached;
            var inherited = _times.Keys.SelectMany(Successors).ToHashSet();
            return Array.AsReadOnly(reached.Where(id => !_times.ContainsKey(id) && !inherited.Contains(id)).ToArray());
        }

        internal AchievementProgress[] LegacyRewards(RankingHistory history) => history.ReadLegacyAchievements()
            .Select(WithClaim).Where(goal => goal.State == AchievementState.Unlocked).ToArray();

        internal IReadOnlyList<AchievementProgress> Rewards(RankingHistory history)
        {
            var legacy = LegacyRewards(history);
            // The inherited HIT plate keeps its original appearance and slot key. A separately
            // claimed modern plate (for example in a merged backup) is also an owned reward.
            var inheritedHits = legacy.Where(row => row.Metric == AchievementMetric.ManualHits)
                .SelectMany(row => Successors(row.Id)).ToHashSet();
            return Apply(history).Where(row => !inheritedHits.Contains(row.Id) || _times.ContainsKey(row.Id))
                .Concat(legacy).ToArray();
        }

        private AchievementProgress WithClaim(AchievementProgress goal) =>
            goal.WithClaim(_times.TryGetValue(goal.Id, out var time) ? time : (DateTimeOffset?)null);

        private static IEnumerable<string> Successors(string id)
        {
            switch (id)
            {
                case "manual_hits_10000": yield return "HIT-01"; break;
                case "manual_hits_100000": yield return "HIT-02"; break;
                case "manual_hits_1000000": yield return "HIT-03"; break;
                case "fc_lv28_10": yield return "FC-02"; goto case "fc_lv25";
                case "fc_lv25": yield return "FC-01"; break;
                case "first_ap": yield return "AP-01"; break;
                case "first_dan_pass": yield return "DAN-01"; break;
            }
        }

        internal AchievementClaims With(string id, DateTimeOffset time)
        {
            var next = Copy();
            next._times[id] = time.ToUniversalTime();
            return next;
        }

        /// <summary>Adds what <paramref name="incoming"/> has unlocked; the local time stands where both do. Returns how many are new.</summary>
        internal int Merge(AchievementClaims incoming)
        {
            foreach (var pair in incoming._retryStarts) _retryStarts.TryAdd(pair.Key, pair.Value);
            return incoming._times.Count(pair => _times.TryAdd(pair.Key, pair.Value));
        }

        internal static AchievementClaims Decode(string json)
        {
            AchievementClaims claims;
            try { claims = ProfileBackupArchive.Parse(json).ToObject<AchievementClaims>(); }
            catch (JsonException e) { throw new InvalidDataException("Invalid achievement archive.", e); }
            if (claims._retryStarts == null || claims._retryStarts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.ScoreKey) || string.IsNullOrWhiteSpace(pair.Value.SourceId) ||
                pair.Value.Source is not ("pause" or "result") || pair.Value.Time == default))
                throw new InvalidDataException("Invalid retry start.");
            if (claims._times.Keys.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Invalid unlocked achievement.");
            return claims;
        }

        private AchievementClaims Copy() => new()
        {
            _times = new Dictionary<string, DateTimeOffset>(_times, StringComparer.Ordinal),
            _retryStarts = new Dictionary<string, RetryStart>(_retryStarts, StringComparer.Ordinal)
        };
    }
}
