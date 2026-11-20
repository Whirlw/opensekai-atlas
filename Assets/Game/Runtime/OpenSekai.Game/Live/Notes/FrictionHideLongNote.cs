namespace Sekai.Live
{
	public class FrictionHideLongNote : FrictionLongNote
	{
		private FrictionNote _nextRushTraceHead;
		private FrictionFlickNote _nextRushTraceFlickHead;
		private FlickNote _nextRushFlickHead;
		private NoteBase _nextRushReleaseOwner;

		public void SetNextRushReleaseOwner(NoteBase head) => _nextRushReleaseOwner = head;
		public void SetNextRushTraceHead(FrictionNote head) => _nextRushTraceHead = head;
		public void SetNextRushTraceFlickHead(FrictionFlickNote head) => _nextRushTraceFlickHead = head;
		public void SetNextRushFlickHead(FlickNote head) => _nextRushFlickHead = head;
		internal override bool IsReleaseTailReady => (_nextRushReleaseOwner == null || _nextRushReleaseOwner.Result != NoteResult.None)
			&& (_nextRushTraceHead == null || _nextRushTraceHead.Result != NoteResult.None)
			&& (_nextRushTraceFlickHead?.CanStartNextRushHold ?? true)
			&& (_nextRushFlickHead?.CanStartNextRushHold ?? true);

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
				SettleComboPointsAtEnd();
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
			: this(info, id, laneStart, laneEnd, category, type, LiveUtility.GetLaneOffset(category, bundleBuildData), speedRatio, lineType) { }

		public FrictionHideLongNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, float laneOffset, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, type, laneOffset, speedRatio, lineType)
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
			if (_nextRushTraceFlickHead != null && (MusicScore.CurrentFrameInfo.time < MusicScoreInfo.time
				|| _nextRushTraceFlickHead.IsTailFlickOwner && !_nextRushTraceFlickHead.CanStartNextRushHold)) return false;
			if (_nextRushReleaseOwner != null && (MusicScore.CurrentFrameInfo.time < MusicScoreInfo.time
				|| _nextRushReleaseOwner is NormalNote { TakesPress: true, Result: NoteResult.None })) return false;
			// A press arms a flick head. After a miss, the player can still pick up the body.
			if (_nextRushFlickHead != null
				&& (MusicScore.CurrentFrameInfo.time < MusicScoreInfo.time || !_nextRushFlickHead.CanStartNextRushHold))
				return false;
			if (_nextRushTraceHead != null)
			{
				float now = MusicScore.CurrentFrameInfo.time;
				if (now < MusicScoreInfo.time || (!_nextRushTraceHead.HasRecordedContact
					&& now <= _nextRushTraceHead.MusicScoreInfo.time + 0.125f)) return false;
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
