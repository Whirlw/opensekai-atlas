using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The emblem's layers as sprites. They are drawn by <see cref="BrandArt"/> on worker threads
    /// the first time anything asks, then kept for the rest of the run (the library scene is
    /// rebuilt after every live, so a per-scene cache would redraw them each time). Textures are
    /// uncompressed with mipmaps: the same sprite serves the splash at full size and the header at
    /// a fifth of it.
    /// </summary>
    internal sealed class BrandSprites
    {
        private static BrandSprites _loaded;
        private static UniTaskCompletionSource<BrandSprites> _loading;

        private readonly Dictionary<BrandArt.Part, Sprite> _parts;

        private BrandSprites(Dictionary<BrandArt.Part, Sprite> parts)
        {
            _parts = parts;
        }

        internal Sprite this[BrandArt.Part part] => _parts.TryGetValue(part, out Sprite sprite) ? sprite : null;

        /// <summary>The sprites once they are drawn; null before.</summary>
        internal static BrandSprites Loaded => _loaded;

        /// <summary>Starts drawing the sprites, or joins the drawing already under way.</summary>
        internal static UniTask<BrandSprites> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_loaded != null)
            {
                return UniTask.FromResult(_loaded);
            }

            if (_loading == null)
            {
                _loading = new UniTaskCompletionSource<BrandSprites>();
                BuildAsync(_loading).Forget();
            }

            return _loading.Task.AttachExternalCancellation(cancellationToken);
        }

        private static async UniTaskVoid BuildAsync(UniTaskCompletionSource<BrandSprites> completion)
        {
            int size = TextureSize();
            var parts = (BrandArt.Part[])Enum.GetValues(typeof(BrandArt.Part));
            byte[][] pixels;
            try
            {
                var renders = new UniTask<byte[]>[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    BrandArt.Part part = parts[i];
                    renders[i] = UniTask.RunOnThreadPool(() => BrandArt.Render(part, size));
                }

                pixels = await UniTask.WhenAll(renders);
                await UniTask.SwitchToMainThread();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _loading = null;
                completion.TrySetException(exception);
                return;
            }

            var sprites = new Dictionary<BrandArt.Part, Sprite>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                sprites[parts[i]] = CreateSprite("Brand-" + parts[i], size, pixels[i]);
            }

            _loaded = new BrandSprites(sprites);
            completion.TrySetResult(_loaded);
        }

        /// <summary>
        /// Large enough for the splash emblem at the screen's scale, and a power of two so the
        /// mipmaps the header samples stay clean.
        /// </summary>
        private static int TextureSize()
        {
            float scale = Mathf.Sqrt(Screen.width / 1920f * (Screen.height / 1080f));
            float needed = HomeTheme.Layout.SPLASH_EMBLEM_SIZE * Mathf.Max(1f, scale);
            return needed > 560f ? 1024 : 512;
        }

        private static Sprite CreateSprite(string name, int size, byte[] pixels)
        {
            // Uncompressed on purpose: block compression bands the soft edges and the gradient.
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(true, true);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
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
            const int length = 256;
            int width = vertical ? 4 : length;
            int height = vertical ? length : 4;
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
