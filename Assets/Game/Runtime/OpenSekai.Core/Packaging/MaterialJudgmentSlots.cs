using System;
using System.Collections.Generic;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialJudgmentSlots
    {
        private static readonly HashSet<string> _slots = new(StringComparer.Ordinal)
        {
            "judgment.miss", "judgment.bad", "judgment.good", "judgment.great",
            "judgment.perfect", "judgment.auto",
            "judgment.fast", "judgment.late", "judgment.flickMiss"
        };

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string name) => _slots.Contains(name);
    }
}
