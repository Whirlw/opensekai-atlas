namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The hard time window a key must be in for a finger's press to be offered to it.
    ///
    /// The offset is <c>touch - note</c> (negative = the finger is early). A tap and a long note's head
    /// take a press from 125 ms early to 125 ms late; a flick from 125 ms early to 141.7 ms late. Outside
    /// it the key is not a candidate at all, whatever its own grading table would say.
    /// </summary>
    public static class PressCandidateWindow
    {
        public const float EARLY_MS = 125f;
        public const float TAP_LATE_MS = 125f;
        public const float FLICK_LATE_MS = 141.7f;

        /// <summary>Whether a press <paramref name="touchMinusNoteSeconds"/> off a tap or a head is a candidate.</summary>
        public static bool AcceptsTap(float touchMinusNoteSeconds) => Accepts(touchMinusNoteSeconds, TAP_LATE_MS);

        /// <summary>Whether a press <paramref name="touchMinusNoteSeconds"/> off a flick is a candidate.</summary>
        public static bool AcceptsFlick(float touchMinusNoteSeconds) => Accepts(touchMinusNoteSeconds, FLICK_LATE_MS);

        private static bool Accepts(float touchMinusNoteSeconds, float lateMs)
        {
            float ms = touchMinusNoteSeconds * 1000f;
            // A float second cannot hold 0.125 or 0.1417 exactly; the epsilon keeps the edges inside.
            return ms >= -EARLY_MS - 1e-3f && ms <= lateMs + 1e-3f;
        }
    }
}
