namespace Sekai.Live
{
	public abstract class HoldPointNote : NoteBase
	{
		private bool _hasContact;
		private readonly bool _alwaysHeld;
		private float _dueAt = -1f;

		internal abstract bool UsesHoldPointJudgment { get; }
		private bool CanJudge => UsesHoldPointJudgment && State != NoteState.Done
			&& !IsDecoration && parentNote?.IsDecoration != true;

		protected HoldPointNote() { }

		protected HoldPointNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category,
			float speedRatio, float laneOffset, NoteType type, bool alwaysHeld = false)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
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
			if (State != NoteState.Done && now >= target + ConnectionNote.CONTACT_WINDOW_SECONDS)
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
			FinishHoldPoint(result);
		}

		protected virtual void FinishHoldPoint(NoteResult result) => FinishWith(result);

	}
}
