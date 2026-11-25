using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// The drawing surface every V2 asset goes through. Shapes are signed distances (negative inside) in texture
// pixels, colours are floats, and layers composite premultiplied, so a glow at 1% keeps its exact hue. The file
// is written with straight alpha; pixels that end up fully transparent borrow the colour next to them, so
// bilinear filtering never pulls black into an edge.
namespace ArtV2
{
    public delegate float Sdf(float x, float y);

    public delegate Ink Paint(float x, float y);

    /// <summary>A straight-alpha colour, 0..1 per channel, in sRGB like the interface's tokens.</summary>
    public struct Ink
    {
        public float R, G, B, A;

        public Ink(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        public static Ink Rgb(int r, int g, int b) { return new Ink(r / 255f, g / 255f, b / 255f, 1f); }

        public Ink Fade(float opacity) { return new Ink(R, G, B, A * opacity); }

        public static Ink Mix(Ink a, Ink b, float t)
        {
            t = M.Unit(t);
            return new Ink(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        /// <summary>A colour along evenly spaced stops, 0 at the first.</summary>
        public static Ink Along(Ink[] stops, float t)
        {
            float scaled = M.Unit(t) * (stops.Length - 1);
            int stop = Math.Min((int)scaled, stops.Length - 2);
            return Mix(stops[stop], stops[stop + 1], scaled - stop);
        }

        public static implicit operator Paint(Ink ink) { return delegate { return ink; }; }
    }

    public static class M
    {
        public const float PI = (float)Math.PI;

        public static float Unit(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

        public static float Smooth(float a, float b, float x)
        {
            float t = Unit((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float Gauss(float d, float sigma) { return (float)Math.Exp(-d * d / (2f * sigma * sigma)); }

        public static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }

        public static float Pow(float v, float p) { return (float)Math.Pow(Math.Max(0f, v), p); }

        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>A repeatable pseudo-random number in 0..1 for a pixel or an index.</summary>
        public static float Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263);
                n = (n ^ (n >> 13)) * 1274126177;
                return (n ^ (n >> 16)) / (float)uint.MaxValue;
            }
        }
    }

    public sealed class Canvas
    {
        /// <summary>The whole picture's size in texture pixels.</summary>
        public readonly int W, H;

        private readonly float[] _pixels;
        private readonly int _scale, _stride, _rows;

        // The view: where its origin sits and what it may touch, in texture pixels.
        private readonly float _left, _top, _clipX0, _clipY0, _clipX1, _clipY1;

        public Canvas(int width, int height, int scale)
        {
            W = width;
            H = height;
            _scale = scale;
            _stride = width * scale;
            _rows = height * scale;
            _pixels = new float[_stride * _rows * 4];
            _clipX1 = width;
            _clipY1 = height;
        }

        private Canvas(Canvas source, float left, float top, float width, float height)
        {
            W = source.W;
            H = source.H;
            _pixels = source._pixels;
            _scale = source._scale;
            _stride = source._stride;
            _rows = source._rows;
            _left = source._left + left;
            _top = source._top + top;
            _clipX0 = Math.Max(source._clipX0, _left);
            _clipY0 = Math.Max(source._clipY0, _top);
            _clipX1 = Math.Min(source._clipX1, _left + width);
            _clipY1 = Math.Min(source._clipY1, _top + height);
        }

        /// <summary>An atlas slot: its own origin, and nothing drawn in it leaves it.</summary>
        public Canvas View(float left, float top, float width, float height)
        {
            return new Canvas(this, left, top, width, height);
        }

        /// <summary>The same slot with its origin moved; what it may touch stays the same.</summary>
        public Canvas Shift(float dx, float dy)
        {
            var moved = new Canvas(this, dx, dy, 0f, 0f);
            return new Canvas(moved, _clipX0, _clipY0, _clipX1, _clipY1, true);
        }

        private Canvas(Canvas source, float clipX0, float clipY0, float clipX1, float clipY1, bool exact)
        {
            W = source.W;
            H = source.H;
            _pixels = source._pixels;
            _scale = source._scale;
            _stride = source._stride;
            _rows = source._rows;
            _left = source._left;
            _top = source._top;
            _clipX0 = clipX0;
            _clipY0 = clipY0;
            _clipX1 = clipX1;
            _clipY1 = clipY1;
        }

        // ------------------------------------------------------------------------------------ painting

        /// <summary>Fraction of a pixel inside an edge at <paramref name="distance"/>.</summary>
        public float Cover(float distance) { return M.Unit(0.5f - distance * _scale); }

        /// <summary>Coverage of a line <paramref name="width"/> wide, right for lines thinner than a pixel too.</summary>
        public float Line(float distance, float width)
        {
            return Cover(distance - width * 0.5f) - Cover(distance + width * 0.5f);
        }

        /// <summary>Runs a paint over every pixel of the view.</summary>
        public void Pixels(Paint paint)
        {
            int x0 = (int)Math.Round(_clipX0 * _scale), x1 = (int)Math.Round(_clipX1 * _scale);
            int y0 = (int)Math.Round(_clipY0 * _scale), y1 = (int)Math.Round(_clipY1 * _scale);
            float unit = 1f / _scale;
            Parallel.For(y0, y1, delegate(int row)
            {
                float y = (row + 0.5f) * unit - _top;
                int index = (row * _stride + x0) * 4;
                for (int column = x0; column < x1; column++, index += 4)
                {
                    Ink ink = paint((column + 0.5f) * unit - _left, y);
                    if (ink.A <= 0f) continue;
                    float a = ink.A > 1f ? 1f : ink.A, keep = 1f - a;
                    _pixels[index] = ink.R * a + _pixels[index] * keep;
                    _pixels[index + 1] = ink.G * a + _pixels[index + 1] * keep;
                    _pixels[index + 2] = ink.B * a + _pixels[index + 2] * keep;
                    _pixels[index + 3] = a + _pixels[index + 3] * keep;
                }
            });
        }

        /// <summary>A filled shape.</summary>
        public void Fill(Sdf shape, Paint paint)
        {
            Pixels(delegate(float x, float y)
            {
                float cover = Cover(shape(x, y));
                return cover <= 0f ? default(Ink) : paint(x, y).Fade(cover);
            });
        }

        /// <summary>A line along a shape's edge; <paramref name="align"/> -1 keeps it inside, 1 outside, 0 centred.</summary>
        public void Stroke(Sdf shape, float width, Paint paint, float align)
        {
            float shift = align * width * 0.5f;
            Pixels(delegate(float x, float y)
            {
                float cover = Line(shape(x, y) - shift, width);
                return cover <= 0f ? default(Ink) : paint(x, y).Fade(cover);
            });
        }

        /// <summary>Soft light around a shape, strongest at its edge. The inside is filled at <paramref name="inside"/> of the peak.</summary>
        public void Glow(Sdf shape, float sigma, float peak, float inside, Paint paint)
        {
            Pixels(delegate(float x, float y)
            {
                float d = shape(x, y);
                float a = d <= 0f ? inside + (1f - inside) * M.Gauss(d, sigma) : M.Gauss(d, sigma);
                a *= peak;
                return a < 0.0008f ? default(Ink) : paint(x, y).Fade(a);
            });
        }

        /// <summary>Light inside a shape, strongest at its edge and falling toward the middle.</summary>
        public void InnerGlow(Sdf shape, float sigma, float peak, Paint paint)
        {
            Pixels(delegate(float x, float y)
            {
                float d = shape(x, y);
                float a = Cover(d) * peak * M.Gauss(Math.Min(0f, d), sigma);
                return a < 0.0008f ? default(Ink) : paint(x, y).Fade(a);
            });
        }

        /// <summary>Multiplies what is already drawn by a 0..1 mask.</summary>
        public void Mask(Func<float, float, float> mask)
        {
            int x0 = (int)Math.Round(_clipX0 * _scale), x1 = (int)Math.Round(_clipX1 * _scale);
            int y0 = (int)Math.Round(_clipY0 * _scale), y1 = (int)Math.Round(_clipY1 * _scale);
            float unit = 1f / _scale;
            Parallel.For(y0, y1, delegate(int row)
            {
                float y = (row + 0.5f) * unit - _top;
                int index = (row * _stride + x0) * 4;
                for (int column = x0; column < x1; column++, index += 4)
                {
                    float k = M.Unit(mask((column + 0.5f) * unit - _left, y));
                    _pixels[index] *= k;
                    _pixels[index + 1] *= k;
                    _pixels[index + 2] *= k;
                    _pixels[index + 3] *= k;
                }
            });
        }

        // ------------------------------------------------------------------------------------ saving

        /// <summary>
        /// Writes the PNG. <paramref name="dither"/> adds a triangular +-1 code value so long dark gradients do not
        /// band; leave it off for flat shapes.
        /// </summary>
        public void Save(string path, bool dither)
        {
            var bytes = new byte[W * H * 4];
            float weight = 1f / (_scale * _scale);
            var empty = new bool[W * H];
            Parallel.For(0, H, delegate(int y)
            {
                for (int x = 0; x < W; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int j = 0; j < _scale; j++)
                    {
                        int index = ((y * _scale + j) * _stride + x * _scale) * 4;
                        for (int i = 0; i < _scale; i++, index += 4)
                        {
                            r += _pixels[index];
                            g += _pixels[index + 1];
                            b += _pixels[index + 2];
                            a += _pixels[index + 3];
                        }
                    }

                    a *= weight;
                    int at = (y * W + x) * 4;
                    if (a <= 0.0000001f)
                    {
                        empty[y * W + x] = true;
                        continue;
                    }

                    float back = weight / a;
                    float n = 0f, na = 0f;
                    if (dither)
                    {
                        n = M.Hash(x, y) + M.Hash(x + 4099, y + 7919) - 1f;
                        // A solid pixel stays solid and a nearly clear one is not pushed around.
                        na = a >= 0.9999f ? 0f : M.Hash(x + 911, y + 3571) + M.Hash(x + 6007, y + 1471) - 1f;
                    }

                    bytes[at] = Code(b * back, n);
                    bytes[at + 1] = Code(g * back, n);
                    bytes[at + 2] = Code(r * back, n);
                    bytes[at + 3] = Code(a, na);
                    if (bytes[at + 3] == 0) empty[y * W + x] = true;
                }
            });
            Bleed(bytes, empty);
            Write(path, bytes, W, H);
        }

        private static byte Code(float v, float noise)
        {
            float code = v * 255f + noise;
            return (byte)(code <= 0f ? 0 : code >= 255f ? 255 : (int)(code + 0.5f));
        }

        /// <summary>Gives transparent pixels near an edge that edge's colour; their alpha stays 0.</summary>
        private void Bleed(byte[] bytes, bool[] empty)
        {
            var known = new bool[W * H];
            for (int i = 0; i < known.Length; i++) known[i] = !empty[i];
            for (int pass = 0; pass < 3; pass++)
            {
                var next = (bool[])known.Clone();
                Parallel.For(0, H, delegate(int y)
                {
                    for (int x = 0; x < W; x++)
                    {
                        int at = y * W + x;
                        if (known[at]) continue;
                        int r = 0, g = 0, b = 0, count = 0;
                        for (int j = Math.Max(0, y - 1); j <= Math.Min(H - 1, y + 1); j++)
                            for (int i = Math.Max(0, x - 1); i <= Math.Min(W - 1, x + 1); i++)
                            {
                                int from = j * W + i;
                                if (!known[from]) continue;
                                b += bytes[from * 4];
                                g += bytes[from * 4 + 1];
                                r += bytes[from * 4 + 2];
                                count++;
                            }

                        if (count == 0) continue;
                        bytes[at * 4] = (byte)(b / count);
                        bytes[at * 4 + 1] = (byte)(g / count);
                        bytes[at * 4 + 2] = (byte)(r / count);
                        next[at] = true;
                    }
                });
                known = next;
            }
        }

        /// <summary>Writes BGRA bytes, top row first, as a 32-bit PNG.</summary>
        public static void Write(string path, byte[] bytes, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
                bitmap.UnlockBits(data);
                bitmap.Save(path, ImageFormat.Png);
            }
        }
    }
}
