using System;
using System.Globalization;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public sealed class MaterialBackgroundScene
    {
        public const string SLOT = "background.scene";
        internal const int MAX_LAYERS = 256;
        public bool PreferVideo { get; set; } = true;
        public MaterialBackgroundLayer[] Layers { get; set; }

        internal static string LayerKey(int i) => "background.layers." + i.ToString(CultureInfo.InvariantCulture);
        internal static string MaskKey(int layer, int mask) => LayerKey(layer) + ".mask." + mask.ToString(CultureInfo.InvariantCulture);
        internal bool IsValid() => Layers?.Length is > 0 and <= MAX_LAYERS && Layers.All(layer => layer?.IsValid(false) == true);
    }

    public sealed class MaterialBackgroundLayer
    {
        public string Type { get; set; }
        public MaterialSpriteSlot Image { get; set; }
        public float[] Size { get; set; }
        public float[] Scale { get; set; }
        public float[] Position { get; set; }
        public float[] Pivot { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public float[] Rotation { get; set; }
        public float Depth { get; set; }
        public float[] Color { get; set; }
        public string Fit { get; set; }
        public MaterialBackgroundLayer[] Masks { get; set; }
        public float Cutoff { get; set; } = .5f;

        internal bool IsValid(bool mask)
        {
            if (Type == "image")
            {
                if (Image?.Hash?.Length != 64 || !Image.Hash.All(Uri.IsHexDigit) || !Image.IsValid(MaterialBackgroundScene.SLOT)) return false;
                MaterialPackManifest.CheckPath(Image.File);
            }
            else if (Type != "jacket" || Image != null || mask) return false;
            return MaterialSpriteSlot.PositivePair(Size, 8192) && MaterialSpriteSlot.PositivePair(Scale, 64)
                && MaterialSpriteSlot.PairInRange(Position, -8192, 8192) && MaterialSpriteSlot.PairInRange(Pivot, 0, 1)
                && Depth is >= -8192 and <= 8192 && Fit is null or "cover" or "contain"
                && (Rotation == null || Rotation.Length == 3 && Rotation.All(v => v is >= -360 and <= 360))
                && (Color == null || Color.Length == 4 && Color.All(v => v is >= 0 and <= 1))
                && Cutoff is >= 0 and <= 1 && (Masks == null || !mask && Masks.Length is > 0 and <= 16 && Masks.All(m => m?.IsValid(true) == true));
        }
    }
}
