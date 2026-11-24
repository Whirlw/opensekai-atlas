using System;
using Ink = OpenSekai.App.UI.VectorMesh.Ink;
using Path = OpenSekai.App.UI.VectorMesh.Path;
using Point = OpenSekai.App.UI.VectorMesh.Point;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The emblems of the avatars that the achievements give, each drawn from what its name says. Like
    /// the built-in ones they are flat, in the avatar's color and its lighter tone, with white kept
    /// for the one part that is lit.
    ///
    /// Pure math with no Unity types. Units: the avatar's disc is 1 across, the origin its center, y up.
    /// </summary>
    internal static class AvatarArt
    {
        private const float PI = (float)Math.PI;
        private const float TAU = VectorMesh.TAU;

        private static readonly Ink _white = Ink.White(1f);

        /// <summary>The five difficulties of 白昼折光, red to violet.</summary>
        private static readonly Ink[] _five =
        {
            Ink.Of(255, 116, 124), Ink.Of(255, 208, 92), Ink.Of(120, 226, 146), Ink.Of(104, 190, 255), Ink.Of(198, 136, 255)
        };

        internal static bool Has(string id) => Find(id) != null;

        internal static void Draw(VectorMesh mesh, string id, Ink color)
        {
            Action<VectorMesh, Tones> draw = Find(id);
            if (draw == null) return;
            mesh.Place(0f, 0f);
            draw(mesh, new Tones(color));
            mesh.Place(0f, 0f);
        }

        private static Action<VectorMesh, Tones> Find(string id)
        {
            switch (id)
            {
                case "encore_light": return EncoreLight;
                case "tour_book": return TourBook;
                case "practice_moon": return PracticeMoon;
                case "long_kite": return LongKite;
                case "flawless_atlas": return FlawlessAtlas;
                case "flying_arrow": return FlyingArrow;
                case "silk_moon": return SilkMoon;
                case "setlist": return Setlist;
                case "baton": return Baton;
                case "chromatic_keys": return ChromaticKeys;
                case "ranker": return Ranker;
                case "aureole": return Aureole;
                case "prism_eye": return PrismEye;
                case "seventh_light": return SeventhLight;
                case "final_barline": return FinalBarline;
                default: return null;
            }
        }

        // ---------------------------------------------------------------- practice and technique

        /// <summary>返场灯: a spotlight hung from above, its cone on a note.</summary>
        private static void EncoreLight(VectorMesh m, Tones t)
        {
            var lamp = new Point(0f, 0.21f);
            var floor = new Point(0f, -0.27f);
            m.Glow(0f, -0.24f, 0.4f, 0.16f, t.Color.With(0.5f));
            m.Beam(lamp, floor, 0.16f, 0.78f, _white.With(0.75f), t.Light.With(0.12f));
            m.Ellipse(0f, -0.27f, 0.27f, 0.065f, 0f, t.Light.With(0.34f));
            m.Box(0f, -0.25f, 0.16f, 0.042f, 0.042f, _white);
            // The yoke and the lamp.
            m.Line(0f, 0.46f, 0f, 0.36f, 0.032f, t.Light);
            Path yoke = m.NewPath();
            yoke.Add(-0.15f, 0.22f);
            yoke.Add(-0.15f, 0.35f);
            yoke.Add(0.15f, 0.35f);
            yoke.Add(0.15f, 0.22f);
            m.Stroke(yoke, false, 0.03f, t.Light.Fade(0.8f));
            m.Quad(new Point(-0.085f, 0.33f), new Point(0.085f, 0.33f), new Point(0.125f, 0.15f), new Point(-0.125f, 0.15f), t.Light);
            m.Ellipse(0f, 0.15f, 0.125f, 0.035f, 0f, _white);
            m.Diamond(-0.24f, 0.02f, 0.028f, t.Light, default);
            m.Diamond(0.26f, -0.06f, 0.022f, t.Light.Fade(0.8f), default);
        }

        /// <summary>巡演手册: a booklet folded open, a route across its pages to a pin.</summary>
        private static void TourBook(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.46f, 0.4f, t.Color.With(0.4f));
            m.Quad(new Point(-0.33f, 0.21f), new Point(0f, 0.15f), new Point(0f, -0.27f), new Point(-0.33f, -0.2f), t.Deep);
            m.Quad(new Point(0f, 0.15f), new Point(0.33f, 0.21f), new Point(0.33f, -0.2f), new Point(0f, -0.27f), Ink.Lerp(t.Deep, t.Color, 0.45f));
            Path cover = m.NewPath();
            cover.Add(-0.33f, 0.21f);
            cover.Add(0f, 0.15f);
            cover.Add(0.33f, 0.21f);
            cover.Add(0.33f, -0.2f);
            cover.Add(0f, -0.27f);
            cover.Add(-0.33f, -0.2f);
            m.Stroke(cover, true, 0.034f, t.Light);
            m.Line(0f, 0.15f, 0f, -0.27f, 0.024f, t.Light.Fade(0.6f));
            Point[] stops = { new Point(-0.24f, -0.1f), new Point(-0.15f, 0.05f), new Point(-0.05f, -0.06f), new Point(0.09f, -0.12f), new Point(0.19f, 0f) };
            for (int i = 0; i + 1 < stops.Length; i++)
            {
                for (int dot = 0; dot < 3; dot++)
                {
                    Point at = Point.Lerp(stops[i], stops[i + 1], dot / 3f);
                    m.Dot(at.X, at.Y, dot == 0 ? 0.022f : 0.012f, t.Light.Fade(dot == 0 ? 1f : 0.8f));
                }
            }

            // The pin stands over the page.
            m.Kite(0.19f, 0.13f, 0.085f, 0.085f, 0.15f, _white);
            m.Dot(0.19f, 0.135f, 0.034f, t.Color);
            m.Line(-0.25f, 0.13f, -0.1f, 0.103f, 0.02f, t.Light.Fade(0.5f));
            m.Line(-0.25f, 0.08f, -0.17f, 0.065f, 0.02f, t.Light.Fade(0.5f));
        }

        /// <summary>练习室的月亮: the moon in the practice room's window, over a row of keys.</summary>
        private static void PracticeMoon(VectorMesh m, Tones t)
        {
            m.Glow(0.02f, 0.1f, 0.4f, 0.36f, t.Color.With(0.5f));
            Path window = m.NewPath();
            window.Add(-0.25f, -0.15f);
            window.Arc(0f, 0.11f, 0.25f, 0.25f, PI, 0f, 22);
            window.Add(0.25f, -0.15f);
            m.Fill(window, new Point(0f, 0.05f), new VectorMesh.Rise(t.Deep.With(0.9f), Ink.Lerp(t.Deep, t.Color, 0.5f), -0.15f, 0.36f));
            MedalEmblemArt.Crescent(m, 0.01f, 0.12f, 0.15f, _white);
            m.Diamond(0.13f, 0.2f, 0.026f, t.Light, default);
            m.Diamond(-0.17f, 0.04f, 0.018f, t.Light.Fade(0.8f), default);
            m.Diamond(0.17f, -0.03f, 0.018f, t.Light.Fade(0.8f), default);
            m.Line(0f, 0.36f, 0f, -0.15f, 0.016f, t.Light.With(0.4f));
            m.Line(-0.25f, 0.06f, 0.25f, 0.06f, 0.016f, t.Light.With(0.4f));
            window = m.NewPath();
            window.Add(-0.25f, -0.15f);
            window.Arc(0f, 0.11f, 0.25f, 0.25f, PI, 0f, 22);
            window.Add(0.25f, -0.15f);
            m.Stroke(window, true, 0.032f, t.Light);
            // The keys under the sill, moonlit.
            for (int i = 0; i < 7; i++) m.Box(-0.21f + i * 0.07f, -0.26f, 0.03f, 0.07f, 0.01f, t.Light.Fade(i == 3 ? 1f : 0.55f));
            foreach (int i in new[] { 0, 1, 3, 4, 5 }) m.Box(-0.175f + i * 0.07f, -0.23f, 0.018f, 0.044f, 0.006f, t.Deep);
        }

        /// <summary>长线风筝: a kite at the end of one very long line.</summary>
        private static void LongKite(VectorMesh m, Tones t)
        {
            m.Glow(0.12f, 0.14f, 0.36f, 0.36f, t.Color.With(0.45f));
            // Its own path: the bows are drawn along it.
            var line = new Path();
            line.Add(0.04f, 0.02f);
            line.Curve(new Point(-0.06f, -0.16f), new Point(0.2f, -0.2f), new Point(0.04f, -0.28f), 14);
            line.Curve(new Point(-0.1f, -0.35f), new Point(-0.12f, -0.16f), new Point(-0.4f, -0.3f), 16);
            m.Stroke(line, false, 0.02f, new VectorMesh.Ramp(t.Light, t.Light.With(0.35f)));
            foreach (float along in new[] { 0.16f, 0.36f, 0.56f, 0.78f })
            {
                Point bow = line.At(along, false);
                m.Kite(bow.X, bow.Y, 0.045f, 0.022f, 0.022f, t.Light.Fade(1f - along * 0.4f));
            }

            m.Place(0.14f, 0.15f, -0.6f);
            var top = new Point(0f, 0.22f);
            var right = new Point(0.165f, 0.04f);
            var bottom = new Point(0f, -0.24f);
            var left = new Point(-0.165f, 0.04f);
            var heart = new Point(0f, 0.04f);
            m.Kite(0f, 0.04f, 0.165f, 0.18f, 0.28f, t.Light);
            m.Triangle(top, heart, left, _white);
            m.Triangle(heart, right, bottom, t.Color);
            m.Line(0f, 0.22f, 0f, -0.24f, 0.014f, t.Deep.With(0.5f));
            m.Line(-0.165f, 0.04f, 0.165f, 0.04f, 0.014f, t.Deep.With(0.5f));
        }

        /// <summary>无瑕星图: a chart of the sky with every star where it should be.</summary>
        private static void FlawlessAtlas(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.46f, 0.46f, Ink.Prism(0.25f, 0.45f));
            for (int i = 0; i < 2; i++)
            {
                Path ring = m.NewPath();
                float radius = i == 0 ? 0.36f : 0.2f;
                ring.Arc(0f, 0f, radius, radius, 0f, TAU, 48, false);
                m.Stroke(ring, true, i == 0 ? 0.022f : 0.012f, new VectorMesh.Spectrum(0.7f, 0.75f, 0.3f, i == 0 ? 0.95f : 0.45f));
            }

            for (int i = 0; i < 12; i++)
            {
                Point from = Point.Polar(i * PI / 6f, 0.315f);
                Point to = Point.Polar(i * PI / 6f, 0.345f);
                m.Line(from.X, from.Y, to.X, to.Y, 0.012f, t.Light.With(0.7f));
            }

            m.Line(-0.34f, 0f, 0.34f, 0f, 0.008f, t.Light.With(0.3f));
            m.Line(0f, -0.34f, 0f, 0.34f, 0.008f, t.Light.With(0.3f));
            Point[] stars = { new Point(-0.24f, 0.12f), new Point(-0.1f, 0.22f), new Point(0.12f, 0.17f), new Point(0.23f, -0.02f), new Point(0.1f, -0.2f), new Point(-0.15f, -0.18f) };
            Path figure = m.NewPath();
            foreach (Point star in stars) figure.Add(star);
            m.Stroke(figure, true, 0.014f, new VectorMesh.Spectrum(1f, 0.7f, 0.4f, 0.8f));
            for (int i = 0; i < stars.Length; i++)
            {
                Ink ink = Ink.Lerp(Ink.Prism(0.1f + i * 0.08f), _white, 0.35f);
                m.Diamond(stars[i].X, stars[i].Y, i % 2 == 0 ? 0.042f : 0.034f, ink, _white);
            }

            m.Place(0f, 0f, PI * 0.25f);
            m.Sparkle(0f, 0f, 0.1f, 0.1f, Ink.Prism(0.4f, 0.8f));
            m.Place(0f, 0f);
            m.Sparkle(0f, 0f, 0.19f, 0.19f, _white);
        }

        /// <summary>流矢: an arrow in flight.</summary>
        private static void FlyingArrow(VectorMesh m, Tones t)
        {
            m.Glow(0.12f, 0.12f, 0.36f, 0.36f, t.Color.With(0.45f));
            m.Place(0.035f, 0.035f, PI * 0.25f);
            // Along x: the tail streaks, the shaft, the feathers, the head.
            m.Line(-0.42f, 0.1f, -0.12f, 0.1f, 0.022f, t.Light.With(0f), t.Light.With(0.65f));
            m.Line(-0.36f, -0.11f, -0.04f, -0.11f, 0.022f, t.Light.With(0f), t.Light.With(0.65f));
            m.Line(-0.3f, 0.18f, -0.14f, 0.18f, 0.016f, t.Light.With(0f), t.Light.With(0.45f));
            m.Line(-0.42f, 0f, 0.22f, 0f, 0.042f, t.Light.With(0.25f), _white);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.18f - i * 0.085f;
                Path feather = m.NewPath();
                feather.Add(x - 0.13f, 0.085f);
                feather.Add(x, 0f);
                feather.Add(x - 0.13f, -0.085f);
                m.Stroke(feather, false, 0.036f, t.Light.Fade(1f - i * 0.24f));
            }

            Path head = m.NewPath();
            head.Add(0.4f, 0f);
            head.Add(0.14f, 0.15f);
            head.Add(0.2f, 0f);
            head.Add(0.14f, -0.15f);
            m.Fill(head, new Point(0.24f, 0f), _white);
        }

        /// <summary>丝线月: a moon strung from silk, one thread still hanging.</summary>
        private static void SilkMoon(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0.04f, 0.44f, 0.44f, t.Color.With(0.45f));
            const int THREADS = 6;
            const float R = 0.27f;
            const float TIP = PI * 0.24f;
            var place = new Point(-0.03f, 0.1f);
            for (int i = 0; i < THREADS; i++)
            {
                // Every thread runs between the same two tips; the first is the moon's edge.
                float share = (float)i / (THREADS - 1);
                Path thread = m.NewPath();
                for (int step = 0; step <= 30; step++)
                {
                    float along = step / 30f;
                    Point edge = Point.Polar(TIP + (TAU - 2f * TIP) * along, R);
                    Point chord = Point.Lerp(Point.Polar(TIP, R), Point.Polar(TAU - TIP, R), along);
                    var inside = new Point(chord.X - 0.2f * (float)Math.Sin(along * PI), chord.Y);
                    Point at = Point.Lerp(edge, inside, share);
                    thread.Add(at + place);
                }

                Ink ink = Ink.Lerp(_white, t.Light, Math.Min(1f, share * 1.6f));
                m.Stroke(thread, false, i == 0 ? 0.032f : 0.015f, ink.With(i == 0 ? 1f : 0.9f - 0.3f * share));
            }

            Point upper = Point.Polar(TIP, R) + place;
            Point lower = Point.Polar(-TIP, R) + place;
            m.Diamond(upper.X, upper.Y, 0.04f, _white, default);
            // The loose thread, drawn out like a hold past its points down to its note.
            var loose = new Path();
            loose.Add(lower);
            loose.Curve(new Point(0.3f, -0.26f), new Point(0.16f, -0.3f), new Point(-0.02f, -0.22f), 12);
            loose.Curve(new Point(-0.2f, -0.14f), new Point(-0.3f, -0.26f), new Point(-0.2f, -0.35f), 12);
            m.Stroke(loose, false, 0.02f, new VectorMesh.Ramp(_white, t.Light));
            m.Diamond(lower.X, lower.Y, 0.04f, _white, default);
            for (int i = 0; i < 2; i++)
            {
                Point knot = loose.At(0.34f + i * 0.3f, false);
                m.Diamond(knot.X, knot.Y, 0.03f, t.Light, default);
            }

            m.Box(-0.17f, -0.365f, 0.13f, 0.032f, 0.032f, _white);
            m.Diamond(0.16f, 0.13f, 0.03f, t.Light, default);
            m.Diamond(0.27f, 0.06f, 0.02f, t.Light.Fade(0.7f), default);
        }

        // ---------------------------------------------------------------- consistency and courses

        /// <summary>点歌单: the sheet with the songs asked for, the next one marked.</summary>
        private static void Setlist(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.42f, 0.44f, t.Color.With(0.4f));
            m.Place(0.02f, -0.01f, -0.09f);
            m.Box(0f, 0f, 0.22f, 0.3f, 0.035f, t.Deep);
            m.BoxLine(0f, 0f, 0.22f, 0.3f, 0.035f, 0.03f, t.Light);
            m.Box(0f, 0.3f, 0.085f, 0.04f, 0.02f, t.Light);
            m.Dot(0f, 0.325f, 0.018f, t.Deep);
            float[] end = { 0.15f, 0.08f, 0.13f, 0.04f };
            for (int i = 0; i < end.Length; i++)
            {
                float y = 0.16f - i * 0.12f;
                bool next = i == 1;
                if (next) m.Box(0f, y, 0.19f, 0.052f, 0.026f, t.Color.With(0.75f));
                m.Diamond(-0.13f, y, next ? 0.036f : 0.026f, next ? _white : t.Light.Fade(0.8f), default);
                m.Line(-0.07f, y, end[i], y, 0.03f, next ? _white : t.Light.Fade(0.55f));
            }

            m.Line(-0.1f, -0.25f, 0.1f, -0.25f, 0.016f, t.Light.Fade(0.35f));
        }

        /// <summary>接力棒: the baton, handed on along an unbroken line.</summary>
        private static void Baton(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.46f, 0.4f, t.Color.With(0.45f));
            Path arc = m.NewPath();
            arc.Add(-0.42f, -0.1f);
            arc.Curve(new Point(-0.2f, -0.34f), new Point(0.16f, -0.34f), new Point(0.4f, -0.12f), 20);
            m.Stroke(arc, false, 0.02f, new VectorMesh.Ramp(t.Light.With(0f), t.Light.With(0.9f)));
            MedalEmblemArt.Head(m, new Point(0.385f, -0.135f), 0.72f, 0.06f, t.Light);
            m.Place(0f, 0.03f, 0.5f);
            for (int i = 0; i < 3; i++)
                m.Line(-0.46f + i * 0.03f, 0.17f - i * 0.17f, -0.36f, 0.17f - i * 0.17f, 0.02f, t.Light.With(0f), t.Light.With(0.6f));
            m.Box(0f, 0f, 0.31f, 0.078f, 0.04f, t.Light);
            m.Box(0f, 0.038f, 0.28f, 0.022f, 0.02f, _white);
            for (int i = 0; i < 3; i++) m.Box(-0.15f + i * 0.15f, 0f, 0.026f, 0.078f, 0f, t.Color);
            m.Place(0f, 0f);
            m.Diamond(0.3f, 0.28f, 0.04f, _white, default);
            m.Diamond(0.38f, 0.2f, 0.022f, t.Light, default);
        }

        /// <summary>半音键盘: a keyboard going up a step at a time, the next key lit.</summary>
        private static void ChromaticKeys(VectorMesh m, Tones t)
        {
            m.Glow(0.06f, 0f, 0.44f, 0.4f, t.Color.With(0.45f));
            const int KEYS = 5;
            for (int i = 0; i < KEYS; i++)
            {
                float x = -0.28f + i * 0.14f;
                float top = -0.06f + i * 0.085f;
                bool lit = i == KEYS - 1;
                m.Box(x, (top - 0.3f) * 0.5f, 0.06f, (top + 0.3f) * 0.5f, 0.02f, lit ? _white : t.Light.Fade(0.6f + 0.06f * i));
                if (lit) m.Glow(x, top - 0.06f, 0.16f, 0.2f, _white.With(0.6f));
            }

            for (int i = 0; i < KEYS - 1; i++)
            {
                if (i == 2) continue;
                float x = -0.21f + i * 0.14f;
                float top = i * 0.085f;
                m.Box(x, top - 0.13f, 0.037f, 0.13f, 0.012f, t.Deep);
                m.BoxLine(x, top - 0.13f, 0.037f, 0.13f, 0.012f, 0.012f, t.Light.With(0.5f));
            }

            Path climb = m.NewPath();
            climb.Add(-0.33f, 0.12f);
            climb.Add(-0.2f, 0.12f);
            climb.Add(-0.2f, 0.22f);
            climb.Add(-0.06f, 0.22f);
            climb.Add(-0.06f, 0.32f);
            climb.Add(0.07f, 0.32f);
            m.Stroke(climb, false, 0.022f, new VectorMesh.Ramp(t.Light.With(0.2f), t.Light));
            MedalEmblemArt.Head(m, new Point(0.07f, 0.32f), 0f, 0.055f, t.Light);
        }

        /// <summary>Ranker: the top crest, crowned.</summary>
        private static void Ranker(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.46f, 0.46f, Ink.Prism(0.25f, 0.5f));
            Path back = m.NewPath();
            back.Arc(0f, -0.04f, 0.4f, 0.12f, 0.3f, PI - 0.3f, 24, true, -0.3f);
            m.Stroke(back, false, 0.016f, Ink.Prism(0.4f, 0.5f));
            Path hex = m.NewPath();
            hex.Arc(0f, -0.04f, 0.25f, 0.25f, PI * 0.5f, PI * 2.5f, 6, false);
            m.Fill(hex, new Point(0f, -0.04f), new VectorMesh.Rise(t.Deep, Ink.Lerp(t.Deep, t.Color, 0.5f), -0.3f, 0.2f));
            hex = m.NewPath();
            hex.Arc(0f, -0.04f, 0.25f, 0.25f, PI * 0.5f, PI * 2.5f, 6, false);
            hex.Subdivide(0.06f, true);
            m.Stroke(hex, true, 0.036f, new VectorMesh.Spectrum(1f, 0.75f, 0.15f, 1f));
            hex = m.NewPath();
            hex.Arc(0f, -0.04f, 0.2f, 0.2f, PI * 0.5f, PI * 2.5f, 6, false);
            m.Stroke(hex, true, 0.01f, Ink.Prism(0.3f, 0.6f));
            m.Place(0f, -0.04f, PI * 0.25f);
            m.Sparkle(0f, 0f, 0.09f, 0.09f, Ink.Prism(0.35f, 0.8f));
            m.Place(0f, 0f);
            m.Sparkle(0f, -0.04f, 0.17f, 0.17f, _white);
            // Three points over the plaque, as a crown.
            for (int i = -1; i <= 1; i++)
                m.Kite(i * 0.15f, 0.29f - Math.Abs(i) * 0.065f, 0.045f, i == 0 ? 0.1f : 0.075f, 0.055f, Ink.Lerp(Ink.Prism(0.25f + i * 0.2f), _white, 0.45f));
            Path front = m.NewPath();
            front.Arc(0f, -0.04f, 0.4f, 0.12f, PI - 0.3f, TAU + 0.3f, 32, true, -0.3f);
            m.Stroke(front, false, 0.022f, new VectorMesh.Spectrum(0.7f, 0.75f, 0.3f, 1f));
            m.Dot(0.36f, -0.17f, 0.034f, _white);
        }

        /// <summary>后光: light all round a 段位 plate.</summary>
        private static void Aureole(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.48f, 0.48f, t.Color.With(0.55f));
            for (int i = 0; i < 24; i++)
            {
                bool tall = i % 2 == 0;
                float angle = i * TAU / 24f;
                Point from = Point.Polar(angle, 0.3f);
                Point to = Point.Polar(angle, tall ? 0.44f : 0.37f);
                m.Line(from.X, from.Y, to.X, to.Y, tall ? 0.024f : 0.016f, t.Light, t.Light.With(0.2f));
            }

            m.Ring(0f, 0f, 0.25f, 0.036f, _white);
            m.Ring(0f, 0f, 0.2f, 0.012f, t.Light.With(0.6f));
            for (int i = 0; i < 4; i++)
            {
                Point gem = Point.Polar(PI * 0.25f + i * PI * 0.5f, 0.25f);
                m.Diamond(gem.X, gem.Y, 0.04f, t.Light, _white);
            }

            m.Box(0f, 0f, 0.3f, 0.095f, 0.03f, t.Deep);
            m.BoxLine(0f, 0f, 0.3f, 0.095f, 0.03f, 0.028f, t.Light);
            m.Diamond(-0.2f, 0f, 0.052f, _white, default);
            m.Line(-0.1f, 0.025f, 0.22f, 0.025f, 0.026f, t.Light);
            m.Line(-0.1f, -0.032f, 0.12f, -0.032f, 0.022f, t.Light.Fade(0.6f));
        }

        // ---------------------------------------------------------------- the special ones

        /// <summary>折光之眼: an eye with a prism for a pupil and the five colors in its iris.</summary>
        private static void PrismEye(VectorMesh m, Tones t)
        {
            m.Glow(0f, 0f, 0.48f, 0.36f, t.Color.With(0.5f));
            for (int i = 0; i < _five.Length; i++)
            {
                float angle = PI * 0.5f + (2 - i) * 0.3f;
                Point from = Point.Polar(angle, 0.31f);
                Point to = Point.Polar(angle, 0.31f + (i == 2 ? 0.11f : 0.09f));
                m.Line(from.X, from.Y - 0.02f, to.X, to.Y - 0.02f, 0.026f, _five[i], _five[i].With(0.6f));
            }

            Path lids = m.NewPath();
            Almond(lids, 0.4f, 0.24f);
            m.Fill(lids, default, new VectorMesh.Rise(Ink.Lerp(t.Deep, t.Color, 0.2f), t.Deep, -0.24f, 0.24f));
            for (int i = 0; i < _five.Length; i++)
            {
                float from = PI * 0.5f - i * TAU / 5f + 0.06f;
                m.ArcLine(0f, 0f, 0.155f, from - 0.06f, from - TAU / 5f, 0.062f, _five[i]);
            }

            m.Dot(0f, 0f, 0.118f, Ink.Of(16, 16, 38));
            m.Triangle(new Point(0f, 0.085f), new Point(0.078f, -0.05f), new Point(-0.078f, -0.05f), _white);
            m.Triangle(new Point(0f, 0.085f), new Point(0.078f, -0.05f), new Point(0f, -0.05f), Ink.Of(196, 206, 255));
            lids = m.NewPath();
            Almond(lids, 0.4f, 0.24f);
            m.Stroke(lids, true, 0.036f, t.Light);
            m.Line(-0.52f, -0.05f, -0.4f, 0f, 0.03f, _white.With(0.3f), _white);
            m.Diamond(0f, -0.35f, 0.04f, _white, default);
            m.Diamond(-0.11f, -0.32f, 0.024f, t.Light, default);
            m.Diamond(0.11f, -0.32f, 0.024f, t.Light, default);
        }

        /// <summary>第七束光: seven beams over a ticket torn a hundred times; the seventh is lit.</summary>
        private static void SeventhLight(VectorMesh m, Tones t)
        {
            var stage = new Point(0f, -0.22f);
            for (int i = 0; i < 7; i++)
            {
                if (i == 3) continue;
                float angle = PI * 0.5f - (i - 3) * 0.33f;
                Point to = Point.Polar(angle, 0.62f) + stage;
                m.Beam(stage, to, 0.03f, 0.16f, t.Light.With(0.75f), t.Color.With(0.16f));
            }

            m.Beam(stage, new Point(0f, 0.5f), 0.07f, 0.34f, _white, _white.With(0.5f));
            m.Beam(stage, new Point(0f, 0.5f), 0.03f, 0.14f, _white, _white.With(0.7f));
            m.Glow(0f, -0.2f, 0.4f, 0.2f, t.Light.With(0.55f));
            m.Place(0f, -0.2f, 0f);
            Path ticket = m.NewPath();
            const float W = 0.27f;
            const float H = 0.125f;
            const float BITE = 0.045f;
            ticket.Arc(-W, 0f, BITE, BITE, -PI * 0.5f, PI * 0.5f, 8);
            ticket.Add(-W, H);
            ticket.Add(W, H);
            ticket.Arc(W, 0f, BITE, BITE, PI * 0.5f, PI * 1.5f, 8);
            ticket.Add(W, -H);
            ticket.Add(-W, -H);
            m.Fill(ticket, default, t.Deep);
            m.Stroke(ticket, true, 0.028f, t.Light);
            for (int i = 0; i < 4; i++) m.Dot(0.185f, -0.075f + i * 0.05f, 0.011f, t.Light);
            SpecialPlateArt.Hundred(m, 0f, 0f, 0.068f, 0.026f, _white);
        }

        /// <summary>终止双线: the last chord before the final barline.</summary>
        private static void FinalBarline(VectorMesh m, Tones t)
        {
            m.Glow(0.1f, 0f, 0.42f, 0.42f, t.Color.With(0.35f));
            for (int i = 0; i < 5; i++)
            {
                float y = (2 - i) * 0.105f;
                m.Line(-0.42f, y, 0.25f, y, 0.014f, t.Light.With(0.1f), t.Light.With(0.75f));
            }

            m.Line(0.13f, 0.225f, 0.13f, -0.225f, 0.026f, t.Light);
            m.Line(0.225f, 0.225f, 0.225f, -0.225f, 0.075f, _white);
            // The chord.
            float[] notes = { 0.105f, -0.0525f, -0.21f };
            foreach (float y in notes) m.Ellipse(-0.1f, y, 0.074f, 0.054f, 0.38f, _white);
            m.Line(-0.034f, -0.2f, -0.034f, 0.32f, 0.026f, _white);
            m.Diamond(-0.28f, 0.05f, 0.028f, t.Light.Fade(0.7f), default);
            m.Diamond(-0.28f, -0.16f, 0.02f, t.Light.Fade(0.5f), default);
        }

        // ---------------------------------------------------------------- parts

        /// <summary>An eye's outline, pointed at both ends.</summary>
        private static void Almond(Path path, float half, float tall)
        {
            path.Add(-half, 0f);
            path.Curve(new Point(-half * 0.5f, tall * 1.25f), new Point(half * 0.5f, tall * 1.25f), new Point(half, 0f), 24);
            path.Curve(new Point(half * 0.5f, -tall * 1.25f), new Point(-half * 0.5f, -tall * 1.25f), new Point(-half + 0.001f, 0f), 24);
        }

        /// <summary>The tones of one avatar.</summary>
        private readonly struct Tones
        {
            internal readonly Ink Color;

            /// <summary>Strokes and solid parts, as on the built-in avatars.</summary>
            internal readonly Ink Light;

            /// <summary>Parts in shadow, over the disc.</summary>
            internal readonly Ink Deep;

            internal Tones(Ink color)
            {
                Color = color;
                Light = Ink.Lerp(color, Ink.White(1f), 0.55f);
                Deep = Ink.Lerp(Ink.Of(21, 26, 34), color, 0.3f);
            }
        }
    }
}
