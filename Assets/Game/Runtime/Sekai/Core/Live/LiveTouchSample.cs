using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Core.Live
{
    internal readonly struct LiveTouchSample
    {
        public readonly int touchId;
        public readonly int fingerId;
        public readonly Vector2 screenPosition;
        public readonly Vector3 worldPosition;
        public readonly double time;
        public readonly TouchPhase phase;
        public readonly Vector2 deltaPosition;

        public LiveTouchSample(int touchId, Vector2 screenPosition, Vector3 worldPosition,
            double time, TouchPhase phase, Vector2 deltaPosition, int? fingerId = null)
        {
            this.touchId = touchId;
            this.fingerId = fingerId ?? touchId;
            this.screenPosition = screenPosition;
            this.worldPosition = worldPosition;
            this.time = time;
            this.phase = phase;
            this.deltaPosition = deltaPosition;
        }
    }
}
