using System;
using System.Collections.Generic;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The Ranking Score crests from 新星 up, grown from the plain 星尘 badge: the same tinted
    /// hexagon, solid outline, fine inner line and center diamond, in the same flat colors, with
    /// each tier adding to the one below it. The diamond lights into a star, gains diagonal points,
    /// a ring, four small diamonds on the ring and short rays (see <see cref="Heart"/>). Around the
    /// plaque, a row of diamonds runs out of each flank, one at 流星 and three by 星海; 银河 adds an
    /// outer frame, 星海 diamonds on the frame's corners, 天穹 sun rays and a second inner line, and
    /// 寰宇 the ALL PERFECT prism on every stroke, a second frame, an orbit like the brand mark's and
    /// a fourth flank diamond.
    /// The hexagon shrinks a little as the ornaments grow, so every crest fits the space a badge is
    /// given (a caption sits above it and text beside it on most pages).
    ///
    /// The badge moves some parts on its own, so they are separate layers: the rays turn, one flank
    /// is drawn for the badge to mirror and slide out, and the orbit is cut into the half behind the
    /// plaque and the half in front of it.
    ///
    /// Pure math with no Unity types, so it can run off the main thread. Units: the badge's square
    /// is -0.5..0.5 on both axes, y up. Output is RGBA32 with straight alpha, bottom row first
    /// (Unity's texture order).
    /// </summary>
    internal static class RankingCrestArt
    {
        internal enum Layer
        {
            /// <summary>Thin sun rays behind everything; the badge turns them slowly. 天穹 and up.</summary>
            Rays,

            /// <summary>The far half of the orbits, behind the plaque. 寰宇 only.</summary>
            OrbitBack,

            /// <summary>The row of diamonds out of the right flank; the badge mirrors it for the left. 流星 and up.</summary>
            Side,

            /// <summary>The plaque: frames, corner diamonds, outline, inner line and the heart.</summary>
            Core,

            /// <summary>The near half of the orbits, over the plaque. 寰宇 only.</summary>
            OrbitFront
        }

        /// <summary>The lowest tier drawn here; 星尘 below it keeps the plain badge.</summary>
        internal const int FIRST_TIER = 1;

        internal const int TOP_TIER = 6;

        // The plain badge's proportions, against the badge's size.
        private const float RING_STROKE = 0.045f;
        private const float INNER_SCALE = 0.8f;
        private const float INNER_STROKE = 0.02f;
        private const float ROUNDING = 0.035f;

        private const float PI = (float)Math.PI;
        private const float TAU = PI * 2f;
        private const float SIXTH = PI / 3f;

        private static readonly Rgb White = new Rgb(255, 255, 255);
        private static readonly Rgb Surface = new Rgb(21, 26, 34);

        /// <summary>The ALL PERFECT pill's stops: pink, violet, cyan.</summary>
        private static readonly Rgb[] _prism = { new Rgb(255, 146, 214), new Rgb(186, 158, 255), new Rgb(112, 224, 255) };

        private static readonly Design[] _designs = BuildDesigns();

        // ---------------------------------------------------------------- what the badge needs to know

        /// <summary>True when <paramref name="tier"/> has a crest; 星尘 keeps the plain badge.</summary>
        internal static bool HasCrest(int tier) => tier >= FIRST_TIER && tier <= TOP_TIER;

        /// <summary>The plaque's size against the badge: 1 fills the badge's square top to bottom.</summary>
        internal static float HexScale(int tier) => Get(tier).HexScale;

        /// <summary>寰宇 strokes run through the ALL PERFECT prism instead of one color.</summary>
        internal static bool IsPrismatic(int tier) => Get(tier).Prismatic;

        internal static bool Has(int tier, Layer layer)
        {
            Design design = Get(tier);
            switch (layer)
            {
                case Layer.Rays: return design.Rays > 0;
                case Layer.OrbitBack:
                case Layer.OrbitFront: return design.Orbits.Length > 0;
                case Layer.Side: return design.Side.Length > 0;
                default: return true;
            }
        }

        /// <summary>The rectangle a layer covers, in badge units, and the point it turns around.</summary>
        internal static Box Bounds(int tier, Layer layer)
        {
            Design design = Get(tier);
            switch (layer)
            {
                case Layer.Rays: return new Box(-0.66f, -0.66f, 0.66f, 0.66f, 0f, 0f);
                case Layer.OrbitBack:
                case Layer.OrbitFront: return new Box(-0.7f, -0.4f, 0.7f, 0.4f, 0f, 0f);
                case Layer.Side: return new Box(0.3f, -0.08f, 0.7f, 0.08f, design.SideRoot, 0f);
                default: return new Box(-0.54f, -0.54f, 0.54f, 0.54f, 0f, 0f);
            }
        }

        /// <summary>The orbits a satellite can ride: radii and tilt in badge units and radians.</summary>
        internal static Orbit[] Orbits(int tier) => Get(tier).Orbits;

        /// <summary>
        /// Points worth a twinkle, in badge units: the heart, the corner diamonds and the first
        /// diamond out of each flank. Higher tiers have more.
        /// </summary>
        internal static Point[] Glints(int tier)
        {
            Design design = Get(tier);
            var points = new List<Point>();
            float s = design.HexScale;
            points.Add(new Point(0.13f * s, 0.12f * s));
            if (design.Studs)
            {
                float radius = design.FrameRadius;
                points.Add(new Point(0f, -radius));
                points.Add(new Point(-radius * 0.866f, radius * 0.5f));
            }

            if (design.Side.Length > 0)
            {
                float first = design.SideRoot + design.Side[0].Offset;
                points.Add(new Point(first, 0f));
                points.Add(new Point(-first, 0f));
            }

            return points.ToArray();
        }

        /// <summary>
        /// Renders one layer of a tier's crest in <paramref name="color"/> (the tier's color) at
        /// <paramref name="pixelsPerUnit"/> pixels per badge width; <see cref="Bounds"/> says how
        /// much of the badge it covers.
        /// </summary>
        internal static byte[] Render(int tier, Layer layer, Rgb color, float pixelsPerUnit, out int width, out int height)
        {
            Design design = Get(tier);
            var ink = new Ink(design, color);
            Box box = Bounds(tier, layer);
            width = Math.Max(2, (int)Math.Ceiling(box.Width * pixelsPerUnit));
            height = Math.Max(2, (int)Math.Ceiling(box.Height * pixelsPerUnit));
            float pixel = box.Width / width;
            float pixelY = box.Height / height;
            var pixels = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                float y = box.Y0 + (row + 0.5f) * pixelY;
                for (int column = 0; column < width; column++)
                {
                    float x = box.X0 + (column + 0.5f) * pixel;
                    Paint paint;
                    switch (layer)
                    {
                        case Layer.Rays: paint = Rays(design, ink, x, y, pixel); break;
                        case Layer.OrbitBack: paint = OrbitLines(design, ink, x, y, pixel, false); break;
                        case Layer.OrbitFront: paint = OrbitLines(design, ink, x, y, pixel, true); break;
                        case Layer.Side: paint = Side(design, ink, x, y, pixel); break;
                        default: paint = Core(design, ink, x, y, pixel); break;
                    }

                    paint.Write(pixels, (row * width + column) * 4);
                }
            }

            return pixels;
        }

        /// <summary>A soft white four-pointed sparkle filling a square, for glints and twinkling stars.</summary>
        internal static byte[] RenderSparkle(int size)
        {
            var pixels = new byte[size * size * 4];
            float pixel = 1f / size;
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    float x = (column + 0.5f) * pixel - 0.5f;
                    float y = (row + 0.5f) * pixel - 0.5f;
                    float r = Length(x, y);
                    float spikes = Cover(Star4(x, y, 0.48f, 1f), pixel * 2f) * Smooth(0.5f, 0.1f, r);
                    float core = Gauss(r, 0.07f);
                    float glow = Gauss(r, 0.2f) * 0.3f;
                    Paint.Of(White, Math.Max(spikes, Math.Max(core, glow))).Write(pixels, (row * size + column) * 4);
                }
            }

            return pixels;
        }

        /// <summary>
        /// The ALL PERFECT prism, pink through violet to cyan. Positions past 1 come back down, so
        /// a prism running round a closed stroke has no seam.
        /// </summary>
        internal static Rgb Prism(float position)
        {
            float wrapped = position - (float)Math.Floor(position);
            float t = 1f - Math.Abs(wrapped * 2f - 1f);
            float scaled = t * (_prism.Length - 1);
            int stop = Math.Min((int)scaled, _prism.Length - 2);
            return Rgb.Lerp(_prism[stop], _prism[stop + 1], scaled - stop);
        }

        private static Design Get(int tier) => _designs[Math.Max(FIRST_TIER, Math.Min(TOP_TIER, tier))];

        // ---------------------------------------------------------------- the designs

        private static Design[] BuildDesigns()
        {
            var designs = new Design[TOP_TIER + 1];
            designs[1] = new Design(1, 1f);
            designs[2] = new Design(2, 0.92f)
            {
                Side = new[] { new SideDiamond(0.075f, 0.034f) }, SideReach = 0.12f
            };
            designs[3] = new Design(3, 0.88f)
            {
                Frames = 1,
                Side = new[] { new SideDiamond(0.08f, 0.036f), new SideDiamond(0.15f, 0.024f) }, SideReach = 0.19f
            };
            designs[4] = new Design(4, 0.86f)
            {
                Frames = 1, Studs = true,
                Side = new[] { new SideDiamond(0.08f, 0.038f), new SideDiamond(0.155f, 0.027f), new SideDiamond(0.215f, 0.018f) },
                SideReach = 0.25f
            };
            designs[5] = new Design(5, 0.84f)
            {
                Frames = 1, Studs = true, Rays = 16, DoubleLine = true,
                Side = new[] { new SideDiamond(0.08f, 0.04f), new SideDiamond(0.16f, 0.029f), new SideDiamond(0.225f, 0.02f) },
                SideReach = 0.27f
            };
            designs[6] = new Design(6, 0.82f)
            {
                Frames = 2, Studs = true, Rays = 24, DoubleLine = true, Prismatic = true,
                Orbits = new[] { new Orbit(0.64f, 0.15f, -0.24f) },
                Side = new[]
                {
                    new SideDiamond(0.085f, 0.042f), new SideDiamond(0.165f, 0.03f), new SideDiamond(0.23f, 0.022f),
                    new SideDiamond(0.28f, 0.014f)
                },
                SideReach = 0.31f
            };
            return designs;
        }

        // ---------------------------------------------------------------- core: frames, plaque, emblem

        private static Paint Core(Design d, Ink ink, float x, float y, float pixel)
        {
            float s = d.HexScale;
            float radius = d.Radius;
            float hex = Hexagon(x, y, radius, ROUNDING * s);
            if (d.Frames == 0 && hex > pixel)
            {
                return default;
            }

            float angle = (float)Math.Atan2(y, x);
            Paint paint = default;
            if (d.Frames > 0 && hex > -pixel)
            {
                paint = Frames(d, ink, x, y, angle, pixel);
            }

            if (hex > pixel)
            {
                return paint;
            }

            // The plain badge's plaque: a tinted fill with a solid outline, and a fine line at four fifths.
            float r = Length(x, y);
            Rgb fill = Rgb.Add(ink.Fill, ink.Glow, 0.16f * Gauss(r, 0.3f * s));
            paint = paint.Over(Paint.Of(fill, Cover(hex, pixel)));
            float inner = Hexagon(x, y, radius * INNER_SCALE, ROUNDING * s * INNER_SCALE);
            float innerLine = Math.Max(inner, -inner - INNER_STROKE * s);
            paint = paint.Over(Paint.Of(ink.Stroke(angle), Cover(innerLine, pixel) * 0.36f));
            if (d.DoubleLine)
            {
                float second = Hexagon(x, y, radius * 0.86f, ROUNDING * s * 0.86f);
                paint = paint.Over(Paint.Of(ink.Stroke(angle + 0.5f), Cover(Math.Abs(second) - 0.004f * s, pixel) * 0.28f));
            }

            if (-hex > RING_STROKE * s + pixel)
            {
                paint = paint.Over(Heart(d, ink, x, y, angle, pixel));
            }

            float ring = Math.Max(hex, -hex - RING_STROKE * s);
            paint = paint.Over(Paint.Of(ink.Stroke(angle), Cover(ring, pixel)));
            return paint;
        }

        /// <summary>The outer frame line(s) and their corner diamonds, around the plaque.</summary>
        private static Paint Frames(Design d, Ink ink, float x, float y, float angle, float pixel)
        {
            float s = d.HexScale;
            float frame = d.FrameRadius;
            Paint paint = default;
            for (int i = 0; i < d.Frames; i++)
            {
                float radius = frame + i * 0.032f * s;
                float line = Math.Abs(Hexagon(x, y, radius, ROUNDING * s)) - (i == 0 ? 0.007f : 0.004f) * s;
                paint = paint.Over(Paint.Of(ink.Stroke(angle + i * 0.35f), Cover(line, pixel) * (i == 0 ? 0.75f : 0.5f)));
            }

            if (!d.Studs)
            {
                return paint;
            }

            // Only the nearest corner can be under the pixel: corners sit 60° apart.
            float corner = (float)Math.Round((angle - PI * 0.5f) / SIXTH) * SIXTH + PI * 0.5f;
            float cx = x - (float)Math.Cos(corner) * frame;
            float cy = y - (float)Math.Sin(corner) * frame;
            return paint.Over(Diamond(ink, cx, cy, 0.036f * s, pixel, corner));
        }

        /// <summary>A flat diamond in the tier's color with a light heart, as on the plain badge but brighter.</summary>
        private static Paint Diamond(Ink ink, float x, float y, float size, float pixel, float hue)
        {
            float distance = (Math.Abs(x) + Math.Abs(y) - size) * 0.7071f;
            if (distance > pixel)
            {
                return default;
            }

            float heart = (Math.Abs(x) + Math.Abs(y) - size * 0.42f) * 0.7071f;
            Rgb color = Rgb.Lerp(ink.Stroke(hue), ink.Light, Cover(heart, pixel));
            return Paint.Of(color, Cover(distance, pixel));
        }

        // ---------------------------------------------------------------- the heart

        /// <summary>
        /// The plain badge's diamond, grown one step per tier: 新星 lights a four-pointed star behind
        /// it, 流星 adds short diagonal points, 银河 a ring round the star, 星海 four small diamonds
        /// riding the ring, 天穹 short rays outside it, and 寰宇 a second ring, all in the prism.
        /// </summary>
        private static Paint Heart(Design d, Ink ink, float x, float y, float angle, float pixel)
        {
            float s = d.HexScale;
            int tier = d.Tier;
            float r = Length(x, y);
            Paint paint = Paint.Of(ink.Glow, (0.3f + 0.04f * tier) * Gauss(r, 0.15f * s));

            if (tier >= 5)
            {
                // Short rays between the ring and the inner line, long and short in turn.
                float step = TAU / 16f;
                float turn = angle - PI * 0.5f;
                float index = (float)Math.Round(turn / step);
                float offset = Math.Abs(turn - index * step) * r - 0.0045f * s;
                bool isLong = ((int)index & 1) == 0;
                float reach = (isLong ? 0.27f : 0.235f) * s;
                float ray = Cover(offset, pixel) * Smooth(reach, reach - 0.02f * s, r) * Smooth(0.18f * s, 0.2f * s, r);
                paint = paint.Over(Paint.Of(ink.Stroke(angle), ray * (isLong ? 0.75f : 0.45f)));
            }

            if (tier >= 3)
            {
                float ring = Math.Abs(r - 0.15f * s) - 0.006f * s;
                paint = paint.Over(Paint.Of(ink.Stroke(angle), Cover(ring, pixel) * 0.8f));
                if (tier >= 6)
                {
                    float outer = Math.Abs(r - 0.185f * s) - 0.0035f * s;
                    paint = paint.Over(Paint.Of(ink.Stroke(angle + PI), Cover(outer, pixel) * 0.55f));
                }
            }

            if (tier >= 2)
            {
                Rotate(x, y, PI * 0.25f, out float rx, out float ry);
                paint = paint.Over(Paint.Of(ink.Stroke(angle + 1f), Cover(Star4(rx, ry, 0.12f * s, 1.1f), pixel) * 0.6f));
            }

            paint = paint.Over(Paint.Of(ink.Stroke(angle), Cover(Star4(x, y, 0.22f * s, 1.12f), pixel)));

            if (tier >= 4)
            {
                // Four diamonds riding the ring between the star's points.
                float corner = (float)Math.Round((angle - PI * 0.25f) / (PI * 0.5f)) * (PI * 0.5f) + PI * 0.25f;
                float cx = x - (float)Math.Cos(corner) * 0.15f * s;
                float cy = y - (float)Math.Sin(corner) * 0.15f * s;
                paint = paint.Over(Diamond(ink, cx, cy, 0.028f * s, pixel, corner));
            }

            return paint.Over(Diamond(ink, x, y, 0.07f * s, pixel, angle));
        }

        // ---------------------------------------------------------------- flanks

        /// <summary>
        /// Out of the right flank: a fine line fading outward with a row of diamonds on it, the
        /// nearest largest, like the plain badge's diamond carried outside the plaque.
        /// </summary>
        private static Paint Side(Design d, Ink ink, float x, float y, float pixel)
        {
            float along = x - d.SideRoot;
            float line = Math.Abs(y) - 0.0045f;
            float fade = Smooth(d.SideReach, d.SideReach * 0.55f, along) * Smooth(-0.01f, 0.02f, along);
            Paint paint = Paint.Of(ink.Stroke(along * 9f), Cover(line, pixel) * fade * 0.8f);
            for (int i = 0; i < d.Side.Length; i++)
            {
                SideDiamond diamond = d.Side[i];
                paint = paint.Over(Diamond(ink, along - diamond.Offset, y, diamond.Size, pixel, 1.2f + i * 1.1f));
            }

            return paint;
        }

        // ---------------------------------------------------------------- rays and orbits

        /// <summary>Thin sun rays, long and short in turn, beyond the plaque.</summary>
        private static Paint Rays(Design d, Ink ink, float x, float y, float pixel)
        {
            float r = Length(x, y);
            if (r < 0.3f || r > 0.66f)
            {
                return default;
            }

            float angle = (float)Math.Atan2(y, x) - PI * 0.5f;
            float step = TAU / d.Rays;
            float index = (float)Math.Round(angle / step);
            float offset = Math.Abs(angle - index * step) * r;
            bool isLong = ((int)index & 1) == 0;
            float reach = isLong ? 0.64f : 0.56f;
            float line = offset - (isLong ? 0.006f : 0.0045f);
            float alpha = Cover(line, pixel) * Smooth(reach, reach - 0.12f, r) * Smooth(0.34f, 0.44f, r);
            return Paint.Of(ink.Stroke(angle + PI * 0.5f), alpha * (isLong ? 0.8f : 0.5f));
        }

        /// <summary>The orbit lines, cut into the half behind the plaque and the half in front.</summary>
        private static Paint OrbitLines(Design d, Ink ink, float x, float y, float pixel, bool front)
        {
            Paint paint = default;
            for (int i = 0; i < d.Orbits.Length; i++)
            {
                Orbit orbit = d.Orbits[i];
                Rotate(x, y, orbit.Tilt, out float u, out float v);
                float line = Math.Abs(Ellipse(u, v, orbit.RadiusX, orbit.RadiusY)) - (i == 0 ? 0.0065f : 0.005f);
                if (line > pixel)
                {
                    continue;
                }

                float near = Smooth(pixel, -pixel, v);
                float share = front ? near : 1f - near;
                float hue = (float)Math.Atan2(v / orbit.RadiusY, u / orbit.RadiusX);
                paint = paint.Over(Paint.Of(ink.Stroke(hue + i), Cover(line, pixel) * share * (i == 0 ? 0.85f : 0.65f)));
            }

            return paint;
        }

        // ---------------------------------------------------------------- shapes

        /// <summary>Signed distance to a pointy-top hexagon of the given circumradius with rounded corners.</summary>
        private static float Hexagon(float x, float y, float radius, float rounding)
        {
            const float KX = -0.8660254f;
            const float KY = 0.5f;
            const float KZ = 0.57735027f;
            float inradius = radius * 0.8660254f - rounding;
            float px = Math.Abs(y);
            float py = Math.Abs(x);
            float fold = 2f * Math.Min(KX * px + KY * py, 0f);
            px -= fold * KX;
            py -= fold * KY;
            px -= Clamp(px, -KZ * inradius, KZ * inradius);
            py -= inradius;
            return (float)Math.Sqrt(px * px + py * py) * Math.Sign(py) - rounding;
        }

        /// <summary>
        /// A four-pointed sparkle with curved sides, points up, down and sideways: a square with a
        /// circle cut from each corner. <paramref name="fullness"/> 1 gives slim points; more fattens the waist.
        /// </summary>
        private static float Star4(float x, float y, float radius, float fullness)
        {
            float qx = Math.Abs(x);
            float qy = Math.Abs(y);
            float box = Math.Max(qx, qy) - radius;
            float center = radius * fullness;
            float bite = (float)Math.Sqrt((radius - center) * (radius - center) + center * center);
            return Math.Max(box, bite - Length(qx - center, qy - center));
        }

        /// <summary>Approximate signed distance to an axis-aligned ellipse; close enough for thin lines.</summary>
        private static float Ellipse(float x, float y, float rx, float ry)
        {
            float k0 = Length(x / rx, y / ry);
            float k1 = Length(x / (rx * rx), y / (ry * ry));
            return k1 <= 0f ? -Math.Min(rx, ry) : k0 * (k0 - 1f) / k1;
        }

        // ---------------------------------------------------------------- helpers

        private static void Rotate(float x, float y, float angle, out float u, out float v)
        {
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            u = x * cos + y * sin;
            v = -x * sin + y * cos;
        }

        private static float Cover(float distance, float pixel) => Clamp01(0.5f - distance / pixel);

        private static float Gauss(float distance, float width) => (float)Math.Exp(-(distance * distance) / (width * width));

        private static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        // ---------------------------------------------------------------- types

        /// <summary>A layer's rectangle in badge units and the point it turns around.</summary>
        internal readonly struct Box
        {
            internal readonly float X0;
            internal readonly float Y0;
            internal readonly float X1;
            internal readonly float Y1;
            internal readonly float PivotX;
            internal readonly float PivotY;

            internal Box(float x0, float y0, float x1, float y1, float pivotX, float pivotY)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
                PivotX = pivotX;
                PivotY = pivotY;
            }

            internal float Width => X1 - X0;
            internal float Height => Y1 - Y0;
        }

        internal readonly struct Point
        {
            internal readonly float X;
            internal readonly float Y;

            internal Point(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>A tilted ellipse around the badge, in badge units; <see cref="Tilt"/> is in radians, counterclockwise.</summary>
        internal readonly struct Orbit
        {
            internal readonly float RadiusX;
            internal readonly float RadiusY;
            internal readonly float Tilt;

            internal Orbit(float radiusX, float radiusY, float tilt)
            {
                RadiusX = radiusX;
                RadiusY = radiusY;
                Tilt = tilt;
            }

            /// <summary>The point at <paramref name="angle"/> round the orbit; its far half has y above the orbit's center line.</summary>
            internal Point At(float angle)
            {
                float u = (float)Math.Cos(angle) * RadiusX;
                float v = (float)Math.Sin(angle) * RadiusY;
                float cos = (float)Math.Cos(Tilt);
                float sin = (float)Math.Sin(Tilt);
                return new Point(u * cos - v * sin, u * sin + v * cos);
            }
        }

        /// <summary>Color in 0..1 sRGB components.</summary>
        internal readonly struct Rgb
        {
            internal readonly float R;
            internal readonly float G;
            internal readonly float B;

            internal Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }

            internal Rgb(int r, int g, int b) : this(r / 255f, g / 255f, b / 255f)
            {
            }

            internal static Rgb Lerp(Rgb a, Rgb b, float t) =>
                new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

            internal static Rgb Add(Rgb a, Rgb light, float amount) =>
                new Rgb(a.R + light.R * amount, a.G + light.G * amount, a.B + light.B * amount);
        }

        /// <summary>The colors one crest is drawn in, from the tier's color.</summary>
        private readonly struct Ink
        {
            private readonly bool _prismatic;

            /// <summary>Strokes and solid shapes.</summary>
            internal readonly Rgb Base;

            /// <summary>Hearts of diamonds and stars.</summary>
            internal readonly Rgb Light;

            /// <summary>The plaque, as on the plain badge: the surface with a fifth of the color.</summary>
            internal readonly Rgb Fill;

            internal readonly Rgb Glow;

            internal Ink(Design design, Rgb color)
            {
                _prismatic = design.Prismatic;
                Rgb tone = _prismatic ? _prism[1] : color;
                Base = color;
                Light = Rgb.Lerp(color, White, 0.65f);
                Fill = Rgb.Lerp(Surface, tone, 0.22f);
                Glow = tone;
            }

            /// <summary>A stroke's color; 寰宇's runs round the prism with <paramref name="angle"/> (radians).</summary>
            internal Rgb Stroke(float angle) => _prismatic ? Prism(angle / TAU) : Base;
        }

        /// <summary>Premultiplied color, composited front over back.</summary>
        private readonly struct Paint
        {
            private readonly float _r;
            private readonly float _g;
            private readonly float _b;
            internal readonly float A;

            private Paint(float r, float g, float b, float a)
            {
                _r = r;
                _g = g;
                _b = b;
                A = a;
            }

            internal static Paint Of(Rgb color, float alpha)
            {
                alpha = Clamp01(alpha);
                return new Paint(color.R * alpha, color.G * alpha, color.B * alpha, alpha);
            }

            /// <summary>This paint with <paramref name="top"/> laid over it.</summary>
            internal Paint Over(Paint top)
            {
                if (top.A <= 0f)
                {
                    return this;
                }

                float keep = 1f - top.A;
                return new Paint(top._r + _r * keep, top._g + _g * keep, top._b + _b * keep, top.A + A * keep);
            }

            internal void Write(byte[] target, int offset)
            {
                if (A <= 0f)
                {
                    return;
                }

                target[offset] = ToByte(_r / A);
                target[offset + 1] = ToByte(_g / A);
                target[offset + 2] = ToByte(_b / A);
                target[offset + 3] = ToByte(A);
            }

            private static byte ToByte(float value)
            {
                int scaled = (int)(value * 255f + 0.5f);
                return (byte)(scaled < 0 ? 0 : scaled > 255 ? 255 : scaled);
            }
        }

        /// <summary>One diamond in a flank's row: how far out from the plaque's side, and its size.</summary>
        private readonly struct SideDiamond
        {
            internal readonly float Offset;
            internal readonly float Size;

            internal SideDiamond(float offset, float size)
            {
                Offset = offset;
                Size = size;
            }
        }

        private sealed class Design
        {
            internal readonly int Tier;
            internal readonly float HexScale;
            internal int Frames;
            internal bool Studs;
            internal bool DoubleLine;
            internal int Rays;
            internal bool Prismatic;
            internal Orbit[] Orbits = Array.Empty<Orbit>();
            internal SideDiamond[] Side = Array.Empty<SideDiamond>();

            /// <summary>How far the flank's line runs out from the plaque's side.</summary>
            internal float SideReach;

            internal Design(int tier, float hexScale)
            {
                Tier = tier;
                HexScale = hexScale;
            }

            /// <summary>Circumradius of the plaque, as the plain badge's hexagon sits in its square.</summary>
            internal float Radius => 0.5f * HexScale - 0.005f;

            internal float FrameRadius => Radius + 0.05f * HexScale;

            /// <summary>The plaque's flat side, where the flank's row starts.</summary>
            internal float SideRoot => Radius * 0.8660254f;
        }
    }
}
