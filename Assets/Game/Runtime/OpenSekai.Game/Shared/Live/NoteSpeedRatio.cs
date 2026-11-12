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
    /// Extracted from <c>LiveLogic.CalcNoteSpeedRatio</c>. One rule is added on top of the plain
    /// length-weighted average, and it is about a segment narrower than a frame.
    ///
    /// Such a segment is a flash: the chart at bar 80.5 has a 799 event that lasts 15 ticks (about 9 ms).
    /// No frame ever lands inside it, so its travel can never be drawn - but folded into the span of a
    /// note a bar later it still added several bars of travel, and the frame the playhead crossed it that
    /// travel left the span again: those notes were thrown about four screens up and came back part way
    /// down the screen instead of falling in from the top. A flash therefore adds no width to the span.
    ///
    /// The notes that sit INSIDE the flash are still drawn with its speed, which is what makes them appear
    /// abruptly - the effect the chart author asked for.
    /// </remarks>
    public static class NoteSpeedRatio
    {
        /// <summary>The width below which a speed segment is a flash no frame can land in.</summary>
        public const float FlashSegmentSeconds = 1f / 60f;

        /// <summary>
        /// Length-weighted average of the speed events between the playhead and the note, with flash
        /// segments left out unless the note sits inside one.
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

            int own = -1;
            for (int i = 0; i < scoreInfos.Length; i++)
            {
                if (scoreInfos[i].bar + scoreInfos[i].barProgress > noteProgress)
                {
                    break;
                }

                own = i;
            }

            if (own >= 0 && IsFlash(scoreInfos, own))
            {
                // The note is one of the flash's own: it is drawn with the flash's speed, which is the
                // whole point of writing one.
                return scoreInfos[own].speedRatio;
            }

            float accumulated = 0f;
            float range = 0f;
            float cursor = currentProgress;
            float speed = scoreInfos[0].speedRatio;
            for (int i = 0; i < scoreInfos.Length; i++)
            {
                float at = scoreInfos[i].bar + scoreInfos[i].barProgress;
                if (at <= currentProgress)
                {
                    speed = scoreInfos[i].speedRatio;
                    continue;
                }

                // The segment that ends here began at the previous event; when that event opened a flash the
                // span has no width across it.
                if (IsFlash(scoreInfos, i - 1))
                {
                    cursor = at;
                    speed = scoreInfos[i].speedRatio;
                    continue;
                }

                float end = Mathf.Min(at, noteProgress);
                accumulated += (end - cursor) * speed;
                range += end - cursor;
                cursor = at;
                speed = scoreInfos[i].speedRatio;
                if (at >= noteProgress)
                {
                    break;
                }
            }

            if (cursor < noteProgress)
            {
                accumulated += (noteProgress - cursor) * speed;
                range += noteProgress - cursor;
            }

            return range <= 0f ? fallbackSpeedRatio : accumulated / range;
        }

        private static bool IsFlash(MusicScoreInfo[] scoreInfos, int index)
        {
            if (index < 0 || index + 1 >= scoreInfos.Length)
            {
                return false;
            }

            return scoreInfos[index + 1].time - scoreInfos[index].time < FlashSegmentSeconds;
        }
    }
}
