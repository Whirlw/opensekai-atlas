using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Live.Tails;

namespace Sekai.Live
{
	public partial class LongNote : NoteBase
	{
		protected float lastInputTime;
		private HoldBody _body;
		private HoldBody Body => _body ??= new HoldBody(this);

		public bool WasHoldingWhenTerminated
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
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
					// The bar shares its fate with its interior points and nothing else: whatever point the
					// bar's own end made unreachable is settled Perfect here, so the bar leaving can never
					// leave a marker unjudged. A point that already has a verdict keeps it (its own Miss,
					// its own hit), which is why the settle only touches what is still open.
					SettleLeftoverAsPerfect();
					WasHoldingWhenTerminated = HasFinger;
					state = value;
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

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime) =>
			AdvanceBar(currentFrameInfo, offsetTime, deferPressExpiry: false);

		internal void AdvanceBeforeInput(MusicScoreInfo frame, float offsetTime) =>
			AdvanceBar(frame, offsetTime, deferPressExpiry: true);

		private void AdvanceBar(MusicScoreInfo currentFrameInfo, float offsetTime, bool deferPressExpiry)
		{
			this.offsetTime = offsetTime;
			Body.BeginFrame(currentFrameInfo.time);
			if (State == NoteState.Done)
			{
				return;
			}

			var prevState = State;
			var prevResult = Result;
			if (State == NoteState.Wait)
			{
				State = NoteState.Scrolling;
			}

			// An unsettled head in Last still follows the legacy head-miss path.
			// A held bar has already settled its head and leaves Last through the timed grace below.
			if (State == NoteState.Last && !Head.IsSettled && !Body.HeadTouched)
			{
				State = NoteState.Release;
			}

			Progress = CalcProgress(currentFrameInfo, offsetTime);

			if (NoteList != null)
			{
				for (var i = 1; i < NoteList.Count; i++)
				{
					var note = NoteList[i];
					var childOffsetTime = CalcTimeOffset?.Invoke(note) ?? offsetTime;
					if (currentFrameInfo.time < note.MusicScoreInfo.time - childOffsetTime)
					{
						break;
					}

					// A judged end note that judges itself has its own clock in the session (LiveNoteSession
					// .TickTails): ticking it here as well would run it twice, and it must keep ticking
					// after this bar has left. Only the bar's own children are ticked from here.
					if (ReferenceEquals(note, TailNote) && HasIndependentTail)
					{
						continue;
					}

					if (ReferenceEquals(note, TailNote) && (TailBehaviour.AcceptsRelease || TailBehaviour.AcceptsSwipe))
					{
						// The tail's own pass decides its timeout using the same window as its hit path.
						note.AdvanceNote(currentFrameInfo, childOffsetTime, settleTimeout: false);
					}
					else
					{
						note.Excute(currentFrameInfo, childOffsetTime);
					}
				}
			}

			BarView.UpdateLane(this, ref currentFrameInfo);

			// The head has its own judgement: it is settled once, when its window closes, and whether
			// the bar is lit or grey afterwards has nothing to do with it. A hidden head was never a
			// note the player could answer, so it settles silently.
			if (!deferPressExpiry)
			{
				Head.SettleExpiredHead(currentFrameInfo.time);
			}

			Body.Update(currentFrameInfo.time);

			if ((prevState != State || prevResult != Result) && LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"barExcute type={GetType().Name} cat={Category} barT={MusicScoreInfo.time:F3} now={currentFrameInfo.time:F3} prev={prevState}/{prevResult} next={State}/{Result} childT={(childNote == null ? 0f : childNote.MusicScoreInfo.time):F3} childState={(childNote == null ? NoteState.Wait : childNote.State)}");
			}
		}

		public virtual void ForceTerminate(NoteBase by = null)
		{

			if (State != NoteState.Done)
			{
				// An independent end note does not end its bar, in either direction: its Done only means
				// its own verdict is in. The bar leaves on its own time, through the tail's detach
				// (tail-classes.md §3 类 1). Only a decisive end - a flick or an ordinary one - may end it.
				if (by != null && by.ParentRole == NoteParentRole.Tail && TailBehaviour.JudgesItself)
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Hold))
					{
						LiveTrace.Write(LiveTraceCategory.Hold, $"forceTermIndependentTail type={by.GetType().Name} res={by.Result} barT={MusicScoreInfo.time:F3}");
					}

					return;
				}

				// Only the tail may end the bar. A connection note, a combo or a plain note that we
				// carry becomes Done on its own schedule; letting that finish the whole bar is how a
				// bar could be judged the moment it appeared, long before the head reached the line.
				if (by != null && by.ParentRole != NoteParentRole.Tail && !ReferenceEquals(by, TailNote))
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Hold))
					{
						LiveTrace.Write(LiveTraceCategory.Hold, $"forceTermRejected role={by.ParentRole} type={by.GetType().Name} barT={MusicScoreInfo.time:F3}");
					}

					return;
				}

				// The head has not even been reached yet: nothing on this bar may finish it. A swipe
				// that lands on the tail note while the bar is still playing must not award a Miss
				// for a bar the player never had a chance to hold.
				if (by != null && (State == NoteState.Wait || (State == NoteState.Scrolling && !HasFinger)))
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Hold))
					{
						LiveTrace.Write(LiveTraceCategory.Hold, $"forceTermIgnored by={CallerName(1)} barT={MusicScoreInfo.time:F3} childT={(childNote == null ? 0f : childNote.MusicScoreInfo.time):F3} barState={State}");
					}

					return;
				}

				if (childNote != null && LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"forceTerm by={CallerName(1)} barT={MusicScoreInfo.time:F3} childT={childNote.MusicScoreInfo.time:F3} childRes={childNote.Result} childState={childNote.State} barState={State}");
				}

				// The bar stops being played here; the head is not settled by that. It answers to its
				// own window (see LongNoteHead.SettleExpiredHead), so a bar ended early by its tail
				// leaves the head to close itself.
				JudgeInfo = childNote != null
					&& (childNote.Result > NoteResult.Pass || childNote.State == NoteState.Input)
						? (NoteResult.JustPerfect, NoteResultDescription.None)
						: (NoteResult.Miss, NoteResultDescription.None);
				State = NoteState.Done;
			}
		}

		internal TailKeyPhase GetTailKeyPhase(float currentTime) => BarView.GetTailKeyPhase(this, currentTime);

		protected override void OnUnSpawnNote()
		{
			BarView.Recycle(this);
		}

		/// <summary>
		/// True while a finger is on this bar, whatever phase that finger is in. The touch id is the
		/// truth; the InputBegan/Input states are its second spelling and are on their way out.
		/// </summary>
		public bool HasFinger => _body?.HasFinger ?? false;

		/// <summary>
		/// True once a finger has ever taken this bar. The hold itself goes stale and the bar greys
		/// out, but the bar stays marked as one the player engaged with - that is what keeps it in
		/// the refresh pass until its tail resolves, rather than being dropped the moment it greys.
		/// </summary>
		public bool WasTouched => _body?.WasTouched ?? false;

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			// The head answers the press. The bar only forwards, so the entry the candidate list will
			// call (the head's own) is the same one every existing caller already reaches.
			return Head.Judgment(ref touch, lane);
		}

		/// <summary>
		/// The resident pass's entry: the body, points and tail, never the head. A head is an instant key
		/// the press channel answers with the finger's single press credit. Reaching it from here handed the
		/// same Began sample to every bar under the finger after the press channel had already spent it on
		/// another note - one tap judged a tap note and two heads in the same frame (device trace: touch
		/// 1781 at 11.270 graded tap 13981 Great, then heads 13984 Good and 13982 Perfect).
		/// </summary>
		internal virtual bool DriveResidentFrame(ref LiveTouch touch, float lane) =>
			DriveJudgmentFrame(ref touch, lane, judgeHead: false);

		/// <summary>Feeds this bar one input frame: head, hold facts, frame stamp, points and tail.</summary>
		internal bool DriveJudgmentFrame(ref LiveTouch touch, float lane, bool judgeHead = true)
		{
			// A bar is driven only by a finger inside its own judge range. The candidate pipe gates on
			// LongNote.IsJudgment, but this entry is also reached from the head's candidate and from the hold
			// pass, and the device traces show a finger at lane 8.85 driving a bar whose range is -1.50..4.50
			// (146 of 170 grabs in one run). That is how a bar nobody is touching gets a finger, keeps
			// refreshing, and has its end graded by the release of a finger in another lane.
			if (lane < JudgeLaneStart || lane > JudgeLaneEnd)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"grabRejected barT={MusicScoreInfo.time:F3} lane={lane:F2} range={JudgeLaneStart:F2}..{JudgeLaneEnd:F2} finger={touch.fingerId} phase={touch.phase}");
				}

				return false;
			}

			// The head answers here, before anything about the bar's own life is consulted. The head is an
			// instant key with its own window, and the bar sticking it to the body is only a picture: whether
			// the bar is still playing, released, greyed or already finished says nothing about the head.
			// The head's own guard is IsSettled, so a settled head is never judged twice.
			bool headTaken = judgeHead && Head.TryJudge(ref touch, lane);

			// Everything below is the body's business, and a finished bar has none: no hold, no points.
			if (State == NoteState.Done)
			{
				return headTaken;
			}

			// The hold is the body's. A finger resting in the lane does not decide an ordinary head (the
			// head is judged by a press, through the press channel), but it does keep the bar held and its
			// points refreshing - returning early here left a held bar with no finger at all, which is how a
			// hold could grey out or vanish from the resident pass.
			RecordHoldingInput(MusicScore.CurrentFrameInfo.time);
			RestoreHeldState();
			MarkHeadTouched();
			MarkPressedThisFrame();
			if (LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"grab barT={MusicScoreInfo.time:F3} lane={lane:F2} range={JudgeLaneStart:F2}..{JudgeLaneEnd:F2} finger={touch.fingerId} phase={touch.phase} head={headTaken}");
			}

			ClearHeadJudgeOffset();
			ConnectionNoteJudgment(ref touch, lane);

			// A tail that judges itself takes its own turn in the candidate list, so pressing near its
			// time must not judge it through the bar as well: that would be two judges for one note.
			// The note that counts is the one the chart bound as this bar's end (TailNote), never the
			// last child in the list: a hidden interior point may be appended after it.
			NoteBase pressTail = TailNote;
			if (!HasIndependentTail && !TailBehaviour.JudgesItself
				&& pressTail != null
				&& LiveConfig.IsLongEndJudgeTime(pressTail.JudgeOffsetAt(touch.musicTime))
				&& pressTail.IsJudgment(ref touch, lane)
				&& pressTail.Judgment(ref touch, lane))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"headPressChild barT={MusicScoreInfo.time:F3} touchT={touch.musicTime:F3} childT={pressTail.MusicScoreInfo.time:F3}");
				}

				State = NoteState.Done;
				return true;
			}

			// Being held: the bar is kept alive by its own refresh pass, so the finger can still serve
			// a note that sits on top of it.
			return headTaken;
		}

		/// <summary>
		/// Name of the caller <paramref name="depth"/> frames up, for trace lines only. Capturing a
		/// stack trace is expensive, so this is only ever reached with tracing on.
		/// </summary>
		private static string CallerName(int depth)
		{
			System.Diagnostics.StackFrame frame = new System.Diagnostics.StackTrace().GetFrame(depth);
			return frame == null || frame.GetMethod() == null
				? "?"
				: frame.GetMethod().DeclaringType.Name + "." + frame.GetMethod().Name;
		}

		internal void KeepAlive(float musicTime) => Body.KeepAlive(musicTime);

		/// <summary>
		/// An ordinary bar: a plain long note, not a rub and not a decoration. Its head is where the
		/// body is supposed to shrink from, so it is the only kind that holds its head on the line while
		/// nobody is holding it.
		/// </summary>
		internal bool IsOrdinaryHead => Category == NoteCategory.Long && this is not GuideNote && this is not FrictionLongNote;

		/// <summary>
		/// Whether the bar's body starts at the judgement line instead of flowing past it. Only an ordinary
		/// head parks there: that is the head the player presses and holds, so its body has to shrink from
		/// the line. A rub head plays like a standalone friction note and a guide bar sweeps across the lane,
		/// so both keep travelling past the line instead of freezing on it.
		/// </summary>
		internal bool KeepsHeadOnTheLine => IsOrdinaryHead;

		/// <summary>
		/// Whether the bar's key has left with the head's verdict. A rub head lives like a standalone
		/// friction note: it travels to the line, and the moment it is judged (rubbed, auto-played or
		/// missed) its key disappears while the judgement effect plays. The body goes on without it.
		/// </summary>
		internal bool HeadKeyLeftWithItsVerdict => this is FrictionLongNote && Head.IsSettled;

		/// <summary>
		/// A bar greys when its hold lapses. A returning finger lights it again while the
		/// post-head state still drives grace and tail judgement.
		/// </summary>
		public override bool ShowsAsReleased => !IsAutoPlaying && !HasFinger && (State == NoteState.Release || State == NoteState.Last);

		/// <summary>True while autoplay plays this bar - there is no finger to keep it lit.</summary>
		internal bool IsAutoPlaying { get; set; }

		internal bool PublishesHeadMiss => HeadIsANote;

		/// <summary>
		/// True when this bar's head is a note the player answers: an ordinary head, drawn, and judged.
		/// A hidden head (a skip point, a carrier, a decoration) is not something anybody can press, and
		/// letting the bar be a press candidate for it made the bar light up and grey again whenever the
		/// player pressed the ordinary note sitting on the same lane and time - the "灰一下" report.
		/// </summary>
		internal bool HeadIsANote => IsOrdinaryHead && HasJudgment && !IsSkip;

		internal void SetHeadJudgeInfo((NoteResult result, NoteResultDescription description) value) => JudgeInfo = value;

		internal Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> HeadResultUpdater => onUpdateResult;

		internal void MarkHeadTouched() => Body.MarkHeadTouched();

		internal void ClearHeadJudgeOffset() => OffsetJudgeTime = 0f;

		internal void RecordHoldingInput(float musicTime) => Body.RecordInput(musicTime);
		internal void ClearFinger() => _body?.ClearFinger();
		internal void MarkPressedThisFrame() => Body.MarkPressed();
		private LongNoteHead _head;

		internal LongNoteHead Head => _head ??= new LongNoteHead(this);

		/// <summary>
		/// Releasing or stepping out of the bar keeps the hold alive for this many frames; only after
		/// that does the bar become "released". The chart's own MissMesh is added on top.
		/// </summary>
		internal const int HoldGraceFrames = 3;
		/// <summary>
		/// The same grace as a duration: three frames of the 60 fps judgement tables, so 50 ms on a
		/// 165 Hz phone and on a 60 Hz one alike.
		/// </summary>
		internal static float HoldGraceSeconds => ReferenceFrame.FromFrames(HoldGraceFrames);

		internal void RestoreHeldState() => Body.RestoreHeldState();

		/// <summary>
		/// Refresh a hold that this finger already owns. Interior combo ticks are judged inside
		/// ConnectionNoteJudgment, so a refresh that skipped it would leave the sixteenth-note markers
		/// unjudged even though the hold stays alive.
		/// </summary>
		internal void RefreshHeldInput(ref LiveTouch touch, float lane)
		{
			RecordHoldingInput(touch.musicTime);
			RestoreHeldState();
			ConnectionNoteJudgment(ref touch, lane);
		}

		protected void ConnectionNoteJudgment(ref LiveTouch touch, float lane)
		{
			Head.ConnectionNoteJudgment(ref touch, lane);
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done)
			{
				return false;
			}

			var left = JudgeInfo.result == NoteResult.None ? JudgeLaneStart : JudgedLaneStart;
			var right = JudgeInfo.result == NoteResult.None ? JudgeLaneEnd : JudgedLaneEnd;
			if (left > lane || right < lane)
			{
				return false;
			}

			// Judged through the frame offset on purpose: candidate admission happens after Excute for the
			// same frame, which is the contract this cache stands for.
			return LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), OffsetJudgeTime)
				&& ((HasFinger && State == NoteState.Scrolling) || (State != NoteState.Scrolling && State != NoteState.Last) || touch.phase == UnityEngine.InputSystem.TouchPhase.Began);
		}

		/// <summary>
		/// Lane containment without the judgment timing window. A hold that is already being held has to
		/// keep refreshing its input frame while its finger stays inside the lane, including the frames
		/// between two sixteenth-note combo checks, where <see cref="IsJudgment"/> is closed.
		/// </summary>
		internal bool ContainsLane(float lane)
		{
			var left = JudgeInfo.result == NoteResult.None ? JudgeLaneStart : JudgedLaneStart;
			var right = JudgeInfo.result == NoteResult.None ? JudgeLaneEnd : JudgedLaneEnd;
			return left <= lane && right >= lane;
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			lastInputTime = currentFrameInfo.time;
			IsAutoPlaying = true;

			// A carrier is not a note: it is the lane a rub travels along. Nobody judges its head, so
			// autoplay does not invent a verdict for it either - only the interior points it carries
			// are answered. The note it ends with is a real one and keeps its own say, and the bar
			// still has to leave, or the session would stop walking at it.
			if (!HasJudgment)
			{
				ConnectionNoteAutoJudgment(currentFrameInfo);
				if (childNote != null
					&& (childNote.State == NoteState.Done || childNote.AutoJudgment(currentFrameInfo)))
				{
					State = NoteState.Done;
				}

				return true;
			}

			Head.Settle((NoteResult.Auto, NoteResultDescription.None));
			RestoreHeldState();
			OffsetJudgeTime = 0f;
			ConnectionNoteAutoJudgment(currentFrameInfo);

			// A tail that has already settled itself (a rub, a hidden carrier) answers "no" to a second
			// AutoJudgment, so it has to count as finished here: otherwise the bar waits forever for a
			// verdict that already exists and the session never walks past it.
			if (childNote != null
				&& (childNote.State == NoteState.Done || childNote.AutoJudgment(currentFrameInfo)))
			{
				State = NoteState.Done;
			}

			return true;
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return LiveConfig.CalculateLongStartNoteResult(this, musicTime).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			lastInputTime = 0f;
			_body?.Reset();
			WasHoldingWhenTerminated = false;
			IsAutoPlaying = false;
			_tail?.Reset();
			_head?.Reset();
			_lastBoundNote = null;
			_skipTailJudgement = false;
			_judgedPoints = null;
		}

	}
}
