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
    /// reference because the original leaves it untouched when the speed ratio is zero.
    /// </remarks>
    public static class NoteDisplayOffset
    {
        /// <summary>
        /// Time offset for a note, or zero when the speed is neutral.
        /// </summary>
        /// <param name="displayTimeOffset">The view's base window, in seconds.</param>
        /// <param name="speedRatio">Chart scroll speed at the note's position.</param>
        /// <param name="noteSpeedRatio">Per-note speed multiplier.</param>
        /// <param name="isNegativeSpeed">Set when the effective speed is negative; untouched otherwise.</param>
        public static float Resolve(float displayTimeOffset, float speedRatio, float noteSpeedRatio, ref bool isNegativeSpeed)
        {
            if (Mathf.Approximately(speedRatio, 0f))
            {
                return 0f;
            }

            float rawEffectiveSpeedRatio = speedRatio * noteSpeedRatio;
            isNegativeSpeed = rawEffectiveSpeedRatio < 0f;

            float effectiveSpeedRatio = Mathf.Abs(rawEffectiveSpeedRatio);
            if (Mathf.Approximately(effectiveSpeedRatio, 0f))
            {
                return 0f;
            }

            return displayTimeOffset / effectiveSpeedRatio;
        }
    }
}
