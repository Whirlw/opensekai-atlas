namespace Sekai.Live.Tails
{
	/// <summary>
	/// The ordinary end: keep holding, then lift inside the window.
	///
	/// This is the only behaviour a release finishes. A flick end waits for a swipe (SwipeTail) and a
	/// rub, a rub flick or a hidden carrier grades itself (SelfJudgedTail), so neither is ever handed to
	/// this class by TailBehaviours.For.
	/// </summary>
	public sealed class ReleaseTail : ITailBehaviour
	{
		public static readonly ReleaseTail Instance = new ReleaseTail();

		private ReleaseTail()
		{
		}

		public bool TryFinish(LongNote bar, in TailFrame frame)
		{
			// Only the lift matters. Whether the bar still counts as held on this very frame is the
			// bar's own bookkeeping, and letting it decide here is how a finger that held to the end
			// and then lifted could never finish a bar: the lift sample is already "finger gone".
			// The window check itself lives in JudgeTailAsRelease, so an early lift is never graded.
			if (!frame.ReleasedInside)
			{
				return false;
			}

			return bar.JudgeTailAsRelease(frame.Now);
		}

		public bool AcceptsRelease => true;

		public bool AcceptsSwipe => false;

		public bool JudgesItself => false;
	}
}
