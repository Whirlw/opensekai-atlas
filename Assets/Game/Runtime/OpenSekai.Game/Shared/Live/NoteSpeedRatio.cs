using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The scroll speed a note is drawn with: the speed of the chart segment the note sits in.
    /// </summary>
    /// <remarks>
    /// A speed event owns the segment that starts at it and nothing else, so what the PLAYHEAD is doing
    /// is none of a note's business: a note's distance from the line is its own segment's speed times
    /// the time left. The spike at bar 80.5 is 15 ticks (about 9 ms) wide, and it belongs to the notes
    /// inside it; the notes a bar later keep the speed they were written with.
    ///
    /// The span average this used to be folded every event between the playhead and the note into one
    /// number. That made a 9 ms spike add several bars of travel to the span of every note behind it,
    /// and the travel vanished again the frame the spike left the playhead: the note was flung about
    /// four screens up and snapped back into the screen - a note appearing in the middle of the screen.
    ///
    /// Falls back to the frame's own ratio when the note is at or behind the playhead, when the chart
    /// carries no speed events, or when the segment it lands in has no speed at all.
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

            if (!hasSegment || Mathf.Approximately(speedRatio, 0f))
            {
                // A segment with no speed would freeze the note on the line; the frame's own ratio is
                // the honest answer there, and it also covers a chart whose first event sits after the
                // note.
                return fallbackSpeedRatio;
            }

            return speedRatio;
        }
    }
}
