using Sekai.Live;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The grade a long note's end point gets from how early or late the finger was lifted.
    ///
    /// The end point has its own table (milliseconds): it is generous early and slightly less
    /// generous late, and it has no "bad" band at all - a release inside the outer window always
    /// scores at least good. Reusing the head's symmetric window here was wrong: the end is graded,
    /// not just accepted or rejected.
    /// </summary>
    public static class LongEndJudgement
    {
        // Early side: perfect 58.3 ms, great 108.3 ms, good 125 ms.
        public const float EarlyPerfectMs = 58.3f;
        public const float EarlyGreatMs = 108.3f;
        public const float EarlyGoodMs = 125f;

        // Late side: perfect 66.7 ms, great 133.3 ms, good 141.7 ms.
        public const float LatePerfectMs = 66.7f;
        public const float LateGreatMs = 133.3f;
        public const float LateGoodMs = 141.7f;

        /// <summary>
        /// Grades a release at <paramref name="offsetSeconds"/> (negative = early). Returns null-ish
        /// <see cref="NoteResult.None"/> when the release is outside both windows.
        /// </summary>
        public static NoteResult Evaluate(float offsetSeconds)
        {
            if (float.IsNaN(offsetSeconds))
            {
                return NoteResult.None;
            }

            float ms = offsetSeconds * 1000f;
            bool early = ms < 0f;
            float magnitude = early ? -ms : ms;

            if (early)
            {
                if (magnitude <= EarlyPerfectMs) return NoteResult.JustPerfect;
                if (magnitude <= EarlyGreatMs) return NoteResult.Great;
                if (magnitude <= EarlyGoodMs) return NoteResult.Good;
                return NoteResult.None;
            }

            if (magnitude <= LatePerfectMs) return NoteResult.JustPerfect;
            if (magnitude <= LateGreatMs) return NoteResult.Great;
            if (magnitude <= LateGoodMs) return NoteResult.Good;
            return NoteResult.None;
        }
    }
}
