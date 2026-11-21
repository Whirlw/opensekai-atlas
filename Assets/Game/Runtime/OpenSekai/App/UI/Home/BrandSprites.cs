using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenSekai.App.UI
{
    internal static class BrandSprites
    {
        internal static readonly IReadOnlyDictionary<BrandArt.Part, Sprite> Layers = Load();

        private static Dictionary<BrandArt.Part, Sprite> Load()
        {
            var layers = new Dictionary<BrandArt.Part, Sprite>();
            foreach (Sprite sprite in Sekai.ResourceCatalog.LoadHomeEmblemLayers())
            {
                layers.Add((BrandArt.Part)Enum.Parse(typeof(BrandArt.Part), sprite.name), sprite);
            }

            return layers;
        }
    }

    /// <summary>Small generated sprites the home page needs beyond <see cref="LibraryShapes"/>.</summary>
    internal static class HomeShapes
    {
        private static Sprite _fadeDown;
        private static Sprite _fadeUp;
        private static Sprite _fadeRight;

        /// <summary>White, transparent at the top edge and opaque at the bottom.</summary>
        internal static Sprite FadeDown() => _fadeDown != null ? _fadeDown : _fadeDown = CreateRamp("FadeDown", true, false);

        /// <summary>White, opaque at the top edge and transparent at the bottom.</summary>
        internal static Sprite FadeUp() => _fadeUp != null ? _fadeUp : _fadeUp = CreateRamp("FadeUp", true, true);

        /// <summary>White, opaque at the left edge and transparent at the right.</summary>
        internal static Sprite FadeRight() => _fadeRight != null ? _fadeRight : _fadeRight = CreateRamp("FadeRight", false, false);

        private static Sprite CreateRamp(string name, bool vertical, bool reverse)
        {
            const int LENGTH = 256;
            int width = vertical ? 4 : LENGTH;
            int height = vertical ? LENGTH : 4;
            var pixels = new Color32[width * height];
            // A fixed seed keeps the dither identical between runs, so screenshots stay comparable.
            var random = new System.Random(20261117);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // t is 1 at the opaque end: the bottom row (y = 0) or the left column (x = 0).
                    float t = vertical ? 1f - (y + 0.5f) / height : 1f - (x + 0.5f) / width;
                    if (reverse)
                    {
                        t = 1f - t;
                    }

                    float eased = t * t * (3f - 2f * t);
                    float alpha = eased * 255f + (float)random.NextDouble() - 0.5f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HomeShape-" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
