using System;
using System.Collections.Generic;
using Sekai.Core.Live;

namespace OpenSekai.Shared.Playback
{
    public enum ClearAward { None, Clear, FullCombo, AllPerfect }
    public enum RankingEligibility { Eligible, Auto, Incomplete, TestPlay, MissingChart, UnknownRules, InvalidChart, LoadError }
    public enum HistorySaveState { NotRecorded, Saved, SaveFailed }

    public static class RankingRules
    {
        public const int FormulaVersion = 1;
        public const int MaximumScore = 1_000_000;
        private static readonly int[] TierStarts = { 0, 3000, 6000, 8400, 9300 };

        public static int Tier(long total) => Array.FindLastIndex(TierStarts, threshold => total >= threshold);

        public static long BasePoints(int level, int score, int maximum)
        {
            if (level <= 0 || maximum <= 0) throw new ArgumentOutOfRangeException();
            decimal earned = Math.Min(Math.Max(score, 0), maximum);
            decimal rate = earned / maximum;
            return (long)Math.Round(10m * level * rate * rate, MidpointRounding.AwayFromZero);
        }

        public static int Bonus(ClearAward award) =>
            award == ClearAward.AllPerfect ? 10 : award == ClearAward.FullCombo ? 5 : 0;

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
        public IReadOnlyList<string> UnlockedAchievementIds => Array.Empty<string>();

        public RankingSettlement(string playId, string scoreKey, RankingEligibility eligibility,
            HistorySaveState saveState, ClearAward award, long basePoints, RankingStanding before, RankingStanding after)
        {
            PlayId = playId;
            ScoreKey = scoreKey;
            Eligibility = eligibility;
            SaveState = saveState;
            Award = award;
            BasePoints = basePoints;
            Before = before;
            After = after;
        }
    }
}
