using System;
using System.Collections.Generic;
using Sekai.Core.Live;
using OpenSekai.Shared.Scoring;

namespace OpenSekai.Shared.Playback
{
    public enum ClearAward { None, Clear, FullCombo, AllPerfect }
    public enum RankingEligibility { Eligible, Auto, Incomplete, TestPlay, MissingChart, UnknownRules, InvalidChart, LoadError, Unranked, BelowRankC }
    public enum HistorySaveState { NotRecorded, Saved, SaveFailed }

    public static class RankingRules
    {
        public const int FORMULA_VERSION = 2;
        public const int MAXIMUM_SCORE = ScoreGauge.MAXIMUM_SCORE;
        private const int POINTS_PER_LEVEL = 10;
        private static readonly int[] _tierStarts = { 0, 7000, 8200, 9200, 10000, 10500, 11000 };

        /// <summary>Returns the highest tier reached, or -1 below the first threshold.</summary>
        public static int Tier(long total) => Array.FindLastIndex(_tierStarts, threshold => total >= threshold);

        /// <summary>Returns the next tier's starting total, or null at the highest tier.</summary>
        public static int? NextTierThreshold(long total)
        {
            int nextTier = Tier(total) + 1;
            return nextTier < _tierStarts.Length ? _tierStarts[nextTier] : null;
        }

        // Both history versions contain the raw inputs needed to rebuild Best 30.
        public static bool SupportsFormula(int version) => version == 1 || version == FORMULA_VERSION;

        public static bool ReachesRankC(int score, int maximum) =>
            maximum > 0 && (long)score * MAXIMUM_SCORE >= (long)maximum * ScoreGauge.SCORE_C;

        public static long BasePoints(int level, int score, int maximum)
        {
            if (level <= 0 || maximum <= 0) throw new ArgumentOutOfRangeException();
            if (!ReachesRankC(score, maximum)) return 0;

            decimal normalized = (decimal)Math.Min(score, maximum) * MAXIMUM_SCORE / maximum;
            decimal effectiveLevel = level + Math.Max(0, level - 33) / 2m;
            decimal Segment(int fromScore, int toScore, int levelsBelowFull)
            {
                decimal fromLevel = Math.Max(0m, effectiveLevel - levelsBelowFull);
                decimal toLevel = Math.Max(0m, effectiveLevel - levelsBelowFull + 2);
                return fromLevel + (toLevel - fromLevel) * (normalized - fromScore) / (toScore - fromScore);
            }

            decimal resultLevel;
            if (normalized < ScoreGauge.SCORE_B) resultLevel = Segment(ScoreGauge.SCORE_C, ScoreGauge.SCORE_B, 8);
            else if (normalized < ScoreGauge.SCORE_A) resultLevel = Segment(ScoreGauge.SCORE_B, ScoreGauge.SCORE_A, 6);
            else if (normalized < ScoreGauge.SCORE_S) resultLevel = Segment(ScoreGauge.SCORE_A, ScoreGauge.SCORE_S, 4);
            else resultLevel = Segment(ScoreGauge.SCORE_S, MAXIMUM_SCORE, 2);
            return (long)Math.Round(resultLevel * POINTS_PER_LEVEL, MidpointRounding.AwayFromZero);
        }

        public static int Bonus(ClearAward award) =>
            award == ClearAward.AllPerfect ? POINTS_PER_LEVEL : award == ClearAward.FullCombo ? POINTS_PER_LEVEL / 2 : 0;

        public static ClearAward Award(PlayResultKind kind, bool auto, LiveScore score)
        {
            if (auto || kind != PlayResultKind.Completed || score.totalComboCount <= 0 || score.autoCount != 0)
                return ClearAward.None;
            if (score.goodCount == 0 && score.badCount == 0 && score.missCount == 0)
            {
                if (score.perfectCount == score.totalComboCount && score.greatCount == 0)
                    return ClearAward.AllPerfect;
                if (score.maxCombo == score.totalComboCount) return ClearAward.FullCombo;
            }
            return ClearAward.Clear;
        }
    }

    /// <summary>Chart best and personal ranking immediately before or after a play. Positions are one-based.</summary>
    public readonly struct RankingStanding
    {
        public long Total { get; }
        public int TierId => RankingRules.Tier(Total);
        public long? ChartBest { get; }
        public int? ChartPosition { get; }
        public RankingStanding(long total, long? chartBest, int? chartPosition)
        {
            Total = total;
            ChartBest = chartBest;
            ChartPosition = chartPosition;
        }
    }

    public sealed class RankingSettlement
    {
        public string PlayId { get; }
        public string ScoreKey { get; }
        public RankingEligibility Eligibility { get; }
        public HistorySaveState SaveState { get; }
        public ClearAward Award { get; }
        public long BasePoints { get; }
        public int Bonus => Eligibility == RankingEligibility.Eligible ? RankingRules.Bonus(Award) : 0;
        public long Points => BasePoints + Bonus;
        public RankingStanding Before { get; }
        public RankingStanding After { get; }
        public long Delta => After.Total - Before.Total;
        public bool TierUp => After.TierId > Before.TierId;
        public bool ChartImproved => After.ChartBest.HasValue &&
            (!Before.ChartBest.HasValue || After.ChartBest.Value > Before.ChartBest.Value);
        public IReadOnlyList<string> UnlockedAchievementIds { get; }

        public RankingSettlement(string playId, string scoreKey, RankingEligibility eligibility,
            HistorySaveState saveState, ClearAward award, long basePoints, RankingStanding before, RankingStanding after,
            IReadOnlyList<string> unlockedAchievementIds = null)
        {
            PlayId = playId;
            ScoreKey = scoreKey;
            Eligibility = eligibility;
            SaveState = saveState;
            Award = award;
            BasePoints = basePoints;
            Before = before;
            After = after;
            UnlockedAchievementIds = unlockedAchievementIds == null || unlockedAchievementIds.Count == 0
                ? Array.Empty<string>() : new List<string>(unlockedAchievementIds).AsReadOnly();
        }
    }
}
