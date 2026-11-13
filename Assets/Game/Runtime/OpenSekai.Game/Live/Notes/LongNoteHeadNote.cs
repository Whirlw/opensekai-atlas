namespace Sekai.Live
{
	/// <summary>
	/// The head's own candidate identity: the object the judgement list holds and the press loop asks.
	/// It is not the bar. A press that reaches this object is answered by the head
	/// (<see cref="LongNoteHead.Judgment"/>), never by <see cref="LongNote.Judgment"/>; the visual side
	/// keeps using the bar, so nothing about the picture changes.
	///
	/// It carries the bar's lane and time because that is where the head sits on the chart, and it is
	/// one object per bar, rearmed by <see cref="LongNoteHead.Reset"/>.
	/// </summary>
	internal sealed class LongNoteHeadNote : NoteBase
	{
		private readonly LongNote _bar;
		private readonly LongNoteHead _head;

		internal LongNoteHeadNote(LongNote bar, LongNoteHead head)
			: base(bar.MusicScoreInfo, bar.Id, bar.LaneStart, bar.LaneEnd, bar.Category, bar.speedRatio, bar.LaneOffset, bar.Type)
		{
			_bar = bar;
			_head = head;
		}

		internal LongNoteHead Head => _head;

		/// <summary>
		/// True when the head is a note the player answers - an ordinary head, drawn, judged, and still
		/// unsettled. The body is never a press candidate any more; the head is the only part of a bar that
		/// can take a press, and a hidden head takes none.
		///
		/// A head is an instant key like a tap note: the press settles it once, so once it is settled it is
		/// not a key any more and must leave the candidate list. Leaving it in let an already-judged bar
		/// answer a press with "I have nothing to do" and claim the finger's single judgement slot, which is
		/// how a second press in a lane where a bar was being held never reached the next head.
		/// </summary>
		internal bool IsAnswerable => _bar.HeadIsANote && !_head.IsSettled;

		/// <summary>The head is judged on its own window; the bar's own gate is the window it has always used.</summary>
		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			return !_head.IsSettled && _bar.IsJudgment(ref touch, lane);
		}

		/// <summary>The candidate's verdict entry: this is the head answering, not the bar.</summary>
		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			return _head.Judgment(ref touch, lane);
		}
	}
}
