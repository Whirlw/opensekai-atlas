using System;
using Ink = OpenSekai.App.UI.VectorMesh.Ink;
using Point = OpenSekai.App.UI.VectorMesh.Point;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The emblem of each achievement series: one flat drawing in one color, which its medals wear in
    /// their band and its discs on their face. The levels of a series share it, except the Ranking
    /// Score's, whose star grows with the tier as the crests' do. The special medals have emblems of
    /// their own (<see cref="SpecialPlateArt"/>).
    ///
    /// Pure math with no Unity types. Units: the emblem fills a square of -0.5..0.5, y up.
    /// </summary>
    internal static class MedalEmblemArt
    {
        private const float PI = (float)Math.PI;

        /// <summary>The ids it can draw: a series, or a level of one whose emblem grows.</summary>
        internal static bool Has(string id) => Draw(null, id, default);

        /// <summary>Draws the emblem of <paramref name="id"/>; false for an id it does not know. A null mesh only asks.</summary>
        internal static bool Draw(VectorMesh mesh, string id, Ink ink)
        {
            string series = SeriesOf(id, out int level);
            Action<VectorMesh, Ink> draw = null;
            switch (series)
            {
                case "HIT": draw = Hits; break;
                case "SET": draw = Encore; break;
                case "MAP": draw = Map; break;
                case "DAY": draw = Diary; break;
                case "RETRY": draw = Retry; break;
                case "COMBO": draw = Hand; break;
                case "FC": draw = FullCombo; break;
                case "AP": draw = AllPerfect; break;
                case "EAR": draw = TuningFork; break;
                case "GREAT": draw = HairBreadth; break;
                case "LONG": draw = Road; break;
                case "DENSE": draw = Dense; break;
                case "FLICK": draw = Arrow; break;
                case "HOLD": draw = Silk; break;
                case "FCLVL": draw = Line; break;
                case "APLVL": draw = Crystal; break;
                case "SETLIST": draw = List; break;
                case "CHAIN": draw = Links; break;
                case "REVIEW": draw = Book; break;
                case "STAIR": draw = Stair; break;
                case "FIX": draw = Mended; break;
                case "DAN":
                case "DANPASS": draw = Plates; break;
                case "GOLRIOLE": draw = Aureole; break;
                case "B30":
                    if (mesh != null) Star(mesh, ink, Math.Max(1, level));
                    return true;
            }

            if (draw != null && mesh != null) draw(mesh, ink);
            return draw != null;
        }

        /// <summary>"FCLVL-11" is level 11 of FCLVL.</summary>
        internal static string SeriesOf(string id, out int level)
        {
            level = 0;
            int dash = id?.IndexOf('-') ?? -1;
            if (dash < 0) return id;
            int.TryParse(id.Substring(dash + 1), out level);
            return id.Substring(0, dash);
        }

        // ---------------------------------------------------------------- practice

        /// <summary>万键之路: notes coming down a lane, the nearest one widest.</summary>
        private static void Hits(VectorMesh m, Ink ink)
        {
            m.Line(-0.14f, 0.46f, -0.48f, -0.46f, 0.035f, ink.Fade(0.2f), ink.Fade(0.45f));
            m.Line(0.14f, 0.46f, 0.48f, -0.46f, 0.035f, ink.Fade(0.2f), ink.Fade(0.45f));
            m.Box(0f, -0.28f, 0.36f, 0.095f, 0.095f, ink);
            m.Box(0f, 0.03f, 0.25f, 0.07f, 0.07f, ink.Fade(0.78f));
            m.Box(0f, 0.28f, 0.16f, 0.05f, 0.05f, ink.Fade(0.55f));
        }

        /// <summary>再来一曲: a note in the repeat arrows.</summary>
        private static void Encore(VectorMesh m, Ink ink)
        {
            for (int i = 0; i < 2; i++)
            {
                float turn = i * PI;
                m.Place(0f, 0f, turn);
                m.ArcLine(0f, 0f, 0.37f, 0.62f, PI - 0.1f, 0.075f, ink.Fade(i == 0 ? 1f : 0.7f));
                Point tip = Point.Polar(0.62f, 0.37f);
                Head(m, tip, 0.62f - PI * 0.5f, 0.15f, ink.Fade(i == 0 ? 1f : 0.7f));
            }

            m.Place(0f, 0f);
            m.Box(0f, 0f, 0.18f, 0.065f, 0.065f, ink);
        }

        /// <summary>谱面地图: a folded map with a route to its pin.</summary>
        private static void Map(VectorMesh m, Ink ink)
        {
            VectorMesh.Path path = m.NewPath();
            float[] x = { -0.43f, -0.14f, 0.14f, 0.43f, 0.43f, 0.14f, -0.14f, -0.43f };
            float[] y = { 0.3f, 0.39f, 0.3f, 0.39f, -0.3f, -0.39f, -0.3f, -0.39f };
            for (int i = 0; i < x.Length; i++) path.Add(x[i], y[i]);
            m.Stroke(path, true, 0.06f, ink);
            m.Line(-0.14f, 0.39f, -0.14f, -0.3f, 0.035f, ink.Fade(0.4f));
            m.Line(0.14f, 0.3f, 0.14f, -0.39f, 0.035f, ink.Fade(0.4f));
            m.Dot(-0.29f, -0.14f, 0.045f, ink.Fade(0.75f));
            m.Dot(-0.12f, -0.02f, 0.035f, ink.Fade(0.75f));
            m.Dot(0.04f, 0f, 0.035f, ink.Fade(0.75f));
            m.Kite(0.26f, 0.07f, 0.11f, 0.11f, 0.16f, ink);
        }

        /// <summary>练习日记: a diary with the moon of the day.</summary>
        private static void Diary(VectorMesh m, Ink ink)
        {
            m.BoxLine(0.03f, 0f, 0.33f, 0.42f, 0.08f, 0.06f, ink);
            m.Line(-0.16f, 0.4f, -0.16f, -0.4f, 0.035f, ink.Fade(0.45f));
            for (int i = 0; i < 3; i++) m.Line(-0.4f, 0.24f - i * 0.24f, -0.24f, 0.24f - i * 0.24f, 0.05f, ink);
            Crescent(m, 0.12f, 0.14f, 0.15f, ink);
            m.Line(-0.03f, -0.13f, 0.26f, -0.13f, 0.04f, ink.Fade(0.55f));
            m.Line(-0.03f, -0.26f, 0.16f, -0.26f, 0.04f, ink.Fade(0.55f));
        }

        /// <summary>再试一次: once more round.</summary>
        private static void Retry(VectorMesh m, Ink ink)
        {
            m.ArcLine(0f, 0f, 0.36f, PI * 0.5f + 0.5f, PI * 2.5f - 0.2f, 0.08f, ink);
            Head(m, Point.Polar(PI * 0.5f + 0.42f, 0.36f), PI + 0.42f + PI, 0.17f, ink);
            m.Diamond(0f, 0f, 0.14f, ink, default);
        }

        // ---------------------------------------------------------------- technique

        /// <summary>腱鞘炎: a tapping hand that has had enough.</summary>
        private static void Hand(VectorMesh m, Ink ink)
        {
            float[] x = { -0.2f, -0.065f, 0.07f, 0.205f };
            float[] top = { 0.2f, 0.32f, 0.29f, 0.16f };
            for (int i = 0; i < x.Length; i++)
            {
                float bottom = -0.2f;
                m.Box(x[i], (top[i] + bottom) * 0.5f, 0.053f, (top[i] - bottom) * 0.5f, 0.053f, ink);
            }

            m.Box(0.002f, -0.2f, 0.256f, 0.15f, 0.1f, ink);
            m.Place(0.3f, -0.1f, -0.62f);
            m.Box(0f, 0f, 0.058f, 0.18f, 0.058f, ink);
            m.Place(0f, 0f);
            // The wrist's bolt.
            VectorMesh.Path bolt = m.NewPath();
            bolt.Add(-0.4f, -0.1f);
            bolt.Add(-0.31f, -0.24f);
            bolt.Add(-0.42f, -0.26f);
            bolt.Add(-0.32f, -0.44f);
            m.Stroke(bolt, false, 0.05f, ink.Fade(0.75f));
            m.Line(-0.03f, 0.4f, -0.03f, 0.48f, 0.04f, ink.Fade(0.55f));
            m.Line(0.16f, 0.37f, 0.21f, 0.45f, 0.04f, ink.Fade(0.55f));
            m.Line(-0.22f, 0.3f, -0.28f, 0.38f, 0.04f, ink.Fade(0.55f));
        }

        /// <summary>全连达人: a ring that closes without a gap.</summary>
        private static void FullCombo(VectorMesh m, Ink ink)
        {
            m.Ring(0f, 0f, 0.35f, 0.075f, ink);
            for (int i = 0; i < 4; i++)
            {
                Point node = Point.Polar(i * PI * 0.5f, 0.35f);
                m.Diamond(node.X, node.Y, 0.13f, ink, default);
            }

            m.Diamond(0f, 0f, 0.14f, ink.Fade(0.75f), default);
        }

        /// <summary>完美无缺: a star with nothing missing.</summary>
        private static void AllPerfect(VectorMesh m, Ink ink)
        {
            m.Place(0f, 0f, PI * 0.25f);
            m.Sparkle(0f, 0f, 0.27f, 0.27f, ink.Fade(0.55f));
            m.Place(0f, 0f);
            m.Sparkle(0f, 0f, 0.48f, 0.48f, ink);
            for (int i = 0; i < 4; i++)
            {
                Point gem = Point.Polar(PI * 0.25f + i * PI * 0.5f, 0.38f);
                m.Diamond(gem.X, gem.Y, 0.06f, ink.Fade(0.75f), default);
            }
        }

        /// <summary>校音师: a tuning fork ringing.</summary>
        private static void TuningFork(VectorMesh m, Ink ink)
        {
            VectorMesh.Path path = m.NewPath();
            path.Add(-0.14f, 0.44f);
            path.Arc(0f, 0.04f, 0.14f, 0.14f, PI, PI * 2f, 10);
            path.Add(0.14f, 0.44f);
            m.Stroke(path, false, 0.085f, ink);
            m.Line(0f, -0.1f, 0f, -0.4f, 0.08f, ink);
            m.Dot(0f, -0.4f, 0.075f, ink);
            for (int i = 0; i < 2; i++)
            {
                float radius = 0.29f + i * 0.13f;
                Ink faint = ink.Fade(0.7f - i * 0.3f);
                m.ArcLine(0f, 0.26f, radius, -0.6f, 0.6f, 0.045f, faint);
                m.ArcLine(0f, 0.26f, radius, PI - 0.6f, PI + 0.6f, 0.045f, faint);
            }
        }

        /// <summary>毫厘之间: two points a hair apart.</summary>
        private static void HairBreadth(VectorMesh m, Ink ink)
        {
            m.Triangle(new Point(-0.45f, 0.25f), new Point(-0.07f, 0f), new Point(-0.45f, -0.25f), ink);
            m.Triangle(new Point(0.45f, 0.25f), new Point(0.45f, -0.25f), new Point(0.07f, 0f), ink.Fade(0.72f));
            m.Line(0f, 0.46f, 0f, 0.1f, 0.045f, ink.Fade(0.6f));
            m.Line(0f, -0.1f, 0f, -0.46f, 0.045f, ink.Fade(0.6f));
        }

        /// <summary>长途旅行: a road into the low sun.</summary>
        private static void Road(VectorMesh m, Ink ink)
        {
            VectorMesh.Path sun = m.NewPath();
            sun.Arc(0f, 0.08f, 0.2f, 0.2f, PI, 0f, 14);
            m.Fill(sun, new Point(0f, 0.12f), ink);
            m.Line(-0.46f, 0.06f, 0.46f, 0.06f, 0.05f, ink);
            for (int i = 0; i < 3; i++)
            {
                float angle = PI * (0.25f + i * 0.25f);
                Point from = Point.Polar(angle, 0.27f);
                Point to = Point.Polar(angle, 0.36f);
                m.Line(from.X, from.Y + 0.08f, to.X, to.Y + 0.08f, 0.04f, ink.Fade(0.6f));
            }

            m.Line(-0.07f, 0.02f, -0.4f, -0.44f, 0.055f, ink.Fade(0.75f));
            m.Line(0.07f, 0.02f, 0.4f, -0.44f, 0.055f, ink.Fade(0.75f));
            m.Line(0f, -0.06f, 0f, -0.14f, 0.035f, ink.Fade(0.6f));
            m.Line(0f, -0.23f, 0f, -0.4f, 0.05f, ink.Fade(0.75f));
        }

        /// <summary>耐力惊人: a wall of notes.</summary>
        private static void Dense(VectorMesh m, Ink ink)
        {
            for (int column = 0; column < 3; column++)
            {
                int rows = column == 1 ? 4 : 5;
                for (int row = 0; row < rows; row++)
                {
                    float y = -0.4f + row * 0.2f + (column == 1 ? 0.1f : 0f);
                    m.Box((column - 1) * 0.3f, y, 0.12f, 0.055f, 0.055f, ink.Fade(1f - 0.75f * (y + 0.4f)));
                }
            }
        }

        /// <summary>箭羽: the flick's arrow with its feathers.</summary>
        private static void Arrow(VectorMesh m, Ink ink)
        {
            m.Line(0f, 0.36f, 0f, -0.22f, 0.07f, ink);
            Chevron(m, 0.45f, 0.24f, 0.23f, 0.09f, ink);
            for (int i = 0; i < 3; i++) Chevron(m, -0.02f - i * 0.15f, 0.2f, 0.15f, 0.065f, ink.Fade(0.85f - i * 0.2f));
        }

        /// <summary>牵丝: a hold drawn out like silk.</summary>
        private static void Silk(VectorMesh m, Ink ink)
        {
            var left = new VectorMesh.Path();
            var right = new VectorMesh.Path();
            for (int i = 0; i <= 18; i++)
            {
                float t = i / 18f;
                float y = VectorMesh.Mix(0.38f, -0.32f, t);
                float x = 0.2f * (float)Math.Cos(t * PI);
                float half = VectorMesh.Mix(0.11f, 0.15f, t);
                left.Add(x - half, y);
                right.Add(x + half, y);
            }

            m.Strip(left, right, ink.Fade(0.5f), ink.Fade(0.28f));
            m.Box(0.2f, 0.39f, 0.2f, 0.06f, 0.06f, ink.Fade(0.85f));
            m.Box(-0.2f, -0.35f, 0.25f, 0.08f, 0.08f, ink);
            m.Diamond(0.1f, 0.14f, 0.075f, ink, default);
            m.Diamond(-0.08f, -0.08f, 0.075f, ink, default);
        }

        /// <summary>皆传: one line, unbroken, that rises to a peak.</summary>
        private static void Line(VectorMesh m, Ink ink)
        {
            VectorMesh.Path path = m.NewPath();
            path.Add(-0.46f, -0.26f);
            path.Curve(new Point(-0.16f, -0.26f), new Point(-0.06f, -0.06f), new Point(0f, 0.34f), 12);
            path.Curve(new Point(0.06f, -0.06f), new Point(0.16f, -0.26f), new Point(0.46f, -0.26f), 12);
            m.Stroke(path, false, 0.075f, ink);
            m.Diamond(0f, 0.37f, 0.11f, ink, default);
            m.Dot(-0.44f, -0.26f, 0.06f, ink);
            m.Dot(0.44f, -0.26f, 0.06f, ink);
            m.Line(-0.3f, -0.42f, 0.3f, -0.42f, 0.045f, ink.Fade(0.5f));
        }

        /// <summary>真·皆传: a flawless crystal.</summary>
        private static void Crystal(VectorMesh m, Ink ink)
        {
            m.Kite(0f, 0.04f, 0.24f, 0.44f, 0.44f, ink.Fade(0.5f));
            m.Triangle(new Point(0f, 0.48f), new Point(0f, -0.4f), new Point(-0.24f, 0.04f), ink);
            m.Diamond(-0.39f, 0.04f, 0.07f, ink.Fade(0.75f), default);
            m.Diamond(0.39f, 0.04f, 0.07f, ink.Fade(0.75f), default);
            m.Line(-0.2f, -0.45f, 0.2f, -0.45f, 0.045f, ink.Fade(0.5f));
        }

        // ---------------------------------------------------------------- consistency

        /// <summary>今日歌单: three songs written down.</summary>
        private static void List(VectorMesh m, Ink ink)
        {
            float[] end = { 0.44f, 0.26f, 0.38f };
            for (int i = 0; i < end.Length; i++)
            {
                float y = 0.29f - i * 0.29f;
                m.Diamond(-0.33f, y, 0.11f, ink, default);
                m.Line(-0.12f, y, end[i], y, 0.085f, ink.Fade(i == 0 ? 1f : 0.65f));
            }
        }

        /// <summary>全连接力: links handed on from one to the next.</summary>
        private static void Links(VectorMesh m, Ink ink)
        {
            m.Ring(0f, 0f, 0.17f, 0.07f, ink.Fade(0.6f));
            m.Ring(-0.27f, 0f, 0.17f, 0.07f, ink);
            m.Ring(0.27f, 0f, 0.17f, 0.07f, ink);
            m.Diamond(0f, 0.33f, 0.065f, ink.Fade(0.75f), default);
            m.Diamond(0f, -0.33f, 0.065f, ink.Fade(0.75f), default);
        }

        /// <summary>复习: an open book.</summary>
        private static void Book(VectorMesh m, Ink ink)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                VectorMesh.Path page = m.NewPath();
                page.Add(0f, 0.24f);
                page.Curve(new Point(side * 0.14f, 0.36f), new Point(side * 0.3f, 0.36f), new Point(side * 0.44f, 0.3f), 8);
                page.Add(side * 0.44f, -0.26f);
                page.Curve(new Point(side * 0.3f, -0.2f), new Point(side * 0.14f, -0.2f), new Point(0f, -0.34f), 8);
                m.Stroke(page, false, 0.06f, ink);
                for (int line = 0; line < 2; line++)
                {
                    float y = 0.13f - line * 0.16f;
                    m.Line(side * 0.14f, y, side * 0.34f, y + 0.01f, 0.04f, ink.Fade(0.55f));
                }
            }

            m.Line(0f, 0.24f, 0f, -0.34f, 0.05f, ink.Fade(0.75f));
        }

        /// <summary>半音阶: keys going up a half step at a time.</summary>
        private static void Stair(VectorMesh m, Ink ink)
        {
            for (int i = 0; i < 5; i++)
            {
                float half = 0.11f + i * 0.08f;
                m.Box(-0.38f + i * 0.19f, -0.42f + half, 0.07f, half, 0.03f, ink.Fade(i % 2 == 0 ? 1f : 0.5f));
            }
        }

        /// <summary>把断点接上: a break in the line, joined.</summary>
        private static void Mended(VectorMesh m, Ink ink)
        {
            m.Line(-0.48f, 0f, -0.2f, 0f, 0.085f, ink);
            m.Line(0.2f, 0f, 0.48f, 0f, 0.085f, ink);
            m.Diamond(0f, 0f, 0.18f, ink, default);
            m.ArcLine(0f, 0f, 0.33f, 0.5f, PI - 0.5f, 0.05f, ink.Fade(0.6f));
            m.ArcLine(0f, 0f, 0.33f, PI + 0.5f, PI * 2f - 0.5f, 0.05f, ink.Fade(0.6f));
        }

        /// <summary>The Ranking Score's star, grown a step per tier as on the crests.</summary>
        private static void Star(VectorMesh m, Ink ink, int tier)
        {
            m.HexagonLine(0f, 0f, 0.48f, 0.05f, ink.Fade(0.5f));
            if (tier >= 5)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * PI / 6f + PI / 12f;
                    Point from = Point.Polar(angle, 0.28f);
                    Point to = Point.Polar(angle, 0.36f);
                    m.Line(from.X, from.Y, to.X, to.Y, 0.028f, ink.Fade(0.6f));
                }
            }

            if (tier >= 3) m.Ring(0f, 0f, 0.22f, 0.035f, ink.Fade(0.75f));
            if (tier >= 2)
            {
                m.Place(0f, 0f, PI * 0.25f);
                m.Sparkle(0f, 0f, 0.21f, 0.21f, ink.Fade(0.6f));
                m.Place(0f, 0f);
            }

            m.Sparkle(0f, 0f, 0.36f, 0.36f, ink);
            if (tier >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    Point gem = Point.Polar(PI * 0.25f + i * PI * 0.5f, 0.22f);
                    m.Diamond(gem.X, gem.Y, 0.05f, ink, default);
                }
            }

            if (tier < 6) return;
            VectorMesh.Path orbit = m.NewPath();
            orbit.Arc(0f, 0f, 0.5f, 0.13f, 0f, VectorMesh.TAU, 40, false, -0.42f);
            m.Stroke(orbit, true, 0.03f, ink.Fade(0.8f));
            m.Dot(0.44f, -0.12f, 0.05f, ink);
        }

        // ---------------------------------------------------------------- courses

        /// <summary>段位: the plates of a course, one behind the other.</summary>
        private static void Plates(VectorMesh m, Ink ink)
        {
            for (int i = 0; i < 3; i++)
            {
                m.Place(-0.08f + i * 0.08f, 0.3f - i * 0.3f, -0.1f);
                Ink tone = ink.Fade(i == 2 ? 1f : 0.35f + i * 0.2f);
                m.BoxLine(0f, 0f, 0.35f, 0.115f, 0.04f, 0.05f, tone);
                m.Diamond(-0.22f, 0f, 0.07f, tone, default);
                m.Line(-0.08f, 0f, 0.24f, 0f, 0.05f, tone.Fade(0.7f));
            }

            m.Place(0f, 0f);
        }

        /// <summary>后光: light all round.</summary>
        private static void Aureole(VectorMesh m, Ink ink)
        {
            for (int i = 0; i < 16; i++)
            {
                bool tall = i % 2 == 0;
                Point from = Point.Polar(i * PI / 8f, 0.34f);
                Point to = Point.Polar(i * PI / 8f, tall ? 0.5f : 0.43f);
                m.Line(from.X, from.Y, to.X, to.Y, 0.045f, ink.Fade(tall ? 0.9f : 0.6f));
            }

            m.Ring(0f, 0f, 0.25f, 0.065f, ink);
            m.Diamond(0f, 0f, 0.13f, ink, default);
        }

        // ---------------------------------------------------------------- parts

        /// <summary>An arrow's head at <paramref name="tip"/>, pointing along <paramref name="angle"/>.</summary>
        internal static void Head(VectorMesh m, Point tip, float angle, float size, Ink ink)
        {
            Point along = Point.Polar(angle, size);
            Point across = Point.Polar(angle + PI * 0.5f, size * 0.8f);
            m.Triangle(tip + along, tip + across, tip - across, ink);
        }

        /// <summary>A chevron pointing up, its point at <paramref name="top"/>.</summary>
        private static void Chevron(VectorMesh m, float top, float half, float drop, float width, Ink ink)
        {
            VectorMesh.Path path = m.NewPath();
            path.Add(-half, top - drop);
            path.Add(0f, top);
            path.Add(half, top - drop);
            m.Stroke(path, false, width, ink);
        }

        /// <summary>A moon lit from the left.</summary>
        internal static void Crescent(VectorMesh m, float x, float y, float radius, Ink ink)
        {
            var outer = new VectorMesh.Path();
            var inner = new VectorMesh.Path();
            const int STEPS = 22;
            for (int i = 0; i <= STEPS; i++)
            {
                float angle = PI * 0.28f + PI * 1.44f * i / STEPS;
                Point edge = Point.Polar(angle, radius);
                float along = (float)i / STEPS;
                outer.Add(x + edge.X, y + edge.Y);
                // The shadow's edge runs between the same two tips.
                float depth = 0.78f * (float)Math.Sin(along * PI);
                Point tipA = Point.Polar(PI * 0.28f, radius);
                Point chord = Point.Lerp(tipA, Point.Polar(PI * 1.72f, radius), along);
                inner.Add(x + chord.X - radius * depth * 0.95f, y + chord.Y);
            }

            m.Strip(outer, inner, ink, ink);
        }
    }
}
