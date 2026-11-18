namespace Sekai.Live
{
	public class FrictionHideLongNote : FrictionLongNote
	{
		private FrictionNote _nextRushTraceHead;

		public void SetNextRushTraceHead(FrictionNote head) => _nextRushTraceHead = head;

		// This bar ends in a friction note: the friction note's own judgement finishes it, not a release.
		protected override NoteResult CompletedTouchResult => NoteResult.Pass;

		public override bool HasJudgment
		{
			get
			{
				return false;
			}
		}

		public override NoteState State
		{
			get
			{
				return state;
			}
			protected set
			{
				if (value == NoteState.Done && state == NoteState.Done) return;
				if (value == NoteState.Done)
				{
					WasHoldingWhenTerminated = state == NoteState.Scrolling
						|| state == NoteState.Last
						|| state == NoteState.InputBegan
						|| state == NoteState.Input;
					state = NoteState.Done;
					OnUnSpawnNote();
					return;
				}

				state = value;
				if (value == NoteState.Scrolling)
				{
					OnSpawnNote();
				}
			}
		}

		public FrictionHideLongNote()
		{
			Category = NoteCategory.FrictionHideLong;
		}

		public FrictionHideLongNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, LiveBundleBuildData bundleBuildData, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, type, bundleBuildData, speedRatio, lineType)
		{
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			base.Excute(currentFrameInfo, offsetTime);
			if (State == NoteState.Done)
			{
				return;
			}

			if (!Head.IsSettled && State <= NoteState.Last && isTouched)
			{
				if (Progress < 1f)
				{
					return;
				}

				Head.Settle((NoteResult.Pass, NoteResultDescription.None));
				State = NoteState.InputBegan;
			}
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			return base.Judgment(ref touch, lane);
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (NativeConnector == null) return base.IsJudgment(ref touch, lane);
			if (_nextRushTraceHead != null)
			{
				float elapsed = MusicScore.CurrentFrameInfo.time - MusicScoreInfo.time;
				if (elapsed < 0f || (!_nextRushTraceHead.HasNextRushContact && elapsed <= 0.125f)) return false;
			}
			return State != NoteState.Done && ContainsLane(lane)
				&& LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), OffsetJudgeTime);
		}

	protected override void OnSpawnNote()
	{
		onSpawn?.Invoke(this);
	}

		protected override void OnUnSpawnNote()
		{
			ConnectionNoteTerminate();
			RemoveView();
		}
	}
}
