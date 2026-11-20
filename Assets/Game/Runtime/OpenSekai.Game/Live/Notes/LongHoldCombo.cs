namespace Sekai.Live
{
	public class LongHoldCombo : NoteBase
	{
		private readonly bool _alwaysHeld;
		public override bool IsVisible => !HasNativeAttachment;


		// Keep contact recorded on the early side until the point reaches its chart time.
		private bool _hasContact;

		public LongHoldCombo()
		{
			Category = NoteCategory.Combo;
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, float speedRatio, float _laneOffset)
			: base(info, 0, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type)
		{
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, int id, float speedRatio, float _laneOffset,
			bool alwaysHeld = false)
			: base(info, id, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type)
		{
			_alwaysHeld = alwaysHeld;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			if (!CanJudge) return;

			float now = currentFrameInfo.time;
			float target = MusicScoreInfo.time;
			if (now >= target && State != NoteState.Last)
			{
				_dueAt = now;
				State = NoteState.Last;
				Trace("pointDue", now, withCaller: true);
			}

			ObserveBarContact(now);
			if (State != NoteState.Done && now >= target + ConnectionNote.ContactWindowSeconds)
				Finish(NoteResult.Miss, "pointMiss", now);
		}

		// Called both after advancing the body and when this frame's input changes its hold state.
		// An early contact is retained even if the bar leaves its grace period before the point arrives.
		internal void ObserveBarContact(float now)
		{
			if (!CanJudge) return;

			float target = MusicScoreInfo.time;
			if (ConnectionNote.InContactWindow(now, target)
				&& parentNote is LongNote bar
				&& (bar.State is NoteState.InputBegan or NoteState.Input or NoteState.Last
					|| (bar.State == NoteState.Release && bar.HasFinger)))
			{
				_hasContact = true;
			}

			if ((_hasContact || _alwaysHeld) && now >= target)
				Finish(NoteResult.JustPerfect, "pointHit", now);
		}

		private float _dueAt = -1f;
		private bool CanJudge => State != NoteState.Done && !IsDecoration && parentNote?.IsDecoration != true;

		public override void ResetNote()
		{
			base.ResetNote();
			_dueAt = -1f;
			_hasContact = false;
		}

		private void Trace(string what, float now, bool withCaller = false)
		{
			if (!OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge))
			{
				return;
			}

			var bar = parentNote as LongNote;
			string caller = withCaller ? " by=" + CallerOf(3) : string.Empty;
			OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge,
				$"{what} id={Id} t={MusicScoreInfo.time:F3} now={now:F3} frame={MusicScore.CurrentFrameInfo.time:F3} dueAt={_dueAt:F3}"
				+ $" bar={(bar == null ? -1 : bar.Id)}@{(bar == null ? -1f : bar.MusicScoreInfo.time):F3} barState={bar?.State} barFinger={bar?.HasFinger}"
				+ $" barLanes={(bar == null ? 0f : bar.BodyLaneStart):F2}..{(bar == null ? 0f : bar.BodyLaneEnd):F2} inPlay={_hasContact}{caller}");
		}

		private static string CallerOf(int depth)
		{
			System.Diagnostics.StackFrame frame = new System.Diagnostics.StackTrace().GetFrame(depth);
			System.Reflection.MethodBase method = frame?.GetMethod();
			return method == null ? "?" : method.DeclaringType?.Name + "." + method.Name;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane) => false;

		internal void SettleFromTail(NoteBase tail, float now)
		{
			if (!CanJudge || Result != NoteResult.None) return;

			bool tailHasInput = tail.HasSlideEndInputAt(now);
			if (tail.State != NoteState.Done && (!tailHasInput || now < MusicScoreInfo.time)) return;

			if (_hasContact || _alwaysHeld)
				Finish(NoteResult.JustPerfect, "pointTerminateHit", now);
			else if (tailHasInput)
				Finish(NoteResult.Perfect, "pointTailHit", now);
			else
				Finish(NoteResult.Miss, "pointTerminateMiss", now);
		}

		public override void Terminate()
		{
			if (!CanJudge) return;

			float now = MusicScore.CurrentFrameInfo.time;
			ObserveBarContact(now);
			if (State != NoteState.Done)
			{
				bool hit = _hasContact || _alwaysHeld;
				Finish(hit ? NoteResult.JustPerfect : NoteResult.Miss, hit ? "pointTerminateHit" : "pointTerminateMiss", now);
			}
		}

		private void Finish(NoteResult result, string trace, float now)
		{
			Trace(trace, now);
			JudgeInfo = (result, NoteResultDescription.None);
			State = NoteState.Done;
		}

		protected override void OnSpawnNote()
		{
		}

		protected override void PublishResult()
		{
			// Recycling an unanswered marker must not publish a score or a combo event.
			if (JudgeInfo.result == NoteResult.None)
			{
				return;
			}

			ResultPublication.Publish(this, Callbacks, this, PublishShape.InteriorPoint);
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
