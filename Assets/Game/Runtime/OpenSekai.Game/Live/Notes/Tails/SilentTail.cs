namespace Sekai.Live.Tails
{
	/// <summary>
	/// A bar whose end is not played at all (a decoration, or a bar explicitly told to skip its tail
	/// judgement). Nothing it holds can be graded, so every capability answers no and every frame is
	/// ignored; the bar still expires on its own timeout, with no result.
	/// </summary>
	public sealed class SilentTail : ITailBehaviour
	{
		public static readonly SilentTail Instance = new SilentTail();

		private SilentTail()
		{
		}

		public bool TryFinish(LongNote bar, in TailFrame frame)
		{
			// Whatever the end note is, this bar is not the thing that grades it: a decoration's tail
			// is judged on its own, in its own right.
			return false;
		}

		public bool AcceptsRelease => false;

		public bool AcceptsSwipe => false;

		public bool JudgesItself => false;
	}
}
