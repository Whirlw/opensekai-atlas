using System;
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

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
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

					note.Excute(currentFrameInfo, childOffsetTime);
				}
			}

			BarView.UpdateLane(this, ref currentFrameInfo);

			// The head has its own judgement: it is settled once, when its window closes, and whether
			// the bar is lit or grey afterwards has nothing to do with it. A hidden head was never a
			// note the player could answer, so it settles silently.
			Head.SettleExpiredHead(currentFrameInfo.time);

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
				if (by != null && by.ParentRole != NoteParentRole.Tail && !ReferenceEquals(by, childNote))
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
		/// True while a finger is on this bar and its hold has not gone stale. This is the fact the
		/// bar acts on: it says nothing about which input phase a finger is in, and it stops being
		/// true as soon as the last refresh is older than the hold tolerance — earlier than the
		/// state machine greys the bar out.
		/// </summary>
		public bool IsHeld(float currentTime)
		{
			return HasFinger;
		}

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

		/// <summary>Feeds this bar one input frame: head, hold facts, frame stamp, points and tail.</summary>
		internal bool DriveJudgmentFrame(ref LiveTouch touch, float lane)
		{
			if (!Head.TryJudge(ref touch, lane)) return false;
			RecordHoldingInput(touch.fingerId, MusicScore.CurrentFrameInfo.time);
			RestoreHeldState();
			MarkHeadTouched();
			MarkPressedThisFrame();
			if (LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"grab barT={MusicScoreInfo.time:F3} lane={lane:F2} range={JudgeLaneStart:F2}..{JudgeLaneEnd:F2} finger={touch.fingerId} phase={touch.phase}");
			}

			ClearHeadJudgeOffset();
			ConnectionNoteJudgment(ref touch, lane);

			// A tail that judges itself takes its own turn in the candidate list, so pressing near its
			// time must not judge it through the bar as well: that would be two judges for one note.
			if (!TailBehaviour.JudgesItself
				&& childNote != null
				&& LiveConfig.IsLongEndJudgeTime(childNote.JudgeOffsetAt(touch.musicTime))
				&& childNote.IsJudgment(ref touch, lane)
				&& childNote.Judgment(ref touch, lane))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"headPressChild barT={MusicScoreInfo.time:F3} touchT={touch.musicTime:F3} childT={childNote.MusicScoreInfo.time:F3}");
				}

				State = NoteState.Done;
				return true;
			}

			// Being held: the bar is kept alive by its own refresh pass, so the finger can still serve
			// a note that sits on top of it.
			return true;
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
		/// How this bar is meant to end, derived from the note that ends it. Deliberately not cached:
		/// the chart is built once but bars are reset between runs, and any cached copy of this answer
		/// is wiped by that reset while the binding survives it.
		/// </summary>
		internal ITailBehaviour TailBehaviour => SkipsTailJudgement
			? SilentTail.Instance
			: childNote == null ? ReleaseTail.Instance : TailBehaviours.For(childNote);

		/// <summary>
		/// An ordinary bar: a plain long note, not a rub and not a decoration. Its head is where the
		/// body is supposed to shrink from, so it is the only kind that holds its head on the line while
		/// nobody is holding it.
		/// </summary>
		internal bool IsOrdinaryHead => Category == NoteCategory.Long && this is not GuideNote && this is not FrictionLongNote;

		/// <summary>
		/// Whether the bar's body starts at the judgement line instead of flowing past it. Every bar the
		/// player holds does; a guide bar sweeps across the lane. It used to be tied to
		/// <see cref="IsOrdinaryHead"/>, which also decides who may be judged, so a rub's head slid off.
		/// </summary>
		internal bool KeepsHeadOnTheLine => this is not GuideNote;

		/// <summary>
		/// The note that really ends this bar: the latest one that is neither skipped nor decorative.
		/// Picking the latest by time alone picked up a hidden or skipped point, so the real tail was
		/// never "the tail" and its window could never open - every ordinary bar missed its end.
		/// </summary>
		internal bool IsChildTheTail
		{
			get
			{
				return TryGetTail(out NoteBase tail) && ReferenceEquals(childNote, tail);
			}
		}

		/// <summary>Whether the final chart point ends the bar without its own judgement.</summary>
		internal bool EndsWithoutJudgement => SkipsTailJudgement || (_lastBoundNote != null && !IsTailCandidate(_lastBoundNote));

		/// <summary>
		/// A bar greys when its hold lapses. A returning finger lights it again while the
		/// post-head state still drives grace and tail judgement.
		/// </summary>
		public override bool ShowsAsReleased => !IsAutoPlaying && !HasFinger && (State == NoteState.Release || State == NoteState.Last);

		/// <summary>True while autoplay plays this bar - there is no finger to keep it lit.</summary>
		internal bool IsAutoPlaying { get; set; }

		internal bool PublishesHeadMiss => IsOrdinaryHead && HasJudgment;

		internal bool CanFinishSilently(float currentTime)
		{
			// Allow a visible head to publish its result before ending a silent tail.
			if (PublishesHeadMiss && !Head.IsSettled)
			{
				return false;
			}

			return EndsWithoutJudgement && _lastBoundNote != null
				&& currentTime > _lastBoundNote.MusicScoreInfo.time;
		}

		private void SettleLeftoverAsPerfect()
		{
			if (NoteList == null)
			{
				return;
			}

			for (int i = 0; i < NoteList.Count; i++)
			{
				NoteBase part = NoteList[i];
				if (part == null || !(part is LongHoldCombo))
				{
					continue;
				}

				if (part.ParentNote != this || part.Result != NoteResult.None)
				{
					continue;
				}

				part.FinishWith(NoteResult.Perfect);
			}
		}

		internal void FinishSilently()
		{
			if (State == NoteState.Done)
			{
				return;
			}

			// CanFinishSilently already waits for a visible head, but that wait is about the head's own
			// window: the head settles itself and is not touched here.
			_tail?.ResetDecision();
			// The leftover interior points are settled by the Done transition itself (see State), so this
			// path and every other ending agree without each one remembering to do it.
			State = NoteState.Done;
		}

		private LongNoteTail _tail;
		private LongNoteTail Tail => _tail ??= new LongNoteTail(this);

		internal static bool IsTailCandidate(NoteBase note) => LongNoteTail.IsTailCandidate(note);
		internal bool TryGetTail(out NoteBase tail) => Tail.TryGetTail(out tail);
		internal bool IsTailJudgeableAt(float currentTime) => Tail.IsTailJudgeableAt(currentTime);
		internal bool IsTailWindowOpenAt(float currentTime) => Tail.IsTailWindowOpenAt(currentTime);
		internal bool IsPastTailWindow(float currentTime) => Tail.IsPastTailWindow(currentTime);
		internal bool ExpireTail(float currentTime) => Tail.ExpireTail(currentTime);
		internal bool JudgeTailAsRelease(float currentTime) => Tail.JudgeTailAsRelease(currentTime);
		internal bool DetachStandaloneTail(float currentTime) => Tail.DetachStandaloneTail(currentTime);
		internal bool NotifyLaneSwipe(float magnitude, float currentTime) => Tail.NotifyLaneSwipe(magnitude, currentTime);
		internal bool HasDeferredSwipe => _tail?.HasDeferredSwipe ?? false;
		internal void MarkTailPressed() => Tail.MarkTailPressed();
		internal void NotifyLaneRelease() => _tail?.NotifyLaneRelease();
		internal bool TickSwipeTail(float currentTime) => Tail.TickSwipeTail(currentTime);
		internal bool FailTail() => Tail.FailTail();
		internal bool TrySwipeTail(Vector2 delta, float deltaTime, float currentTime) => Tail.TrySwipeTail(delta, deltaTime, currentTime);

		internal void SetTailResult(NoteResult result) => JudgeInfo = (result, NoteResultDescription.None);
		internal void CompleteTail() => State = NoteState.Done;

		/// <summary>Kept for callers that still describe a swipe by its magnitude.</summary>
		internal const float TailSwipeThreshold = 5f;

		// Body transitions remain visual/lifecycle operations. Only Head settles head results.
		internal NoteBase HeadChildNote => childNote;

		internal void SetHeadJudgeInfo((NoteResult result, NoteResultDescription description) value) => JudgeInfo = value;

		internal Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> HeadResultUpdater => onUpdateResult;

		internal void MarkHeadTouched() => Body.MarkHeadTouched();

		internal void ClearHeadJudgeOffset() => OffsetJudgeTime = 0f;

		internal Action<NoteBase> HeadDamage => onDamage;
		internal Action<NoteBase> HeadCombo => onUpdateCombo;
		internal Action<NoteBase> HeadJudgment => onJudgment;
		internal Action<NoteBase> HeadScore => onUpdateScore;

		internal void RecordHoldingInput(int fingerId, float musicTime) => Body.RecordInput(musicTime);
		internal void ClearFinger() => _body?.ClearFinger();
		internal void MarkPressedThisFrame() => Body.MarkPressed();
		private LongNoteHead _head;

		internal LongNoteHead Head => _head ??= new LongNoteHead(this);
		private NoteBase _lastBoundNote;

		/// <summary>
		/// A decorative bar never owns its end: the tail is a note of its own that the player judges.
		/// The bar just stops when that note's time passes, with no result of its own.
		/// </summary>
		private bool _skipTailJudgement;

		public void SkipTailJudgement()
		{
			_skipTailJudgement = true;
		}

		/// <summary>
		/// A bar type may decide by itself that the note at its end is played by the player.
		/// <see cref="GuideNote"/> does, so the converter flag is only a fallback.
		/// </summary>
		protected virtual bool SkipsTailJudgement => _skipTailJudgement;

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
			RecordHoldingInput(touch.fingerId, touch.musicTime);
			RestoreHeldState();
			ConnectionNoteJudgment(ref touch, lane);
		}

		protected void ConnectionNoteJudgment(ref LiveTouch touch, float lane)
		{
			Head.ConnectionNoteJudgment(ref touch, lane);
		}

		protected void ConnectionNoteTerminate()
		{
			if (NoteList == null)
			{
				return;
			}

			for (var i = 1; i < NoteList.Count - 1; i++)
			{
				var note = NoteList[i];
				if (note != null && note.State != NoteState.Done)
				{
					note.Terminate();
				}
			}
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

		public virtual bool IsJudgmentChild(ref LiveTouch touch, float lane)
		{
			return State != NoteState.Done && JudgedLaneStart <= lane && JudgedLaneEnd >= lane;
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			lastInputTime = currentFrameInfo.time;
			IsAutoPlaying = true;
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

		private void ConnectionNoteAutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (Head.Result != NoteResult.Auto)
			{
				return;
			}

			if (NoteList == null)
			{
				return;
			}

			for (var i = 1; i < NoteList.Count - 1; i++)
			{
				NoteList[i]?.AutoJudgment(currentFrameInfo);
			}
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
		}

	}
}