using System;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Average scroll speed a note travels through between the current frame and its own position.
    /// Speed events split the chart into segments, so the answer is the length-weighted average of
    /// the segment speeds inside [currentProgress, noteProgress].
    /// </summary>
    /// <remarks>
    /// Extracted from <c>LiveLogic.CalcNoteSpeedRatio</c> so the segment walk can be tested without a
    /// live session. The behaviour, including every fallback, is intended to be identical: when the
    /// note is at or behind the playhead, when the chart carries no speed events, and when the span is
    /// zero, the frame's own speed ratio is returned.
    /// </remarks>
    public static class NoteSpeedRatio
    {
        // Walk backwards from the hit. A key can meet the line or leave the screen several
        // times across a reversal. Only the connected approach ending at its hit is drawn.
        internal static float FindApproachStart(MusicScoreInfo[] events, MusicScoreInfo target,
            float displayWindow, float noteSpeed)
        {
            if (events == null || events.Length == 0 || noteSpeed == 0f)
                return float.NegativeInfinity;

            int lo = 0, hi = events.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (events[mid].time < target.time) lo = mid + 1;
                else hi = mid;
            }

            double targetBar = (double)target.bar + target.barProgress;
            double endBar = targetBar, endTime = target.time, distance = 0d;
            for (int i = lo - 1; i >= 0; i--)
            {
                var start = events[i];
                double startBar = (double)start.bar + start.barProgress;
                double atStart = distance + (endBar - startBar) * start.speedRatio;
                if (distance != 0d && distance * atStart <= 0d)
                {
                    double fraction = distance / (distance - atStart);
                    return (float)(endTime + (start.time - endTime) * fraction);
                }

                // Resolve uses bar-weighted speed times seconds to the hit. Use those same
                // units to recognize an off-screen boundary, including across BPM changes.
                double bars = targetBar - startBar;
                if (bars > 0d && Math.Abs(atStart * noteSpeed) * (target.time - start.time) >= displayWindow * bars)
                    return start.time;

                endBar = startBar;
                endTime = start.time;
                distance = atStart;
            }
            return float.NegativeInfinity;
        }

        /// <summary>
        /// Length-weighted average of the speed events between the current playhead and the note.
        /// </summary>
        /// <param name="scoreInfos">Speed events in chart order; may be null or empty.</param>
        /// <param name="currentProgress">Playhead position in bars (bar + barProgress).</param>
        /// <param name="noteProgress">Note position in bars.</param>
        /// <param name="fallbackSpeedRatio">Speed ratio to use when no averaging applies.</param>
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

            float accumulated = 0f;
            bool hasRange = false;
            float previousProgress = scoreInfos[0].bar + scoreInfos[0].barProgress;
            float previousSpeedRatio = scoreInfos[0].speedRatio;

            for (int i = 0; i <= scoreInfos.Length; i++)
            {
                float segmentProgress = i < scoreInfos.Length
                    ? scoreInfos[i].bar + scoreInfos[i].barProgress
                    : noteProgress;
                float segmentSpeedRatio = i < scoreInfos.Length ? scoreInfos[i].speedRatio : previousSpeedRatio;

                if (segmentProgress >= currentProgress)
                {
                    if (hasRange)
                    {
                        if (segmentProgress > noteProgress)
                        {
                            accumulated += (noteProgress - previousProgress) * previousSpeedRatio;
                            break;
                        }

                        accumulated += (segmentProgress - previousProgress) * previousSpeedRatio;
                    }
                    else if (i > 0)
                    {
                        if (segmentProgress > noteProgress)
                        {
                            // The note sits inside the segment that starts at scoreInfos[i - 1]: the walk never
                            // opened a range, but the span is non-zero, so it must still be covered at that
                            // segment's speed instead of falling back to the frame's own ratio.
                            accumulated += (noteProgress - currentProgress) * scoreInfos[i - 1].speedRatio;
                            hasRange = true;
                            break;
                        }

                        accumulated += (segmentProgress - currentProgress) * scoreInfos[i - 1].speedRatio;
                    }
                    else
                    {
                        accumulated += segmentProgress - currentProgress;
                    }

                    hasRange = true;
                }

                if (segmentProgress >= noteProgress)
                {
                    break;
                }

                previousProgress = segmentProgress;
                previousSpeedRatio = segmentSpeedRatio;
            }

            if (!hasRange)
            {
                return previousSpeedRatio;
            }

            float range = noteProgress - currentProgress;
            return Mathf.Approximately(range, 0f) ? fallbackSpeedRatio : accumulated / range;
        }
    }
}
