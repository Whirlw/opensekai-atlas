using System;
using System.Collections.Generic;
using System.Globalization;
using OpenSekai.App.Library;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// How each series of achievements reads and looks: its color and emblem, and from a goal's own
    /// numbers what the level asks for in one line, what counts, and the few characters under its
    /// emblem. The names and the medals come from the goal. A special medal takes its own emblem.
    /// </summary>
    internal static class AchievementSeriesCopy
    {
        private const string RANKED_ONLY = "只计 Ranked 谱面";
        private const string NEW_DAY = "每天 04:00 换日";

        private static readonly Dictionary<string, AchievementLook> _looks = new Dictionary<string, AchievementLook>(StringComparer.Ordinal);

        /// <summary>The look of <paramref name="goal"/>; null for a series this table does not know.</summary>
        internal static AchievementLook For(AchievementGoal goal)
        {
            if (_looks.TryGetValue(goal.Id, out AchievementLook look)) return look;
            look = Build(goal);
            if (look != null) _looks[goal.Id] = look;
            return look;
        }

        private static AchievementLook Build(AchievementGoal goal)
        {
            string target = Number(goal.Target);
            string level = "Lv." + goal.MinimumLevel;
            string keys = Number(goal.MinimumKeys) + " 键";
            int threshold = goal.Threshold;
            switch (goal.Series)
            {
                case "HIT":
                    return Look(goal, new Color32(110, 200, 255, 255), target + " HITS", Short(goal.Target),
                        "累计击打 " + target + " 个音符", AchievementCopy.HITS_RULE);
                case "SET":
                    return Look(goal, new Color32(255, 158, 138, 255), target + " CLEARS", "×" + Short(goal.Target),
                        "通关 " + target + " 局，每局至少一分钟", "重打同一张谱面也算；失败、自动游玩和段位中的曲目不计");
                case "MAP":
                    return Look(goal, new Color32(96, 212, 188, 255), target + " CHARTS", "×" + target,
                        "通关 " + target + " 张不同的谱面", "按谱面内容计数，换包或改名仍是同一张；同一首歌的不同难度分开算");
                case "DAY":
                    return Look(goal, new Color32(160, 172, 255, 255), target + (goal.Target == 1 ? " DAY" : " DAYS"), target + " D",
                        goal.Target == 1 ? "在一个游戏日通关 3 张不同的谱面" : "在 " + target + " 个游戏日，每天通关 3 张不同的谱面",
                        NEW_DAY + "；日子可以不连续");
                case "RETRY":
                    return Look(goal, new Color32(152, 220, 124, 255), target + (goal.Target == 1 ? " RETRY" : " RETRIES"), "×" + target,
                        "在同一张谱面上重试 " + target + " 次", "在暂停页或结算页点「重试」并成功开局才算，可以跨天累计；从曲库重新选曲不计");
                case "COMBO":
                    return Look(goal, new Color32(255, 122, 134, 255), target + " COMBO", target,
                        "单局最大连击达到 " + target + "，并通关", "普通模式手动通关即可，不限谱面");
                case "FC":
                    return Look(goal, new Color32(255, 198, 112, 255), Award("FULL COMBO", goal), Needs(goal),
                        goal.MinimumLevel == 0 ? "在任意 Ranked 谱面取得 FULL COMBO"
                            : "在 " + target + " 张 " + level + "+、" + keys + "以上的谱面取得 FULL COMBO",
                        RANKED_ONLY + "，同一张只算一次；ALL PERFECT 同样算数");
                case "AP":
                    return Look(goal, new Color32(255, 150, 210, 255), Award("ALL PERFECT", goal), Needs(goal),
                        goal.MinimumLevel == 0 ? "在任意 Ranked 谱面取得 ALL PERFECT"
                            : "在 " + target + " 张 " + level + "+、" + keys + "以上的谱面取得 ALL PERFECT",
                        RANKED_ONLY + "，同一张只算一次");
                case "EAR":
                    return Look(goal, new Color32(126, 222, 226, 255), "PERFECT " + Percent(threshold), Percent(threshold),
                        target + " 局 " + level + "+、" + keys + "以上，PERFECT 率不低于 " + Percent(threshold),
                        RANKED_ONLY + "的通关局，可以重打同一张；按原始比例比较，不四舍五入");
                case "GREAT":
                    return Look(goal, new Color32(214, 150, 255, 255), "ONE NOTE AWAY", "×" + target,
                        target + " 局 " + keys + "以上，距 AP 或 FC 只差一个判定", "差 AP 指只有一个非 Perfect，差 FC 指恰好断一次；直接 AP 不计，同一局只算一次");
                case "LONG":
                    return Look(goal, new Color32(255, 170, 104, 255), threshold + " MINUTES", threshold + " MIN",
                        "在 " + threshold + " 分钟以上的谱面取得 A 级或以上", "按完整曲长计算，不要求 Ranked");
                case "DENSE":
                    return Look(goal, new Color32(255, 112, 156, 255), threshold + " NOTES / SEC", threshold + " / S",
                        "在 " + keys + "以上、每秒 " + threshold + " 键以上的谱面取得 S 级", RANKED_ONLY + "；密度按总键数除以完整曲长计算");
                case "FLICK":
                    return Look(goal, new Color32(255, 132, 176, 255), threshold + " FLICKS", threshold.ToString(CultureInfo.InvariantCulture),
                        "通关有 " + threshold + " 个以上滑键的谱面，滑键全 Perfect", "带滑动的尾键也算滑键；其余音符只需保证通关");
                case "HOLD":
                    return Look(goal, new Color32(130, 226, 160, 255), threshold + " HOLDS × " + target, threshold + "+",
                        target + " 张有 " + threshold + " 条以上长按的谱面，中间点全 Perfect", "只看长按的计分中间点；首尾和其余音符只需保证通关，同一张谱面只算一次");
                case "FCLVL":
                    return Look(goal, new Color32(255, 196, 96, 255), "FULL COMBO · LV." + goal.MinimumLevel, "LV " + goal.MinimumLevel,
                        "在一张 " + level + " 及以上的 Ranked 谱面取得 FULL COMBO", "ALL PERFECT 同样算数；等级更高的谱面会一并达成前面的等级");
                case "APLVL":
                    return Look(goal, new Color32(192, 154, 255, 255), "ALL PERFECT · LV." + goal.MinimumLevel, "LV " + goal.MinimumLevel,
                        "在一张 " + level + " 及以上的 Ranked 谱面取得 ALL PERFECT", "等级更高的谱面会一并达成前面的等级");
                case "SETLIST":
                    return Look(goal, new Color32(224, 214, 120, 255), target + " IN A DAY", "×" + target,
                        "同一个游戏日在 " + target + " 张 Ranked 谱面取得 A 级以上", NEW_DAY + "；同一张谱面当天只算一次");
                case "CHAIN":
                    return Look(goal, new Color32(255, 176, 88, 255), target + " IN A ROW", "×" + target,
                        "连续 " + target + " 局在不同的 Ranked 谱面取得 FULL COMBO", "失败、中途退出、重复的谱面或没有全连都会中断；自动游玩和段位不影响连续");
                case "REVIEW":
                    return Look(goal, new Color32(122, 170, 255, 255), target + " DAYS", target + " D",
                        "在 " + target + " 个游戏日，对同一张谱面取得 S 级并全连", RANKED_ONLY + "；" + NEW_DAY + "，日子可以不连续");
                case "STAIR":
                    return Look(goal, new Color32(142, 150, 255, 255), target + " STEPS UP", "×" + target,
                        "连续 " + target + " 局全连，谱面等级一局比一局高", RANKED_ONLY + "，从 " + level + " 或以上起步；中间任何一局没有全连都要重来");
                case "FIX":
                    return Look(goal, new Color32(120, 196, 214, 255), target + " MENDED", "×" + target,
                        "在 " + target + " 张谱面上先断一次通关，再取得 FULL COMBO", "先以恰好一个断连判定通关，之后在同一张谱面全连；中间可以打别的谱面");
                case "B30":
                {
                    MedalEmblemArt.SeriesOf(goal.Id, out int tier);
                    return Look(goal, RankingTiers.Color(tier), "RANKING SCORE " + target, target,
                        "Ranking Score 达到 " + target, "按个人最佳 30 张谱面的贡献合计，与排名评级一致", goal.Id);
                }
                case "DAN":
                    return Look(goal, AchievementCopy.DanTint, "DAN CLEAR", string.Empty,
                        "首次通过任意一套段位", "打完段位的全部曲目时仍有生命即为通过，任何段位都算");
                case "DANPASS":
                    return Look(goal, new Color32(150, 140, 255, 255), target + " DAN CLEARS", "×" + target,
                        "通过 " + target + " 次段位", "任何段位都算，可以重复挑战同一套");
                case "GOLRIOLE":
                    return Look(goal, new Color32(255, 220, 140, 255), "PERFECT 99%", "×" + target,
                        "通过 " + target + " 次段位，每首 PERFECT 率不低于 99%", "整套中的每一首都要达到，平均达到不算；可以重复挑战同一套");
                case "PRISM":
                    return Look(goal, new Color32(150, 170, 255, 255), "FIVE COLORS OF LIGHT", "AP ×5",
                        "按红、黄、绿、蓝、紫的顺序连续五局 ALL PERFECT",
                        "依次为 EXPERT、HARD、EASY、NORMAL、MASTER 五张不同的 Ranked 谱面；中间插入其他局要从头再来");
                case "ENCORE":
                    return Look(goal, new Color32(244, 112, 132, 255), "100TH ENCORE", "×100",
                        "同一张 Lv.33 及以上的 Ranked 谱面通关 100 次", "可以跨天累计，换谱或中途退出都会保留已有次数");
                case "CODA":
                    return Look(goal, new Color32(240, 224, 190, 255), "FINALE", "AP ×4",
                        "一次通过 4 首以上的段位，每首 ALL PERFECT", "每首都须是 Lv.32 及以上的 Ranked 谱面，第 5 首起同样检查");
                default:
                    return null;
            }
        }

        private static AchievementLook Look(AchievementGoal goal, Color32 tint, string latin, string caption, string text, string rule,
            string emblem = null)
        {
            if (SpecialPlateArt.Has(goal.MedalFaceId)) emblem = goal.MedalFaceId;
            return new AchievementLook(goal.Title, latin, text, rule, AchievementCopy.CategoryOf(goal.Category), AchievementGlyph.Emblem,
                string.Empty, caption, tint, goal.MedalFrameTier, goal.MedalName, goal.MedalGrade, emblem ?? goal.Series);
        }

        /// <summary>"5 × FULL COMBO · LV.28+".</summary>
        private static string Award(string award, AchievementGoal goal) =>
            (goal.Target > 1 ? Number(goal.Target) + " × " : string.Empty) + award
            + (goal.MinimumLevel > 0 ? " · LV." + goal.MinimumLevel + "+" : string.Empty);

        /// <summary>"LV28+" under the emblem.</summary>
        private static string Needs(AchievementGoal goal) => goal.MinimumLevel == 0 ? string.Empty : "LV" + goal.MinimumLevel + "+";

        /// <summary>995 is 99.5%.</summary>
        private static string Percent(int perMille) => (perMille / 10d).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        private static string Number(long value) => AchievementCopy.Number(value);

        private static string Short(long value) => AchievementCopy.Short(value);
    }
}
