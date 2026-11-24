using System;
using OpenSekai.App.Library;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The frames of the medal plates and the 段位 plates' ground pattern. 初阶 is the plain plate:
    /// one rounded outline. Every tier above it keeps what the one below has and adds to it, in the
    /// same flat strokes and diamonds: 进阶 a fine inner line and a diamond on each end; 高阶
    /// brackets round the corners and a diamond in the middle of the long sides; 星阶 a whole outer
    /// frame with corner diamonds, and rows of diamonds out of the ends; 虹阶 the ALL PERFECT prism
    /// on every stroke, a second outer line, stars in place of the middle diamonds and a soft glow.
    /// The plate is the same in every tier and leaves room in its rectangle for the largest frame,
    /// so a lower tier looks smaller as a whole. A medal drawn small has a simpler frame.
    ///
    /// Pure math with no Unity types. Units: the plate's height is 1, the origin its center, y up.
    /// A frame is the same on both sides and above and below, so one quarter is worked out and
    /// mirrored. Output is RGBA32 with straight alpha, bottom row first (Unity's texture order).
    /// </summary>
    internal static class MedalArt
    {
        /// <summary>How far a frame's texture reaches past the plate's ends, against the plate's height.</summary>
        internal const float MARGIN_X = 0.28f;

        /// <summary>How far it reaches past the long sides.</summary>
        internal const float MARGIN_Y = 0.25f;

        /// <summary>How far the plate's face sits inside its rectangle on every side.</summary>
        internal const float INSET = 0.1f;

        /// <summary>The face's corners, against the plate's height.</summary>
        internal const float RADIUS = 0.144f;

        /// <summary>The corners of a medal drawn small, whose face fills its rectangle.</summary>
        internal const float COMPACT_RADIUS = 8f / 36f;

        /// <summary>One tile of the 段位 pattern, against the plate's height: two rows of diamonds fill it.</summary>
        internal const float PATTERN_TILE_X = 0.64f;
        internal const float PATTERN_TILE_Y = 0.5f;

        /// <summary>虹阶 strokes run through the ALL PERFECT prism instead of the medal's color.</summary>
        internal static bool IsPrismatic(int tier) => Clamp(tier) == MedalTiers.TOP;

        /// <summary>
        /// Draws the frame of a tier above 初阶 for a plate <paramref name="aspect"/> times as wide as high, at
        /// <paramref name="density"/> pixels per plate height. The strokes are white, to be tinted
        /// (虹阶's carry the prism); <paramref name="lights"/> holds the hearts of the diamonds,
        /// always white.
        /// </summary>
        internal static void RenderFrame(int tier, float aspect, float density, out byte[] strokes, out byte[] lights,
            out int width, out int height) =>
            Render(tier, aspect, density, false, out strokes, out lights, out width, out height);

        /// <summary>
        /// Draws the frame of a medal drawn small, white to be tinted (虹阶's carries the prism). It
        /// keeps what still reads at that size: 进阶 a thicker outline and a diamond on each end;
        /// 高阶 a diamond in the middle of the long sides; 星阶 an outer line; 虹阶 the prism, stars
        /// and the glow.
        /// </summary>
        internal static byte[] RenderCompactFrame(int tier, float aspect, float density, out int width, out int height)
        {
            Render(tier, aspect, density, true, out byte[] strokes, out _, out width, out height);
            return strokes;
        }

        private static void Render(int tier, float aspect, float density, bool compact, out byte[] strokes, out byte[] lights,
            out int width, out int height)
        {
            tier = Clamp(tier);
            int halfWidth = Math.Max(2, (int)Math.Ceiling((aspect * 0.5f + MARGIN_X) * density));
            int halfHeight = Math.Max(2, (int)Math.Ceiling((0.5f + MARGIN_Y) * density));
            width = halfWidth * 2;
            height = halfHeight * 2;
            strokes = new byte[width * height * 4];
            lights = compact ? null : new byte[strokes.Length];
            float pixelX = (aspect * 0.5f + MARGIN_X) / halfWidth;
            float pixelY = (0.5f + MARGIN_Y) / halfHeight;
            var frame = new Frame(tier, aspect, pixelX, compact);
            for (int row = 0; row < halfHeight; row++)
            {
                float y = (row + 0.5f) * pixelY;
                for (int column = 0; column < halfWidth; column++)
                {
                    float x = (column + 0.5f) * pixelX;
                    frame.At(x, y, out float stroke, out float light);
                    if (stroke <= 0f && light <= 0f)
                    {
                        continue;
                    }

                    Write(strokes, lights, width, halfWidth + column, halfHeight + row, x, y, aspect, frame.Prismatic, stroke, light);
                    Write(strokes, lights, width, halfWidth - 1 - column, halfHeight + row, -x, y, aspect, frame.Prismatic, stroke, light);
                    Write(strokes, lights, width, halfWidth + column, halfHeight - 1 - row, x, -y, aspect, frame.Prismatic, stroke, light);
                    Write(strokes, lights, width, halfWidth - 1 - column, halfHeight - 1 - row, -x, -y, aspect, frame.Prismatic, stroke, light);
                }
            }
        }

        /// <summary>
        /// One tile of the 段位 pattern, white to be tinted: a lattice of diamonds, each holding a
        /// smaller solid one. It repeats in both directions.
        /// </summary>
        internal static byte[] RenderPattern(int width, int height)
        {
            var pixels = new byte[width * height * 4];
            float pixel = 2f / height;
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    // One lattice cell is 2 wide and 2 high here; the offset rows fall on its corners.
                    float u = Math.Abs((column + 0.5f) / width * 2f - 1f);
                    float v = Math.Abs((row + 0.5f) / height * 2f - 1f);
                    float reach = u + v;
                    float line = Cover(Math.Abs(reach - 1f) * 0.7071f - 0.036f, pixel);
                    // Near a corner the tile shows a quarter of the next diamond.
                    float nested = reach <= 1f ? reach : 2f - reach;
                    float inner = Cover(Math.Abs(nested - 0.58f) * 0.7071f - 0.024f, pixel) * 0.55f;
                    float core = Cover((nested - 0.24f) * 0.7071f, pixel);
                    float alpha = Math.Max(line, Math.Max(inner, core));
                    int offset = (row * width + column) * 4;
                    pixels[offset] = 255;
                    pixels[offset + 1] = 255;
                    pixels[offset + 2] = 255;
                    pixels[offset + 3] = ToByte(alpha);
                }
            }

            return pixels;
        }

        private static void Write(byte[] strokes, byte[] lights, int width, int column, int row, float x, float y, float aspect,
            bool prismatic, float stroke, float light)
        {
            int offset = (row * width + column) * 4;
            if (stroke > 0f)
            {
                var color = new RankingCrestArt.Rgb(1f, 1f, 1f);
                if (prismatic)
                {
                    // Pink at the left end through violet to cyan at the right, leaning a little with the height.
                    float along = Clamp01(x / aspect + 0.5f + y * 0.06f);
                    color = RankingCrestArt.Prism(along * 0.5f);
                }

                strokes[offset] = ToByte(color.R);
                strokes[offset + 1] = ToByte(color.G);
                strokes[offset + 2] = ToByte(color.B);
                strokes[offset + 3] = ToByte(stroke);
            }

            if (light > 0f)
            {
                lights[offset] = 255;
                lights[offset + 1] = 255;
                lights[offset + 2] = 255;
                lights[offset + 3] = ToByte(light);
            }
        }

        private static int Clamp(int tier) => Math.Max(MedalTiers.LOWEST, Math.Min(MedalTiers.SPECIAL, tier));

        private static float Cover(float distance, float pixel) => Clamp01(0.5f - distance / pixel);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float value)
        {
            int scaled = (int)(value * 255f + 0.5f);
            return (byte)(scaled < 0 ? 0 : scaled > 255 ? 255 : scaled);
        }

        /// <summary>One tier's frame, worked out for the upper right quarter (both coordinates positive).</summary>
        private readonly struct Frame
        {
            private readonly int _tier;
            private readonly bool _compact;
            private readonly float _pixel;
            private readonly float _halfWidth;
            private readonly float _halfHeight;
            private readonly float _radius;
            private readonly float _outer;

            internal Frame(int tier, float aspect, float pixel, bool compact)
            {
                _tier = tier;
                _compact = compact;
                _pixel = pixel;
                float inset = compact ? 0f : INSET;
                _halfWidth = aspect * 0.5f - inset;
                _halfHeight = 0.5f - inset;
                _radius = compact ? COMPACT_RADIUS : RADIUS;
                _outer = compact ? 0.09f : tier >= 3 ? 0.062f : 0.056f;
            }

            internal bool Prismatic => IsPrismatic(_tier);

            internal void At(float x, float y, out float stroke, out float light)
            {
                stroke = 0f;
                light = 0f;
                float plate = Box(x, y, 0f);

                // Deep inside the face nothing is drawn.
                if (plate < -0.2f)
                {
                    return;
                }

                if (_compact)
                {
                    stroke = Compact(x, y, plate);
                    return;
                }

                float lines = Cover(Math.Max(plate, -plate - 0.036f), _pixel) * 0.92f;
                lines = Math.Max(lines, Cover(Math.Abs(plate + 0.085f) - 0.006f, _pixel) * 0.42f);
                if (_tier == 2)
                {
                    lines = Math.Max(lines, Brackets(x, y));
                }

                if (_tier >= 3)
                {
                    lines = Math.Max(lines, Cover(Math.Abs(Box(x, y, _outer)) - 0.009f, _pixel) * 0.8f);
                }

                if (_tier >= 4)
                {
                    float second = Math.Abs(Box(x, y, _outer + 0.04f)) - 0.005f;
                    lines = Math.Max(lines, Cover(second, _pixel) * 0.5f);
                    float glow = Math.Max(0f, plate);
                    lines = Math.Max(lines, plate > 0f ? 0.26f * (float)Math.Exp(-(glow * glow) / (0.085f * 0.085f)) : 0f);
                }

                // Ornaments lie over the lines, with a thin gap cut round each so they read apart.
                float solid = 0f;
                float gap = 0f;
                void Diamond(float cx, float cy, float size, ref float solidMax, ref float gapMax, ref float lightMax, float pixel)
                {
                    float reach = Math.Abs(x - cx) + Math.Abs(y - cy);
                    solidMax = Math.Max(solidMax, Cover((reach - size) * 0.7071f, pixel));
                    gapMax = Math.Max(gapMax, Cover((reach - size) * 0.7071f - 0.02f, pixel));
                    lightMax = Math.Max(lightMax, Cover((reach - size * 0.42f) * 0.7071f, pixel));
                }

                // The ends.
                float endX = _tier >= 3 ? _halfWidth + _outer : _halfWidth - 0.018f;
                Diamond(endX, 0f, 0.07f + 0.012f * _tier, ref solid, ref gap, ref light, _pixel);
                if (_tier >= 3)
                {
                    Diamond(endX + 0.15f, 0f, 0.045f, ref solid, ref gap, ref light, _pixel);
                }

                if (_tier >= 4)
                {
                    Diamond(endX + 0.225f, 0f, 0.028f, ref solid, ref gap, ref light, _pixel);
                }

                if (_tier >= 2)
                {
                    // The middles of the long sides.
                    float midY = _tier >= 3 ? _halfHeight + _outer : _halfHeight - 0.018f;
                    if (_tier >= 4)
                    {
                        float star = Star(x, y - midY, 0.17f, 0.12f);
                        solid = Math.Max(solid, Cover(star, _pixel));
                        gap = Math.Max(gap, Cover(star - 0.02f, _pixel));
                        Diamond(0f, midY, 0.052f, ref solid, ref gap, ref light, _pixel);
                    }
                    else
                    {
                        Diamond(0f, midY, _tier == 2 ? 0.075f : 0.095f, ref solid, ref gap, ref light, _pixel);
                    }

                    Diamond(_tier >= 4 ? 0.25f : 0.17f, midY, _tier == 2 ? 0.036f : 0.048f, ref solid, ref gap, ref light, _pixel);
                    if (_tier >= 3)
                    {
                        Diamond(_tier >= 4 ? 0.36f : 0.28f, midY, 0.03f, ref solid, ref gap, ref light, _pixel);
                    }
                }

                if (_tier >= 3)
                {
                    // The corners of the outer frame, on its rounding.
                    float turn = (_radius + _outer) * (1f - 0.7071f);
                    Diamond(_halfWidth + _outer - turn, _halfHeight + _outer - turn, _tier >= 4 ? 0.07f : 0.06f, ref solid, ref gap,
                        ref light, _pixel);
                }

                stroke = Math.Max(lines * (1f - gap), solid);
            }

            /// <summary>The frame of a medal drawn small.</summary>
            private float Compact(float x, float y, float plate)
            {
                float lines = Cover(Math.Max(plate, -plate - 0.05f), _pixel) * 0.92f;
                bool outer = _tier >= 3;
                if (outer)
                {
                    lines = Math.Max(lines, Cover(Math.Abs(Box(x, y, _outer)) - 0.016f, _pixel) * 0.85f);
                }

                if (_tier >= 4 && plate > 0f)
                {
                    lines = Math.Max(lines, 0.3f * (float)Math.Exp(-(plate * plate) / (0.1f * 0.1f)));
                }

                float rise = outer ? _outer : -0.02f;
                float ornament = (Math.Abs(x - _halfWidth - rise) + y - 0.15f) * 0.7071f;
                if (_tier >= 2)
                {
                    float offset = y - _halfHeight - rise;
                    float middle = _tier >= 4 ? Star(x, offset, 0.2f, 0.12f) : (x + Math.Abs(offset) - 0.12f) * 0.7071f;
                    ornament = Math.Min(ornament, middle);
                }

                return Math.Max(lines * (1f - Cover(ornament - 0.028f, _pixel)), Cover(ornament, _pixel));
            }

            /// <summary>高阶: an outline outside the plate, kept only round its corners and tapering off along the sides.</summary>
            private float Brackets(float x, float y)
            {
                float line = Cover(Math.Abs(Box(x, y, _outer)) - 0.013f, _pixel);
                if (line <= 0f)
                {
                    return 0f;
                }

                float along = Smooth(_halfWidth - 0.62f, _halfWidth - 0.3f, x);
                float up = Smooth(_halfHeight - 0.3f, _halfHeight - 0.16f, y);
                bool side = x > _halfWidth;
                return line * 0.9f * (side ? up : along);
            }

            /// <summary>Signed distance to the plate's outline grown by <paramref name="grow"/>.</summary>
            private float Box(float x, float y, float grow)
            {
                float radius = _radius + grow;
                float qx = x - (_halfWidth + grow) + radius;
                float qy = y - (_halfHeight + grow) + radius;
                float outside = (float)Math.Sqrt(Math.Max(qx, 0f) * Math.Max(qx, 0f) + Math.Max(qy, 0f) * Math.Max(qy, 0f));
                return outside + Math.Min(Math.Max(qx, qy), 0f) - radius;
            }

            /// <summary>A slim four-pointed star with curved sides.</summary>
            private static float Star(float x, float y, float reachX, float reachY)
            {
                float qx = Math.Abs(x) / reachX;
                float qy = Math.Abs(y) / reachY;
                float box = Math.Max(qx, qy) - 1f;
                float bite = (float)Math.Sqrt(0.1f * 0.1f + 1.1f * 1.1f);
                float cut = bite - (float)Math.Sqrt((qx - 1.1f) * (qx - 1.1f) + (qy - 1.1f) * (qy - 1.1f));
                return Math.Max(box, cut) * Math.Min(reachX, reachY);
            }
        }
    }
}
