using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

// The live HUD, the lane and the jacket stage behind it. Sizes, pivots and borders come from data/hud-sizes.tsv;
// lane and jacket coordinates are the first set's, written down next to the code that uses them.
namespace ArtV2
{
    public static class Hud
    {
        private sealed class Slot
        {
            public string Name;
            public int W, H;
            public float Ppu, PivotX, PivotY;
        }

        // Functional masks and one-colour squares the engine uses for their shape alone. They are copied, not drawn.
        private static readonly string[] Kept =
        {
            "16_9_white.png", "bg_base_wh.png", "bg_base_ingame_wh.png", "white6x6.png", "mask_line.png"
        };

        private static Dictionary<string, Slot> _slots;
        private static string _data;

        public static IEnumerable<Asset> All(string data)
        {
            _data = data;
            _slots = LoadSlots(Path.Combine(data, "hud-sizes.tsv"));
            foreach (Slot slot in _slots.Values)
            {
                Slot s = slot;
                if (Array.IndexOf(Kept, s.Name) >= 0)
                {
                    yield return Asset.Copy(s.Name, s.W, s.H, Path.Combine(data, "kept", s.Name));
                    continue;
                }

                bool large = Math.Max(s.W, s.H) > 1024;
                bool dither = s.Name.StartsWith("lane_base") || s.Name.StartsWith("mask_2dmode_floor") || s.Name.StartsWith("bg_2dmode")
                    || s.Name.StartsWith("tex_2dmode") || s.Name.StartsWith("tex_lifeZero") || s.Name.StartsWith("tex_transition");
                int scale = s.Name.StartsWith("lane_") || s.Name.StartsWith("mask_2dmode_floor") ? 1 : large ? 2 : 4;
                yield return new Asset(s.Name, s.W, s.H, scale, dither, c => Draw(c, s));
            }
        }

        private static Dictionary<string, Slot> LoadSlots(string file)
        {
            var result = new Dictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(file))
            {
                string[] cells = line.Split('\t');
                if (cells.Length < 10 || cells[0] == "file") continue;
                result[cells[0]] = new Slot
                {
                    Name = cells[0],
                    W = int.Parse(cells[1], CultureInfo.InvariantCulture),
                    H = int.Parse(cells[2], CultureInfo.InvariantCulture),
                    Ppu = float.Parse(cells[7], CultureInfo.InvariantCulture),
                    PivotX = float.Parse(cells[8], CultureInfo.InvariantCulture),
                    PivotY = float.Parse(cells[9], CultureInfo.InvariantCulture)
                };
            }

            return result;
        }

        private static void Draw(Canvas c, Slot s)
        {
            string n = s.Name.Substring(0, s.Name.Length - 4);
            if (n.StartsWith("judge_timing_")) Timing(c, s, n.Substring(13).ToUpperInvariant());
            else if (n.StartsWith("judge_")) Judgement(c, s, n.Substring(6).ToUpperInvariant());
            else if (n.StartsWith("number_48px_shadow_")) ScoreDigit(c, s, n.Substring(19), true);
            else if (n.StartsWith("number_48px_")) ScoreDigit(c, s, n.Substring(12, 1), false);
            else if (n.StartsWith("number_combo_ap_")) ComboDigit(c, s, n.Substring(22, 1), true);
            else if (n.StartsWith("number_combo_")) ComboDigit(c, s, n.Substring(19, 1), false);
            else if (n == "eff_combo") ComboGlow(c, s, null);
            else if (n.StartsWith("eff_")) ComboGlow(c, s, n.Substring(4, 1));
            else if (n.StartsWith("txt_combo")) ComboWord(c, s, n.Contains("_ap_"));
            else if (n.StartsWith("ui_rank_word_")) RankWord(c, s);
            else if (n.StartsWith("score_")) RankLetter(c, s, n.Substring(6));
            else if (n.Length == 1) Marker(c, s, n);
            else if (n.StartsWith("text_")) Plus(c, s, n.Contains("outline"));
            else if (n.StartsWith("tex_live_countdown_")) Countdown(c, s, n.Substring(19));
            else if (n == "label_autolive") AutoLabel(c, s);
            else if (n.StartsWith("btn_")) Button(c, n);
            else if (n == "bg_lifeGauge" || n == "bg_scoreGauge") Gauge(c, s, n == "bg_lifeGauge");
            else if (n.StartsWith("gauge_base")) c.Fill(Shape.Pill(0.5f, 0.5f, s.W - 1f, s.H - 1f), Theme.HudWell.Fade(0.82f));
            else if (n.StartsWith("bar_lifeGauge")) Bar(c, s, n.EndsWith("rd") ? Theme.Danger : Theme.Success);
            else if (n == "gauge_bar_ingame_freeLive_s") ScoreBar(c, s);
            else if (n.StartsWith("effect_")) GaugeGlow(c, s, n.Contains("score") ? Theme.Gold : Theme.Success);
            else if (n == "icon_heart_gn") c.Fill(Shape.Grow(Shape.Heart(3f, 4f, s.W - 6f, s.H - 8f), 1f), Theme.Success);
            else if (n == "meter_mark") MeterMark(c, s);
            else if (n == "lane_base") LaneBase(c, s);
            else if (n == "lane_line") LaneLines(c, s);
            else if (n == "lane_mask") LaneMask(c, s);
            else if (n == "judgeLine") JudgeLine(c);
            else if (n == "mask_2dmode_floor_grd") Floor(c, s);
            else if (n.StartsWith("mask_2dmode_jacket")) c.Fill(JacketMask(n), Theme.White);
            else if (n.StartsWith("tex_2dmode_jacket")) JacketVeil(c, s, n);
            else if (n.StartsWith("bg_2dmode_window")) JacketWindow(c, s, n.Contains("mirror"));
            else if (n == "tex_lifeZero") LifeZero(c, s);
            else if (n == "grd_append") c.Pixels(Theme.Horizontal(0f, s.W, Theme.Violet, Theme.Append));
            else if (n == "tex_transition_01") Transition(c, s);
            else if (n == "tex_transition_03") Swirl(c, s);
            else throw new Exception("No drawing for " + s.Name);
        }

        private static RectangleF Whole(Slot s) { return new RectangleF(0f, 0f, s.W, s.H); }

        /// <summary>The soft dark seat that keeps lettering readable over a jacket or a video.</summary>
        private static void Seat(Canvas c, Sdf shape, float sigma, float peak, float drop)
        {
            c.Glow(Shape.Move(shape, 0f, drop), sigma, peak, 1f, Theme.Ground);
        }

        // ------------------------------------------------------------------------------------ lettering

        private static void Judgement(Canvas c, Slot s, string word)
        {
            bool prism = word == "PERFECT";
            Ink accent = prism ? Theme.PrismGlow
                : word == "GREAT" ? Theme.Great
                : word == "GOOD" ? Theme.Good
                : word == "BAD" ? Theme.Bad
                : word == "MISS" ? Theme.TextMuted : Theme.DefaultAccent;
            const float cap = 52f;
            float tracking = Fit(word, cap, s.W - 22f, 7f);
            using (GraphicsPath path = Type.Centered(word, cap, tracking, s.PivotX, s.PivotY))
            {
                var text = new Lettering(path, Whole(s), 8, 0f);
                Seat(c, text.Field, 4.5f, 0.6f, 1.5f);
                c.Glow(text.Field, 5f, prism ? 0.22f : 0.14f, 1f, accent);
                float top = s.PivotY - cap * 0.5f;
                Paint face = prism ? PrismRun(top, cap) : Theme.Vertical(top, Theme.Light(accent, 0.42f), top + cap, accent);
                text.Fill(c, (x, y) => Theme.Light(face(x, y), 0.26f));
            }
        }

        // The run down the ALL PERFECT combo digits, which PERFECT wears too.
        private static Paint PrismRun(float top, float cap)
        {
            float inset = cap * 4f / ComboCap;
            return Theme.VerticalStops(top + inset, top + cap - inset, Theme.Prism);
        }

        /// <summary>The widest tracking up to <paramref name="most"/> that keeps a word within a width.</summary>
        private static float Fit(string word, float cap, float width, float most)
        {
            float tight = Type.Width(word, cap, 0f);
            return Math.Min(most, (width - tight) / Math.Max(1, word.Length - 1));
        }

        private static void Timing(Canvas c, Slot s, string word)
        {
            Ink accent = word == "FAST" ? Theme.Good : word == "LATE" ? Theme.Great : Theme.Gold;
            const float top = 10f, height = 36f, cap = 20f;
            float middle = top + height * 0.5f;
            Sdf pill = Shape.Pill(3f, top, s.W - 6f, height);
            Chip(c, pill, 0.32f, accent);
            Ink ink = Theme.Light(accent, 0.60f);
            using (GraphicsPath path = Type.Centered(word, cap, 5f, s.W * 0.5f + 10f, middle))
                new Lettering(path, Whole(s), 8, 0f).Fill(c, ink);
            float x = s.W * 0.5f - Type.Width(word, cap, 5f) * 0.5f - 8f;
            c.Fill(Shape.Diamond(x, middle, 5.5f, 5.5f, 0.6f), ink);
        }

        // Digits of one family share a size and a baseline, and sit on the sprite's pivot, which is the middle of the
        // cell the game puts them in. The stretch fills those cells the way the interface sets its big numbers;
        // "thin" takes back the weight it adds to the upright strokes.
        private static Lettering Digit(Slot s, string digit, float cap, float wide, float thin, float offsetY)
        {
            float baseline = s.PivotY + offsetY + cap * 0.5f;
            using (GraphicsPath path = Type.Line(digit, cap, 0f, wide, s.PivotX, 0f, baseline))
                return new Lettering(path, Whole(s), 8, thin);
        }

        private static void ScoreDigit(Canvas c, Slot s, string digit, bool shadow)
        {
            Lettering text = Digit(s, digit, 38f, 1.17f, 0.35f, 0f);
            if (!shadow)
            {
                text.Fill(c, Theme.White);
                return;
            }

            c.Glow(text.Field, 3.4f, 0.72f, 1f, Theme.Ground);
        }

        private const float ComboCap = 126f, ComboWide = 1.15f, ComboThin = 1.1f, ComboY = 1f;

        private static void ComboDigit(Canvas c, Slot s, string digit, bool prism)
        {
            Lettering text = Digit(s, digit, ComboCap, ComboWide, ComboThin, ComboY);
            float top = s.PivotY + ComboY - ComboCap * 0.5f, bottom = top + ComboCap;
            Seat(c, text.Field, 3.6f, 0.55f, 1.5f);
            c.Glow(text.Field, 4.5f, prism ? 0.22f : 0.14f, 1f, prism ? Theme.PrismGlow : Theme.Violet);
            if (prism) text.Fill(c, PrismRun(top, ComboCap));
            else text.Fill(c, Theme.Vertical(top, Theme.White, bottom, Ink.Mix(Theme.TextPrimary, Theme.Violet, 0.16f)));
        }

        // Light behind a digit or the word, flashed over it on ALL PERFECT. It is nearly white with the prism's violet in
        // it: plain white at low strength reads as grey on a dark page.
        private static void ComboGlow(Canvas c, Slot s, string digit)
        {
            Lettering text;
            if (digit != null) text = Digit(s, digit, ComboCap, ComboWide, ComboThin, ComboY);
            else
                using (GraphicsPath path = Type.Centered("COMBO", 30f, 7f, s.PivotX, s.PivotY))
                    text = new Lettering(path, Whole(s), 8, 0f);
            float sigma = digit != null ? 9f : 5f;
            c.Glow(text.Field, sigma, 0.3f, 1f, Theme.Light(Theme.PrismGlow, 0.3f));
            // Fade out before the sprite's edge, so the light never shows its rectangle.
            c.Mask((x, y) => M.Smooth(0f, 14f, Math.Min(Math.Min(x, s.W - x), Math.Min(y, s.H - y))));
        }

        private static void ComboWord(Canvas c, Slot s, bool prism)
        {
            const float cap = 30f;
            using (GraphicsPath path = Type.Centered("COMBO", cap, 7f, s.PivotX, s.PivotY))
            {
                var text = new Lettering(path, Whole(s), 8, 0f);
                Seat(c, text.Field, 2.8f, 0.5f, 1f);
                c.Glow(text.Field, 3.2f, prism ? 0.3f : 0.12f, 1f, prism ? Theme.PrismGlow : Theme.Violet);
                if (prism) text.Fill(c, Theme.Horizontal(s.PivotX - 62f, s.PivotX + 62f, Theme.Prism));
                else text.Fill(c, Ink.Mix(Theme.TextPrimary, Theme.Violet, 0.4f));
            }
        }

        private static void RankLetter(Canvas c, Slot s, string rank)
        {
            Ink accent = Theme.HudRank(rank);
            float cap = 0.6f * s.Ppu, baseline = s.PivotY + 0.27f * s.Ppu;
            using (GraphicsPath path = Type.Line(rank, cap, 0f, 1f, s.PivotX, 0f, baseline))
            {
                var text = new Lettering(path, Whole(s), 8, 0f);
                Seat(c, text.Field, 4f, 0.6f, 1.5f);
                text.Fill(c, Theme.Vertical(baseline - cap, Theme.Light(accent, 0.6f), baseline, accent));
            }
        }

        private static void RankWord(Canvas c, Slot s)
        {
            // Drawn about three times its display size, like the plate it stands on.
            const float cap = 43f;
            using (GraphicsPath path = Type.Line("RANK", cap, 17f, 1f, s.W * 0.5f, 0f, s.H - 19f))
                new Lettering(path, Whole(s), 8, 0f).Fill(c, Theme.Light(Theme.Periwinkle, 0.6f));
        }

        private static void Marker(Canvas c, Slot s, string rank)
        {
            Ink accent = Theme.Rank(rank);
            Chip(c, Shape.Rect(1f, 1f, s.W - 2f, s.H - 2f, 9f), 0.34f, accent);
            using (GraphicsPath path = Type.Centered(rank, 18.04f / 0.989f, 0f, s.W * 0.5f, s.H * 0.5f))
                new Lettering(path, Whole(s), 8, 0f).Fill(c, Theme.Light(accent, 0.55f));
        }

        private static void Plus(Canvas c, Slot s, bool outline)
        {
            float cx = s.W * 0.5f, cy = s.H * 0.5f, reach = 11f, half = 2.6f;
            Sdf plus = Shape.Union(
                Shape.Rect(cx - reach, cy - half, reach * 2f, half * 2f, half),
                Shape.Rect(cx - half, cy - reach, half * 2f, reach * 2f, half));
            if (outline) c.Glow(plus, 2.6f, 0.9f, 1f, Theme.White);
            else c.Fill(plus, Theme.White);
        }

        private static void Countdown(Canvas c, Slot s, string digit)
        {
            const float cap = 172f;
            Lettering text = Digit(s, digit, cap, 1.06f, 0.6f, 0f);
            Seat(c, text.Field, 6f, 0.42f, 2f);
            c.Glow(text.Field, 7f, 0.3f, 1f, Theme.Violet);
            // The 1 stands close to the right of its slice: its light has to end there, not be cut.
            c.Mask((x, y) => M.Smooth(0f, 8f, Math.Min(Math.Min(x, s.W - x), Math.Min(y, s.H - y))));
            text.Fill(c, Theme.Vertical(s.PivotY - cap * 0.5f, Theme.White, s.PivotY + cap * 0.5f, Ink.Mix(Theme.TextPrimary, Theme.Violet, 0.14f)));
        }

        private static void AutoLabel(Canvas c, Slot s)
        {
            Sdf pill = Shape.Pill(3f, 9f, s.W - 6f, s.H - 18f);
            Panel(c, pill, 9f, s.H - 18f, 2.2f, Theme.HudBlue, Theme.HudViolet);

            // The round play button of the interface's preview rows.
            float cx = 37f, cy = s.H * 0.5f;
            c.Fill(Shape.Circle(cx, cy, 19f), Theme.Light(Theme.Periwinkle, 0.2f));
            c.Fill(Shape.RoundPolygon(new[] { new PointF(cx - 6f, cy - 9.5f), new PointF(cx + 10.5f, cy), new PointF(cx - 6f, cy + 9.5f) }, 1.6f), Theme.Ground);
            using (GraphicsPath path = Type.Centered("AUTO LIVE", 28f, 6f, 189f, cy))
                new Lettering(path, Whole(s), 8, 0f).Fill(c, Theme.White);
        }

        // ------------------------------------------------------------------------------------ controls and gauges

        /// <summary>A panel of the HUD: a deep face from one colour to another inside the bright edge.</summary>
        private static void Panel(Canvas c, Sdf shape, float top, float height, float edge, Ink from, Ink to)
        {
            c.Fill(shape, Theme.Vertical(top, from, top + height, to));
            c.Stroke(shape, edge, Theme.HudEdge(top, height), -1f);
        }

        /// <summary>A small label of the HUD in a colour of its own.</summary>
        private static void Chip(Canvas c, Sdf shape, float depth, Ink accent)
        {
            c.Fill(shape, Theme.Deep(accent, depth));
            c.Stroke(shape, 1.8f, accent, -1f);
        }

        private static void Button(Canvas c, string n)
        {
            bool disabled = n.Contains("disable");
            float k = disabled ? 0.38f : 1f;
            const float cx = 48f, cy = 48f, r = 40f;
            Sdf disc = Shape.Circle(cx, cy, r);
            if (!disabled)
            {
                Seat(c, disc, 2.4f, 0.55f, 1f);
                Panel(c, disc, cy - r, r * 2f, 2.4f, Theme.HudBlue, Theme.HudViolet);
                c.Stroke(Shape.Circle(cx, cy, r - 6f), 1f, Theme.HudLine, -1f);
            }
            else
            {
                c.Fill(disc, Theme.Vertical(cy - r, Theme.HudBlue.Fade(0.6f), cy + r, Theme.HudViolet.Fade(0.6f)));
                c.Stroke(disc, 2.4f, Theme.Periwinkle.Fade(0.45f), -1f);
            }

            Ink ink = Theme.White.Fade(k);
            if (n.Contains("skip"))
            {
                c.Fill(Shape.RoundPolygon(new[] { new PointF(29f, 31f), new PointF(48f, 48f), new PointF(29f, 65f) }, 2.4f), ink);
                c.Fill(Shape.RoundPolygon(new[] { new PointF(47f, 31f), new PointF(66f, 48f), new PointF(47f, 65f) }, 2.4f), ink);
                c.Fill(Shape.Pill(66.5f, 31.5f, 5f, 33f), ink);
            }
            else
                foreach (float x in new[] { 34f, 53f }) c.Fill(Shape.Pill(x, 31f, 9f, 34f), ink);
        }

        private static void Gauge(Canvas c, Slot s, bool life)
        {
            const float top = 28f, height = 46f, stroke = 2.2f;
            const float starRadiusX = 4.5f, starRadiusY = 6.3f;
            // Leave room for the outward point where the star crosses the exposed end of the rim.
            float left = life ? starRadiusX : 2f, right = s.W - (life ? 2f : starRadiusX);
            Sdf pill = Shape.Pill(left, top, right - left, height);
            Seat(c, pill, 2.6f, 0.55f, 1f);
            // The seat ends inside the texture: the score panel is tiled a pixel taller than it, so whatever is in the
            // last row shows again as a line above the panel.
            c.Mask((px, py) => M.Smooth(0.5f, 5f, s.H - py) * M.Smooth(0f, 2.4f, life ? px : s.W - px));
            Panel(c, pill, top, height, stroke, Ink.Mix(Theme.Ground, Theme.HudBlue, 0.95f), Ink.Mix(Theme.Ground, Theme.HudBlue, 0.45f));
            float starX = life ? left + stroke * 0.5f : right - stroke * 0.5f;
            c.Fill(Shape.Star4(starX, top + height * 0.5f, starRadiusX, starRadiusY, 1.16f, 0f), Theme.White);

            // The eyebrow: a diamond in the part's colour and the name, inside the border that does not stretch.
            Ink accent = life ? Theme.Success : Theme.Periwinkle;
            float x = life ? 20f : 44f;
            string word = life ? "LIFE" : "SCORE";
            const float cap = 16f, tracking = 4f, middle = 14.5f;
            float width = Type.Width(word, cap, tracking);
            using (GraphicsPath path = Type.Centered(word, cap, tracking, x + 14f + width * 0.5f, middle))
            {
                var text = new Lettering(path, Whole(s), 8, 0f);
                Sdf mark = Shape.Diamond(x, middle, 5f, 6.5f, 0.6f);
                Seat(c, Shape.Union(text.Field, mark), 2.6f, 0.7f, 0.8f);
                c.Fill(mark, accent);
                text.Fill(c, Theme.TextPrimary);
            }
        }

        // Fills sit 2 px inside their trough, as the first set measured.
        private const float BarInset = 2f;

        private static void Bar(Canvas c, Slot s, Ink accent)
        {
            BarFill(c, s, Theme.Vertical(BarInset, Theme.Light(accent, 0.1f), s.H - BarInset, Theme.Deep(accent, 0.8f)));
        }

        private static void ScoreBar(Canvas c, Slot s)
        {
            BarFill(c, s, Theme.Horizontal(0f, s.W, Theme.ScoreRun));
        }

        private static void BarFill(Canvas c, Slot s, Paint paint)
        {
            Sdf bar = Shape.Pill(BarInset, BarInset, s.W - BarInset * 2f, s.H - BarInset * 2f);
            c.Fill(bar, paint);
            c.Fill(Shape.Intersect(bar, Shape.Rect(-8f, -8f, s.W + 16f, 8f + s.H * 0.4f, 0f)), Theme.White.Fade(0.28f));
        }

        private static void GaugeGlow(Canvas c, Slot s, Ink accent)
        {
            float top = s.H * 0.28f, height = s.H * 0.5f;
            Sdf pill = Shape.Pill(15f, top, s.W - 23f, height);
            c.Glow(pill, Math.Min(11f, s.H * 0.07f), 0.5f, 0f, accent);
            c.Stroke(pill, 2.2f, Theme.Light(accent, 0.2f), 0f);
            c.Mask((x, y) => M.Smooth(0f, 7f, Math.Min(x, s.W - x)));
        }

        private static void MeterMark(Canvas c, Slot s)
        {
            Sdf mark = Shape.Diamond(s.W * 0.5f, s.H * 0.5f, 6.5f, 8.5f, 0.8f);
            Seat(c, mark, 1.8f, 0.7f, 0.5f);
            c.Fill(mark, Theme.TextPrimary);
        }

        // ------------------------------------------------------------------------------------ the lane
        // Notes run from the vanishing point (0, 5.46) to the judge line at y -2.96 over twelve lanes of 1.19 units,
        // so every lane edge is a straight line from that point and the art takes its edges from it.
        private const float VanishY = 5.46f, JudgeY = -2.96f, HalfAtJudge = 6f * 1.19f;

        private static float HalfWidth(float worldY) { return HalfAtJudge * (VanishY - worldY) / (VanishY - JudgeY); }

        // lane_base, lane_line, lane_mask: 101.27473 px per unit, pivot at pixel (1024, 588), placed at y -0.8.
        private const float LanePpu = 101.27473f, LaneX = 1024f, LaneRow = 588f, LaneY = -0.8f;

        private static float LaneHalf(float row) { return HalfWidth(LaneY + (LaneRow - row) / LanePpu) * LanePpu; }

        private static float LaneRowAt(float worldY) { return LaneRow - (worldY - LaneY) * LanePpu; }

        private static void LaneBase(Canvas c, Slot s)
        {
            const float top = 8f, bottom = 1171f;
            float judge = LaneRowAt(JudgeY);
            c.Pixels(delegate(float x, float y)
            {
                if (y < top || y > bottom) return default(Ink);
                float half = LaneHalf(y), d = Math.Abs(x - LaneX) - half;
                float cover = c.Cover(d * 0.8f);
                if (cover <= 0f) return default(Ink);

                // Quiet at the horizon, level from halfway down, a little fuller at the bottom.
                float t = (y - top) / (bottom - top), k = M.Unit(t / 0.55f), rise = 1f - (1f - k) * (1f - k) * (1f - k);
                float alpha = (26f + 144f * rise + 8f * M.Smooth(0.55f, 1f, t)) / 255f;

                // The splash's light over the judgement line, kept faint so notes stay the brightest thing here.
                float across = M.Unit(Math.Abs(x - LaneX) / half);
                float light = 0.085f * M.Gauss(y - judge, 120f) * (1f - 0.65f * across * across);
                return Ink.Mix(Theme.Ground, Theme.Violet, light).Fade(alpha * cover);
            });
        }

        private static void LaneLines(Canvas c, Slot s)
        {
            // Every second lane edge: the splash's rails outside, fine light lines inside.
            const int count = 7;
            c.Pixels(delegate(float x, float y)
            {
                float depth = y / s.H;
                float fade = M.Smooth(0f, 0.06f, depth) * M.Smooth(1f, 0.9f, depth);
                float half = LaneHalf(y);
                float at = (x - LaneX) / half * 0.5f + 0.5f;
                int index = (int)Math.Round(at * (count - 1));
                if (index < 0 || index >= count) return default(Ink);
                bool outer = index == 0 || index == count - 1;
                float center = LaneX + (2f * index / (count - 1f) - 1f) * half;
                float cover = c.Line(x - center, outer ? 4.2f : 1.9f);
                if (cover <= 0f) return default(Ink);
                Ink ink = outer ? Theme.LaneEdge : Theme.Light(Theme.Violet, 0.62f);
                float strength = outer ? M.Lerp(0.3f, 0.86f, M.Smooth(0.05f, 0.72f, depth)) : M.Lerp(0.16f, 0.3f, depth);
                return ink.Fade(cover * fade * strength);
            });
        }

        // lane_mask hides notes outside the floor. Its fades at the top and the bottom decide where notes appear, so
        // they are the first set's, row by row (data/lane-mask-profile.csv); the sides follow the lane edges.
        private static void LaneMask(Canvas c, Slot s)
        {
            string[] rows = File.ReadAllLines(Path.Combine(_data, "lane-mask-profile.csv"));
            var profile = new Ink[s.H];
            for (int i = 1; i < rows.Length; i++)
            {
                string[] cells = rows[i].Split(',');
                int row = int.Parse(cells[0], CultureInfo.InvariantCulture);
                profile[row] = new Ink(
                    int.Parse(cells[2]) / 255f, int.Parse(cells[3]) / 255f, int.Parse(cells[4]) / 255f, int.Parse(cells[1]) / 255f);
            }

            c.Pixels((x, y) => profile[(int)y].Fade(M.Unit(LaneHalf(y) + 0.5f - Math.Abs(x - LaneX))));
        }

        // judgeLine: 108 px per unit, pivot at pixel (804, 57), placed at y -2.92. The slab is 83 px tall, which is
        // what the sprite's rows 15.08..98.92 leave room for, and its ends are lane edges.
        private const float JudgePpu = 108f, JudgeX = 804f, JudgeRow = 57f, JudgeOriginY = -2.92f;

        private static float JudgeHalf(float row) { return HalfWidth(JudgeOriginY + (JudgeRow - row) / JudgePpu) * JudgePpu; }

        private static void JudgeLine(Canvas c)
        {
            const float top = JudgeRow - 41.5f, bottom = JudgeRow + 41.5f;
            float th = JudgeHalf(top), bh = JudgeHalf(bottom), slope = (bh - th) / (bottom - top);
            var corners = new[]
            {
                new PointF(JudgeX - th, top), new PointF(JudgeX + th, top), new PointF(JudgeX + bh, bottom), new PointF(JudgeX - bh, bottom)
            };
            Sdf slab = Shape.RoundPolygon(corners, 12f);

            // The floor shows through; a bar of violet light lies along the middle.
            c.Fill(slab, Theme.Ground.Fade(0.46f));
            c.Pixels(delegate(float x, float y)
            {
                float cover = c.Cover(slab(x, y));
                if (cover <= 0f) return default(Ink);
                float across = Math.Abs(x - JudgeX) / bh;
                return Ink.Mix(Theme.Violet, Theme.Periwinkle, across).Fade(cover * 0.2f * M.Gauss(y - JudgeRow, 24f) * (1f - 0.4f * across));
            });

            // Lane edges, short of the rim at both ends so they read as marks and not as a grid.
            c.Pixels(delegate(float x, float y)
            {
                float half = th + slope * (y - top);
                float at = (x - JudgeX) / half * 6f;
                int k = (int)Math.Round(at);
                if (k <= -6 || k >= 6) return default(Ink);
                float lean = slope * k / 6f, scale = 1f / (float)Math.Sqrt(1f + lean * lean);
                float cover = c.Line((x - (JudgeX + half * k / 6f)) * scale, 1.8f);
                float ends = M.Smooth(top + 5f, top + 17f, y) * M.Smooth(bottom - 5f, bottom - 17f, y);
                return Theme.Light(Theme.Violet, 0.7f).Fade(cover * ends * (k % 2 == 0 ? 0.72f : 0.46f));
            });

            // The rim: the judgement line's near white in the middle, the rails' periwinkle toward the ends.
            c.Stroke(slab, 3.6f, (x, y) => Ink.Mix(Theme.Light(Theme.Violet, 0.8f), Theme.Light(Theme.Periwinkle, 0.12f), M.Smooth(0.15f, 1f, Math.Abs(x - JudgeX) / bh)), -1f);

            float inset = JudgeHalf(JudgeRow) - 24f;
            foreach (float x in new[] { JudgeX - inset, JudgeX + inset })
            {
                c.Fill(Shape.Diamond(x, JudgeRow, 6f, 9f, 0.8f), Theme.TextPrimary);
                c.Fill(Shape.Diamond(x, JudgeRow, 2.2f, 3.4f, 0.2f), Theme.Periwinkle);
            }
        }

        // The floor under the stage fades into the page colour with the brand's run glowing along the bottom edge.
        private static void Floor(Canvas c, Slot s)
        {
            c.Pixels(delegate(float x, float y)
            {
                float depth = y / s.H, across = x / s.W;
                float alpha = M.Pow(M.Unit(depth / 0.5f), 2f);
                float edge = M.Smooth(0f, 0.04f, Math.Min(across, 1f - across));
                float glow = 0.46f * M.Pow(depth, 1.5f) * edge;
                return Ink.Mix(Theme.Ground, Ink.Along(Theme.Brand, across), glow).Fade(alpha);
            });
        }

        // ------------------------------------------------------------------------------------ the jacket stage
        // Hand-placed corners of the cover and of its reflection in texture pixels, clockwise from the upper left.
        private static PointF[] Plane(string name)
        {
            switch (name)
            {
                case "tex_2dmode_jacket_mirror_main": return Quad(2, 3, 471, 3, 460, 425, 13, 425);
                case "tex_2dmode_jacket_nml_main": return Quad(2, 4, 494, 4, 483, 470, 13, 470);
                default: throw new ArgumentException("Unknown jacket: " + name);
            }
        }

        private static PointF[] Quad(params float[] v)
        {
            return new[] { new PointF(v[0], v[1]), new PointF(v[2], v[3]), new PointF(v[4], v[5]), new PointF(v[6], v[7]) };
        }

        /// <summary>The cover with the detail cover's corners.</summary>
        private static Sdf JacketOutline(string name) { return Shape.RoundPolygon(Plane(name), 24f); }

        private static Sdf JacketMask(string maskName)
        {
            // Both sprites pivot on their centres; the mask has one pixel less padding than its texture.
            string name = "tex_" + maskName.Substring("mask_".Length);
            Slot mask = _slots[maskName + ".png"], mesh = _slots[name + ".png"];
            return Shape.Move(JacketOutline(name), (mask.W - mesh.W) * 0.5f, (mask.H - mesh.H) * 0.5f);
        }

        // A veil over the cover in the page's colour, so the lane in front stays the brightest thing.
        private static void JacketVeil(Canvas c, Slot s, string n)
        {
            bool mirror = n.Contains("mirror");
            Sdf outline = JacketOutline(n);
            float near = mirror ? 0.56f : 0.4f, far = mirror ? 0.86f : 0.66f;
            c.Fill(outline, Theme.Vertical(0f, Theme.Ground.Fade(near), s.H, Theme.Ground.Fade(far)));
            // A breath of the stage's light on the cover, then the card's hairline on top.
            c.Fill(outline, Theme.Vertical(0f, Theme.Violet.Fade(mirror ? 0.02f : 0.07f), s.H * 0.8f, Theme.Violet.Fade(0f)));
            c.Stroke(outline, 2f, Theme.White.Fade(mirror ? 0.07f : 0.16f), -1f);
        }

        // The card the main jacket hangs on. Its corners are the cover's, moved out: 21 px at the sides and the far edge
        // and 48 on the side with the header (the top, or the bottom of the reflection).
        private static void JacketWindow(Canvas c, Slot s, bool mirror)
        {
            const float margin = 21f, header = 48f;
            string name = "tex_2dmode_jacket_" + (mirror ? "mirror" : "nml") + "_main";
            Slot jacket = _slots[name + ".png"];
            // Where Background2DView puts the cover's mask on the card, in card pixels: the mask's position less the
            // card's over 0.7487 / 0.7484 and times 108, and its scale over the card's. Both sprites pivot on the middle.
            float scaleX = mirror ? 1.0061f : 1.0017f, scaleY = mirror ? 1.0024f : 1.0021f;
            float midX = s.W * 0.5f + 0.35f, midY = s.H * 0.5f + (mirror ? 13.42f : 20.78f);
            PointF[] corners = Plane(name);
            for (int i = 0; i < 4; i++)
                corners[i] = new PointF(midX + (corners[i].X - jacket.W * 0.5f) * scaleX, midY + (corners[i].Y - jacket.H * 0.5f) * scaleY);
            PointF[] outer = Shape.Outset(corners, mirror ? margin : header, margin, mirror ? header : margin, margin);
            Sdf card = Shape.RoundPolygon(outer, 36f);
            float top = outer[0].Y, height = outer[2].Y - top;
            if (mirror) { top += height; height = -height; }

            // Keep a readable rim around the darker, muted jacket panel.
            Paint face = Theme.HudCard(top, height);
            if (!mirror) c.Glow(Shape.Move(card, 0f, 4f), 14f, 0.34f, 1f, Theme.Black);
            c.Fill(card, (x, y) => JacketInk(face(x, y), 0.65f));
            c.Stroke(card, 2.4f, Theme.LaneEdge, -1f);

            // The rank card's line in the header: a white diamond between two fading lines.
            PointF a = corners[mirror ? 2 : 0], b = corners[mirror ? 3 : 1];
            float cx = (a.X + b.X) * 0.5f, cy = (a.Y + b.Y) * 0.5f + (mirror ? 0.5f : -0.5f) * header;
            Ink ink = JacketInk(Theme.Light(Theme.Periwinkle, 0.4f), 0.65f);
            c.Pixels(delegate(float x, float y)
            {
                float d = Math.Abs(x - cx);
                if (d < 13f || d > 92f) return default(Ink);
                return ink.Fade(c.Line(y - cy, 1.4f) * 0.7f * M.Pow(1f - (d - 13f) / 79f, 1.2f));
            });
            foreach (float offset in new[] { -26f, 26f }) c.Fill(Shape.Diamond(cx + offset, cy, 3f, 3f, 0.3f), ink.Fade(0.8f));
            c.Fill(Shape.Diamond(cx, cy, 6.5f, 6.5f, 0.6f), JacketInk(Theme.White, 0.65f));
        }

        private static Ink JacketInk(Ink ink, float brightness)
        {
            float grey = 0.2126f * ink.R + 0.7152f * ink.G + 0.0722f * ink.B;
            Ink muted = Ink.Mix(ink, new Ink(grey, grey, grey, ink.A), 0.25f);
            return new Ink(muted.R * brightness, muted.G * brightness, muted.B * brightness, ink.A);
        }

        // ------------------------------------------------------------------------------------ screen effects

        private static void LifeZero(Canvas c, Slot s)
        {
            c.Pixels(delegate(float x, float y)
            {
                float r = M.Len(x / s.W * 2f - 1f, y / s.H * 2f - 1f), t = M.Unit((r - 0.2f) / 0.8f);
                return Theme.White.Fade(240f / 255f * t * t * (3f - 2f * t));
            });
        }

        // An atlas of seven slots, laid out on a 1024 x 1011 grid and fitted to the 1024 x 1024 canvas.
        private static void Transition(Canvas c, Slot s)
        {
            const float fit = 1011f / 1024f;
            Func<Sdf, Sdf> grid = shape => ((x, y) => shape(x, y * fit));
            Ink[] accents = { Theme.Rose, Theme.Periwinkle, Theme.Gold, Theme.Teal };
            for (int i = 0; i < 4; i++)
            {
                float left = i * 256f;
                Canvas slot = c.View(left, 0f, 256f, 260f / fit);
                PointF[] panel =
                {
                    new PointF(91f, 0f), new PointF(162f, 0f), new PointF(256f, 207f), new PointF(256f, 259f), new PointF(0f, 259f), new PointF(0f, 207f)
                };
                Sdf shape = grid(Shape.RoundPolygon(panel, 8f));
                slot.Fill(shape, Theme.Vertical(0f, Theme.Light(accents[i], 0.5f), 262f, accents[i]));
                slot.Stroke(shape, 2f, Theme.White.Fade(0.6f), -1f);
            }

            c.Fill(grid(Shape.Ring(Shape.Circle(129f, 388f, 111f), 34f)), Theme.White);
            c.Fill(grid(Shape.Circle(385f, 387f, 122f)), Theme.White);
            c.View(514f, 260f / fit, 510f, 245f / fit).Shift(-514f, -260f / fit)
                .Fill(grid(Shape.Rect(514f, 266f, 700f, 238f, 119f)), (x, y) => Theme.White.Fade(0.9f * M.Pow(M.Unit(1f - (x - 514f) / 510f), 1.2f)));
            Soft(c, grid, 256f, 755.5f, 255f, Theme.White);
            c.Fill(grid(Shape.Circle(640f, 620f, 118f)), Theme.Periwinkle);
            c.Stroke(grid(Shape.Circle(640f, 620f, 118f)), 3f, Theme.Light(Theme.Periwinkle, 0.6f), -1f);
            c.Fill(grid(Shape.RoundPolygon(new[] { new PointF(612f, 566f), new PointF(696f, 620f), new PointF(612f, 674f) }, 9f)), Theme.Ground);
            Soft(c, grid, 893f, 631f, 130f, Theme.Light(Theme.Violet, 0.35f));
        }

        private static void Soft(Canvas c, Func<Sdf, Sdf> grid, float cx, float cy, float radius, Ink ink)
        {
            Sdf distance = grid((x, y) => M.Len(x - cx, y - cy) / radius);
            c.Pixels(delegate(float x, float y)
            {
                float r = distance(x, y);
                return r >= 1f ? default(Ink) : ink.Fade(M.Pow(1f - r, 1.5f) * (1f + 1.5f * r) * 0.98f);
            });
        }

        // Two soft arms of the home page's aurora turning round an empty middle.
        private static void Swirl(Canvas c, Slot s)
        {
            c.Pixels(delegate(float x, float y)
            {
                float dx = x / s.W * 2f - 1f, dy = y / s.H * 2f - 1f, r = M.Len(dx, dy);
                float swirl = 0.5f + 0.5f * (float)Math.Cos(2.0 * Math.Atan2(dy, dx) - 8.0 * r);
                float fade = M.Smooth(0f, 0.16f, r) * M.Smooth(1f, 0.62f, r);
                return Ink.Along(Theme.Brand, swirl).Fade(0.8f * fade * (0.14f + 0.86f * swirl * swirl));
            });
        }
    }
}
