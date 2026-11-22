using System;
using System.Collections.Generic;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialLaneSlots
    {
        public const string BASE = "lane.base";
        public const string DIVIDERS = "lane.dividers";
        public const string JUDGE_LINE = "lane.judgeLine";
        private static readonly HashSet<string> _slots = new(StringComparer.Ordinal) { BASE, DIVIDERS, JUDGE_LINE };

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string name) => _slots.Contains(name);
    }
}
