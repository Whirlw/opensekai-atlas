using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.App.Library
{
    public enum AchievementCategoryGroup { Practice, Technique, Consistency, Course, Special, Legacy }

    public enum AchievementMetric { ManualHits, FullComboCharts, AllPerfect, DanPass, LongEnoughClears, ClearedCharts, PracticeDays, ChartRetries, MaximumCombo, PerfectRateClears, NearMissClears, LongChart, DenseChart, PerfectFlicks, PerfectHoldCharts, FullComboLevel, AllPerfectLevel, DailySetlist, DistinctComboStreak, ReviewDays, RisingLevels, RepairedCharts, Best30, PassedCourses, PerfectRateCourses, Prism, Encore, Coda }

    /// <summary>
    /// Where a goal stands. A reached goal waits as <see cref="Claimable"/> until the player unlocks
    /// it on the 成就 page; only then does it count and hand over what it gives.
    /// </summary>
    public enum AchievementState { InProgress, Claimable, Unlocked }

    /// <summary>One thing to reach and what it gives. Goals with the same <see cref="Series"/> are its levels, in order.</summary>
    public sealed class AchievementGoal
    {
        internal AchievementGoal(string id, AchievementMetric metric, int minimumLevel, int target, string series = null,
            string avatarId = null, bool givesMedal = true, string legacyMedal = null, int legacyTier = 0)
        {
            Id = id;
            Metric = metric;
            MinimumLevel = minimumLevel;
            Target = target;
            Series = series ?? id;
            AvatarId = avatarId;
            GivesMedal = givesMedal;
            IsLegacy = legacyMedal != null;
            Title = MedalName = legacyMedal;
            MedalTier = legacyTier;
        }

        internal AchievementGoal(string id, AchievementMetric metric, int target, string title, string description,
            int minimumLevel = 0, int minimumKeys = 0, int threshold = 0, string medalName = null, int tier = 0,
            string grade = null, string avatarId = null, string avatarName = null)
            : this(id, metric, minimumLevel, target, id.Split('-')[0], avatarId, medalName != null)
        {
            Title = title;
            Description = description;
            MinimumKeys = minimumKeys;
            Threshold = threshold;
            MedalName = medalName;
            MedalTier = tier;
            MedalGrade = grade;
            AvatarName = avatarName;
        }

        public bool IsLegacy { get; }
        public string Title { get; }
        public string Description { get; }
        public string MedalName { get; }
        public int MedalTier { get; }
        public string MedalGrade { get; }
        public int MedalFrameTier => Id == "CODA-01" ? MedalTiers.TOP : MedalTier;
        public string MedalFaceId => MedalName == null ? null : Id;
        public string MedalDisplayName => MedalGrade == null ? MedalName : MedalName + " " + MedalGrade;
        public string AvatarName { get; }
        public AchievementCategoryGroup Category => Series switch
        {
            "HIT" or "SET" or "MAP" or "DAY" or "RETRY" => AchievementCategoryGroup.Practice,
            "SETLIST" or "CHAIN" or "REVIEW" or "STAIR" or "FIX" or "B30" => AchievementCategoryGroup.Consistency,
            "DAN" or "DANPASS" or "GOLRIOLE" => AchievementCategoryGroup.Course,
            "PRISM" or "ENCORE" or "CODA" => AchievementCategoryGroup.Special,
            _ => IsLegacy ? AchievementCategoryGroup.Legacy : AchievementCategoryGroup.Technique
        };
        public int MinimumKeys { get; }
        internal int Threshold { get; }
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
    public static class AchievementGoals
    {
        private const string HITS = "manual_hits";
        private const string FULL_COMBO = "full_combo";

        internal static readonly AchievementGoal[] Legacy =
        {
            new("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000, HITS, legacyMedal: "万键之路", legacyTier: 1),
            new("manual_hits_50000", AchievementMetric.ManualHits, 0, 50000, HITS, legacyMedal: "五万键之路", legacyTier: 2),
            new("manual_hits_100000", AchievementMetric.ManualHits, 0, 100000, HITS, "beat", legacyMedal: "十万键之路", legacyTier: 2),
            new("manual_hits_500000", AchievementMetric.ManualHits, 0, 500000, HITS, legacyMedal: "五十万键之路", legacyTier: 3),
            new("manual_hits_1000000", AchievementMetric.ManualHits, 0, 1000000, HITS, legacyMedal: "百万键之路", legacyTier: 4),
            new("fc_lv25", AchievementMetric.FullComboCharts, 25, 1, FULL_COMBO, legacyMedal: "高阶全连", legacyTier: 2),
            new("fc_lv28_10", AchievementMetric.FullComboCharts, 28, 10, FULL_COMBO, "crest", legacyMedal: "十面全连", legacyTier: 3),
            new("first_ap", AchievementMetric.AllPerfect, 0, 1, avatarId: "sparkle", legacyMedal: "完美无缺", legacyTier: 2),
            new("first_dan_pass", AchievementMetric.DanPass, 0, 1, legacyMedal: "初获段位", legacyTier: 0)
        };

        private static readonly AchievementGoal[] _all =
        {
            new("HIT-01", AchievementMetric.ManualHits, 10000, "万键之路", "累计手动击打 10,000 个音符。", medalName: "万键之路", tier: 1, grade: "I"),
            new("HIT-02", AchievementMetric.ManualHits, 100000, "十万键之路", "累计手动击打 100,000 个音符。", medalName: "十万键之路", tier: 2, grade: "II", avatarId: "beat", avatarName: "节拍"),
            new("HIT-03", AchievementMetric.ManualHits, 1000000, "百万键之路", "累计手动击打 1,000,000 个音符。", medalName: "百万键之路", tier: 3, grade: "III"),
            new("HIT-04", AchievementMetric.ManualHits, 10000000, "千万键之路", "累计手动击打 10,000,000 个音符。", medalName: "千万键之路", tier: 4, grade: "IV"),
            new("SET-01", AchievementMetric.LongEnoughClears, 3, "再来一曲 · 1", "累计通关 3 局，每局时长至少一分钟。"),
            new("SET-02", AchievementMetric.LongEnoughClears, 10, "再来一曲 · 2", "累计通关 10 局，每局时长至少一分钟。"),
            new("SET-03", AchievementMetric.LongEnoughClears, 30, "再来一曲 · 3", "累计通关 30 局，每局时长至少一分钟。", medalName: "再来一曲", tier: 0, grade: "I"),
            new("SET-04", AchievementMetric.LongEnoughClears, 100, "再来一曲 · 4", "累计通关 100 局，每局时长至少一分钟。", medalName: "再来一曲", tier: 1, grade: "II"),
            new("SET-05", AchievementMetric.LongEnoughClears, 300, "再来一曲 · 5", "累计通关 300 局，每局时长至少一分钟。", medalName: "再来一曲", tier: 2, grade: "III"),
            new("SET-06", AchievementMetric.LongEnoughClears, 1000, "再来一曲 · 6", "累计通关 1000 局，每局时长至少一分钟。", medalName: "再来一曲", tier: 3, grade: "IV", avatarId: "encore_light", avatarName: "返场灯"),
            new("MAP-01", AchievementMetric.ClearedCharts, 10, "谱面地图 · 1", "累计通关 10 张不同谱面。"),
            new("MAP-02", AchievementMetric.ClearedCharts, 20, "谱面地图 · 2", "累计通关 20 张不同谱面。"),
            new("MAP-03", AchievementMetric.ClearedCharts, 50, "谱面地图 · 3", "累计通关 50 张不同谱面。"),
            new("MAP-04", AchievementMetric.ClearedCharts, 100, "谱面地图 · 4", "累计通关 100 张不同谱面。", medalName: "谱面地图", tier: 1, grade: "I"),
            new("MAP-05", AchievementMetric.ClearedCharts, 200, "谱面地图 · 5", "累计通关 200 张不同谱面。", medalName: "谱面地图", tier: 2, grade: "II"),
            new("MAP-06", AchievementMetric.ClearedCharts, 500, "谱面地图 · 6", "累计通关 500 张不同谱面。", medalName: "谱面地图", tier: 3, grade: "III", avatarId: "tour_book", avatarName: "巡演手册"),
            new("DAY-01", AchievementMetric.PracticeDays, 1, "练习日记 · 1", "累计 1 个游戏日，每日通关至少 3 张不同的谱面。"),
            new("DAY-02", AchievementMetric.PracticeDays, 7, "练习日记 · 2", "累计 7 个游戏日，每日通关至少 3 张不同的谱面。"),
            new("DAY-03", AchievementMetric.PracticeDays, 30, "练习日记 · 3", "累计 30 个游戏日，每日通关至少 3 张不同的谱面。", medalName: "练习日记", tier: 1, grade: "I"),
            new("DAY-04", AchievementMetric.PracticeDays, 90, "练习日记 · 4", "累计 90 个游戏日，每日通关至少 3 张不同的谱面。", medalName: "练习日记", tier: 2, grade: "II"),
            new("DAY-05", AchievementMetric.PracticeDays, 365, "练习日记 · 5", "累计 365 个游戏日，每日通关至少 3 张不同的谱面。", medalName: "练习日记", tier: 3, grade: "III", avatarId: "practice_moon", avatarName: "练习室的月亮"),
            new("RETRY-01", AchievementMetric.ChartRetries, 1, "再试一次 · 1", "在 1 张谱面上累积重试 1 次。"),
            new("RETRY-02", AchievementMetric.ChartRetries, 5, "再试一次 · 2", "在 1 张谱面上累积重试 5 次。"),
            new("RETRY-03", AchievementMetric.ChartRetries, 10, "再试一次 · 3", "在 1 张谱面上累积重试 10 次。", medalName: "再试一次", tier: 1, grade: "I"),
            new("RETRY-04", AchievementMetric.ChartRetries, 30, "再试一次 · 4", "在 1 张谱面上累积重试 30 次。", medalName: "再试一次", tier: 2, grade: "II"),
            new("COMBO-01", AchievementMetric.MaximumCombo, 500, "腱鞘炎 · 1", "通关一张谱面，单局最大连击达到 500。"),
            new("COMBO-02", AchievementMetric.MaximumCombo, 1000, "腱鞘炎 · 2", "通关一张谱面，单局最大连击达到 1000。"),
            new("COMBO-03", AchievementMetric.MaximumCombo, 1500, "腱鞘炎 · 3", "通关一张谱面，单局最大连击达到 1500。"),
            new("COMBO-04", AchievementMetric.MaximumCombo, 2000, "腱鞘炎 · 4", "通关一张谱面，单局最大连击达到 2000。", medalName: "腱鞘炎", tier: 1, grade: "I"),
            new("COMBO-05", AchievementMetric.MaximumCombo, 3000, "腱鞘炎 · 5", "通关一张谱面，单局最大连击达到 3000。", medalName: "腱鞘炎", tier: 2, grade: "II"),
            new("COMBO-06", AchievementMetric.MaximumCombo, 5000, "腱鞘炎 · 6", "通关一张谱面，单局最大连击达到 5000。", medalName: "腱鞘炎", tier: 3, grade: "III", avatarId: "long_kite", avatarName: "长线风筝"),
            new("FC-01", AchievementMetric.FullComboCharts, 1, "全连达人 · 1", "首次在任意 Ranked 谱面取得 FC。"),
            new("FC-02", AchievementMetric.FullComboCharts, 1, "全连达人 · 2", "在 1 张 Lv.25+、500 键以上的 Ranked 谱面上取得 FC。", minimumLevel: 25, minimumKeys: 500, medalName: "全连达人", tier: 0, grade: "I"),
            new("FC-03", AchievementMetric.FullComboCharts, 5, "全连达人 · 3", "在 5 张不同的 Lv.28+、800 键以上的 Ranked 谱面上取得 FC。", minimumLevel: 28, minimumKeys: 800, medalName: "全连达人", tier: 1, grade: "II", avatarId: "crest", avatarName: "纹章"),
            new("FC-04", AchievementMetric.FullComboCharts, 10, "全连达人 · 4", "在 10 张不同的 Lv.30+、1000 键以上的 Ranked 谱面上取得 FC。", minimumLevel: 30, minimumKeys: 1000, medalName: "全连达人", tier: 2, grade: "III"),
            new("FC-05", AchievementMetric.FullComboCharts, 20, "全连达人 · 5", "在 20 张不同的 Lv.31+、1000 键以上的 Ranked 谱面上取得 FC。", minimumLevel: 31, minimumKeys: 1000, medalName: "全连达人", tier: 3, grade: "IV"),
            new("FC-06", AchievementMetric.FullComboCharts, 30, "全连达人 · 6", "在 30 张不同的 Lv.32+、1200 键以上的 Ranked 谱面上取得 FC。", minimumLevel: 32, minimumKeys: 1200, medalName: "全连达人", tier: 4, grade: "V"),
            new("AP-01", AchievementMetric.AllPerfect, 1, "完美无缺 · 1", "首次在任意 Ranked 谱面取得 AP。"),
            new("AP-02", AchievementMetric.AllPerfect, 1, "完美无缺 · 2", "在 1 张 Lv.22+、500 键以上的 Ranked 谱面上取得 AP。", minimumLevel: 22, minimumKeys: 500, medalName: "完美无缺", tier: 0, grade: "I", avatarId: "sparkle", avatarName: "星芒"),
            new("AP-03", AchievementMetric.AllPerfect, 3, "完美无缺 · 3", "在 3 张不同的 Lv.25+、600 键以上的 Ranked 谱面上取得 AP。", minimumLevel: 25, minimumKeys: 600, medalName: "完美无缺", tier: 1, grade: "II"),
            new("AP-04", AchievementMetric.AllPerfect, 6, "完美无缺 · 4", "在 6 张不同的 Lv.28+、800 键以上的 Ranked 谱面上取得 AP。", minimumLevel: 28, minimumKeys: 800, medalName: "完美无缺", tier: 2, grade: "III"),
            new("AP-05", AchievementMetric.AllPerfect, 10, "完美无缺 · 5", "在 10 张不同的 Lv.30+、1000 键以上的 Ranked 谱面上取得 AP。", minimumLevel: 30, minimumKeys: 1000, medalName: "完美无缺", tier: 3, grade: "IV"),
            new("AP-06", AchievementMetric.AllPerfect, 15, "完美无缺 · 6", "在 15 张不同的 Lv.31+、1200 键以上的 Ranked 谱面上取得 AP。", minimumLevel: 31, minimumKeys: 1200, medalName: "完美无缺", tier: 4, grade: "V", avatarId: "flawless_atlas", avatarName: "无瑕星图"),
            new("EAR-01", AchievementMetric.PerfectRateClears, 1, "校音师 · 1", "总计 1 次在 Lv.26+、800 键以上的 Ranked 谱面上通关，单局 PERFECT 率 ≥ 90%。", minimumLevel: 26, minimumKeys: 800, threshold: 900),
            new("EAR-02", AchievementMetric.PerfectRateClears, 3, "校音师 · 2", "总计 3 次在 Lv.26+、800 键以上的 Ranked 谱面上通关，单局 PERFECT 率 ≥ 95%。", minimumLevel: 26, minimumKeys: 800, threshold: 950),
            new("EAR-03", AchievementMetric.PerfectRateClears, 5, "校音师 · 3", "总计 5 次在 Lv.26+、800 键以上的 Ranked 谱面上通关，单局 PERFECT 率 ≥ 98%。", minimumLevel: 26, minimumKeys: 800, threshold: 980, medalName: "校音师", tier: 1, grade: "I"),
            new("EAR-04", AchievementMetric.PerfectRateClears, 10, "校音师 · 4", "总计 10 次在 Lv.26+、800 键以上的 Ranked 谱面上通关，单局 PERFECT 率 ≥ 99%。", minimumLevel: 26, minimumKeys: 800, threshold: 990, medalName: "校音师", tier: 2, grade: "II"),
            new("EAR-05", AchievementMetric.PerfectRateClears, 15, "校音师 · 5", "总计 15 次在 Lv.26+、800 键以上的 Ranked 谱面上通关，单局 PERFECT 率 ≥ 99.5%。", minimumLevel: 26, minimumKeys: 800, threshold: 995, medalName: "校音师", tier: 3, grade: "III"),
            new("GREAT-01", AchievementMetric.NearMissClears, 1, "毫厘之间 · 1", "总计 1 次在 1000 键以上谱面上，通关且距 AP 或 FC 恰差 1 个判定。", minimumKeys: 1000),
            new("GREAT-02", AchievementMetric.NearMissClears, 3, "毫厘之间 · 2", "总计 3 次在 1000 键以上谱面上，通关且距 AP 或 FC 恰差 1 个判定。", minimumKeys: 1000, medalName: "毫厘之间", tier: 2, grade: "I"),
            new("GREAT-03", AchievementMetric.NearMissClears, 5, "毫厘之间 · 3", "总计 5 次在 1000 键以上谱面上，通关且距 AP 或 FC 恰差 1 个判定。", minimumKeys: 1000, medalName: "毫厘之间", tier: 2, grade: "II"),
            new("GREAT-04", AchievementMetric.NearMissClears, 10, "毫厘之间 · 4", "总计 10 次在 1000 键以上谱面上，通关且距 AP 或 FC 恰差 1 个判定。", minimumKeys: 1000, medalName: "毫厘之间", tier: 3, grade: "III"),
            new("GREAT-05", AchievementMetric.NearMissClears, 20, "毫厘之间 · 5", "总计 20 次在 1000 键以上谱面上，通关且距 AP 或 FC 恰差 1 个判定。", minimumKeys: 1000, medalName: "毫厘之间", tier: 4, grade: "IV"),
            new("LONG-01", AchievementMetric.LongChart, 1, "长途旅行 · 1", "在 1 张 3 分钟或以上的谱面上取得 A 级或以上。", threshold: 3),
            new("LONG-02", AchievementMetric.LongChart, 1, "长途旅行 · 2", "在 1 张 4 分钟或以上的谱面上取得 A 级或以上。", threshold: 4),
            new("LONG-03", AchievementMetric.LongChart, 1, "长途旅行 · 3", "在 1 张 5 分钟或以上的谱面上取得 A 级或以上。", threshold: 5, medalName: "长途旅行", tier: 1, grade: "I"),
            new("LONG-04", AchievementMetric.LongChart, 1, "长途旅行 · 4", "在 1 张 10 分钟或以上的谱面上取得 A 级或以上。", threshold: 10, medalName: "长途旅行", tier: 2, grade: "II"),
            new("LONG-05", AchievementMetric.LongChart, 1, "长途旅行 · 5", "在 1 张 20 分钟或以上的谱面上取得 A 级或以上。", threshold: 20, medalName: "长途旅行", tier: 3, grade: "III"),
            new("DENSE-01", AchievementMetric.DenseChart, 1, "耐力惊人 · 1", "在 1 张 1000 键以上，平均每秒至少 6 键的 Ranked 谱面上取得 S 级。", minimumKeys: 1000, threshold: 6),
            new("DENSE-02", AchievementMetric.DenseChart, 1, "耐力惊人 · 2", "在 1 张 1200 键以上，平均每秒至少 8 键的 Ranked 谱面上取得 S 级。", minimumKeys: 1200, threshold: 8, medalName: "耐力惊人", tier: 1, grade: "I"),
            new("DENSE-03", AchievementMetric.DenseChart, 1, "耐力惊人 · 3", "在 1 张 1400 键以上，平均每秒至少 10 键的 Ranked 谱面上取得 S 级。", minimumKeys: 1400, threshold: 10, medalName: "耐力惊人", tier: 2, grade: "II"),
            new("DENSE-04", AchievementMetric.DenseChart, 1, "耐力惊人 · 4", "在 1 张 1600 键以上，平均每秒至少 12 键的 Ranked 谱面上取得 S 级。", minimumKeys: 1600, threshold: 12, medalName: "耐力惊人", tier: 3, grade: "III"),
            new("DENSE-05", AchievementMetric.DenseChart, 1, "耐力惊人 · 5", "在 1 张 1800 键以上，平均每秒至少 14 键的 Ranked 谱面上取得 S 级。", minimumKeys: 1800, threshold: 14, medalName: "耐力惊人", tier: 3, grade: "IV"),
            new("FLICK-01", AchievementMetric.PerfectFlicks, 1, "箭羽 · 1", "通关 1 张至少有 15 个滑键判定的谱面，所有滑键均为 PERFECT。", threshold: 15),
            new("FLICK-02", AchievementMetric.PerfectFlicks, 1, "箭羽 · 2", "通关 1 张至少有 20 个滑键判定的谱面，所有滑键均为 PERFECT。", threshold: 20),
            new("FLICK-03", AchievementMetric.PerfectFlicks, 1, "箭羽 · 3", "通关 1 张至少有 30 个滑键判定的谱面，所有滑键均为 PERFECT。", threshold: 30, medalName: "箭羽", tier: 1, grade: "I"),
            new("FLICK-04", AchievementMetric.PerfectFlicks, 1, "箭羽 · 4", "通关 1 张至少有 40 个滑键判定的谱面，所有滑键均为 PERFECT。", threshold: 40, medalName: "箭羽", tier: 2, grade: "II"),
            new("FLICK-05", AchievementMetric.PerfectFlicks, 1, "箭羽 · 5", "通关 1 张至少有 50 个滑键判定的谱面，所有滑键均为 PERFECT。", threshold: 50, medalName: "箭羽", tier: 3, grade: "III", avatarId: "flying_arrow", avatarName: "流矢"),
            new("HOLD-01", AchievementMetric.PerfectHoldCharts, 1, "牵丝 · 1", "通关 1 张含至少 30 条有效长按链的谱面，全部计分中间点均为 PERFECT。", threshold: 30),
            new("HOLD-02", AchievementMetric.PerfectHoldCharts, 3, "牵丝 · 2", "通关 3 张互不相同的谱面，每张均含至少 40 条有效长按链，全部计分中间点均为 PERFECT。", threshold: 40),
            new("HOLD-03", AchievementMetric.PerfectHoldCharts, 5, "牵丝 · 3", "通关 5 张互不相同的谱面，每张均含至少 50 条有效长按链，全部计分中间点均为 PERFECT。", threshold: 50, medalName: "牵丝", tier: 1, grade: "I"),
            new("HOLD-04", AchievementMetric.PerfectHoldCharts, 8, "牵丝 · 4", "通关 8 张互不相同的谱面，每张均含至少 80 条有效长按链，全部计分中间点均为 PERFECT。", threshold: 80, medalName: "牵丝", tier: 2, grade: "II"),
            new("HOLD-05", AchievementMetric.PerfectHoldCharts, 12, "牵丝 · 5", "通关 12 张互不相同的谱面，每张均含至少 100 条有效长按链，全部计分中间点均为 PERFECT。", threshold: 100, medalName: "牵丝", tier: 3, grade: "III", avatarId: "silk_moon", avatarName: "丝线月"),
            new("FCLVL-01", AchievementMetric.FullComboLevel, 1, "皆传 · 1", "FC 1 张至少 Lv.26 的 Ranked 谱面。", minimumLevel: 26, medalName: "皆传", tier: 0, grade: "I"),
            new("FCLVL-02", AchievementMetric.FullComboLevel, 1, "皆传 · 2", "FC 1 张至少 Lv.27 的 Ranked 谱面。", minimumLevel: 27, medalName: "皆传", tier: 0, grade: "II"),
            new("FCLVL-03", AchievementMetric.FullComboLevel, 1, "皆传 · 3", "FC 1 张至少 Lv.28 的 Ranked 谱面。", minimumLevel: 28, medalName: "皆传", tier: 1, grade: "III"),
            new("FCLVL-04", AchievementMetric.FullComboLevel, 1, "皆传 · 4", "FC 1 张至少 Lv.29 的 Ranked 谱面。", minimumLevel: 29, medalName: "皆传", tier: 1, grade: "IV"),
            new("FCLVL-05", AchievementMetric.FullComboLevel, 1, "皆传 · 5", "FC 1 张至少 Lv.30 的 Ranked 谱面。", minimumLevel: 30, medalName: "皆传", tier: 2, grade: "V"),
            new("FCLVL-06", AchievementMetric.FullComboLevel, 1, "皆传 · 6", "FC 1 张至少 Lv.31 的 Ranked 谱面。", minimumLevel: 31, medalName: "皆传", tier: 2, grade: "VI"),
            new("FCLVL-07", AchievementMetric.FullComboLevel, 1, "皆传 · 7", "FC 1 张至少 Lv.32 的 Ranked 谱面。", minimumLevel: 32, medalName: "皆传", tier: 3, grade: "VII"),
            new("FCLVL-08", AchievementMetric.FullComboLevel, 1, "皆传 · 8", "FC 1 张至少 Lv.33 的 Ranked 谱面。", minimumLevel: 33, medalName: "皆传", tier: 3, grade: "VIII"),
            new("FCLVL-09", AchievementMetric.FullComboLevel, 1, "皆传 · 9", "FC 1 张至少 Lv.34 的 Ranked 谱面。", minimumLevel: 34, medalName: "皆传", tier: 4, grade: "IX"),
            new("FCLVL-10", AchievementMetric.FullComboLevel, 1, "皆传 · 10", "FC 1 张至少 Lv.35 的 Ranked 谱面。", minimumLevel: 35, medalName: "皆传", tier: 4, grade: "X"),
            new("FCLVL-11", AchievementMetric.FullComboLevel, 1, "皆传 · 11", "FC 1 张至少 Lv.36 的 Ranked 谱面。", minimumLevel: 36, medalName: "皆传", tier: 5, grade: "α"),
            new("FCLVL-12", AchievementMetric.FullComboLevel, 1, "皆传 · 12", "FC 1 张至少 Lv.37 的 Ranked 谱面。", minimumLevel: 37, medalName: "皆传", tier: 5, grade: "β"),
            new("FCLVL-13", AchievementMetric.FullComboLevel, 1, "皆传 · 13", "FC 1 张至少 Lv.38 的 Ranked 谱面。", minimumLevel: 38, medalName: "皆传", tier: 5, grade: "γ"),
            new("FCLVL-14", AchievementMetric.FullComboLevel, 1, "皆传 · 14", "FC 1 张至少 Lv.39 的 Ranked 谱面。", minimumLevel: 39, medalName: "皆传", tier: 5, grade: "δ"),
            new("APLVL-01", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 1", "AP 1 张至少 Lv.26 的 Ranked 谱面。", minimumLevel: 26, medalName: "真·皆传", tier: 0, grade: "I"),
            new("APLVL-02", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 2", "AP 1 张至少 Lv.27 的 Ranked 谱面。", minimumLevel: 27, medalName: "真·皆传", tier: 0, grade: "II"),
            new("APLVL-03", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 3", "AP 1 张至少 Lv.28 的 Ranked 谱面。", minimumLevel: 28, medalName: "真·皆传", tier: 1, grade: "III"),
            new("APLVL-04", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 4", "AP 1 张至少 Lv.29 的 Ranked 谱面。", minimumLevel: 29, medalName: "真·皆传", tier: 1, grade: "IV"),
            new("APLVL-05", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 5", "AP 1 张至少 Lv.30 的 Ranked 谱面。", minimumLevel: 30, medalName: "真·皆传", tier: 2, grade: "V"),
            new("APLVL-06", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 6", "AP 1 张至少 Lv.31 的 Ranked 谱面。", minimumLevel: 31, medalName: "真·皆传", tier: 2, grade: "VI"),
            new("APLVL-07", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 7", "AP 1 张至少 Lv.32 的 Ranked 谱面。", minimumLevel: 32, medalName: "真·皆传", tier: 3, grade: "VII"),
            new("APLVL-08", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 8", "AP 1 张至少 Lv.33 的 Ranked 谱面。", minimumLevel: 33, medalName: "真·皆传", tier: 3, grade: "VIII"),
            new("APLVL-09", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 9", "AP 1 张至少 Lv.34 的 Ranked 谱面。", minimumLevel: 34, medalName: "真·皆传", tier: 4, grade: "IX"),
            new("APLVL-10", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 10", "AP 1 张至少 Lv.35 的 Ranked 谱面。", minimumLevel: 35, medalName: "真·皆传", tier: 4, grade: "X"),
            new("APLVL-11", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 11", "AP 1 张至少 Lv.36 的 Ranked 谱面。", minimumLevel: 36, medalName: "真·皆传", tier: 5, grade: "α"),
            new("APLVL-12", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 12", "AP 1 张至少 Lv.37 的 Ranked 谱面。", minimumLevel: 37, medalName: "真·皆传", tier: 5, grade: "β"),
            new("APLVL-13", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 13", "AP 1 张至少 Lv.38 的 Ranked 谱面。", minimumLevel: 38, medalName: "真·皆传", tier: 5, grade: "γ"),
            new("APLVL-14", AchievementMetric.AllPerfectLevel, 1, "真·皆传 · 14", "AP 1 张至少 Lv.39 的 Ranked 谱面。", minimumLevel: 39, medalName: "真·皆传", tier: 5, grade: "δ"),
            new("SETLIST-01", AchievementMetric.DailySetlist, 5, "今日歌单 · 1", "在同一个游戏日通关 5 张不同的 Ranked 谱面，每张达到 A 级或以上。"),
            new("SETLIST-02", AchievementMetric.DailySetlist, 10, "今日歌单 · 2", "在同一个游戏日通关 10 张不同的 Ranked 谱面，每张达到 A 级或以上。"),
            new("SETLIST-03", AchievementMetric.DailySetlist, 20, "今日歌单 · 3", "在同一个游戏日通关 20 张不同的 Ranked 谱面，每张达到 A 级或以上。", medalName: "今日歌单", tier: 1, grade: "I"),
            new("SETLIST-04", AchievementMetric.DailySetlist, 30, "今日歌单 · 4", "在同一个游戏日通关 30 张不同的 Ranked 谱面，每张达到 A 级或以上。", medalName: "今日歌单", tier: 2, grade: "II", avatarId: "setlist", avatarName: "点歌单"),
            new("CHAIN-01", AchievementMetric.DistinctComboStreak, 2, "全连接力 · 1", "连续 2 局在互不相同的 Ranked 谱面上取得 FC。"),
            new("CHAIN-02", AchievementMetric.DistinctComboStreak, 3, "全连接力 · 2", "连续 3 局在互不相同的 Ranked 谱面上取得 FC。", medalName: "全连接力", tier: 2, grade: "I"),
            new("CHAIN-03", AchievementMetric.DistinctComboStreak, 5, "全连接力 · 3", "连续 5 局在互不相同的 Ranked 谱面上取得 FC。", medalName: "全连接力", tier: 2, grade: "II"),
            new("CHAIN-04", AchievementMetric.DistinctComboStreak, 8, "全连接力 · 4", "连续 8 局在互不相同的 Ranked 谱面上取得 FC。", medalName: "全连接力", tier: 3, grade: "III"),
            new("CHAIN-05", AchievementMetric.DistinctComboStreak, 12, "全连接力 · 5", "连续 12 局在互不相同的 Ranked 谱面上取得 FC。", medalName: "全连接力", tier: 4, grade: "IV", avatarId: "baton", avatarName: "接力棒"),
            new("REVIEW-01", AchievementMetric.ReviewDays, 2, "复习 · 1", "在 2 个不同游戏日，对同一张 Ranked 谱面取得 S 级且取得 FC。"),
            new("REVIEW-02", AchievementMetric.ReviewDays, 3, "复习 · 2", "在 3 个不同游戏日，对同一张 Ranked 谱面取得 S 级且取得 FC。", medalName: "复习", tier: 2, grade: "I"),
            new("REVIEW-03", AchievementMetric.ReviewDays, 5, "复习 · 3", "在 5 个不同游戏日，对同一张 Ranked 谱面取得 S 级且取得 FC。", medalName: "复习", tier: 2, grade: "II"),
            new("REVIEW-04", AchievementMetric.ReviewDays, 8, "复习 · 4", "在 8 个不同游戏日，对同一张 Ranked 谱面取得 S 级且取得 FC。", medalName: "复习", tier: 3, grade: "III"),
            new("REVIEW-05", AchievementMetric.ReviewDays, 12, "复习 · 5", "在 12 个不同游戏日，对同一张 Ranked 谱面取得 S 级且取得 FC。", medalName: "复习", tier: 3, grade: "IV"),
            new("STAIR-01", AchievementMetric.RisingLevels, 2, "半音阶 · 1", "连续 2 次在 Ranked 谱面达成 FC，每次谱面等级必须高于前一次，最低一级须为 Lv.23 或以上。", minimumLevel: 23),
            new("STAIR-02", AchievementMetric.RisingLevels, 3, "半音阶 · 2", "连续 3 次在 Ranked 谱面达成 FC，每次谱面等级必须高于前一次，最低一级须为 Lv.24 或以上。", minimumLevel: 24, medalName: "半音阶", tier: 1, grade: "I"),
            new("STAIR-03", AchievementMetric.RisingLevels, 4, "半音阶 · 3", "连续 4 次在 Ranked 谱面达成 FC，每次谱面等级必须高于前一次，最低一级须为 Lv.25 或以上。", minimumLevel: 25, medalName: "半音阶", tier: 2, grade: "II"),
            new("STAIR-04", AchievementMetric.RisingLevels, 5, "半音阶 · 4", "连续 5 次在 Ranked 谱面达成 FC，每次谱面等级必须高于前一次，最低一级须为 Lv.26 或以上。", minimumLevel: 26, medalName: "半音阶", tier: 3, grade: "III"),
            new("STAIR-05", AchievementMetric.RisingLevels, 6, "半音阶 · 5", "连续 6 次在 Ranked 谱面达成 FC，每次谱面等级必须高于前一次，最低一级须为 Lv.27 或以上。", minimumLevel: 27, medalName: "半音阶", tier: 4, grade: "IV", avatarId: "chromatic_keys", avatarName: "半音键盘"),
            new("FIX-01", AchievementMetric.RepairedCharts, 1, "把断点接上 · 1", "在 1 张谱面上，先以恰好 1 个断连判定通关，之后在同谱取得 FC。"),
            new("FIX-02", AchievementMetric.RepairedCharts, 3, "把断点接上 · 2", "在 3 张不同的谱面上，先以恰好 1 个断连判定通关，之后在同谱取得 FC。"),
            new("FIX-03", AchievementMetric.RepairedCharts, 5, "把断点接上 · 3", "在 5 张不同的谱面上，先以恰好 1 个断连判定通关，之后在同谱取得 FC。", medalName: "把断点接上", tier: 1, grade: "I"),
            new("FIX-04", AchievementMetric.RepairedCharts, 10, "把断点接上 · 4", "在 10 张不同的谱面上，先以恰好 1 个断连判定通关，之后在同谱取得 FC。", medalName: "把断点接上", tier: 2, grade: "II"),
            new("FIX-05", AchievementMetric.RepairedCharts, 15, "把断点接上 · 5", "在 15 张不同的谱面上，先以恰好 1 个断连判定通关，之后在同谱取得 FC。", medalName: "把断点接上", tier: 3, grade: "III"),
            new("B30-01", AchievementMetric.Best30, 6000, "新星", "个人 Ranking Score 总分达到 6,000。", medalName: "新星", tier: 0, grade: "I"),
            new("B30-02", AchievementMetric.Best30, 6900, "流星", "个人 Ranking Score 总分达到 6,900。", medalName: "流星", tier: 1, grade: "II"),
            new("B30-03", AchievementMetric.Best30, 7800, "银河", "个人 Ranking Score 总分达到 7,800。", medalName: "银河", tier: 2, grade: "III"),
            new("B30-04", AchievementMetric.Best30, 9000, "星海", "个人 Ranking Score 总分达到 9,000。", medalName: "星海", tier: 3, grade: "IV"),
            new("B30-05", AchievementMetric.Best30, 9900, "天穹", "个人 Ranking Score 总分达到 9,900。", medalName: "天穹", tier: 4, grade: "V"),
            new("B30-06", AchievementMetric.Best30, 10500, "寰宇", "个人 Ranking Score 总分达到 10,500。", medalName: "寰宇", tier: 4, grade: "VI", avatarId: "ranker", avatarName: "Ranker"),
            new("DAN-01", AchievementMetric.DanPass, 1, "初获段位", "首次通过任意一套段位。"),
            new("DANPASS-01", AchievementMetric.PassedCourses, 1, "过段 · 1", "通过 1 次任意段位。"),
            new("DANPASS-02", AchievementMetric.PassedCourses, 3, "过段 · 2", "通过 3 次任意段位。"),
            new("DANPASS-03", AchievementMetric.PassedCourses, 5, "过段 · 3", "通过 5 次任意段位。", medalName: "过段", tier: 0, grade: "I"),
            new("DANPASS-04", AchievementMetric.PassedCourses, 10, "过段 · 4", "通过 10 次任意段位。", medalName: "过段", tier: 1, grade: "II"),
            new("DANPASS-05", AchievementMetric.PassedCourses, 20, "过段 · 5", "通过 20 次任意段位。", medalName: "过段", tier: 2, grade: "III"),
            new("GOLRIOLE-01", AchievementMetric.PerfectRateCourses, 1, "后光 · 1", "通过 1 次任意段位，每次整套中每首的 PERFECT 率 ≥ 99%。"),
            new("GOLRIOLE-02", AchievementMetric.PerfectRateCourses, 3, "后光 · 2", "通过 3 次任意段位，每次整套中每首的 PERFECT 率 ≥ 99%。"),
            new("GOLRIOLE-03", AchievementMetric.PerfectRateCourses, 5, "后光 · 3", "通过 5 次任意段位，每次整套中每首的 PERFECT 率 ≥ 99%。", medalName: "后光", tier: 1, grade: "I"),
            new("GOLRIOLE-04", AchievementMetric.PerfectRateCourses, 10, "后光 · 4", "通过 10 次任意段位，每次整套中每首的 PERFECT 率 ≥ 99%。", medalName: "后光", tier: 2, grade: "II"),
            new("GOLRIOLE-05", AchievementMetric.PerfectRateCourses, 20, "后光 · 5", "通过 20 次任意段位，每次整套中每首的 PERFECT 率 ≥ 99%。", medalName: "后光", tier: 3, grade: "III", avatarId: "aureole", avatarName: "后光"),
            new("PRISM-01", AchievementMetric.Prism, 5, "白昼折光", "按红→黄→绿→蓝→紫的顺序，在 5 张互不相同的 Ranked 谱面连续取得 AP（EXPERT→HARD→EASY→NORMAL→MASTER），且这 5 张谱面的等级之和至少为 111。", medalName: "白昼折光", tier: 5, grade: "ω", avatarId: "prism_eye", avatarName: "折光之眼"),
            new("ENCORE-01", AchievementMetric.Encore, 100, "不落幕的安可", "在同一张 Lv.33+ 的 Ranked 谱面上累计手动通关 100 次，可以跨游戏日。", minimumLevel: 33, medalName: "不落幕的安可", tier: 5, grade: "ω", avatarId: "seventh_light", avatarName: "第七束光"),
            new("CODA-01", AchievementMetric.Coda, 1, "终乐章", "一次通过至少 4 首的段位组曲，每首均为 Lv.32+ 的 Ranked 谱面，且每首均取得 AP。", medalName: "终乐章", tier: 5, grade: "ω", avatarId: "final_barline", avatarName: "终止双线"),
        };

        public static IReadOnlyList<AchievementGoal> All { get; } = Array.AsReadOnly(_all);
        private static readonly Dictionary<string, AchievementGoal> _byId = _all.ToDictionary(goal => goal.Id);
        public static AchievementGoal Find(string id) => id != null && _byId.TryGetValue(id, out var goal) ? goal : null;

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
