using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.Library
{
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
        private readonly AchievementProgress[] _goals = AchievementGoals.All.Select(goal => new AchievementProgress(goal)).ToArray();
        private readonly Dictionary<int, HashSet<string>> _fullCombos = new(); // Distinct charts, by the lowest level that counts.
        private readonly Dictionary<string, List<string>> _playGoals = new(StringComparer.Ordinal);
        private long _hits;
        private AchievementEvent _last;

        internal IReadOnlyList<AchievementProgress> Snapshot() => Array.AsReadOnly((AchievementProgress[])_goals.Clone());

        internal IReadOnlyList<string> ForPlay(string id) =>
            id != null && _playGoals.TryGetValue(id, out var ids) ? ids.AsReadOnly() : Array.Empty<string>();

        internal static IReadOnlyList<string> NewlyReached(IReadOnlyList<AchievementProgress> before,
            IReadOnlyList<AchievementProgress> after) => Array.AsReadOnly(after
            .Where((item, i) => item.ReachedAtUtc.HasValue && !before[i].ReachedAtUtc.HasValue)
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
            _hits += e.Hits;
            for (int i = 0; i < _goals.Length; i++) Update(i, Value(_goals[i], e), e);
            return true;
        }

        private long Value(AchievementProgress goal, AchievementEvent e)
        {
            switch (goal.Metric)
            {
                case AchievementMetric.ManualHits: return _hits;
                case AchievementMetric.FullComboCharts: return FullCombos(goal.MinimumLevel, e);
                case AchievementMetric.AllPerfect: return e.AllPerfect ? 1 : goal.Current;
                case AchievementMetric.DanPass: return e.DanPass ? 1 : goal.Current;
                default: throw new ArgumentOutOfRangeException(nameof(goal));
            }
        }

        private long FullCombos(int minimumLevel, AchievementEvent e)
        {
            if (!_fullCombos.TryGetValue(minimumLevel, out var charts))
                _fullCombos.Add(minimumLevel, charts = new HashSet<string>(StringComparer.Ordinal));
            if (!string.IsNullOrWhiteSpace(e.FullComboChart) && e.Level >= minimumLevel) charts.Add(e.FullComboChart);
            return charts.Count;
        }

        private void Update(int index, long value, AchievementEvent e)
        {
            var progress = _goals[index];
            if (progress.Current == value) return;
            bool wasReached = progress.ReachedAtUtc.HasValue;
            _goals[index] = progress = progress.WithValue(value, e.Time);
            if (wasReached || !progress.ReachedAtUtc.HasValue || e.SongNumber != 0) return;
            if (!_playGoals.TryGetValue(e.Id, out var ids)) _playGoals.Add(e.Id, ids = new List<string>());
            ids.Add(progress.Id);
        }
    }
}
