using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// How each Ranking Score tier is named and colored. The ids are
    /// <see cref="RankingRules.Tier"/>'s (0 to 6); the score each tier starts at belongs to the
    /// backend, so it is not repeated here (<see cref="Start"/> asks it). An id past the table
    /// reads as the top tier. The colors walk the spectrum up to gold, and the top tier, 寰宇, runs through
    /// the ALL PERFECT prism instead (its single color is the light it starts from).
    /// </summary>
    internal static class RankingTiers
    {
        internal const int TOP = 6;

        private static readonly string[] _names = { "星尘", "新星", "流星", "银河", "星海", "天穹", "寰宇" };
        private static readonly string[] _latinNames = { "STARDUST", "NOVA", "METEOR", "GALAXY", "STARFIELD", "ZENITH", "COSMOS" };

        private static readonly Color32[] _colors =
        {
            new Color32(150, 163, 186, 255),
            new Color32(110, 214, 192, 255),
            new Color32(122, 182, 255, 255),
            new Color32(182, 146, 255, 255),
            new Color32(255, 132, 200, 255),
            new Color32(255, 198, 112, 255),
            new Color32(236, 226, 255, 255)
        };

        internal static int Count => _names.Length;

        internal static string Name(int tier) => _names[Clamp(tier)];

        internal static string LatinName(int tier) => _latinNames[Clamp(tier)];

        internal static Color32 Color(int tier) => _colors[Clamp(tier)];

        /// <summary>The Ranking Score <paramref name="tier"/> starts at, walked up from 0 through the backend's thresholds.</summary>
        internal static long Start(int tier)
        {
            long start = 0L;
            for (int i = 0; i < Clamp(tier); i++)
            {
                int? next = RankingRules.NextTierThreshold(start);
                if (!next.HasValue)
                {
                    break;
                }

                start = next.Value;
            }

            return start;
        }

        /// <summary>True for the tier drawn in the ALL PERFECT prism rather than one color.</summary>
        internal static bool IsPrismatic(int tier) => Clamp(tier) == TOP;

        /// <summary>
        /// Colors a label with the tier: its color, or for 寰宇 the prism run top to bottom over
        /// each glyph, the way FULL COMBO runs its gold.
        /// </summary>
        internal static void Paint(TMP_Text label, int tier)
        {
            bool prismatic = IsPrismatic(tier);
            label.enableVertexGradient = prismatic;
            if (!prismatic)
            {
                label.color = Color(tier);
                return;
            }

            Color32[] stops = LibraryTheme.Colors.AllPerfectStops;
            Color top = UnityEngine.Color.Lerp(stops[0], UnityEngine.Color.white, 0.35f);
            Color bottom = UnityEngine.Color.Lerp(stops[stops.Length - 1], UnityEngine.Color.white, 0.2f);
            label.color = UnityEngine.Color.white;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        private static int Clamp(int tier) => Mathf.Clamp(tier, 0, _names.Length - 1);
    }
}
