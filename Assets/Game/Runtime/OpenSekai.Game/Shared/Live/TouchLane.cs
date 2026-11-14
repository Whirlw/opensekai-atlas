using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Maps a touch position onto the lane axis.
    ///
    /// The result is fractional on purpose: a touch between lane 2 and lane 3 reports 2.5, and the judge
    /// range check in <see cref="JudgeLaneRange"/> then decides whether that lands on a note. A negative
    /// result means the touch is outside the playable area (past the outer lanes by more than the
    /// configured tolerance).
    ///
    /// The vertical half of that question is not asked here any more. It is asked once, when the touch is
    /// born (<see cref="IsAboveTheLine"/>), and the answer is remembered for the life of that touch: a
    /// finger that lands above the judgement line never becomes an input, and a finger that landed on the
    /// line keeps its lane when it drifts above it during a swipe.
    /// </summary>
    public static class TouchLane
    {
        /// <summary>Returned when the touch is outside the playable area.</summary>
        public const float Outside = -1f;

        /// <summary>
        /// True when a touch position is above the judgement line, which is the one place the vertical
        /// gate exists: such a touch is not an input at all, whatever it does afterwards.
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
