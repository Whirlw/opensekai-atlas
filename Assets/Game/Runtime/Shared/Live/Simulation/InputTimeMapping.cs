namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>
	/// Maps an Input System timestamp onto the music timeline.
	///
	/// This is the whole of the "when did the player actually touch" question, so it lives on its own:
	/// the two formulas stay side by side, because the older one is still selectable and its numbers
	/// are part of how the game used to feel.
	/// </summary>
	public static class InputTimeMapping
	{
		/// <summary>
		/// A touch older than this is treated as this old. A stale touch can survive a pause, a
		/// resume, or a focus loss, and without a floor it would land far outside any window.
		/// </summary>
		public const float MaxInputLatencySeconds = 0.25f;

		/// <summary>
		/// The original formula: the wall clock delta is multiplied by one frame time, which shrinks
		/// real latency by a factor of 60 and effectively judges every touch at the current frame's
		/// music time. Kept verbatim behind the "legacy judge timing" option so the old feel stays
		/// available.
		/// </summary>
		public static float FromLegacyClock(double frameMusicTime, double frameGameTime, double inputTime)
		{
			// One frame time at the judge table's fixed rate, matching the original formula.
			const double OneFrameSeconds = 1d / 60d;
			return (float)(frameMusicTime + (inputTime - frameGameTime) * OneFrameSeconds);
		}

		/// <summary>
		/// Passes the measured latency through and clamps it to [-250 ms, 0 ms]: a touch stamped after
		/// the frame's clock reading is treated as this frame, one older than the floor is clamped.
		/// </summary>
		public static float FromClock(double frameMusicTime, double frameGameTime, double inputTime)
		{
			double deltaSeconds = inputTime - frameGameTime;
			if (deltaSeconds > 0d)
			{
				deltaSeconds = 0d;
			}
			else if (deltaSeconds < -MaxInputLatencySeconds)
			{
				deltaSeconds = -MaxInputLatencySeconds;
			}

			return (float)(frameMusicTime + deltaSeconds);
		}
	}
}
