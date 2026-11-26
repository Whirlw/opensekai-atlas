using System;
using Ink = OpenSekai.App.UI.VectorMesh.Ink;
using Path = OpenSekai.App.UI.VectorMesh.Path;
using Point = OpenSekai.App.UI.VectorMesh.Point;

namespace OpenSekai.App.UI
{
    internal abstract partial class SpecialPlateArt
    {
        /// <summary>
        /// 皆传 α to δ: FULL COMBO as one line that never breaks. The frame is a single golden line; it
        /// rises out of the top into one peak per letter, and the face carries the same line from the
        /// band to the grade with the same peaks. A light runs along both without stopping, and each
        /// peak flashes as it passes.
        /// </summary>
        private sealed class FullComboArt : SpecialPlateArt
        {
            private const float LAP = 4.2f;

            private static readonly Ink _top = Ink.Of(255, 240, 186);
            private static readonly Ink _deep = Ink.Of(255, 176, 84);
            private static readonly Ink _gold = Ink.Of(255, 206, 122);
            private static readonly Ink _white = Ink.Of(255, 252, 238);
            private static readonly Ink _groundTop = Ink.Of(44, 47, 74);
            private static readonly Ink _groundBottom = Ink.Of(17, 19, 36);

            private readonly int _rank;
            private readonly Path _trace = new Path();
            private readonly float[] _tips = new float[4];
            private Peak[] _peaks;
            private Peak[] _under;
            private float _traceLeft;
            private float _traceRight;

            internal FullComboArt(int rank) => _rank = rank;

            internal override Ink TextTop => _top;

            internal override Ink TextBottom => Ink.Of(255, 200, 118);

            private float Base => -0.25f;

            protected override void Outline(Path path, float grow)
            {
                _peaks = TopPeaks();
                _under = BottomPeaks();
                PlateOutline(path, grow, Corner.Round, 0f, _peaks, _under);
            }

            protected override void Arrange()
            {
                _trace.Clear();
                if (Space.Compact) return;
                // The line across the face: flat under the text, then a peak per letter before the grade.
                _traceRight = Space.Grade - 0.14f;
                _traceLeft = Math.Max(Space.TextEnd + 0.2f, _traceRight - 0.42f - 0.46f * _rank);
                _trace.Add(Space.Band, Base);
                float width = _traceRight - _traceLeft;
                int peaks = width > 0.35f ? _rank : 0;
                for (int i = 0; i < peaks; i++)
                {
                    float half = width / _rank * 0.5f;
                    float middle = _traceLeft + half * (2 * i + 1);
                    float height = FaceHeight(i);
                    if (i == 0) _trace.Add(middle - half, Base);
                    for (int step = 1; step <= 16; step++)
                    {
                        float t = step / 16f * 2f - 1f;
                        _trace.Add(middle + half * t, Base + height * (float)Math.Pow(1f - Math.Abs(t), 2.1d));
                    }
                }

                _trace.Add(Space.Grade, Base);
                _trace.Subdivide(0.07f, false);
                float total = _trace.Length(false);
                for (int i = 0; i < peaks; i++)
                {
                    float middle = _traceLeft + width / _rank * (i + 0.5f);
                    float along = 0f;
                    for (int p = 1; p < _trace.Count && _trace[p].X <= middle + 0.001f; p++) along += (_trace[p] - _trace[p - 1]).Length;
                    _tips[i] = along / total;
                }
            }

            internal override void Ground(VectorMesh m)
            {
                var sky = new VectorMesh.Rise(_groundBottom, _groundTop, -Space.HalfHeight, Space.HalfHeight);
                foreach (Peak peak in _peaks) PeakGround(m, peak, 1f);
                foreach (Peak peak in _under) PeakGround(m, peak, -1f);
                m.Box(0f, 0f, Space.HalfWidth, Space.HalfHeight, Space.Radius, sky);
                if (Space.Compact)
                {
                    m.Glow(0f, Space.HalfHeight, Space.HalfWidth * 0.6f, Space.HalfHeight, _gold.With(0.12f));
                    return;
                }

                // A sheet of fine lines, as under a score's graph.
                for (int i = 0; i < 4; i++)
                {
                    float y = Base + 0.14f * (i + 1);
                    m.Line(Space.Band, y, Space.Grade, y, 0.008f, _gold.With(0.02f), _gold.With(0.14f));
                }

                Path band = m.NewPath();
                LeftPart(band, Space.Band, Corner.Round, 0f);
                m.Fill(band, new Point((Space.Band - Space.HalfWidth) * 0.5f, 0f),
                    new VectorMesh.Rise(_deep.With(0.16f), _gold.With(0.4f), -0.4f, 0.4f));
                m.Line(Space.Band, Space.HalfHeight, Space.Band, -Space.HalfHeight, 0.026f, _top.With(0.95f), _deep.With(0.9f));

                int peaks = _traceRight - _traceLeft > 0.35f ? _rank : 0;
                if (peaks > 0)
                {
                    var top = new Path();
                    var bottom = new Path();
                    for (int i = 0; i < _trace.Count; i++)
                    {
                        if (_trace[i].X < _traceLeft || _trace[i].X > _traceRight) continue;
                        top.Add(_trace[i]);
                        bottom.Add(_trace[i].X, Base - 0.002f);
                    }

                    m.Strip(top, bottom, _gold.With(0.34f), _deep.With(0.1f));
                    for (int i = 0; i < peaks; i++)
                    {
                        float x = _traceLeft + (_traceRight - _traceLeft) / _rank * (i + 0.5f);
                        m.Line(x, Base + FaceHeight(i), x, Base, 0.012f, _top.With(0.5f), _top.With(0.1f));
                    }
                }

                m.Stroke(_trace, false, 0.026f, new VectorMesh.Across(_gold.With(0.3f), _gold.With(0.9f), Space.Band, Space.Grade));
                for (int i = 0; i < peaks; i++)
                {
                    float x = _traceLeft + (_traceRight - _traceLeft) / _rank * (i + 0.5f);
                    m.Diamond(x, Base + FaceHeight(i), 0.04f, _top, _white);
                }
            }

            internal override void GroundMotion(VectorMesh m, float time)
            {
                if (Space.Compact) return;
                float at = VectorMesh.Cycle(time, LAP) * 1.7f;
                m.Stroke(_trace, false, 0.06f, new VectorMesh.Comet(_white, _gold, at, 0.3f, false));
                m.Stroke(_trace, false, 0.14f, new VectorMesh.Comet(_gold.With(0.4f), _deep, at, 0.2f, false));
                if (_traceRight - _traceLeft <= 0.35f) return;
                for (int i = 0; i < _rank; i++)
                {
                    float since = at - _tips[i];
                    if (since < 0f || since > 0.5f) continue;
                    float flash = (1f - since / 0.5f) * (1f - since / 0.5f);
                    float x = _traceLeft + (_traceRight - _traceLeft) / _rank * (i + 0.5f);
                    float y = Base + FaceHeight(i);
                    m.Glow(x, y, 0.22f, 0.22f, _gold.With(0.6f * flash));
                    m.Sparkle(x, y, 0.16f * flash, 0.16f * flash, _white.With(flash));
                }
            }

            internal override void Frame(VectorMesh m)
            {
                bool compact = Space.Compact;
                var gold = new VectorMesh.Rise(_deep, _top, -Space.HalfHeight, Space.HalfHeight + 0.2f);
                float outer = compact ? 0.09f : 0.062f;
                Path path;
                if (!compact)
                {
                    path = m.NewPath();
                    PlateOutline(path, -0.085f, Corner.Round, 0f);
                    m.Stroke(path, true, 0.011f, _gold.With(0.45f));
                }

                path = m.NewPath();
                PlateOutline(path, outer, Corner.Round, 0f, _peaks, _under);
                m.Stroke(path, true, compact ? 0.032f : 0.018f, _gold.With(0.85f));
                if (_rank >= 2 && !compact)
                {
                    path = m.NewPath();
                    PlateOutline(path, outer + 0.042f, Corner.Round, 0f, _peaks, _under);
                    m.Stroke(path, true, 0.01f, _gold.With(0.55f));
                }

                if (_rank >= 4 && !compact)
                {
                    path = m.NewPath();
                    PlateOutline(path, outer + 0.075f, Corner.Round, 0f, _peaks, _under);
                    m.Stroke(path, true, 0.008f, _gold.With(0.35f));
                }

                // The line itself, one stroke all the way round.
                path = m.NewPath();
                PlateOutline(path, compact ? -0.02f : -0.016f, Corner.Round, 0f, _peaks, _under);
                m.Stroke(path, true, compact ? 0.062f : 0.042f, gold);

                EndDiamonds(m, -Space.HalfWidth - outer, -1f, _gold, _white, compact ? 1 : _rank >= 3 ? 3 : 2);
                EndDiamonds(m, Space.HalfWidth + outer, 1f, _gold, _white, compact ? 1 : _rank >= 3 ? 3 : 2);
                float last = _peaks[_peaks.Length - 1].X + _peaks[_peaks.Length - 1].Half;
                if (!compact)
                {
                    SideDiamonds(m, last + 0.14f, Space.HalfHeight + outer, _gold, _white);
                    CornerDiamonds(m, outer, Space.Radius, 0.062f, _gold, _white);
                    if (_under.Length == 0)
                    {
                        m.Diamond(0f, -Space.HalfHeight - outer, 0.085f, _gold, _white);
                        SideDiamonds(m, 0.16f, -Space.HalfHeight - outer, _gold, _white);
                    }
                    else
                    {
                        SideDiamonds(m, _under[_under.Length - 1].X + _under[0].Half + 0.12f, -Space.HalfHeight - outer, _gold, _white);
                    }
                }

                foreach (Peak peak in _peaks) m.Diamond(peak.X, Space.HalfHeight + peak.Height + 0.03f, compact ? 0.075f : 0.052f, _top, _white);
            }

            internal override void FrameMotion(VectorMesh m, float time)
            {
                bool compact = Space.Compact;
                float at = VectorMesh.Cycle(time, LAP);
                float width = compact ? 0.075f : 0.05f;
                int lights = _rank >= 3 ? 2 : 1;
                for (int i = 0; i < lights; i++)
                {
                    float head = at + (float)i / lights;
                    head -= (float)Math.Floor(head);
                    RunAround(m, _white, _top, width, head, 0.2f);
                    RunAround(m, _gold.With(0.35f), _deep, width * 3f, head, 0.12f);
                    foreach (Peak peak in _peaks)
                    {
                        // The middle of the top edge is a quarter of the way round, a little more or less.
                        float quarter = 0.25f + peak.X / (4f * (Space.HalfWidth + Space.HalfHeight));
                        float flash = Passed(head, quarter, 0.14f);
                        if (flash <= 0f) continue;
                        float y = Space.HalfHeight + peak.Height + 0.03f;
                        m.Glow(peak.X, y, 0.26f, 0.26f, _gold.With(0.7f * flash));
                        m.Sparkle(peak.X, y, 0.2f * flash, 0.2f * flash, _white.With(flash));
                    }
                }
            }

            internal override void Emblem(VectorMesh m)
            {
                Path path = m.NewPath();
                EmblemLine(path);
                m.Stroke(path, false, 0.075f, new VectorMesh.Rise(_deep, _top, -0.3f, 0.4f));
                for (int i = 0; i < _rank; i++)
                {
                    Point tip = EmblemTip(i);
                    m.Diamond(tip.X, tip.Y, _rank > 2 ? 0.085f : 0.1f, _top, _white);
                }

                m.Dot(-0.47f, -0.24f, 0.055f, _gold);
                m.Dot(0.47f, -0.24f, 0.055f, _gold);
                for (int i = 0; i < (_rank + 1) / 2; i++)
                {
                    float half = 0.36f - i * 0.12f;
                    m.Line(-half, -0.38f - i * 0.09f, half, -0.38f - i * 0.09f, 0.045f, _gold.With(0.6f - i * 0.2f));
                }
            }

            internal override void Disc(VectorMesh m)
            {
                Path path = m.NewPath();
                DiscLine(path, 0f);
                m.Fill(path, default, new VectorMesh.Rise(_groundBottom, _groundTop, -0.5f, 0.6f));
                m.Glow(0f, -0.1f, 0.46f, 0.4f, _gold.With(0.2f));
                m.Ring(0f, 0f, 0.4f, 0.014f, _gold.With(0.45f));
                path = m.NewPath();
                DiscLine(path, -0.018f);
                m.Stroke(path, true, 0.042f, new VectorMesh.Rise(_deep, _top, -0.5f, 0.6f));
                for (int i = 0; i < _rank; i++)
                {
                    Point tip = DiscTip(i);
                    m.Diamond(tip.X, tip.Y, 0.05f, _top, _white);
                }
            }

            internal override void DiscMotion(VectorMesh m, float time)
            {
                Path path = m.NewPath();
                DiscLine(path, -0.018f);
                float at = VectorMesh.Cycle(time, LAP);
                int lights = _rank >= 3 ? 2 : 1;
                for (int i = 0; i < lights; i++)
                {
                    float head = VectorMesh.Cycle(at + (float)i / lights, 1f);
                    m.Stroke(path, true, 0.05f, new VectorMesh.Comet(_white, _top, head, 0.28f, true));
                }

                for (int i = 0; i < _rank; i++)
                {
                    Point tip = DiscTip(i);
                    float flash = 0f;
                    for (int light = 0; light < lights; light++)
                        flash = Math.Max(flash, Passed(VectorMesh.Cycle(at + (float)light / lights, 1f), 0.5f - (i - (_rank - 1) * 0.5f) * 0.045f, 0.2f));
                    if (flash <= 0f) continue;
                    m.Glow(tip.X, tip.Y, 0.2f, 0.2f, _gold.With(0.6f * flash));
                    m.Sparkle(tip.X, tip.Y, 0.15f * flash, 0.15f * flash, _white.With(flash));
                }
            }

            private Peak[] TopPeaks()
            {
                float zoom = Space.Compact ? 0.82f : 1f;
                switch (_rank)
                {
                    case 1: return new[] { new Peak(0f, 0.36f * zoom, 0.25f * zoom) };
                    case 2: return Pair(new Peak(0.19f * zoom, 0.24f * zoom, 0.25f * zoom));
                    case 3:
                        return new[]
                        {
                            new Peak(-0.36f * zoom, 0.22f * zoom, 0.18f * zoom), new Peak(0f, 0.24f * zoom, 0.26f * zoom),
                            new Peak(0.36f * zoom, 0.22f * zoom, 0.18f * zoom)
                        };
                    default:
                        return new[]
                        {
                            new Peak(-0.5f * zoom, 0.21f * zoom, 0.18f * zoom), new Peak(-0.17f * zoom, 0.21f * zoom, 0.26f * zoom),
                            new Peak(0.17f * zoom, 0.21f * zoom, 0.26f * zoom), new Peak(0.5f * zoom, 0.21f * zoom, 0.18f * zoom)
                        };
                }
            }

            private Peak[] BottomPeaks()
            {
                if (_rank < 3 || Space.Compact) return Array.Empty<Peak>();
                return _rank == 3 ? new[] { new Peak(0f, 0.26f, 0.15f) } : Pair(new Peak(0.17f, 0.2f, 0.15f));
            }

            private static Peak[] Pair(Peak right) => new[] { new Peak(-right.X, right.Half, right.Height), right };

            private void PeakGround(VectorMesh m, Peak peak, float up)
            {
                var top = new Path();
                var bottom = new Path();
                float edge = up * (Space.HalfHeight - 0.03f);
                for (int i = 0; i <= 14; i++)
                {
                    float t = i / 7f - 1f;
                    float x = peak.X + t * peak.Half;
                    top.Add(x, up * (Space.HalfHeight + peak.Height * (float)Math.Pow(1f - Math.Abs(t), 2.4d)));
                    bottom.Add(x, edge);
                }

                Ink ink = up > 0f ? _groundTop : _groundBottom;
                m.Strip(top, bottom, Ink.Lerp(ink, _gold, 0.4f), ink);
            }

            /// <summary>How high a peak on the face stands.</summary>
            private float FaceHeight(int index)
            {
                switch (_rank)
                {
                    case 1: return 0.52f;
                    case 2: return index == 0 ? 0.38f : 0.52f;
                    case 3: return index == 1 ? 0.53f : 0.36f;
                    default: return index == 1 || index == 2 ? 0.53f : 0.36f;
                }
            }

            private void EmblemLine(Path path)
            {
                path.Add(-0.48f, -0.24f);
                float half = 0.36f / _rank;
                for (int i = 0; i < _rank; i++)
                {
                    Point tip = EmblemTip(i);
                    float height = tip.Y + 0.24f;
                    for (int step = i == 0 ? 0 : 1; step <= 14; step++)
                    {
                        float t = step / 7f - 1f;
                        path.Add(tip.X + half * t, -0.24f + height * (float)Math.Pow(1f - Math.Abs(t), _rank > 2 ? 1.6d : 2d));
                    }
                }

                path.Add(0.48f, -0.24f);
            }

            private Point EmblemTip(int index)
            {
                float x = (index - (_rank - 1) * 0.5f) * 0.72f / _rank;
                float outside = _rank < 2 ? 0f : Math.Abs(index - (_rank - 1) * 0.5f) / ((_rank - 1) * 0.5f);
                return new Point(x, _rank == 2 ? (index == 0 ? 0.2f : 0.38f) : 0.38f - 0.16f * (_rank > 2 ? outside * outside : 0f));
            }

            /// <summary>A circle that rises into the peaks at its top.</summary>
            private void DiscLine(Path path, float grow)
            {
                const int STEPS = 120;
                for (int i = 0; i < STEPS; i++)
                {
                    float angle = -PI * 0.5f - TAU * i / STEPS;
                    float rise = 0f;
                    for (int peak = 0; peak < _rank; peak++)
                    {
                        float away = (float)Math.IEEERemainder(angle - DiscAngle(peak), TAU);
                        float near = 1f - Math.Abs(away) / (_rank == 1 ? 0.5f : 0.3f);
                        if (near > 0f) rise = Math.Max(rise, DiscHeight(peak) * (float)Math.Pow(near, 2.2d));
                    }

                    path.Add(Point.Polar(angle, 0.5f + grow + rise));
                }
            }

            private float DiscAngle(int index) => PI * 0.5f - (index - (_rank - 1) * 0.5f) * 0.36f;

            private float DiscHeight(int index)
            {
                float outside = _rank < 3 ? 0f : Math.Abs(index - (_rank - 1) * 0.5f);
                return 0.17f - 0.045f * outside;
            }

            private Point DiscTip(int index) => Point.Polar(DiscAngle(index), 0.5f + DiscHeight(index) + 0.005f);
        }

        /// <summary>
        /// 真·皆传 α to δ: ALL PERFECT as a crystal without a flaw. The plate's corners are cut, its
        /// strokes run through the prism, and one crystal per letter stands on its top edge and on its
        /// face. The colors drift along the frame, and the crystals catch the light one after another.
        /// </summary>
        private sealed class AllPerfectArt : SpecialPlateArt
        {
            private const float CUT = 0.17f;
            private const float DRIFT = 6f;
            private const float GLINT = 3.4f;

            private static readonly Ink _white = Ink.White(1f);
            private static readonly Ink _pale = Ink.Of(232, 220, 255);
            private static readonly Ink _mid = Ink.Of(184, 162, 246);
            private static readonly Ink _shade = Ink.Of(122, 104, 204);
            private static readonly Ink _groundTop = Ink.Of(64, 50, 102);
            private static readonly Ink _groundBottom = Ink.Of(22, 17, 42);

            private readonly int _rank;
            private Crystal[] _crown;
            private Crystal[] _cluster;

            internal AllPerfectArt(int rank) => _rank = rank;

            internal override Ink TextTop => _white;

            internal override Ink TextBottom => Ink.Of(214, 196, 255);

            private float Turns => 0.5f / (Space.HalfWidth * 2f);

            protected override void Outline(Path path, float grow) =>
                PlateOutline(path, grow, Corner.Cut, Space.Compact ? 0.24f : CUT);

            protected override void Arrange()
            {
                float zoom = Space.Compact ? 0.82f : 1f;
                float edge = Space.HalfHeight;
                switch (_rank)
                {
                    case 1: _crown = new[] { new Crystal(0f, edge + 0.04f, 0.14f, 0.25f, 0.19f, 0f, zoom) }; break;
                    case 2:
                        _crown = new[]
                        {
                            new Crystal(-0.15f * zoom, edge + 0.03f, 0.115f, 0.21f, 0.15f, 0.1f, zoom),
                            new Crystal(0.15f * zoom, edge + 0.04f, 0.125f, 0.25f, 0.17f, -0.1f, zoom)
                        };
                        break;
                    case 3:
                        _crown = new[]
                        {
                            new Crystal(-0.25f * zoom, edge + 0.01f, 0.1f, 0.19f, 0.12f, 0.34f, zoom),
                            new Crystal(0.25f * zoom, edge + 0.01f, 0.1f, 0.19f, 0.12f, -0.34f, zoom),
                            new Crystal(0f, edge + 0.04f, 0.135f, 0.26f, 0.18f, 0f, zoom)
                        };
                        break;
                    default:
                        _crown = new[]
                        {
                            new Crystal(-0.41f * zoom, edge, 0.09f, 0.17f, 0.1f, 0.5f, zoom),
                            new Crystal(0.41f * zoom, edge, 0.09f, 0.17f, 0.1f, -0.5f, zoom),
                            new Crystal(-0.145f * zoom, edge + 0.04f, 0.125f, 0.25f, 0.16f, 0.12f, zoom),
                            new Crystal(0.145f * zoom, edge + 0.04f, 0.125f, 0.25f, 0.16f, -0.12f, zoom)
                        };
                        break;
                }

                // On the face: the crystals stand before the grade, the tallest last but one.
                _cluster = Array.Empty<Crystal>();
                float right = Space.Grade - 0.1f;
                float width = Math.Min(0.36f * _rank + 0.1f, right - Space.TextEnd - 0.16f);
                if (Space.Compact || width < 0.3f) return;
                _cluster = new Crystal[_rank];
                float step = width / _rank;
                for (int i = 0; i < _rank; i++)
                {
                    float tall = _rank == 1 ? 1f : i == _rank - 2 ? 1f : i == _rank - 1 ? 0.82f : 0.62f + 0.08f * i;
                    float up = 0.4f * tall;
                    float down = 0.2f;
                    _cluster[i] = new Crystal(right - width + step * (i + 0.5f), -0.31f + down, Math.Min(0.155f, step * 0.46f), up, down,
                        (i - (_rank - 1) * 0.5f) * -0.1f, 1f);
                }
            }

            internal override void Ground(VectorMesh m)
            {
                float w = Space.HalfWidth;
                float h = Space.HalfHeight;
                Path face = m.NewPath();
                Outline(face, 0f);
                m.Fill(face, default, new VectorMesh.Rise(_groundBottom, _groundTop, -h, h));
                if (Space.Compact)
                {
                    Facets(m, -w + 0.4f, w - 0.4f, 0.09f);
                    return;
                }

                Facets(m, Space.Band + 0.1f, Space.Grade, 0.075f);
                Path band = m.NewPath();
                LeftPart(band, Space.Band, Corner.Cut, CUT);
                m.Fill(band, new Point((Space.Band - w) * 0.5f, 0f), new VectorMesh.Rise(_shade.With(0.2f), _pale.With(0.36f), -h, h));
                m.Line(Space.Band, h, Space.Band, -h, 0.026f, Ink.Prism(0f, 0.95f), Ink.Prism(0.5f, 0.95f));
                if (_cluster.Length == 0) return;
                float middle = (_cluster[0].X + _cluster[_cluster.Length - 1].X) * 0.5f;
                m.Glow(middle, -0.1f, 0.9f, 0.42f, _mid.With(0.3f));
                m.Line(middle - 0.5f - 0.2f * _rank, -0.31f, middle + 0.25f + 0.1f * _rank, -0.31f, 0.014f, _pale.With(0f), _pale.With(0.6f));
                foreach (Crystal crystal in _cluster) Draw(m, crystal, 0.86f);
            }

            internal override void GroundMotion(VectorMesh m, float time)
            {
                float at = VectorMesh.Cycle(time, GLINT);
                for (int i = 0; i < _cluster.Length; i++) Glint(m, _cluster[i], Passed(at, 0.1f + i * 0.09f, 0.22f), 0.85f);
            }

            internal override void Frame(VectorMesh m)
            {
                bool compact = Space.Compact;
                float outer = compact ? 0.09f : 0.062f;
                var prism = new VectorMesh.Spectrum(Turns, 0.5f, 0.22f, 1f);
                Path path;
                if (!compact)
                {
                    path = m.NewPath();
                    PlateOutline(path, -0.085f, Corner.Cut, CUT);
                    m.Stroke(path, true, 0.011f, new VectorMesh.Spectrum(Turns, 0.5f, 0.4f, 0.5f));
                }

                path = m.NewPath();
                Outline(path, outer);
                path.Subdivide(0.2f, true);
                m.Stroke(path, true, compact ? 0.032f : 0.018f, new VectorMesh.Spectrum(Turns, 0.5f, 0.3f, 0.85f));
                if (_rank >= 2 && !compact)
                {
                    path = m.NewPath();
                    Outline(path, outer + 0.042f);
                    path.Subdivide(0.2f, true);
                    m.Stroke(path, true, 0.01f, new VectorMesh.Spectrum(Turns, 0.5f, 0.3f, 0.55f));
                }

                if (_rank >= 4 && !compact)
                {
                    path = m.NewPath();
                    Outline(path, outer + 0.075f);
                    path.Subdivide(0.2f, true);
                    m.Stroke(path, true, 0.008f, new VectorMesh.Spectrum(Turns, 0.5f, 0.3f, 0.35f));
                }

                path = m.NewPath();
                Outline(path, compact ? -0.02f : -0.016f);
                path.Subdivide(0.2f, true);
                m.Stroke(path, true, compact ? 0.062f : 0.04f, prism);

                EndDiamonds(m, -Space.HalfWidth - outer, -1f, Ink.Prism(0f), _white, compact ? 1 : _rank >= 3 ? 3 : 2);
                EndDiamonds(m, Space.HalfWidth + outer, 1f, Ink.Prism(0.5f), _white, compact ? 1 : _rank >= 3 ? 3 : 2);
                if (!compact)
                {
                    Ink violet = Ink.Prism(0.25f);
                    float reach = 0.22f + 0.13f * _rank;
                    SideDiamonds(m, reach, Space.HalfHeight + outer, violet, _white);
                    SideDiamonds(m, _rank >= 3 ? 0.3f : 0.18f, -Space.HalfHeight - outer, violet, _white);
                    // Small stones set in the cut corners.
                    float cut = CUT + outer * 0.42f;
                    float x = Space.HalfWidth + outer - cut * 0.5f;
                    float y = Space.HalfHeight + outer - cut * 0.5f;
                    for (int i = 0; i < 4; i++)
                    {
                        bool right = i % 2 == 0;
                        m.Diamond(right ? x : -x, i < 2 ? y : -y, 0.056f, Ink.Prism(right ? 0.5f : 0f), _white);
                    }

                    int under = _rank >= 3 ? _rank - 1 : 1;
                    for (int i = 0; i < under; i++)
                        Draw(m, new Crystal((i - (under - 1) * 0.5f) * 0.17f, -Space.HalfHeight - outer, 0.055f, 0.08f, 0.11f, 0f, 1f), 1f);
                }

                foreach (Crystal crystal in _crown) Draw(m, crystal, 1f);
            }

            internal override void FrameMotion(VectorMesh m, float time)
            {
                bool compact = Space.Compact;
                // The prism moves on along the whole outline, over the one that stands still.
                m.Stroke(OutlinePath, true, compact ? 0.05f : 0.03f, new VectorMesh.Spectrum(Turns * 2f, 0.5f - VectorMesh.Cycle(time, DRIFT), 0.35f, 0.9f));
                float at = VectorMesh.Cycle(time, GLINT);
                RunAround(m, _white, _pale, compact ? 0.06f : 0.04f, VectorMesh.Cycle(time, GLINT * 2f), 0.14f);
                for (int i = 0; i < _crown.Length; i++)
                {
                    float turn = (_crown[i].X + 0.5f) * 0.3f;
                    Glint(m, _crown[i], Passed(at, turn, 0.24f), 1f);
                }

                if (compact) return;
                for (int i = 0; i < 2 + _rank; i++)
                {
                    float shown = (float)Math.Sin(VectorMesh.Cycle(time + i * 0.83f, 2.6f) * PI);
                    float x = (VectorMesh.Cycle(i * 0.381f + 0.11f, 1f) * 2f - 1f) * Space.HalfWidth * 0.94f;
                    float y = (i % 2 == 0 ? 1f : -1f) * (Space.HalfHeight + 0.05f);
                    float size = 0.1f * shown * shown;
                    if (size > 0.012f) m.Sparkle(x, y, size, size, _white.With(shown));
                }
            }

            internal override void Emblem(VectorMesh m)
            {
                switch (_rank)
                {
                    case 1: Draw(m, new Crystal(0f, 0f, 0.26f, 0.46f, 0.4f, 0f, 1f), 1f); break;
                    case 2:
                        Draw(m, new Crystal(-0.17f, -0.07f, 0.18f, 0.34f, 0.3f, 0.08f, 1f), 1f);
                        Draw(m, new Crystal(0.15f, 0.02f, 0.2f, 0.44f, 0.36f, -0.06f, 1f), 1f);
                        break;
                    case 3:
                        Draw(m, new Crystal(-0.27f, -0.12f, 0.15f, 0.3f, 0.22f, 0.36f, 1f), 1f);
                        Draw(m, new Crystal(0.27f, -0.12f, 0.15f, 0.3f, 0.22f, -0.36f, 1f), 1f);
                        Draw(m, new Crystal(0f, 0f, 0.21f, 0.46f, 0.38f, 0f, 1f), 1f);
                        break;
                    default:
                        Draw(m, new Crystal(-0.34f, -0.18f, 0.12f, 0.26f, 0.16f, 0.52f, 1f), 1f);
                        Draw(m, new Crystal(0.34f, -0.18f, 0.12f, 0.26f, 0.16f, -0.52f, 1f), 1f);
                        Draw(m, new Crystal(-0.14f, -0.02f, 0.16f, 0.4f, 0.32f, 0.14f, 1f), 1f);
                        Draw(m, new Crystal(0.14f, -0.02f, 0.16f, 0.4f, 0.32f, -0.14f, 1f), 1f);
                        break;
                }

                m.Line(-0.34f, -0.46f, 0.34f, -0.46f, 0.045f, Ink.Prism(0f, 0.8f), Ink.Prism(0.5f, 0.8f));
            }

            internal override void Disc(VectorMesh m)
            {
                const int SIDES = 8;
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.5f, 0.5f, 0f, TAU, 64, false);
                m.Fill(path, default, new VectorMesh.Rise(_groundBottom, _groundTop, -0.5f, 0.5f));
                m.Glow(0f, 0f, 0.44f, 0.44f, _mid.With(0.3f));
                // Cut like a stone seen from above.
                for (int i = 0; i < SIDES; i++)
                {
                    Point a = Point.Polar(PI / SIDES + i * TAU / SIDES, 0.4f);
                    Point b = Point.Polar(PI / SIDES + i * TAU / SIDES, 0.48f);
                    m.Line(a.X, a.Y, b.X, b.Y, 0.012f, _pale.With(0.35f));
                }

                path = m.NewPath();
                path.Arc(0f, 0f, 0.4f, 0.4f, PI / SIDES, PI / SIDES + TAU, SIDES, false);
                path.Subdivide(0.1f, true);
                m.Stroke(path, true, 0.014f, new VectorMesh.Spectrum(0.5f, 0.75f, 0.4f, 0.6f));
                path = m.NewPath();
                path.Arc(0f, 0f, 0.482f, 0.482f, 0f, TAU, 64, false);
                m.Stroke(path, true, 0.042f, new VectorMesh.Spectrum(0.5f, 0.75f, 0.22f, 1f));
                for (int i = 0; i < _rank; i++) Draw(m, DiscCrystal(i), 1f);
            }

            internal override void DiscMotion(VectorMesh m, float time)
            {
                Path path = m.NewPath();
                path.Arc(0f, 0f, 0.482f, 0.482f, -PI * 0.5f, -PI * 2.5f, 64, false);
                m.Stroke(path, true, 0.03f, new VectorMesh.Spectrum(1f, 0.5f - VectorMesh.Cycle(time, DRIFT), 0.35f, 0.9f));
                m.Stroke(path, true, 0.042f, new VectorMesh.Comet(_white, _pale, VectorMesh.Cycle(time, GLINT * 2f), 0.2f, true));
                float at = VectorMesh.Cycle(time, GLINT);
                for (int i = 0; i < _rank; i++) Glint(m, DiscCrystal(i), Passed(at, 0.1f + i * 0.1f, 0.24f), 1f);
            }

            private Crystal DiscCrystal(int index)
            {
                float away = index - (_rank - 1) * 0.5f;
                float angle = PI * 0.5f - away * 0.4f;
                float size = 1f - 0.16f * Math.Abs(away) * (_rank > 2 ? 1f : 0f);
                Point place = Point.Polar(angle, 0.515f);
                return new Crystal(place.X, place.Y, 0.085f, 0.16f, 0.12f, angle - PI * 0.5f, size);
            }

            /// <summary>Wide flat panes across the face, as if it were cut.</summary>
            private void Facets(VectorMesh m, float left, float right, float strength)
            {
                float h = Space.HalfHeight;
                const float STEP = 0.62f;
                int index = 0;
                for (float x = left; x + 0.3f < right; x += STEP, index++)
                {
                    float end = Math.Min(x + STEP, right);
                    float middle = (x + end) * 0.5f;
                    m.Triangle(new Point(x, h), new Point(end, h), new Point(middle, -h), _white.With(strength * (index % 2 == 0 ? 0.9f : 0.5f)));
                    if (end + STEP * 0.5f <= right)
                        m.Triangle(new Point(middle, -h), new Point(end, h), new Point(middle + STEP, -h), _white.With(strength * 0.35f));
                }
            }

            /// <summary>One crystal: four flat panes, the light one upper left, a thin prism edge.</summary>
            private static void Draw(VectorMesh m, in Crystal c, float alpha)
            {
                m.Place(c.X, c.Y, c.Tilt, c.Zoom);
                var top = new Point(0f, c.Up);
                var right = new Point(c.Half, 0f);
                var bottom = new Point(0f, -c.Down);
                var left = new Point(-c.Half, 0f);
                var heart = new Point(-c.Half * 0.16f, c.Up * 0.04f);
                Path path = m.NewPath();
                path.Add(top);
                path.Add(right);
                path.Add(bottom);
                path.Add(left);
                m.Fill(path, default, _mid.With(alpha));
                m.Triangle(top, heart, left, _white.With(alpha));
                m.Triangle(left, heart, bottom, _pale.Fade(0.75f * alpha));
                m.Triangle(heart, right, bottom, _shade.With(alpha));
                path = m.NewPath();
                path.Add(top);
                path.Add(right);
                path.Add(bottom);
                path.Add(left);
                m.Stroke(path, true, Math.Max(0.012f, c.Half * 0.1f), _white.With(0.9f * alpha));
                m.Place(0f, 0f);
            }

            /// <summary>The same crystal as the light crosses it.</summary>
            private static void Glint(VectorMesh m, in Crystal c, float flash, float reach)
            {
                if (flash <= 0f) return;
                m.Glow(c.X, c.Y, c.Up * 1.1f, c.Up * 1.1f, _mid.With(0.5f * flash));
                m.Place(c.X, c.Y, c.Tilt, c.Zoom);
                m.Kite(0f, 0f, c.Half, c.Up, c.Down, _white.With(0.7f * flash));
                float size = c.Up * 0.8f * reach;
                m.Sparkle(0f, c.Up * 0.55f, size * flash, size * flash, _white.With(flash));
                m.Place(0f, 0f);
            }

            private readonly struct Crystal
            {
                internal readonly float X;
                internal readonly float Y;
                internal readonly float Half;
                internal readonly float Up;
                internal readonly float Down;
                internal readonly float Tilt;
                internal readonly float Zoom;

                internal Crystal(float x, float y, float half, float up, float down, float tilt, float zoom)
                {
                    X = x;
                    Y = y;
                    Half = half;
                    Up = up;
                    Down = down;
                    Tilt = tilt;
                    Zoom = zoom;
                }
            }
        }
    }
}
