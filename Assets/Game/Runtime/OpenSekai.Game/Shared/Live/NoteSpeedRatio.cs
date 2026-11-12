using System;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The scroll speed a note travels at: the speed of the segment the note itself sits in.
    /// </summary>
    /// <remarks>
    /// This used to be the length-weighted average of the segments between the playhead and the note. That
    /// average depends on where the playhead is, so the same note was drawn at different speeds on different
    /// frames: a bar crossing a speed change was re-measured every frame, a segment behind the note (a 50x
    /// entry four bars back) was still charged into the span, and the whole speed effect looked like it
    /// started a bar early.
    ///
    /// What the view needs is the integral of the speed profile: the screen length of a span must be
    /// proportional to speed * chart distance, so the stretch of a 20x segment is visible as 20x. A
    /// per-segment speed gives exactly that, because each note's display offset becomes window / its own
    /// segment speed, and the distance between two notes then scales with the speed they travelled at. It
    /// also stops depending on the playhead, so a body cannot change shape while it is on screen.
    /// </remarks>
    public static class NoteSpeedRatio
    {
        /// <summary>
        /// Above this, a segment's speed is treated as a display spike rather than a scroll speed. Charts
        /// spike the value on purpose (one shipped chart goes to 799 on a bar line) to make notes appear
        /// abruptly. A spike still appears abruptly under the clamp, but charging it in full would turn the
        /// display offset into a millisecond and squash whatever is already drawn.
        /// </summary>
        public const float MaxSegmentSpeedRatio = 20f;

        /// <summary>
        /// Speed of the segment that contains <paramref name="noteProgress"/>, clamped to a sane magnitude.
        /// </summary>
        /// <param name="scoreInfos">Speed events in chart order; may be null or empty.</param>
        /// <param name="currentProgress">Playhead position in bars; only gates the "not reached yet" case.</param>
        /// <param name="noteProgress">Note position in bars.</param>
        /// <param name="fallbackSpeedRatio">Speed to use when no event applies (a note before the first one).</param>
        public static float Resolve(MusicScoreInfo[] scoreInfos, float currentProgress, float noteProgress, float fallbackSpeedRatio)
        {
            if (noteProgress < currentProgress)
            {
                return Clamp(fallbackSpeedRatio);
            }

            if (scoreInfos == null || scoreInfos.Length == 0)
            {
                return Clamp(fallbackSpeedRatio);
            }

            bool found = false;
            float speed = fallbackSpeedRatio;
            for (int i = 0; i < scoreInfos.Length; i++)
            {
                MusicScoreInfo info = scoreInfos[i];
                if (info.bar + info.barProgress > noteProgress)
                {
                    break;
                }

                speed = info.speedRatio;
                found = true;
            }

            if (!found)
            {
                return Clamp(fallbackSpeedRatio);
            }

            // A spike is a display trick, not a speed to charge a note with; the sign (reverse scroll) stays.
            return Clamp(speed);
        }

        /// <summary>
        /// Every path is bounded, including the frame's own fallback: the current section can itself be the
        /// spike (a note the playhead has just passed still uses it), and an unbounded 799 there collapses
        /// whatever is on screen exactly like the unbounded segment would.
        /// </summary>
        private static float Clamp(float speedRatio)
        {
            return Mathf.Clamp(speedRatio, -MaxSegmentSpeedRatio, MaxSegmentSpeedRatio);
        }
    }
}
