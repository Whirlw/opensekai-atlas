using System;
using System.Collections.Generic;
using OpenSekai.App.Playback;

namespace OpenSekai.App.Library
{
    public enum MedalKind { Dan, Achievement }

    /// <summary>A medal's tier decides its frame. Ids are never reused; 0 is the plain frame.</summary>
    public static class MedalTiers
    {
        public const int LOWEST = 0;
        public const int TOP = 4;

        public static bool IsKnown(int tier) => tier >= LOWEST && tier <= TOP;
    }

    /// <summary>The grades a medal can show on its right, lowest first: I to X, then α to ω. A medal may have none.</summary>
    public static class MedalGrades
    {
        /// <summary>How many of <see cref="All"/> are Roman numerals; the rest are Greek letters.</summary>
        public const int ROMAN_COUNT = 10;

        private static readonly string[] _all =
        {
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", "ι", "κ", "λ", "μ",
            "ν", "ξ", "ο", "π", "ρ", "σ", "τ", "υ", "φ", "χ", "ψ", "ω"
        };

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(_all);

        public static bool IsKnown(string grade) => Array.IndexOf(_all, grade) >= 0;

        public static bool IsRoman(string grade)
        {
            int index = Array.IndexOf(_all, grade);
            return index >= 0 && index < ROMAN_COUNT;
        }
    }

    /// <summary>
    /// A medal the player owns: the plate of a 段位 they passed, or the one an unlocked achievement
    /// gives. Nothing about it is stored; it follows from the play history, the 段位 attempts and the
    /// unlocked achievements. Only the keys of the worn ones are saved.
    /// </summary>
    public sealed class Medal
    {
        /// <summary>How many medals a player shows at once.</summary>
        public const int SLOTS = 2;

        private const string DAN_PREFIX = "dan:";
        private const string ACHIEVEMENT_PREFIX = "achievement:";

        private Medal(string key, MedalKind kind, DanDefinition dan, AchievementProgress achievement)
        {
            Key = key;
            Kind = kind;
            Dan = dan;
            Achievement = achievement;
        }

        public string Key { get; }
        public MedalKind Kind { get; }

        /// <summary>The 段位 that was passed; null for other kinds.</summary>
        public DanDefinition Dan { get; }

        /// <summary>The achievement that was unlocked; null for other kinds.</summary>
        public AchievementProgress Achievement { get; }

        public static string DanKey(string definitionId) => DAN_PREFIX + definitionId;
        public static string AchievementKey(string achievementId) => ACHIEVEMENT_PREFIX + achievementId;

        internal static Medal Of(DanDefinition definition) => new(DanKey(definition.Id), MedalKind.Dan, definition, null);

        internal static Medal Of(AchievementProgress progress) =>
            new(AchievementKey(progress.Id), MedalKind.Achievement, null, progress);

        /// <summary>The 段位 id in a key; null for a key of another kind.</summary>
        internal static string DanIdOf(string key) => key != null && key.StartsWith(DAN_PREFIX, StringComparison.Ordinal)
            ? key.Substring(DAN_PREFIX.Length) : null;

        internal static bool IsAchievementKey(string key) => key != null && key.StartsWith(ACHIEVEMENT_PREFIX, StringComparison.Ordinal);
    }
}
