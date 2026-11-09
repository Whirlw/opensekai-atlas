namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Two inputs can nominate the same note in one frame. The decompiled loop kept the note for the
    /// input that sits closer to it and let the tie go to the input that was handled first, i.e. the
    /// older entry of the active list. The rule lives here so it can be tested without a live board.
    /// </summary>
    public enum CandidateSide
    {
        /// <summary>The input being compared loses the note.</summary>
        Current,

        /// <summary>The other input loses the note.</summary>
        Other
    }

    public static class CandidateOwnership
    {
        /// <summary>
        /// Decides which side gives the note up. A missing note reads as <see cref="float.MaxValue"/>,
        /// so two unresolved candidates tie and the current input drops the note.
        /// </summary>
        public static CandidateSide Loser(float currentDistance, float otherDistance)
        {
            return otherDistance <= currentDistance ? CandidateSide.Current : CandidateSide.Other;
        }

        /// <summary>
        /// Same decision, but a note that one finger is already holding stays with that finger no
        /// matter how the lanes line up. Two overlapping long notes are shared candidates of both
        /// fingers; handing a held note to the other finger made the hold skip a frame and release.
        /// </summary>
        public static CandidateSide Loser(bool currentHolds, bool otherHolds, float currentDistance, float otherDistance)
        {
            if (currentHolds != otherHolds)
            {
                return currentHolds ? CandidateSide.Other : CandidateSide.Current;
            }

            return Loser(currentDistance, otherDistance);
        }
    }
}
