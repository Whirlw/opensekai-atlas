using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialComboSlots
    {
        private static readonly HashSet<string> _slots = new(
            new[] { "combo.label", "combo.ap.label", "combo.glow.label" }.Concat(
                from prefix in new[] { "combo.digit.", "combo.ap.digit.", "combo.glow.digit." }
                from digit in "0123456789" select prefix + digit), StringComparer.Ordinal);

        public static IEnumerable<string> Slots => _slots;
        public static bool HasSlot(string name) => _slots.Contains(name);
    }

    public sealed class MaterialComboLayout
    {
        public float[] Position { get; set; }
        public float[] Scale { get; set; }
        public float[] DigitsPosition { get; set; }
        public float[] BurstPosition { get; set; }
        public float[] GlowPosition { get; set; }

        internal bool IsValid() => MaterialSpriteSlot.PairInRange(Position, -8192, 8192)
            && MaterialSpriteSlot.PairInRange(DigitsPosition, -8192, 8192)
            && MaterialSpriteSlot.PairInRange(BurstPosition, -8192, 8192)
            && MaterialSpriteSlot.PairInRange(GlowPosition, -8192, 8192)
            && MaterialSpriteSlot.PositivePair(Scale, 64);
    }

    public sealed class MaterialSpriteSlot : MaterialMediaSlot
    {
        public const int MAX_DIMENSION = 2048;
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Rect { get; set; }
        public float[] NoteSlice { get; set; }
        public float[] Pivot { get; set; }
        public float[] Offset { get; set; }
        public float[] Scale { get; set; }
        public float[] Size { get; set; }
        public float[] Position { get; set; }
        public float? Opacity { get; set; }
        public float? Advance { get; set; }

        public int? TemplateSet { get; set; }
        public MaterialEffectLayer[] Layers { get; set; }

        internal bool IsValid(string slot)
        {
            if (TemplateSet.HasValue && (TemplateSet is not (1 or 2) || !MaterialHitFxSlots.HasSlot(slot) || MaterialHitFxSlots.IsTrail(slot))) return false;
            if (Layers != null && (!MaterialHitFxSlots.HasSlot(slot) || MaterialHitFxSlots.IsTrail(slot)
                || Layers.Length is < 1 or > 8 || Layers.Any(layer => layer == null || !layer.IsValid(slot))
                || File != Layers[0].File || Hash != Layers[0].Hash || Width != Layers[0].Width || Height != Layers[0].Height)) return false;
            if (MaterialHitFxSlots.HasSlot(slot))
            {
                if (Rect?.Length != 4 || Rect[0] != 0 || Rect[1] != 0 || Rect[2] != Width || Rect[3] != Height
                    || Pivot != null || Offset != null || Size != null || Advance != null || Opacity != null
                    || MaterialHitFxSlots.IsTrail(slot) && (Scale != null || Position != null)) return false;
            }
            if (NoteSlice != null && (!MaterialNoteSlots.HasSlot(slot) || MaterialNoteSlots.IsLine(slot)
                || NoteSlice.Length != 4 || !(NoteSlice[0] >= 0 && NoteSlice[1] >= 0 && NoteSlice[2] > 0 && NoteSlice[3] > 0
                    && NoteSlice[0] < Width && NoteSlice[1] < Height
                    // Fractional source slices can touch the last pixel within one float rounding step.
                    && NoteSlice[0] + NoteSlice[2] <= Width + Width * 1e-7
                    && NoteSlice[1] + NoteSlice[3] <= Height + Height * 1e-7))) return false;
            if (MaterialNoteSlots.HasSlot(slot) && (Offset != null || Size != null || MaterialNoteSlots.IsLine(slot)
                && (Pivot != null || Rect?.Length != 4 || Rect[0] != 0 || Rect[1] != 0 || Rect[2] != Width || Rect[3] != Height))) return false;
            return Width is > 0 and <= MAX_DIMENSION && Height is > 0 and <= MAX_DIMENSION
                && Rect?.Length == 4 && Rect[0] >= 0 && Rect[1] >= 0 && Rect[2] > 0 && Rect[3] > 0
                && Rect[0] <= Width - Rect[2] && Rect[1] <= Height - Rect[3]
                && PairInRange(Pivot, 0, 1) && PairInRange(Offset, -8192, 8192)
                && PositivePair(Scale, 64) && PositivePair(Size, 8192)
                && (Position == null || (slot.EndsWith(".label", StringComparison.Ordinal) || MaterialJudgmentSlots.HasSlot(slot) || MaterialHitFxSlots.HasSlot(slot) || MaterialNoteSlots.HasSlot(slot)) && PairInRange(Position, -8192, 8192))
                && (Opacity == null || slot.StartsWith("combo.glow.", StringComparison.Ordinal) && Opacity >= 0 && Opacity <= 1)
                && (Advance == null || slot.Contains(".digit.") && !slot.StartsWith("combo.glow.", StringComparison.Ordinal)
                    && Advance > 0 && Advance <= 8192);
        }

        internal static bool PositivePair(float[] pair, float max) => PairInRange(pair, 0, max) && (pair == null || pair.All(x => x > 0));

        internal static bool PairInRange(float[] pair, float min, float max) =>
            pair == null || pair.Length == 2 && pair.All(x => x >= min && x <= max);
    }
}
