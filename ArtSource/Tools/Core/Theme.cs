using System;

// The interface's tokens (docs/reference/ui-style-guide.md, LibraryTheme.cs, HomeTheme.cs) and how the play field
// uses them. Nothing else in V2 names a colour of its own, except the three glows in Backgrounds.cs.
namespace ArtV2
{
    public static class Theme
    {
        // Surfaces.
        public static readonly Ink Ground = Ink.Rgb(12, 15, 20);
        public static readonly Ink Surface = Ink.Rgb(21, 26, 34);
        public static readonly Ink Raised = Ink.Rgb(27, 34, 44);
        public static readonly Ink Card = Ink.Rgb(23, 28, 39);
        public static readonly Ink ResultCard = Ink.Rgb(16, 20, 27);

        // Text.
        public static readonly Ink White = Ink.Rgb(255, 255, 255);
        public static readonly Ink TextPrimary = Ink.Rgb(242, 244, 247);
        public static readonly Ink TextSecondary = Ink.Rgb(197, 206, 218);
        public static readonly Ink TextMuted = Ink.Rgb(154, 166, 181);
        public static readonly Ink Black = Ink.Rgb(0, 0, 0);

        // Brand.
        public static readonly Ink Periwinkle = Ink.Rgb(120, 156, 255);
        public static readonly Ink Violet = Ink.Rgb(172, 146, 255);
        public static readonly Ink Rose = Ink.Rgb(255, 150, 210);
        public static readonly Ink Gold = Ink.Rgb(255, 198, 112);
        public static readonly Ink DefaultAccent = Ink.Rgb(157, 184, 255);

        // Shared by the two outer lane rails and the jacket window outlines.
        public static readonly Ink LaneEdge = Light(Periwinkle, 0.1f);

        // Ranking tiers and judgements that the play field needs too.
        public static readonly Ink Teal = Ink.Rgb(110, 214, 192);
        public static readonly Ink Stardust = Ink.Rgb(150, 163, 186);
        public static readonly Ink Meteor = Ink.Rgb(122, 182, 255);
        public static readonly Ink Galaxy = Ink.Rgb(182, 146, 255);
        public static readonly Ink Great = Ink.Rgb(255, 140, 186);
        public static readonly Ink Good = Ink.Rgb(122, 182, 255);
        public static readonly Ink Bad = Ink.Rgb(123, 216, 143);
        public static readonly Ink Success = Ink.Rgb(123, 216, 143);
        public static readonly Ink Danger = Ink.Rgb(245, 128, 128);
        public static readonly Ink Append = Ink.Rgb(242, 176, 222);

        /// <summary>ALL PERFECT: pink, violet, cyan.</summary>
        public static readonly Ink[] Prism = { Ink.Rgb(255, 146, 214), Ink.Rgb(186, 158, 255), Ink.Rgb(112, 224, 255) };
        public static readonly Ink PrismGlow = Ink.Rgb(206, 150, 255);

        /// <summary>The brand mark's own run.</summary>
        public static readonly Ink[] Brand = { Periwinkle, Violet, Rose };

        // The hues of the first set's banners at the end of a live, a pair to each: FULL COMBO, LIVE CLEAR and LIVE FINISH
        // run from the first to the second, LIVE FAILED has the one.
        public static readonly Ink BannerPink = Ink.Rgb(255, 115, 179);
        public static readonly Ink BannerPurple = Ink.Rgb(174, 129, 255);
        public static readonly Ink BannerGold = Ink.Rgb(255, 204, 70);
        public static readonly Ink BannerPeach = Ink.Rgb(255, 202, 160);
        public static readonly Ink BannerBlue = Ink.Rgb(101, 202, 255);
        public static readonly Ink BannerGrey = Ink.Rgb(165, 177, 200);

        // What each kind of note is. Tap is the brand's own colour, hold the 新星 teal, flick the rose the resource
        // pack card paints its note in, critical the gold of everything that is worth more.
        public static readonly Ink Tap = Periwinkle;
        public static readonly Ink Hold = Teal;
        public static readonly Ink Damage = Ink.Rgb(178, 99, 255);
        public static readonly Ink Flick = Rose;
        public static readonly Ink Critical = Gold;

        /// <summary>The prism run round and round without a seam.</summary>
        public static Ink PrismLoop(float position)
        {
            float wrapped = position - (float)Math.Floor(position);
            return Ink.Along(Prism, 1f - Math.Abs(wrapped * 2f - 1f));
        }

        /// <summary>A colour lifted toward white.</summary>
        public static Ink Light(Ink ink, float amount) { return Ink.Mix(ink, White, amount); }

        /// <summary>The same hue, a little more saturated and darker: how JacketPalette makes a glow out of an accent.</summary>
        public static Ink Deep(Ink ink, float value)
        {
            float max = Math.Max(ink.R, Math.Max(ink.G, ink.B)), min = Math.Min(ink.R, Math.Min(ink.G, ink.B));
            float saturation = max <= 0f ? 0f : (max - min) / max, more = Math.Min(0.8f, saturation * 1.2f);
            float stretch = saturation <= 0f ? 1f : more / saturation;
            return new Ink(
                value * (1f - (max - ink.R) / max * stretch), value * (1f - (max - ink.G) / max * stretch),
                value * (1f - (max - ink.B) / max * stretch), ink.A);
        }

        /// <summary>The same hue at a given saturation and value.</summary>
        public static Ink Vivid(Ink ink, float saturation, float value)
        {
            float max = Math.Max(ink.R, Math.Max(ink.G, ink.B)), min = Math.Min(ink.R, Math.Min(ink.G, ink.B));
            float k = max <= min ? 0f : saturation / ((max - min) / max);
            return new Ink(
                value * (1f - (max - ink.R) / max * k), value * (1f - (max - ink.G) / max * k),
                value * (1f - (max - ink.B) / max * k), ink.A);
        }

        /// <summary>The medals' tinted face: the card colour with some of the accent in it.</summary>
        public static Ink Tint(Ink accent, float amount) { return Ink.Mix(Card, accent, amount); }

        // The HUD's panels. The stage is nearly as dark as a result card, and a face that is only lighter than it goes
        // grey, so the faces are the brand's blue and violet gone deep and a bright edge in the same two sets them off.
        public static readonly Ink HudBlue = Deep(Periwinkle, 0.36f);
        public static readonly Ink HudViolet = Deep(Violet, 0.34f);

        /// <summary>The trough a gauge runs in.</summary>
        public static readonly Ink HudWell = Ink.Rgb(8, 11, 24);

        /// <summary>The face of a whole card, lit at its <paramref name="top"/>; a reflection's height is negative.</summary>
        public static Paint HudCard(float top, float height)
        {
            return Vertical(top, Deep(Periwinkle, 0.46f), top + height, Deep(Violet, 0.24f));
        }

        public static Paint HudEdge(float top, float height)
        {
            return Vertical(top, Light(Periwinkle, 0.55f), top + height, Light(Violet, 0.1f));
        }

        /// <summary>The fine line inside a HUD panel.</summary>
        public static readonly Ink HudLine = Light(Periwinkle, 0.4f).Fade(0.4f);

        /// <summary>The score gauge's run, which ends on the gold of rank S.</summary>
        public static readonly Ink[] ScoreRun = { Periwinkle, Violet, Rose, Gold };

        /// <summary>A rank's colour on the HUD. The interface's grey for D would be the first thing every live shows.</summary>
        public static Ink HudRank(string rank) { return rank == "D" ? Light(DefaultAccent, 0.35f) : Rank(rank); }

        public static Ink Rank(string rank)
        {
            switch (rank)
            {
                case "S": return Gold;
                case "A": return Galaxy;
                case "B": return Meteor;
                case "C": return Teal;
                default: return Stardust;
            }
        }

        public static Paint Vertical(float top, Ink a, float bottom, Ink b)
        {
            return delegate(float x, float y) { return Ink.Mix(a, b, (y - top) / (bottom - top)); };
        }

        public static Paint Horizontal(float left, float right, params Ink[] stops)
        {
            return delegate(float x, float y) { return Ink.Along(stops, (x - left) / (right - left)); };
        }

        public static Paint VerticalStops(float top, float bottom, params Ink[] stops)
        {
            return delegate(float x, float y) { return Ink.Along(stops, (y - top) / (bottom - top)); };
        }
    }
}
