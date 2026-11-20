using Sekai;
using Sekai.Live;

namespace OpenSekai.Charts
{
    /// <summary>
    /// A chart after it has been converted into the note and event arrays the live scene consumes,
    /// together with source metadata and totals the library and result panel need before loading.
    /// Source formats have separate loaders; live playback consumes the same MusicScore boundary.
    /// </summary>
    public sealed class RuntimeChartDefinition
    {
        private RuntimeChartDefinition(ChartDocument document, MusicScore score, int totalNoteCount, bool hasPlayableNotes,
            long ticksMax, NextRushChart nextRush = null, NextRushCompatibilityReport compatibility = null)
        {
            Document = document;
            Score = score;
            TotalNoteCount = totalNoteCount;
            HasPlayableNotes = hasPlayableNotes;
            TicksMax = ticksMax;
            NextRush = nextRush;
            NextRushCompatibility = compatibility;
        }

        /// <summary>The authored chart, still in ticks; kept for hashes and future re-conversion.</summary>
        public ChartDocument Document { get; }

        /// <summary>The converted chart handed to <see cref="Sekai.LiveLogic"/>.</summary>
        public MusicScore Score { get; }

        /// <summary>Judged note count, used for scoring and the result panel.</summary>
        public int TotalNoteCount { get; }

        public bool HasPlayableNotes { get; }

        public long TicksMax { get; }

        /// <summary>Native source for NextRUSH charts. Document is null for this format.</summary>
        public NextRushChart NextRush { get; }
        public NextRushCompatibilityReport NextRushCompatibility { get; }

        public static RuntimeChartDefinition Create(ChartDocument document, MusicScore score)
        {
            int totalNoteCount = score != null ? LiveUtility.CalculateTotalComboCount(score) : 0;
            bool hasPlayableNotes = document != null && document.HasPlayableNotes && totalNoteCount > 0;
            return new RuntimeChartDefinition(document, score, totalNoteCount, hasPlayableNotes, document?.MusicScoreTicksMax ?? 0);
        }

        public static RuntimeChartDefinition CreateNative(NextRushChart source, MusicScore score, long ticksMax,
            NextRushCompatibilityReport compatibility)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            int count = score != null ? LiveUtility.CalculateTotalComboCount(score) : 0;
            return new RuntimeChartDefinition(null, score, count, count > 0, ticksMax, source, compatibility);
        }
    }
}
