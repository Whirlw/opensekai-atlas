using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialHudSlots
    {
        // Paths are relative to FrontUIView. Keep the existing safe-area anchors and animated parents.
        public static IReadOnlyDictionary<string, string> Nodes { get; } = new Dictionary<string, string>
        {
            ["hud.score.layout"] = "LiveUI/LeftTopAnchor/ScoreView",
            ["hud.score.base"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase",
            ["hud.score.gaugeBase"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/ScoreGaugeBase",
            ["hud.score.gauge"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/ScoreGauge",
            ["hud.score.rankPlate"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/RankPlate",
            ["hud.score.skill"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreSkill",
            ["hud.life.layout"] = "LiveUI/RightTopAnchor/LifeView/root",
            ["hud.life.base"] = "LiveUI/RightTopAnchor/LifeView/root/LifeBackground",
            ["hud.life.normal"] = "LiveUI/RightTopAnchor/LifeView/root/LifeBaseDefault",
            ["hud.life.damage"] = "LiveUI/RightTopAnchor/LifeView/root/LifeBaseDamage",
            ["hud.life.normalGauge"] = "LiveUI/RightTopAnchor/LifeView/root/LifeBaseDefault/lifeGauge",
            ["hud.life.damageGauge"] = "LiveUI/RightTopAnchor/LifeView/root/LifeBaseDamage/lifeGauge",
            ["hud.life.icon"] = "LiveUI/RightTopAnchor/LifeView/root/Icon",
            ["hud.life.skill"] = "LiveUI/RightTopAnchor/LifeView/root/LifeSkill",
            ["hud.life.pause"] = "LiveUI/RightTopAnchor/LifeView/root/PauseButton",
            ["hud.life.pauseDisabled"] = "LiveUI/RightTopAnchor/LifeView/root/PauseButtonDisable",
            ["hud.life.skip"] = "LiveUI/RightTopAnchor/LifeView/root/SkipButton",
            ["hud.auto.label"] = "LiveUI/RighBottmAnchor/Auto",
            ["hud.continuous.shade"] = "ConsecutiveAutoLive/GameObject",
            ["hud.continuous.pause"] = "ConsecutiveAutoLive/TopRight/PauseButton",
            ["hud.continuous.pauseDisabled"] = "ConsecutiveAutoLive/TopRight/PauseButtonDisable",
            ["hud.continuous.label"] = "ConsecutiveAutoLive/BottomRight/Auto",
            ["hud.countdown.layout"] = "LiveUI/CountdownView",
            ["hud.countdown.1.text"] = "LiveUI/CountdownView/fx_live_countdown_1/root/text",
            ["hud.countdown.1.ring"] = "LiveUI/CountdownView/fx_live_countdown_1/root/circle",
            ["hud.countdown.2.text"] = "LiveUI/CountdownView/fx_live_countdown_2/root/text",
            ["hud.countdown.2.ring"] = "LiveUI/CountdownView/fx_live_countdown_2/root/circle",
            ["hud.countdown.3.text"] = "LiveUI/CountdownView/fx_live_countdown_3/root/text",
            ["hud.countdown.3.ring"] = "LiveUI/CountdownView/fx_live_countdown_3/root/circle",
            ["hud.score.marker.C.base"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineC",
            ["hud.score.marker.C.line"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineC/Line",
            ["hud.score.marker.C.label"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineC/C",
            ["hud.score.marker.B.base"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineB",
            ["hud.score.marker.B.line"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineB/Line",
            ["hud.score.marker.B.label"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineB/B",
            ["hud.score.marker.A.base"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineA",
            ["hud.score.marker.A.line"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineA/Line",
            ["hud.score.marker.A.label"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineA/A",
            ["hud.score.marker.S.base"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineS",
            ["hud.score.marker.S.line"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineS/Line",
            ["hud.score.marker.S.label"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/lineS/S",
            ["hud.score.digit.layout"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/Number/NumberRoot",
            ["hud.score.outline.layout"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/Number/NumberOutlineRoot",
            ["hud.add.digit.layout"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/AddNumber/AddNumberRoot",
            ["hud.add.outline.layout"] = "LiveUI/LeftTopAnchor/ScoreView/ScoreBase/AddNumber/AddNumberOutlineRoot",
            ["hud.life.digit.layout"] = "LiveUI/RightTopAnchor/LifeView/root/NumberRoot",
            ["hud.life.outline.layout"] = "LiveUI/RightTopAnchor/LifeView/root/NumberOutlineRoot",
        };

        private static readonly string[] _numberPrefixes =
        {
            "hud.score.digit", "hud.score.outline", "hud.add.digit", "hud.add.outline", "hud.life.digit", "hud.life.outline"
        };

        private static readonly HashSet<string> _slots = new(Nodes.Keys.Concat(
            from prefix in _numberPrefixes from digit in Enumerable.Range(0, 10) select prefix + "." + digit)
            .Concat(from prefix in _numberPrefixes.Where(p => p.StartsWith("hud.add.", StringComparison.Ordinal))
                    from sign in new[] { ".plus", ".minus" } select prefix + sign)
            .Concat(from rank in "SABCD" from prefix in new[] { "hud.score.rank.", "hud.score.rankLabel." } select prefix + rank),
            StringComparer.Ordinal);

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string key) => _slots.Contains(key);
        public static bool IsLayout(string key) => key.EndsWith(".layout", StringComparison.Ordinal) && Nodes.ContainsKey(key);
        public static bool IsRing(string key) => key.EndsWith(".ring", StringComparison.Ordinal) && Nodes.ContainsKey(key);
    }
}
