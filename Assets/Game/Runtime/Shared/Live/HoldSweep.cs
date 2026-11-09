﻿using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Whether a finger's motion this frame touched a long note's lanes.
    ///
    /// Sampling the touch position once per frame is not enough: a fast swipe can leave the lane on
    /// one side and re-enter it on the other between two frames, and both samples would read as
    /// "outside". A hold is therefore tested as a segment (previous position to current position),
    /// which also matches what the eye sees - the finger did pass over the bar.
    /// </summary>
    public static class HoldSweep
    {
        /// <summary>
        /// How many points along the segment are tested. Three is enough for the framerates and note
        /// widths here and keeps the per-frame cost flat.
        /// </summary>
        public const int Samples = 3;

        /// <summary>
        /// True when any point on the segment from -> to resolves to a lane inside [laneStart, laneEnd].
        /// </summary>
        public static bool TouchesLane(
            Vector3 from,
            Vector3 to,
            float laneStart,
            float laneEnd,
            System.Func<Vector3, float> resolveLane)
        {
            if (resolveLane == null)
            {
                return false;
            }

            float low = Mathf.Min(laneStart, laneEnd);
            float high = Mathf.Max(laneStart, laneEnd);

            for (int sample = 0; sample < Samples; sample++)
            {
                float t = Samples == 1 ? 0f : sample / (float)(Samples - 1);
                float lane = resolveLane(Vector3.Lerp(from, to, t));
                if (lane >= low && lane <= high)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Single-point overload for a touch that did not move this frame.</summary>
        public static bool TouchesLane(float lane, float laneStart, float laneEnd)
        {
            float low = Mathf.Min(laneStart, laneEnd);
            float high = Mathf.Max(laneStart, laneEnd);
            return lane >= low && lane <= high;
        }
    }
}
