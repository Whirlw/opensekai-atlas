using Sekai.Live;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Ranking used before a press picks a note. The original game ordered a finger's candidates by
    /// the result they would get right now, then by score time, then by lane distance, so the note the
    /// finger judges is the one it is already passing and is closest to.
    ///
    /// The first key is <see cref="Rank"/>: a finger judges exactly one note per frame, so candidates
    /// that can only complete on this exact frame must win it. A long note that is already held does
    /// not need the slot at all, because <c>LiveLogic.RefreshSustainedHolds</c> refreshes it every
    /// frame its owner stays in the lane; ranking it first used to starve the flick or rub note the
    /// same finger was swiping over.
    /// </summary>
    public static class CandidateOrder
    {
        /// <summary>A flick reads its swipe on the frame it runs, so it outranks everything.</summary>
        public const int FlickRank = 2;

        /// <summary>A note judged by the touch itself: it must run Judgment on this frame.</summary>
        public const int TouchRank = 1;

        /// <summary>A long note that is already held: it survives on the refresh pass, not the slot.</summary>
        public const int HeldRank = 0;

        public static int Compare(
            int firstRank,
            NoteResult firstResult,
            float firstTime,
            float firstDistance,
            int secondRank,
            NoteResult secondResult,
            float secondTime,
            float secondDistance)
        {
            if (firstRank != secondRank)
            {
                return firstRank > secondRank ? -1 : 1;
            }

            int byResult = firstResult.CompareTo(secondResult);
            if (byResult != 0)
            {
                return byResult;
            }

            int byTime = firstTime.CompareTo(secondTime);
            if (byTime != 0)
            {
                return byTime;
            }

            return firstDistance.CompareTo(secondDistance);
        }

        /// <summary>
        /// Two candidates that share a judgment time: the higher rank survives, then the closer one,
        /// and a tie keeps the one that was already first in the list.
        /// </summary>
        public static bool KeepsFirst(int firstRank, float firstDistance, int secondRank, float secondDistance)
        {
            if (firstRank != secondRank)
            {
                return firstRank > secondRank;
            }

            return firstDistance <= secondDistance;
        }
    }
}
