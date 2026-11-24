using System.Collections.Generic;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>How one medal reads on its plate: its text, what its band shows, color, frame, grade and ground.</summary>
    internal sealed class MedalLook
    {
        internal MedalLook(string label, string emblem, string mark, Color32 color, int tier, string grade, bool patterned)
        {
            Label = label ?? string.Empty;
            Emblem = emblem;
            Mark = mark;
            Color = color;
            Tier = Mathf.Clamp(tier, MedalTiers.LOWEST, MedalTiers.SPECIAL);
            Grade = string.IsNullOrEmpty(grade) ? null : grade;
            Patterned = patterned;
        }

        internal string Label { get; }

        /// <summary>The emblem in the band, one of <see cref="DanStyles.Emblems"/>; unused when <see cref="Mark"/> is set.</summary>
        internal string Emblem { get; }

        /// <summary>Short text in the band instead of an emblem (10K, FC, AP); null for an emblem.</summary>
        internal string Mark { get; }

        internal Color32 Color { get; }

        internal int Tier { get; }

        /// <summary>One of <see cref="MedalGrades"/>; null for none.</summary>
        internal string Grade { get; }

        /// <summary>The 段位 pattern behind the text, which no other kind of medal has.</summary>
        internal bool Patterned { get; }

        internal static MedalLook Of(Medal medal) =>
            medal.Kind == MedalKind.Dan ? Of(medal.Dan) : Of(AchievementCopy.For(medal.Achievement));

        /// <summary>The worn medals of <paramref name="slots"/>, in order, without the empty slots.</summary>
        internal static IReadOnlyList<MedalLook> Of(IReadOnlyList<Medal> slots)
        {
            var looks = new List<MedalLook>();
            foreach (Medal medal in slots)
            {
                if (medal != null) looks.Add(Of(medal));
            }

            return looks;
        }

        internal static MedalLook Of(DanDefinition definition) => new MedalLook(definition.Label, definition.Emblem, null,
            DanStyles.ParseColor(definition.Color), definition.Tier, definition.Grade, true);

        internal static MedalLook Of(AchievementLook look) => new MedalLook(look.MedalName,
            "trophy", string.IsNullOrEmpty(look.Mark) ? null : look.Mark, look.Tint, look.MedalTier, look.Grade, false);
    }

    /// <summary>The words around medals.</summary>
    internal static class MedalCopy
    {
        private static readonly string[] _tiers = { "初阶", "进阶", "高阶", "尊阶", "极阶", "特殊" };
        private static readonly string[] _slots = { "第一块", "第二块" };

        internal static string Tier(int tier) => _tiers[Mathf.Clamp(tier, 0, _tiers.Length - 1)];

        internal static string Slot(int slot) => _slots[slot];

        internal static string Kind(MedalKind kind) => kind == MedalKind.Dan ? "段位" : "成就";

        /// <summary>"段位 · 高阶" under a medal in a list.</summary>
        internal static string Describe(Medal medal) => Kind(medal.Kind) + " · " + Tier(MedalLook.Of(medal).Tier);

        /// <summary>The medal's text with its grade, as a toast names it.</summary>
        internal static string Name(Medal medal)
        {
            MedalLook look = MedalLook.Of(medal);
            return look.Grade != null ? look.Label + " " + look.Grade : look.Label;
        }
    }
}
