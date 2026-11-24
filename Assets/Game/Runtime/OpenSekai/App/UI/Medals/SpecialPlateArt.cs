using System;
using Ink = OpenSekai.App.UI.VectorMesh.Ink;
using Path = OpenSekai.App.UI.VectorMesh.Path;
using Point = OpenSekai.App.UI.VectorMesh.Point;

namespace OpenSekai.App.UI
{
    /// <summary>Where things are on a plate that a special medal is drawn on: its height is 1, the origin its center, y up.</summary>
    internal readonly struct PlateSpace
    {
        /// <param name="aspect">How many times as wide as high the plate is.</param>
        /// <param name="compact">A medal drawn small: its face fills the rectangle and has no band.</param>
        /// <param name="grade">The grade's divider, from the center; the right edge when there is no grade.</param>
        /// <param name="textEnd">Where the text ends.</param>
        internal PlateSpace(float aspect, bool compact, float band, float grade, float textEnd)
        {
            Compact = compact;
            float inset = compact ? 0f : MedalArt.INSET;
            HalfWidth = aspect * 0.5f - inset;
            HalfHeight = 0.5f - inset;
            Radius = compact ? MedalArt.COMPACT_RADIUS : MedalArt.RADIUS;
            Band = band;
            Grade = grade;
            TextEnd = textEnd;
        }

        internal readonly bool Compact;
        internal readonly float HalfWidth;
        internal readonly float HalfHeight;
        internal readonly float Radius;

        /// <summary>The band's right edge; the left edge of a compact plate.</summary>
        internal readonly float Band;

        internal readonly float Grade;
        internal readonly float TextEnd;
    }

    /// <summary>
    /// The medals of the 特殊 tier, each with a face, a frame, an emblem and motions of its own instead
    /// of a tint and a tier's frame. They keep the medals' language (a rectangular plate, flat strokes,
    /// diamonds) and are recognised by their outline: 皆传's frame is one golden line that rises into
    /// a peak per letter, 真·皆传 is cut like a crystal and carries a crystal per letter, 白昼折光
    /// comes apart into five colors toward its right end, 不落幕的安可 is a ticket with running lights,
    /// and 终乐章 keeps 极阶's frame round a staff that runs into the final barline.
    ///
    /// Four layers: the face and what moves on it lie under the text, the frame and what runs along it
    /// over it. The moving two are drawn again for each moment; nothing else changes. A plate, a chip
    /// and a thumbnail differ only in their <see cref="PlateSpace"/>; a disc has drawings of its own.
    ///
    /// Pure math with no Unity types.
    /// </summary>
    internal abstract partial class SpecialPlateArt
    {
        protected const float PI = (float)Math.PI;
        protected const float TAU = VectorMesh.TAU;

        /// <summary>How far past the plate's rectangle a drawing can reach, against its height.</summary>
        internal const float MARGIN = 0.38f;

        private readonly Path _outline = new Path();
        private PlateSpace _space;

        protected PlateSpace Space => _space;

        /// <summary>The plate's outline, points close enough for a light to run along it.</summary>
        protected Path OutlinePath => _outline;

        // ---------------------------------------------------------------- what the plates need to know

        internal static bool Has(string faceId) => Create(faceId) != null;

        /// <summary>The art of <paramref name="faceId"/>; null for a medal that is not special.</summary>
        internal static SpecialPlateArt Create(string faceId)
        {
            string series = MedalEmblemArt.SeriesOf(faceId, out int level);
            switch (series)
            {
                case "FCLVL": return level > 10 ? new FullComboArt(Math.Min(4, level - 10)) : null;
                case "APLVL": return level > 10 ? new AllPerfectArt(Math.Min(4, level - 10)) : null;
                case "PRISM": return new DaylightArt();
                case "ENCORE": return new EncoreArt();
                case "CODA": return new CodaArt();
                default: return null;
            }
        }

        /// <summary>Runs through the text and the grade, from top to bottom.</summary>
        internal abstract Ink TextTop { get; }

        internal abstract Ink TextBottom { get; }

        /// <summary>True when the frame is one of <see cref="MedalArt"/>'s, under what this draws.</summary>
        internal virtual bool KeepsTierFrame => false;

        /// <summary>True when the face has a divider of its own before the grade.</summary>
        internal virtual bool DividesGrade => false;

        internal void SetSpace(in PlateSpace space)
        {
            _space = space;
            _outline.Clear();
            Outline(_outline, 0f);
            _outline.Subdivide(space.Compact ? 0.16f : 0.12f, true);
            Arrange();
        }

        /// <summary>The face, its band and what stands still on it.</summary>
        internal abstract void Ground(VectorMesh m);

        /// <summary>What moves on the face, under the text.</summary>
        internal abstract void GroundMotion(VectorMesh m, float time);

        internal abstract void Frame(VectorMesh m);

        /// <summary>What runs along the frame, over the text.</summary>
        internal abstract void FrameMotion(VectorMesh m, float time);

        /// <summary>The emblem, in a square of -0.5..0.5.</summary>
        internal abstract void Emblem(VectorMesh m);

        /// <summary>A disc's face and rim; the disc is 1 across.</summary>
        internal abstract void Disc(VectorMesh m);

        /// <summary>What moves round a disc.</summary>
        internal abstract void DiscMotion(VectorMesh m, float time);

        /// <summary>The closed outline grown by <paramref name="grow"/>, from the middle of the left end, over the top.</summary>
        protected abstract void Outline(Path path, float grow);

        /// <summary>Works out what depends on the plate's size.</summary>
        protected virtual void Arrange()
        {
        }

        // ---------------------------------------------------------------- parts they share

        protected enum Corner { Round, Cut }

        /// <summary>The plate's outline with peaks along the top and the bottom; none for a plain one.</summary>
        protected void PlateOutline(Path path, float grow, Corner corner, float cut, Peak[] top = null, Peak[] bottom = null, float notch = 0f)
        {
            float w = _space.HalfWidth + grow;
            float h = _space.HalfHeight + grow;
            float r = corner == Corner.Round ? _space.Radius + grow : cut + grow * 0.42f;
            if (notch > 0f)
            {
                // A bite out of the end, shallower for the lines outside it.
                float bite = Math.Max(0.02f, notch - grow * 0.6f);
                path.Arc(-w, 0f, bite, bite, -PI * 0.5f, PI * 0.5f, 8);
            }
            else
            {
                path.Add(-w, 0f);
            }

            AddCorner(path, -w, h, r, corner, 0);
            AddPeaks(path, top, h, grow, 1f);
            AddCorner(path, w, h, r, corner, 1);
            if (notch > 0f)
            {
                float bite = Math.Max(0.02f, notch - grow * 0.6f);
                path.Arc(w, 0f, bite, bite, PI * 0.5f, PI * 1.5f, 8);
            }
            else
            {
                path.Add(w, 0f);
            }

            AddCorner(path, w, -h, r, corner, 2);
            AddPeaks(path, bottom, -h, grow, -1f);
            AddCorner(path, -w, -h, r, corner, 3);
        }

        private static void AddCorner(Path path, float x, float y, float r, Corner corner, int index)
        {
            float cx = x > 0f ? x - r : x + r;
            float cy = y > 0f ? y - r : y + r;
            float from = PI - index * PI * 0.5f;
            if (corner == Corner.Round)
            {
                path.Arc(cx, cy, r, r, from, from - PI * 0.5f, 7);
                return;
            }

            Point a = Point.Polar(from, r);
            Point b = Point.Polar(from - PI * 0.5f, r);
            path.Add(cx + a.X, cy + a.Y);
            path.Add(cx + b.X, cy + b.Y);
        }

        private static void AddPeaks(Path path, Peak[] peaks, float edge, float grow, float up)
        {
            if (peaks == null || peaks.Length == 0) return;
            const int STEPS = 9;
            // Over the top from left to right; under the bottom from right to left.
            float first = peaks[0].X - peaks[0].Half - grow;
            float last = peaks[peaks.Length - 1].X + peaks[peaks.Length - 1].Half + grow;
            float from = up > 0f ? first : last;
            for (int i = 0; i <= peaks.Length * 2; i++)
            {
                // Knots: an end, then each tip and the valley after it.
                int at = up > 0f ? i : peaks.Length * 2 - i;
                float knot = at == 0 ? first : at == peaks.Length * 2 ? last
                    : at % 2 == 1 ? peaks[at / 2].X : (peaks[at / 2 - 1].X + peaks[at / 2].X) * 0.5f;
                for (int step = i == 0 ? STEPS : 1; step <= STEPS; step++)
                {
                    float x = VectorMesh.Mix(from, knot, (float)step / STEPS);
                    path.Add(x, edge + up * Rise(peaks, x, grow));
                }

                from = knot;
            }
        }

        /// <summary>How high the peaks stand over their edge at <paramref name="x"/>.</summary>
        private static float Rise(Peak[] peaks, float x, float grow)
        {
            float rise = 0f;
            foreach (Peak peak in peaks)
            {
                float near = 1f - Math.Abs(x - peak.X) / (peak.Half + grow);
                if (near > 0f) rise = Math.Max(rise, (peak.Height + grow * 0.5f) * (float)Math.Pow(near, 2.4d));
            }

            return rise;
        }

        /// <summary>The part of the face left of <paramref name="right"/>, corners and all.</summary>
        protected void LeftPart(Path path, float right, Corner corner, float cut)
        {
            float w = _space.HalfWidth;
            float h = _space.HalfHeight;
            float r = corner == Corner.Round ? _space.Radius : cut;
            AddCorner(path, -w, h, r, corner, 0);
            path.Add(right, h);
            path.Add(right, -h);
            AddCorner(path, -w, -h, r, corner, 3);
        }

        /// <summary>A row of diamonds along the long sides, out from <paramref name="start"/> on both sides of the middle.</summary>
        protected static void SideDiamonds(VectorMesh m, float start, float y, Ink ink, Ink heart)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                m.Diamond(side * start, y, 0.052f, ink, heart);
                m.Diamond(side * (start + 0.11f), y, 0.032f, ink, default);
            }
        }

        /// <summary>The diamonds out of an end, the nearest largest.</summary>
        protected static void EndDiamonds(VectorMesh m, float x, float side, Ink ink, Ink heart, int count)
        {
            m.Diamond(x, 0f, 0.115f, ink, heart);
            if (count > 1) m.Diamond(x + side * 0.16f, 0f, 0.052f, ink, heart);
            if (count > 2) m.Diamond(x + side * 0.235f, 0f, 0.03f, ink, default);
        }

        /// <summary>The four corners of a frame line grown by <paramref name="grow"/>.</summary>
        protected void CornerDiamonds(VectorMesh m, float grow, float round, float size, Ink ink, Ink heart)
        {
            float turn = (round + grow) * (1f - 0.7071f);
            float x = _space.HalfWidth + grow - turn;
            float y = _space.HalfHeight + grow - turn;
            for (int i = 0; i < 4; i++) m.Diamond(i % 2 == 0 ? x : -x, i < 2 ? y : -y, size, ink, heart);
        }

        /// <summary>A light that runs round the outline.</summary>
        protected void RunAround(VectorMesh m, Ink head, Ink tail, float width, float at, float length) =>
            m.Stroke(_outline, true, width, new VectorMesh.Comet(head, tail, at, length, true));

        /// <summary>1 as <paramref name="at"/> passes <paramref name="point"/>, fading after; both 0 to 1 on a loop.</summary>
        protected static float Passed(float at, float point, float fade)
        {
            float since = at - point;
            since -= (float)Math.Floor(since);
            return since < fade ? (1f - since / fade) * (1f - since / fade) : 0f;
        }

        /// <summary>A sharp rise out of the plate's edge.</summary>
        protected readonly struct Peak
        {
            internal readonly float X;
            internal readonly float Half;
            internal readonly float Height;

            internal Peak(float x, float half, float height)
            {
                X = x;
                Half = half;
                Height = height;
            }
        }
    }
}
