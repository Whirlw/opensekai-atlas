using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// How far ahead of its own time a note has to be registered before it can be judged.
    /// </summary>
    /// <remarks>
    /// Two different leads were being conflated. The <em>display</em> lead is how early a note is
    /// drawn, and it shrinks as the scroll speed rises (<see cref="NoteDisplayOffset"/>): a note at
    /// speed 3 is drawn a third of a window early. The <em>judgeable</em> lead is how early a note
    /// must exist in the judgment list, and it has nothing to do with drawing — it only has to be
    /// longer than the earliest window in which a press can count.
    ///
    /// The scanner used to stop at the display lead, so a fast note was registered roughly 130 ms
    /// before its time, already at the edge of the bad window: a rub note queued behind a flick
    /// entered the list too late for any input to reach it. This type keeps the judgeable lead a
    /// constant that no speed event can shorten.
    /// </remarks>
    public static class JudgeListLead
    {
        /// <summary>
        /// Seconds a note must spend in the judgment list before its own time. Comfortably longer than
        /// the widest window (<c>LiveConfig.JudgeTimeAfter</c> is about 125-142 ms at 60 fps).
        /// </summary>
        public const float Seconds = 0.35f;

        /// <summary>
        /// True while <paramref name="noteTime"/> is further away than the lead, meaning the scan can
        /// stop: this note and every note after it are still too far ahead to be judged.
        /// </summary>
        public static bool IsStillTooFarAhead(float currentTime, float noteTime)
        {
            return IsStillTooFarAhead(currentTime, noteTime, Seconds);
        }

        /// <summary>
        /// Same as <see cref="IsStillTooFarAhead(float, float)"/> with an explicit lead, for callers
        /// that need a different one (tests, or a view that wants a wider window).
        /// </summary>
        public static bool IsStillTooFarAhead(float currentTime, float noteTime, float leadSeconds)
        {
            if (float.IsNaN(currentTime) || float.IsNaN(noteTime))
            {
                return false;
            }

            return currentTime + leadSeconds <= noteTime;
        }
    }
}
