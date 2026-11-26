using System;
using System.Collections.Generic;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Flat shapes as a colored triangle mesh: strokes, fills, soft beams and glows with feathered
    /// edges, so they stay sharp at any size and can be redrawn every frame without textures. The
    /// special medals, the achievements' emblems and their avatars are drawn with it;
    /// <see cref="VectorGraphic"/> puts the mesh on a canvas.
    ///
    /// Pure math with no Unity types. Drawing units are the caller's own; <see cref="Begin"/> says
    /// how many output units one of them is. Colors have straight alpha.
    /// </summary>
    internal sealed class VectorMesh
    {
        internal const float TAU = (float)(Math.PI * 2d);

        private const float MITER_LIMIT = 2.6f;

        private readonly List<Vertex> _vertices = new List<Vertex>();
        private readonly List<int> _indices = new List<int>();
        private readonly Path _path = new Path();
        private float _scale = 1f;
        private float _feather = 1f;
        private float _cos = 1f;
        private float _sin;
        private float _zoom = 1f;
        private float _x;
        private float _y;

        internal List<Vertex> Vertices => _vertices;

        /// <summary>Three per triangle.</summary>
        internal List<int> Indices => _indices;

        /// <summary>One antialiased edge, in drawing units.</summary>
        internal float Pixel => _feather / (_scale * _zoom);

        /// <summary>Starts over.</summary>
        /// <param name="scale">Output units per drawing unit.</param>
        /// <param name="feather">How wide an edge's antialiasing is, in output units.</param>
        internal void Begin(float scale, float feather)
        {
            _vertices.Clear();
            _indices.Clear();
            _scale = scale;
            _feather = feather;
            Place(0f, 0f);
        }

        /// <summary>Everything drawn from here on is moved, turned (radians, counterclockwise) and zoomed.</summary>
        internal void Place(float x, float y, float angle = 0f, float zoom = 1f)
        {
            _x = x;
            _y = y;
            _cos = (float)Math.Cos(angle);
            _sin = (float)Math.Sin(angle);
            _zoom = zoom;
        }

        /// <summary>A path to fill in and draw; every call starts the same one over.</summary>
        internal Path NewPath()
        {
            _path.Clear();
            return _path;
        }

        // ---------------------------------------------------------------- strokes

        internal void Stroke(Path path, bool closed, float width, Ink ink) => Stroke(path, closed, width, new Solid(ink));

        /// <summary>Draws along <paramref name="path"/>; the shade gets each point and how far along it is, 0 to 1.</summary>
        internal void Stroke<T>(Path path, bool closed, float width, in T shade) where T : struct, IShade
        {
            int count = path.Count;
            if (count < 2) return;
            float feather = Pixel;
            // A line thinner than its antialiasing keeps its weight by fading instead.
            float weight = Math.Min(1f, width / feather);
            float half = Math.Max(width, feather) * 0.5f;
            float inner = Math.Max(0f, half - feather * 0.5f);
            float outer = half + feather * 0.5f;
            float total = path.Length(closed);
            float along = 0f;
            int rows = closed ? count + 1 : count;
            bool joined = false;
            Ink before = default;
            for (int row = 0; row < rows; row++)
            {
                int i = row % count;
                Ink ink = shade.At(path[i], total > 0f ? along / total : 0f).Fade(weight);
                // A light on its way shows on a short stretch only; the rest is left out of the mesh.
                if (row > 0 && (ink.A > 0f || before.A > 0f))
                {
                    if (!joined) Row(path, row - 1, closed, inner, outer, before);
                    Row(path, i, closed, inner, outer, ink);
                    int v = _vertices.Count - 8;
                    for (int lane = 0; lane < 3; lane++) Face(v + lane, v + lane + 4, v + lane + 5, v + lane + 1);
                    joined = true;
                }
                else
                {
                    joined = false;
                }

                before = ink;
                if (row + 1 < rows) along += (path[(row + 1) % count] - path[i]).Length;
            }
        }

        internal void Line(float x0, float y0, float x1, float y1, float width, Ink ink) => Line(x0, y0, x1, y1, width, ink, ink);

        /// <summary>A straight stroke that runs from one color to another.</summary>
        internal void Line(float x0, float y0, float x1, float y1, float width, Ink from, Ink to)
        {
            Path path = NewPath();
            path.Add(x0, y0);
            path.Add(x1, y1);
            Stroke(path, false, width, new Ramp(from, to));
        }

        internal void Ring(float x, float y, float radius, float width, Ink ink)
        {
            Path path = NewPath();
            path.Arc(x, y, radius, radius, 0f, TAU, Segments(radius), false);
            Stroke(path, true, width, ink);
        }

        internal void ArcLine(float x, float y, float radius, float from, float to, float width, Ink ink)
        {
            Path path = NewPath();
            path.Arc(x, y, radius, radius, from, to, Segments(radius));
            Stroke(path, false, width, ink);
        }

        // ---------------------------------------------------------------- fills

        internal void Fill(Path path, Point center, Ink ink) => Fill(path, center, new Solid(ink));

        /// <summary>Fills a closed outline all of which can be seen from <paramref name="center"/>.</summary>
        internal void Fill<T>(Path path, Point center, in T shade) where T : struct, IShade
        {
            int count = path.Count;
            if (count < 3) return;
            float feather = Pixel * 0.5f;
            float turn = path.Area() >= 0f ? 1f : -1f;
            int middle = _vertices.Count;
            Add(center, shade.At(center, 0f));
            for (int i = 0; i < count; i++)
            {
                Point here = path[i];
                Point outward = path.Normal(i, true, out float miter) * -turn;
                Ink ink = shade.At(here, (float)i / count);
                Add(here - outward * (feather * miter), ink);
                Add(here + outward * (feather * miter), ink.Clear());
            }

            Fan(middle, count);
        }

        /// <summary>Fills between two lines with as many points, from <paramref name="a"/> to <paramref name="b"/>.</summary>
        internal void Strip(Path a, Path b, Ink inkA, Ink inkB)
        {
            int count = Math.Min(a.Count, b.Count);
            float feather = Pixel;
            int first = _vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Point across = a[i] - b[i];
                Point outward = across.Length > 1e-5f ? across * (1f / across.Length) : a.Normal(i, false, out _);
                Add(a[i] + outward * feather, inkA.Clear());
                Add(a[i], inkA);
                Add(b[i], inkB);
                Add(b[i] - outward * feather, inkB.Clear());
            }

            Lanes(first, count);
        }

        internal void Dot(float x, float y, float radius, Ink ink) => Ellipse(x, y, radius, radius, 0f, ink);

        internal void Ellipse(float x, float y, float radiusX, float radiusY, float angle, Ink ink)
        {
            Path path = NewPath();
            path.Arc(x, y, radiusX, radiusY, 0f, TAU, Segments(Math.Max(radiusX, radiusY)), false, angle);
            Fill(path, new Point(x, y), ink);
        }

        internal void Triangle(Point a, Point b, Point c, Ink ink)
        {
            Path path = NewPath();
            path.Add(a);
            path.Add(b);
            path.Add(c);
            Fill(path, (a + b + c) * (1f / 3f), ink);
        }

        internal void Quad(Point a, Point b, Point c, Point d, Ink ink)
        {
            Path path = NewPath();
            path.Add(a);
            path.Add(b);
            path.Add(c);
            path.Add(d);
            Fill(path, (a + b + c + d) * 0.25f, ink);
        }

        internal void Box(float x, float y, float halfWidth, float halfHeight, float radius, Ink ink) =>
            Box(x, y, halfWidth, halfHeight, radius, new Solid(ink));

        internal void Box<T>(float x, float y, float halfWidth, float halfHeight, float radius, in T shade) where T : struct, IShade
        {
            Path path = NewPath();
            path.Box(x, y, halfWidth, halfHeight, radius);
            Fill(path, new Point(x, y), shade);
        }

        internal void BoxLine(float x, float y, float halfWidth, float halfHeight, float radius, float width, Ink ink)
        {
            Path path = NewPath();
            path.Box(x, y, halfWidth, halfHeight, radius);
            Stroke(path, true, width, ink);
        }

        /// <summary>A diamond with a heart of another color, as on the medals' frames.</summary>
        internal void Diamond(float x, float y, float size, Ink ink, Ink heart)
        {
            Kite(x, y, size, size, size, ink);
            if (heart.A > 0f) Kite(x, y, size * 0.42f, size * 0.42f, size * 0.42f, heart);
        }

        /// <summary>A diamond with its own width, height above its middle and depth below it.</summary>
        internal void Kite(float x, float y, float halfWidth, float up, float down, Ink ink)
        {
            Path path = NewPath();
            path.Add(x, y + up);
            path.Add(x + halfWidth, y);
            path.Add(x, y - down);
            path.Add(x - halfWidth, y);
            Fill(path, new Point(x, y), ink);
        }

        /// <summary>A pointy-top hexagon's outline.</summary>
        internal void HexagonLine(float x, float y, float radius, float width, Ink ink)
        {
            Path path = NewPath();
            path.Arc(x, y, radius, radius, TAU * 0.25f, TAU * 1.25f, 6, false);
            Stroke(path, true, width, ink);
        }

        /// <summary>A four-pointed sparkle with curved sides.</summary>
        internal void Sparkle(float x, float y, float reachX, float reachY, Ink ink)
        {
            const int STEPS = 5;
            Path path = NewPath();
            for (int quarter = 0; quarter < 4; quarter++)
            {
                bool upright = quarter % 2 == 0;
                float signX = quarter < 2 ? 1f : -1f;
                float signY = quarter == 0 || quarter == 3 ? 1f : -1f;
                for (int i = 0; i < STEPS; i++)
                {
                    float t = (float)i / STEPS;
                    float leaving = (float)Math.Pow(1f - t, 2.3d);
                    float arriving = (float)Math.Pow(t, 2.3d);
                    path.Add(x + signX * reachX * (upright ? arriving : leaving), y + signY * reachY * (upright ? leaving : arriving));
                }
            }

            Fill(path, new Point(x, y), ink);
        }

        // ---------------------------------------------------------------- soft light

        /// <summary>A beam that is full along its middle and gone at its sides.</summary>
        internal void Beam(Point from, Point to, float width0, float width1, Ink ink0, Ink ink1)
        {
            Point along = to - from;
            if (along.Length < 1e-5f) return;
            Point side = new Point(-along.Y, along.X) * (0.5f / along.Length);
            const int ROWS = 6;
            int first = _vertices.Count;
            for (int row = 0; row <= ROWS; row++)
            {
                float t = (float)row / ROWS;
                Point middle = Point.Lerp(from, to, t);
                Point reach = side * Mix(width0, width1, t);
                Ink ink = Ink.Lerp(ink0, ink1, t);
                Add(middle - reach, ink.Clear());
                Add(middle - reach * 0.5f, ink.Fade(0.42f));
                Add(middle, ink);
                Add(middle + reach * 0.5f, ink.Fade(0.42f));
                Add(middle + reach, ink.Clear());
            }

            // The two sides are split the other way round, so that one is the other's mirror image.
            for (int row = 0; row < ROWS; row++)
            {
                int v = first + row * 5;
                Face(v, v + 5, v + 6, v + 1);
                Face(v + 1, v + 6, v + 7, v + 2);
                Face(v + 4, v + 9, v + 8, v + 3);
                Face(v + 3, v + 8, v + 7, v + 2);
            }
        }

        /// <summary>A soft round light.</summary>
        internal void Glow(float x, float y, float radiusX, float radiusY, Ink ink)
        {
            int steps = Math.Max(10, Math.Min(24, (int)(8f + Math.Max(radiusX, radiusY) * _zoom * _scale / _feather * 0.6f)));
            int middle = _vertices.Count;
            Add(new Point(x, y), ink);
            for (int i = 0; i < steps; i++)
            {
                float angle = TAU * i / steps;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                Add(new Point(x + cos * radiusX * 0.45f, y + sin * radiusY * 0.45f), ink.Fade(0.36f));
                Add(new Point(x + cos * radiusX, y + sin * radiusY), ink.Clear());
            }

            Fan(middle, steps);
        }

        // ---------------------------------------------------------------- helpers

        internal static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        internal static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        internal static float Mix(float a, float b, float t) => a + (b - a) * t;

        /// <summary>How far <paramref name="value"/> is into its turn of <paramref name="period"/>, 0 to 1.</summary>
        internal static float Cycle(float value, float period)
        {
            float turns = value / period;
            return turns - (float)Math.Floor(turns);
        }

        private int Segments(float radius)
        {
            float edges = radius * _zoom * _scale / _feather;
            return Math.Max(9, Math.Min(72, (int)(7f + edges * 1.2f)));
        }

        private void Add(Point point, Ink ink)
        {
            float px = (point.X * _cos - point.Y * _sin) * _zoom + _x;
            float py = (point.X * _sin + point.Y * _cos) * _zoom + _y;
            _vertices.Add(new Vertex(px * _scale, py * _scale, ink));
        }

        /// <summary>One point of a stroke: its two edges, and the two where it has faded out.</summary>
        private void Row(Path path, int i, bool closed, float inner, float outer, Ink ink)
        {
            Point here = path[i];
            Point normal = path.Normal(i, closed, out float miter);
            Add(here - normal * (outer * miter), ink.Clear());
            Add(here - normal * (inner * miter), ink);
            Add(here + normal * (inner * miter), ink);
            Add(here + normal * (outer * miter), ink.Clear());
        }

        private void Face(int a, int b, int c, int d)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
            _indices.Add(a);
            _indices.Add(c);
            _indices.Add(d);
        }

        /// <summary>Joins rows of four vertices.</summary>
        private void Lanes(int first, int rows)
        {
            for (int row = 0; row + 1 < rows; row++)
            {
                int v = first + row * 4;
                for (int lane = 0; lane < 3; lane++) Face(v + lane, v + lane + 4, v + lane + 5, v + lane + 1);
            }
        }

        /// <summary>Joins a middle vertex and <paramref name="count"/> pairs round it, inner then outer.</summary>
        private void Fan(int middle, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int a = middle + 1 + i * 2;
                int b = middle + 1 + (i + 1) % count * 2;
                _indices.Add(middle);
                _indices.Add(a);
                _indices.Add(b);
                Face(a, a + 1, b + 1, b);
            }
        }

        // ---------------------------------------------------------------- types

        internal readonly struct Point
        {
            internal readonly float X;
            internal readonly float Y;

            internal Point(float x, float y)
            {
                X = x;
                Y = y;
            }

            internal float Length => (float)Math.Sqrt(X * X + Y * Y);

            public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);

            public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);

            public static Point operator *(Point a, float scale) => new Point(a.X * scale, a.Y * scale);

            internal static Point Lerp(Point a, Point b, float t) => a + (b - a) * t;

            /// <summary>The point <paramref name="radius"/> away at <paramref name="angle"/> radians.</summary>
            internal static Point Polar(float angle, float radius) =>
                new Point((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
        }

        /// <summary>Color with straight alpha, components 0 to 1.</summary>
        internal readonly struct Ink
        {
            internal readonly float R;
            internal readonly float G;
            internal readonly float B;
            internal readonly float A;

            internal Ink(float r, float g, float b, float a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            internal static Ink Of(int r, int g, int b, float a = 1f) => new Ink(r / 255f, g / 255f, b / 255f, a);

            internal static Ink White(float a) => new Ink(1f, 1f, 1f, a);

            internal Ink With(float a) => new Ink(R, G, B, a);

            internal Ink Fade(float share) => new Ink(R, G, B, A * share);

            internal Ink Clear() => new Ink(R, G, B, 0f);

            internal static Ink Lerp(Ink a, Ink b, float t) => new Ink(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

            /// <summary>The ALL PERFECT prism, pink through violet to cyan and back.</summary>
            internal static Ink Prism(float position, float a = 1f)
            {
                RankingCrestArt.Rgb rgb = RankingCrestArt.Prism(position);
                return new Ink(rgb.R, rgb.G, rgb.B, a);
            }
        }

        internal readonly struct Vertex
        {
            internal readonly float X;
            internal readonly float Y;
            internal readonly Ink Ink;

            internal Vertex(float x, float y, Ink ink)
            {
                X = x;
                Y = y;
                Ink = ink;
            }
        }

        /// <summary>What color a point of a shape is; structs, so drawing every frame makes no garbage.</summary>
        internal interface IShade
        {
            /// <param name="along">0 to 1 along a stroke; round the outline for a fill.</param>
            Ink At(Point point, float along);
        }

        internal readonly struct Solid : IShade
        {
            private readonly Ink _ink;

            internal Solid(Ink ink) => _ink = ink;

            public Ink At(Point point, float along) => _ink;
        }

        /// <summary>From one color to another along a stroke.</summary>
        internal readonly struct Ramp : IShade
        {
            private readonly Ink _from;
            private readonly Ink _to;

            internal Ramp(Ink from, Ink to)
            {
                _from = from;
                _to = to;
            }

            public Ink At(Point point, float along) => Ink.Lerp(_from, _to, along);
        }

        /// <summary>From one color at <c>y0</c> up to another at <c>y1</c>.</summary>
        internal readonly struct Rise : IShade
        {
            private readonly Ink _bottom;
            private readonly Ink _top;
            private readonly float _y0;
            private readonly float _y1;

            internal Rise(Ink bottom, Ink top, float y0, float y1)
            {
                _bottom = bottom;
                _top = top;
                _y0 = y0;
                _y1 = y1;
            }

            public Ink At(Point point, float along) => Ink.Lerp(_bottom, _top, Clamp01((point.Y - _y0) / (_y1 - _y0)));
        }

        /// <summary>From one color at <c>x0</c> across to another at <c>x1</c>.</summary>
        internal readonly struct Across : IShade
        {
            private readonly Ink _left;
            private readonly Ink _right;
            private readonly float _x0;
            private readonly float _x1;

            internal Across(Ink left, Ink right, float x0, float x1)
            {
                _left = left;
                _right = right;
                _x0 = x0;
                _x1 = x1;
            }

            public Ink At(Point point, float along) => Ink.Lerp(_left, _right, Clamp01((point.X - _x0) / (_x1 - _x0)));
        }

        /// <summary>The prism across the drawing, drifting with <c>phase</c>, some white mixed in.</summary>
        internal readonly struct Spectrum : IShade
        {
            private readonly float _turns;
            private readonly float _phase;
            private readonly float _white;
            private readonly float _alpha;

            /// <param name="turns">How much of the prism one drawing unit covers.</param>
            internal Spectrum(float turns, float phase, float white, float alpha)
            {
                _turns = turns;
                _phase = phase;
                _white = white;
                _alpha = alpha;
            }

            public Ink At(Point point, float along) =>
                Ink.Lerp(Ink.Prism(point.X * _turns + point.Y * 0.06f + _phase), Ink.White(1f), _white).With(_alpha);
        }

        /// <summary>A light running along a stroke with a tail behind it; nothing elsewhere.</summary>
        internal readonly struct Comet : IShade
        {
            private readonly Ink _head;
            private readonly Ink _tail;
            private readonly float _at;
            private readonly float _length;
            private readonly bool _loops;

            /// <param name="at">Where its head is, 0 to 1; past 1 only the tail is left unless it loops.</param>
            /// <param name="length">How long its tail is, against the whole stroke.</param>
            internal Comet(Ink head, Ink tail, float at, float length, bool loops)
            {
                _head = head;
                _tail = tail;
                _at = at;
                _length = length;
                _loops = loops;
            }

            public Ink At(Point point, float along)
            {
                float behind = _at - along;
                if (_loops && behind < -_length * 0.1f) behind += 1f;
                if (behind < 0f) return _head.Fade(Clamp01(1f + behind / (_length * 0.1f)));
                float share = Clamp01(1f - behind / _length);
                return Ink.Lerp(_tail, _head, share * share).With(_head.A * share * share);
            }
        }

        /// <summary>A list of points, open or closed.</summary>
        internal sealed class Path
        {
            private const float QUARTER = TAU * 0.25f;

            private readonly List<Point> _points = new List<Point>();

            internal int Count => _points.Count;

            internal Point this[int index] => _points[index];

            internal void Clear() => _points.Clear();

            internal void Add(float x, float y) => _points.Add(new Point(x, y));

            internal void Add(Point point) => _points.Add(point);

            /// <summary>Points round an ellipse; without <paramref name="withLast"/> the last one is left out.</summary>
            internal void Arc(float x, float y, float radiusX, float radiusY, float from, float to, int steps, bool withLast = true,
                float tilt = 0f)
            {
                float cosTilt = (float)Math.Cos(tilt);
                float sinTilt = (float)Math.Sin(tilt);
                int count = withLast ? steps + 1 : steps;
                for (int i = 0; i < count; i++)
                {
                    float angle = from + (to - from) * i / steps;
                    float u = (float)Math.Cos(angle) * radiusX;
                    float v = (float)Math.Sin(angle) * radiusY;
                    Add(x + u * cosTilt - v * sinTilt, y + u * sinTilt + v * cosTilt);
                }
            }

            /// <summary>On from the last point to <paramref name="end"/>, bending toward two control points.</summary>
            internal void Curve(Point control0, Point control1, Point end, int steps)
            {
                Point start = _points[_points.Count - 1];
                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float s = 1f - t;
                    Add(start * (s * s * s) + control0 * (3f * s * s * t) + control1 * (3f * s * t * t) + end * (t * t * t));
                }
            }

            internal void Box(float x, float y, float halfWidth, float halfHeight, float radius)
            {
                const int STEPS = 7;
                radius = Math.Min(radius, Math.Min(halfWidth, halfHeight));
                if (radius <= 0f)
                {
                    Add(x - halfWidth, y + halfHeight);
                    Add(x + halfWidth, y + halfHeight);
                    Add(x + halfWidth, y - halfHeight);
                    Add(x - halfWidth, y - halfHeight);
                    return;
                }

                Arc(x - halfWidth + radius, y + halfHeight - radius, radius, radius, QUARTER * 2f, QUARTER, STEPS);
                Arc(x + halfWidth - radius, y + halfHeight - radius, radius, radius, QUARTER, 0f, STEPS);
                Arc(x + halfWidth - radius, y - halfHeight + radius, radius, radius, 0f, -QUARTER, STEPS);
                Arc(x - halfWidth + radius, y - halfHeight + radius, radius, radius, -QUARTER, -QUARTER * 2f, STEPS);
            }

            /// <summary>Puts extra points in, so that none are further apart than <paramref name="step"/>.</summary>
            internal void Subdivide(float step, bool closed)
            {
                int last = closed ? _points.Count : _points.Count - 1;
                for (int i = last - 1; i >= 0; i--)
                {
                    Point a = _points[i];
                    Point b = _points[(i + 1) % _points.Count];
                    int parts = (int)Math.Ceiling((b - a).Length / step);
                    for (int part = parts - 1; part >= 1; part--) _points.Insert(i + 1, Point.Lerp(a, b, (float)part / parts));
                }
            }

            internal float Length(bool closed)
            {
                float total = 0f;
                for (int i = 1; i < _points.Count; i++) total += (_points[i] - _points[i - 1]).Length;
                return closed ? total + (_points[0] - _points[_points.Count - 1]).Length : total;
            }

            /// <summary>The point <paramref name="along"/> (0 to 1) the path.</summary>
            internal Point At(float along, bool closed)
            {
                float left = Length(closed) * along;
                int last = closed ? _points.Count : _points.Count - 1;
                for (int i = 0; i < last; i++)
                {
                    Point a = _points[i];
                    Point b = _points[(i + 1) % _points.Count];
                    float step = (b - a).Length;
                    if (left <= step) return step > 0f ? Point.Lerp(a, b, left / step) : a;
                    left -= step;
                }

                return closed ? _points[0] : _points[_points.Count - 1];
            }

            /// <summary>Above 0 when it runs counterclockwise.</summary>
            internal float Area()
            {
                float sum = 0f;
                for (int i = 0; i < _points.Count; i++)
                {
                    Point a = _points[i];
                    Point b = _points[(i + 1) % _points.Count];
                    sum += a.X * b.Y - b.X * a.Y;
                }

                return sum * 0.5f;
            }

            /// <summary>Left of the path's direction at point <paramref name="i"/>; <paramref name="miter"/> keeps a bend as wide as its arms.</summary>
            internal Point Normal(int i, bool closed, out float miter)
            {
                int count = _points.Count;
                Point here = _points[i];
                Point before = i > 0 ? _points[i - 1] : closed ? _points[count - 1] : here;
                Point after = i + 1 < count ? _points[i + 1] : closed ? _points[0] : here;
                Point arriving = Unit(here - before);
                Point leaving = Unit(after - here);
                if (arriving.Length < 0.5f) arriving = leaving;
                if (leaving.Length < 0.5f) leaving = arriving;
                Point direction = Unit(arriving + leaving);
                if (direction.Length < 0.5f) direction = arriving;
                var normal = new Point(-direction.Y, direction.X);
                float cosine = -arriving.Y * normal.X + arriving.X * normal.Y;
                miter = Math.Min(MITER_LIMIT, 1f / Math.Max(0.1f, cosine));
                return normal;
            }

            private static Point Unit(Point point)
            {
                float length = point.Length;
                return length > 1e-6f ? point * (1f / length) : default;
            }
        }
    }
}
