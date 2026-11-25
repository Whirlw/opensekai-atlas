using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OpenSekai.App.Library
{
    /// <summary>
    /// The goals the player unlocked and when, the one thing about achievements that is saved;
    /// progress follows from the records. A goal counts as unlocked only while it is also reached.
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

        internal AchievementClaims WithoutRetry(string id)
        {
            var next = Copy();
            if (id == null) next._retryStarts.Clear();
            else next._retryStarts.Remove(id);
            return next;
        }

        // Legacy keys stay intact for old medal slots and for backup merges. Only a supported,
        // previously claimed entitlement can grandfather a new goal.
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

        internal IReadOnlyList<string> WithoutLegacyUnlocks(IReadOnlyList<string> reached, RankingHistory history)
        {
            if (!AchievementGoals.Legacy.Any(goal => _times.ContainsKey(goal.Id))) return reached;
            var inherited = LegacyRewards(history).SelectMany(goal => Successors(goal.Id)).ToHashSet();
            return Array.AsReadOnly(reached.Where(id => !inherited.Contains(id)).ToArray());
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

        /// <summary>Removes claims that lost their supporting history. Unknown IDs survive.</summary>
        internal AchievementClaims WithoutUnreached(RankingHistory history)
        {
            var next = Copy();
            foreach (var goal in Apply(history).Concat(history.ReadLegacyAchievements()))
                if (!goal.ReachedAtUtc.HasValue) next._times.Remove(goal.Id);
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
