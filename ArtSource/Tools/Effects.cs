using System;
using System.Collections.Generic;
using System.IO;

// Hit effects. The two atlases and the lane sheet are cut by the particle prefabs at fixed slots; the slot list at
// each Cell call says where, and which effect plays it. The shapes are the splash's (rings on the floor, short round
// strokes, soft light) and the badges' (star, diamond).
namespace ArtV2
{
    public static partial class Effects
    {
        private const string Tap = "live_tap_effect_0_";

        public static IEnumerable<Asset> All(string data)
        {
            yield return new Asset(Tap + "tex_note_common_all.png", 1024, 1024, 2, false, c => Atlas(c, 1f));
            yield return new Asset(Tap + "tex_note_common_all_v2.png", 1024, 1024, 2, false, c => Atlas(c, 0.78f));
            yield return new Asset(Tap + "tex_note_common_lane.png", 512, 512, 2, false, LaneSheet);
            yield return new Asset(Tap + "tex_note_tail_01_v2.png", 64, 64, 4, false, Tail);
            yield return new Asset(Tap + "tex_ove_damage_01.png", 256, 256, 1, true, Damage);
            foreach (Asset asset in Results(data)) yield return asset;
        }

        private static void Cell(Canvas c, float x, float y, float w, float h, Action<Canvas, float, float> draw)
        {
            draw(c.View(x, y, w, h), w, h);
        }

        // ------------------------------------------------------------------------------------ shapes

        /// <summary>Round light that is gone before the slot's edge. Slots are tiled, so nothing may reach it.</summary>
        private static void Orb(Canvas c, float w, float h, Ink ink, float strength, float core)
        {
            c.Pixels(delegate(float x, float y)
            {
                float r = M.Len(x / w * 2f - 1f, y / h * 2f - 1f);
                if (r >= 1f) return default(Ink);
                float centre = M.Gauss(r, core), halo = M.Gauss(r, 0.4f);
                float a = strength * (centre * 0.75f + halo * 0.42f) * M.Smooth(1f, 0.55f, r);
                return Ink.Mix(ink, Theme.White, M.Pow(centre, 0.7f) * 0.92f).Fade(a);
            });
        }

        /// <summary>The badges' star: a glow, four points, and a diamond heart. Criticals get the diagonal points too.</summary>
        private static void Star(Canvas c, float w, float h, Ink ink, float k, bool critical)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = Math.Min(w, h) * 0.46f;
            c.Pixels((x, y) => Edge(x, y, w, h, ink.Fade(0.55f * k * M.Gauss(M.Len(x - cx, y - cy), r * 0.42f))));
            if (critical) c.Fill(Shape.Star4(cx, cy, r * 0.74f, r * 0.74f, 1.14f, 45f), Theme.Light(ink, 0.15f).Fade(0.9f * k));
            Sdf star = Shape.Star4(cx, cy, r * 0.94f, 1.1f);
            c.Fill(star, (x, y) => Ink.Mix(Theme.Light(ink, 0.75f), Theme.Light(ink, 0.1f), M.Len(x - cx, y - cy) / (r * 0.62f)).Fade(k));
            c.Fill(Shape.Diamond(cx, cy, r * 0.17f, r * 0.17f, r * 0.015f), Theme.White.Fade(k));
        }

        /// <summary>The splash's ring. Lying on the floor it is the ellipse that spreads from a hit.</summary>
        private static void Ring(Canvas c, float w, float h, Ink ink, float k, float radius, bool studs)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = Math.Min(w, h) * radius;
            Sdf ring = Shape.Circle(cx, cy, r);
            c.Pixels((x, y) => Edge(x, y, w, h, ink.Fade(0.3f * k * M.Gauss(ring(x, y), 5f))));
            c.Stroke(ring, 2.2f, Theme.Light(ink, 0.45f).Fade(0.95f * k), 0f);
            c.Stroke(Shape.Circle(cx, cy, r - 6.5f), 1f, ink.Fade(0.5f * k), 0f);
            if (!studs) return;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * M.PI * 0.5f + M.PI * 0.25f;
                float x = cx + (float)Math.Cos(angle) * r, y = cy + (float)Math.Sin(angle) * r;
                c.Fill(Shape.Diamond(x, y, 5f, 5f, 0.5f), Theme.Light(ink, 0.45f).Fade(k));
                c.Fill(Shape.Diamond(x, y, 2f, 2f, 0.2f), Theme.White.Fade(k));
            }
        }

        /// <summary>The badges' diamond as an outline, with a faint tint.</summary>
        private static void Gem(Canvas c, float w, float h, Ink ink, float k, bool solid)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = Math.Min(w, h) * 0.4f;
            Sdf gem = Shape.Diamond(cx, cy, r, r, r * 0.08f);
            c.Pixels(delegate(float x, float y)
            {
                float d = gem(x, y);
                return d <= 0f ? default(Ink) : Edge(x, y, w, h, ink.Fade((solid ? 0.36f : 0.28f) * k * M.Gauss(d, r * 0.16f)));
            });
            if (solid)
            {
                c.Fill(gem, Theme.Vertical(cy - r, Theme.Light(ink, 0.55f).Fade(k), cy + r, Theme.Light(ink, 0.1f).Fade(k)));
                c.Fill(Shape.Diamond(cx, cy, r * 0.42f, r * 0.42f, r * 0.04f), Theme.White.Fade(k));
                return;
            }

            c.Fill(gem, ink.Fade(0.09f * k));
            c.Stroke(gem, Math.Max(1.4f, r * 0.05f), Theme.Light(ink, 0.35f).Fade(0.95f * k), -1f);
        }

        /// <summary>Light rising from the judge line; the bottom of the slot is the line.</summary>
        private static void Beam(Canvas c, float w, float h, Ink ink, float k)
        {
            c.Pixels(delegate(float x, float y)
            {
                float t = y / h, profile = M.Pow(t, 2f);
                float side = M.Smooth(0f, w * 0.1f, Math.Min(x, w - x));
                return Theme.Light(ink, 0.72f * M.Pow(t, 3f)).Fade(0.92f * k * profile * M.Lerp(0.55f, 1f, side));
            });
        }

        // The frame that lights a lane's cell of the judge line. Measured in game for the first set: around the slot's
        // middle (64, 64) the cell spans 88.26 x 82.59 texels where the original frame was 82 x 81.39, so each slot's
        // own frame scales with it and its line lands on the lane edges.
        private const float FrameLine = 2.6f;

        private static void Frame(Canvas c, float sourceWidth, float sourceHeight, Ink ink, float k)
        {
            const float cellWidth = 87f * 1.0145f, cellHeight = 79.6f * 1.0375f;
            float w = sourceWidth * (cellWidth - 2.3f) / 82f, h = sourceHeight * (cellHeight - 2.3f) / 81.39129f;
            float radius = 15f * Math.Min(w, h) / 84f;
            Sdf cell = Shape.Rect(64f - w * 0.5f, 64f - h * 0.5f, w, h, radius);
            c.Pixels((x, y) => Edge(x, y, 128f, 128f, ink.Fade(0.42f * k * M.Gauss(Math.Max(0f, cell(x, y)), 6f) * (cell(x, y) > 0f ? 1f : 0.2f))));
            c.InnerGlow(cell, 9f, 0.34f * k, ink);
            c.Stroke(cell, FrameLine, Theme.Light(ink, 0.5f).Fade(k), 0f);
        }

        /// <summary>A round stroke of light, the splash's spark.</summary>
        private static void Spark(Canvas c, float w, float h, Ink ink, float k, float length, float width)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            Sdf spark = Shape.Segment(cx, cy - length * 0.5f, cx, cy + length * 0.5f, width * 0.5f);
            c.Pixels((x, y) => Edge(x, y, w, h, ink.Fade(0.4f * k * M.Gauss(Math.Max(0f, spark(x, y)), width * 0.7f))));
            c.Fill(spark, Theme.Light(ink, 0.82f).Fade(k));
        }

        /// <summary>Keeps light off a slot's border.</summary>
        private static Ink Edge(float x, float y, float w, float h, Ink ink)
        {
            float d = Math.Min(Math.Min(x, w - x), Math.Min(y, h - y));
            return ink.Fade(M.Smooth(0f, Math.Min(w, h) * 0.08f, d));
        }

        /// <summary>A soft cone of light opening from the left edge's middle.</summary>
        private static void Cone(Canvas c, float w, float h, Ink ink, float k)
        {
            c.Pixels(delegate(float x, float y)
            {
                float u = (x - w * 0.06f) / (w * 0.88f), v = Math.Abs(y / h - 0.5f) * 2f;
                if (u <= 0f || u >= 1f) return default(Ink);
                float spread = M.Lerp(0.05f, 0.82f, u);
                float body = M.Gauss(v / spread, 0.42f) * M.Pow(1f - u, 1.1f) * M.Smooth(0f, 0.06f, u);
                return Ink.Mix(Theme.Light(ink, 0.8f), ink, u).Fade(0.85f * k * body * M.Smooth(1f, 0.86f, v));
            });
        }

        /// <summary>The flick's flash: a streak that climbs out of a bright foot, with the chevrons of the flick notes.</summary>
        private static void Streak(Canvas c, float w, float h, Ink ink, float k)
        {
            float cx = w * 0.5f, foot = h * 0.82f;
            c.Pixels(delegate(float x, float y)
            {
                float t = y / foot;
                if (t <= 0.08f || y > h * 0.97f) return default(Ink);
                float below = y > foot ? M.Gauss(y - foot, h * 0.05f) : 1f;
                float rise = t < 1f ? M.Smooth(0.08f, 1f, t) : 1f;
                float width = M.Lerp(3f, 15f, M.Pow(Math.Min(1f, t), 2f));
                float a = M.Gauss(x - cx, width) * rise * below;
                return Ink.Mix(ink, Theme.White, M.Pow(a, 1.2f) * 0.9f).Fade(a * 0.95f * k);
            });
            for (int i = 0; i < 3; i++)
            {
                float y = foot - 26f - i * 30f, size = 24f - i * 4f;
                c.Fill(
                    Shape.Polyline(3.4f - i * 0.6f, cx - size, y + size * 0.55f, cx, y, cx + size, y + size * 0.55f),
                    Theme.Light(ink, 0.7f).Fade((0.9f - i * 0.26f) * k));
            }

            c.Fill(Shape.Star4(cx, foot, 30f, 20f, 1.12f, 0f), Theme.White.Fade(k));
        }

        // ------------------------------------------------------------------------------------ the atlases
        // Both sheets share one layout; the second one is the softer set.
        private static void Atlas(Canvas c, float k)
        {
            Ink tap = Theme.Tap, hold = Theme.Hold, flick = Theme.Flick, gold = Theme.Critical, white = Theme.White;

            // Small particles, 64 x 64.
            Cell(c, 0, 0, 64, 64, (q, w, h) => Gem(q, w, h, hold, 1f, true));                       // hold sparkles
            Cell(c, 64, 0, 64, 64, (q, w, h) => Gem(q, w, h, Theme.TextSecondary, 1f, true));       // white sparkles of every kind
            Cell(c, 128, 0, 64, 64, (q, w, h) => Music(q));
            Cell(c, 192, 0, 64, 64, (q, w, h) => Orb(q, w, h, white, 0.95f * k, 0.3f));             // a hit's white core
            Cell(c, 0, 64, 64, 64, (q, w, h) => Gem(q, w, h, gold, 1f, false));
            Cell(c, 64, 64, 64, 64, (q, w, h) => Twinkle(q, w, h, white, false));                   // trace and hold stars
            Cell(c, 128, 64, 64, 64, (q, w, h) => Twinkle(q, w, h, Theme.Light(gold, 0.55f), true)); // critical stars
            Cell(c, 192, 64, 64, 64, (q, w, h) => Twinkle(q, w, h, hold, false));
            Cell(c, 0, 128, 64, 64, (q, w, h) => Gem(q, w, h, hold, 1f, false));                    // trace sparkles
            Cell(c, 64, 128, 64, 64, (q, w, h) => Gem(q, w, h, gold, 1f, false));                   // critical hold and trace sparkles
            Cell(c, 128, 128, 64, 64, (q, w, h) => Gem(q, w, h, Theme.Light(flick, 0.5f), 1f, true)); // flick sparkles, both kinds
            Cell(c, 192, 128, 64, 64, (q, w, h) => Orb(q, w, h, Theme.Light(Theme.DefaultAccent, 0.25f), 0.85f * k, 0.16f));

            // The scatter of a critical picks one of these four at random: the award ceremony's confetti.
            Cell(c, 0, 192, 64, 64, (q, w, h) => Confetti(q, w, h, Theme.Light(gold, 0.3f), 0));
            Cell(c, 64, 192, 64, 64, (q, w, h) => Confetti(q, w, h, white, 1));
            Cell(c, 128, 192, 64, 64, (q, w, h) => Confetti(q, w, h, Theme.Light(Theme.Rose, 0.3f), 2));
            Cell(c, 192, 192, 64, 64, (q, w, h) => Confetti(q, w, h, Theme.DefaultAccent, 3));

            Cell(c, 256, 0, 64, 64, (q, w, h) => Orb(q, w, h, flick, k, 0.2f));                     // flick droplets
            Cell(c, 320, 0, 64, 64, (q, w, h) => Orb(q, w, h, gold, k, 0.2f));                      // critical flick droplets
            Cell(c, 384, 0, 64, 64, (q, w, h) => Orb(q, w, h, white, 0.9f * k, 0.28f));
            Cell(c, 256, 64, 64, 64, (q, w, h) => Comet(q, w, h, Theme.Light(hold, 0.55f), k));     // sparks thrown from a held line
            Cell(c, 256, 128, 128, 128, (q, w, h) => Rail(q, w, h, hold, k));
            Cell(c, 384, 128, 128, 128, (q, w, h) => TraceMark(q, w, h, k));                        // the mark of a trace

            // Columns of light over a lane, 32 x 128 and 64 x 128.
            Cell(c, 0, 256, 32, 128, (q, w, h) => Beam(q, w, h, white, k));
            Cell(c, 32, 256, 32, 128, (q, w, h) => Beam(q, w, h, hold, k));                         // hold
            Cell(c, 64, 256, 32, 128, (q, w, h) => Beam(q, w, h, white, k));
            Cell(c, 96, 256, 32, 128, (q, w, h) => Beam(q, w, h, gold, k));                         // critical hold
            Cell(c, 256, 256, 64, 128, (q, w, h) => Beam(q, w, h, white, k));                       // a tapped lane
            Cell(c, 320, 256, 64, 128, (q, w, h) => Beam(q, w, h, white, 0.72f * k));
            Cell(c, 256, 384, 64, 128, (q, w, h) => Beam(q, w, h, gold, k));                        // a critical's lane
            Cell(c, 320, 384, 64, 128, (q, w, h) => Beam(q, w, h, flick, k));
            Cell(c, 384, 640, 64, 128, (q, w, h) => Beam(q, w, h, hold, k));
            Cell(c, 448, 640, 64, 128, (q, w, h) => Beam(q, w, h, hold, 0.72f * k));
            Cell(c, 384, 896, 64, 128, (q, w, h) => Beam(q, w, h, gold, k));
            Cell(c, 448, 896, 64, 128, (q, w, h) => Beam(q, w, h, gold, 0.72f * k));
            Cell(c, 704, 640, 64, 128, (q, w, h) => Beam(q, w, h, flick, 0.8f * k));                // flick
            Cell(c, 704, 896, 64, 128, (q, w, h) => Beam(q, w, h, gold, 0.8f * k));                 // critical flick and hold
            Cell(c, 896, 128, 64, 128, (q, w, h) => Beam(q, w, h, tap, k));                         // tap
            Cell(c, 960, 128, 64, 128, (q, w, h) => Beam(q, w, h, tap, 0.72f * k));                 // tap, GREAT and GOOD
            Cell(c, 896, 384, 64, 128, (q, w, h) => Beam(q, w, h, gold, k));
            Cell(c, 960, 384, 64, 128, (q, w, h) => Beam(q, w, h, gold, 0.72f * k));                // critical tap

            // The judge line's cells.
            Cell(c, 128, 256, 128, 128, (q, w, h) => Frame(q, 82f, 81.39f, tap, k));
            Cell(c, 384, 256, 128, 128, (q, w, h) => Frame(q, 81.5f, 81f, flick, k));
            Cell(c, 128, 384, 128, 128, (q, w, h) => Frame(q, 81.56f, 81f, gold, k));
            Cell(c, 384, 384, 128, 128, (q, w, h) => Frame(q, 81.62f, 81.73f, hold, k));

            // Three smaller rings, 39 texels round: hold and critical hold ticks, and a critical's second ripple.
            Cell(c, 256, 640, 128, 128, (q, w, h) => Ring(q, w, h, hold, k, 0.305f, false));
            Cell(c, 128, 896, 128, 128, (q, w, h) => Ring(q, w, h, gold, k, 0.305f, false));
            Cell(c, 256, 896, 128, 128, (q, w, h) => Ring(q, w, h, tap, k, 0.305f, false));

            Cell(c, 0, 384, 128, 128, (q, w, h) => Orbit(q, w, h, gold, k));                        // the circle around a critical

            // A hit on the floor, one 2 x 2 block per kind: star, ring, ring with diamonds, plain ripple.
            // The last one is also the small squares that float up, so it has no corners to turn.
            Burst(c, 0, 512, hold, k, false);
            Burst(c, 0, 768, gold, k, true);
            Burst(c, 512, 0, tap, k, false);
            Burst(c, 512, 256, gold, k, true);
            // The critical hold's fourth slot is one of the small rings above; the flicks have only their left column.
            Cell(c, 512, 512, 128, 128, (q, w, h) => Star(q, w, h, flick, k, false));
            Cell(c, 512, 640, 128, 128, (q, w, h) => Ring(q, w, h, flick, k, 0.4f, true));
            Cell(c, 512, 768, 128, 128, (q, w, h) => Star(q, w, h, gold, k, true));
            Cell(c, 512, 896, 128, 128, (q, w, h) => Ring(q, w, h, gold, k, 0.4f, true));

            // Soft light.
            Cell(c, 256, 512, 128, 128, (q, w, h) => Pulse(q, w, h, hold, k));
            Cell(c, 256, 768, 128, 128, (q, w, h) => Pulse(q, w, h, gold, k));
            Cell(c, 384, 512, 128, 128, (q, w, h) => Orb(q, w, h, hold, 0.8f * k, 0.22f));
            Cell(c, 384, 768, 128, 128, (q, w, h) => Orb(q, w, h, gold, 0.8f * k, 0.22f));
            Cell(c, 768, 512, 128, 128, (q, w, h) => Orb(q, w, h, flick, 0.8f * k, 0.22f));
            Cell(c, 768, 768, 128, 128, (q, w, h) => Orb(q, w, h, gold, 0.8f * k, 0.22f));
            Cell(c, 896, 0, 128, 128, (q, w, h) => Orb(q, w, h, Ink.Mix(tap, Theme.Violet, 0.4f), 0.85f * k, 0.22f));
            Cell(c, 896, 256, 128, 128, (q, w, h) => Orb(q, w, h, gold, 0.85f * k, 0.22f));

            // Eight frames of a flick's spark, long to short, in two rows of four.
            for (int group = 0; group < 2; group++)
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 4; i++)
                    {
                        int frame = row * 4 + i;
                        Ink ink = group == 0 ? flick : gold;
                        Cell(c, 640 + i * 32, 512 + group * 256 + row * 64, 32, 64, (q, w, h) =>
                            Spark(q, w, h, ink, (1f - frame * 0.09f) * k, 34f - frame * 3.6f, 7.5f - frame * 0.4f));
                    }

            Cell(c, 640, 640, 64, 128, (q, w, h) => Cone(q, w, h, flick, k));
            Cell(c, 640, 896, 64, 128, (q, w, h) => Cone(q, w, h, gold, k));
            Cell(c, 896, 512, 128, 256, (q, w, h) => Streak(q, w, h, flick, k));
            Cell(c, 896, 768, 128, 256, (q, w, h) => Streak(q, w, h, gold, k));
        }

        private static void Burst(Canvas c, float x, float y, Ink ink, float k, bool critical)
        {
            Cell(c, x, y, 128, 128, (q, w, h) => Star(q, w, h, ink, k, critical));
            Cell(c, x + 128f, y, 128, 128, (q, w, h) => Ring(q, w, h, ink, k, 0.42f, false));
            Cell(c, x, y + 128f, 128, 128, (q, w, h) => Ring(q, w, h, ink, k, 0.4f, true));
            // The critical hold's fourth slot at (128, 896) is a small ring, drawn with the others.
            if (x == 0f && y == 768f) return;
            Cell(c, x + 128f, y + 128f, 128, 128, (q, w, h) => Ripple(q, w, h, ink, k));
        }

        /// <summary>The thinnest ring, with a little light gathered inside its edge.</summary>
        private static void Ripple(Canvas c, float w, float h, Ink ink, float k)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.4f;
            c.Pixels(delegate(float x, float y)
            {
                float d = M.Len(x - cx, y - cy);
                float inside = d < r ? 0.16f * M.Pow(d / r, 5f) : 0f;
                return Edge(x, y, w, h, ink.Fade((inside + 0.2f * M.Gauss(d - r, 4f)) * k));
            });
            c.Stroke(Shape.Circle(cx, cy, r), 1.8f, Theme.Light(ink, 0.4f).Fade(0.85f * k), 0f);
        }

        private static void Twinkle(Canvas c, float w, float h, Ink ink, bool diagonal)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            c.Pixels((x, y) => Edge(x, y, w, h, ink.Fade(0.4f * M.Gauss(M.Len(x - cx, y - cy), w * 0.13f))));
            if (diagonal) c.Fill(Shape.Star4(cx, cy, 13f, 13f, 1.1f, 45f), ink.Fade(0.75f));
            c.Fill(Shape.Star4(cx, cy, 25f, 1.12f), Theme.Light(ink, 0.5f));
            c.Fill(Shape.Diamond(cx, cy, 4f, 4f, 0.3f), Theme.White);
        }

        private static void Confetti(Canvas c, float w, float h, Ink ink, int kind)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            Sdf piece = kind == 0 ? Shape.Diamond(cx, cy, 17f, 17f, 2f)
                : kind == 1 ? Shape.Segment(cx, cy - 13f, cx, cy + 13f, 6f)
                : kind == 2 ? Shape.Rect(cx - 12f, cy - 12f, 24f, 24f, 6f) : Shape.Circle(cx, cy, 11f);
            c.Pixels((x, y) => Edge(x, y, w, h, ink.Fade(0.34f * M.Gauss(Math.Max(0f, piece(x, y)), 5f))));
            c.Fill(piece, Theme.Light(ink, 0.2f));
        }

        private static void Comet(Canvas c, float w, float h, Ink ink, float k)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            c.Pixels(delegate(float x, float y)
            {
                float t = (x - w * 0.1f) / (w * 0.78f);
                if (t <= 0f || t >= 1.08f) return default(Ink);
                float width = M.Lerp(1.4f, 6f, M.Pow(Math.Min(1f, t), 1.6f));
                float a = M.Gauss(y - cy, width * 0.6f) * M.Pow(Math.Min(1f, t), 1.5f) * M.Smooth(1.08f, 0.96f, t);
                return Ink.Mix(ink, Theme.White, t * 0.7f).Fade(a * k);
            });
            c.Fill(Shape.Circle(w * 0.8f, cy, 3.6f), Theme.White.Fade(k));
        }

        private static void Rail(Canvas c, float w, float h, Ink ink, float k)
        {
            float cx = w * 0.5f;
            c.Pixels(delegate(float x, float y)
            {
                float v = M.Gauss(x - cx, w * 0.07f) * M.Pow((float)Math.Sin(y / h * Math.PI), 0.6f);
                return Ink.Mix(ink, Theme.White, M.Pow(v, 0.8f)).Fade(0.95f * v * k);
            });
        }

        private static void Orbit(Canvas c, float w, float h, Ink ink, float k)
        {
            // The brand mark's orbit: a ring open at the bottom, with its satellite.
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.41f;
            Sdf ring = Shape.Circle(cx, cy, r);
            c.Pixels(delegate(float x, float y)
            {
                float angle = Math.Abs((float)Math.Atan2(x - cx, cy - y)) / M.PI;
                float open = M.Smooth(0.74f, 0.6f, angle);
                float line = c.Line(ring(x, y), 2.4f), glow = 0.32f * M.Gauss(ring(x, y), 5f);
                return Edge(x, y, w, h, Ink.Mix(ink, Theme.Light(ink, 0.5f), line).Fade(Math.Max(line * 0.95f, glow) * open * k));
            });
            c.Fill(Shape.Circle(cx + r * 0.7071f, cy - r * 0.7071f, 5f), Theme.White.Fade(k));
            c.Fill(Shape.Diamond(cx - r * 0.7071f, cy - r * 0.7071f, 4f, 4f, 0.4f), Theme.Light(ink, 0.5f).Fade(k));
        }

        private static void Pulse(Canvas c, float w, float h, Ink ink, float k)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.4f;
            c.Pixels(delegate(float x, float y)
            {
                float d = M.Len(x - cx, y - cy);
                float fill = d < r ? 0.2f * M.Pow(d / r, 3f) : 0f;
                return Edge(x, y, w, h, ink.Fade((fill + 0.34f * M.Gauss(d - r, 5f)) * k));
            });
            c.Stroke(Shape.Circle(cx, cy, r), 2.4f, Theme.Light(ink, 0.5f).Fade(0.9f * k), 0f);
        }

        // The game turns this one, so it has nothing but a ring and the star.
        private static void TraceMark(Canvas c, float w, float h, float k)
        {
            Ink ink = Theme.Light(Theme.Violet, 0.45f);
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.38f;
            Sdf ring = Shape.Circle(cx, cy, r);
            c.Pixels((x, y) => Edge(x, y, w, h, Theme.Violet.Fade(0.3f * k * M.Gauss(ring(x, y), 5f))));
            c.Stroke(ring, 1.8f, ink.Fade(0.9f * k), 0f);
            c.Stroke(Shape.Circle(cx, cy, r * 0.82f), 1f, ink.Fade(0.4f * k), 0f);
            c.Fill(Shape.Star4(cx, cy, r * 0.6f, 1.1f), Theme.White.Fade(0.9f * k));
        }

        private static void Music(Canvas c)
        {
            Ink ink = Theme.TextPrimary;
            c.Fill(Shape.Polyline(2.4f, 23f, 45f, 23f, 18f, 45f, 13f, 45f, 40f), ink);
            c.Fill(Shape.Circle(17.5f, 45f, 7.2f), ink);
            c.Fill(Shape.Circle(39.5f, 40f, 7.2f), ink);
        }

        private static void LaneSheet(Canvas c)
        {
            Cell(c, 0, 0, 128, 128, (q, w, h) => Frame(q, 82f, 81.39f, Theme.Tap, 1f));
            Cell(c, 128, 0, 128, 128, (q, w, h) => Frame(q, 81.5f, 81f, Theme.Flick, 1f));
            Cell(c, 0, 128, 128, 128, (q, w, h) => Frame(q, 81.56f, 81f, Theme.Critical, 1f));
            Cell(c, 128, 128, 128, 128, (q, w, h) => Frame(q, 81.62f, 81.73f, Theme.Hold, 1f));
            Cell(c, 256, 0, 64, 128, (q, w, h) => Beam(q, w, h, Theme.White, 1f));
            Cell(c, 320, 0, 64, 128, (q, w, h) => Beam(q, w, h, Theme.White, 0.72f));
            Cell(c, 256, 128, 64, 128, (q, w, h) => Beam(q, w, h, Theme.Critical, 1f));
            Cell(c, 320, 128, 64, 128, (q, w, h) => Beam(q, w, h, Theme.Flick, 1f));
        }

        // A star's trail: a streak through the middle row with its head at the right.
        private static void Tail(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                float t = (x - 3f) / 46f;
                if (t <= 0f || t >= 1.14f) return default(Ink);
                float a = M.Gauss(y - 31.5f, M.Lerp(0.8f, 2.6f, t)) * M.Pow(Math.Min(1f, t), 1.4f) * M.Smooth(1.14f, 0.98f, t);
                return Ink.Mix(Theme.Light(Theme.Periwinkle, 0.6f), Theme.White, t).Fade(a);
            });
            c.Fill(Shape.Star4(44f, 31.5f, 9f, 5f, 1.1f, 0f), Theme.White);
        }

        // One quarter of the damage vignette: it meets the other three at the clear middle of the screen.
        private static void Damage(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                float u = 1f - x / 256f, v = 1f - y / 256f;
                return Theme.Danger.Fade(0.56f * M.Smooth(0.24f, 1.76f, M.Len(u, v)));
            });
        }
    }
}
