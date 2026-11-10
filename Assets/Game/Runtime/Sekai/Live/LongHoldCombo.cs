namespace Sekai.Live
{
	public class LongHoldCombo : NoteBase
	{
		private readonly float _laneOffset;

		// "The bar was in play while this point's window was open" - the window can be longer than
		// the moment the point is judged, so the answer is remembered instead of sampled.
		private bool _barWasInPlay;

		public LongHoldCombo()
		{
			Category = NoteCategory.Combo;
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, float speedRatio, float _laneOffset)
			: base(info, 0, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type)
		{
			this._laneOffset = _laneOffset;
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, int id, float speedRatio, float _laneOffset)
			: base(info, id, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type)
		{
			this._laneOffset = _laneOffset;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			if (State == NoteState.Done)
			{
				return;
			}

			// Decoration long hold combo should not be marked as Last/Miss
			// Check parent note's decoration status
			bool isDecorationCombo = IsDecoration || (parentNote != null && parentNote.IsDecoration);

			if (State == NoteState.Last)
			{
				if (!isDecorationCombo)
				{
					JudgeInfo = (NoteResult.Miss, NoteResultDescription.None);
					State = NoteState.Done;
				}
				return;
			}

			if (MusicScoreInfo.time > currentFrameInfo.time)
			{
				return;
			}

			// Two steps, and this is only the first one.
			//
			// 1. here: a marker that reached its own time while the bar was not settled becomes Last,
			//    which means "due, waiting for the press". Nothing is decided yet.
			// 2. LongNote.ConnectionNoteJudgment, driven by the held bar every frame the press is on
			//    it: LongHoldCombo.Judgment sees Last and awards JustPerfect. If no press ever arrives,
			//    the Last branch at the top of this method turns it into Miss on the next frame.
			//
			// So "was the bar held" is answered by whether step 2 runs, not here. The only thing this
			// method settles on its own is the tail already being graded: then the whole bar is over
			// and the marker inherits its outcome instead of waiting for a press that can no longer come.
			var tailNote = parentNote?.ChildNote;
			bool barAlreadySettled = tailNote != null
				&& (tailNote.Result > NoteResult.Pass || tailNote.State == NoteState.Input);
			if (barAlreadySettled)
			{
				JudgeInfo = (NoteResult.JustPerfect, NoteResultDescription.None);
				State = NoteState.Done;
				return;
			}

			// Decoration long hold combo should not be marked as Last
			if (!isDecorationCombo)
			{
				State = NoteState.Last;
			}
		}

		/// <summary>Remembers that the bar was held (or in its release grace) while this point was live.</summary>
		internal void MarkBarInPlay()
		{
			_barWasInPlay = true;
		}

		/// <summary>True once the owning bar has been in play inside this point's judgement window.</summary>
		internal bool WasBarInPlay => _barWasInPlay;


		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (State != NoteState.Last || parentNote == null)
			{
				return false;
			}

			return parentNote.LaneStartF - _laneOffset <= lane && parentNote.LaneEndF + _laneOffset >= lane;
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Last)
			{
				JudgeInfo = (NoteResult.JustPerfect, NoteResultDescription.None);
				State = NoteState.Done;
			}

			return true;
		}

		public override void Terminate()
		{
			if (State == NoteState.Done)
			{
				return;
			}

			var childNote = parentNote?.ChildNote;
			JudgeInfo = parentNote != null
				&& (parentNote.WasHoldingWhenTerminated
					|| (childNote != null && (childNote.Result > NoteResult.Pass || childNote.State == NoteState.Input)))
						? (NoteResult.JustPerfect, NoteResultDescription.None)
						: (NoteResult.Miss, NoteResultDescription.None);
			State = NoteState.Done;
		}

		protected override void OnSpawnNote()
		{
		}

		protected override void PublishResult()
		{
			// A marker that was never judged (the bar ended before its time, or the note is being
			// recycled) must not report a result: the callbacks below score, count combo and play the
			// judgement sound, so firing them for a marker stuck at None is what made an early swipe
			// spike the combo and machine-gun the hit sound.
			if (JudgeInfo.result == NoteResult.None)
			{
				return;
			}

			onUpdateScore?.Invoke(this);
			onUpdateCombo?.Invoke(this);
			onDamage?.Invoke(this);
			onJudgment?.Invoke(this);
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			JudgeInfo = (NoteResult.Auto, NoteResultDescription.None);
			State = NoteState.Done;
			return true;
		}
	}
}