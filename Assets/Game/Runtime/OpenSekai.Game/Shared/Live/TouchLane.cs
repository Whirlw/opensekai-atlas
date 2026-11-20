using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Maps a touch position onto the lane axis.
    ///
    /// The result is fractional on purpose: a touch between lane 2 and lane 3 reports 2.5, and the judge
    /// range check in <see cref="JudgeLaneRange"/> then decides whether that lands on a note. The Outside
    /// result means the touch is outside the playable area (past the outer lanes by more than the
    /// configured tolerance).
    ///
    /// The router checks the vertical extent for each native sample, or at touch start for legacy charts.
    /// </summary>
    public static class TouchLane
    {
        /// <summary>Returned when the touch is outside the playable area.</summary>
        public const float OUTSIDE = float.NegativeInfinity;

        /// <summary>
        /// True when the sample exceeds the upper edge of the judgement area.
        /// </summary>
        public static bool IsAboveTheLine(Vector2[] lanePositions, Vector3 touchPosition, float toleranceY)
        {
            if (lanePositions == null || lanePositions.Length == 0)
            {
                return false;
            }

            return touchPosition.y - lanePositions[0].y > toleranceY;
        }

        public static float Resolve(
            Vector2[] lanePositions,
            Vector3 touchPosition,
            float toleranceX)
        {
            if (lanePositions == null || lanePositions.Length == 0)
            {
                return OUTSIDE;
            }

            if (lanePositions[0].x - touchPosition.x > toleranceX)
            {
                return OUTSIDE;
            }

            int lastIndex = lanePositions.Length - 1;
            if (touchPosition.x - lanePositions[lastIndex].x > toleranceX)
            {
                return OUTSIDE;
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
