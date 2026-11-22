using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.Library
{
    public enum AchievementMetric { ManualHits, FullComboCharts, AllPerfect, DanPass }

    public sealed class AchievementProgress
    {
        public string Id { get; }
        public AchievementMetric Metric { get; }
        public int MinimumLevel { get; }
        public int Target { get; }
        public long Current { get; }
        public DateTimeOffset? UnlockedAtUtc { get; }

        internal AchievementProgress(string id, AchievementMetric metric, int minimumLevel, int target,
            long current = 0, DateTimeOffset? unlockedAtUtc = null)
        {
            Id = id;
            Metric = metric;
            MinimumLevel = minimumLevel;
            Target = target;
            Current = current;
            UnlockedAtUtc = unlockedAtUtc;
        }

        internal AchievementProgress WithValue(long current, DateTimeOffset time) =>
            new(Id, Metric, MinimumLevel, Target, current, UnlockedAtUtc ?? (current >= Target ? time : (DateTimeOffset?)null));
    }

    // One contribution per saved normal play or checkpointed course song. Course totals never add hits.
    internal sealed class AchievementEvent : IComparable<AchievementEvent>
    {
        internal DateTimeOffset Time;
        internal string Id;
        internal int SongNumber; // Zero for normal plays. Attempt IDs and play IDs are separate namespaces.
        internal long Hits;
        internal string FullComboChart;
        internal int Level;
        internal bool AllPerfect;
        internal bool DanPass;

        internal static AchievementEvent FromPlay(CustomMusicScorePlayHistoryRecord play)
        {
            if (play.SchemaVersion != 3 || string.IsNullOrWhiteSpace(play.RecordId) || play.AutoCount != 0 ||
                play.EndReason is not (PlayResultKind.Completed or PlayResultKind.Failed or PlayResultKind.Retired) ||
                !DateTimeOffset.TryParse(play.PlayedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                return null;
            var award = play.Life > 0 && play.TotalComboCount > 0 ? play.Award() : ClearAward.None;
            return new AchievementEvent
            {
                Time = time, Id = play.RecordId,
                Hits = (long)play.PerfectCount + play.GreatCount + play.GoodCount + play.BadCount,
                FullComboChart = award is ClearAward.FullCombo or ClearAward.AllPerfect ? play.ScoreKey : null,
                Level = play.PlayLevel, AllPerfect = award == ClearAward.AllPerfect
            };
        }

        internal static AchievementEvent FromSong(DanAttempt attempt, int index)
        {
            var song = attempt.Songs[index];
            var score = song.Score;
            return new AchievementEvent
            {
                Time = (song.FinishedUtc ?? attempt.StartedUtc).ToUniversalTime(),
                Id = attempt.Id, SongNumber = index + 1,
                Hits = (long)score.perfectCount + score.greatCount + score.goodCount + score.badCount,
                DanPass = index + 1 == attempt.Songs.Count && attempt.Outcome == DanOutcome.Passed
            };
        }

        public int CompareTo(AchievementEvent other)
        {
            int order = Time.CompareTo(other.Time);
            if (order == 0) order = string.CompareOrdinal(Id, other.Id);
            return order == 0 ? SongNumber.CompareTo(other.SongNumber) : order;
        }
    }

    internal sealed class AchievementIndex
    {
        private AchievementProgress _hits = new("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000);
        private AchievementProgress _fc25 = new("fc_lv25", AchievementMetric.FullComboCharts, 25, 1);
        private AchievementProgress _fc28 = new("fc_lv28_10", AchievementMetric.FullComboCharts, 28, 10);
        private AchievementProgress _ap = new("first_ap", AchievementMetric.AllPerfect, 0, 1);
        private AchievementProgress _dan = new("first_dan_pass", AchievementMetric.DanPass, 0, 1);
        private readonly HashSet<string> _charts25 = new(StringComparer.Ordinal);
        private readonly HashSet<string> _charts28 = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _playUnlocks = new(StringComparer.Ordinal);
        private AchievementEvent _last;

        internal IReadOnlyList<AchievementProgress> Snapshot() => Array.AsReadOnly(new[] { _hits, _fc25, _fc28, _ap, _dan });

        internal IReadOnlyList<string> ForPlay(string id) =>
            id != null && _playUnlocks.TryGetValue(id, out var ids) ? ids.AsReadOnly() : Array.Empty<string>();

        internal static IReadOnlyList<string> NewUnlocks(IReadOnlyList<AchievementProgress> before,
            IReadOnlyList<AchievementProgress> after) => Array.AsReadOnly(after
            .Where((item, i) => item.UnlockedAtUtc.HasValue && !before[i].UnlockedAtUtc.HasValue)
            .Select(item => item.Id).ToArray());

        internal static AchievementIndex Build(IEnumerable<CustomMusicScorePlayHistoryRecord> plays, DanArchive dan)
        {
            var events = plays.Select(AchievementEvent.FromPlay).Where(e => e != null).ToList();
            if (dan != null)
                foreach (var attempt in dan.Attempts)
                    for (int i = 0; i < attempt.Songs.Count; i++)
                        events.Add(AchievementEvent.FromSong(attempt, i));
            events.Sort();
            var index = new AchievementIndex();
            foreach (var contribution in events) index.TryAppend(contribution);
            return index;
        }

        // An earlier imported/restored event or a backwards clock requires chronological replay.
        internal bool TryAppend(AchievementEvent e)
        {
            if (e == null) return true;
            if (_last != null && e.CompareTo(_last) < 0) return false;
            _last = e;
            if (e.Hits > 0) Update(ref _hits, _hits.Current + e.Hits, e);
            if (!string.IsNullOrWhiteSpace(e.FullComboChart))
            {
                if (e.Level >= _fc25.MinimumLevel && _charts25.Add(e.FullComboChart)) Update(ref _fc25, _charts25.Count, e);
                if (e.Level >= _fc28.MinimumLevel && _charts28.Add(e.FullComboChart)) Update(ref _fc28, _charts28.Count, e);
            }
            if (e.AllPerfect) Update(ref _ap, 1, e);
            if (e.DanPass) Update(ref _dan, 1, e);
            return true;
        }

        private void Update(ref AchievementProgress progress, long value, AchievementEvent e)
        {
            if (progress.Current == value) return;
            bool wasUnlocked = progress.UnlockedAtUtc.HasValue;
            progress = progress.WithValue(value, e.Time);
            if (wasUnlocked || !progress.UnlockedAtUtc.HasValue || e.SongNumber != 0) return;
            if (!_playUnlocks.TryGetValue(e.Id, out var ids)) _playUnlocks.Add(e.Id, ids = new List<string>());
            ids.Add(progress.Id);
        }
    }
}
