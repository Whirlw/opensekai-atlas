using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public class ConnectionNote : HoldPointNote
	{
		internal const float CONTACT_WINDOW_FRAMES = 5f;
		internal const float CONTACT_WINDOW_SECONDS = CONTACT_WINDOW_FRAMES / 60f;
		private LongNote _endpointSection;
		private NextRushTickPathPoint _connectorInputHead;
		private NextRushTickPathPoint _connectorInputTail;

		internal bool UsesNextRushContactWindow { get; private set; }
		private float _crossingFrame;
		private LongNote TickOwner => _endpointSection ?? parentNote;
		private bool IsVisibleTick => Category == NoteCategory.Connection && IsVisible && !IsSkip && HasJudgment;
		internal override bool UsesHoldPointJudgment => IsVisibleTick && !UsesNextRushJudgment && !UsesContactWindow
			&& parentNote is { IsDecorativeBar: false, NativeConnector: null }
			&& ParentRole == NoteParentRole.Connection;
		internal bool IsConnectedVisibleTick => IsVisibleTick && !UsesContactWindow && TickOwner != null
			&& !UsesHoldPointJudgment;
		internal virtual bool UsesContactWindow => UsesNextRushContactWindow
			|| !UsesNextRushJudgment && Category == NoteCategory.Connection && TickOwner == null && !IsDecoration;
		internal virtual float EarlyContactSeconds => CONTACT_WINDOW_SECONDS;
		internal virtual float LateContactSeconds => CONTACT_WINDOW_SECONDS;
		internal bool AcceptsContactAt(float now, float target) =>
			now > target - EarlyContactSeconds && now < target + LateContactSeconds;
		internal bool HasRecordedContact { get; private set; }

		public void SetNextRushContactWindow() => UsesNextRushContactWindow = true;

		public void SetConnectorInputPath(NextRushTickPathPoint head, NextRushTickPathPoint tail)
		{
			_connectorInputHead = head;
			_connectorInputTail = tail;
		}

		protected bool ContainsConnectorInput(ref LiveTouch touch, float lane, float now) =>
			_connectorInputHead != null
				? _connectorInputHead.Contains(_connectorInputTail,
					touch.PreviousFrameMusicTime ?? (now - OpenSekai.Shared.Live.ReferenceFrame.SECONDS), now, lane)
				: JudgeLaneStart <= lane && lane <= JudgeLaneEnd;

		public void SetNextRushEndpointSection(LongNote section)
		{
			_endpointSection = section;
		}

		// Hidden geometry markers retain a reference frame before retiring.
		public const float SLIDE_LATE_WINDOW_SECONDS = OpenSekai.Shared.Live.ReferenceFrame.SECONDS;

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

			if (UsesHoldPointJudgment)
			{
				float time = currentFrameInfo.time;
				OffsetJudgeTime = time - MusicScoreInfo.time;
				if (State != NoteState.Wait || time >= MusicScoreInfo.time - offsetTime)
				{
					if (State == NoteState.Wait) State = NoteState.Scrolling;
					Progress = CalcProgress(currentFrameInfo, offsetTime);
				}
				base.Excute(currentFrameInfo, offsetTime);
				return;
			}

			if (SettleFromCompletedTail()) return;

			if (TickOwner is GuideNote { IsFakeHold: true })
			{
				AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: false);
				if (currentFrameInfo.time >= MusicScoreInfo.time) FinishContact(NoteResult.Perfect);
				return;
			}

			if (UsesContactWindow)
			{
				OffsetJudgeTime = currentFrameInfo.time - MusicScoreInfo.time;
				if (State == NoteState.Wait) State = NoteState.Scrolling;
				Progress = CalcProgress(currentFrameInfo, offsetTime);
				if (HasRecordedContact && currentFrameInfo.time >= MusicScoreInfo.time)
					FinishContact(NoteResult.Perfect);
				else if (currentFrameInfo.time >= MusicScoreInfo.time + LateContactSeconds)
					FinishContact(NoteResult.Miss);
				return;
			}

			// Decoration connection notes should not be marked as Last/Miss
			// Check parent note's decoration status
			bool isDecorationConnection = IsDecoration || (parentNote != null && parentNote.IsDecoration);

			if (State == NoteState.Last)
			{
				if (!isDecorationConnection)
				{
					OffsetJudgeTime = currentFrameInfo.time - MusicScoreInfo.time;
					if (currentFrameInfo.time > _crossingFrame)
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
				_crossingFrame = currentFrameInfo.time;
				State = NoteState.Last;
			}
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (UsesHoldPointJudgment) return false;

			if (UsesContactWindow)
				return State is NoteState.Scrolling or NoteState.Input && JudgeLaneStart <= lane && JudgeLaneEnd >= lane
					&& AcceptsContactAt(MusicScoreInfo.time + OffsetJudgeTime, MusicScoreInfo.time)
					&& touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled;

			return State == NoteState.Last
				&& touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled
				&& (TickOwner == null || !TickOwner.PublishesHeadMiss || TickOwner.Head.IsSettled)
				&& ContainsConnectorInput(ref touch, lane, _crossingFrame);
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			if (UsesHoldPointJudgment) return false;

			if (UsesContactWindow)
			{
				if (!IsJudgment(ref touch, lane)) return false;
				TimingSample ??= MusicScoreInfo.time + OffsetJudgeTime;
				HasRecordedContact = true;
				if (OffsetJudgeTime < 0f && !(LiveConfig.HasVisualLatency && UsesTraceFeedback))
				{
					State = NoteState.Input;
					return false;
				}
				FinishContact(NoteResult.Perfect);
				return true;
			}

			if (IsJudgment(ref touch, lane))
			{
				OffsetJudgeTime = 0f;
				Progress = 1f;
				JudgeInfo = (NoteResult.Perfect, NoteResultDescription.None);
				State = NoteState.Done;
				return true;
			}

			// Not in its late window yet: the press is not for this slide.
			return true;
		}

		internal static bool InContactWindow(float now, float target) =>
			now > target - CONTACT_WINDOW_SECONDS && now < target + CONTACT_WINDOW_SECONDS;

		internal bool SettleFromCompletedTail()
		{
			if (State == NoteState.Done || UsesHoldPointJudgment || !IsVisibleTick
				|| TickOwner?.LinkedPointTail is not { State: NoteState.Done })
				return false;
			FinishContact(NoteResult.Perfect);
			return true;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			_crossingFrame = 0f;
			HasRecordedContact = false;
		}

		protected void FinishContact(NoteResult result)
		{
			if (result == NoteResult.Perfect) Progress = 1f;
			FinishWith(result);
		}

		protected override void FinishHoldPoint(NoteResult result) => FinishContact(result);

		public override void Terminate()
		{
			if (UsesHoldPointJudgment)
			{
				base.Terminate();
				return;
			}

			if (State == NoteState.Done)
			{
				return;
			}

			// A hidden point changes the bar's shape only: ending the bar may not award it a hit.
			JudgeInfo = IsSkip
				? (NoteResult.Pass, NoteResultDescription.None)
				: (NoteResult.Perfect, NoteResultDescription.None);
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
