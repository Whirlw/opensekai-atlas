namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The note that will be judged for one candidate on this frame.
    /// A pass reports what the note became after <c>Judgment</c> ran.
    /// </summary>
    public interface IJudgmentPass
    {
        /// <summary>
        /// Judges the candidate at <paramref name="candidateIndex"/> and reports the outcome.
        /// </summary>
        JudgmentSchedule.Outcome Judge(int candidateIndex);

        /// <summary>Called once after a note consumed the press.</summary>
        void Consumed(int candidateIndex);
    }

    /// <summary>
    /// How a finger spends its single judgment slot on a frame.
    ///
    /// The original loop judged one note per finger per frame and stopped as soon as that note
    /// had a result. The stop condition was wrong in a way that is hard to see: a note whose
    /// <c>Judgment</c> returned without producing a result (a rub note whose first touch only set
    /// <c>isTouched</c>, or a friction flick still waiting for the swipe to line up with its time)
    /// also stopped the loop. Everything queued behind that note on the same finger was skipped for
    /// as long as the wait lasted, which is exactly how a row of rub notes behind a flick expired
    /// without ever being touched.
    ///
    /// The rule is therefore: walk the candidates in order, let each one take its turn, and stop
    /// only when one of them actually consumed the press (finished, or produced a result). This
    /// type holds that rule, and <c>LiveLogic</c> only drives it.
    /// </summary>
    public static class JudgmentSchedule
    {
        /// <summary>What a single <c>Judgment</c> call did to a note.</summary>
        ///
        /// <remarks>
        /// The note decides this, not the caller: <c>NoteBase.ClosesSlotAfterPress</c> is true once the
        /// note has a result or reached Done. Calling it "closes the slot" keeps the three completion
        /// shapes readable in one place — a rub note that only arms itself keeps the slot open, a flick
        /// that is still waiting for its swipe keeps it open, and a slide that finishes closes it.
        /// </remarks>
        public readonly struct Outcome
        {
            public Outcome(bool consumesSlot)
            {
                ConsumesSlot = consumesSlot;
            }

            /// <summary>
            /// True when the press went into this note, so the finger is spent for this frame.
            /// A note that merely reacted (started being rubbed, recorded a swipe direction) and is
            /// still waiting to complete does not consume the press.
            /// </summary>
            public bool ConsumesSlot { get; }
        }

        /// <summary>
        /// Runs <paramref name="pass"/> over <paramref name="count"/> candidates in order and stops after
        /// the first one that consumes the press.
        /// </summary>
        /// <returns>
        /// The index that consumed the press, or -1 when every candidate was visited and none did.
        /// </returns>
        /// <remarks>
        /// <typeparamref name="T"/> is a struct so the hot path stays allocation free: the pass carries
        /// the frame's input and writes back to the live state through the reference it already holds.
        /// </remarks>
        public static int Run<T>(int count, ref T pass) where T : struct, IJudgmentPass
        {
            for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
            {
                if (!pass.Judge(candidateIndex).ConsumesSlot)
                {
                    continue;
                }

                pass.Consumed(candidateIndex);
                return candidateIndex;
            }

            return -1;
        }
    }
}
