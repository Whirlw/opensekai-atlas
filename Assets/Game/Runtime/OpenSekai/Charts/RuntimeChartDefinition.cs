using Sekai;
using Sekai.Live;

namespace OpenSekai.Charts
{
    /// <summary>
    /// A chart after it has been converted into the note and event arrays the live scene consumes,
    /// together with the totals the library and the result panel need before that scene loads.
    /// Creating one is the only place that turns <see cref="ChartDocument"/> into <see cref="MusicScore"/>.
    /// </summary>
    public sealed class RuntimeChartDefinition
    {
        private RuntimeChartDefinition(ChartDocument document, MusicScore score, int totalNoteCount, bool hasPlayableNotes)
        {
            Document = document;
            Score = score;
            TotalNoteCount = totalNoteCount;
            HasPlayableNotes = hasPlayableNotes;
        }

        /// <summary>The authored chart, still in ticks; kept for hashes and future re-conversion.</summary>
        public ChartDocument Document { get; }

        /// <summary>The converted chart handed to <see cref="Sekai.LiveLogic"/>.</summary>
        public MusicScore Score { get; }

        /// <summary>Judged note count, used for scoring and the result panel.</summary>
        public int TotalNoteCount { get; }

        public bool HasPlayableNotes { get; }

        public long TicksMax => Document?.MusicScoreTicksMax ?? 0L;

        public static RuntimeChartDefinition Create(ChartDocument document, MusicScore score)
        {
            int totalNoteCount = score != null ? LiveUtility.CalculateTotalComboCount(score) : 0;
            bool hasPlayableNotes = document != null && document.HasPlayableNotes && totalNoteCount > 0;
            return new RuntimeChartDefinition(document, score, totalNoteCount, hasPlayableNotes);
        }
    }
}
