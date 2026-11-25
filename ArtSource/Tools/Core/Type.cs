using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;

// Lettering. Digits and English captions are Barlow Condensed SemiBold, the face the interface uses for them.
namespace ArtV2
{
    public sealed class Face
    {
        private readonly GlyphTypeface _face;

        /// <summary>Cap height as a share of the em.</summary>
        public readonly float Cap;

        public Face(string file)
        {
            _face = new GlyphTypeface(new Uri(Path.GetFullPath(file)));
            Cap = (float)_face.CapsHeight;
        }

        /// <summary>
        /// Outlines of a line of text standing on y = 0 with capitals <paramref name="cap"/> pixels tall, starting
        /// at x = 0. <paramref name="tracking"/> is extra space between letters, in pixels.
        /// </summary>
        public GraphicsPath Outline(string text, float cap, float tracking)
        {
            var path = new GraphicsPath(FillMode.Winding);
            float em = cap / Cap, cursor = 0f;
            foreach (char letter in text)
            {
                ushort index;
                if (!_face.CharacterToGlyphMap.TryGetValue(letter, out index)) throw new Exception("No glyph for " + letter);
                Add(path, index, em, cursor);
                cursor += (float)_face.AdvanceWidths[index] * em + tracking;
            }

            return path;
        }

        private void Add(GraphicsPath path, ushort index, float em, float x)
        {
            PathGeometry outline = _face.GetGlyphOutline(index, em, em).GetFlattenedPathGeometry(0.02, ToleranceType.Absolute);
            foreach (PathFigure figure in outline.Figures)
            {
                var points = new List<PointF>();
                points.Add(new PointF(x + (float)figure.StartPoint.X, (float)figure.StartPoint.Y));
                foreach (PathSegment segment in figure.Segments)
                {
                    var poly = segment as PolyLineSegment;
                    var line = segment as LineSegment;
                    if (poly != null)
                        foreach (System.Windows.Point p in poly.Points) points.Add(new PointF(x + (float)p.X, (float)p.Y));
                    else if (line != null) points.Add(new PointF(x + (float)line.Point.X, (float)line.Point.Y));
                }

                if (points.Count < 3) continue;
                path.StartFigure();
                path.AddLines(points.ToArray());
                path.CloseFigure();
            }
        }
    }

    /// <summary>
    /// Lettering turned into a field over part of a texture: how much of each point the letters cover, and how far
    /// the nearest letter edge is. That makes text an ordinary shape that can glow, cast a shadow or be outlined.
    /// </summary>
    public sealed class Lettering
    {
        private readonly float[] _cover, _distance;
        private readonly int _w, _h, _scale;
        private readonly float _left, _top;

        /// <param name="path">Outlines in texture pixels.</param>
        /// <param name="area">The part of the texture the field covers; outside it there is no text.</param>
        /// <param name="scale">Field samples per pixel.</param>
        /// <param name="thin">Pixels shaved off each side of a stroke horizontally, to undo the weight a stretch adds.</param>
        public Lettering(GraphicsPath path, RectangleF area, int scale, float thin)
            : this(path, area, scale, thin > 0f ? (x => thin) : (Func<float, float>)null, null)
        {
        }

        /// <param name="thin">The same, by texture x, for letters that differ; null for none.</param>
        /// <param name="cuts">Rectangles taken out of the letters; null for none.</param>
        public Lettering(GraphicsPath path, RectangleF area, int scale, Func<float, float> thin, RectangleF[] cuts)
        {
            _scale = scale;
            _left = area.Left;
            _top = area.Top;
            _w = Math.Max(1, (int)Math.Ceiling(area.Width * scale));
            _h = Math.Max(1, (int)Math.Ceiling(area.Height * scale));
            _cover = Raster(path);
            if (thin != null) _cover = Erode(_cover, thin);
            if (cuts != null) Cut(cuts);
            _distance = Distances();
        }

        /// <summary>Distance to the letters, usable as a shape.</summary>
        public Sdf Field { get { return Distance; } }

        public float Cover(float x, float y) { return Sample(_cover, x, y, 0f); }

        public float Distance(float x, float y)
        {
            // Beyond the field, add the way back to it, so a glow keeps falling off.
            float u = (x - _left) * _scale, v = (y - _top) * _scale;
            float cu = Math.Max(0.5f, Math.Min(_w - 0.5f, u)), cv = Math.Max(0.5f, Math.Min(_h - 0.5f, v));
            return Sample(_distance, cu / _scale + _left, cv / _scale + _top, 9999f) + M.Len(u - cu, v - cv) / _scale;
        }

        /// <summary>Paints the letters.</summary>
        public void Fill(Canvas canvas, Paint paint)
        {
            canvas.Pixels(delegate(float x, float y)
            {
                float cover = Cover(x, y);
                return cover <= 0f ? default(Ink) : paint(x, y).Fade(cover);
            });
        }

        /// <summary>
        /// The letters' coverage under a Gaussian blur, 0..1. Wide light needs this: light measured by distance shows
        /// a ridge halfway between two letters.
        /// </summary>
        public Func<float, float, float> Blur(float sigma)
        {
            // Work on a grid coarse enough that the kernel stays small.
            int step = Math.Max(1, (int)(sigma * _scale / 5f));
            int w = (_w + step - 1) / step, h = (_h + step - 1) / step;
            var small = new float[w * h];
            Parallel.For(0, h, delegate(int y)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int j = y * step; j < Math.Min(_h, (y + 1) * step); j++)
                        for (int i = x * step; i < Math.Min(_w, (x + 1) * step); i++) sum += _cover[j * _w + i];
                    small[y * w + x] = sum / (step * step);
                }
            });
            float spread = sigma * _scale / step;
            int radius = (int)Math.Ceiling(spread * 3.2f);
            var kernel = new float[radius * 2 + 1];
            float total = 0f;
            for (int i = -radius; i <= radius; i++) total += kernel[i + radius] = M.Gauss(i, spread);
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= total;
            float[] soft = Pass(Pass(small, w, h, kernel, true), w, h, kernel, false);
            float unit = (float)_scale / step;
            return delegate(float x, float y)
            {
                float u = (x - _left) * unit - 0.5f, v = (y - _top) * unit - 0.5f;
                int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
                float fx = u - x0, fy = v - y0;
                float top = At(soft, w, h, x0, y0) * (1f - fx) + At(soft, w, h, x0 + 1, y0) * fx;
                float bottom = At(soft, w, h, x0, y0 + 1) * (1f - fx) + At(soft, w, h, x0 + 1, y0 + 1) * fx;
                return top * (1f - fy) + bottom * fy;
            };
        }

        private static float At(float[] field, int w, int h, int x, int y)
        {
            return x < 0 || y < 0 || x >= w || y >= h ? 0f : field[y * w + x];
        }

        private static float[] Pass(float[] source, int w, int h, float[] kernel, bool across)
        {
            int radius = kernel.Length / 2;
            var result = new float[w * h];
            Parallel.For(0, h, delegate(int y)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                        sum += kernel[k + radius] * (across ? At(source, w, h, x + k, y) : At(source, w, h, x, y + k));
                    result[y * w + x] = sum;
                }
            });
            return result;
        }

        private float Sample(float[] field, float x, float y, float outside)
        {
            float u = (x - _left) * _scale - 0.5f, v = (y - _top) * _scale - 0.5f;
            int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
            if (x0 < -1 || y0 < -1 || x0 >= _w || y0 >= _h) return outside;
            float fx = u - x0, fy = v - y0;
            int xa = Math.Max(0, x0), xb = Math.Min(_w - 1, x0 + 1), ya = Math.Max(0, y0), yb = Math.Min(_h - 1, y0 + 1);
            float top = field[ya * _w + xa] * (1f - fx) + field[ya * _w + xb] * fx;
            float bottom = field[yb * _w + xa] * (1f - fx) + field[yb * _w + xb] * fx;
            return top * (1f - fy) + bottom * fy;
        }

        private float[] Raster(GraphicsPath path)
        {
            var result = new float[_w * _h];
            using (var bitmap = new Bitmap(_w, _h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.ScaleTransform(_scale, _scale);
                    g.TranslateTransform(-_left, -_top);
                    using (var brush = new SolidBrush(System.Drawing.Color.White)) g.FillPath(brush, path);
                }

                var pixels = new int[_w * _h];
                BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, _w, _h), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                bitmap.UnlockBits(data);
                for (int i = 0; i < pixels.Length; i++) result[i] = ((uint)pixels[i] >> 24) / 255f;
            }

            return result;
        }

        private void Cut(RectangleF[] cuts)
        {
            foreach (RectangleF cut in cuts)
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        float px = (x + 0.5f) / _scale + _left, py = (y + 0.5f) / _scale + _top;
                        if (px > cut.Left && px < cut.Right && py > cut.Top && py < cut.Bottom) _cover[y * _w + x] = 0f;
                    }
        }

        private float[] Erode(float[] source, Func<float, float> thin)
        {
            var result = new float[source.Length];
            Parallel.For(0, _h, delegate(int y)
            {
                for (int x = 0; x < _w; x++)
                {
                    float radius = thin((x + 0.5f) / _scale + _left) * _scale;
                    if (radius <= 0f)
                    {
                        result[y * _w + x] = source[y * _w + x];
                        continue;
                    }

                    int whole = (int)Math.Floor(radius);
                    float part = radius - whole;
                    float inner = 1f, outer = 1f;
                    for (int k = -whole - 1; k <= whole + 1; k++)
                    {
                        int at = x + k;
                        float value = at < 0 || at >= _w ? 0f : source[y * _w + at];
                        if (Math.Abs(k) <= whole) inner = Math.Min(inner, value);
                        outer = Math.Min(outer, value);
                    }

                    result[y * _w + x] = inner + (outer - inner) * part;
                }
            });
            return result;
        }

        // Felzenszwalb's exact distance transform, run on the letters and on the space around them.
        private float[] Distances()
        {
            float[] outside = Transform(true), inside = Transform(false);
            var result = new float[_w * _h];
            for (int i = 0; i < result.Length; i++)
            {
                float d = (float)Math.Sqrt(outside[i]) - (float)Math.Sqrt(inside[i]);
                // Half a sample either way, and the edge pixels' own coverage, put the zero line on the outline.
                if (_cover[i] > 0f && _cover[i] < 1f) d = 0.5f - _cover[i];
                else d += d > 0f ? -0.5f : 0.5f;
                result[i] = d / _scale;
            }

            return result;
        }

        private float[] Transform(bool toLetters)
        {
            const float far = 1e12f;
            var field = new float[_w * _h];
            for (int i = 0; i < field.Length; i++) field[i] = _cover[i] >= 0.5f == toLetters ? 0f : far;
            Parallel.For(0, _w, delegate(int x)
            {
                var line = new float[_h];
                for (int y = 0; y < _h; y++) line[y] = field[y * _w + x];
                float[] done = Pass(line);
                for (int y = 0; y < _h; y++) field[y * _w + x] = done[y];
            });
            Parallel.For(0, _h, delegate(int y)
            {
                var line = new float[_w];
                Array.Copy(field, y * _w, line, 0, _w);
                Array.Copy(Pass(line), 0, field, y * _w, _w);
            });
            return field;
        }

        private static float[] Pass(float[] f)
        {
            int n = f.Length, k = 0;
            var d = new float[n];
            var v = new int[n];
            var z = new double[n + 1];
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s;
                while (true)
                {
                    s = ((double)f[q] + (double)q * q - ((double)f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                    if (s > z[k]) break;
                    k--;
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (float)Math.Min(1e12, (double)(q - v[k]) * (q - v[k]) + f[v[k]]);
            }

            return d;
        }
    }

    /// <summary>Puts lines of text where an asset wants them.</summary>
    public static class Type
    {
        public static Face Condensed;

        public static void Load(string fonts)
        {
            Condensed = new Face(Path.Combine(fonts, "BarlowCondensed-SemiBold.ttf"));
        }

        /// <summary>A line centred on a point, or standing on a baseline when <paramref name="baseline"/> is a number.</summary>
        public static GraphicsPath Line(string text, float cap, float tracking, float wide, float centerX, float centerY, float baseline)
        {
            GraphicsPath path = Condensed.Outline(text, cap, tracking);
            RectangleF bounds = path.GetBounds();
            float y = float.IsNaN(baseline) ? centerY + cap * 0.5f : baseline;
            using (var matrix = new System.Drawing.Drawing2D.Matrix(wide, 0f, 0f, 1f, centerX - (bounds.Left + bounds.Width * 0.5f) * wide, y))
                path.Transform(matrix);
            return path;
        }

        public static GraphicsPath Centered(string text, float cap, float tracking, float centerX, float centerY)
        {
            return Line(text, cap, tracking, 1f, centerX, centerY, float.NaN);
        }

        /// <summary>How wide a line's ink is, in pixels.</summary>
        public static float Width(string text, float cap, float tracking)
        {
            using (GraphicsPath path = Condensed.Outline(text, cap, tracking)) return path.GetBounds().Width;
        }
    }
}
