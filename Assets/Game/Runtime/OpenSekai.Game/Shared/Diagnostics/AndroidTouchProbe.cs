using System;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Shared.Diagnostics
{
    /// <summary>
    /// Latency-capture bridge to <c>com.opensekai.TouchProbe</c>. The Activity copies touch edges into
    /// Java arrays; this class only starts the probe, samples clock anchors and drains once per segment.
    /// </summary>
    internal static class AndroidTouchProbe
    {
        internal const int Capacity = 16384;
        private const int EdgeAnchors = 9;
        private const double AnchorIntervalSeconds = 1.0;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static double _nextAnchor;
        private static AndroidJavaClass _class;
        private static IntPtr _nowNanos;
        private static readonly jvalue[] NoArgs = new jvalue[0];
        private static bool _running;

        internal static void Begin()
        {
            _running = false;
            try
            {
                _class ??= new AndroidJavaClass("com.opensekai.TouchProbe");
                if (_nowNanos == IntPtr.Zero)
                    _nowNanos = AndroidJNI.GetStaticMethodID(_class.GetRawClass(), "nowNanos", "()J");
                _class.CallStatic("start", Capacity);
                _running = true;
                long uptimeOffset = _class.CallStatic<long>("uptimeMinusNanoTimeMicros");
                bool nanos = _class.CallStatic<bool>("nanosEventTime");
                LatencyCapture.Record("android_probe", value: uptimeOffset / 1000.0,
                    detail: FormattableString.Invariant($"start capacity={Capacity} event_time_ns={nanos} uptime_minus_nanotime_ms={uptimeOffset / 1000.0:R}"));
                AnchorBurst("begin");
            }
            catch (Exception exception)
            {
                LatencyCapture.Record("android_probe", detail: "unavailable " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        internal static void Frame()
        {
            if (!_running || Time.realtimeSinceStartupAsDouble < _nextAnchor)
                return;
            Anchor("frame");
        }

        internal static void End()
        {
            if (!_running)
                return;
            _running = false;
            try
            {
                AnchorBurst("end");
                int count = _class.CallStatic<int>("drain");
                long[] eventNs = _class.CallStatic<long[]>("drainedEventNs");
                long[] receiveNs = _class.CallStatic<long[]>("drainedReceiveNs");
                int[] action = _class.CallStatic<int[]>("drainedAction");
                int[] pointer = _class.CallStatic<int[]>("drainedPointerId");
                int[] pointers = _class.CallStatic<int[]>("drainedPointerCount");
                float[] x = _class.CallStatic<float[]>("drainedX");
                float[] y = _class.CallStatic<float[]>("drainedY");
                for (int i = 0; i < count; i++)
                {
                    LatencyCapture.Record("android_edge", touch: pointer[i], finger: pointers[i], phase: Phase(action[i]),
                        x: x[i], y: y[i], androidEvent: eventNs[i] * 1e-9, androidReceive: receiveNs[i] * 1e-9,
                        detail: "activity");
                }
                LatencyCapture.Record("android_probe", value: count, detail: FormattableString.Invariant(
                    $"end edges={count} overflow={_class.CallStatic<int>("drainedOverflow")} move_events={_class.CallStatic<int>("drainedMoveEvents")} move_history={_class.CallStatic<int>("drainedMoveHistory")}"));
            }
            catch (Exception exception)
            {
                LatencyCapture.Record("android_probe", detail: "drain_failed " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void AnchorBurst(string detail)
        {
            for (int i = 0; i < EdgeAnchors; i++)
                Anchor(detail);
        }

        // Unity time read on both sides of one JNI clock read; the midpoint maps the Java reading.
        private static void Anchor(string detail)
        {
            double before = Time.realtimeSinceStartupAsDouble;
            long android = AndroidJNI.CallStaticLongMethod(_class.GetRawClass(), _nowNanos, NoArgs);
            double after = Time.realtimeSinceStartupAsDouble;
            _nextAnchor = after + AnchorIntervalSeconds;
            LatencyCapture.Record("clock_anchor", time: (before + after) * 0.5, value: (after - before) * 1000.0,
                androidReceive: android * 1e-9, detail: detail);
        }
#else
        internal static void Begin() { }
        internal static void Frame() { }
        internal static void End() { }
#endif

        internal static TouchPhase Phase(int action) => action switch
        {
            0 => TouchPhase.Began,
            1 => TouchPhase.Ended,
            3 => TouchPhase.Canceled,
            _ => TouchPhase.None,
        };
    }
}
