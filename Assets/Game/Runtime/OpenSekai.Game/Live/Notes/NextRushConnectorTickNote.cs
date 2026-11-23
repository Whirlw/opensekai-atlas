using System.Collections.Generic;

namespace Sekai.Live
{
	// With an explicit owner, the head gates input while geometry supplies the touch position.
	public class NextRushConnectorTickNote : ConnectionNote
	{
		private NoteBase _owner;
		private bool _isAttached;
		private List<LongNote> _outgoingHolds;
		private float _pendingAt = float.PositiveInfinity;

		public NextRushConnectorTickNote(MusicScoreInfo info, int id, int left, int right, NoteType type, float laneOffset)
			: base(info, id, left, right, NoteCategory.Connection, type, 1, laneOffset) { }

		public void SetConnectorOwner(NoteBase owner, bool isAttached)
		{
			_owner = owner;
			_isAttached = isAttached;
			SetNextRushContactWindow();
		}

		public void AddOutgoingHold(LongNote bar) => (_outgoingHolds ??= new List<LongNote>()).Add(bar);

		public override void Excute(MusicScoreInfo frame, float offset)
		{
			if (State == NoteState.Done) return;
			if (frame.time >= MusicScoreInfo.time && _outgoingHolds != null)
			{
				foreach (var bar in _outgoingHolds)
				{
					if (bar is GuideNote { IsFakeHold: true } || _owner == null && bar.HasFinger)
					{
						FinishContact(NoteResult.Perfect);
						return;
					}
				}
			}
			if (_owner == null)
			{
				base.Excute(frame, offset);
				return;
			}
			OffsetJudgeTime = frame.time - MusicScoreInfo.time;
			if (State == NoteState.Wait) State = NoteState.Scrolling;
			Progress = CalcProgress(frame, offset);
			if (OffsetJudgeTime < 0) return;
			if (frame.time > _pendingAt) FinishContact(NoteResult.Miss);
			else _pendingAt = frame.time;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (_owner == null) return base.IsJudgment(ref touch, lane);
			if (State == NoteState.Done || float.IsPositiveInfinity(_pendingAt)
				|| touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled
				|| _owner is LongNote bar && bar.PublishesHeadMiss && !bar.Head.IsSettled
				|| _owner is NormalNote { TakesPress: true, HasJudgment: true, Result: NoteResult.None }
				|| _owner is FrictionFlickNote { IsTailFlickOwner: true, CanStartNextRushHold: false }
				|| _owner is FlickNote { HasJudgment: true } flick && (_isAttached || flick.IsNextRushHead)
					&& !flick.CanStartNextRushHold) return false;
			return ContainsConnectorInput(ref touch, lane, _pendingAt);
		}

		public override void ResetNote()
		{
			base.ResetNote();
			_pendingAt = float.PositiveInfinity;
		}

	}
}
