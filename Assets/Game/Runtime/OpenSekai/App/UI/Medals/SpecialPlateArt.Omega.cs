using System;
using Ink = OpenSekai.App.UI.VectorMesh.Ink;
using Path = OpenSekai.App.UI.VectorMesh.Path;
using Point = OpenSekai.App.UI.VectorMesh.Point;

namespace OpenSekai.App.UI
{
    internal abstract partial class SpecialPlateArt
    {
        /// <summary>
        /// 白昼折光: white light goes into a prism in the band and leaves it as the five difficulty colors
        /// in the order they were played, red first. Each runs under the text to a note of its own before
        /// the grade, and the frame comes apart into the same five lines toward its right end. The light
        /// goes in again and again, and the colors light up one after another.
        /// </summary>
        private sealed class DaylightArt : SpecialPlateArt
        {
            private const float LOOP = 4.4f;

            private static readonly Ink _white = Ink.White(1f);
            private static readonly Ink _pale = Ink.Of(206, 214, 255);
            private static readonly Ink _groundTop = Ink.Of(46, 50, 96);
            private static readonly Ink _groundBottom = Ink.Of(15, 16, 40);

            /// <summary>EXPERT, HARD, EASY, NORMAL, MASTER.</summary>
            private static readonly Ink[] _colors =
            {
                Ink.Of(255, 116, 124), Ink.Of(255, 208, 92), Ink.Of(120, 226, 146), Ink.Of(104, 190, 255), Ink.Of(198, 136, 255)
            };

            internal override Ink TextTop => _white;

            internal override Ink TextBottom => Ink.Of(212, 222, 255);

            protected override void Outline(Path path, float grow) => PlateOutline(path, grow, Corner.Round, 0f);

            internal override void Ground(VectorMesh m)
            {
                float w = Space.HalfWidth;
                float h = Space.HalfHeight;
                m.Box(0f, 0f, w, h, Space.Radius, new VectorMesh.Rise(_groundBottom, _groundTop, -h, h));
                if (Space.Compact)
                {
                    // Too small for rays: the five colors come up at the right end.
                    for (int i = 0; i < _colors.Length; i++)
                    {
                        float y = CompactY(i);
                        m.Beam(new Point(-w * 0.3f, y * 0.2f), new Point(w - 0.05f, y), 0.05f, 0.24f, _colors[i].With(0f), _colors[i].With(0.5f));
                    }

                    return;
                }

                // The rays spread evenly from the prism to the grade.
                for (int i = 0; i < _colors.Length; i++)
                {
                    Point from = RayStart(i);
                    Point to = RayEnd(i);
                    // Faint under the text, full past it.
                    Point clear = Point.Lerp(from, to, TextShare);
                    m.Beam(from, clear, 0.05f, 0.1f, _colors[i].With(0.55f), _colors[i].With(0.16f));
                    m.Beam(clear, to, 0.1f, 0.2f, _colors[i].With(0.16f), _colors[i].With(0.32f));
                    m.Line(from.X, from.Y, clear.X, clear.Y, 0.016f, _colors[i].With(0.8f), _colors[i].With(0.14f));
                    m.Line(clear.X, clear.Y, to.X, to.Y, 0.018f, _colors[i].With(0.14f), _colors[i].With(0.95f));
                    m.Kite(to.X, to.Y, 0.055f, 0.04f, 0.04f, _colors[i]);
                }

                Path band = m.NewPath();
                LeftPart(band, Space.Band, Corner.Round, 0f);
                m.Fill(band, new Point((Space.Band - w) * 0.5f, 0f), new VectorMesh.Rise(_groundBottom.With(0.5f), _pale.With(0.16f), -h, h));
                m.Line(Space.Band, h, Space.Band, -h, 0.018f, _pale.With(0.35f));
            }

            internal override void GroundMotion(VectorMesh m, float time)
            {
                if (Space.Compact) return;
                float at = VectorMesh.Cycle(time, LOOP) * LOOP;
                // In through the band, then out along each ray in turn.
                Pulse(m, new Point(-Space.HalfWidth + 0.04f, -0.12f), new Point(PrismX - 0.1f, -0.03f), _white, at / 0.45f, 0.6f, 0.05f);
                float inside = 1f - Math.Abs(at - 0.55f) / 0.35f;
                if (inside > 0f) m.Glow(PrismX, 0f, 0.4f, 0.36f, _white.With(0.45f * inside));
                for (int i = 0; i < _colors.Length; i++)
                {
                    float since = at - 0.5f - i * 0.22f;
                    float shown = VectorMesh.Mix(0.3f, 1f, VectorMesh.Smooth(TextShare - 0.15f, TextShare + 0.1f, since / 0.9f));
                    Pulse(m, RayStart(i), RayEnd(i), Ink.Lerp(_colors[i], _white, 0.35f).With(shown), since / 0.9f, 0.35f, 0.04f);
                    float arrived = since - 0.9f;
                    if (arrived < 0f || arrived > 1.5f) continue;
                    float glow = (1f - arrived / 1.5f) * (1f - arrived / 1.5f);
                    Point end = RayEnd(i);
                    m.Glow(end.X, end.Y, 0.2f, 0.16f, _colors[i].With(0.75f * glow));
                    m.Kite(end.X, end.Y, 0.07f, 0.05f, 0.05f, Ink.Lerp(_colors[i], _white, 0.6f).With(glow));
                }
            }

            internal override void Frame(VectorMesh m)
            {
                bool compact = Space.Compact;
                float outer = compact ? 0.08f : 0.056f;
                Path path;
                if (!compact)
                {
                    path = m.NewPath();
                    PlateOutline(path, -0.085f, Corner.Round, 0f);
                    path.Subdivide(0.1f, true);
                    m.Stroke(path, true, 0.011f, new Split(Space, 0.4f, 0.75f));
                }

                // White at the left; toward the right the five colors leave it one outside the other.
                for (int i = 0; i < _colors.Length; i++)
                {
                    path = m.NewPath();
                    PlateOutline(path, outer + i * (compact ? 0.036f : 0.033f), Corner.Round, 0f);
                    path.Subdivide(0.12f, true);
                    m.Stroke(path, true, compact ? 0.026f : 0.017f,
                        new VectorMesh.Across(_colors[i].With(0f), _colors[i].With(1f), -Space.HalfWidth * 0.1f + i * 0.18f, Space.HalfWidth));
                }

                path = m.NewPath();
                PlateOutline(path, outer, Corner.Round, 0f);
                path.Subdivide(0.12f, true);
                m.Stroke(path, true, compact ? 0.03f : 0.018f, new VectorMesh.Across(_white.With(0.9f), _white.With(0f), -Space.HalfWidth, Space.HalfWidth * 0.2f));

                path = m.NewPath();
                PlateOutline(path, compact ? -0.02f : -0.016f, Corner.Round, 0f);
                path.Subdivide(0.07f, true);
                m.Stroke(path, true, compact ? 0.062f : 0.04f, new Split(Space, 1f, 0.35f));

                EndDiamonds(m, -Space.HalfWidth - outer, -1f, _pale, _white, compact ? 1 : 3);
                for (int i = 0; i < _colors.Length; i++)
                {
                    float y = (2 - i) * (compact ? 0.15f : 0.115f);
                    float x = Space.HalfWidth + outer + (compact ? 0.06f : 0.1f) + 0.03f * (2 - Math.Abs(2 - i));
                    m.Diamond(x, y, compact ? 0.085f : 0.062f, _colors[i], Ink.Lerp(_colors[i], _white, 0.75f));
                }

                if (compact) return;
                // A prism on the top edge, a diamond below it.
                float edge = Space.HalfHeight + outer;
                m.Triangle(new Point(-0.15f, edge - 0.08f), new Point(0.15f, edge - 0.08f), new Point(0f, edge + 0.17f), Ink.Lerp(_groundTop, _white, 0.4f));
                path = m.NewPath();
                path.Add(-0.15f, edge - 0.08f);
                path.Add(0.15f, edge - 0.08f);
                path.Add(0f, edge + 0.17f);
                m.Stroke(path, true, 0.022f, _white);
                m.Diamond(-0.25f, edge, 0.05f, _pale, _white);
                m.Diamond(-0.35f, edge, 0.03f, _pale, default);
                for (int i = 0; i < _colors.Length; i++) m.Diamond(0.22f + i * 0.09f, edge, 0.036f, _colors[i], default);
                m.Diamond(0f, -edge, 0.08f, _pale, _white);
                m.Diamond(-0.16f, -edge, 0.04f, _pale, default);
                for (int i = 0; i < _colors.Length; i++) m.Diamond(0.16f + i * 0.09f, -edge, 0.036f, _colors[_colors.Length - 1 - i], default);
            }

            internal override void FrameMotion(VectorMesh m, float time)
            {
                bool compact = Space.Compact;
                float outer = compact ? 0.08f : 0.056f;
                float at = VectorMesh.Cycle(time, LOOP) * LOOP;
                // Along the top and the bottom at once, white first, then each color on its own line.
                for (int side = -1; side <= 1; side += 2)
                {
                    float y = side * (Space.HalfHeight - 0.02f);
                    Pulse(m, new Point(-Space.HalfWidth + 0.1f, y), new Point(0f, y), _white, at / 0.7f, 0.5f, compact ? 0.07f : 0.05f);
                    for (int i = 0; i < _colors.Length; i++)
                    {
                        float line = side * (Space.HalfHeight + outer + i * (compact ? 0.036f : 0.033f));
                        Pulse(m, new Point(0.1f, line), new Point(Space.HalfWidth - Space.Radius, line), Ink.Lerp(_colors[i], _white, 0.5f),
                            (at - 0.5f - i * 0.22f) / 0.9f, 0.45f, compact ? 0.05f : 0.032f);
                    }
                }

                for (int i = 0; i < _colors.Length; i++)
                {
                    float since = at - 1.3f - i * 0.22f;
                    if (since < 0f || since > 1.4f) continue;
                    float glow = (1f - since / 1.4f) * (1f - since / 1.4f);
                    float y = (2 - i) * (compact ? 0.15f : 0.115f);
                    float x = Space.HalfWidth + outer + (compact ? 0.06f : 0.1f) + 0.03f * (2 - Math.Abs(2 - i));
                    m.Glow(x, y, 0.24f, 0.2f, _colors[i].With(0.8f * glow));
                    m.Diamond(x, y, 0.075f, Ink.Lerp(_colors[i], _white, 0.7f).With(glow), default);
                }
            }

            internal override void Emblem(VectorMesh m)
            {
                m.Line(-0.6f, -0.14f, -0.15f, 0f, 0.06f, _white.With(0.1f), _white);
                for (int i = 0; i < _colors.Length; i++)
                {
                    float y = 0.24f - i * 0.13f;
                    m.Line(0.14f, 0.03f - i * 0.02f, 0.6f, y, 0.055f, _colors[i], _colors[i].With(0.85f));
                }

                m.Triangle(new Point(-0.32f, -0.28f), new Point(0.32f, -0.28f), new Point(0f, 0.36f), Ink.Lerp(_groundTop, _white, 0.25f));
                m.Triangle(new Point(-0.32f, -0.28f), new Point(0f, -0.28f), new Point(0f, 0.36f), _white.With(0.4f));
                m.Triangle(new Point(0f, 0.36f), new Point(0f, -0.28f), new Point(0.32f, -0.28f), _pale.With(0.16f));
                Path path = m.NewPath();
                path.Add(-0.32f, -0.28f);
                path.Add(0.32f, -0.28f);
                path.Add(0f, 0.36f);
                m.Stroke(path, true, 0.055f, _white);
            }

            internal override void Disc(VectorMesh m)
            {
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.5f, 0.5f, 0f, TAU, 64, false);
                m.Fill(path, default, new VectorMesh.Rise(_groundBottom, _groundTop, -0.5f, 0.5f));
                m.Glow(0f, 0f, 0.42f, 0.42f, _pale.With(0.16f));
                // Half of the rim is white, the other comes apart.
                m.ArcLine(0f, 0f, 0.48f, PI * 0.5f, PI * 1.5f, 0.042f, _white);
                for (int i = 0; i < _colors.Length; i++)
                {
                    float from = PI * 0.5f - i * PI / 5f;
                    m.ArcLine(0f, 0f, 0.48f, from + 0.01f, from - PI / 5f - 0.01f, 0.042f, _colors[i]);
                    m.ArcLine(0f, 0f, 0.55f, from - 0.05f, from - PI / 5f + 0.05f, 0.022f, _colors[i].With(0.8f));
                    Point gem = Point.Polar(from - PI / 10f, 0.62f);
                    m.Diamond(gem.X, gem.Y, 0.045f, _colors[i], Ink.Lerp(_colors[i], _white, 0.7f));
                }

                m.Diamond(-0.5f, 0f, 0.06f, _pale, _white);
            }

            internal override void DiscMotion(VectorMesh m, float time)
            {
                float at = VectorMesh.Cycle(time, LOOP) * LOOP;
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.48f, 0.48f, PI * 1.5f, PI * 0.5f, 24);
                m.Stroke(path, false, 0.05f, new VectorMesh.Comet(_white, _pale, at / 0.8f, 0.6f, false));
                for (int i = 0; i < _colors.Length; i++)
                {
                    float since = at - 0.6f - i * 0.24f;
                    if (since < 0f || since > 1.6f) continue;
                    float glow = (1f - since / 1.6f) * (1f - since / 1.6f);
                    float middle = PI * 0.5f - (i + 0.5f) * PI / 5f;
                    m.ArcLine(0f, 0f, 0.48f, middle + PI * 0.1f, middle - PI * 0.1f, 0.05f, Ink.Lerp(_colors[i], _white, 0.6f).With(glow));
                    Point gem = Point.Polar(middle, 0.62f);
                    m.Glow(gem.X, gem.Y, 0.18f, 0.18f, _colors[i].With(0.8f * glow));
                    m.Diamond(gem.X, gem.Y, 0.06f, Ink.Lerp(_colors[i], _white, 0.7f).With(glow), default);
                }
            }

            private float PrismX => (Space.Band - Space.HalfWidth) * 0.5f;

            /// <summary>How much of a ray lies under the text.</summary>
            private float TextShare => VectorMesh.Clamp01((Space.TextEnd + 0.1f - Space.Band) / (Space.Grade - Space.Band));

            private Point RayStart(int i) => new Point(Space.Band + 0.02f, 0.06f - i * 0.03f);

            private Point RayEnd(int i) => new Point(Space.Grade - 0.1f, (2 - i) * 0.135f);

            private static float CompactY(int i) => (2 - i) * 0.19f;

            /// <summary>A stretch of light on its way; gone before 0 and after 1.</summary>
            private static void Pulse(VectorMesh m, Point from, Point to, Ink ink, float at, float tail, float width)
            {
                float head = VectorMesh.Clamp01(at);
                float end = VectorMesh.Clamp01(at - tail);
                if (head <= end) return;
                Point a = Point.Lerp(from, to, end);
                Point b = Point.Lerp(from, to, head);
                m.Line(a.X, a.Y, b.X, b.Y, width, ink.Clear(), ink);
            }

            /// <summary>White on the left; on the right the five colors one above the other.</summary>
            private readonly struct Split : VectorMesh.IShade
            {
                private readonly float _halfWidth;
                private readonly float _halfHeight;
                private readonly float _alpha;
                private readonly float _pastel;

                internal Split(in PlateSpace space, float alpha, float pastel)
                {
                    _halfWidth = space.HalfWidth;
                    _halfHeight = space.HalfHeight;
                    _alpha = alpha;
                    _pastel = pastel;
                }

                public Ink At(Point point, float along)
                {
                    float at = VectorMesh.Clamp01((1f - point.Y / _halfHeight) * 0.5f) * (_colors.Length - 1);
                    int low = Math.Min((int)at, _colors.Length - 2);
                    Ink color = Ink.Lerp(_colors[low], _colors[low + 1], at - low);
                    color = Ink.Lerp(color, _white, _pastel * 0.4f);
                    float right = VectorMesh.Smooth(-0.1f * _halfWidth, 0.95f * _halfWidth, point.X);
                    return Ink.Lerp(Ink.Lerp(_pale, _white, 0.6f), color, right).With(_alpha);
                }
            }
        }

        /// <summary>
        /// 不落幕的安可: a ticket, bitten at both ends and perforated at its dividers, in front of a
        /// stage curtain. Bulbs run along its frame as round a marquee, and two spotlights sway over the
        /// curtain without ever going out.
        /// </summary>
        private sealed class EncoreArt : SpecialPlateArt
        {
            private const float NOTCH = 0.105f;
            private const float CHASE = 1.5f;

            private static readonly Ink _white = Ink.Of(255, 250, 236);
            private static readonly Ink _gold = Ink.Of(255, 212, 146);
            private static readonly Ink _rose = Ink.Of(255, 138, 152);
            private static readonly Ink _wine = Ink.Of(176, 44, 78);
            private static readonly Ink _groundTop = Ink.Of(112, 28, 54);
            private static readonly Ink _groundBottom = Ink.Of(46, 12, 28);

            private readonly Path _bulbs = new Path();

            internal override Ink TextTop => Ink.Of(255, 250, 238);

            internal override Ink TextBottom => Ink.Of(255, 218, 178);

            private float Notch => Space.Compact ? 0.16f : NOTCH;

            private float BulbGrow => Space.Compact ? 0.11f : 0.092f;

            protected override void Outline(Path path, float grow) => PlateOutline(path, grow, Corner.Round, 0f, null, null, Notch);

            protected override void Arrange()
            {
                // Bulbs at even distances round the line outside the frame.
                var line = new Path();
                PlateOutline(line, BulbGrow, Corner.Round, 0f);
                float length = line.Length(true);
                int count = Math.Max(9, (int)(length / (Space.Compact ? 0.28f : 0.18f)) / 3 * 3);
                _bulbs.Clear();
                for (int i = 0; i < count; i++) _bulbs.Add(line.At((i + 0.5f) / count, true));
            }

            internal override void Ground(VectorMesh m)
            {
                float w = Space.HalfWidth;
                float h = Space.HalfHeight;
                Path face = m.NewPath();
                Outline(face, 0f);
                // The bites turn away from the middle, so the two halves are filled from within.
                m.Fill(face, default, new VectorMesh.Rise(_groundBottom, _groundTop, -h, h));
                Curtain(m, Space.Compact ? -w + 0.26f : Space.Band, Space.Compact ? w - 0.26f : Space.Grade);
                if (Space.Compact) return;

                Perforation(m, Space.Band);
                m.Glow((Space.Band - w) * 0.5f, 0f, 0.4f, 0.4f, _gold.With(0.22f));
                m.Glow(Space.Band + 0.5f, -h + 0.06f, 1.2f, 0.16f, _rose.With(0.25f));
            }

            internal override void GroundMotion(VectorMesh m, float time)
            {
                if (Space.Compact)
                {
                    float sway = (float)Math.Sin(time * 1.3f) * Space.HalfWidth * 0.5f;
                    m.Glow(sway, 0f, 0.8f, 0.5f, _gold.With(0.16f));
                    return;
                }

                // Two spotlights from the top corners, crossing on the text.
                float middle = (Space.Band + Space.Grade) * 0.5f;
                float reach = (Space.Grade - Space.Band) * 0.28f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float swing = (float)Math.Sin(time * 0.9f + side * 1.2f);
                    var from = new Point(middle + side * reach * 1.4f, Space.HalfHeight - 0.02f);
                    var to = new Point(middle - side * reach * 0.35f + swing * reach * 0.8f, -Space.HalfHeight + 0.04f);
                    m.Beam(from, to, 0.05f, 0.62f, _white.With(0.4f), _gold.With(0.1f));
                    m.Glow(to.X, to.Y, 0.42f, 0.09f, _gold.With(0.5f));
                }
            }

            internal override void Frame(VectorMesh m)
            {
                bool compact = Space.Compact;
                Path path = m.NewPath();
                Outline(path, compact ? -0.02f : -0.016f);
                m.Stroke(path, true, compact ? 0.062f : 0.04f, new VectorMesh.Rise(_rose, _gold, -0.5f, 0.5f));
                path = m.NewPath();
                PlateOutline(path, BulbGrow, Corner.Round, 0f);
                m.Stroke(path, true, compact ? 0.07f : 0.058f, _wine.With(0.85f));
                for (int i = 0; i < _bulbs.Count; i++) m.Dot(_bulbs[i].X, _bulbs[i].Y, compact ? 0.028f : 0.021f, _gold.With(0.5f));
                if (compact) return;
                // The return, on the top edge.
                float edge = Space.HalfHeight + BulbGrow;
                m.Dot(0f, edge, 0.15f, _wine);
                m.Ring(0f, edge, 0.145f, 0.022f, _gold);
                m.ArcLine(0f, edge, 0.075f, PI * 0.5f + 0.6f, PI * 2.5f - 0.1f, 0.028f, _white);
                Point tip = Point.Polar(PI * 0.5f + 0.5f, 0.075f);
                MedalEmblemArt.Head(m, new Point(tip.X, tip.Y + edge), 0.5f, 0.06f, _white);
                for (int side = -1; side <= 1; side += 2)
                {
                    m.Diamond(side * 0.25f, edge, 0.062f, _gold, _white);
                    m.Diamond(side * 0.25f, -edge, 0.05f, _gold, _white);
                    m.Diamond(side * (Space.HalfWidth + BulbGrow), 0f, 0.1f, _gold, _white);
                    m.Diamond(side * (Space.HalfWidth + BulbGrow + 0.15f), 0f, 0.045f, _gold, _white);
                }

                m.Diamond(0f, -edge, 0.09f, _gold, _white);
            }

            internal override void FrameMotion(VectorMesh m, float time)
            {
                bool compact = Space.Compact;
                // Every third bulb is lit; the lit ones move on.
                float step = VectorMesh.Cycle(time, CHASE) * 3f;
                for (int i = 0; i < _bulbs.Count; i++)
                {
                    float away = VectorMesh.Cycle(i - step, 3f) * 3f;
                    float lit = 1f - Math.Min(away, 3f - away) * 1.3f;
                    if (lit <= 0.02f) continue;
                    Point bulb = _bulbs[i];
                    m.Glow(bulb.X, bulb.Y, compact ? 0.1f : 0.085f, compact ? 0.1f : 0.085f, _gold.With(0.8f * lit));
                    m.Dot(bulb.X, bulb.Y, compact ? 0.034f : 0.026f, _white.With(lit));
                }
            }

            internal override void Emblem(VectorMesh m)
            {
                m.Place(0f, 0.02f, 0.14f);
                Path path = m.NewPath();
                const float W = 0.44f;
                const float H = 0.29f;
                const float BITE = 0.085f;
                path.Arc(-W, 0f, BITE, BITE, -PI * 0.5f, PI * 0.5f, 8);
                path.Add(-W, H);
                path.Add(W, H);
                path.Arc(W, 0f, BITE, BITE, PI * 0.5f, PI * 1.5f, 8);
                path.Add(W, -H);
                path.Add(-W, -H);
                m.Stroke(path, true, 0.06f, _gold);
                Hundred(m, -0.03f, 0f, 0.13f, 0.048f, _white);
                for (int i = 0; i < 4; i++) m.Dot(0.3f, -0.17f + i * 0.113f, 0.02f, _gold);
                m.Place(0f, 0f);
                m.Diamond(-0.33f, 0.4f, 0.055f, _rose, default);
                m.Diamond(0.35f, -0.4f, 0.055f, _rose, default);
            }

            internal override void Disc(VectorMesh m)
            {
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.5f, 0.5f, 0f, TAU, 64, false);
                m.Fill(path, default, new VectorMesh.Rise(_groundBottom, _groundTop, -0.5f, 0.5f));
                // Grooves: the same song over and over.
                for (int i = 0; i < 3; i++) m.Ring(0f, 0f, 0.26f + i * 0.065f, 0.01f, _rose.With(0.3f - i * 0.05f));
                m.Beam(new Point(-0.1f, 0.48f), new Point(-0.16f, -0.36f), 0.06f, 0.5f, _white.With(0.3f), _gold.With(0.04f));
                m.Beam(new Point(0.1f, 0.48f), new Point(0.16f, -0.36f), 0.06f, 0.5f, _white.With(0.3f), _gold.With(0.04f));
                m.Ring(0f, 0f, 0.48f, 0.042f, _gold);
                m.Ring(0f, 0f, 0.56f, 0.066f, _wine);
                for (int i = 0; i < 18; i++)
                {
                    Point bulb = Point.Polar(i * TAU / 18f, 0.56f);
                    m.Dot(bulb.X, bulb.Y, 0.02f, _gold.With(0.5f));
                }
            }

            internal override void DiscMotion(VectorMesh m, float time)
            {
                float step = VectorMesh.Cycle(time, CHASE) * 3f;
                for (int i = 0; i < 18; i++)
                {
                    float away = VectorMesh.Cycle(i + step, 3f) * 3f;
                    float lit = 1f - Math.Min(away, 3f - away) * 1.3f;
                    if (lit <= 0.02f) continue;
                    Point bulb = Point.Polar(i * TAU / 18f, 0.56f);
                    m.Glow(bulb.X, bulb.Y, 0.08f, 0.08f, _gold.With(0.8f * lit));
                    m.Dot(bulb.X, bulb.Y, 0.026f, _white.With(lit));
                }

                // A light turning with the record.
                float turn = -VectorMesh.Cycle(time, 3.2f) * TAU;
                for (int i = 0; i < 2; i++) m.ArcLine(0f, 0f, 0.39f, turn + i * PI, turn + i * PI + 0.7f, 0.016f, _gold.With(0.55f));
            }

            /// <summary>Folds and a valance, between <paramref name="left"/> and <paramref name="right"/>.</summary>
            private void Curtain(VectorMesh m, float left, float right)
            {
                float h = Space.HalfHeight;
                int folds = Math.Max(2, (int)Math.Round((right - left) / 0.26f));
                float step = (right - left) / folds;
                for (int i = 0; i < folds; i++)
                {
                    float x = left + step * (i + 0.5f);
                    m.Beam(new Point(x, h - 0.01f), new Point(x, -h + 0.01f), step * 0.9f, step * 0.9f, _rose.With(0.2f), _rose.With(0.05f));
                }

                for (int i = 0; i < folds; i++)
                {
                    float x = left + step * (i + 0.5f);
                    Path swag = m.NewPath();
                    swag.Add(x - step * 0.5f, h - 0.005f);
                    swag.Add(x + step * 0.5f, h - 0.005f);
                    swag.Arc(x, h - 0.02f, step * 0.5f, 0.13f, 0f, -PI, 10);
                    m.Fill(swag, new Point(x, h - 0.06f), Ink.Lerp(_wine, _rose, 0.12f).With(0.9f));
                    Path hem = m.NewPath();
                    hem.Arc(x, h - 0.02f, step * 0.5f, 0.13f, 0f, -PI, 10);
                    m.Stroke(hem, false, 0.016f, _gold.With(0.7f));
                }
            }

            private void Perforation(VectorMesh m, float x)
            {
                for (float y = -Space.HalfHeight + 0.08f; y < Space.HalfHeight - 0.05f; y += 0.092f) m.Dot(x, y, 0.02f, _gold.With(0.85f));
            }
        }

        /// <summary>
        /// 终乐章: 虹阶's frame round a staff on black. Four voices come out of the band at four heights
        /// and run into the final barline before the grade; notes travel along them, and the barline
        /// answers as they arrive.
        /// </summary>
        private sealed class CodaArt : SpecialPlateArt
        {
            private const float LOOP = 3.6f;
            private const int VOICES = 4;

            private static readonly Ink _white = Ink.White(1f);
            private static readonly Ink _ivory = Ink.Of(250, 238, 214);
            private static readonly Ink _gold = Ink.Of(255, 212, 146);
            private static readonly Ink _groundTop = Ink.Of(58, 52, 60);
            private static readonly Ink _groundBottom = Ink.Of(17, 14, 20);

            private readonly Path[] _voices = { new Path(), new Path(), new Path(), new Path() };

            internal override Ink TextTop => Ink.Of(255, 250, 238);

            internal override Ink TextBottom => Ink.Of(240, 220, 186);

            internal override bool KeepsTierFrame => true;

            internal override bool DividesGrade => true;

            /// <summary>A thumbnail has no grade, so nothing to end before.</summary>
            private bool HasBar => Space.Grade < Space.HalfWidth - 0.01f;

            private float Bar => HasBar ? Space.Grade - 0.07f : Space.HalfWidth - 0.1f;

            protected override void Outline(Path path, float grow) => PlateOutline(path, grow, Corner.Round, 0f);

            protected override void Arrange()
            {
                float left = Space.Compact ? -Space.HalfWidth + 0.1f : Space.Band + 0.03f;
                for (int i = 0; i < VOICES; i++)
                {
                    Path voice = _voices[i];
                    voice.Clear();
                    float start = (1.5f - i) * 0.2f;
                    float end = (1.5f - i) * 0.13f;
                    const int STEPS = 28;
                    for (int step = 0; step <= STEPS; step++)
                    {
                        float t = (float)step / STEPS;
                        float wave = (float)Math.Sin(t * PI * 2.6f + i * 1.7f) * 0.075f * (1f - t);
                        voice.Add(VectorMesh.Mix(left, Bar, t), VectorMesh.Mix(start, end, VectorMesh.Smooth(0.2f, 1f, t)) + wave);
                    }
                }
            }

            internal override void Ground(VectorMesh m)
            {
                float w = Space.HalfWidth;
                float h = Space.HalfHeight;
                m.Box(0f, 0f, w, h, Space.Radius, new VectorMesh.Rise(_groundBottom, _groundTop, -h, h));
                float left = Space.Compact ? -w + 0.12f : Space.Band;
                for (int i = 0; i < 5; i++)
                {
                    float y = (2 - i) * (Space.Compact ? 0.17f : 0.13f);
                    m.Line(left, y, Bar, y, 0.008f, _ivory.With(0.08f), _ivory.With(0.28f));
                }

                if (!Space.Compact)
                {
                    Path band = m.NewPath();
                    LeftPart(band, Space.Band, Corner.Round, 0f);
                    m.Fill(band, new Point((Space.Band - w) * 0.5f, 0f), new VectorMesh.Rise(_ivory.With(0.05f), _ivory.With(0.22f), -h, h));
                    m.Line(Space.Band, h, Space.Band, -h, 0.02f, _ivory.With(0.7f), _gold.With(0.6f));
                    for (int i = 0; i < VOICES; i++)
                        m.Stroke(_voices[i], false, 0.018f, new VectorMesh.Across(_ivory.With(0.14f), _ivory.With(0.85f), Space.TextEnd - 0.3f, Bar));
                }

                if (!HasBar) return;
                // The final barline: thin, then thick.
                float tall = Space.Compact ? 0.34f : 0.3f;
                m.Glow(Bar + 0.04f, 0f, 0.4f, 0.4f, _gold.With(0.16f));
                m.Line(Bar, tall, Bar, -tall, 0.018f, _ivory);
                m.Line(Bar + 0.07f, tall, Bar + 0.07f, -tall, 0.052f, _ivory);
            }

            internal override void GroundMotion(VectorMesh m, float time)
            {
                float at = VectorMesh.Cycle(time, LOOP) * LOOP;
                float answer = 0f;
                for (int i = 0; i < VOICES; i++)
                {
                    float since = at - i * 0.26f;
                    float travel = since / 1.9f;
                    if (travel > 0f && travel < 1.3f && !Space.Compact)
                    {
                        Point note = _voices[i].At(Math.Min(1f, travel), false);
                        float shown = VectorMesh.Mix(0.28f, 1f, VectorMesh.Smooth(Space.TextEnd - 0.15f, Space.TextEnd + 0.2f, note.X));
                        m.Stroke(_voices[i], false, 0.036f, new VectorMesh.Comet(_white.With(shown), _gold, travel, 0.2f, false));
                        if (travel < 1f)
                        {
                            m.Glow(note.X, note.Y, 0.13f, 0.13f, _gold.With(0.6f * shown));
                            m.Ellipse(note.X, note.Y, 0.046f, 0.034f, 0.4f, _white.With(shown));
                        }
                    }

                    float arrived = since - 1.9f;
                    if (arrived >= 0f && arrived < 1f) answer = Math.Max(answer, (1f - arrived) * (1f - arrived));
                }

                if (answer <= 0f || !HasBar) return;
                float tall = Space.Compact ? 0.34f : 0.3f;
                m.Glow(Bar + 0.04f, 0f, 0.5f, 0.5f, _gold.With(0.5f * answer));
                m.Line(Bar, tall, Bar, -tall, 0.03f, _white.With(answer));
                m.Line(Bar + 0.07f, tall, Bar + 0.07f, -tall, 0.07f, _white.With(answer));
            }

            internal override void Frame(VectorMesh m)
            {
                if (Space.Compact) return;
                // A fermata rests on the frame's star: hold it as long as you like.
                float edge = Space.HalfHeight + 0.062f;
                Path arc = m.NewPath();
                arc.Arc(0f, edge + 0.02f, 0.22f, 0.2f, PI * 0.94f, PI * 0.06f, 18);
                m.Stroke(arc, false, 0.026f, new VectorMesh.Spectrum(0.5f / (Space.HalfWidth * 2f), 0.25f, 0.3f, 1f));
            }

            internal override void FrameMotion(VectorMesh m, float time)
            {
                float at = VectorMesh.Cycle(time, LOOP * 2f);
                RunAround(m, _white, Ink.Prism(0.3f), Space.Compact ? 0.06f : 0.044f, at, 0.16f);
                RunAround(m, _white, Ink.Prism(0.8f), Space.Compact ? 0.06f : 0.044f, VectorMesh.Cycle(at + 0.5f, 1f), 0.16f);
            }

            internal override void Emblem(VectorMesh m)
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = (1.5f - i) * 0.2f;
                    Path voice = m.NewPath();
                    voice.Add(-0.48f, y * 1.5f + (i % 2 == 0 ? 0.06f : -0.06f));
                    voice.Curve(new Point(-0.28f, y * 1.5f - 0.04f), new Point(-0.1f, y), new Point(0.14f, y * 0.8f), 12);
                    m.Stroke(voice, false, 0.045f, new VectorMesh.Ramp(_ivory.With(0.35f), _ivory));
                }

                m.Line(0.2f, 0.42f, 0.2f, -0.42f, 0.05f, _ivory);
                m.Line(0.37f, 0.42f, 0.37f, -0.42f, 0.13f, _ivory);
            }

            internal override void Disc(VectorMesh m)
            {
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.5f, 0.5f, 0f, TAU, 64, false);
                m.Fill(path, default, new VectorMesh.Rise(_groundBottom, _groundTop, -0.5f, 0.5f));
                for (int i = 0; i < 5; i++)
                {
                    float y = 0.06f + (2 - i) * 0.1f;
                    float half = (float)Math.Sqrt(0.42f * 0.42f - y * y);
                    m.Line(-half, y, half, y, 0.008f, _ivory.With(0.16f));
                }

                // The final barline, bent round.
                path = m.NewPath();
                path.Arc(0f, 0f, 0.473f, 0.473f, 0f, TAU, 64, false);
                m.Stroke(path, true, 0.06f, new VectorMesh.Spectrum(0.5f, 0.75f, 0.15f, 1f));
                path = m.NewPath();
                path.Arc(0f, 0f, 0.55f, 0.55f, 0f, TAU, 64, false);
                m.Stroke(path, true, 0.018f, new VectorMesh.Spectrum(0.5f, 0.75f, 0.2f, 0.9f));
                for (int i = 0; i < 4; i++)
                {
                    Point gem = Point.Polar(i * PI * 0.5f, 0.55f);
                    m.Diamond(gem.X, gem.Y, 0.062f, Ink.Prism(i == 0 ? 0.5f : i == 2 ? 0f : 0.25f), _white);
                }
            }

            internal override void DiscMotion(VectorMesh m, float time)
            {
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.473f, 0.473f, PI * 0.5f, -PI * 1.5f, 64, false);
                float at = VectorMesh.Cycle(time, LOOP);
                m.Stroke(path, true, 0.06f, new VectorMesh.Comet(_white, Ink.Prism(0.3f), at, 0.2f, true));
                m.Stroke(path, true, 0.06f, new VectorMesh.Comet(_white, Ink.Prism(0.8f), VectorMesh.Cycle(at + 0.5f, 1f), 0.2f, true));
                for (int i = 0; i < VOICES; i++)
                {
                    float travel = VectorMesh.Cycle(time - i * 0.26f, LOOP) * LOOP / 1.9f;
                    if (travel > 1f) continue;
                    float y = 0.16f - i * 0.1f;
                    float x = VectorMesh.Mix(-0.36f, 0.36f, travel);
                    float shown = (float)Math.Sin(travel * PI);
                    m.Glow(x, y, 0.07f, 0.07f, _gold.With(0.5f * shown));
                }
            }
        }

        /// <summary>The number 100, <paramref name="half"/> half as high.</summary>
        internal static void Hundred(VectorMesh m, float x, float y, float half, float width, Ink ink)
        {
            float round = half * 0.62f;
            Path one = m.NewPath();
            one.Add(x - round * 3.1f, y + half * 0.55f);
            one.Add(x - round * 2.5f, y + half);
            one.Add(x - round * 2.5f, y - half);
            m.Stroke(one, false, width, ink);
            for (int i = 0; i < 2; i++)
            {
                Path zero = m.NewPath();
                zero.Arc(x - round * 0.55f + i * round * 2.45f, y, round * 0.85f, half, 0f, TAU, 20, false);
                m.Stroke(zero, true, width, ink);
            }
        }
    }
}
