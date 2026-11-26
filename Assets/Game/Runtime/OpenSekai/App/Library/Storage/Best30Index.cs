using System;
using System.Collections.Generic;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.Library
{
    public sealed class Best30Entry
    {
        public CustomMusicScorePlayHistoryRecord Play { get; }
        public long Points { get; }
        public ClearAward Award { get; }

        internal Best30Entry(CustomMusicScorePlayHistoryRecord play, long points, ClearAward award)
        {
            Play = play;
            Points = points;
            Award = award;
        }
    }

    public sealed class Best30Snapshot
    {
        public long Total { get; }
        public int TierId => RankingRules.Tier(Total);
        /// <summary>Descending contribution. Position is array index + 1.</summary>
        public IReadOnlyList<Best30Entry> Entries { get; }

        internal Best30Snapshot(long total, Best30Entry[] entries)
        {
            Total = total;
            Entries = Array.AsReadOnly(entries);
        }
    }

    internal sealed class Best30Index
    {
        private const int CAPACITY = 30;
        private static readonly IComparer<Best30Entry> _order = Comparer<Best30Entry>.Create(Compare);
        private readonly Dictionary<string, Best30Entry> _best = new(StringComparer.Ordinal);
        private readonly List<Best30Entry> _top = new(CAPACITY + 1);
        private long _total;

        internal Best30Snapshot Snapshot() => new(_total, _top.ToArray());

        internal RankingSettlement Evaluate(CustomMusicScorePlayHistoryRecord play, HistorySaveState state,
            RankingEligibility? exclusion = null, IReadOnlyList<string> unlocks = null)
        {
            var before = Standing(play.ScoreKey);
            var eligibility = exclusion ?? Eligibility(play);
            var award = eligibility == RankingEligibility.Auto ? ClearAward.None : play.Award();
            long points = 0;
            if (eligibility == RankingEligibility.Eligible)
            {
                points = RankingRules.BasePoints(play.PlayLevel, play.TotalScore, play.MaximumScore);
                if (state == HistorySaveState.Saved)
                    Consider(new Best30Entry(play, points + RankingRules.Bonus(award), award));
            }
            return new RankingSettlement(play.RecordId, play.ScoreKey, eligibility, state, award, points,
                before, Standing(play.ScoreKey), unlocks);
        }

        private static RankingEligibility Eligibility(CustomMusicScorePlayHistoryRecord play)
        {
            if (play.SchemaVersion != 3 || !RankingRules.SupportsFormula(play.FormulaVersion) ||
                play.MaximumScore <= 0 || !play.EndReason.HasValue)
                return RankingEligibility.UnknownRules;
            if (play.AutoCount != 0) return RankingEligibility.Auto;
            if (play.EndReason != PlayResultKind.Completed || play.Life <= 0) return RankingEligibility.Incomplete;
            if (string.IsNullOrWhiteSpace(play.ScoreKey)) return RankingEligibility.MissingChart;
            if (play.PlayLevel <= 0 || play.TotalComboCount <= 0 || play.TotalScore < 0)
                return RankingEligibility.InvalidChart;
            if (!play.Ranked) return RankingEligibility.Unranked;
            return RankingRules.ReachesRankC(play.TotalScore, play.MaximumScore)
                ? RankingEligibility.Eligible : RankingEligibility.BelowRankC;
        }

        private RankingStanding Standing(string key)
        {
            if (key == null || !_best.TryGetValue(key, out var entry)) return new RankingStanding(_total, null, null);
            int index = _top.IndexOf(entry);
            return new RankingStanding(_total, entry.Points, index < 0 ? (int?)null : index + 1);
        }

        private void Consider(Best30Entry entry)
        {
            if (_best.TryGetValue(entry.Play.ScoreKey, out var previous))
            {
                if (Compare(entry, previous) >= 0) return;
                if (_top.Remove(previous)) _total -= previous.Points;
            }
            _best[entry.Play.ScoreKey] = entry;
            int index = _top.BinarySearch(entry, _order);
            _top.Insert(index < 0 ? ~index : index, entry);
            _total += entry.Points;
            if (_top.Count > CAPACITY)
            {
                _total -= _top[CAPACITY].Points;
                _top.RemoveAt(CAPACITY);
            }
        }

        private static int Compare(Best30Entry a, Best30Entry b)
        {
            int order = b.Points.CompareTo(a.Points);
            if (order != 0) return order;
            order = a.Play.PlayedAtTicks.CompareTo(b.Play.PlayedAtTicks);
            return order != 0 ? order : string.CompareOrdinal(a.Play.RecordId, b.Play.RecordId);
        }
    }
}
