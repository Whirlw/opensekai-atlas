using System;
using LitMotion;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Design tokens of the library page: every color, size, radius and animation timing the page
    /// uses comes from here, so the look is tuned in one place. Sizes are in canvas units at the
    /// 1920×1080 reference resolution.
    /// </summary>
    internal static class LibraryTheme
    {
        internal static class Colors
        {
            internal static readonly Color32 Ground = new Color32(12, 15, 20, 255);
            internal static readonly Color32 Surface = new Color32(21, 26, 34, 255);
            internal static readonly Color32 SurfaceSelected = new Color32(34, 42, 54, 255);
            internal static readonly Color32 SurfaceRaised = new Color32(27, 34, 44, 255);
            internal static readonly Color32 Control = new Color32(255, 255, 255, 15);
            internal static readonly Color32 ControlActive = new Color32(255, 255, 255, 41);
            internal static readonly Color32 Hairline = new Color32(255, 255, 255, 20);
            internal static readonly Color32 CoverPlaceholder = new Color32(37, 44, 56, 255);

            internal static readonly Color32 TextPrimary = new Color32(242, 244, 247, 255);
            internal static readonly Color32 TextSecondary = new Color32(197, 206, 218, 255);
            internal static readonly Color32 TextMuted = new Color32(154, 166, 181, 255);
            internal static readonly Color32 TextOnAccent = new Color32(12, 15, 20, 255);
            internal static readonly Color32 Warning = new Color32(245, 196, 81, 255);

            /// <summary>Accent used until a jacket supplies its own.</summary>
            internal static readonly Color32 DefaultAccent = new Color32(157, 184, 255, 255);

            private static readonly Color32 DifficultyEasy = new Color32(123, 216, 143, 255);
            private static readonly Color32 DifficultyNormal = new Color32(124, 200, 245, 255);
            private static readonly Color32 DifficultyHard = new Color32(245, 196, 81, 255);
            private static readonly Color32 DifficultyExpert = new Color32(245, 154, 154, 255);
            private static readonly Color32 DifficultyMaster = new Color32(205, 166, 245, 255);
            private static readonly Color32 DifficultyAppend = new Color32(242, 176, 222, 255);

            /// <summary>Label color of a manifest <c>musicDifficultyType</c>; unknown types read as plain text.</summary>
            internal static Color32 Difficulty(string difficultyType)
            {
                if (string.Equals(difficultyType, "easy", StringComparison.OrdinalIgnoreCase)) return DifficultyEasy;
                if (string.Equals(difficultyType, "normal", StringComparison.OrdinalIgnoreCase)) return DifficultyNormal;
                if (string.Equals(difficultyType, "hard", StringComparison.OrdinalIgnoreCase)) return DifficultyHard;
                if (string.Equals(difficultyType, "expert", StringComparison.OrdinalIgnoreCase)) return DifficultyExpert;
                if (string.Equals(difficultyType, "master", StringComparison.OrdinalIgnoreCase)) return DifficultyMaster;
                if (string.Equals(difficultyType, "append", StringComparison.OrdinalIgnoreCase)) return DifficultyAppend;
                return TextSecondary;
            }
        }

        internal static class Spacing
        {
            internal const float XS = 4f;
            internal const float S = 8f;
            internal const float M = 16f;
            internal const float L = 24f;
            internal const float XL = 32f;
            internal const float PAGE_MARGIN = 64f;
        }

        internal static class Radius
        {
            internal const float CHIP = 8f;
            internal const float COVER_SMALL = 12f;
            internal const float ROW = 18f;
            internal const float PANEL = 24f;
        }

        internal static class FontSize
        {
            internal const float CAPTION = 18f;
            internal const float BODY = 20f;
            internal const float ROW_TITLE = 26f;
            internal const float PAGE_TITLE = 40f;
            internal const float DETAIL_TITLE = 46f;
            internal const float SCORE = 60f;
        }

        internal static class Layout
        {
            internal const float TOP_BAR_HEIGHT = 120f;
            internal const float CONTROL_HEIGHT = 72f;
            internal const float LIST_WIDTH = 860f;
            internal const float ROW_HEIGHT = 100f;
            internal const float ROW_SPACING = 8f;
            internal const float ROW_COVER_SIZE = 72f;
            internal const int GRID_COLUMNS = 4;
            internal const float GRID_SPACING_X = 20f;
            internal const float GRID_SPACING_Y = 24f;

            /// <summary>Height under a grid cover that holds the title line.</summary>
            internal const float GRID_TITLE_HEIGHT = 38f;

            internal const float DETAIL_COVER_SIZE = 380f;
            internal const float PRIMARY_BUTTON_HEIGHT = 88f;
        }

        /// <summary>
        /// Timings and easings for library animations. Cell binding during scroll applies its
        /// visual state directly; entrance animations are reserved for a new list.
        /// </summary>
        internal static class Motion
        {
            internal const float FAST = 0.15f;
            internal const float STANDARD = 0.22f;
            internal const float SLOW = 0.4f;

            /// <summary>Delay between consecutive rows when a list animates in.</summary>
            internal const float STAGGER = 0.025f;

            internal const float PRESS_SCALE = 0.97f;
            internal const float SELECTED_TILE_SCALE = 1.02f;
            internal const float SELECTED_ROW_SHIFT = 14f;

            /// <summary>Seconds the old preview takes to fade out and the new one to fade in.</summary>
            internal const float PREVIEW_FADE = 0.5f;

            /// <summary>Seconds after a song is selected before another selection is accepted.</summary>
            internal const float SELECT_COOLDOWN = 0.5f;

            internal const Ease ENTER_EASE = Ease.OutCubic;
            internal const Ease EXIT_EASE = Ease.InCubic;
            internal const Ease SELECT_EASE = Ease.OutBack;
            internal const Ease FADE_EASE = Ease.OutQuad;
        }
    }
}
