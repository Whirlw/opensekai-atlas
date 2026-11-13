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
			// A judged end note keeps its own clock: it is executed for as long as it stays in the bar's
			// note list, and when its window closes with no verdict it publishes its own Miss. Detaching it
			// at the bar's own end instead orphaned it before that could happen - the friction-flick end
			// that was never judged and never missed. The bar waits for the note it does not grade; the
			// forced expiry (LongNoteTail.ExpireTail) still ends the bar regardless.
			if (bar.ChildNote is NoteBase tail
				&& tail.HasJudgment
				&& tail.State != NoteState.Done
				&& tail.Result == NoteResult.None)
			{
				return false;
			}

			return bar.DetachStandaloneTail(frame.Now);
		}

		public bool AcceptsRelease => false;

		public bool AcceptsSwipe => false;

		public bool JudgesItself => true;
	}
}
