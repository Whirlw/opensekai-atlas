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
        private static Sprite _streak;
        private static Sprite _beam;

        /// <summary>White, transparent at the top edge and opaque at the bottom.</summary>
        internal static Sprite FadeDown() => _fadeDown != null ? _fadeDown : _fadeDown = CreateRamp("FadeDown", true, false);

        /// <summary>White, opaque at the top edge and transparent at the bottom.</summary>
        internal static Sprite FadeUp() => _fadeUp != null ? _fadeUp : _fadeUp = CreateRamp("FadeUp", true, true);

        /// <summary>White, opaque at the left edge and transparent at the right.</summary>
        internal static Sprite FadeRight() => _fadeRight != null ? _fadeRight : _fadeRight = CreateRamp("FadeRight", false, false);

        /// <summary>
        /// A shooting star lying along x: a bright head at the right end and a tail thinning and
        /// fading to nothing at the left. Its pivot is the head.
        /// </summary>
        internal static Sprite Streak() => _streak != null ? _streak : _streak = CreateStreak();

        /// <summary>
        /// A beam of light lying along x from its pivot at the left: it fades in, holds, and fades
        /// out toward the right end, soft across its width.
        /// </summary>
        internal static Sprite Beam() => _beam != null ? _beam : _beam = CreateBeam();

        private static Sprite CreateStreak()
        {
            const int WIDTH = 256;
            const int HEIGHT = 24;
            const float HEAD = 0.92f;
            var pixels = new Color32[WIDTH * HEIGHT];
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int x = 0; x < WIDTH; x++)
                {
                    float u = (x + 0.5f) / WIDTH;
                    float v = Mathf.Abs((y + 0.5f) / HEIGHT - 0.5f) * 2f;
                    // The tail: as wide as the head at the head, a hairline at the far end.
                    float along = Mathf.Clamp01(u / HEAD);
                    float width = Mathf.Lerp(0.08f, 0.42f, along * along);
                    float tail = Mathf.Clamp01((width - v) * HEIGHT * 0.5f) * along * along * (u <= HEAD ? 1f : 0f);
                    float dx = (u - HEAD) * WIDTH / HEIGHT;
                    float head = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + v * v) / 0.5f);
                    float alpha = Mathf.Max(tail, head * head * (3f - 2f * head));
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return CreateSprite("Streak", WIDTH, HEIGHT, pixels, new Vector2(HEAD, 0.5f));
        }

        private static Sprite CreateBeam()
        {
            const int WIDTH = 256;
            const int HEIGHT = 16;
            var pixels = new Color32[WIDTH * HEIGHT];
            // A fixed seed keeps the dither identical between runs, so screenshots stay comparable.
            var random = new System.Random(20261124);
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int x = 0; x < WIDTH; x++)
                {
                    float u = (x + 0.5f) / WIDTH;
                    float v = Mathf.Abs((y + 0.5f) / HEIGHT - 0.5f) * 2f;
                    float along = Mathf.SmoothStep(0f, 1f, u / 0.2f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.75f);
                    float across = Mathf.Clamp01(1f - v);
                    float alpha = along * across * across * 255f + (float)random.NextDouble() - 0.5f;
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
                }
            }

            return CreateSprite("Beam", WIDTH, HEIGHT, pixels, new Vector2(0f, 0.5f));
        }

        private static Sprite CreateSprite(string name, int width, int height, Color32[] pixels, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HomeShape-" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, 100f, 0, SpriteMeshType.FullRect);
        }

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
