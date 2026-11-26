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
        internal CustomMusicScorePlayHistoryRecord Play;
        internal DanAttempt Passed;
        internal string RetryKey;

        internal static AchievementEvent FromPlay(CustomMusicScorePlayHistoryRecord play)
        {
            if (play.SchemaVersion != 3 || string.IsNullOrWhiteSpace(play.RecordId) || play.AutoCount != 0 ||
                (!string.IsNullOrEmpty(play.LivePlayMode) && play.LivePlayMode != nameof(Sekai.LivePlayMode.Free)) ||
                play.EndReason is not (PlayResultKind.Completed or PlayResultKind.Failed or PlayResultKind.Retired) ||
                !DateTimeOffset.TryParse(play.PlayedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                return null;
            return new AchievementEvent
            {
                Time = time, Id = play.RecordId, Play = play,
                Hits = (long)play.PerfectCount + play.GreatCount + play.GoodCount + play.BadCount
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
                Passed = index + 1 == attempt.Songs.Count && attempt.Outcome == DanOutcome.Passed ? attempt : null
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
        private readonly AchievementProgress[] _goals = AchievementGoals.All.Concat(AchievementGoals.Legacy)
            .Select(goal => new AchievementProgress(goal)).ToArray();
        private readonly Dictionary<string, List<string>> _playGoals = new(StringComparer.Ordinal);
        private readonly HashSet<(string, int)> _events = new();
        private readonly Dictionary<string, HashSet<string>> _charts = new();
        private readonly Dictionary<string, int> _retries = new();
        private readonly Dictionary<string, int> _encores = new();
        private readonly HashSet<string> _broken = new();
        private readonly HashSet<string> _windowKeys = new();
        private readonly Best30Index _ranking = new();
        private readonly List<CustomMusicScorePlayHistoryRecord> _recent = new();
        private static readonly string[] _prismOrder = { "expert", "hard", "easy", "normal", "master" };
        private long _hits, _rankingTotal;
        private int _days, _setlist, _review, _encore, _retry;
        private AchievementEvent _last;

        internal IReadOnlyList<AchievementProgress> Snapshot() => Array.AsReadOnly(_goals.Take(AchievementGoals.All.Count).ToArray());
        internal IReadOnlyList<AchievementProgress> LegacySnapshot() => Array.AsReadOnly(_goals.Skip(AchievementGoals.All.Count).ToArray());

        internal IReadOnlyList<string> ForPlay(string id) =>
            id != null && _playGoals.TryGetValue(id, out var ids) ? ids.AsReadOnly() : Array.Empty<string>();

        internal static IReadOnlyList<string> NewlyReached(IReadOnlyList<AchievementProgress> before,
            IReadOnlyList<AchievementProgress> after) => Array.AsReadOnly(after
            .Where((item, i) => item.ReachedAtUtc.HasValue && !before[i].ReachedAtUtc.HasValue)
            .Select(item => item.Id).ToArray());

        internal static AchievementIndex Build(IEnumerable<CustomMusicScorePlayHistoryRecord> plays, DanArchive dan, IEnumerable<AchievementEvent> retries = null)
        {
            var events = plays.Select(AchievementEvent.FromPlay).Where(e => e != null).ToList();
            if (dan != null)
                foreach (var attempt in dan.Attempts)
                    for (int i = 0; i < attempt.Songs.Count; i++)
                        events.Add(AchievementEvent.FromSong(attempt, i));
            if (retries != null) events.AddRange(retries);
            events.Sort();
            var index = new AchievementIndex();
            foreach (var contribution in events) index.TryAppend(contribution);
            return index;
        }

        internal bool TryAppend(AchievementEvent e)
        {
            if (e == null || _events.Contains((e.Id, e.SongNumber))) return true;
            if (_last != null && e.CompareTo(_last) < 0) return false;
            if (e.Play is { RetrySource: "pause" or "result" } retried && !string.IsNullOrEmpty(retried.RetrySourceId)
                && !string.IsNullOrEmpty(retried.ScoreKey))
                TryAppend(new AchievementEvent { Id = e.Id, Time = e.Time, SongNumber = -1, RetryKey = retried.ScoreKey });
            _events.Add((e.Id, e.SongNumber));
            _last = e;
            _hits += e.Hits;
            var play = e.Play;
            bool clear = IsClear(play);
            bool fc = clear && (play.IsFullCombo || play.IsAllPerfect);
            if (play != null)
            {
                _rankingTotal = _ranking.Evaluate(play, HistorySaveState.Saved).After.Total;
                if (_recent.Count == 12) _recent.RemoveAt(0);
                _recent.Add(play);
                if (clear) TrackClear(play, fc);
            }
            if (e.RetryKey != null) _retry = Math.Max(_retry, Increment(_retries, e.RetryKey));
            bool aureole = e.Passed != null && e.Passed.Songs.All(song => song.Score.totalComboCount > 0 &&
                song.Score.autoCount == 0 && 100L * song.Score.perfectCount >= 99L * song.Score.totalComboCount);
            bool coda = e.Passed?.Songs.Count >= 4 && e.Passed.Songs.All(song => song.Ranked == true && song.PlayLevel >= 32 &&
                RankingRules.Award(song.Kind, false, song.Score) == ClearAward.AllPerfect);
            int chain = ComboWindow(0, false);
            int prism = PrismWindow();
            for (int i = 0; i < _goals.Length; i++)
            {
                var progress = _goals[i];
                long value = Value(progress, e, clear, fc, chain, prism, aureole, coda);
                Update(i, value, e);
            }
            if (clear && Breaks(play) == 1) _broken.Add(play.ScoreKey);
            return true;
        }

        private static bool IsClear(CustomMusicScorePlayHistoryRecord p) =>
            p?.EndReason == PlayResultKind.Completed && p.Life > 0 && p.TotalComboCount > 0 &&
            (long)p.PerfectCount + p.GreatCount + Breaks(p) > 0 && !string.IsNullOrWhiteSpace(p.ScoreKey);

        private static long Breaks(CustomMusicScorePlayHistoryRecord p) => (long)p.GoodCount + p.BadCount + p.MissCount;

        private void TrackClear(CustomMusicScorePlayHistoryRecord p, bool fc)
        {
            if (p.Ranked && p.PlayLevel >= 33) _encore = Math.Max(_encore, Increment(_encores, p.ScoreKey));
            if (string.IsNullOrEmpty(p.GameDay)) return;
            var daily = Set("day:" + p.GameDay);
            if (daily.Add(p.ScoreKey) && daily.Count == 3) _days++;
            if (!p.Ranked) return;
            if (p.Rank is "S" or "A") _setlist = Math.Max(_setlist, Distinct("setlist:" + p.GameDay, p.ScoreKey));
            if (fc && p.Rank == "S") _review = Math.Max(_review, Distinct("review:" + p.ScoreKey, p.GameDay));
        }

        private HashSet<string> Set(string group)
        {
            if (!_charts.TryGetValue(group, out var set)) _charts.Add(group, set = new HashSet<string>(StringComparer.Ordinal));
            return set;
        }

        private int Distinct(string group, string value)
        {
            var set = Set(group);
            set.Add(value);
            return set.Count;
        }

        private static int Increment(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            return counts[key] = count + 1;
        }

        private int ComboWindow(int floor, bool rising)
        {
            _windowKeys.Clear();
            int count = 0;
            int next = int.MaxValue;
            for (int i = _recent.Count - 1; i >= 0; i--)
            {
                var p = _recent[i];
                if (!IsClear(p) || !p.Ranked || !(p.IsFullCombo || p.IsAllPerfect) || p.PlayLevel < floor) break;
                if (rising ? p.PlayLevel >= next : !_windowKeys.Add(p.ScoreKey)) break;
                next = p.PlayLevel;
                count++;
            }
            return count;
        }

        private int PrismWindow()
        {
            for (int length = Math.Min(_prismOrder.Length, _recent.Count); length > 0; length--)
            {
                _windowKeys.Clear();
                int i = 0;
                for (; i < length; i++)
                {
                    var p = _recent[_recent.Count - length + i];
                    if (!IsClear(p) || !p.Ranked || !p.IsAllPerfect || !_windowKeys.Add(p.ScoreKey) ||
                        !string.Equals(p.Difficulty, _prismOrder[i], StringComparison.OrdinalIgnoreCase)) break;
                }
                if (i == length) return length;
            }
            return 0;
        }

        private long Value(AchievementProgress progress, AchievementEvent e, bool clear, bool fc,
            int chain, int prism, bool aureole, bool coda)
        {
            var goal = progress.Goal;
            var p = e.Play;
            long current = progress.Current;
            if (goal.IsLegacy && p != null && p.Life > 0 && p.TotalComboCount > 0)
            {
                var award = p.Award();
                if (goal.Metric == AchievementMetric.AllPerfect) return award == ClearAward.AllPerfect ? 1 : current;
                if (goal.Metric == AchievementMetric.FullComboCharts)
                    return award is ClearAward.FullCombo or ClearAward.AllPerfect && p.PlayLevel >= goal.MinimumLevel &&
                        !string.IsNullOrWhiteSpace(p.ScoreKey) ? Distinct(goal.Id, p.ScoreKey) : current;
            }
            switch (goal.Metric)
            {
                case AchievementMetric.ManualHits: return _hits;
                case AchievementMetric.DanPass: return e.Passed != null ? 1 : current;
                case AchievementMetric.PassedCourses: return current + (e.Passed != null ? 1 : 0);
                case AchievementMetric.PerfectRateCourses: return current + (aureole ? 1 : 0);
                case AchievementMetric.Coda: return coda ? 1 : current;
                case AchievementMetric.ChartRetries: return _retry;
                case AchievementMetric.Best30: return _rankingTotal;
                case AchievementMetric.PracticeDays: return _days;
                case AchievementMetric.DailySetlist: return _setlist;
                case AchievementMetric.ReviewDays: return _review;
                case AchievementMetric.Encore: return _encore;
                case AchievementMetric.DistinctComboStreak: return progress.ReachedAtUtc.HasValue ? Math.Max(goal.Target, chain) : chain;
                case AchievementMetric.Prism: return progress.ReachedAtUtc.HasValue ? goal.Target : prism;
                case AchievementMetric.RisingLevels:
                    int stairs = ComboWindow(goal.MinimumLevel, true);
                    return progress.ReachedAtUtc.HasValue ? Math.Max(goal.Target, stairs) : stairs;
            }
            if (!clear || p.PlayLevel < goal.MinimumLevel || p.TotalComboCount < goal.MinimumKeys) return current;
            switch (goal.Metric)
            {
                case AchievementMetric.LongEnoughClears: return current + (p.MusicLengthMs >= 60000 ? 1 : 0);
                case AchievementMetric.ClearedCharts: return Distinct(goal.Series, p.ScoreKey);
                case AchievementMetric.MaximumCombo: return Math.Max(current, p.MaxCombo);
                case AchievementMetric.FullComboLevel: return p.Ranked && fc ? 1 : current;
                case AchievementMetric.AllPerfectLevel: return p.Ranked && p.IsAllPerfect ? 1 : current;
                case AchievementMetric.FullComboCharts: return p.Ranked && fc ? Distinct(goal.Id, p.ScoreKey) : current;
                case AchievementMetric.AllPerfect: return p.Ranked && p.IsAllPerfect ? Distinct(goal.Id, p.ScoreKey) : current;
                case AchievementMetric.PerfectRateClears:
                    return current + (p.Ranked && p.PerfectCount * 1000L >= (long)p.TotalComboCount * goal.Threshold ? 1 : 0);
                case AchievementMetric.NearMissClears:
                    return current + ((long)p.PerfectCount + p.GreatCount + Breaks(p) == p.TotalComboCount &&
                        (p.TotalComboCount - p.PerfectCount == 1 || Breaks(p) == 1) ? 1 : 0);
                case AchievementMetric.LongChart:
                    return p.Rank is "S" or "A" && p.MusicLengthMs >= goal.Threshold * 60000L ? 1 : current;
                case AchievementMetric.DenseChart:
                    return p.Ranked && p.Rank == "S" && p.MusicLengthMs > 0 &&
                        p.MusicLengthMs <= p.TotalComboCount * 1000L / goal.Threshold ? 1 : current;
                case AchievementMetric.PerfectFlicks:
                    return p.FlickJudgments >= goal.Threshold && p.PerfectFlicks == p.FlickJudgments ? 1 : current;
                case AchievementMetric.PerfectHoldCharts:
                    return p.HoldChains >= goal.Threshold && p.HoldPoints >= p.HoldChains && p.PerfectHoldPoints == p.HoldPoints
                        ? Distinct(goal.Id, p.ScoreKey) : current;
                case AchievementMetric.RepairedCharts: return fc && _broken.Contains(p.ScoreKey) ? Distinct(goal.Series, p.ScoreKey) : current;
                default: throw new ArgumentOutOfRangeException(nameof(goal));
            }
        }

        private void Update(int index, long value, AchievementEvent e)
        {
            var progress = _goals[index];
            if (progress.Current == value) return;
            bool wasReached = progress.ReachedAtUtc.HasValue;
            _goals[index] = progress = progress.WithValue(value, e.Time);
            if (wasReached || !progress.ReachedAtUtc.HasValue || e.SongNumber > 0 || index >= AchievementGoals.All.Count) return;
            if (!_playGoals.TryGetValue(e.Id, out var ids)) _playGoals.Add(e.Id, ids = new List<string>());
            ids.Add(progress.Id);
        }
    }
}
