using System.Collections.Generic;
using UnityEngine;

namespace OpenSekai.App.UI
{
    internal enum LibraryIcon
    {
        Back, Search, Add, Refresh, Settings, List, Grid, Check, Wave, Play, More
    }

    /// <summary>
    /// Shape sprites the library page draws with: rounded rectangles, rings, a soft glow and a
    /// chevron. They are generated once and cached, white with alpha, so an Image tints them with
    /// its color. Rounded shapes are 9-sliced: one small texture serves every size.
    /// </summary>
    internal static class LibraryShapes
    {
        // Shapes are drawn at twice the canvas size so corners stay sharp on a phone screen.
        private const int SCALE = 2;
        private const float PIXELS_PER_UNIT = 100f * SCALE;
        private const int GLOW_SIZE = 256;

        private static readonly Dictionary<int, Sprite> RoundedRects = new Dictionary<int, Sprite>();
        private static readonly Dictionary<long, Sprite> Rings = new Dictionary<long, Sprite>();
        private static readonly Dictionary<long, Sprite> Shadows = new Dictionary<long, Sprite>();
        private static readonly Dictionary<LibraryIcon, Sprite> Icons = new Dictionary<LibraryIcon, Sprite>();
        private static Sprite _glow;
        private static Sprite _chevron;

        /// <summary>Filled rectangle with the given corner radius, in canvas units.</summary>
        internal static Sprite RoundedRect(float radius)
        {
            int pixels = Mathf.Max(1, Mathf.RoundToInt(radius * SCALE));
            if (!RoundedRects.TryGetValue(pixels, out Sprite sprite) || sprite == null)
            {
                sprite = CreateSliced("RoundedRect" + pixels, pixels, 0f);
                RoundedRects[pixels] = sprite;
            }

            return sprite;
        }

        /// <summary>Outline of a rounded rectangle, stroked inward from its edge.</summary>
        internal static Sprite Ring(float radius, float thickness)
        {
            int pixels = Mathf.Max(1, Mathf.RoundToInt(radius * SCALE));
            int stroke = Mathf.Max(1, Mathf.RoundToInt(thickness * SCALE));
            long key = ((long)pixels << 32) | (uint)stroke;
            if (!Rings.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = CreateSliced("Ring" + pixels + "x" + stroke, pixels, stroke);
                Rings[key] = sprite;
            }

            return sprite;
        }

        internal static Sprite Shadow(float radius, float blur)
        {
            int corner = Mathf.RoundToInt(radius * SCALE);
            int spread = Mathf.RoundToInt(blur * SCALE);
            long key = ((long)corner << 32) | (uint)spread;
            if (Shadows.TryGetValue(key, out Sprite shadow) && shadow != null)
            {
                return shadow;
            }

            int border = corner + spread;
            int size = border * 2 + 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - Mathf.Clamp(x + 0.5f, border, size - border);
                    float dy = y + 0.5f - Mathf.Clamp(y + 0.5f, border, size - border);
                    float distance = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dy * dy) - corner);
                    float fade = 1f - Mathf.Clamp01(distance / spread);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade * fade));
                }
            }

            Texture2D texture = CreateTexture("Shadow" + corner + "x" + spread, size, size, pixels);
            shadow = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            Shadows[key] = shadow;
            return shadow;
        }

        /// <summary>Round blob that fades to nothing at its edge; scale it up for a background glow.</summary>
        internal static Sprite Glow()
        {
            if (_glow == null)
            {
                _glow = CreateGlow();
            }

            return _glow;
        }

        /// <summary>Small downward chevron for dropdown buttons.</summary>
        internal static Sprite Chevron()
        {
            if (_chevron == null)
            {
                _chevron = CreateChevron();
            }

            return _chevron;
        }

        internal static Sprite Icon(LibraryIcon icon)
        {
            if (Icons.TryGetValue(icon, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int viewSize = 24;
            const int size = viewSize * SCALE;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2((x + 0.5f) / SCALE, viewSize - (y + 0.5f) / SCALE);
                    float alpha = Mathf.Clamp01(0.5f - IconDistance(icon, point) * SCALE);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture(icon.ToString(), size, size, pixels);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT);
            Icons[icon] = sprite;
            return sprite;
        }

        private static float IconDistance(LibraryIcon icon, Vector2 point)
        {
            float Line(float ax, float ay, float bx, float by) =>
                DistanceToSegment(point, new Vector2(ax, ay), new Vector2(bx, by)) - 1.05f;
            float Circle(float x, float y, float radius) =>
                Vector2.Distance(point, new Vector2(x, y)) - radius;
            float Ring(float x, float y, float radius) => Mathf.Abs(Circle(x, y, radius)) - 1f;
            float Box(float x, float y)
            {
                Vector2 delta = point - new Vector2(x, y);
                var edge = new Vector2(Mathf.Max(0f, Mathf.Abs(delta.x) - 1.9f),
                    Mathf.Max(0f, Mathf.Abs(delta.y) - 1.9f));
                return Mathf.Abs(edge.magnitude - 1.4f) - 1f;
            }

            switch (icon)
            {
                case LibraryIcon.Back:
                    return Mathf.Min(Line(5, 12, 19, 12),
                        Mathf.Min(Line(5, 12, 11, 6), Line(5, 12, 11, 18)));
                case LibraryIcon.Search:
                    return Mathf.Min(Ring(10.5f, 10.5f, 6.5f), Line(15.5f, 15.5f, 20f, 20f));
                case LibraryIcon.Add:
                    return Mathf.Min(Line(12, 5, 12, 19), Line(5, 12, 19, 12));
                case LibraryIcon.Refresh:
                    return RefreshDistance(point);
                case LibraryIcon.Settings:
                    return Mathf.Min(Mathf.Min(Line(4, 7, 13, 7), Line(19, 7, 21, 7)),
                        Mathf.Min(Mathf.Min(Line(4, 17, 5, 17), Line(11, 17, 21, 17)),
                            Mathf.Min(Ring(16, 7, 2), Ring(8, 17, 2))));
                case LibraryIcon.List:
                    return Mathf.Min(Line(4, 6, 20, 6), Mathf.Min(Line(4, 12, 20, 12), Line(4, 18, 20, 18)));
                case LibraryIcon.Grid:
                    return Mathf.Min(Mathf.Min(Box(7.2f, 7.2f), Box(16.8f, 7.2f)),
                        Mathf.Min(Box(7.2f, 16.8f), Box(16.8f, 16.8f)));
                case LibraryIcon.Check:
                    return Mathf.Min(Line(5, 12, 9.5f, 16.5f), Line(9.5f, 16.5f, 19, 7));
                case LibraryIcon.Wave:
                    return Mathf.Min(Mathf.Min(Line(4, 10, 4, 14), Line(9, 5, 9, 19)),
                        Mathf.Min(Line(14, 8, 14, 16), Line(19, 3, 19, 21)));
                case LibraryIcon.Play:
                    return Mathf.Max(6f - point.x, (Mathf.Abs(point.y - 12f) - (20.5f - point.x) * 0.55f) * 0.88f);
                default:
                    return Mathf.Min(Circle(5, 12, 1.9f), Mathf.Min(Circle(12, 12, 1.9f), Circle(19, 12, 1.9f)));
            }
        }

        private static float RefreshDistance(Vector2 point)
        {
            Vector2 center = new Vector2(12f, 12f);
            Vector2 arcStart = new Vector2(20f, 12f);
            Vector2 arcEnd = new Vector2(17.4f, 6.1f);
            Vector2 direction = point - center;
            Vector2 end = arcEnd - center;

            // In the upper-right gap the nearest point on the arc is one of its rounded ends.
            bool inGap = direction.y < 0f && direction.x * end.y - direction.y * end.x < 0f;
            float arc = inGap
                ? Mathf.Min(Vector2.Distance(point, arcStart), Vector2.Distance(point, arcEnd))
                : Mathf.Abs(direction.magnitude - 8f);

            Vector2 arrowCorner = new Vector2(20f, 9f);
            float arrow = Mathf.Min(
                DistanceToSegment(point, new Vector2(20f, 4f), arrowCorner),
                DistanceToSegment(point, arrowCorner, new Vector2(15f, 9f)));
            return Mathf.Min(arc, arrow) - 1f;
        }

        private static Sprite CreateSliced(string name, int radius, float stroke)
        {
            // Two extra pixels in the middle give the 9-slice a center to stretch.
            int size = radius * 2 + 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (stroke > 0f)
                    {
                        alpha *= Mathf.Clamp01(distance - (radius - stroke) + 0.5f);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture(name, size, size, pixels);
            return Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateGlow()
        {
            var pixels = new Color32[GLOW_SIZE * GLOW_SIZE];
            float center = GLOW_SIZE * 0.5f;
            // A fixed seed keeps the dither identical between runs, so screenshots stay comparable.
            var random = new System.Random(20261116);
            for (int y = 0; y < GLOW_SIZE; y++)
            {
                for (int x = 0; x < GLOW_SIZE; x++)
                {
                    float dx = (x + 0.5f - center) / center;
                    float dy = (y + 0.5f - center) / center;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = falloff * falloff * (3f - 2f * falloff) * 255f;
                    // Dither the 8-bit ramp: a smooth dark gradient stretched over the screen bands
                    // visibly without it.
                    alpha += (float)random.NextDouble() - 0.5f;
                    pixels[y * GLOW_SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255));
                }
            }

            Texture2D texture = CreateTexture("Glow", GLOW_SIZE, GLOW_SIZE, pixels);
            return Sprite.Create(texture, new Rect(0f, 0f, GLOW_SIZE, GLOW_SIZE), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateChevron()
        {
            const int size = 20 * SCALE;
            const float halfStroke = 1.3f * SCALE;
            var pixels = new Color32[size * size];
            // A "v": two strokes from the upper corners down to the middle.
            var left = new Vector2(size * 0.22f, size * 0.64f);
            var right = new Vector2(size * 0.78f, size * 0.64f);
            var bottom = new Vector2(size * 0.5f, size * 0.36f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Mathf.Min(DistanceToSegment(point, left, bottom), DistanceToSegment(point, right, bottom));
                    float alpha = Mathf.Clamp01(halfStroke - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture("Chevron", size, size, pixels);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, a + segment * t);
        }

        private static Texture2D CreateTexture(string name, int width, int height, Color32[] pixels)
        {
            // Uncompressed on purpose: block compression bands soft alpha ramps.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "LibraryShape-" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
