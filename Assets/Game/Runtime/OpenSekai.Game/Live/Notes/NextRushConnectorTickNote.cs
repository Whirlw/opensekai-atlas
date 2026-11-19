using System.Collections.Generic;
using UnityEngine;

namespace Sekai.Live
{
	// With an explicit owner, the head gates input while geometry supplies the touch position.
	public class NextRushConnectorTickNote : ConnectionNote
	{
		private NoteBase _owner;
		private NoteBase _next;
		private int _ease;
		private List<LongNote> _outgoingHolds;
		private float _pendingAt = float.PositiveInfinity;
		private NoteBase _from;

		public NextRushConnectorTickNote(MusicScoreInfo info, int id, int left, int right, NoteType type, float laneOffset)
			: base(info, id, left, right, NoteCategory.Connection, type, 1, laneOffset) { }

		public void SetAttachedConnector(NoteBase owner, NoteBase from, NoteBase to, int ease)
		{
			_from = from;
			SetNextRushContactWindow();
			SetConnector(owner, to, ease);
		}

		public void SetConnector(NoteBase owner, NoteBase next, int ease)
		{
			_owner = owner;
			_next = next;
			_ease = ease;
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
						FinishContact(NoteResult.JustPerfect);
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
				|| _owner is FlickNote { HasJudgment: true } flick && (_from != null || flick.IsNextRushHead)
					&& !flick.CanStartNextRushHold) return false;
			float left = JudgeLaneStart, right = JudgeLaneEnd;
			if (_from != null && _next.MusicScoreInfo.time > _from.MusicScoreInfo.time)
			{
				float fraction = Mathf.InverseLerp(_from.MusicScoreInfo.time, _next.MusicScoreInfo.time, _pendingAt);
				float t = NextRushConnectorSegment.Interpolate(_ease, fraction);
				left += Mathf.LerpUnclamped(_from.NativeLeft, _next.NativeLeft, t) - NativeLeft;
				right += Mathf.LerpUnclamped(_from.NativeRight, _next.NativeRight, t) - NativeRight;
			}
			else if (_from == null && _next != null && _next.MusicScoreInfo.time > MusicScoreInfo.time)
			{
				float fraction = Mathf.InverseLerp(MusicScoreInfo.time, _next.MusicScoreInfo.time, _pendingAt);
				float progress = NextRushConnectorSegment.Interpolate(_ease, fraction);
				left = Mathf.LerpUnclamped(left, _next.JudgeLaneStart, progress);
				right = Mathf.LerpUnclamped(right, _next.JudgeLaneEnd, progress);
			}
			return left <= lane && lane <= right;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			_pendingAt = float.PositiveInfinity;
		}

	}
}
