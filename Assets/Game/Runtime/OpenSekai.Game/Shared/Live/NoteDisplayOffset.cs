using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Converts the note display window into a time offset, scaled by the effective scroll speed.
    /// </summary>
    /// <remarks>
    /// A note with a negative effective speed (a speed event and a note speed pulling in opposite
    /// directions) is flagged so the view can react, and the magnitude is used for the division so the
    /// offset stays positive. Extracted from <c>LiveLogic.CalcTimeOffset</c>; the flag is passed by
    /// reference because the value stays untouched when the speed ratio is zero.
    ///
    /// A zero speed is a DEAD STOP, and a stop is not an infinite speed: window / 0 is an offset of zero,
    /// which reads as "already on the judgement line" to every consumer (NoteBase.CalcProgress returns 1,
    /// NoteLineProgress.Body clamps every sample to 1) and is a value <see cref="Sekai.NoteLineView"/>
    /// refuses to draw at all (it skips any segment whose end offset is ~0). deinos-phainein holds
    /// speed 0.0 for a quarter bar (tick 71520, t = 47.07..47.37), and for those 19 frames every bar in
    /// play stopped being drawn and then came back - the "长条到了判定线后整个消失几帧再出现" report.
    /// A stop therefore keeps the offset the note already had; a note that has never been drawn keeps
    /// the window itself (one screen per display window).
    /// </remarks>
    public static class NoteDisplayOffset
    {
        /// <summary>
        /// Time offset for a note, or zero only when the display window itself is zero.
        /// </summary>
        /// <param name="displayTimeOffset">The view's base window, in seconds.</param>
        /// <param name="speedRatio">Chart scroll speed at the note's position.</param>
        /// <param name="noteSpeedRatio">Per-note speed multiplier.</param>
        /// <param name="isNegativeSpeed">Set when the effective speed is negative; untouched otherwise.</param>
        /// <param name="previousOffset">
        /// The offset this note was last drawn with, so a dead stop can keep the speed it had. Zero for a
        /// note that has not been drawn yet.
        /// </param>
        public static float Resolve(float displayTimeOffset, float speedRatio, float noteSpeedRatio, ref bool isNegativeSpeed, float previousOffset = 0f)
        {
            if (Mathf.Approximately(speedRatio, 0f))
            {
                // The chart stopped. The note keeps the offset it had instead of the zero that "window over
                // nothing" would produce.
                return FromStop(displayTimeOffset, previousOffset);
            }

            float rawEffectiveSpeedRatio = speedRatio * noteSpeedRatio;
            isNegativeSpeed = rawEffectiveSpeedRatio < 0f;

            float effectiveSpeedRatio = Mathf.Abs(rawEffectiveSpeedRatio);
            if (Mathf.Approximately(effectiveSpeedRatio, 0f))
            {
                return FromStop(displayTimeOffset, previousOffset);
            }

            return displayTimeOffset / effectiveSpeedRatio;
        }

        /// <summary>
        /// What a stopped note is drawn with: the offset it already had, or a whole display window for a
        /// note that has not been drawn yet. A window of zero is the only answer that stays zero.
        /// </summary>
        private static float FromStop(float displayTimeOffset, float previousOffset)
        {
            if (displayTimeOffset <= 0f)
            {
                return 0f;
            }

            return previousOffset > 0f ? previousOffset : displayTimeOffset;
        }
    }
}
