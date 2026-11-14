namespace Sekai.Live.Tails
{
	/// <summary>
	/// The bar closes on a flick: the player swipes inside the bar instead of lifting. A swipe before
	/// the tail time waits for the lift, which is what makes an early swipe easier to score well.
	/// </summary>
	public sealed class SwipeTail : ITailBehaviour
	{
		public static readonly SwipeTail Instance = new SwipeTail();

		private SwipeTail()
		{
		}

		public bool TryFinish(LongNote bar, in TailFrame frame)
		{
			// Swiping before the tail time is only remembered: the release starts a three frame
			// countdown and the deferred swipe is graded then, which is what makes an early swipe
			// easier to turn into a good grade.
			if (frame.SwipeDelta.x != 0f || frame.SwipeDelta.y != 0f)
			{
				bar.TrySwipeTail(frame.SwipeDelta, frame.DeltaTime, frame.Now, frame.SwipeTouchId,
					frame.SwipeFromTailLane);
			}

			// The release frame itself is frame zero: ticking before recording it keeps the countdown
			// at "third frame AFTER the release" instead of one frame earlier.
			bool settled = bar.TickSwipeTail(frame.Now);

			// Releasing means any of: lifting the finger, sliding it out of the lane, or the window
			// closing. The closed window is the bar's own timeout; here the lane one is the missing
			// half - a finger that slides away has stopped holding just as much as one that lifts.
			if (frame.ReleasedInside || !frame.Covered)
			{
				bar.NotifyLaneRelease();
			}

			return settled;
		}

		public bool AcceptsRelease => false;

		public bool AcceptsSwipe => true;

		public bool JudgesItself => false;
	}
}
