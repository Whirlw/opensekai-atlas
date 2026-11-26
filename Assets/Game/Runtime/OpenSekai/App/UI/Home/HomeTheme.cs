using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Design tokens of the home page and the splash. Everything shared with the library (ground,
    /// surfaces, text colors, spacing, press feedback) comes from <see cref="LibraryTheme"/>; this
    /// adds the brand colors and the home page's own sizes and timings. Sizes are canvas units at
    /// the 1920×1080 reference resolution.
    /// </summary>
    internal static class HomeTheme
    {
        internal static class Colors
        {
            internal static readonly Color32 Periwinkle = ToColor32(BrandArt.Periwinkle);
            internal static readonly Color32 Violet = ToColor32(BrandArt.Violet);
            internal static readonly Color32 Rose = ToColor32(BrandArt.Rose);

            /// <summary>The 成就 card's tint; the same gold as the 天穹 Ranking Score tier.</summary>
            internal static readonly Color32 Gold = new Color32(255, 198, 112, 255);

            /// <summary>Card body: a step above the library's surface so cards lift off the aurora.</summary>
            internal static readonly Color32 Card = new Color32(23, 28, 39, 255);

            /// <summary>Top edge light of a card.</summary>
            internal static readonly Color32 CardHighlight = new Color32(255, 255, 255, 16);

            internal static readonly Color32 Chip = new Color32(255, 255, 255, 18);

            /// <summary>The "ATLAS" in the wordmark.</summary>
            internal const string WORDMARK_ACCENT_HEX = "#C2AEFF";

            private static Color32 ToColor32(BrandArt.Rgb rgb)
            {
                return new Color32(
                    (byte)Mathf.RoundToInt(rgb.R * 255f), (byte)Mathf.RoundToInt(rgb.G * 255f),
                    (byte)Mathf.RoundToInt(rgb.B * 255f), 255);
            }

            internal static Color32 WithAlpha(Color32 color, byte alpha) => new Color32(color.r, color.g, color.b, alpha);
        }

        internal static class Layout
        {
            internal const float HEADER_HEIGHT = 128f;
            internal const float FOOTER_HEIGHT = 92f;
            internal const float GRID_GAP = 24f;

            /// <summary>Share of the grid's width the library card takes.</summary>
            internal const float HERO_SHARE = 0.585f;

            internal const float HERO_RADIUS = 36f;
            internal const float CARD_RADIUS = 30f;
            internal const float CARD_PADDING = 44f;
            internal const float HERO_PADDING = 56f;

            internal const float BRAND_EMBLEM_SIZE = 68f;
            internal const float WORDMARK_SIZE = 34f;
            internal const float WORDMARK_SPACING = 14f;

            internal const float SPLASH_EMBLEM_SIZE = 360f;
            internal const float SPLASH_WORDMARK_SIZE = 84f;

            internal const float MARQUEE_TILE = 188f;
            internal const float MARQUEE_GAP = 22f;
            internal const float MARQUEE_TILT = -13f;
        }

        internal static class Motion
        {
            /// <summary>Home cards rising in, one after another.</summary>
            internal const float ENTER = 0.62f;
            internal const float ENTER_STAGGER = 0.07f;
            internal const float ENTER_RISE = 56f;

            /// <summary>The library card growing over the page on the way into the library.</summary>
            internal const float EXPAND = 0.46f;

            /// <summary>The library card shrinking back into place on the way home.</summary>
            internal const float COLLAPSE = 0.55f;

            internal const float COUNT_UP = 0.9f;

            /// <summary>Seconds one tile row takes to scroll by in the library card.</summary>
            internal const float MARQUEE_SPEED = 26f;

            /// <summary>Length of the idle loop; every idle wave fits a whole number of times into it.</summary>
            internal const float IDLE_PERIOD = 120f;

            internal const float TOAST = 2.2f;

            /// <summary>Second press of back within this window quits the app.</summary>
            internal const float QUIT_WINDOW = 2f;

            /// <summary>The pages sliding to the other page of the home page once the finger lifts, or on a tap.</summary>
            internal const float PAGE_TURN = 0.42f;

            /// <summary>Canvas units a finger moves sideways before a touch becomes a swipe and stops being a tap.</summary>
            internal const float SWIPE_SLOP = 28f;

            /// <summary>Share of the page width a swipe has to cover to turn the page.</summary>
            internal const float PAGE_TURN_DISTANCE = 0.18f;

            /// <summary>Canvas units per second above which a short flick turns the page anyway.</summary>
            internal const float PAGE_FLICK_SPEED = 1100f;
        }
    }
}
