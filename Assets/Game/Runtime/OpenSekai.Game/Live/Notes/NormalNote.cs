using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public class NormalNote : NoteBase
	{
		private readonly bool _isNextRushTap;
		internal bool TakesPress => Category == NoteCategory.Normal || _isNextRushTap;
		internal bool IsIndependentRelease { get; private set; }
		public bool IsTailReleaseOwner { get; }
		private NoteBase _releaseOwner;

		public void SetNextRushIndependentRelease(NoteBase releaseOwner = null)
		{
			_releaseOwner = releaseOwner;
			IsIndependentRelease = true;
			Category = NoteCategory.Long;
		}

		public override NoteState State
		{
			get => state;
			protected set
			{
				if (value == NoteState.Done && state != NoteState.Done)
				{
					state = NoteState.Done;
					// Publish first, then tell the bar: the ordinary end note ends its bar through its
					// verdict, not before it (tail-classes.md §3 R2).
					OnUnSpawnNote();
					NotifyParentFinished();
					return;
				}
				base.State = value;
			}
		}
		public NormalNote()
		{
			Type = NoteType.Default;
			Category = NoteCategory.Normal;
		}

		public NormalNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio, NoteType type, float laneOffset,
			bool isNextRushTap = false, bool isTailReleaseOwner = false)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			_isNextRushTap = isNextRushTap;
			IsTailReleaseOwner = isTailReleaseOwner;
		}

		public override void SetParentNote(LongNote note)
		{
			base.SetParentNote(note);
			Type = (note != null && note.Type == NoteType.Critical) || Type == NoteType.Critical
				? NoteType.Critical
				: NoteType.Default;
			Category = NoteCategory.Long;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done || JudgeLaneStart > lane || JudgeLaneEnd < lane)
			{
				return false;
			}

			var offset = MusicScoreInfo.time - touch.musicTime;
			if (IsIndependentRelease)
			{
				return (_releaseOwner == null || _releaseOwner.Result != NoteResult.None
					|| _releaseOwner is FrictionFlickNote { HasCapturedPress: true } or FlickNote { CanStartNextRushHold: true }
					|| _releaseOwner is LongNote hold && hold.Head.IsSettled)
					&& touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && LongEndJudgement.Accepts(-offset);
			}

			if (!LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), offset))
			{
				return false;
			}

			return TakesPress ? touch.phase == UnityEngine.InputSystem.TouchPhase.Began
				: Category == NoteCategory.Long && touch.phase == UnityEngine.InputSystem.TouchPhase.Ended;
		}

		internal void SettleExpiredPress(float now)
		{
			if (LiveConfig.IsPastJudgeWindow(LiveUtility.GetINoteToJudgeFrameType(this), MusicScoreInfo.time - now))
			{
				FinishWith(NoteResult.Miss);
			}
		}

		internal void SettleExpiredRelease(float now)
		{
			if (LongEndJudgement.IsPast(now - MusicScoreInfo.time))
			{
				FinishWith(NoteResult.Miss);
			}
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done)
			{
				return false;
			}

			if (IsIndependentRelease)
			{
				if (!IsJudgment(ref touch, lane)) return false;
				HasNextRushHit = IsNextRush;
				TimingSample = touch.musicTime;
				FinishWith(LongEndJudgement.EvaluateHit(touch.musicTime - MusicScoreInfo.time));
				return true;
			}

			if ((TakesPress && touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
				|| (!TakesPress && Category == NoteCategory.Long && touch.phase != UnityEngine.InputSystem.TouchPhase.Ended))
			{
				return false;
			}

			TimingSample = touch.musicTime;
			var judgeInfo = GradesAsBarPart
				? LiveConfig.CalculateLongEndNoteResult(this, touch.musicTime)
				: LiveConfig.CalculateDefaultNoteResult(this, touch.musicTime);
			HasNextRushHit = IsNextRush;

			JudgeInfo = onUpdateResult != null ? onUpdateResult(judgeInfo) : judgeInfo;
			State = NoteState.Done;
			return true;
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			if (IsIndependentRelease) return LongEndJudgement.Evaluate(musicTime - MusicScoreInfo.time);

			return (GradesAsBarPart
				? LiveConfig.CalculateLongEndNoteResult(this, musicTime)
				: LiveConfig.CalculateDefaultNoteResult(this, musicTime)).Item1;
		}
	}
}
