using UnityEngine;

namespace Sekai.Live
{
    public sealed class NextRushTickPathPoint
    {
        private readonly float _time;
        private readonly float _left;
        private readonly float _right;
        private readonly int _ease;
        private readonly float? _attachmentFraction;
        private readonly bool _isVisibleConnectorTick;
        private NextRushTickPathPoint _previous;
        private NextRushTickPathPoint _next;

        public NextRushTickPathPoint(NoteBase note, int ease, float? attachmentFraction, bool isVisibleConnectorTick)
        {
            _time = note.MusicScoreInfo.time;
            _left = note.LaneStartF - 0.5f;
            _right = note.LaneEndF + 0.5f;
            _ease = ease;
            _attachmentFraction = attachmentFraction;
            _isVisibleConnectorTick = isVisibleConnectorTick;
        }

        public void SetNeighbors(NextRushTickPathPoint previous, NextRushTickPathPoint next)
        {
            _previous = previous;
            _next = next;
        }

        internal bool Contains(NextRushTickPathPoint tail, float previousTime, float now, float lane)
        {
            var head = this;
            // Consecutive visible connector ticks crossed on one frame all use the first one's lanes.
            NextRushTickPathPoint stop = null;
            for (var point = head; point != null && point._time > previousTime; point = point._previous)
            {
                if (point._isVisibleConnectorTick && !point._attachmentFraction.HasValue) stop = point;
            }

            if (stop != null)
            {
                head = tail = stop;
            }
            else
            {
                while (tail != null)
                {
                    if (!tail._attachmentFraction.HasValue)
                    {
                        if (tail._time > now) break;
                        head = tail;
                        if (head._isVisibleConnectorTick && head._time > previousTime) break;
                    }
                    tail = tail._next;
                }
            }

            tail ??= head;
            float progress = head.InterpolateTo(tail, now);
            float left = Mathf.LerpUnclamped(head._left, tail._left, progress);
            float right = Mathf.LerpUnclamped(head._right, tail._right, progress);
            if (right < left) left = right = (left + right) * 0.5f;

            // Static NextRUSH stage: use one lane's leniency and extend the outermost reachable lane.
            const float OUTSIDE_MARGIN = 1.8f / 1.19f;
            bool withinStage = left >= -0.5f && right <= 11.5f;
            left -= 1f;
            right += 1f;
            if (withinStage)
            {
                left = left <= 0f && right >= 0f ? -OUTSIDE_MARGIN : Mathf.Max(left, -OUTSIDE_MARGIN);
                right = left <= 11f && right >= 11f ? 11f + OUTSIDE_MARGIN : Mathf.Min(right, 11f + OUTSIDE_MARGIN);
            }
            return left <= lane && lane <= right;
        }

        private float InterpolateTo(NextRushTickPathPoint tail, float now)
        {
            if (ReferenceEquals(this, tail) || _ease is 0 or 38) return 0f;
            bool sameTime = Mathf.Abs(tail._time - _time) < 1e-6f;
            float fraction = sameTime ? 0.5f : Mathf.InverseLerp(_time, tail._time, now);
            float from = sameTime ? 0f : _attachmentFraction ?? 0f;
            float to = sameTime ? 1f : tail._attachmentFraction ?? 1f;
            float easedFrom = NextRushConnectorSegment.EaseEndpoint(_ease, from);
            float easedTo = NextRushConnectorSegment.EaseEndpoint(_ease, to);
            if (Mathf.Abs(easedTo - easedFrom) < 1e-6f) return fraction;
            float eased = NextRushConnectorSegment.Interpolate(_ease, Mathf.Lerp(from, to, fraction));
            return (eased - easedFrom) / (easedTo - easedFrom);
        }
    }
}
