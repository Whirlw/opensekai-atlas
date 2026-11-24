using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.App.Library
{
    public enum AchievementMetric { ManualHits, FullComboCharts, AllPerfect, DanPass }

    /// <summary>
    /// Where a goal stands. A reached goal waits as <see cref="Claimable"/> until the player unlocks
    /// it on the 成就 page; only then does it count and hand over what it gives.
    /// </summary>
    public enum AchievementState { InProgress, Claimable, Unlocked }

    /// <summary>One thing to reach and what it gives. Goals with the same <see cref="Series"/> are its levels, in order.</summary>
    public sealed class AchievementGoal
    {
        internal AchievementGoal(string id, AchievementMetric metric, int minimumLevel, int target, string series = null,
            string avatarId = null, bool givesMedal = true)
        {
            Id = id;
            Metric = metric;
            MinimumLevel = minimumLevel;
            Target = target;
            Series = series ?? id;
            AvatarId = avatarId;
            GivesMedal = givesMedal;
        }

        public string Id { get; }
        public AchievementMetric Metric { get; }
        public int MinimumLevel { get; }
        public int Target { get; }
        public string Series { get; }
        public bool GivesMedal { get; }

        /// <summary>The avatar it gives; null for none.</summary>
        public string AvatarId { get; }
    }

    /// <summary>
    /// Every goal, in the order the page lists them. Ids are saved with the unlocks and never
    /// reused. The levels of one series ask for more each time, so reaching one reaches those before it.
    /// </summary>
    internal static class AchievementGoals
    {
        private const string HITS = "manual_hits";
        private const string FULL_COMBO = "full_combo";

        private static readonly AchievementGoal[] _all =
        {
            new("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000, HITS),
            new("manual_hits_50000", AchievementMetric.ManualHits, 0, 50000, HITS),
            new("manual_hits_100000", AchievementMetric.ManualHits, 0, 100000, HITS, "beat"),
            new("manual_hits_500000", AchievementMetric.ManualHits, 0, 500000, HITS),
            new("manual_hits_1000000", AchievementMetric.ManualHits, 0, 1000000, HITS),
            new("fc_lv25", AchievementMetric.FullComboCharts, 25, 1, FULL_COMBO),
            new("fc_lv28_10", AchievementMetric.FullComboCharts, 28, 10, FULL_COMBO, "crest"),
            new("first_ap", AchievementMetric.AllPerfect, 0, 1, avatarId: "sparkle"),
            new("first_dan_pass", AchievementMetric.DanPass, 0, 1)
        };

        internal static IReadOnlyList<AchievementGoal> All { get; } = Array.AsReadOnly(_all);

        /// <summary>The goal that gives <paramref name="avatarId"/>; null for an avatar everyone has.</summary>
        internal static AchievementGoal ForAvatar(string avatarId) =>
            avatarId == null ? null : _all.FirstOrDefault(goal => goal.AvatarId == avatarId);
    }

    public sealed class AchievementProgress
    {
        private readonly DateTimeOffset? _claimedAtUtc;

        internal AchievementProgress(AchievementGoal goal, long current = 0, DateTimeOffset? reachedAtUtc = null,
            DateTimeOffset? claimedAtUtc = null)
        {
            Goal = goal;
            Current = current;
            ReachedAtUtc = reachedAtUtc;
            _claimedAtUtc = claimedAtUtc;
        }

        internal AchievementProgress(string id, AchievementMetric metric, int minimumLevel, int target, long current = 0,
            DateTimeOffset? reachedAtUtc = null, DateTimeOffset? claimedAtUtc = null)
            : this(new AchievementGoal(id, metric, minimumLevel, target), current, reachedAtUtc, claimedAtUtc)
        {
        }

        public AchievementGoal Goal { get; }
        public string Id => Goal.Id;
        public AchievementMetric Metric => Goal.Metric;
        public int MinimumLevel => Goal.MinimumLevel;
        public int Target => Goal.Target;
        public long Current { get; }

        /// <summary>When the play or course that first reached the goal ended; null while it is under way.</summary>
        public DateTimeOffset? ReachedAtUtc { get; }

        /// <summary>When the player unlocked it; null until they do, and again if the runs behind it are deleted.</summary>
        public DateTimeOffset? UnlockedAtUtc => ReachedAtUtc.HasValue ? _claimedAtUtc : null;

        public AchievementState State => !ReachedAtUtc.HasValue ? AchievementState.InProgress
            : _claimedAtUtc.HasValue ? AchievementState.Unlocked : AchievementState.Claimable;

        internal AchievementProgress WithValue(long current, DateTimeOffset time) =>
            new(Goal, current, ReachedAtUtc ?? (current >= Target ? time : (DateTimeOffset?)null), _claimedAtUtc);

        internal AchievementProgress WithClaim(DateTimeOffset? time) => new(Goal, Current, ReachedAtUtc, time);
    }

    /// <summary>The levels of one achievement, lowest first. Most have a single one.</summary>
    public sealed class AchievementSeries
    {
        internal AchievementSeries(IReadOnlyList<AchievementProgress> steps)
        {
            Steps = steps;
            while (UnlockedCount < steps.Count && steps[UnlockedCount].State == AchievementState.Unlocked) UnlockedCount++;
        }

        public string Id => Steps[0].Goal.Series;
        public IReadOnlyList<AchievementProgress> Steps { get; }

        /// <summary>How many levels are unlocked, counted from the lowest.</summary>
        public int UnlockedCount { get; }

        public bool IsComplete => UnlockedCount == Steps.Count;

        /// <summary>The level to work on (or to unlock); the last one once all are unlocked.</summary>
        public int CurrentStep => Math.Min(UnlockedCount, Steps.Count - 1);

        /// <summary>The levels the player can see: the unlocked ones and the next.</summary>
        public int OpenCount => Math.Min(UnlockedCount + 1, Steps.Count);

        public AchievementState State => IsComplete ? AchievementState.Unlocked : Steps[UnlockedCount].State;

        public int IndexOf(string id)
        {
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Id == id) return i;
            return -1;
        }

        /// <summary>The achievements of <paramref name="goals"/>, each with its levels, in the goals' order.</summary>
        public static IReadOnlyList<AchievementSeries> Group(IEnumerable<AchievementProgress> goals) => Array.AsReadOnly(goals
            .GroupBy(goal => goal.Goal.Series, StringComparer.Ordinal)
            .Select(steps => new AchievementSeries(Array.AsReadOnly(steps.ToArray()))).ToArray());

        /// <summary>What can be unlocked first, then what is under way, then what is unlocked through and through.</summary>
        public static IReadOnlyList<AchievementSeries> Sort(IEnumerable<AchievementSeries> series) => Array.AsReadOnly(series
            .OrderBy(item => item.State == AchievementState.Claimable ? 0 : item.State == AchievementState.InProgress ? 1 : 2)
            .ToArray());
    }
}
