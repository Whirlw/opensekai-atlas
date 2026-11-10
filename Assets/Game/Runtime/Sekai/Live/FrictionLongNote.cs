namespace Sekai.Live
{
	public class FrictionLongNote : LongNote
	{
		// This bar ends in a friction note: the friction note's own judgement finishes it, not a release.
		protected bool isTouched;

		protected virtual NoteResult CompletedTouchResult => NoteResult.JustPerfect;

		public FrictionLongNote()
		{
			Category = NoteCategory.FrictionLong;
		}

		public FrictionLongNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, LiveBundleBuildData bundleBuildData, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, type, speedRatio, LiveUtility.GetLaneOffset(category, bundleBuildData), lineType)
		{
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			base.Excute(currentFrameInfo, offsetTime);
			if (State == NoteState.Done)
			{
				return;
			}

			if (isTouched && Progress > 1f)
			{
				// A rubbed bar is being held, so it stops at the judgement line instead of
				// sliding past it while the finger keeps working the lane.
				Progress = 1f;
			}

			if (!Head.IsSettled && State <= NoteState.Last && isTouched)
			{
				if (Progress < 1f)
				{
					return;
				}

				Head.Settle((CompletedTouchResult, NoteResultDescription.None));
				State = NoteState.InputBegan;
			}
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			var left = JudgeInfo.result == NoteResult.None ? JudgeLaneStart : JudgedLaneStart;
			var right = JudgeInfo.result == NoteResult.None ? JudgeLaneEnd : JudgedLaneEnd;
			return State != NoteState.Done
				&& left <= lane
				&& right >= lane
				&& LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), OffsetJudgeTime);
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			// A rubbed bar takes any finger: the whole body is worked by whichever hand is over it,
			// so nothing here is reserved for the finger that arrived first.
			if (State == NoteState.Done)
			{
				return false;
			}

			if (State == NoteState.Release || State == NoteState.InputBegan || State == NoteState.Last)
			{
				State = NoteState.Input;
			}

			isTouched = true;
			RecordHoldingInput(touch.fingerId, touch.musicTime);
			OffsetJudgeTime = 0f;
			ConnectionNoteJudgment(ref touch, lane);

			if (childNote != null
				&& childNote.State != NoteState.Wait
				&& LiveConfig.IsLongEndJudgeTime(childNote.JudgeOffsetAt(touch.musicTime))
				&& childNote.IsJudgment(ref touch, lane)
				&& childNote.Judgment(ref touch, lane))
			{
				State = NoteState.Done;
				return true;
			}

			return true;
		}
	}
}
