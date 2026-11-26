using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

// The note skins. Both have a pale face that runs from one hue to another, the full run at
// both ends, and a side below the face. Notes carries its marks on the inset face, NotesV2 on the wide ends; the
// arrows, the nodes and the lines are the same for both. Where things sit comes from docs/operations/notes-atlas-spec.md.
namespace ArtV2
{
    public static class Notes
    {
        private const float Left = 55f, Right = 299f, Width = 354f;

        // From 45 px inside an end the sprite stretches, so whatever changes along x has to be over by then.
        private const float Reach = 44.6f;

        // How far the face of each cell is lifted toward white. Slim notes are the other way round: a full face, pale ends.
        private static readonly float[] Lift = { 0.78f, 0.72f, 0.74f, 0.82f, 0.06f, 0.17f, 0.10f };

        private enum Kind { Critical, Hold, Tap, Flick }

        /// <summary>One cell: what it is, where its face and its body sit, and the inks of its run.</summary>
        private sealed class Note
        {
            public Kind Kind;
            public bool Slim;

            /// <summary>The face runs from the top for this height; the side takes the rest of the body.</summary>
            public float Top, FaceHeight, Depth, Middle, Radius;

            public Ink Accent, From, To, Lift0, Lift1;

            /// <summary>Marks and lines on a light face: deeper than the run, or gold would not show on gold.</summary>
            public Ink Line;

            public float FaceBottom { get { return Top + FaceHeight; } }

            public float Bottom { get { return FaceBottom + Depth; } }

            public Paint Run { get { return Theme.Vertical(Top + 4f, From, FaceBottom - 4f, To); } }

            public Paint Face { get { return Theme.Vertical(Top, Lift0, FaceBottom, Lift1); } }

            /// <summary>The light rim and ends of the slim notes.</summary>
            public Paint Pale
            {
                get { return Theme.Vertical(Top, Theme.Light(Accent, 0.94f), FaceBottom, Theme.Light(Accent, 0.78f)); }
            }
        }

        /// <summary>Draws a skin with inset or banded note bodies.</summary>
        public static IEnumerable<Asset> All(string data, bool inset)
        {
            yield return new Asset("notes.png", 354, 1316, 4, false, c => Atlas(c, inset));
            yield return new Asset("longNoteLine.png", 306, 16, 1, false, LongLine);
            yield return new Asset("traceLine_eff.png", 306, 224, 1, false, TraceLine);
            yield return new Asset("simultaneousLine.png", 10, 40, 4, false, Simultaneous);
            yield return new Asset("notes_long_among.png", 158, 160, 4, false, c => Node(c, false, Make(1)));
            yield return new Asset("notes_long_among_crtcl.png", 158, 160, 4, false, c => Node(c, false, Make(0)));
            yield return new Asset("notes_friction_among_long.png", 104, 112, 4, false, c => Node(c, true, Make(1)));
            yield return new Asset("notes_friction_among_flick.png", 104, 112, 4, false, c => Node(c, true, Make(3)));
            yield return new Asset("notes_friction_among_crtcl.png", 104, 112, 4, false, c => Node(c, true, Make(0)));
            const int frictionRow = 4;
            Note damage = Make(frictionRow, damage: true);
            yield return new Asset("notes_damage.png", 354, 1316, 4, false, c => DrawCell(c, frictionRow, damage, inset));
            yield return new Asset("notes_damage_friction.png", 104, 112, 4, false, c => Node(c, true, damage));
            foreach (KeyValuePair<string, Arrow> pair in Arrow.Load(Path.Combine(data, "flick-arrows.csv")))
                foreach (bool critical in new[] { false, true })
                {
                    Arrow arrow = pair.Value;
                    Note kind = Make(critical ? 0 : 3);
                    yield return new Asset(
                        "notes_flick_arrow_" + (critical ? "crtcl_" : "") + pair.Key + ".png",
                        arrow.Width, arrow.Height, 3, false, c => FlickArrow(c, arrow, kind));
                }
        }

        // ------------------------------------------------------------------------------------ notes.png
        // Seven cells of 354 x 188, top to bottom: critical, hold, tap, flick (wide); trace, critical trace, flick
        // trace (slim). The body runs from x 55 to 299, the parts that must not stretch stay inside the 100 px
        // borders, and everything between 100 and 254 is the same at every x. A wide body covers rows 50..133 and a
        // slim one 72..116, which is where the judge line is. The side below the face is part of the body.
        private static void Atlas(Canvas canvas, bool inset)
        {
            for (int row = 0; row < 7; row++)
            {
                DrawCell(canvas, row, Make(row), inset);
            }
        }

        private static void DrawCell(Canvas canvas, int row, Note note, bool inset)
        {
            // Rows 0 and 187 stay clear so adjacent cells do not bleed into each other.
            Canvas cell = canvas.View(0f, row * 188f, 354f, 188f).View(0f, 1f, 354f, 186f).Shift(0f, -1f);
            if (inset) Inset(cell, note);
            else Banded(cell, note);
        }

        private static Note Make(int row, bool damage = false)
        {
            Kind[] kinds = { Kind.Critical, Kind.Hold, Kind.Tap, Kind.Flick, Kind.Hold, Kind.Critical, Kind.Flick };
            var n = new Note { Kind = kinds[row], Slim = row >= 4 };
            n.Top = n.Slim ? 72f : 50f;
            n.Depth = n.Slim ? 4.5f : 6f;
            n.FaceHeight = (n.Slim ? 44f : 83f) - n.Depth;
            n.Middle = n.Top + n.FaceHeight * 0.5f;
            n.Radius = n.Slim ? 13f : 19f;
            switch (n.Kind)
            {
                case Kind.Critical: // gold to amber
                    n.Accent = Theme.Critical;
                    n.From = Ink.Mix(Theme.Vivid(n.Accent, 0.76f, 1f), Theme.BannerGold, 0.55f);
                    n.To = Ink.Mix(Theme.Vivid(n.Accent, 0.74f, 1f), Theme.Danger, 0.35f);
                    break;
                case Kind.Hold: // teal to aqua
                    n.Accent = damage ? Theme.Damage : Theme.Hold;
                    n.From = Theme.Vivid(n.Accent, 0.72f, 0.87f);
                    Ink end = damage ? Theme.Violet : Theme.Prism[2];
                    n.To = Ink.Mix(n.From, Theme.Vivid(end, 0.66f, 0.97f), 0.6f);
                    break;
                case Kind.Tap: // violet to periwinkle
                    n.Accent = Theme.Tap;
                    n.From = Theme.Vivid(Theme.Violet, 0.5f, 1f);
                    n.To = Theme.Vivid(n.Accent, 0.64f, 1f);
                    break;
                default: // coral to rose
                    n.Accent = Theme.Flick;
                    n.To = Theme.Vivid(n.Accent, 0.60f, 1f);
                    n.From = Ink.Mix(n.To, Theme.Vivid(Theme.Danger, 0.5f, 1f), 0.5f);
                    break;
            }

            n.Lift0 = Theme.Light(n.From, Lift[row]);
            n.Lift1 = Theme.Light(n.To, Lift[row]);
            n.Line = Theme.Vivid(Ink.Mix(n.From, n.To, 0.5f), n.Kind == Kind.Tap ? 0.6f : 0.74f, n.Kind == Kind.Hold ? 0.78f : 0.96f);
            return n;
        }

        // ------------------------------------------------------------------------------------ the parts of a body
        /// <summary>A rounded rectangle inside the face.</summary>
        private static Sdf Inside(Note n, float side, float edge, float radius)
        {
            return Shape.Rect(Left + side, n.Top + edge, Right - Left - side * 2f, n.FaceHeight - edge * 2f, radius);
        }

        /// <summary>The light around the note and its side, down to the last row of the body. Criticals shine wider.</summary>
        private static void Base(Canvas c, Note n)
        {
            bool critical = n.Kind == Kind.Critical;
            Sdf body = Shape.Rect(Left, n.Top, Right - Left, n.FaceHeight + n.Depth, n.Radius);
            c.Glow(body, (n.Slim ? 7f : 8.5f) + (critical ? 2.5f : 0f), critical ? 0.52f : 0.42f, 1f, Light(n, n.Run));
            c.Fill(body, Theme.Vertical(n.FaceBottom - 10f, n.To, n.Bottom, Shade(n)));
        }

        /// <summary>A level line between the two ends; the same at every x.</summary>
        private static void Midline(Canvas c, Note n, float reach, float radius, Paint paint)
        {
            c.Fill(Shape.Segment(Left + reach, n.Middle, Width - Left - reach, n.Middle, radius), paint);
        }

        private static void Marks(Canvas c, Note n, float inset, float size, Ink ink)
        {
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? Left + inset : Width - Left - inset;
                float w = (n.Slim ? 7.5f : 10f) * size, h = (n.Slim ? 10f : 15.5f) * size;
                switch (n.Kind)
                {
                    case Kind.Critical:
                        c.Fill(Shape.Star4(x, n.Middle, (n.Slim ? 11f : 15.5f) * size, (n.Slim ? 12.5f : 19f) * size, 1.12f, 0f), ink);
                        break;
                    case Kind.Hold:
                        if (n.Slim) c.Fill(Shape.Diamond(x, n.Middle, w, h, 1f), ink);
                        else c.Fill(Shape.Ring(Shape.Diamond(x, n.Middle, w - 1f, h - 2f, 1f), 4f * size), ink);
                        break;
                    case Kind.Tap:
                        c.Fill(Shape.Diamond(x, n.Middle, w, h, 1.2f), ink);
                        break;
                    default:
                        float cw = (n.Slim ? 7f : 9.5f) * size, ch = (n.Slim ? 6.5f : 9f) * size, weight = (n.Slim ? 2.1f : 2.8f) * size;
                        foreach (float shift in n.Slim ? new[] { 0f } : new[] { -6.5f * size, 6.5f * size })
                        {
                            float y = n.Middle + shift;
                            c.Fill(
                                Shape.Polyline(weight, x - cw, y + ch * 0.5f, x, y - ch * 0.5f, x + cw, y + ch * 0.5f),
                                ink.Fade(shift > 0f ? 0.7f : 1f));
                        }

                        break;
                }
            }
        }

        // ------------------------------------------------------------------------------------ banded notes
        // Wide ends that carry the marks, a white rim around the face.
        private static void Banded(Canvas c, Note n)
        {
            Base(c, n);
            float band = n.Slim ? 31f : 37f;
            Sdf face = Inside(n, 0f, 0f, n.Radius);
            c.Fill(face, n.Face);
            Paint ends = n.Slim ? n.Pale : n.Run;
            c.Fill(face, delegate(float x, float y)
            {
                float edge = Math.Min(x, Width - x) - Left;
                return Ink.Mix(ends(x, y).Fade(c.Cover(edge - band)), Theme.White, c.Line(edge - band - 0.8f, 2f));
            });
            c.Stroke(face, n.Slim ? 2.4f : 3.2f, Theme.Light(n.Accent, 0.92f), -1f);
            Midline(c, n, n.Slim ? 40f : Reach, n.Slim ? 1.5f : 2f, n.Slim ? Theme.White.Fade(0.9f) : n.Line.Fade(0.6f));
            Marks(c, n, n.Slim ? 17f : 20f, n.Slim ? 0.96f : 1f, n.Slim ? n.Line : Theme.White);
        }

        // ------------------------------------------------------------------------------------ inset notes
        // A frame around an inset plate: thin ends with a bar, the marks on the plate.
        private static void Inset(Canvas c, Note n)
        {
            Base(c, n);
            float end = n.Slim ? 14f : 17f;
            Sdf face = Inside(n, 0f, 0f, n.Radius), plate = Inside(n, end, n.Slim ? 4.5f : 6f, n.Slim ? 5f : 6f);
            c.Fill(face, n.Slim ? n.Pale : n.Run);
            c.Fill(plate, n.Face);
            c.Stroke(plate, n.Slim ? 1.4f : 2f, Theme.White.Fade(n.Slim ? 0.5f : 0.95f), -1f);
            c.Stroke(face, n.Slim ? 1.6f : 2f, Theme.Light(n.Accent, 0.92f).Fade(0.9f), -1f);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? Left + end * 0.52f : Width - Left - end * 0.52f, half = n.Slim ? 7f : 18f;
                c.Fill(Shape.Segment(x, n.Middle - half, x, n.Middle + half, n.Slim ? 1.6f : 2f), n.Slim ? n.Line : Theme.White);
            }

            Midline(c, n, n.Slim ? 40f : Reach, n.Slim ? 1.5f : 1.8f, n.Slim ? Theme.White.Fade(0.85f) : n.Line.Fade(0.6f));
            Marks(c, n, n.Slim ? 25f : 30f, n.Slim ? 0.82f : 0.76f, n.Slim ? Theme.White : n.Line);
        }

        private static Ink Shade(Note n)
        {
            return Theme.Vivid(n.To, n.Kind == Kind.Tap ? 0.68f : 0.74f, 0.8f);
        }

        /// <summary>What a note glows in: its own run, or gold for a critical.</summary>
        private static Paint Light(Note n, Paint run)
        {
            return n.Kind == Kind.Critical ? n.From : run;
        }

        /// <summary>A shape dragged straight down: the face and the side below it.</summary>
        private static Sdf Dragged(Sdf shape, float depth)
        {
            return delegate(float x, float y)
            {
                float best = float.MaxValue;
                for (int i = 0; i <= 6; i++) best = Math.Min(best, shape(x, y - depth * i / 6f));
                return best;
            };
        }

        // ------------------------------------------------------------------------------------ the hold's ribbon
        // noteline.shader samples both lines the same way: x 0..38.25 and 267.75..306 are a fixed strip outside each
        // edge, x 38.25..267.75 is the ribbon stretched to its width. The interface's tinted chips, at that shape:
        // a see-through fill with a solid line along each edge and a little light outside it.
        private static Ink Ribbon(float x, Ink accent, float light, float body, float rail, float glow)
        {
            const float width = 306f, edge = width * 0.125f;
            float d = Math.Min(x, width - x);
            Ink line = Theme.Light(accent, 0.2f + 0.5f * light);
            Ink fill = Theme.Light(accent, 0.08f + 0.34f * light);
            if (d < edge - 4.5f)
            {
                float k = M.Unit((d - 12f) / (edge - 4.5f - 12f));
                return Theme.Light(accent, 0.1f + 0.3f * light).Fade(glow * k * k * k);
            }

            if (d < edge) return line.Fade(rail);
            float e = M.Smooth(0f, 1f, (d - edge) / 46f);
            return Ink.Mix(Theme.Light(accent, 0.12f + 0.4f * light), fill, e).Fade(M.Lerp(body + (rail - body) * 0.22f, body, e));
        }

        // Four bands, top to bottom: normal held, normal, critical held, critical. The bottom row of a band is the head.
        private static void LongLine(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                int band = (int)(y / 4f);
                float t = (y - band * 4f - 0.5f) / 3f;
                bool held = band % 2 == 0;
                Ink accent = band < 2 ? Theme.Hold : Theme.Critical;
                return Ribbon(x, accent, (held ? 0.85f : 0.06f) + 0.06f * t, (held ? 0.66f : 0.5f) + 0.05f * t, 0.98f, 0.3f);
            });
        }

        // Four frames of 56 rows in the same order. A guide starts at the bottom of its frame and fades out upwards.
        private static void TraceLine(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                int frame = (int)(y / 56f);
                float t = (y - frame * 56f - 0.5f) / 55f;
                bool held = frame % 2 == 0;
                bool critical = frame >= 2;
                Ink accent = critical ? Theme.Critical : Theme.Hold;
                float floor = critical ? 47f : 36f;
                float alpha = (floor + (252f - floor) * M.Pow(t, 1.48f)) / 255f;
                return Ribbon(x, accent, (held ? 0.7f : 0.08f) + 0.12f * t, alpha * 0.56f, alpha, alpha * 0.2f);
            });
        }

        // Links notes hit together: a fine white line with a little violet light, the splash's judgement line in small.
        private static void Simultaneous(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                float d = Math.Abs(y - 20f);
                float glow = M.Gauss(d, 4.6f) * 0.42f;
                float line = c.Line(y - 20f, 4.2f);
                return Ink.Mix(Theme.Light(Theme.Violet, 0.45f).Fade(glow), Theme.TextPrimary.Fade(0.96f), line);
            });
        }

        // ------------------------------------------------------------------------------------ nodes
        // 158 x 160 centred on row 80, and 104 x 112 centred on row 56.
        private static void Node(Canvas c, bool small, Note n)
        {
            float x = small ? 52f : 79f, depth = small ? 4f : 6f, y = (small ? 56f : 80f) - depth * 0.5f;
            float radius = small ? 29f : 48f, rim = small ? 4.5f : 7f;
            Sdf face = Shape.Diamond(x, y, radius, radius, small ? 4f : 6f), body = Dragged(face, depth);

            // A dark seat, so the node reads on a bright ribbon, then its light.
            c.Glow(body, small ? 6f : 9f, 0.42f, 1f, Ink.Mix(n.Accent, Theme.Ground, 0.82f));
            Paint run = Theme.Vertical(y - radius * 0.8f, n.From, y + radius * 0.8f, n.To);
            c.Glow(body, small ? 5f : 8f, 0.42f, 1f, Light(n, run));
            c.Fill(body, Theme.Vertical(y, n.To, y + radius + depth, Shade(n)));
            c.Fill(face, run);
            c.Stroke(face, rim, Theme.Light(n.Accent, 0.92f), -1f);
            c.Fill(Shape.Star4(x, y, radius * 0.56f, 1.08f), Theme.White);
        }

        // ------------------------------------------------------------------------------------ flick arrows
        private static void FlickArrow(Canvas c, Arrow arrow, Note n)
        {
            const float depth = 6f;
            Ink top = Theme.Light(n.From, 0.22f), bottom = Theme.Light(n.To, 0.22f);
            Sdf face = Shape.Polygon(arrow.Face, 8f), body = Dragged(face, depth);
            float start = arrow.Height * 0.3f, lean = arrow.Diagonal ? 0.58f : 0f;

            // The tail: the arrow dragged down (and sideways, when it leans), fading out.
            PointF[] sweep = arrow.Sweep(arrow.Height * lean, arrow.Height);
            c.Pixels(delegate(float x, float y)
            {
                float d = Shape.PolygonDistance(sweep, x, y);
                if (y <= start || d > 1f) return default(Ink);
                float t = (y - start) / (arrow.Height - start);
                return Theme.Light(bottom, 0.3f * (1f - t)).Fade(M.Smooth(0f, 18f, -d) * M.Pow(1f - t, 1.5f) * 0.62f);
            });

            Paint run = Theme.Vertical(31f, top, arrow.Bottom - 4f, bottom);
            c.Glow(body, 8.5f, 0.42f, 1f, Light(n, run));
            c.Fill(body, Theme.Vertical(27f, bottom, arrow.Bottom + depth, Theme.Light(Shade(n), 0.14f)));
            c.Fill(face, run);
            c.Stroke(face, 6f, Theme.Light(n.Accent, 0.92f), -1f);
        }
    }

    /// <summary>One flick arrow's outline: the face inside its rim, as measured for the first skin.</summary>
    public sealed class Arrow
    {
        public int Width, Height;
        public bool Diagonal;

        /// <summary>Clockwise from the tip: tip, right shoulder, right foot, notch, left foot, left shoulder.</summary>
        public PointF[] Face;

        public float Bottom
        {
            get
            {
                float bottom = 0f;
                foreach (PointF p in Face) bottom = Math.Max(bottom, p.Y);
                return bottom + 8f;
            }
        }

        /// <summary>The area the face covers when it is dragged by an offset, as one outline.</summary>
        public PointF[] Sweep(float dx, float dy)
        {
            // The upper edge stays, the lower edge moves; the sides join them.
            return new[]
            {
                Face[0], Face[1], new PointF(Face[1].X + dx, Face[1].Y + dy), new PointF(Face[5].X + dx, Face[5].Y + dy), Face[5]
            };
        }

        public static Dictionary<string, Arrow> Load(string file)
        {
            var result = new Dictionary<string, Arrow>();
            foreach (string line in File.ReadAllLines(file))
            {
                string[] cells = line.Split(',');
                if (cells.Length < 15 || cells[0] == "arrow") continue;
                var numbers = new float[14];
                for (int i = 0; i < 14; i++) numbers[i] = float.Parse(cells[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                var arrow = new Arrow { Width = (int)numbers[0], Height = (int)numbers[1], Face = new PointF[6] };
                for (int i = 0; i < 6; i++) arrow.Face[i] = new PointF(numbers[2 + i * 2], numbers[3 + i * 2]);
                arrow.Diagonal = cells[0].EndsWith("_diagonal");
                result[cells[0]] = arrow;
            }

            return result;
        }
    }
}
