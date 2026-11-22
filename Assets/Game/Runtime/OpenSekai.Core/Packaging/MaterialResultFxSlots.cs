using System;
using System.Collections.Generic;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialResultFxSlots
    {
        public const string BACKGROUND = "resultFx.background";
        private static readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal)
        {
            ["fx_result_failed_v2"] = "resultFx.failure",
            ["fx_result_finish"] = "resultFx.lifeZero",
            ["fx_result_clear_v2"] = "resultFx.clear",
            ["fx_result_full_combo_v2"] = "resultFx.fullCombo",
            ["fx_result_all_perfect_v2"] = "resultFx.allPerfect"
        };

        private static readonly HashSet<string> _slots = BuildSlots();

        private static HashSet<string> BuildSlots()
        {
            var slots = new HashSet<string>(_templates.Values, StringComparer.Ordinal) { BACKGROUND };
            foreach (string key in _templates.Values) slots.Add(key + ".layout");
            Add("failure", "line text");
            Add("lifeZero", "line flare particles L1 I V E1 F I2 N I3 S H");
            Add("clear", "line flare particles L1 I V E1 C1 L2 E2 A R bang");
            Add("fullCombo", "flare circle particles mesh F U L1 L2 C O1 M B O2 bang");
            Add("allPerfect", "line flare glow particles mesh A L1 L2 P E1 R F E2 C T bang1 bang2");
            return slots;

            void Add(string result, string parts)
            {
                foreach (var part in parts.Split(' ')) slots.Add("resultFx." + result + "." + part);
            }
        }

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string key) => _slots.Contains(key);
        public static bool IsEvent(string key) => _templates.ContainsValue(key);
        public static bool IsLayout(string key) => key.EndsWith(".layout", StringComparison.Ordinal) && HasSlot(key);
        internal static bool IsTexture(string key) => key.EndsWith(".particles", StringComparison.Ordinal) || key.EndsWith(".mesh", StringComparison.Ordinal);
        public static string ForTemplate(string prefab) => _templates.TryGetValue(prefab, out var key) ? key : null;

        public static string SpritePart(string node) => node switch
        {
            "Text_line_01" or "Text_line_02" or "line_01" or "line_02" => "line",
            "tex_flare" or "add" => "flare",
            "Glow" => "glow",
            "Circle_Glow" or "Circle_02" => "circle",
            "!" => "bang", "!1" => "bang1", "!2" => "bang2",
            _ => node
        };
    }
}
