using System;
using System.Collections.Generic;
using System.Globalization;
using OpenSekai.App.Library;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>The page's filters: what kind of goal an achievement counts.</summary>
    internal enum AchievementCategory
    {
        All,
        Hits,
        Charts,
        Dan
    }

    /// <summary>What a medal shows on its face.</summary>
    internal enum AchievementGlyph
    {
        Hits,
        FullCombo,
        AllPerfect,
        Dan,
        Trophy
    }

    /// <summary>How one achievement reads and looks: its name, what it asks for, how it is counted, its medal.</summary>
    internal sealed class AchievementLook
    {
        internal AchievementLook(string title, string latin, string goal, string rule, AchievementCategory category,
            AchievementGlyph glyph, string mark, string subMark, Color32 tint, int medalTier = MedalTiers.LOWEST, string medalName = null, string grade = null)
        {
            Title = title;
            Latin = latin;
            Goal = goal;
            Rule = rule;
            Category = category;
            Glyph = glyph;
            Mark = mark;
            SubMark = subMark;
            Tint = tint;
            MedalTier = medalTier;
            MedalName = medalName ?? title;
            Grade = grade;
        }

        /// <summary>The name, e.g. 万键之路.</summary>
        internal string Title { get; }

        /// <summary>A short Latin caption beside the name, e.g. 10,000 HITS.</summary>
        internal string Latin { get; }

        /// <summary>What the player has to do, in one line.</summary>
        internal string Goal { get; }

        /// <summary>What counts and what does not.</summary>
        internal string Rule { get; }

        internal AchievementCategory Category { get; }
        internal AchievementGlyph Glyph { get; }

        /// <summary>The big text on the medal (10K, FC, AP); empty for a drawn glyph.</summary>
        internal string Mark { get; }

        /// <summary>The small text under it (LV25+, ×10); may be empty.</summary>
        internal string SubMark { get; }

        internal Color32 Tint { get; }

        /// <summary>Which frame the medal it gives wears, one of <see cref="MedalTiers"/>.</summary>
        internal int MedalTier { get; }
        internal string MedalName { get; }
        internal string Grade { get; }
    }

    /// <summary>
    /// The words and medals of the achievements. The backend owns the goals (ids, metrics,
    /// targets, what counts); this maps each stable id to its copy and look. An id this table does
    /// not know still reads sensibly from its metric and target, so a new goal never shows a raw id.
    /// </summary>
    internal static class AchievementCopy
    {
        internal static readonly Color32 HitsTint = new Color32(110, 200, 255, 255);
        internal static readonly Color32 FullComboTint = new Color32(255, 198, 112, 255);
        internal static readonly Color32 FullComboDeepTint = new Color32(255, 160, 96, 255);
        internal static readonly Color32 AllPerfectTint = new Color32(255, 150, 210, 255);
        internal static readonly Color32 DanTint = new Color32(172, 146, 255, 255);

        /// <summary>The two ends of ALL PERFECT's letters, as on the result page.</summary>
        internal static readonly Color32 PrismStart = new Color32(255, 146, 214, 255);
        internal static readonly Color32 PrismEnd = new Color32(112, 224, 255, 255);

        private const string HITS_RULE = "Perfect、Great、Good、Bad 都算，段位各曲与中途结束前的击打也算；Miss 和自动游玩不计";

        internal static readonly string[] CategoryNames = { "全部", "累计击打", "谱面达成", "段位通关" };

        private static readonly Dictionary<string, AchievementLook> _known = new Dictionary<string, AchievementLook>(StringComparer.Ordinal)
        {
            ["manual_hits_10000"] = new AchievementLook("万键之路", "10,000 HITS", "累计击打 10,000 个音符", HITS_RULE,
                AchievementCategory.Hits, AchievementGlyph.Hits, "10K", "HITS", HitsTint, 1),
            ["manual_hits_50000"] = new AchievementLook("五万键之路", "50,000 HITS", "累计击打 50,000 个音符", HITS_RULE,
                AchievementCategory.Hits, AchievementGlyph.Hits, "50K", "HITS", HitsTint, 2),
            ["manual_hits_100000"] = new AchievementLook("十万键之路", "100,000 HITS", "累计击打 100,000 个音符", HITS_RULE,
                AchievementCategory.Hits, AchievementGlyph.Hits, "100K", "HITS", HitsTint, 2),
            ["manual_hits_500000"] = new AchievementLook("五十万键之路", "500,000 HITS", "累计击打 500,000 个音符", HITS_RULE,
                AchievementCategory.Hits, AchievementGlyph.Hits, "500K", "HITS", HitsTint, 3),
            ["manual_hits_1000000"] = new AchievementLook("百万键之路", "1,000,000 HITS", "累计击打 1,000,000 个音符", HITS_RULE,
                AchievementCategory.Hits, AchievementGlyph.Hits, "1M", "HITS", HitsTint, 4),
            ["fc_lv25"] = new AchievementLook("高阶全连", "FULL COMBO · LV.25+", "在 Lv.25 及以上的谱面取得 FULL COMBO",
                "普通模式手动通关即可，ALL PERFECT 同样算数；段位中的曲目不计",
                AchievementCategory.Charts, AchievementGlyph.FullCombo, "FC", "LV25+", FullComboTint, 2),
            ["fc_lv28_10"] = new AchievementLook("十面全连", "10 × FULL COMBO · LV.28+", "在 10 张 Lv.28 及以上的谱面取得 FULL COMBO",
                "按谱面计数，同一张谱反复全连只算一张；ALL PERFECT 同样算数",
                AchievementCategory.Charts, AchievementGlyph.FullCombo, "FC", "×10", FullComboDeepTint, 3),
            ["first_ap"] = new AchievementLook("完美无缺", "ALL PERFECT", "首次取得 ALL PERFECT",
                "普通模式手动通关，每一个判定都是 Perfect",
                AchievementCategory.Charts, AchievementGlyph.AllPerfect, "AP", string.Empty, AllPerfectTint, 2),
            ["first_dan_pass"] = new AchievementLook("初获段位", "DAN CLEAR", "首次通过段位挑战",
                "打完段位的全部曲目时仍有生命即为通过，任何段位都算",
                AchievementCategory.Dan, AchievementGlyph.Dan, string.Empty, string.Empty, DanTint)
        };

        /// <summary>The copy of <paramref name="id"/>; from <paramref name="progress"/>'s metric when the id is new.</summary>
        internal static AchievementLook For(string id, AchievementProgress progress = null)
        {
            if (id != null && _known.TryGetValue(id, out AchievementLook look)) return look;
            var goal = AchievementGoals.Find(id);
            if (goal != null)
            {
                var category = CategoryOf(goal.Metric);
                var glyph = category == AchievementCategory.Hits ? AchievementGlyph.Hits :
                    category == AchievementCategory.Dan ? AchievementGlyph.Dan : AchievementGlyph.Trophy;
                var tint = category == AchievementCategory.Hits ? HitsTint :
                    category == AchievementCategory.Dan ? DanTint : FullComboTint;
                return new AchievementLook(goal.Title, goal.Id, goal.Description, string.Empty, category, glyph,
                    goal.Series, string.Empty, tint, goal.MedalFrameTier, goal.MedalName, goal.MedalGrade);
            }
            return progress != null ? FromMetric(progress) : new AchievementLook("新成就", "ACHIEVEMENT", "达成了一项新目标", string.Empty,
                AchievementCategory.All, AchievementGlyph.Trophy, string.Empty, string.Empty, FullComboTint);
        }

        internal static AchievementLook For(AchievementProgress progress) => For(progress?.Id, progress);

        internal static AchievementCategory CategoryOf(AchievementMetric metric)
        {
            switch (metric)
            {
                case AchievementMetric.ManualHits: return AchievementCategory.Hits;
                case AchievementMetric.DanPass:
                case AchievementMetric.PassedCourses:
                case AchievementMetric.PerfectRateCourses:
                case AchievementMetric.Coda: return AchievementCategory.Dan;
                default: return AchievementCategory.Charts;
            }
        }

        internal static bool IsUnlocked(AchievementProgress progress) => progress?.State == AchievementState.Unlocked;

        internal static bool IsReached(AchievementProgress progress) => progress?.ReachedAtUtc.HasValue == true;

        /// <summary>What a goal hands over, as its box names it; empty when it gives nothing.</summary>
        internal static string RewardKinds(AchievementProgress progress)
        {
            bool avatar = progress.Goal.AvatarId != null;
            if (!progress.Goal.GivesMedal) return avatar ? "头像" : string.Empty;
            return avatar ? "奖牌 · 头像" : "奖牌";
        }

        /// <summary>"已领取奖牌「十万键之路」和头像" once <paramref name="progress"/> is unlocked; null when it gives nothing.</summary>
        internal static string Received(AchievementProgress progress)
        {
            bool avatar = progress.Goal.AvatarId != null;
            if (!progress.Goal.GivesMedal) return avatar ? "已领取头像，可以在编辑资料里使用" : null;
            return "已领取奖牌「" + (progress.Goal.MedalDisplayName ?? For(progress).Title) + "」" + (avatar ? "和头像" : string.Empty) + "，可以到个人信息页使用";
        }

        /// <summary>True when the goal is reached once or not at all, so a bar would say nothing.</summary>
        internal static bool IsOneShot(AchievementProgress progress) => progress.Target <= 1;

        /// <summary>0 to 1, how far toward the target; 1 once reached.</summary>
        internal static float Fraction(AchievementProgress progress)
        {
            if (progress == null) return 0f;
            if (IsReached(progress)) return 1f;
            if (progress.Target <= 0) return 0f;
            return Mathf.Clamp01((float)((double)progress.Current / progress.Target));
        }

        /// <summary>A whole percentage; never 100 before the goal is reached.</summary>
        internal static int Percent(AchievementProgress progress)
        {
            if (IsReached(progress)) return 100;
            return Mathf.Min(99, Mathf.FloorToInt(Fraction(progress) * 100f));
        }

        /// <summary>Where the count stands, e.g. "6,240 / 10,000" or "3 / 10 张".</summary>
        internal static string Count(AchievementProgress progress)
        {
            string unit = progress.Metric == AchievementMetric.FullComboCharts ? " 张" : string.Empty;
            return Number(Math.Min(progress.Current, progress.Target)) + " / " + Number(progress.Target) + unit;
        }

        /// <summary>What the player has piled up beyond the goal, e.g. "累计 12,480 键"; null when nothing more to say.</summary>
        internal static string Total(AchievementProgress progress)
        {
            switch (progress.Metric)
            {
                case AchievementMetric.ManualHits: return "累计 " + Number(progress.Current) + " 键";
                case AchievementMetric.FullComboCharts:
                    return progress.Current > 0 ? "已全连 " + Number(progress.Current) + " 张" : null;
                default: return null;
            }
        }

        /// <summary>The day and time it was unlocked, on this device's clock.</summary>
        internal static string When(DateTimeOffset when) =>
            when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>The names of <paramref name="ids"/> in 「」, joined for one line.</summary>
        internal static string Names(IReadOnlyList<string> ids, int limit = 3)
        {
            if (ids == null || ids.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < ids.Count && i < limit; i++) parts.Add("「" + For(ids[i]).Title + "」");
            string names = string.Join(string.Empty, parts);
            return ids.Count > limit ? names + " 等 " + ids.Count + " 项" : names;
        }

        internal static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static AchievementLook FromMetric(AchievementProgress progress)
        {
            string target = Number(progress.Target);
            string level = progress.MinimumLevel > 0 ? " Lv." + progress.MinimumLevel + " 及以上的" : string.Empty;
            switch (progress.Metric)
            {
                case AchievementMetric.ManualHits:
                    return new AchievementLook("累计击打 " + target, target + " HITS", "累计击打 " + target + " 个音符",
                        "Perfect、Great、Good、Bad 都算；Miss 和自动游玩不计", AchievementCategory.Hits, AchievementGlyph.Hits,
                        Short(progress.Target), "HITS", HitsTint);
                case AchievementMetric.FullComboCharts:
                    return new AchievementLook(target + " 张全连", target + " × FULL COMBO",
                        "在 " + target + " 张" + level + "谱面取得 FULL COMBO", "按谱面计数，同一张谱只算一张",
                        AchievementCategory.Charts, AchievementGlyph.FullCombo, "FC",
                        progress.Target > 1 ? "×" + target : progress.MinimumLevel > 0 ? "LV" + progress.MinimumLevel + "+" : string.Empty,
                        FullComboTint);
                case AchievementMetric.AllPerfect:
                    return new AchievementLook("ALL PERFECT", "ALL PERFECT", "取得 ALL PERFECT", "普通模式手动通关，每一个判定都是 Perfect",
                        AchievementCategory.Charts, AchievementGlyph.AllPerfect, "AP", string.Empty, AllPerfectTint);
                default:
                    return new AchievementLook("段位通关", "DAN CLEAR", "通过段位挑战", "打完段位的全部曲目时仍有生命即为通过",
                        AchievementCategory.Dan, AchievementGlyph.Dan, string.Empty, string.Empty, DanTint);
            }
        }

        /// <summary>10000 → 10K, 2500 → 2.5K, 900 → 900, 2000000 → 2M.</summary>
        private static string Short(long value)
        {
            if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
            bool millions = value >= 1000000;
            double scaled = value / (millions ? 1000000d : 1000d);
            return scaled.ToString("0.#", CultureInfo.InvariantCulture) + (millions ? "M" : "K");
        }
    }
}
