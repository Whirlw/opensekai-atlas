using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialHitFxSlots
    {
        // Both built-in particle sets expose the same template names.
        private static readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal)
        {
            ["fx_lane_tap"] = "hitFx.lane.tap",
            ["fx_lane_default"] = "hitFx.lane.normal",
            ["fx_lane_critical"] = "hitFx.lane.critical",
            ["fx_lane_flick"] = "hitFx.lane.normal.flick",
            ["fx_lane_critical_flick"] = "hitFx.lane.critical.flick",
            ["fx_ove_01"] = "hitFx.screen.damage",
            ["fx_note_normal_aura"] = "hitFx.normal.perfect.aura",
            ["fx_note_normal_gen"] = "hitFx.normal.perfect.particles",
            ["fx_note_trace_aura"] = "hitFx.trace.aura",
            ["fx_note_critical_trace_aura"] = "hitFx.critical.trace.aura",
            ["fx_note_normal_aura_Great"] = "hitFx.normal.great.aura",
            ["fx_note_normal_gen_Great"] = "hitFx.normal.great.particles",
            ["fx_note_normal_aura_Good"] = "hitFx.normal.good.aura",
            ["fx_note_normal_gen_Good"] = "hitFx.normal.good.particles",
            ["fx_note_critical_normal_aura"] = "hitFx.critical.aura",
            ["fx_note_critical_normal_gen"] = "hitFx.critical.particles",
            ["fx_note_flick_aura"] = "hitFx.flick.aura",
            ["fx_note_flick_gen"] = "hitFx.flick.particles",
            ["fx_note_flick_flash"] = "hitFx.flick.flash",
            ["fx_note_critical_flick_aura"] = "hitFx.critical.flick.aura",
            ["fx_note_critical_flick_gen"] = "hitFx.critical.flick.particles",
            ["fx_note_critical_flick_flash"] = "hitFx.critical.flick.flash",
            ["fx_note_long_hold_via_aura"] = "hitFx.tick.aura",
            ["fx_note_critical_long_hold_via_aura"] = "hitFx.critical.tick.aura",
            ["fx_note_long_aura"] = "hitFx.long.aura",
            ["fx_note_long_gen"] = "hitFx.long.particles",
            ["fx_note_critical_long_aura"] = "hitFx.critical.long.aura",
            ["fx_note_critical_long_gen"] = "hitFx.critical.long.particles",
            ["fx_note_hold_aura"] = "hitFx.long.hold.aura",
            ["fx_note_long_hold_gen"] = "hitFx.long.hold.particles",
            ["fx_note_critical_long_hold_gen_aura"] = "hitFx.critical.long.hold.aura",
            ["fx_note_critical_long_hold_gen"] = "hitFx.critical.long.hold.particles",
        };

        private static readonly HashSet<string> _slots = new(_templates.Values.Concat(new[]
        {
            "hitFx.critical.aura.trail",
            "hitFx.critical.long.aura.trail",
            "hitFx.critical.flick.aura.trail",
        }), StringComparer.Ordinal);

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string name) => _slots.Contains(name);
        public static bool IsTrail(string name) => name.EndsWith(".trail", StringComparison.Ordinal);
        public static bool TryGetTemplate(string prefabName, out string slot) => _templates.TryGetValue(prefabName, out slot);
    }
}
