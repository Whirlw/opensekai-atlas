using System;
using System.Collections.Generic;

// Page grounds: the interface's dark ground with its lights, never a picture.
namespace ArtV2
{
    public static class Backgrounds
    {
        // JacketPalette's glow for a page without a cover, and the brand's colours at the same depth.
        private static readonly Ink DefaultGlow = Ink.Rgb(37, 52, 92);
        private static readonly Ink VioletGlow = Ink.Rgb(62, 46, 108);
        private static readonly Ink RoseGlow = Ink.Rgb(92, 42, 78);

        public static IEnumerable<Asset> All(string data)
        {
            yield return new Asset("default.png", 2048, 2048, 1, true, Stage);
            yield return new Asset("bg_sekai.png", 2048, 1170, 1, true, Loading);
            yield return new Asset("eff_bgcommon_circle_triangle.png", 224, 224, 4, false, Particles);
            yield return new Asset("ui_scorerank_plate.png", 500, 684, 2, false, Plate);
        }

        /// <summary>The interface's round glow: full in the middle, nothing at its rim.</summary>
        private static float Light(float x, float y, float cx, float cy, float rx, float ry)
        {
            float r = M.Len((x - cx) / rx, (y - cy) / ry);
            if (r >= 1f) return 0f;
            float t = 1f - r;
            return t * t * (3f - 2f * t);
        }

        // The stage behind a live. Wide screens show roughly rows 560..1490 of it, so the light sits there: violet in
        // the upper right, the page glow at the left, rose low on the right, and a quiet middle for the lane.
        private static void Stage(Canvas c)
        {
            c.Pixels(delegate(float x, float y)
            {
                Ink ink = Theme.Ground;
                ink = Ink.Mix(ink, VioletGlow, 0.62f * Light(x, y, 1690f, 700f, 980f, 820f));
                ink = Ink.Mix(ink, DefaultGlow, 0.58f * Light(x, y, 250f, 980f, 900f, 780f));
                ink = Ink.Mix(ink, RoseGlow, 0.3f * Light(x, y, 1560f, 1380f, 760f, 560f));
                ink = Ink.Mix(ink, Theme.Periwinkle, 0.045f * Light(x, y, 1024f, 1500f, 1200f, 420f));
                return ink;
            });

            // The brand mark's orbit, three times, barely there: lines that fade along their length, with satellites.
            Orbit(c, 1024f, 1240f, 1180f, 250f, -5f, Theme.Periwinkle, 0.16f, 0.12f);
            Orbit(c, 1024f, 1240f, 1400f, 330f, -5f, Theme.Violet, 0.1f, 0.62f);
            Orbit(c, 1660f, 690f, 360f, 300f, 24f, Theme.Violet, 0.13f, 0.3f);

            // Rising points of light, fewer toward the lane. A few are the badges' small diamonds.
            for (int i = 0; i < 190; i++)
            {
                float x = M.Hash(i, 17) * 2048f, y = M.Hash(i, 91) * 2048f;
                float lane = M.Smooth(0.18f, 0.5f, Math.Abs(x - 1024f) / 2048f);
                if (M.Hash(i, 5) > 0.25f + 0.75f * lane) continue;
                float tone = M.Hash(i, 43), strength = (0.1f + 0.34f * M.Pow(M.Hash(i, 29), 2f)) * (0.35f + 0.65f * lane);
                Ink ink = Theme.Light(tone < 0.5f ? Theme.Periwinkle : tone < 0.82f ? Theme.Violet : Theme.Rose, 0.35f);
                Canvas spot = c.View(x - 16f, y - 16f, 32f, 32f);
                float radius = 1f + 1.5f * M.Hash(i, 71);
                if (i % 17 == 0)
                {
                    spot.Glow(Shape.Diamond(16f, 16f, 4f, 4f, 0.4f), 4f, 0.3f * strength, 1f, ink);
                    spot.Fill(Shape.Diamond(16f, 16f, 4f, 4f, 0.4f), ink.Fade(Math.Min(1f, strength * 1.5f)));
                }
                else
                {
                    spot.Glow(Shape.Circle(16f, 16f, radius), 3f, 0.3f * strength, 1f, ink);
                    spot.Fill(Shape.Circle(16f, 16f, radius), ink.Fade(strength));
                }
            }
        }

        private static void Orbit(Canvas c, float cx, float cy, float rx, float ry, float degrees, Ink ink, float strength, float satellite)
        {
            Sdf line = Shape.Ellipse(cx, cy, rx, ry, degrees);
            float cos = (float)Math.Cos(degrees * M.PI / 180f), sin = (float)Math.Sin(degrees * M.PI / 180f);
            float at = satellite * M.PI * 2f;
            c.Pixels(delegate(float x, float y)
            {
                float cover = c.Line(line(x, y), 1.6f);
                if (cover <= 0f) return default(Ink);
                float dx = x - cx, dy = y - cy;
                float angle = (float)Math.Atan2((dy * cos - dx * sin) / ry, (dx * cos + dy * sin) / rx);
                float along = 0.35f + 0.65f * M.Pow(0.5f + 0.5f * (float)Math.Cos(angle - at), 2f);
                return ink.Fade(cover * strength * along);
            });
            float u = (float)Math.Cos(at) * rx, v = (float)Math.Sin(at) * ry;
            float sx = cx + u * cos - v * sin, sy = cy + u * sin + v * cos;
            Canvas spot = c.View(sx - 40f, sy - 40f, 80f, 80f);
            spot.Glow(Shape.Circle(40f, 40f, 3.5f), 9f, strength * 1.9f, 1f, ink);
            spot.Fill(Shape.Circle(40f, 40f, 3.5f), Theme.Light(ink, 0.6f).Fade(Math.Min(1f, strength * 5f)));
        }

        // A page without a cover, straight from the style guide: the main glow 1900 across with its middle 380 x 340
        // inside the upper right corner at 62%, and the accent along the bottom edge at 9%.
        private static void Loading(Canvas c)
        {
            const float unit = 2048f / 1920f;
            c.Pixels(delegate(float x, float y)
            {
                Ink ink = Ink.Mix(Theme.Ground, DefaultGlow, 0.62f * Light(x, y, 2048f - 380f * unit, 340f * unit, 950f * unit, 950f * unit));
                return Ink.Mix(ink, Theme.DefaultAccent, 0.09f * Light(x, y, 640f * unit, 1170f, 580f * unit, 580f * unit));
            });
        }

        // Four floating shapes, 112 px each: a light ring, a rose ring, a rose diamond and a teal diamond.
        private static void Particles(Canvas c)
        {
            for (int i = 0; i < 2; i++)
            {
                Ink ink = i == 0 ? Theme.TextPrimary : Theme.Rose;
                Canvas cell = c.View(i * 112f, 0f, 112f, 112f);
                cell.Stroke(Shape.Circle(56f, 56f, 50f), 2.2f, ink, 0f);
                cell.Stroke(Shape.Circle(56f, 56f, 43.5f), 1f, ink.Fade(0.45f), 0f);
                for (int k = 0; k < 4; k++)
                {
                    float angle = k * M.PI * 0.5f + M.PI * 0.25f;
                    cell.Fill(Shape.Diamond(56f + (float)Math.Cos(angle) * 50f, 56f + (float)Math.Sin(angle) * 50f, 4.5f, 4.5f, 0.4f), ink);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                Ink accent = i == 0 ? Theme.Rose : Theme.Teal;
                Canvas cell = c.View(i * 112f, 112f, 112f, 112f);
                Sdf plate = Shape.Diamond(56f, 56f, 46f, 46f, 5f);
                cell.Glow(plate, 4.5f, 0.36f, 1f, accent);
                cell.Fill(plate, Theme.Vertical(10f, Ink.Mix(Theme.Surface, accent, 0.5f), 102f, Ink.Mix(Theme.Surface, accent, 0.28f)));
                cell.Stroke(plate, 5f, Theme.Light(accent, 0.15f), -1f);
                cell.Fill(Shape.Star4(56f, 56f, 25f, 1.1f), Theme.Light(accent, 0.6f));
                cell.Fill(Shape.Diamond(56f, 56f, 6f, 6f, 0.5f), Theme.White);
                cell.Mask((x, y) => M.Smooth(0f, 8f, Math.Min(Math.Min(x, 112f - x), Math.Min(y, 112f - y))));
            }
        }

        // The top of the card meets the screen at row 51.9. Keep its other three margins transparent.
        private static void Plate(Canvas c)
        {
            const float inset = 24f, left = inset + 1f, top = 51.6f, right = 499f - inset, bottom = 683f - inset;
            Sdf card = Shape.Rect(left, top, right - left, bottom - top, 80f);
            c.Fill(card, Theme.HudCard(top, bottom - top));
            c.Stroke(card, 11f, Theme.HudEdge(top, bottom - top), -1f);
            c.Fill(Shape.Segment(106f, 513f, 394f, 513f, 2.5f), Theme.HudLine);
            c.Fill(Shape.Diamond(250f, 513f, 17f, 17f, 2f), Theme.White);
        }
    }
}
