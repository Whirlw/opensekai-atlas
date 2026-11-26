using System.Globalization;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    /// <summary>The words and numbers of the result page, kept apart from its layout.</summary>
    internal static class ResultCopy
    {
        internal const string NEW_RECORD = "新纪录";
        internal const string TIER_UP = "评级提升";
        internal const string NOT_SAVED = "成绩未保存";

        internal static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        internal static string Signed(long value) => (value > 0 ? "+" : string.Empty) + Number(value);

        /// <summary>The big mark over the score. All perfect wins over full combo; empty for a load error.</summary>
        internal static string Award(PlayResult result)
        {
            if (!result.HasScore) return string.Empty;
            if (result.IsAuto) return "AUTO";
            switch (result.Award)
            {
                case ClearAward.AllPerfect: return "ALL PERFECT";
                case ClearAward.FullCombo: return "FULL COMBO";
            }

            switch (result.Kind)
            {
                case PlayResultKind.Completed: return "CLEAR";
                case PlayResultKind.Retired: return "RETIRED";
                default: return "FAILED";
            }
        }

        internal static string Mode(PlayResult result) => result.IsAuto ? "自动演奏" : "手动演奏";

        /// <summary>Why this run adds nothing to the Ranking Score; null when it takes part.</summary>
        internal static string Exclusion(PlayResult result, RankingSettlement ranking)
        {
            switch (ranking.Eligibility)
            {
                case RankingEligibility.Eligible: return null;
                case RankingEligibility.Auto: return "自动演奏不计入 Ranking Score";
                case RankingEligibility.Incomplete:
                    return result.Kind == PlayResultKind.Retired ? "中途退出，本局不计入 Ranking Score" : "未能完成，本局不计入 Ranking Score";
                case RankingEligibility.TestPlay: return "测试游玩不计入 Ranking Score";
                case RankingEligibility.MissingChart: return "谱面缺少标识，本局无法计入";
                case RankingEligibility.UnknownRules: return "计分规则无法识别，本局无法计入";
                case RankingEligibility.InvalidChart: return "谱面难度或满分无效，本局无法计入";
                case RankingEligibility.BelowRankC: return "未达到 C 评级，本局不计入 Ranking Score";
                case RankingEligibility.Unranked: return "谱面未确认 Ranked，本局不计入 Ranking Score";
                case RankingEligibility.LoadError: return "曲包没能载入，本局没有开始";
                default: return "本局不计入 Ranking Score";
            }
        }

        /// <summary>The line under the total once the run is in: what it did to the Ranking Score.</summary>
        internal static string Standing(RankingSettlement ranking)
        {
            if (ranking.Delta > 0) return Number(ranking.Before.Total) + "  →  " + Number(ranking.After.Total);
            if (ranking.ChartImproved && !ranking.After.ChartPosition.HasValue) return "谱面新纪录在 Best 30 之外，总分持平";
            if (!ranking.ChartImproved && ranking.Before.ChartBest.HasValue) return "未超过该谱最佳，总分持平";
            return "总分持平";
        }

        /// <summary>Base points, then the full combo or all perfect bonus when there is one.</summary>
        internal static string Breakdown(RankingSettlement ranking)
        {
            if (ranking.Eligibility != RankingEligibility.Eligible) return "本局不计分";
            string text = "基础 " + Number(ranking.BasePoints);
            if (ranking.Bonus <= 0) return text;
            string award = ranking.Award == ClearAward.AllPerfect ? "AP" : "FC";
            return text + "   " + award + " 奖励 +" + Number(ranking.Bonus);
        }

        /// <summary>This chart's best points after the run, with where it came from when it moved.</summary>
        internal static string ChartBest(RankingSettlement ranking)
        {
            long? after = ranking.After.ChartBest;
            if (!after.HasValue) return "暂无记录";
            long? before = ranking.Before.ChartBest;
            return ranking.ChartImproved && before.HasValue
                ? Number(before.Value) + "  →  " + Number(after.Value)
                : Number(after.Value);
        }

        /// <summary>This chart's place in the Best 30 after the run.</summary>
        internal static string Position(RankingSettlement ranking)
        {
            int? after = ranking.After.ChartPosition;
            if (!after.HasValue) return ranking.After.ChartBest.HasValue ? "榜外" : "—";
            int? before = ranking.Before.ChartPosition;
            return before.HasValue && before.Value != after.Value
                ? "第 " + before.Value + " → " + after.Value + " 名"
                : "第 " + after.Value + " 名";
        }
    }
}
