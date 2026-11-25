using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

// The banners at the end of a live and the light around them, in the result page's lettering: wide-set Barlow
// Condensed, the prism for ALL PERFECT, the first set's hues for the others. The banners animate letter by letter, so
// every letter stays inside its own slice (data/result-letter-slices.csv, rectangles from the bottom left).
namespace ArtV2
{
    public static partial class Effects
    {
        private sealed class Banner
        {
            public string Words, Texture, Outline, Flare;
            public int Width, FlareWidth, FlareHeight;
            public Ink[] Stops;
            // The halo's colour; null takes the letters' own, all along the banner.
            public Ink? Glow;
            // How near white the light over the letters is. Light in their own colour only adds green to gold, whose red
            // is full already, and turns it lemon.
            public float Pale = 0.25f;
        }

        private const string Res = "live_result_default_";
        private const int BannerHeight = 182;

        private static readonly Banner[] Banners =
        {
            new Banner
            {
                Words = "ALL PERFECT!!", Texture = "ALL-PERFECT", Outline = "txt_allperfect_line_dgr", Flare = "tex_allperfect_flare",
                Width = 1444, FlareWidth = 1800, FlareHeight = 512, Stops = Theme.Prism
            },
            new Banner
            {
                Words = "FULL COMBO!", Texture = "FULL-COMBO", Outline = "txt_fullcombo_line_dgr", Flare = "tex_fullcombo_flare",
                Width = 1382, FlareWidth = 1600, FlareHeight = 384,
                Stops = Run(Theme.BannerPink, Theme.BannerPurple), Glow = Theme.BannerPink
            },
            new Banner
            {
                Words = "LIVE CLEAR!", Texture = "LIVE-CLEAR", Outline = "txt_liveclear_line_dgr", Flare = "tex_clear_flare",
                Width = 1236, FlareWidth = 1400, FlareHeight = 320,
                Stops = Run(Theme.BannerGold, Theme.BannerPeach), Glow = Theme.BannerGold, Pale = 0.95f
            },
            new Banner
            {
                Words = "LIVE FINISH", Texture = "LIVE-FINISH", Outline = "txt_livefinish_line_dgr", Flare = "tex_finish_flare",
                Width = 1236, FlareWidth = 1400, FlareHeight = 320,
                Stops = Run(Theme.BannerBlue, Theme.BannerPurple), Glow = Theme.BannerBlue
            },
            new Banner
            {
                Words = "LIVE FAILED...", Texture = "FAILED", Outline = "txt_livefailed_line_dgr",
                Width = 1368, Stops = Run(Theme.BannerGrey, Theme.BannerGrey), Glow = Theme.BannerGrey
            }
        };

        private static IEnumerable<Asset> Results(string data)
        {
            string slices = Path.Combine(data, "result-letter-slices.csv");
            foreach (Banner item in Banners)
            {
                Banner b = item;
                yield return new Asset(Res + b.Texture + ".png", b.Width, BannerHeight, 2, false, c => Caption(c, b, slices));
                yield return new Asset(Res + b.Outline + ".png", b.Width, BannerHeight, 2, false, c => Outline(c, b, slices));
                if (b.Flare != null)
                    yield return new Asset(Res + b.Flare + ".png", b.FlareWidth, b.FlareHeight, 1, false, c => Flare(c, b, slices));
            }

            yield return new Asset(Res + "tex_common_Line_01.png", 512, 128, 2, false, c => Ribbons(c, false));
            yield return new Asset(Res + "tex_common_Line_02.png", 512, 128, 2, false, c => Ribbons(c, true));
            yield return new Asset(Res + "tex_common_Line_03.png", 512, 128, 1, false, Spectrum);
            yield return new Asset(Res + "tex_common_Circle_03.png", 128, 128, 2, false, c => Orb(c, 128f, 128f, Theme.DefaultAccent, 0.95f, 0.16f));
            yield return new Asset(Res + "tex_common_flare_02.png", 512, 256, 1, false, c => Lens(c, 512f, 256f, Theme.Violet));
            yield return new Asset(Res + "tex_flash_v2_01.png", 960, 540, 1, false, c => Lens(c, 960f, 540f, Theme.Periwinkle));
            yield return new Asset(Res + "tex_kira_v2_01.png", 16, 16, 8, false, Kira);
            foreach (string name in new[] { "tex_common_Square_02", "tex_common_white_02" })
                yield return Asset.Copy(Res + name + ".png", 32, 32, Path.Combine(data, "kept", Res + name + ".png"));
        }

        // ------------------------------------------------------------------------------------ lettering

        /// <summary>The banner's letters in texture pixels, moved by an offset.</summary>
        private static GraphicsPath Letters(Banner b, string slices, float dx, float dy)
        {
            if (b.Texture == "FAILED")
            {
                // Shown as one sprite, so it is set as a line.
                const float cap = 138f;
                float tracking = (b.Width - 90f - Type.Width(b.Words, cap, 0f)) / (b.Words.Length - 1);
                return Type.Centered(b.Words, cap, tracking, dx + b.Width * 0.5f, dy + BannerHeight * 0.5f);
            }

            List<RectangleF> cells = Slices(slices, b);
            string letters = b.Words.Replace(" ", "");
            if (letters.Length != cells.Count) throw new Exception("Letters and slices do not match: " + b.Words);
            float top = 0f, bottom = BannerHeight;
            foreach (RectangleF cell in cells)
            {
                top = Math.Max(top, cell.Top);
                bottom = Math.Min(bottom, cell.Bottom);
            }

            // One cap height and one baseline for the whole banner. The letters are a little wider than the face's
            // own, which fills the slices without breaking the result page's open spacing.
            float height = bottom - top - 26f, baseline = (top + bottom) * 0.5f + height * 0.5f;
            var path = new GraphicsPath(FillMode.Winding);
            for (int i = 0; i < cells.Count; i++)
            {
                RectangleF cell = cells[i];
                const float margin = 6f;
                float natural = Type.Width(letters[i].ToString(), height, 0f);
                float wide = Math.Min(Wide(b, letters[i]), (cell.Width - margin * 2f) / natural);
                // ALL sits left in its slices, which opens the gap before PERFECT to a word's.
                float room = (cell.Width - natural * wide) * 0.5f - margin;
                float shift = b.Texture == "ALL-PERFECT" && i < 3 ? -Math.Min(16f, room) : 0f;
                using (GraphicsPath letter = Type.Line(
                    letters[i].ToString(), height, 0f, wide, dx + cell.Left + cell.Width * 0.5f + shift, 0f, dy + baseline))
                    path.AddPath(letter, false);
            }

            return path;
        }

        // FULL COMBO's C and O sprites have meshes cut along an older, wider letter, holes included (verify.ps1 checks
        // every banner sprite). The O is set wider and its stems brought back, so its counter is larger than the mesh's
        // hole, and the ends of the C are a little shorter.
        private const float RoundThin = 3.2f;

        private static float Wide(Banner b, char letter) { return b.Texture == "FULL-COMBO" && letter == 'O' ? 1.34f : 1.12f; }

        private static Func<float, float> Thin(Banner b, string slices, float dx)
        {
            if (b.Texture != "FULL-COMBO") return null;
            List<RectangleF> cells = Slices(slices, b);
            string letters = b.Words.Replace(" ", "");
            return delegate(float x)
            {
                for (int i = 0; i < cells.Count; i++)
                    if (letters[i] == 'O' && x - dx >= cells[i].Left && x - dx < cells[i].Right) return RoundThin;
                return 0f;
            };
        }

        private static RectangleF[] Cuts(Banner b, string slices, float dx, float dy)
        {
            if (b.Texture != "FULL-COMBO") return null;
            RectangleF c = Slices(slices, b)[4];
            return new[] { new RectangleF(dx + c.Left + 68f, dy + 54f, c.Width - 68f, 74f) };
        }

        private static List<RectangleF> Slices(string file, Banner b)
        {
            var cells = new List<RectangleF>();
            foreach (string line in File.ReadAllLines(file))
            {
                string[] v = line.Split(',');
                if (v.Length < 10 || Path.GetFileNameWithoutExtension(v[2]) != b.Texture) continue;
                float x = float.Parse(v[3], CultureInfo.InvariantCulture), y = float.Parse(v[4], CultureInfo.InvariantCulture);
                float w = float.Parse(v[5], CultureInfo.InvariantCulture), h = float.Parse(v[6], CultureInfo.InvariantCulture);
                if (w < b.Width) cells.Add(new RectangleF(x, BannerHeight - y - h, w, h));
            }

            cells.Sort((p, q) => p.Left.CompareTo(q.Left));
            return cells;
        }

        private static Lettering Set(Banner b, string slices, float width, float height, int scale)
        {
            float dx = (width - b.Width) * 0.5f, dy = (height - BannerHeight) * 0.5f;
            using (GraphicsPath path = Letters(b, slices, dx, dy))
                return new Lettering(path, new RectangleF(0f, 0f, width, height), scale, Thin(b, slices, dx), Cuts(b, slices, dx, dy));
        }

        private static Paint Fill(Banner b, float width, float height)
        {
            float left = (width - b.Width) * 0.5f, middle = height * 0.5f;
            Paint run = Theme.Horizontal(left + 60f, left + b.Width - 60f, b.Stops);
            // The prism keeps its horizontal run. The other banners also catch light along the top.
            if (b.Glow == null) return (x, y) => Theme.Light(run(x, y), 0.24f);
            return (x, y) => Theme.Light(run(x, y), M.Lerp(0.24f, 0.62f, M.Unit((middle + 72f - y) / 144f)));
        }

        // The first set goes from one colour to the other through near white, so the hue leaves the first only past the
        // middle: these are its hues at the thirds of the banner.
        private static Ink[] Run(Ink from, Ink to) { return new[] { from, from, Ink.Mix(from, to, 0.63f), to }; }

        // No shadow: it would cross into the next letter's slice. The flare behind the banner separates it.
        private static void Caption(Canvas c, Banner b, string slices)
        {
            Lettering text = Set(b, slices, b.Width, BannerHeight, 4);
            text.Fill(c, Fill(b, b.Width, BannerHeight));
        }

        private static void Outline(Canvas c, Banner b, string slices)
        {
            Lettering text = Set(b, slices, b.Width, BannerHeight, 4);
            c.Stroke(text.Field, 2.4f, Theme.Vertical(0f, Theme.White.Fade(0.92f), BannerHeight, Theme.White.Fade(0.6f)), 0f);
        }

        // The banner's own letters as light: a wide halo in its colour, a tight one near white, and the letters.
        private static void Flare(Canvas c, Banner b, string slices)
        {
            bool award = b.Texture == "ALL-PERFECT" || b.Texture == "FULL-COMBO";
            Lettering text = Set(b, slices, b.FlareWidth, b.FlareHeight, 2);
            Paint fill = Fill(b, b.FlareWidth, b.FlareHeight);
            Func<float, float, float> wide = text.Blur(award ? 34f : 20f), near = text.Blur(award ? 11f : 7f);
            c.Pixels((x, y) => (b.Glow ?? fill(x, y)).Fade(Math.Min(0.8f, wide(x, y) * (award ? 1.5f : 0.9f))));
            c.Pixels((x, y) => Theme.Light(fill(x, y), b.Pale).Fade(Math.Min(0.8f, near(x, y) * 0.6f)));
            // The letters themselves are soft too: the game spreads the banner's letters a little under the flare, and a
            // sharp copy would show beside them.
            Func<float, float, float> core = text.Blur(3f);
            c.Pixels((x, y) => Theme.Light(fill(x, y), b.Pale + 0.05f).Fade((award ? 0.4f : 0.55f) * core(x, y)));
            // Nothing may reach the texture's edge.
            c.Mask((x, y) => M.Smooth(0f, 36f, Math.Min(Math.Min(x, b.FlareWidth - x), Math.Min(y, b.FlareHeight - y))));
        }

        // ------------------------------------------------------------------------------------ light

        // The aurora of the home page as ribbons. The mesh scrolls along u, so every wave repeats across the width.
        private static void Ribbons(Canvas c, bool prism)
        {
            c.Pixels(delegate(float x, float y)
            {
                float u = x / 512f, v = y / 128f, light = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float phase = i * 1.57f;
                    float middle = 0.5f + (0.06f + i * 0.014f) * (float)Math.Sin(u * M.PI * 2f + phase);
                    float width = 0.011f + i * 0.006f;
                    float swell = 0.4f + 0.6f * M.Pow((float)Math.Cos(u * M.PI * 2f + phase * 1.3f), 2f);
                    float d = v - middle;
                    light += swell * (0.62f * M.Gauss(d, width) + 0.16f * M.Gauss(d, width * 4.5f));
                }

                light = Math.Min(1f, light);
                Ink ink = prism ? Ink.Mix(Theme.PrismLoop(u), Theme.White, M.Pow(light, 1.6f) * 0.7f) : Theme.White;
                return ink.Fade(light * M.Smooth(0f, 0.12f, Math.Min(v, 1f - v)));
            });
        }

        private static void Spectrum(Canvas c)
        {
            c.Pixels((x, y) => Theme.Light(Theme.PrismLoop(x / 512f), 0.18f));
        }

        // A lens of light: a round core, a wide flat ray and a halo, all inside the texture.
        private static void Lens(Canvas c, float w, float h, Ink ink)
        {
            c.Pixels(delegate(float x, float y)
            {
                float dx = x / w * 2f - 1f, dy = y / h * 2f - 1f;
                float core = M.Gauss(M.Len(dx * w / h, dy), 0.12f);
                float ray = M.Gauss(dx, 0.36f) * M.Gauss(dy, 0.03f);
                float halo = M.Gauss(dx, 0.3f) * M.Gauss(dy, 0.24f);
                float a = Math.Min(1f, core + ray * 0.95f + halo * 0.5f);
                a *= M.Smooth(1f, 0.7f, Math.Abs(dx)) * M.Smooth(1f, 0.7f, Math.Abs(dy));
                return Ink.Mix(ink, Theme.White, Math.Min(1f, M.Pow(core, 0.6f) + ray * 0.55f)).Fade(a);
            });
        }

        private static void Kira(Canvas c)
        {
            c.Pixels((x, y) => Theme.White.Fade(0.5f * M.Gauss(M.Len(x - 8f, y - 8f), 2.6f) * M.Smooth(8f, 6f, M.Len(x - 8f, y - 8f))));
            c.Fill(Shape.Star4(8f, 8f, 7.4f, 1.12f), Theme.White);
        }
    }
}
