using System;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    [Flags]
    internal enum LibraryMode { None = 0, Auto = 1, Practice = 2, ConstantScroll = 4, Death = 8 }

    internal static class LibraryModes
    {
        internal static LibraryMode Selected { get; private set; }
        internal static bool Enabled(LibraryMode mode) => (Selected & mode) != 0;
        internal static bool Unranked => (Selected & ~LibraryMode.Death) != 0;
        internal static bool Locked(LibraryMode mode) => mode == LibraryMode.Death
            ? Enabled(LibraryMode.Auto | LibraryMode.Practice)
            : (mode == LibraryMode.Auto || mode == LibraryMode.Practice) && Enabled(LibraryMode.Death);

        internal static void Toggle(LibraryMode mode)
        {
            if (!Locked(mode)) Selected ^= mode;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Selected = LibraryMode.None;

        internal static bool SpeedFollows
        {
            get => PlayerPrefs.GetInt("practice.speedFollows", 1) == 1;
            set => PlayerPrefs.SetInt("practice.speedFollows", value ? 1 : 0);
        }
        internal static bool JudgmentFollows
        {
            get => PlayerPrefs.GetInt("practice.judgmentFollows", 1) == 1;
            set => PlayerPrefs.SetInt("practice.judgmentFollows", value ? 1 : 0);
        }
        // The decoded chart supplies its actual lower bound at boot, including any pre-BGM notes.
        internal static PracticePlan FullSong() => Plan(double.MinValue, null, PracticeForm.ReadSequence("全曲练习", "1x"), true);

        internal static PracticePlan Plan(double from, double? to, PracticeTemplate sequence, bool recordResult = false) =>
            new PracticePlan(from, to, sequence, recordResult ? PracticeLiveControls.CreateForSong : PracticeLiveControls.Create, SpeedFollows, JudgmentFollows, recordResult);
    }
}
