using System;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The brand mark, drawn from signed distances. It is an "A" whose legs are the two rails of a
    /// note highway seen in perspective, with the judgement line for its crossbar, a note falling
    /// toward it, and an orbit around the whole letter for the "world" in the name.
    ///
    /// Pure math with no Unity types: the importer bakes the splash layers, the editor's
    /// icon exporter renders the app icons, and both get the same pixels. Output is RGBA32 with
    /// straight alpha, bottom row first (Unity's texture order).
    /// </summary>
    internal static class BrandArt
    {
        /// <summary>Layers the splash animates separately. All share the emblem's square view box.</summary>
        internal enum Part
        {
            /// <summary>Both legs, cut where the front of the orbit passes over them.</summary>
            Rails,

            /// <summary>Both legs without the cuts, for the moment before the orbit is drawn.</summary>
            RailsWhole,

            /// <summary>The judgement line between the legs.</summary>
            Crossbar,

            /// <summary>The note falling down the highway inside the letter.</summary>
            Note,

            /// <summary>Half of the orbit behind the letter.</summary>
            OrbitBack,

            /// <summary>Half of the orbit in front of the letter.</summary>
            OrbitFront,

            /// <summary>The small moon riding the orbit.</summary>
            Satellite
        }

        internal enum Icon
        {
            /// <summary>Rounded square with transparent corners, for desktop and documents.</summary>
            Full,

            /// <summary>Android adaptive icon, back layer: full bleed, no emblem.</summary>
            AdaptiveBackground,

            /// <summary>Android adaptive icon, front layer: the emblem inside the safe zone.</summary>
            AdaptiveForeground
        }

        // ---------------------------------------------------------------- palette (sRGB, 0..255)

        internal static readonly Rgb Periwinkle = new Rgb(120, 156, 255);
        internal static readonly Rgb Violet = new Rgb(172, 146, 255);
        internal static readonly Rgb Rose = new Rgb(255, 150, 210);
        internal static readonly Rgb Ground = new Rgb(12, 15, 20);
        internal static readonly Rgb Ink = new Rgb(255, 255, 255);

        // ---------------------------------------------------------------- geometry (emblem box 0..1, y up)

        internal const float APEX_X = 0.5f;
        internal const float APEX_Y = 0.875f;
        internal const float FOOT_SPREAD = 0.335f;
        internal const float FOOT_Y = 0.125f;
        private const float RAIL_TOP_RADIUS = 0.028f;
        private const float RAIL_BOTTOM_RADIUS = 0.064f;

        internal const float BAR_Y = 0.455f;
        internal const float BAR_HALF_WIDTH = 0.228f;
        internal const float BAR_HALF_HEIGHT = 0.027f;

        internal const float NOTE_Y = 0.672f;
        internal const float NOTE_HALF_WIDTH = 0.06f;
        internal const float NOTE_HALF_HEIGHT = 0.018f;

        internal const float ORBIT_X = 0.5f;
        internal const float ORBIT_Y = 0.295f;
        private const float ORBIT_RADIUS_X = 0.48f;
        private const float ORBIT_RADIUS_Y = 0.1f;
        private const float ORBIT_TILT = 0.11f;
        private const float ORBIT_HALF_STROKE = 0.0125f;

        /// <summary>Clearance cut into the legs on each side of the orbit where it passes in front.</summary>
        private const float ORBIT_CLEARANCE = 0.016f;

        private const float SATELLITE_ANGLE = 0.3f;
        private const float SATELLITE_RADIUS = 0.03f;

        /// <summary>Orbit layers are drawn in a box of this size centered on the orbit, so a radial fill sweeps around it.</summary>
        internal const float ORBIT_BOX = 1f;

        // ---------------------------------------------------------------- public entry points

        /// <summary>Renders one layer at <paramref name="size"/>×<paramref name="size"/> pixels.</summary>
        internal static byte[] Render(Part part, int size)
        {
            var pixels = new byte[size * size * 4];
            float pixel = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vec((x + 0.5f) * pixel, (y + 0.5f) * pixel);
                    Rgba color = Shade(part, uv, pixel);
                    color.Write(pixels, (y * size + x) * 4);
                }
            }

            return pixels;
        }

        /// <summary>Renders an app icon at <paramref name="size"/>×<paramref name="size"/> pixels.</summary>
        internal static byte[] Render(Icon icon, int size)
        {
            var pixels = new byte[size * size * 4];
            float pixel = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vec((x + 0.5f) * pixel, (y + 0.5f) * pixel);
                    Rgba color = ShadeIcon(icon, uv, pixel, x, y);
                    color.Write(pixels, (y * size + x) * 4);
                }
            }

            return pixels;
        }

        /// <summary>The whole emblem over a transparent background, layers composited in order.</summary>
        internal static Rgba Emblem(Vec p, float pixel, bool glow)
        {
            Rgba color = Rgba.Clear;
            if (glow)
            {
                color = Over(color, Halo(p));
            }

            color = Over(color, OrbitBack(p, pixel));
            color = Over(color, Rails(p, pixel, true));
            color = Over(color, Crossbar(p, pixel, glow));
            color = Over(color, Note(p, pixel, glow));
            color = Over(color, OrbitFront(p, pixel));
            color = Over(color, Satellite(p, pixel, glow));
            return color;
        }

        // ---------------------------------------------------------------- layers

        private static Rgba Shade(Part part, Vec uv, float pixel)
        {
            switch (part)
            {
                case Part.Rails: return Rails(uv, pixel, true);
                case Part.RailsWhole: return Rails(uv, pixel, false);
                case Part.Crossbar: return Crossbar(uv, pixel, false);
                case Part.Note: return Note(uv, pixel, false);
                case Part.OrbitBack: return OrbitBack(FromOrbitBox(uv), pixel);
                case Part.OrbitFront: return OrbitFront(FromOrbitBox(uv), pixel);
                default: return Satellite(uv, pixel, false);
            }
        }

        /// <summary>Orbit layers use a box centered on the orbit; this maps it back to emblem space.</summary>
        private static Vec FromOrbitBox(Vec uv)
        {
            return new Vec(uv.X - 0.5f + ORBIT_X, uv.Y - 0.5f + ORBIT_Y);
        }

        private static Rgba Rails(Vec p, float pixel, bool cut)
        {
            float distance = RailsDistance(p);
            float alpha = Coverage(distance, pixel);
            if (cut && alpha > 0f)
            {
                // Where the front half of the orbit crosses a leg, clear a band a little wider than
                // the orbit's stroke so it reads as passing in front.
                float orbit = OrbitDistance(p, out float side);
                if (side < 0f)
                {
                    alpha *= 1f - Coverage(Math.Abs(orbit) - ORBIT_HALF_STROKE - ORBIT_CLEARANCE, pixel);
                }
            }

            Rgb color = Gradient(p);
            // A soft light falls from the top of each leg so the rails read as receding.
            float lift = Smooth(0.35f, 0.95f, p.Y) * 0.18f;
            color = Rgb.Lerp(color, Ink, lift);
            return new Rgba(color, alpha);
        }

        private static Rgba Crossbar(Vec p, float pixel, bool glow)
        {
            float distance = Capsule(p, new Vec(APEX_X - BAR_HALF_WIDTH, BAR_Y), new Vec(APEX_X + BAR_HALF_WIDTH, BAR_Y), BAR_HALF_HEIGHT);
            float alpha = Coverage(distance, pixel);
            Rgba bar = new Rgba(Rgb.Lerp(Ink, Violet, 0.08f), alpha);
            if (!glow)
            {
                return bar;
            }

            float halo = (float)Math.Exp(-Math.Max(0f, distance) / 0.03f) * 0.55f;
            return Over(new Rgba(Rgb.Lerp(Violet, Ink, 0.4f), halo), bar);
        }

        private static Rgba Note(Vec p, float pixel, bool glow)
        {
            float distance = Capsule(
                p, new Vec(APEX_X - NOTE_HALF_WIDTH, NOTE_Y), new Vec(APEX_X + NOTE_HALF_WIDTH, NOTE_Y), NOTE_HALF_HEIGHT);
            float alpha = Coverage(distance, pixel);
            Rgba note = new Rgba(Rgb.Lerp(Ink, Rose, 0.2f), alpha);
            if (!glow)
            {
                return note;
            }

            float halo = (float)Math.Exp(-Math.Max(0f, distance) / 0.025f) * 0.5f;
            return Over(new Rgba(Rose, halo), note);
        }

        private static Rgba OrbitBack(Vec p, float pixel)
        {
            float distance = OrbitDistance(p, out float side);
            Rgba stroke = OrbitStroke(p, distance, pixel);
            if (side < 0f)
            {
                // Transparent but keeping the stroke's color, so filtering never pulls in black.
                stroke.A = 0f;
            }

            return stroke;
        }

        private static Rgba OrbitFront(Vec p, float pixel)
        {
            float distance = OrbitDistance(p, out float side);
            Rgba stroke = OrbitStroke(p, distance, pixel);
            if (side >= 0f)
            {
                stroke.A = 0f;
            }

            return stroke;
        }

        private static Rgba OrbitStroke(Vec p, float distance, float pixel)
        {
            float alpha = Coverage(Math.Abs(distance) - ORBIT_HALF_STROKE, pixel);
            Rgb color = Rgb.Lerp(Gradient(p), Ink, 0.22f);
            return new Rgba(color, alpha);
        }

        private static Rgba Satellite(Vec p, float pixel, bool glow)
        {
            Vec center = SatelliteCenter();
            float distance = (p - center).Length - SATELLITE_RADIUS;
            Rgba moon = new Rgba(Rgb.Lerp(Ink, Rose, 0.15f), Coverage(distance, pixel));
            if (!glow)
            {
                return moon;
            }

            float halo = (float)Math.Exp(-Math.Max(0f, distance) / 0.03f) * 0.6f;
            return Over(new Rgba(Rose, halo), moon);
        }

        /// <summary>Soft light behind the emblem, used where the emblem sits on a plain background.</summary>
        private static Rgba Halo(Vec p)
        {
            float distance = Math.Min(RailsDistance(p), (p - new Vec(APEX_X, 0.5f)).Length - 0.18f);
            float glow = (float)Math.Exp(-Math.Max(0f, distance) / 0.09f) * 0.42f;
            return new Rgba(Violet, glow);
        }

        /// <summary>Center of the moon: a point on the back half of the orbit, up and to the right.</summary>
        internal static Vec SatelliteCenter()
        {
            float cos = (float)Math.Cos(ORBIT_TILT);
            float sin = (float)Math.Sin(ORBIT_TILT);
            float lx = ORBIT_RADIUS_X * (float)Math.Cos(SATELLITE_ANGLE);
            float ly = ORBIT_RADIUS_Y * (float)Math.Sin(SATELLITE_ANGLE);
            return new Vec(ORBIT_X + lx * cos - ly * sin, ORBIT_Y + lx * sin + ly * cos);
        }

        // ---------------------------------------------------------------- icons

        private static Rgba ShadeIcon(Icon icon, Vec uv, float pixel, int x, int y)
        {
            switch (icon)
            {
                case Icon.AdaptiveBackground:
                    return new Rgba(IconBackground(uv, x, y), 1f);
                case Icon.AdaptiveForeground:
                    return Emblem(ToEmblem(uv, ADAPTIVE_SCALE), pixel / ADAPTIVE_SCALE, true);
                default:
                {
                    float shape = Coverage(Squircle(uv, ICON_INSET), pixel);
                    if (shape <= 0f)
                    {
                        return Rgba.Clear;
                    }

                    Rgba color = new Rgba(IconBackground(uv, x, y), 1f);
                    color = Over(color, Emblem(ToEmblem(uv, FULL_SCALE), pixel / FULL_SCALE, true));
                    // A hairline on the rim, lighter at the top, like the light on a glass tile.
                    float rim = Coverage(Math.Abs(Squircle(uv, ICON_INSET) + 0.004f) - 0.0025f, pixel);
                    color = Over(color, new Rgba(Ink, rim * (0.05f + 0.12f * uv.Y)));
                    color.A *= shape;
                    return color;
                }
            }
        }

        /// <summary>Emblem size as a share of the icon; the adaptive one stays inside the 66% safe circle.</summary>
        private const float ADAPTIVE_SCALE = 0.5f;
        private const float FULL_SCALE = 0.74f;
        private const float ICON_INSET = 0.035f;

        private static Vec ToEmblem(Vec uv, float scale)
        {
            // The emblem's visual middle sits a little above its box center (the orbit is high).
            return new Vec((uv.X - 0.5f) / scale + 0.5f, (uv.Y - 0.5f) / scale + 0.485f);
        }

        private static Rgb IconBackground(Vec uv, int x, int y)
        {
            Rgb top = new Rgb(30, 32, 66);
            Rgb bottom = new Rgb(10, 12, 20);
            Rgb color = Rgb.Lerp(bottom, top, Smooth(0f, 1f, uv.Y));
            color = Rgb.Add(color, Periwinkle, Blob(uv, new Vec(0.18f, 0.2f), 0.55f) * 0.16f);
            color = Rgb.Add(color, Violet, Blob(uv, new Vec(0.5f, 0.6f), 0.5f) * 0.22f);
            color = Rgb.Add(color, Rose, Blob(uv, new Vec(0.9f, 0.92f), 0.45f) * 0.18f);

            // A dark floor under the letter and a hint of the highway's horizon behind it.
            float horizon = (float)Math.Exp(-Math.Abs(uv.Y - 0.31f) / 0.012f) * 0.05f * Smooth(0.95f, 0.2f, Math.Abs(uv.X - 0.5f) * 2f);
            color = Rgb.Add(color, Periwinkle, horizon);

            // One step of ordered noise breaks up the 8-bit bands a smooth dark gradient shows.
            float noise = (Hash(x, y) - 0.5f) / 255f;
            return new Rgb(color.R + noise, color.G + noise, color.B + noise);
        }

        private static float Blob(Vec p, Vec center, float radius)
        {
            float t = Math.Max(0f, 1f - (p - center).Length / radius);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Signed distance to a superellipse (a squircle), inset from the unit square.</summary>
        private static float Squircle(Vec uv, float inset)
        {
            const float EXPONENT = 5f;
            float half = 0.5f - inset;
            float dx = Math.Abs(uv.X - 0.5f) / half;
            float dy = Math.Abs(uv.Y - 0.5f) / half;
            float r = (float)Math.Pow(Math.Pow(dx, EXPONENT) + Math.Pow(dy, EXPONENT), 1d / EXPONENT);
            return (r - 1f) * half;
        }

        // ---------------------------------------------------------------- distance functions

        private static float RailsDistance(Vec p)
        {
            var apex = new Vec(APEX_X, APEX_Y);
            float left = Taper(p, apex, new Vec(APEX_X - FOOT_SPREAD, FOOT_Y), RAIL_TOP_RADIUS, RAIL_BOTTOM_RADIUS);
            float right = Taper(p, apex, new Vec(APEX_X + FOOT_SPREAD, FOOT_Y), RAIL_TOP_RADIUS, RAIL_BOTTOM_RADIUS);
            return Math.Min(left, right);
        }

        /// <summary>
        /// Approximate signed distance to the tilted orbit ellipse (first order, exact enough within a
        /// stroke's width of the curve). <paramref name="side"/> is negative on the front (lower) half.
        /// </summary>
        private static float OrbitDistance(Vec p, out float side)
        {
            float cos = (float)Math.Cos(ORBIT_TILT);
            float sin = (float)Math.Sin(ORBIT_TILT);
            float dx = p.X - ORBIT_X;
            float dy = p.Y - ORBIT_Y;
            float lx = dx * cos + dy * sin;
            float ly = -dx * sin + dy * cos;
            side = ly;

            float ax = ORBIT_RADIUS_X * ORBIT_RADIUS_X;
            float ay = ORBIT_RADIUS_Y * ORBIT_RADIUS_Y;
            float value = lx * lx / ax + ly * ly / ay - 1f;
            float gx = 2f * lx / ax;
            float gy = 2f * ly / ay;
            float gradient = (float)Math.Sqrt(gx * gx + gy * gy);
            return gradient > 1e-6f ? value / gradient : 0f;
        }

        private static float Taper(Vec p, Vec a, Vec b, float radiusA, float radiusB)
        {
            Vec ab = b - a;
            float t = Clamp01(Vec.Dot(p - a, ab) / Vec.Dot(ab, ab));
            return (p - (a + ab * t)).Length - (radiusA + (radiusB - radiusA) * t);
        }

        private static float Capsule(Vec p, Vec a, Vec b, float radius) => Taper(p, a, b, radius, radius);

        /// <summary>Gradient across the emblem, periwinkle at the lower left to rose at the upper right.</summary>
        internal static Rgb Gradient(Vec p)
        {
            float t = Clamp01((p.X - 0.1f) * 0.42f + (p.Y - 0.1f) * 0.78f);
            return t < 0.5f
                ? Rgb.Lerp(Periwinkle, Violet, t * 2f)
                : Rgb.Lerp(Violet, Rose, (t - 0.5f) * 2f);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Pixel coverage of a shape at <paramref name="distance"/> from its edge (negative inside).</summary>
        private static float Coverage(float distance, float pixel) => Clamp01(0.5f - distance / pixel);

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Straight-alpha "over": <paramref name="top"/> drawn on <paramref name="bottom"/>.</summary>
        private static Rgba Over(Rgba bottom, Rgba top)
        {
            float alpha = top.A + bottom.A * (1f - top.A);
            if (alpha <= 0f)
            {
                return Rgba.Clear;
            }

            float keep = bottom.A * (1f - top.A);
            return new Rgba(
                new Rgb(
                    (top.Color.R * top.A + bottom.Color.R * keep) / alpha,
                    (top.Color.G * top.A + bottom.Color.G * keep) / alpha,
                    (top.Color.B * top.A + bottom.Color.B * keep) / alpha),
                alpha);
        }

        internal readonly struct Vec
        {
            internal readonly float X;
            internal readonly float Y;

            internal Vec(float x, float y)
            {
                X = x;
                Y = y;
            }

            internal float Length => (float)Math.Sqrt(X * X + Y * Y);

            internal static float Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y;

            public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y);
            public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y);
            public static Vec operator *(Vec a, float s) => new Vec(a.X * s, a.Y * s);
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

            internal Rgb(byte r, byte g, byte b) : this(r / 255f, g / 255f, b / 255f)
            {
            }

            internal static Rgb Lerp(Rgb a, Rgb b, float t) =>
                new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

            /// <summary>Adds light: <paramref name="light"/> scaled by <paramref name="amount"/>.</summary>
            internal static Rgb Add(Rgb a, Rgb light, float amount) =>
                new Rgb(a.R + light.R * amount, a.G + light.G * amount, a.B + light.B * amount);
        }

        internal struct Rgba
        {
            internal Rgb Color;
            internal float A;

            internal Rgba(Rgb color, float alpha)
            {
                Color = color;
                A = alpha;
            }

            internal static Rgba Clear => new Rgba(new Rgb(0f, 0f, 0f), 0f);

            internal readonly void Write(byte[] target, int offset)
            {
                target[offset] = ToByte(Color.R);
                target[offset + 1] = ToByte(Color.G);
                target[offset + 2] = ToByte(Color.B);
                target[offset + 3] = ToByte(A);
            }

            private static byte ToByte(float value)
            {
                int scaled = (int)(value * 255f + 0.5f);
                return (byte)(scaled < 0 ? 0 : scaled > 255 ? 255 : scaled);
            }
        }
    }
}
