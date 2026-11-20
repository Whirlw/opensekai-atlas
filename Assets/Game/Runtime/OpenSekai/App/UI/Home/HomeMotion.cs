using LitMotion;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Picks the scheduler for every home and splash motion, so a screenshot run can swap in a
    /// manual one and step the page frame by frame (the same arrangement as the live's motions).
    /// Home motions run on unscaled time: nothing on these pages should slow down with timeScale.
    /// </summary>
    public static class HomeMotion
    {
#if UNITY_EDITOR
        /// <summary>Installed by the screenshot driver; null in normal play.</summary>
        public static IMotionScheduler CaptureScheduler { get; set; }
#endif

        internal static IMotionScheduler Scheduler
        {
            get
            {
#if UNITY_EDITOR
                if (CaptureScheduler != null)
                {
                    return CaptureScheduler;
                }
#endif
                return MotionScheduler.UpdateIgnoreTimeScale;
            }
        }

        /// <summary>Progress through a stretch of a timeline: 0 before <paramref name="start"/>, 1 after it ends.</summary>
        internal static float Span(float time, float start, float duration)
        {
            return duration <= 0f ? (time >= start ? 1f : 0f) : Mathf.Clamp01((time - start) / duration);
        }

        /// <summary><see cref="Span"/> with an easing applied.</summary>
        internal static float Span(float time, float start, float duration, Ease ease)
        {
            return EaseUtility.Evaluate(Span(time, start, duration), ease);
        }
    }
}
