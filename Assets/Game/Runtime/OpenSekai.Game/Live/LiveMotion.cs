using System;
using LitMotion;

namespace Sekai.Live
{
	internal static class LiveMotion
	{
#if UNITY_EDITOR
		// Installed by the screenshot driver before it loads a live scene.
		internal static IMotionScheduler CaptureScheduler { get; set; }
#endif

		internal static IMotionScheduler Scheduler(bool unscaled = false)
		{
#if UNITY_EDITOR
			if (CaptureScheduler != null)
			{
				return CaptureScheduler;
			}
#endif
			return unscaled ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update;
		}

		internal static MotionHandle Delay(float seconds, Action callback)
		{
			return LMotion.Create(0f, 1f, seconds)
				.WithScheduler(Scheduler(unscaled: true))
				.WithOnComplete(callback)
				.RunWithoutBinding();
		}
	}
}
