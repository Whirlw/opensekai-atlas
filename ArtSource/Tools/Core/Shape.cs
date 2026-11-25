using System;
using System.Collections.Generic;
using System.Drawing;

// Signed distances: negative inside, in texture pixels, y down. The set follows the interface's own art
// (LibraryShapes, RankingCrestArt, BrandArt): pills, rounded cards, circles, diamonds, four-point stars, chevrons.
namespace ArtV2
{
    public static class Shape
    {
        public static float Box(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = Math.Abs(x) - halfWidth + radius, qy = Math.Abs(y) - halfHeight + radius;
            return M.Len(Math.Max(qx, 0f), Math.Max(qy, 0f)) + Math.Min(Math.Max(qx, qy), 0f) - radius;
        }

        /// <summary>A rounded rectangle; a radius of half the height makes the interface's pill.</summary>
        public static Sdf Rect(float left, float top, float width, float height, float radius)
        {
            float hw = width * 0.5f, hh = height * 0.5f, cx = left + hw, cy = top + hh;
            float r = Math.Min(radius, Math.Min(hw, hh));
            return delegate(float x, float y) { return Box(x - cx, y - cy, hw, hh, r); };
        }

        public static Sdf Pill(float left, float top, float width, float height)
        {
            return Rect(left, top, width, height, Math.Min(width, height) * 0.5f);
        }

        public static Sdf Circle(float cx, float cy, float radius)
        {
            return delegate(float x, float y) { return M.Len(x - cx, y - cy) - radius; };
        }

        /// <summary>Close to the true distance near the edge, which is where lines are drawn.</summary>
        public static Sdf Ellipse(float cx, float cy, float rx, float ry, float degrees)
        {
            float cos = (float)Math.Cos(degrees * M.PI / 180f), sin = (float)Math.Sin(degrees * M.PI / 180f);
            return delegate(float x, float y)
            {
                float dx = x - cx, dy = y - cy, u = dx * cos + dy * sin, v = dy * cos - dx * sin;
                float k0 = M.Len(u / rx, v / ry), k1 = M.Len(u / (rx * rx), v / (ry * ry));
                return k1 <= 0f ? -Math.Min(rx, ry) : k0 * (k0 - 1f) / k1;
            };
        }

        public static float SegmentDistance(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float length = vx * vx + vy * vy;
            float t = length <= 0f ? 0f : M.Unit((px * vx + py * vy) / length);
            return M.Len(px - vx * t, py - vy * t);
        }

        /// <summary>A line with round ends.</summary>
        public static Sdf Segment(float ax, float ay, float bx, float by, float radius)
        {
            return delegate(float x, float y) { return SegmentDistance(x, y, ax, ay, bx, by) - radius; };
        }

        /// <summary>A polyline with round ends and joins, like the interface's line icons.</summary>
        public static Sdf Polyline(float radius, params float[] points)
        {
            return delegate(float x, float y)
            {
                float best = float.MaxValue;
                for (int i = 0; i + 3 < points.Length; i += 2)
                    best = Math.Min(best, SegmentDistance(x, y, points[i], points[i + 1], points[i + 2], points[i + 3]));
                return best - radius;
            };
        }

        /// <summary>A diamond with its corners rounded by <paramref name="radius"/>; it still reaches its half sizes' edges.</summary>
        public static Sdf Diamond(float cx, float cy, float halfWidth, float halfHeight, float radius)
        {
            float shrink = radius * M.Len(halfWidth, halfHeight);
            float w = halfWidth - shrink / halfHeight, h = halfHeight - shrink / halfWidth;
            if (w <= 0f || h <= 0f) { w = halfWidth; h = halfHeight; radius = 0f; }
            return delegate(float x, float y)
            {
                float px = Math.Abs(x - cx), py = Math.Abs(y - cy);
                float hd = Math.Max(-1f, Math.Min(1f, ((w - 2f * px) * w - (h - 2f * py) * h) / (w * w + h * h)));
                float d = M.Len(px - 0.5f * w * (1f - hd), py - 0.5f * h * (1f + hd));
                return d * Math.Sign(px * h + py * w - w * h) - radius;
            };
        }

        /// <summary>The interface's four-point star: a square with a circle cut from each corner.</summary>
        public static Sdf Star4(float cx, float cy, float radius, float fullness)
        {
            return Star4(cx, cy, radius, radius, fullness, 0f);
        }

        /// <summary>The same star stretched to two radii and turned by an angle.</summary>
        public static Sdf Star4(float cx, float cy, float rx, float ry, float fullness, float degrees)
        {
            float cos = (float)Math.Cos(degrees * M.PI / 180f), sin = (float)Math.Sin(degrees * M.PI / 180f);
            float unit = Math.Min(rx, ry), center = unit * fullness;
            float bite = (float)Math.Sqrt((unit - center) * (unit - center) + center * center);
            return delegate(float x, float y)
            {
                float dx = x - cx, dy = y - cy;
                float qx = Math.Abs(dx * cos + dy * sin) * unit / rx, qy = Math.Abs(dy * cos - dx * sin) * unit / ry;
                return Math.Max(Math.Max(qx, qy) - unit, bite - M.Len(qx - center, qy - center));
            };
        }

        /// <summary>Exact distance to a polygon.</summary>
        public static float PolygonDistance(PointF[] p, float x, float y)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                best = Math.Min(best, SegmentDistance(x, y, p[j].X, p[j].Y, p[i].X, p[i].Y));
                if (p[i].Y > y != p[j].Y > y && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
            }

            return inside ? -best : best;
        }

        /// <summary>A polygon grown by <paramref name="grow"/>, which rounds its outer corners by as much.</summary>
        public static Sdf Polygon(PointF[] points, float grow)
        {
            return delegate(float x, float y) { return PolygonDistance(points, x, y) - grow; };
        }

        /// <summary>A convex polygon that keeps its edges where they are and rounds its corners.</summary>
        public static Sdf RoundPolygon(PointF[] points, float radius)
        {
            return Polygon(Inset(points, radius), radius);
        }

        /// <summary>Moves every edge of a polygon inwards (negative: outwards). Works for concave outlines too.</summary>
        public static PointF[] Inset(PointF[] p, float amount)
        {
            float area = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                PointF a = p[i], b = p[(i + 1) % p.Length];
                area += a.X * b.Y - b.X * a.Y;
            }

            float turn = area > 0f ? 1f : -1f;
            var nx = new float[p.Length];
            var ny = new float[p.Length];
            var c = new float[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                PointF a = p[i], b = p[(i + 1) % p.Length];
                float dx = b.X - a.X, dy = b.Y - a.Y, length = M.Len(dx, dy);
                nx[i] = dy / length * turn;
                ny[i] = -dx / length * turn;
                c[i] = nx[i] * a.X + ny[i] * a.Y - amount;
            }

            var result = new PointF[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                int k = (i + p.Length - 1) % p.Length;
                float det = nx[k] * ny[i] - ny[k] * nx[i];
                result[i] = new PointF((c[k] * ny[i] - c[i] * ny[k]) / det, (nx[k] * c[i] - nx[i] * c[k]) / det);
            }

            return result;
        }

        /// <summary>Moves each side of a quad outwards by its own margin: top, right, bottom, left (clockwise from upper left).</summary>
        public static PointF[] Outset(PointF[] p, float top, float right, float bottom, float left)
        {
            float[] margins = { top, right, bottom, left };
            var nx = new float[4];
            var ny = new float[4];
            var c = new float[4];
            for (int i = 0; i < 4; i++)
            {
                PointF a = p[i], b = p[(i + 1) % 4];
                float dx = b.X - a.X, dy = b.Y - a.Y, length = M.Len(dx, dy);
                nx[i] = dy / length;
                ny[i] = -dx / length;
                c[i] = nx[i] * a.X + ny[i] * a.Y + margins[i];
            }

            var result = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                int k = (i + 3) % 4;
                float det = nx[k] * ny[i] - ny[k] * nx[i];
                result[i] = new PointF((c[k] * ny[i] - c[i] * ny[k]) / det, (nx[k] * c[i] - nx[i] * c[k]) / det);
            }

            return result;
        }

        /// <summary>The flat heart of the interface's icon set, filling a box.</summary>
        public static Sdf Heart(float left, float top, float width, float height)
        {
            // Two lobes and a point, as a polygon fine enough to read as a curve at any icon size.
            var points = new List<PointF>();
            const int steps = 96;
            for (int i = 0; i < steps; i++)
            {
                double t = i * 2.0 * Math.PI / steps;
                double s = Math.Sin(t);
                double px = 16.0 * s * s * s;
                double py = 13.0 * Math.Cos(t) - 5.0 * Math.Cos(2 * t) - 2.0 * Math.Cos(3 * t) - Math.Cos(4 * t);
                points.Add(new PointF(
                    (float)(left + width * (px + 16.0) / 32.0), (float)(top + height * (11.95 - py) / 28.95)));
            }

            PointF[] outline = points.ToArray();
            return delegate(float x, float y) { return PolygonDistance(outline, x, y); };
        }

        public static Sdf Union(params Sdf[] shapes)
        {
            return delegate(float x, float y)
            {
                float best = float.MaxValue;
                for (int i = 0; i < shapes.Length; i++) best = Math.Min(best, shapes[i](x, y));
                return best;
            };
        }

        public static Sdf Intersect(Sdf a, Sdf b)
        {
            return delegate(float x, float y) { return Math.Max(a(x, y), b(x, y)); };
        }

        public static Sdf Cut(Sdf a, Sdf hole)
        {
            return delegate(float x, float y) { return Math.Max(a(x, y), -hole(x, y)); };
        }

        public static Sdf Grow(Sdf a, float amount)
        {
            return delegate(float x, float y) { return a(x, y) - amount; };
        }

        /// <summary>The outline of a shape as a shape: a band of the given width centred on its edge.</summary>
        public static Sdf Ring(Sdf a, float width)
        {
            float half = width * 0.5f;
            return delegate(float x, float y) { return Math.Abs(a(x, y)) - half; };
        }

        public static Sdf Move(Sdf a, float dx, float dy)
        {
            return delegate(float x, float y) { return a(x - dx, y - dy); };
        }

        public static Sdf MirrorX(Sdf a, float axis)
        {
            return delegate(float x, float y) { return a(2f * axis - x, y); };
        }
    }
}
