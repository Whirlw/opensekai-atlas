namespace Sekai.Live.Tails
{
	/// <summary>
	/// The ordinary end: keep holding, then lift inside the window. Covers a plain end note as well as
	/// the friction and hidden ones, which all end exactly the same way.
	/// </summary>
	public sealed class ReleaseTail : ITailBehaviour
	{
		public static readonly ReleaseTail Instance = new ReleaseTail();

		private ReleaseTail()
		{
		}

		public bool TryFinish(LongNote bar, in TailFrame frame)
		{
			// Only a lift inside the bar earns the end. The window check itself lives in
			// JudgeTailAsRelease, so an early lift during the hold is never graded here.
			if (!frame.ReleasedInside || !frame.IsBeingHeld)
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
