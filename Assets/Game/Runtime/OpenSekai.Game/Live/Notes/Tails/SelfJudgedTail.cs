namespace Sekai.Live.Tails
{
	/// <summary>
	/// A friction flick end keeps its own judgement. The bar must never write a result onto it: the bar
	/// ends silently where that note sits, and the note scores itself like any standalone flick.
	/// </summary>
	public sealed class SelfJudgedTail : ITailBehaviour
	{
		public static readonly SelfJudgedTail Instance = new SelfJudgedTail();

		private SelfJudgedTail()
		{
		}

		public bool TryFinish(LongNote bar, in TailFrame frame)
		{
			// The end note settles itself, so the bar only detaches once that note has a result or its
			// window has closed. The bar never writes a grade onto it.
			//
			// The note does not depend on this bar for its clock any more: a judged end note is ticked by
			// the session (LiveNoteSession.TickTails), so a bar that leaves first cannot orphan it, and the
			// note still publishes its own Miss when its own window closes.
			return bar.DetachStandaloneTail(frame.Now);
		}

		public bool AcceptsRelease => false;

		public bool AcceptsSwipe => false;

		public bool JudgesItself => true;
	}
}
