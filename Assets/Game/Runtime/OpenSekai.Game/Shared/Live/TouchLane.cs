using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Maps a touch position onto the lane axis.
    ///
    /// The result is fractional on purpose: a touch between lane 2 and lane 3 reports 2.5, and the judge
    /// range check in <see cref="JudgeLaneRange"/> then decides whether that lands on a note. A negative
    /// result means the touch is outside the playable area (above the judgment line, or past the outer
    /// lanes by more than the configured tolerance).
    /// </summary>
    public static class TouchLane
    {
        /// <summary>Returned when the touch is outside the playable area.</summary>
        public const float Outside = -1f;

        public static float Resolve(
            Vector2[] lanePositions,
            Vector3 touchPosition,
            bool checkJudgmentY,
            float toleranceX,
            float toleranceY)
        {
            if (lanePositions == null || lanePositions.Length == 0)
            {
                return Outside;
            }

            // Above the judgment line (touch is "higher" on screen than the note lane) is not an input.
            if (checkJudgmentY && touchPosition.y - lanePositions[0].y > toleranceY)
            {
                return Outside;
            }

            if (lanePositions[0].x - touchPosition.x > toleranceX)
            {
                return Outside;
            }

            int lastIndex = lanePositions.Length - 1;
            if (touchPosition.x - lanePositions[lastIndex].x > toleranceX)
            {
                return Outside;
            }

            for (int i = 0; i < lanePositions.Length; i++)
            {
                if (touchPosition.x - lanePositions[i].x <= 0f)
                {
                    if (i == 0)
                    {
                        return 0f;
                    }

                    float previousX = lanePositions[i - 1].x;
                    float currentX = lanePositions[i].x;
                    return (touchPosition.x - previousX) / (currentX - previousX) + i - 1;
                }
            }

            return lastIndex;
        }
    }
}
