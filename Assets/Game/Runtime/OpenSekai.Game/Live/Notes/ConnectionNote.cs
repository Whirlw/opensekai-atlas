namespace Sekai.Live
{
	public class ConnectionNote : NoteBase
	{
		private const float NextRushContactWindow = 5f / 60f;
		private LongNote _endpointSection;

		internal bool UsesNextRushContactWindow { get; private set; }
		internal bool HasNextRushContact { get; private set; }

		public void SetNextRushContactWindow() => UsesNextRushContactWindow = true;

		public void SetNextRushEndpointSection(LongNote section)
		{
			_endpointSection = section;
		}

		/// <summary>
		/// How long a slide stays judgeable after its time passes: one reference frame.
		/// </summary>
		/// <remarks>
		/// Long enough to cover the frame the time passes on (a 165 Hz device only has ~6 ms per
		/// frame) and far below the 125 ms between sixteenths at 120 BPM, so a parked finger cannot
		/// catch two slides at once.
		/// </remarks>
		public const float SlideLateWindowSeconds = OpenSekai.Shared.Live.ReferenceFrame.Seconds;

		public ConnectionNote()
		{
			Category = NoteCategory.Connection;
		}

		public ConnectionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, float speedRatio, float laneOffset, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			LineType = lineType;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			this.offsetTime = offsetTime;
			if (State == NoteState.Done)
			{
				return;
			}

			if (UsesNextRushContactWindow)
			{
				float previous = OffsetJudgeTime;
				OffsetJudgeTime = currentFrameInfo.time - MusicScoreInfo.time;
				if (State == NoteState.Wait) State = NoteState.Scrolling;
				Progress = CalcProgress(currentFrameInfo, offsetTime);
				// Give contact on the crossing frame a chance before using the recorded early touch.
				if (HasNextRushContact && (previous > 0f || OffsetJudgeTime >= NextRushContactWindow))
					FinishNextRushContact(NoteResult.JustPerfect);
				else if (OffsetJudgeTime >= NextRushContactWindow)
					FinishNextRushContact(NoteResult.Miss);
				return;
			}

			// Decoration connection notes should not be marked as Last/Miss
			// Check parent note's decoration status
			bool isDecorationConnection = IsDecoration || (parentNote != null && parentNote.IsDecoration);

			if (State == NoteState.Last)
			{
				if (!isDecorationConnection)
				{
					// A slide is judged by the frame its time passes, but the finger arrives a few
					// milliseconds later (and at 165 Hz one frame is only ~6 ms). Keep the note
					// judgeable for a short late window instead of failing it on the next Excute;
					// the window stays shorter than a 16th note at 120 BPM (125 ms) so a parked
					// finger cannot catch two slides at once.
					OffsetJudgeTime = currentFrameInfo.time - MusicScoreInfo.time;
					if (OffsetJudgeTime > SlideLateWindowSeconds)
					{
						// A skipped connection is the hidden kind of interior point: it shapes the bar and
						// is never judged, so it ends as Pass instead of failing. It still has to end, or
						// it would keep sliding down the line forever.
						JudgeInfo = IsSkip
							? (NoteResult.Pass, NoteResultDescription.None)
							: (NoteResult.Miss, NoteResultDescription.None);
						State = NoteState.Done;
					}
				}
				return;
			}

			if (State == NoteState.Wait)
			{
				State = NoteState.Scrolling;
			}

			Progress = CalcProgress(currentFrameInfo, offsetTime);
			// Decoration connection notes should not be marked as Last
			if (MusicScoreInfo.time <= currentFrameInfo.time && !isDecorationConnection)
			{
				State = NoteState.Last;
			}
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (UsesNextRushContactWindow)
				return State != NoteState.Done && JudgeLaneStart <= lane && JudgeLaneEnd >= lane
					&& UnityEngine.Mathf.Abs(OffsetJudgeTime) < NextRushContactWindow
					&& touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled;

			return State == NoteState.Last && JudgeLaneStart <= lane && JudgeLaneEnd >= lane
				&& (_endpointSection == null || !_endpointSection.PublishesHeadMiss || _endpointSection.Head.IsSettled);
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			if (UsesNextRushContactWindow)
			{
				if (!IsJudgment(ref touch, lane)) return false;
				HasNextRushContact = true;
				if (OffsetJudgeTime < 0f) return false;
				FinishNextRushContact(NoteResult.JustPerfect);
				return true;
			}

			if (State == NoteState.Last)
			{
				OffsetJudgeTime = 0f;
				Progress = 1f;
				JudgeInfo = (NoteResult.JustPerfect, NoteResultDescription.None);
				State = NoteState.Done;
				return true;
			}

			// Not in its late window yet: the press is not for this slide.
			return true;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			HasNextRushContact = false;
		}

		private void FinishNextRushContact(NoteResult result)
		{
			if (result == NoteResult.JustPerfect) Progress = 1f;
			JudgeInfo = (result, NoteResultDescription.None);
			State = NoteState.Done;
		}

		public override void Terminate()
		{
			if (State == NoteState.Done)
			{
				return;
			}

			// A hidden point changes the bar's shape only: ending the bar may not award it a hit.
			JudgeInfo = IsSkip
				? (NoteResult.Pass, NoteResultDescription.None)
				: (NoteResult.JustPerfect, NoteResultDescription.None);
			State = NoteState.Done;
		}

		protected override void OnSpawnNote()
		{
			// The hidden kind shapes the bar and draws nothing: giving it a view is what put a diamond
			// on screen that nobody was meant to press.
			if (IsSkip)
			{
				return;
			}

			base.OnSpawnNote();
		}

		protected override void PublishResult()
		{
			// The hidden kind never speaks: no judgement text, no sound, no score.
			if (IsSkip)
			{
				return;
			}

			base.PublishResult();
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			OffsetJudgeTime = 0f;
			Progress = 1f;
			JudgeInfo = (NoteResult.Auto, NoteResultDescription.None);
			State = NoteState.Done;
			return true;
		}
	}
}
