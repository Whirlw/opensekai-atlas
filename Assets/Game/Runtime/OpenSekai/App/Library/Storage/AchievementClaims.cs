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

        internal int Count => _times.Count;

        internal bool Contains(string id) => _times.ContainsKey(id);

        internal IReadOnlyList<AchievementProgress> Apply(IReadOnlyList<AchievementProgress> goals) => Array.AsReadOnly(goals
            .Select(goal => goal.WithClaim(_times.TryGetValue(goal.Id, out var time) ? time : (DateTimeOffset?)null)).ToArray());

        internal AchievementClaims With(string id, DateTimeOffset time)
        {
            var next = Copy();
            next._times[id] = time.ToUniversalTime();
            return next;
        }

        /// <summary>Without the goals in <paramref name="goals"/> that are not reached. Ids of other versions stay.</summary>
        internal AchievementClaims WithoutUnreached(IEnumerable<AchievementProgress> goals)
        {
            var next = Copy();
            foreach (var goal in goals)
                if (!goal.ReachedAtUtc.HasValue) next._times.Remove(goal.Id);
            return next;
        }

        /// <summary>Adds what <paramref name="incoming"/> has unlocked; the local time stands where both do. Returns how many are new.</summary>
        internal int Merge(AchievementClaims incoming) => incoming._times.Count(pair => _times.TryAdd(pair.Key, pair.Value));

        internal static AchievementClaims Decode(string json)
        {
            AchievementClaims claims;
            try { claims = ProfileBackupArchive.Parse(json).ToObject<AchievementClaims>(); }
            catch (JsonException e) { throw new InvalidDataException("Invalid achievement archive.", e); }
            if (claims._times.Keys.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Invalid unlocked achievement.");
            return claims;
        }

        private AchievementClaims Copy() => new() { _times = new Dictionary<string, DateTimeOffset>(_times, StringComparer.Ordinal) };
    }
}
