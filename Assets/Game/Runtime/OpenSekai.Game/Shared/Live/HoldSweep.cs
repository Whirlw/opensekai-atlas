using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Whether a finger's lane is inside a long note's lanes.
    ///
    /// This used to test the segment between two frames as well, on the theory that a fast swipe could
    /// leave a lane and re-enter it between samples. That overload never had a caller - the hold pass
    /// has always asked about the lane the touch resolved to this frame - so only the containment test
    /// is left, and it is the one the coordinator actually asks.
    /// </summary>
    public static class HoldSweep
    {
        /// <summary>True when <paramref name="lane"/> is inside [laneStart, laneEnd], either way round.</summary>
        public static bool TouchesLane(float lane, float laneStart, float laneEnd)
        {
            float low = Mathf.Min(laneStart, laneEnd);
            float high = Mathf.Max(laneStart, laneEnd);
            return lane >= low && lane <= high;
        }
    }
}
