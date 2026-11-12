using System;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The scroll speed the note is drawn with: the speed of the chart segment the note sits in.
    /// </summary>
    /// <remarks>
    /// A speed event belongs to the segment that starts at it, so which segment the PLAYHEAD is in is
    /// none of a note's business: a note's distance from the line is its own segment's speed times the
    /// time left. That makes the drawn field a faithful integral of the speed - neighbouring notes
    /// differ by the local speed, so the gap between them is the stretch the event asked for - while a
    /// note is never pulled around by an event it has already been drawn past.
    ///
    /// The span average this used to be could not express that: it folded every event between the
    /// playhead and the note into one number, so a note a bar behind a 9 ms 799 spike was flung ~5
    /// screens up and snapped back into the screen the frame the spike left the playhead - a note
    /// appearing in the middle of the screen.
    ///
    /// Falls back to the frame's own ratio when the note is at or behind the playhead, or when the
    /// chart carries no speed events.
    /// </remarks>
    public static class NoteSpeedRatio
    {
        /// <summary>
        /// Speed of the segment that contains <paramref name="noteProgress"/>.
        /// </summary>
        /// <param name="scoreInfos">Speed events in chart order; may be null or empty.</param>
        /// <param name="currentProgress">Playhead position in bars (bar + barProgress).</param>
        /// <param name="noteProgress">Note position in bars.</param>
        /// <param name="fallbackSpeedRatio">Speed ratio to use when the note has no segment of its own.</param>
        public static float Resolve(MusicScoreInfo[] scoreInfos, float currentProgress, float noteProgress, float fallbackSpeedRatio)
        {
            if (noteProgress <= currentProgress)
            {
                return fallbackSpeedRatio;
            }

            if (scoreInfos == null || scoreInfos.Length == 0)
            {
                return fallbackSpeedRatio;
            }

            float speedRatio = fallbackSpeedRatio;
            bool hasSegment = false;
            for (int i = 0; i < scoreInfos.Length; i++)
            {
                if (scoreInfos[i].bar + scoreInfos[i].barProgress > noteProgress)
                {
                    break;
                }

                speedRatio = scoreInfos[i].speedRatio;
                hasSegment = true;
            }

            return hasSegment ? speedRatio : fallbackSpeedRatio;
        }
    }
}
