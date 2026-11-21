using System;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public sealed class MaterialEffectFrame
    {
        public int[] Rect { get; set; }
        public int DurationMs { get; set; }
    }

    public sealed class MaterialEffectLayer : MaterialMediaSlot
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public float[] Pivot { get; set; }
        public float[] Size { get; set; }
        public float[] Offset { get; set; }
        public float[] Scale { get; set; }
        public MaterialEffectFrame[] Frames { get; set; }
        public int[] LoopFrameRange { get; set; }

        internal bool IsValid(string key)
        {
            MaterialPackManifest.CheckPath(File);
            return Hash?.Length == 64 && Hash.All(Uri.IsHexDigit)
                && Width is > 0 and <= MaterialSpriteSlot.MAX_DIMENSION && Height is > 0 and <= MaterialSpriteSlot.MAX_DIMENSION
                && MaterialSpriteSlot.PairInRange(Pivot, 0, 1) && MaterialSpriteSlot.PairInRange(Offset, -8192, 8192)
                && MaterialSpriteSlot.PositivePair(Size, 8192) && MaterialSpriteSlot.PositivePair(Scale, 64)
                && Frames?.Length is > 0 and <= 256 && Frames.All(f => f?.Rect?.Length == 4 && f.DurationMs is > 0 and <= 60000
                    && f.Rect[0] >= 0 && f.Rect[1] >= 0 && f.Rect[2] > 0 && f.Rect[3] > 0
                    && f.Rect[0] <= Width - f.Rect[2] && f.Rect[1] <= Height - f.Rect[3])
                && (LoopFrameRange == null || key.Contains(".hold.") && LoopFrameRange.Length == 2
                    && LoopFrameRange[0] >= 0 && LoopFrameRange[0] < LoopFrameRange[1] && LoopFrameRange[1] <= Frames.Length);
        }
    }
}
