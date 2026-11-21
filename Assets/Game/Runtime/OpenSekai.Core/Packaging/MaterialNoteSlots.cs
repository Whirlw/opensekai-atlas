using System;
using System.Collections.Generic;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialNoteSlots
    {
        private static readonly HashSet<string> _slots = Build();
        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string name) => _slots.Contains(name);
        public static bool IsLine(string name) => name.StartsWith("notes.line.", StringComparison.Ordinal);

        private static HashSet<string> Build()
        {
            var slots = new HashSet<string>(StringComparer.Ordinal)
            {
                "notes.line.long", "notes.line.guide", "notes.line.simultaneous",
                "notes.notes_long_among", "notes.notes_long_among_crtcl", "notes.notes_friction_among_flick",
                "notes.notes_friction_among_crtcl", "notes.notes_friction_among_long",
                "notes.notes_damage"
            };
            for (int i = 0; i < 7; i++) slots.Add("notes.notes_" + i);
            foreach (string weight in new[] { "", "crtcl_" })
                foreach (string direction in new[] { "", "_diagonal" })
                    for (int i = 1; i <= 6; i++)
                        slots.Add("notes.notes_flick_arrow_" + weight + "0" + i + direction);
            // Both upstream sets use the third diagonal sprite for four-lane ordinary flicks.
            slots.Remove("notes.notes_flick_arrow_04_diagonal");
            return slots;
        }
    }
}
