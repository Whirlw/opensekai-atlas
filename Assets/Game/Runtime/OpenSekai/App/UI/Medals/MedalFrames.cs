using System.Collections.Generic;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The <see cref="MedalArt"/> frames and the 段位 pattern as sprites. A frame depends on its tier
    /// and the plate's size, never on the medal's color, so the pages share a handful of textures;
    /// they are kept for the app's life. Call from the main thread.
    /// </summary>
    internal static class MedalFrames
    {
        /// <summary>Device pixels per canvas unit on the largest phone screens, as for the Ranking Score crests.</summary>
        private const float DEVICE_SCALE = 1.5f;

        private static readonly Dictionary<(int Tier, int Width, int Height), Frame> _frames =
            new Dictionary<(int, int, int), Frame>();

        private static readonly Dictionary<(int Tier, int Width, int Height), Sprite> _compact =
            new Dictionary<(int, int, int), Sprite>();

        private static readonly Dictionary<int, Sprite> _patterns = new Dictionary<int, Sprite>();

        /// <summary>The frame of <paramref name="tier"/> for a plate of <paramref name="size"/> canvas units.</summary>
        internal static Frame Get(int tier, Vector2 size)
        {
            var key = (tier, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            // Leaving play mode in the editor destroys the textures.
            if (_frames.TryGetValue(key, out Frame frame) && frame.Strokes != null && frame.Lights != null)
            {
                return frame;
            }

            MedalArt.RenderFrame(tier, size.x / size.y, size.y * DEVICE_SCALE, out byte[] strokes, out byte[] lights, out int width,
                out int height);
            string name = "Medal" + tier + "-" + key.Item2 + "x" + key.Item3;
            frame = new Frame(
                CreateSprite(CreateTexture(name, width, height, strokes, TextureWrapMode.Clamp), 100f),
                CreateSprite(CreateTexture(name + "Lights", width, height, lights, TextureWrapMode.Clamp), 100f));
            _frames[key] = frame;
            return frame;
        }

        /// <summary>The frame of <paramref name="tier"/> for a medal drawn small, <paramref name="size"/> canvas units large.</summary>
        internal static Sprite Compact(int tier, Vector2 size)
        {
            var key = (tier, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            if (_compact.TryGetValue(key, out Sprite frame) && frame != null && frame.texture != null)
            {
                return frame;
            }

            byte[] pixels = MedalArt.RenderCompactFrame(tier, size.x / size.y, size.y * DEVICE_SCALE, out int width, out int height);
            frame = CreateSprite(CreateTexture("MedalChip" + tier + "-" + key.Item2 + "x" + key.Item3, width, height, pixels,
                TextureWrapMode.Clamp), 100f);
            _compact[key] = frame;
            return frame;
        }

        /// <summary>One tile of the 段位 pattern for a plate face <paramref name="faceHeight"/> units high; draw it tiled.</summary>
        internal static Sprite Pattern(float faceHeight)
        {
            int key = Mathf.RoundToInt(faceHeight * 4f);
            if (_patterns.TryGetValue(key, out Sprite pattern) && pattern != null && pattern.texture != null)
            {
                return pattern;
            }

            int width = Mathf.Max(8, Mathf.RoundToInt(MedalArt.PATTERN_TILE_X * faceHeight * DEVICE_SCALE));
            int height = Mathf.Max(8, Mathf.RoundToInt(MedalArt.PATTERN_TILE_Y * faceHeight * DEVICE_SCALE));
            Texture2D texture = CreateTexture("Pattern" + key, width, height, MedalArt.RenderPattern(width, height), TextureWrapMode.Repeat);
            // The tile's height in canvas units follows from its pixels per unit, against the canvas's 100.
            pattern = CreateSprite(texture, 100f * height / (MedalArt.PATTERN_TILE_Y * faceHeight));
            _patterns[key] = pattern;
            return pattern;
        }

        private static Sprite CreateSprite(Texture2D texture, float pixelsPerUnit) => Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);

        private static Texture2D CreateTexture(string name, int width, int height, byte[] pixels, TextureWrapMode wrap)
        {
            // Uncompressed on purpose: block compression bands soft alpha ramps.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            return texture;
        }

        internal readonly struct Frame
        {
            internal Frame(Sprite strokes, Sprite lights)
            {
                Strokes = strokes;
                Lights = lights;
            }

            /// <summary>The lines and ornaments, white (or in the prism); tint with the medal's color.</summary>
            internal Sprite Strokes { get; }

            /// <summary>The hearts of the ornaments, white.</summary>
            internal Sprite Lights { get; }
        }
    }
}
