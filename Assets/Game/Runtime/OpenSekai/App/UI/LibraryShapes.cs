using System.Collections.Generic;
using UnityEngine;

namespace OpenSekai.App.UI
{
    internal enum LibraryIcon
    {
        Back, Search, Add, Refresh, Settings, List, Grid, Check, Wave, Play, More,
        Close, Minus, ChevronRight, Export, Trash, Info, Forward,
        Heart, Comment, Clock, Trophy, Tag, Globe, Download, Browser, User, Bundle
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
        private const int HEXAGON_SIZE = 320;

        private static readonly Dictionary<int, Sprite> _roundedRects = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> _hexagons = new Dictionary<int, Sprite>();
        private static readonly Dictionary<long, Sprite> _rings = new Dictionary<long, Sprite>();
        private static readonly Dictionary<long, Sprite> _shadows = new Dictionary<long, Sprite>();
        private static readonly Dictionary<LibraryIcon, Sprite> _icons = new Dictionary<LibraryIcon, Sprite>();
        private static readonly Dictionary<long, Sprite> _awardPills = new Dictionary<long, Sprite>();
        private static readonly Dictionary<byte, Sprite> _ditheredBands = new Dictionary<byte, Sprite>();
        private static Sprite _glow;
        private static Sprite _band;
        private static Sprite _chevron;

        /// <summary>Filled rectangle with the given corner radius, in canvas units.</summary>
        internal static Sprite RoundedRect(float radius)
        {
            int pixels = Mathf.Max(1, Mathf.RoundToInt(radius * SCALE));
            if (!_roundedRects.TryGetValue(pixels, out Sprite sprite) || sprite == null)
            {
                sprite = CreateSliced("RoundedRect" + pixels, pixels, 0f);
                _roundedRects[pixels] = sprite;
            }

            return sprite;
        }

        /// <summary>Outline of a rounded rectangle, stroked inward from its edge.</summary>
        internal static Sprite Ring(float radius, float thickness)
        {
            int pixels = Mathf.Max(1, Mathf.RoundToInt(radius * SCALE));
            int stroke = Mathf.Max(1, Mathf.RoundToInt(thickness * SCALE));
            long key = ((long)pixels << 32) | (uint)stroke;
            if (!_rings.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = CreateSliced("Ring" + pixels + "x" + stroke, pixels, stroke);
                _rings[key] = sprite;
            }

            return sprite;
        }

        internal static Sprite Shadow(float radius, float blur)
        {
            int corner = Mathf.RoundToInt(radius * SCALE);
            int spread = Mathf.RoundToInt(blur * SCALE);
            long key = ((long)corner << 32) | (uint)spread;
            if (_shadows.TryGetValue(key, out Sprite shadow) && shadow != null)
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
            _shadows[key] = shadow;
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

        /// <summary>
        /// Horizontal strip, solid through the middle and fading out toward both ends: the band
        /// and the edge lines of a banner. Stretch it to any size.
        /// </summary>
        internal static Sprite Band()
        {
            if (_band == null)
            {
                const int WIDTH = 512;
                const float FADE = 0.3f;
                var pixels = new Color32[WIDTH];
                for (int x = 0; x < WIDTH; x++)
                {
                    float t = (x + 0.5f) / WIDTH;
                    float edge = Mathf.Clamp01(Mathf.Min(t, 1f - t) / FADE);
                    float alpha = edge * edge * (3f - 2f * edge);
                    pixels[x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }

                Texture2D texture = CreateTexture("Band", WIDTH, 1, pixels);
                _band = Sprite.Create(texture, new Rect(0f, 0f, WIDTH, 1f), new Vector2(0.5f, 0.5f), 100f);
            }

            return _band;
        }

        /// <summary>
        /// <see cref="Band"/> with <paramref name="opacity"/> baked in and the ramp dithered: a faint
        /// band tinted by alpha alone shows each 8-bit step as a stripe. Tint it with an opaque color.
        /// </summary>
        internal static Sprite DitheredBand(byte opacity)
        {
            if (_ditheredBands.TryGetValue(opacity, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int WIDTH = 1024;
            const int HEIGHT = 48;
            const float FADE = 0.3f;
            var pixels = new Color32[WIDTH * HEIGHT];
            // A fixed seed keeps the dither identical between runs, so screenshots stay comparable.
            var random = new System.Random(20261123 + opacity);
            for (int x = 0; x < WIDTH; x++)
            {
                float t = (x + 0.5f) / WIDTH;
                float edge = Mathf.Clamp01(Mathf.Min(t, 1f - t) / FADE);
                float alpha = edge * edge * (3f - 2f * edge) * opacity;
                for (int y = 0; y < HEIGHT; y++)
                {
                    float dithered = alpha + (float)random.NextDouble() - 0.5f;
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(dithered), 0, opacity));
                }
            }

            Texture2D texture = CreateTexture("Band" + opacity, WIDTH, HEIGHT, pixels);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f), 100f);
            _ditheredBands[opacity] = sprite;
            return sprite;
        }

        /// <summary>
        /// Pill in an award's own colors, drawn at exactly <paramref name="size"/> canvas units: ALL
        /// PERFECT runs its prism across, FULL COMBO a gold ramp upward. Both carry a gloss over the
        /// upper half and a bright rim. Use it untinted.
        /// </summary>
        internal static Sprite AwardPill(bool allPerfect, Vector2 size)
        {
            int width = Mathf.Max(2, Mathf.RoundToInt(size.x * SCALE));
            int height = Mathf.Max(2, Mathf.RoundToInt(size.y * SCALE));
            long key = ((long)width << 32) | ((long)height << 1) | (allPerfect ? 1L : 0L);
            if (_awardPills.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            float radius = height * 0.5f;
            float rim = 1.4f * SCALE;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, width - radius);
                    float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - radius) * (py - radius));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    float u = px / width;
                    float v = py / height;
                    Color color = allPerfect
                        ? LibraryTheme.Colors.AllPerfectAt(u * 0.85f + (1f - v) * 0.15f)
                        : Color.Lerp(LibraryTheme.Colors.FullComboBottom, LibraryTheme.Colors.FullComboTop, v);
                    float gloss = Mathf.SmoothStep(0f, 1f, (v - 0.48f) / 0.52f) * 0.42f;
                    float edge = Mathf.Clamp01(1f - (radius - distance) / rim);
                    color = Color.Lerp(color, Color.white, Mathf.Max(gloss, edge * 0.6f));
                    color.a = alpha;
                    pixels[y * width + x] = color;
                }
            }

            Texture2D texture = CreateTexture((allPerfect ? "AllPerfect" : "FullCombo") + width + "x" + height, width, height, pixels);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT);
            _awardPills[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Pointy-top hexagon with softened corners, filling a square sprite top to bottom. A
        /// <paramref name="stroke"/> above zero draws only its outline, that fraction of the height thick.
        /// </summary>
        internal static Sprite Hexagon(float stroke = 0f)
        {
            int key = Mathf.RoundToInt(stroke * 1000f);
            if (_hexagons.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int SIZE = HEXAGON_SIZE;
            const float ROUNDING = SIZE * 0.035f;
            float center = SIZE * 0.5f;
            // Circumradius just inside the square, so the top and bottom corners keep their antialiasing.
            float inradius = (center - 1.5f) * 0.8660254f - ROUNDING;
            float strokePixels = stroke * SIZE;
            var pixels = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float distance = HexagonDistance(x + 0.5f - center, y + 0.5f - center, inradius) - ROUNDING;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    if (strokePixels > 0f)
                    {
                        alpha -= Mathf.Clamp01(0.5f - (distance + strokePixels));
                    }

                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            Texture2D texture = CreateTexture("Hexagon" + key, SIZE, SIZE, pixels);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, SIZE, SIZE), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT);
            _hexagons[key] = sprite;
            return sprite;
        }

        /// <summary>Signed distance to a pointy-top hexagon centred on the origin.</summary>
        private static float HexagonDistance(float x, float y, float inradius)
        {
            // The flat-top fold, run on swapped axes so the corners point up and down.
            const float KX = -0.8660254f;
            const float KY = 0.5f;
            const float KZ = 0.57735027f;
            float px = Mathf.Abs(y);
            float py = Mathf.Abs(x);
            float fold = 2f * Mathf.Min(KX * px + KY * py, 0f);
            px -= fold * KX;
            py -= fold * KY;
            px -= Mathf.Clamp(px, -KZ * inradius, KZ * inradius);
            py -= inradius;
            return Mathf.Sqrt(px * px + py * py) * Mathf.Sign(py);
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
            if (_icons.TryGetValue(icon, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int VIEW_SIZE = 24;
            // The bundle also appears at 120 canvas units on the mode card.
            int scale = icon == LibraryIcon.Bundle ? 8 : SCALE;
            int size = VIEW_SIZE * scale;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2((x + 0.5f) / scale, VIEW_SIZE - (y + 0.5f) / scale);
                    float alpha = Mathf.Clamp01(0.5f - IconDistance(icon, point) * scale);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture(icon.ToString(), size, size, pixels);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f * scale);
            _icons[icon] = sprite;
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
                case LibraryIcon.Bundle:
                    // A diamond cover with two offset sheets beneath it.
                    float layer = Mathf.Min(Line(3, 7, 12, 2.5f), Line(12, 2.5f, 21, 7));
                    float Fold(float y) => Mathf.Min(Line(3, y, 12, y + 4.5f), Line(12, y + 4.5f, 21, y));
                    return Mathf.Min(Mathf.Min(layer, Fold(7)), Mathf.Min(Fold(12), Fold(17)));
                case LibraryIcon.Check:
                    return Mathf.Min(Line(5, 12, 9.5f, 16.5f), Line(9.5f, 16.5f, 19, 7));
                case LibraryIcon.Wave:
                    return Mathf.Min(Mathf.Min(Line(4, 10, 4, 14), Line(9, 5, 9, 19)),
                        Mathf.Min(Line(14, 8, 14, 16), Line(19, 3, 19, 21)));
                case LibraryIcon.Play:
                    return Mathf.Max(6f - point.x, (Mathf.Abs(point.y - 12f) - (20.5f - point.x) * 0.55f) * 0.88f);
                case LibraryIcon.Close:
                    return Mathf.Min(Line(6.5f, 6.5f, 17.5f, 17.5f), Line(17.5f, 6.5f, 6.5f, 17.5f));
                case LibraryIcon.Minus:
                    return Line(5, 12, 19, 12);
                case LibraryIcon.ChevronRight:
                    return Mathf.Min(Line(9, 5.5f, 15.5f, 12), Line(15.5f, 12, 9, 18.5f));
                case LibraryIcon.Export:
                    return Mathf.Min(
                        Mathf.Min(Line(12, 3.5f, 12, 14.5f), Mathf.Min(Line(12, 3.5f, 7.5f, 8), Line(12, 3.5f, 16.5f, 8))),
                        Mathf.Min(Line(5, 13, 5, 20), Mathf.Min(Line(5, 20, 19, 20), Line(19, 20, 19, 13))));
                case LibraryIcon.Info:
                    return Mathf.Min(Ring(12, 12, 9f), Mathf.Min(Line(12, 11, 12, 16.5f), Circle(12, 7.4f, 1.35f)));
                case LibraryIcon.Forward:
                    return Mathf.Min(Line(5, 12, 19, 12),
                        Mathf.Min(Line(19, 12, 13, 6), Line(19, 12, 13, 18)));
                case LibraryIcon.Trash:
                    return Mathf.Min(
                        Mathf.Min(Line(4, 6.5f, 20, 6.5f), Line(9.5f, 3.5f, 14.5f, 3.5f)),
                        Mathf.Min(Mathf.Min(Line(6.5f, 6.5f, 7.5f, 20), Line(17.5f, 6.5f, 16.5f, 20)),
                            Mathf.Min(Line(7.5f, 20, 16.5f, 20), Mathf.Min(Line(10.5f, 10.5f, 10.5f, 16), Line(13.5f, 10.5f, 13.5f, 16)))));
                case LibraryIcon.Heart:
                    return Mathf.Min(Mathf.Min(Circle(8.4f, 9.6f, 4.1f), Circle(15.6f, 9.6f, 4.1f)),
                        Triangle(point, new Vector2(4.6f, 11.2f), new Vector2(19.4f, 11.2f), new Vector2(12f, 19.8f)));
                case LibraryIcon.Comment:
                    return Mathf.Min(RoundedRectRing(point, new Vector2(12f, 10.5f), new Vector2(8f, 6f), 2.5f),
                        Mathf.Min(Line(8.5f, 16.5f, 7.5f, 20.5f), Line(7.5f, 20.5f, 12.5f, 16.5f)));
                case LibraryIcon.Clock:
                    return Mathf.Min(Ring(12, 12, 8.5f), Mathf.Min(Line(12, 12, 12, 7.2f), Line(12, 12, 15.2f, 14.2f)));
                case LibraryIcon.Trophy:
                    return Mathf.Min(
                        Mathf.Min(Mathf.Max(Ring(12, 8.5f, 5f), 8.5f - point.y), Mathf.Min(Line(7, 4.5f, 17, 4.5f),
                            Mathf.Min(Line(7, 4.5f, 7, 8.5f), Line(17, 4.5f, 17, 8.5f)))),
                        Mathf.Min(Line(12, 13.5f, 12, 18.5f), Line(8, 19.5f, 16, 19.5f)));
                case LibraryIcon.Tag:
                    return Mathf.Min(
                        Mathf.Min(Mathf.Min(Line(4.5f, 4.5f, 12, 4.5f), Line(12, 4.5f, 19.5f, 12)),
                            Mathf.Min(Line(19.5f, 12, 12, 19.5f), Mathf.Min(Line(12, 19.5f, 4.5f, 12), Line(4.5f, 12, 4.5f, 4.5f)))),
                        Ring(8.6f, 8.6f, 1.4f));
                case LibraryIcon.Globe:
                    return Mathf.Min(Ring(12, 12, 8.5f),
                        Mathf.Min(Line(3.5f, 12, 20.5f, 12), Mathf.Abs(Ellipse(point, new Vector2(12f, 12f), new Vector2(4f, 8.5f))) - 1f));
                case LibraryIcon.Download:
                    return Mathf.Min(
                        Mathf.Min(Line(12, 3.5f, 12, 14.5f), Mathf.Min(Line(12, 14.5f, 7.5f, 10), Line(12, 14.5f, 16.5f, 10))),
                        Line(5, 19.5f, 19, 19.5f));
                case LibraryIcon.Browser:
                    return Mathf.Min(RoundedRectRing(point, new Vector2(12f, 12f), new Vector2(8.5f, 7.5f), 2f),
                        Line(3.5f, 9f, 20.5f, 9f));
                case LibraryIcon.User:
                    return Mathf.Min(Ring(12, 8.2f, 3.6f), Mathf.Max(Ring(12, 21.5f, 7.5f), point.y - 20.5f));
                default:
                    return Mathf.Min(Circle(5, 12, 1.9f), Mathf.Min(Circle(12, 12, 1.9f), Circle(19, 12, 1.9f)));
            }
        }

        /// <summary>Signed distance to a filled triangle (negative inside).</summary>
        private static float Triangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float edge = Mathf.Min(DistanceToSegment(point, a, b), Mathf.Min(DistanceToSegment(point, b, c), DistanceToSegment(point, c, a)));
            float Side(Vector2 p0, Vector2 p1) => (p1.x - p0.x) * (point.y - p0.y) - (p1.y - p0.y) * (point.x - p0.x);
            float s0 = Side(a, b), s1 = Side(b, c), s2 = Side(c, a);
            bool inside = (s0 >= 0f && s1 >= 0f && s2 >= 0f) || (s0 <= 0f && s1 <= 0f && s2 <= 0f);
            return inside ? -edge : edge;
        }

        /// <summary>Distance to the stroke of a rounded rectangle, one unit either side of its edge.</summary>
        private static float RoundedRectRing(Vector2 point, Vector2 center, Vector2 halfSize, float radius)
        {
            Vector2 d = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - halfSize + new Vector2(radius, radius);
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
            return Mathf.Abs(outside) - 1f;
        }

        /// <summary>Approximate signed distance to an ellipse's outline.</summary>
        private static float Ellipse(Vector2 point, Vector2 center, Vector2 radii)
        {
            Vector2 p = point - center;
            float k = new Vector2(p.x / radii.x, p.y / radii.y).magnitude;
            return (k - 1f) * Mathf.Min(radii.x, radii.y);
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
            const int SIZE = 20 * SCALE;
            const float HALF_STROKE = 1.3f * SCALE;
            var pixels = new Color32[SIZE * SIZE];
            // A "v": two strokes from the upper corners down to the middle.
            var left = new Vector2(SIZE * 0.22f, SIZE * 0.64f);
            var right = new Vector2(SIZE * 0.78f, SIZE * 0.64f);
            var bottom = new Vector2(SIZE * 0.5f, SIZE * 0.36f);
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Mathf.Min(DistanceToSegment(point, left, bottom), DistanceToSegment(point, right, bottom));
                    float alpha = Mathf.Clamp01(HALF_STROKE - distance + 0.5f);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture("Chevron", SIZE, SIZE, pixels);
            return Sprite.Create(texture, new Rect(0f, 0f, SIZE, SIZE), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT);
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
