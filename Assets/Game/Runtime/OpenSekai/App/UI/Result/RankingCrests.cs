using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The <see cref="RankingCrestArt"/> layers as sprites, one set per tier and pixel density.
    /// A top-tier crest at the home card's size takes longer than a frame to draw, so the pixels
    /// are worked out on a worker thread the first time a set is asked for; a badge shows its plain
    /// face until the set is in. Sets are kept for the app's life: a few small textures each.
    /// Call from the main thread.
    /// </summary>
    internal static class RankingCrests
    {
        /// <summary>
        /// Device pixels per canvas unit on the largest phone screens, rounded up, so a crest is never
        /// magnified (2800×1272 scales the 1920×1080 canvas by about 1.31; 3200×1440 by about 1.49).
        /// </summary>
        private const float DEVICE_SCALE = 1.5f;

        private const int DENSITY_STEP = 32;
        private const int SPARKLE_SIZE = 64;

        private static readonly RankingCrestArt.Layer[] _layers = (RankingCrestArt.Layer[])Enum.GetValues(typeof(RankingCrestArt.Layer));
        private static readonly Dictionary<int, Sprites> _sets = new Dictionary<int, Sprites>();
        private static readonly Dictionary<int, Task<Layer[]>> _jobs = new Dictionary<int, Task<Layer[]>>();
        private static Sprite _sparkle;

        /// <summary>True while a crest is still being drawn; screenshots wait for it.</summary>
        internal static bool IsBusy
        {
            get
            {
                foreach (Task<Layer[]> job in _jobs.Values)
                {
                    if (!job.IsCompleted)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Starts drawing <paramref name="tier"/>'s crest for a badge of <paramref name="badgeSize"/>, so it is in when needed.</summary>
        internal static void Warm(int tier, float badgeSize) => TryGet(tier, badgeSize, out _);

        /// <summary>
        /// The crest of <paramref name="tier"/> for a badge of <paramref name="badgeSize"/> canvas
        /// units. False while it is being drawn, and for 星尘, which has none.
        /// </summary>
        internal static bool TryGet(int tier, float badgeSize, out Sprites sprites)
        {
            sprites = null;
            if (!RankingCrestArt.HasCrest(tier))
            {
                return false;
            }

            int density = Mathf.Max(DENSITY_STEP, Mathf.CeilToInt(badgeSize * DEVICE_SCALE / DENSITY_STEP) * DENSITY_STEP);
            int key = tier * 100000 + density;
            if (_sets.TryGetValue(key, out sprites) && sprites.IsAlive)
            {
                return true;
            }

            if (!_jobs.TryGetValue(key, out Task<Layer[]> job))
            {
                RankingCrestArt.Rgb color = ToRgb(RankingTiers.Color(tier));
                _jobs[key] = Task.Run(() => Render(tier, color, density));
                sprites = null;
                return false;
            }

            if (!job.IsCompleted)
            {
                sprites = null;
                return false;
            }

            _jobs.Remove(key);
            if (job.IsFaulted)
            {
                // The badge keeps its plain face; nothing on the page depends on the crest.
                Debug.LogException(job.Exception);
                sprites = new Sprites(tier, new Sprite[_layers.Length]);
            }
            else
            {
                sprites = Upload(tier, density, job.Result);
            }

            _sets[key] = sprites;
            return true;
        }

        /// <summary>A soft white four-pointed sparkle, for glints and twinkling stars.</summary>
        internal static Sprite Sparkle()
        {
            if (_sparkle == null)
            {
                byte[] pixels = RankingCrestArt.RenderSparkle(SPARKLE_SIZE);
                Texture2D texture = CreateTexture("Sparkle", SPARKLE_SIZE, SPARKLE_SIZE, pixels);
                _sparkle = Sprite.Create(texture, new Rect(0f, 0f, SPARKLE_SIZE, SPARKLE_SIZE), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect);
            }

            return _sparkle;
        }

        private static Layer[] Render(int tier, RankingCrestArt.Rgb color, int density)
        {
            var layers = new Layer[_layers.Length];
            foreach (RankingCrestArt.Layer layer in _layers)
            {
                if (RankingCrestArt.Has(tier, layer))
                {
                    byte[] pixels = RankingCrestArt.Render(tier, layer, color, density, out int width, out int height);
                    layers[(int)layer] = new Layer(pixels, width, height);
                }
            }

            return layers;
        }

        private static Sprites Upload(int tier, int density, Layer[] layers)
        {
            var sprites = new Sprite[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = layers[i];
                if (layer.Pixels == null)
                {
                    continue;
                }

                RankingCrestArt.Box box = RankingCrestArt.Bounds(tier, (RankingCrestArt.Layer)i);
                Texture2D texture = CreateTexture("Crest" + tier + "-" + (RankingCrestArt.Layer)i + "-" + density, layer.Width, layer.Height,
                    layer.Pixels);
                var pivot = new Vector2((box.PivotX - box.X0) / box.Width, (box.PivotY - box.Y0) / box.Height);
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, layer.Width, layer.Height), pivot, 100f, 0, SpriteMeshType.FullRect);
            }

            return new Sprites(tier, sprites);
        }

        private static Texture2D CreateTexture(string name, int width, int height, byte[] pixels)
        {
            // Uncompressed on purpose: block compression bands soft alpha ramps.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Ranking" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            return texture;
        }

        private static RankingCrestArt.Rgb ToRgb(Color32 color) => new RankingCrestArt.Rgb(color.r, color.g, color.b);

        private readonly struct Layer
        {
            internal readonly byte[] Pixels;
            internal readonly int Width;
            internal readonly int Height;

            internal Layer(byte[] pixels, int width, int height)
            {
                Pixels = pixels;
                Width = width;
                Height = height;
            }
        }

        /// <summary>One tier's crest layers; a layer the tier does not have is null.</summary>
        internal sealed class Sprites
        {
            private readonly Sprite[] _sprites;

            internal Sprites(int tier, Sprite[] sprites)
            {
                Tier = tier;
                _sprites = sprites;
            }

            internal int Tier { get; }

            /// <summary>False when drawing failed: the badge keeps its plain face.</summary>
            internal bool HasCore => _sprites[(int)RankingCrestArt.Layer.Core] != null;

            /// <summary>False once the textures are gone (leaving play mode in the editor destroys them).</summary>
            internal bool IsAlive
            {
                get
                {
                    Sprite core = _sprites[(int)RankingCrestArt.Layer.Core];
                    // A failed set never had sprites; keep it rather than drawing again.
                    if (ReferenceEquals(core, null))
                    {
                        return true;
                    }

                    return core != null && core.texture != null;
                }
            }

            internal Sprite this[RankingCrestArt.Layer layer] => _sprites[(int)layer];
        }
    }
}
