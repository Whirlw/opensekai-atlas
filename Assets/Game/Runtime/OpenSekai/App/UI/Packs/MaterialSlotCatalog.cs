using System;
using System.Collections.Generic;
using System.Text;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// What the resource pack page calls the nine categories and their slots. The registries in
    /// <c>OpenSekai.Core/Packaging</c> decide which slots exist; this only names them and sorts
    /// them into groups for display. A slot the rules here do not know still shows, under its key.
    /// </summary>
    internal static class MaterialSlotCatalog
    {
        internal readonly struct CategoryInfo
        {
            internal readonly string Id;
            internal readonly string Title;
            internal readonly string Eyebrow;
            internal readonly string Description;
            internal readonly Color32 Tint;
            internal readonly string[] BuiltinLabels;

            internal CategoryInfo(string id, string title, string eyebrow, string description, Color32 tint, params string[] builtin)
            {
                Id = id;
                Title = title;
                Eyebrow = eyebrow;
                Description = description;
                Tint = tint;
                BuiltinLabels = builtin;
            }
        }

        private static readonly Dictionary<string, CategoryInfo> _categories = new Dictionary<string, CategoryInfo>(StringComparer.Ordinal)
        {
            [MaterialCategories.NOTES] = new CategoryInfo(MaterialCategories.NOTES, "音符皮肤", "NOTE SKIN",
                "音头、长条、方向箭头、同时线", HomeTheme.Colors.Rose, "样式 1", "样式 2"),
            [MaterialCategories.HIT_FX] = new CategoryInfo(MaterialCategories.HIT_FX, "打击特效", "HIT EFFECTS",
                "打击、长按、轨道反馈与演出", HomeTheme.Colors.Violet, "特效 1", "特效 2"),
            [MaterialCategories.AUDIO] = new CategoryInfo(MaterialCategories.AUDIO, "打歌音效", "SOUNDS",
                "打击、循环、倒数、结算与语音", HomeTheme.Colors.Periwinkle, "音效 1", "音效 2"),
            [MaterialCategories.JUDGMENT] = new CategoryInfo(MaterialCategories.JUDGMENT, "判定文字", "JUDGMENT",
                "判定文字与快慢提示", HomeTheme.Colors.Rose),
            [MaterialCategories.COMBO] = new CategoryInfo(MaterialCategories.COMBO, "COMBO", "COMBO COUNTER",
                "标签、数字与光晕", HomeTheme.Colors.Violet),
            [MaterialCategories.LANE] = new CategoryInfo(MaterialCategories.LANE, "轨道", "LANE",
                "轨道底板、分道线、判定线", HomeTheme.Colors.Periwinkle),
            [MaterialCategories.BACKGROUND] = new CategoryInfo(MaterialCategories.BACKGROUND, "背景", "BACKGROUND",
                "整组背景图层", HomeTheme.Colors.Rose),
            [MaterialCategories.HUD] = new CategoryInfo(MaterialCategories.HUD, "打歌界面", "HUD",
                "分数、生命、暂停、倒数与 AUTO", HomeTheme.Colors.Violet),
            [MaterialCategories.RESULT_FX] = new CategoryInfo(MaterialCategories.RESULT_FX, "结算特效", "RESULT FX",
                "结算模板部件与整段演出", HomeTheme.Colors.Periwinkle),
        };

        internal static CategoryInfo Category(string id) =>
            _categories.TryGetValue(id ?? string.Empty, out CategoryInfo info) ? info : new CategoryInfo(id, id, id?.ToUpperInvariant(), string.Empty, HomeTheme.Colors.Violet);

        /// <summary>"音效 2" for a two-set category, "游戏默认" for the others.</summary>
        internal static string BuiltinLabel(string category, int set)
        {
            CategoryInfo info = Category(category);
            if (info.BuiltinLabels == null || info.BuiltinLabels.Length == 0) return "游戏默认";
            return info.BuiltinLabels[Mathf.Clamp(set - 1, 0, info.BuiltinLabels.Length - 1)];
        }

        internal static string BuiltinLabel(string category, MaterialBuiltinSelection builtin) =>
            MaterialCategories.HasBuiltinSets(category) ? BuiltinLabel(category, (builtin ?? new MaterialBuiltinSelection()).Get(category)) : "游戏默认";

        // ------------------------------------------------------------------ slots

        internal readonly struct SlotInfo
        {
            internal readonly string Name;
            internal readonly string Group;

            internal SlotInfo(string name, string group)
            {
                Name = name;
                Group = group;
            }
        }

        private static readonly Dictionary<string, SlotInfo> _audio = new Dictionary<string, SlotInfo>(StringComparer.Ordinal)
        {
            ["audio.tap"] = new SlotInfo("空敲", "打击"),
            ["audio.perfect"] = new SlotInfo("PERFECT 打击", "打击"),
            ["audio.great"] = new SlotInfo("GREAT 打击", "打击"),
            ["audio.good"] = new SlotInfo("GOOD 打击", "打击"),
            ["audio.critical"] = new SlotInfo("CRITICAL 打击", "打击"),
            ["audio.long"] = new SlotInfo("长条持续（循环）", "长条与连接点"),
            ["audio.longCritical"] = new SlotInfo("CRITICAL 长条持续（循环）", "长条与连接点"),
            ["audio.connect"] = new SlotInfo("连接点", "长条与连接点"),
            ["audio.connectCritical"] = new SlotInfo("CRITICAL 连接点", "长条与连接点"),
            ["audio.flick"] = new SlotInfo("划键", "划键与摩擦"),
            ["audio.flickCritical"] = new SlotInfo("CRITICAL 划键", "划键与摩擦"),
            ["audio.trace"] = new SlotInfo("摩擦（循环）", "划键与摩擦"),
            ["audio.traceCritical"] = new SlotInfo("CRITICAL 摩擦（循环）", "划键与摩擦"),
            ["audio.countdown"] = new SlotInfo("倒数", "倒数与结算"),
            ["audio.clear"] = new SlotInfo("通关", "倒数与结算"),
            ["audio.fullCombo"] = new SlotInfo("FULL COMBO", "倒数与结算"),
            ["audio.allPerfect"] = new SlotInfo("ALL PERFECT", "倒数与结算"),
            ["audio.startVoice"] = new SlotInfo("开场语音", "语音"),
            ["audio.finishVoice"] = new SlotInfo("终了语音", "语音"),
            ["audio.clearVoice"] = new SlotInfo("通关语音", "语音"),
            ["audio.fullComboVoice"] = new SlotInfo("FULL COMBO 语音", "语音"),
            ["audio.allPerfectVoice"] = new SlotInfo("ALL PERFECT 语音", "语音"),
        };

        private static readonly Dictionary<string, string> _noteHeads = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["notes.notes_0"] = "CRITICAL 宽音头",
            ["notes.notes_1"] = "长条头",
            ["notes.notes_2"] = "点按",
            ["notes.notes_3"] = "划键",
            ["notes.notes_4"] = "摩擦 / 摩擦长条",
            ["notes.notes_5"] = "CRITICAL 摩擦",
            ["notes.notes_6"] = "摩擦划键",
            ["notes.notes_damage"] = "伤害音符",
            ["notes.notes_long_among"] = "长条连接点",
            ["notes.notes_long_among_crtcl"] = "CRITICAL 连接点",
            ["notes.notes_friction_among_long"] = "摩擦图标",
            ["notes.notes_friction_among_flick"] = "摩擦划键图标",
            ["notes.notes_friction_among_crtcl"] = "CRITICAL 摩擦图标",
            ["notes.line.long"] = "长条条身",
            ["notes.line.guide"] = "引导线",
            ["notes.line.simultaneous"] = "同时线",
        };

        private static readonly Dictionary<string, string> _words = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Hit effects.
            ["lane"] = "轨道", ["tap"] = "空敲", ["normal"] = string.Empty, ["critical"] = "CRITICAL", ["flick"] = "划键",
            ["trace"] = "摩擦", ["tick"] = "连接点", ["long"] = "长条", ["hold"] = "持续", ["aura"] = "光圈",
            ["particles"] = "粒子", ["flash"] = "闪光", ["trail"] = "拖尾", ["perfect"] = "PERFECT", ["great"] = "GREAT",
            ["good"] = "GOOD", ["screen"] = "全屏", ["damage"] = "伤害",
            // HUD.
            ["score"] = "分数", ["life"] = "生命", ["add"] = "加分", ["auto"] = "AUTO", ["continuous"] = "连续自动",
            ["countdown"] = "倒数", ["layout"] = "布局", ["base"] = "底板", ["gaugeBase"] = "进度槽", ["gauge"] = "进度填充",
            ["rankPlate"] = "评级底板", ["skill"] = "技能高亮", ["normalGauge"] = "普通填充", ["damageGauge"] = "受伤填充",
            ["icon"] = "心形", ["pause"] = "暂停", ["pauseDisabled"] = "暂停（禁用）", ["skip"] = "跳过", ["label"] = "标识",
            ["shade"] = "遮罩", ["text"] = "文字", ["ring"] = "圆环", ["marker"] = "分数线", ["line"] = "线条",
            ["digit"] = "数字", ["outline"] = "描边", ["plus"] = "正号", ["minus"] = "负号", ["rank"] = "评级",
            ["rankLabel"] = "RANK 标签",
            // Result effects.
            ["failure"] = "失败", ["lifeZero"] = "生命耗尽", ["clear"] = "通关", ["fullCombo"] = "FULL COMBO",
            ["allPerfect"] = "ALL PERFECT", ["background"] = "结算背景", ["flare"] = "光芒", ["mesh"] = "网格",
            ["glow"] = "光晕", ["circle"] = "圆环", ["bang"] = "感叹号", ["bang1"] = "感叹号 1", ["bang2"] = "感叹号 2",
        };

        private static readonly Dictionary<string, string> _hudGroups = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["score"] = "分数面板", ["life"] = "生命面板", ["add"] = "加分数字", ["auto"] = "AUTO 标识",
            ["continuous"] = "连续自动打歌", ["countdown"] = "倒数",
        };

        internal static SlotInfo Slot(string category, string slot)
        {
            if (string.IsNullOrEmpty(slot)) return new SlotInfo(string.Empty, string.Empty);
            switch (category)
            {
                case MaterialCategories.AUDIO:
                    return _audio.TryGetValue(slot, out SlotInfo audio) ? audio : new SlotInfo(slot, "其他");
                case MaterialCategories.NOTES:
                    return NoteSlot(slot);
                case MaterialCategories.JUDGMENT:
                    return JudgmentSlot(slot);
                case MaterialCategories.COMBO:
                    return ComboSlot(slot);
                case MaterialCategories.LANE:
                    return new SlotInfo(slot switch
                    {
                        MaterialLaneSlots.BASE => "轨道底板",
                        MaterialLaneSlots.DIVIDERS => "分道线",
                        MaterialLaneSlots.JUDGE_LINE => "判定线",
                        _ => slot
                    }, "轨道");
                case MaterialCategories.BACKGROUND:
                    return new SlotInfo("整组背景图层", "背景");
                case MaterialCategories.HIT_FX:
                    return HitFxSlot(slot);
                case MaterialCategories.HUD:
                    return HudSlot(slot);
                case MaterialCategories.RESULT_FX:
                    return ResultSlot(slot);
                default:
                    return new SlotInfo(slot, "其他");
            }
        }

        private static SlotInfo NoteSlot(string slot)
        {
            if (_noteHeads.TryGetValue(slot, out string name))
            {
                string group = slot.StartsWith("notes.line.", StringComparison.Ordinal) ? "线条"
                    : slot.Contains("among") ? "连接点与图标" : "音头";
                return new SlotInfo(name, group);
            }

            const string ARROW = "notes.notes_flick_arrow_";
            if (slot.StartsWith(ARROW, StringComparison.Ordinal))
            {
                string rest = slot.Substring(ARROW.Length);
                bool critical = rest.StartsWith("crtcl_", StringComparison.Ordinal);
                if (critical) rest = rest.Substring("crtcl_".Length);
                bool diagonal = rest.EndsWith("_diagonal", StringComparison.Ordinal);
                if (diagonal) rest = rest.Substring(0, rest.Length - "_diagonal".Length);
                string width = rest.TrimStart('0');
                return new SlotInfo((critical ? "CRITICAL " : string.Empty) + (diagonal ? "斜划箭头" : "直划箭头") + " · 宽 " + width,
                    critical ? "CRITICAL 方向箭头" : "方向箭头");
            }

            return new SlotInfo(slot, "其他");
        }

        private static SlotInfo JudgmentSlot(string slot)
        {
            switch (slot)
            {
                case "judgment.fast": return new SlotInfo("FAST", "快慢提示");
                case "judgment.late": return new SlotInfo("LATE", "快慢提示");
                case "judgment.flickMiss": return new SlotInfo("划键 MISS", "判定文字");
                default:
                    string word = slot.StartsWith("judgment.", StringComparison.Ordinal) ? slot.Substring("judgment.".Length) : slot;
                    return new SlotInfo(word.ToUpperInvariant(), "判定文字");
            }
        }

        private static SlotInfo ComboSlot(string slot)
        {
            string group = slot.StartsWith("combo.ap.", StringComparison.Ordinal) ? "ALL PERFECT"
                : slot.StartsWith("combo.glow.", StringComparison.Ordinal) ? "光晕" : "普通";
            string prefix = group == "普通" ? string.Empty : group == "光晕" ? "光晕" : "AP ";
            if (slot.EndsWith(".label", StringComparison.Ordinal)) return new SlotInfo(prefix + "COMBO 标签", group);
            int digit = slot.LastIndexOf(".digit.", StringComparison.Ordinal);
            if (digit >= 0) return new SlotInfo(prefix + "数字 " + slot.Substring(digit + ".digit.".Length), group);
            return new SlotInfo(slot, group);
        }

        private static SlotInfo HitFxSlot(string slot)
        {
            string[] parts = Parts(slot, "hitFx.");
            bool critical = Array.IndexOf(parts, "critical") >= 0;
            string group;
            if (Array.IndexOf(parts, "lane") >= 0) group = "轨道反馈";
            else if (Array.IndexOf(parts, "screen") >= 0) group = "全屏演出";
            else if (Array.IndexOf(parts, "flick") >= 0) group = "划键";
            else if (Array.IndexOf(parts, "trace") >= 0) group = "摩擦";
            else if (Array.IndexOf(parts, "long") >= 0 || Array.IndexOf(parts, "tick") >= 0) group = "长条与连接点";
            else group = "打击";
            if (critical) group = "CRITICAL " + group;
            return new SlotInfo(Words(parts), group);
        }

        private static SlotInfo HudSlot(string slot)
        {
            string[] parts = Parts(slot, "hud.");
            if (parts.Length == 0) return new SlotInfo(slot, "其他");
            string group = _hudGroups.TryGetValue(parts[0], out string known) ? known : "其他";
            if (parts.Length > 1 && (parts[1] == "digit" || parts[1] == "outline") && parts[0] != "add")
                group = (parts[0] == "score" ? "分数" : parts[0] == "life" ? "生命" : string.Empty) + "数字";
            else if (parts.Length > 1 && parts[1] == "marker") group = "分数线";
            else if (parts.Length > 1 && (parts[1] == "rank" || parts[1] == "rankLabel")) group = "评级";
            return new SlotInfo(Words(parts), group);
        }

        private static SlotInfo ResultSlot(string slot)
        {
            if (slot == MaterialResultFxSlots.BACKGROUND) return new SlotInfo("结算背景", "结算背景");
            string[] parts = Parts(slot, "resultFx.");
            if (parts.Length == 0) return new SlotInfo(slot, "其他");
            string group = _words.TryGetValue(parts[0], out string name) ? name : parts[0];
            if (parts.Length == 1) return new SlotInfo(group + " · 整段演出", group);
            string part = parts[1];
            string label = part == "layout" ? "布局"
                : _words.TryGetValue(part, out string word) ? word
                : "字母 " + part;
            return new SlotInfo(group + " · " + label, group);
        }

        private static string[] Parts(string slot, string prefix)
        {
            string rest = slot.StartsWith(prefix, StringComparison.Ordinal) ? slot.Substring(prefix.Length) : slot;
            return rest.Split('.');
        }

        private static string Words(string[] parts)
        {
            var builder = new StringBuilder();
            foreach (string part in parts)
            {
                string word = _words.TryGetValue(part, out string known) ? known : part;
                if (word.Length == 0) continue;
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(word);
            }

            return builder.Length > 0 ? builder.ToString() : string.Join(".", parts);
        }
    }
}
