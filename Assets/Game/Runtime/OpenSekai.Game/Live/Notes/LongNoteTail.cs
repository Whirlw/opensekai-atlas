using System;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using UnityEngine;

namespace Sekai.Live
{
	internal sealed class LongNoteTail
	{
		private readonly LongNote _bar;
		private bool _windowTraced;
		private bool _expireTraced;

		internal LongNoteTail(LongNote bar)
		{
			_bar = bar;
		}

		/// <summary>
		/// The note the bar was bound to as its end - what the chart calls the end of this bar, whatever kind
		/// it is (an ordinary note, a flick, a rub, a hidden marker). It is set only by the end-note binding,
		/// so a hidden interior point appended after it cannot steal it (pinned by
		/// TailBehaviourTests.AHiddenMarkerAfterTheTailDoesNotStealIt).
		///
		/// Whether this note can be graded at all is a different question, answered by TryGetTail.
		/// </summary>
		internal NoteBase EndNote => (NoteBase)_bar.ChildNote;

		private NoteBase Child => EndNote;

		internal void ResetDecision()
		{
			_swipeTailDeferred = false;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			_swipeTouchId = null;
			_swipeAccum = Vector2.zero;
			_swipeWindowStart = -1f;
		}

		internal void Reset()
		{
			ResetDecision();
			_tailPressed = false;
			_windowTraced = false;
			_expireTraced = false;
			_tailPublished = false;
		}

		internal static bool IsTailCandidate(NoteBase note)
		{
			return note != null
				&& !note.IsSkip
				&& !note.IsDecoration
				&& !(note is HiddenConnectionNote)
				&& !(note is FrictionHideNote);
		}

		internal bool TryGetTail(out NoteBase tail)
		{
			tail = null;
			if (_bar.NoteList == null)
			{
				return false;
			}

			for (int i = 0; i < _bar.NoteList.Count; i++)
			{
				NoteBase candidate = _bar.NoteList[i];
				if (!IsTailCandidate(candidate))
				{
					continue;
				}

				if (tail == null || candidate.MusicScoreInfo.time >= tail.MusicScoreInfo.time)
				{
					tail = candidate;
				}
			}

			return tail != null;
		}

		internal bool IsTailJudgeableAt(float currentTime)
		{
			// Only the real tail counts. Every interior point - combo ticks as well as the preset and
			// hidden ones - sits at offset ~0 when it comes up, so testing "the current child is inside
			// its window" made a release anywhere in the bar look like the tail.
			//
			// The tail is the note with the latest time, not the last one in the list: hidden points and
			// preset points are appended in chart order and can come after the end.
			if (!CanSettle() || _bar.NoteList == null || _bar.NoteList.Count == 0)
			{
				return false;
			}

			if (!TryGetTail(out NoteBase tail) || !ReferenceEquals(Child, tail) || !_bar.TailBehaviour.AcceptsRelease)
			{
				return false;
			}

			// A release only ever finishes a ReleaseTail, which the check above already established: a
			// flick end waits for the swipe (TrySwipeTail) and a rub, a rub flick or a hidden carrier
			// grades itself, and all three refuse a release before the question of which window is open
			// can be asked. The category used to be pinned to Long here, which silently disabled the
			// release tail for every bar ending on FrictionLong/FrictionHideLong.
			//
			// A decoration end is not an end anybody plays.
			if (Child.IsDecoration)
			{
				return false;
			}
			return IsTailWindowOpenAt(currentTime);
		}

		/// <summary>
		/// Whether this bar's tail is the current child and its grading window is open right now. Split
		/// from <see cref="IsTailJudgeableAt"/> because a flick tail is finished by a swipe rather than by
		/// a release, and its note category is the flick one, not Long.
		/// </summary>
		internal bool IsTailWindowOpenAt(float currentTime)
		{
			if (_bar.NoteList == null || _bar.NoteList.Count == 0 || Child == null)
			{
				return false;
			}

			// This is the bar's tail window. Self-judged notes use their own window to score.
			if (!TryGetTail(out NoteBase tail) || !ReferenceEquals(Child, tail) || Child.IsDecoration)
			{
				return false;
			}

			// The offset has to come from the clock, not from the note: a note only refreshes its
			// OffsetJudgeTime when it is executed, and the tail is not executed while the bar is still
			// mid-body - reading the stale value (0) graded every mid-body release as a perfect tail.
			bool open = LongEndJudgement.Accepts(currentTime - Child.MusicScoreInfo.time);
			if (!open && currentTime > Child.MusicScoreInfo.time && !_windowTraced
				&& LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				_windowTraced = true;
				LiveTrace.Write(LiveTraceCategory.Tail, $"winClosed barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} child={Child.GetType().Name}@{Child.MusicScoreInfo.time:F3} childState={Child.State} childRes={Child.Result} same={ReferenceEquals(Child, tail)} deco={Child.IsDecoration} tail={tail.GetType().Name}@{tail.MusicScoreInfo.time:F3}");
			}

			return open;
		}

		/// <summary>
		/// The tail's own time has gone by and its window has closed without anything ending the bar.
		/// Only a flick tail used to get a timeout, so a bar closed by a friction or a hidden note kept
		/// living forever: nothing could end it and a finger in its lane revived it every frame.
		/// </summary>
		internal bool IsPastTailWindow(float currentTime)
		{
			return Child != null
				&& (LongEndJudgement.IsPast(currentTime - Child.MusicScoreInfo.time)
					|| (currentTime > Child.MusicScoreInfo.time && !IsTailWindowOpenAt(currentTime)));
		}

		/// <summary>
		/// Ends a bar whose tail was never resolved, grading it a miss.
		/// </summary>
		internal bool ExpireTail(float currentTime)
		{
			if (Child != null && currentTime > Child.MusicScoreInfo.time && !_expireTraced
				&& LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				_expireTraced = true;
				LiveTrace.Write(LiveTraceCategory.Tail, $"expireCheck barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} state={_bar.State} res={_bar.Result} child={Child.GetType().Name}@{Child.MusicScoreInfo.time:F3} past={IsPastTailWindow(currentTime)}");
			}

			if (_bar.State == NoteState.Done && !_bar.HasPendingManagedTail)
			{
				return false;
			}

			if (_bar.CanFinishSilently(currentTime))
			{
				_bar.FinishSilently();
				return true;
			}

			// A bar never leaves before its own head has a verdict. A bar shorter than its head's window - a
			// one-tick bar, which is what every broken chart in the reports has - was past its tail window
			// about six milliseconds in, so it finished, went back to the pool and took the head's remaining
			// ~125 ms of window with it: a press that landed after that had nothing left to answer and the
			// head missed. The head settles itself (LongNoteHead.SettleExpiredHead) at exactly that window.
			if (_bar.PublishesHeadMiss && !_bar.Head.IsSettled
				&& !_bar.Head.IsPastWindow(currentTime))
			{
				return false;
			}

			if (!IsPastTailWindow(currentTime))
			{
				return false;
			}

			// Past the last window nothing about the bar may survive: no hold keeps it alive and no
			// deferred swipe keeps waiting. A standalone tail owns its own result, so the bar just
			// detaches instead of writing a miss onto it.
			if (_bar.TailBehaviour.JudgesItself)
			{
				return DetachStandaloneTail(currentTime);
			}

			return FailTail();
		}

		/// <summary>
		/// Scores the tail because a finger left the screen inside the bar. The bar finishes and the
		/// tail note carries the result, so the tail is judged explicitly here.
		/// </summary>
		internal bool JudgeTailAsRelease(float currentTime)
		{
			if (!CanSettle() || !IsTailJudgeableAt(currentTime))
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				LiveTrace.Write(LiveTraceCategory.Tail, $"release barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} tailT={Child.MusicScoreInfo.time:F3}");
			}

			return JudgeTail(currentTime);
		}

		/// <summary>Scores the tail and finishes the bar. Callers own the gate; this only grades.</summary>
		private bool JudgeTail(float currentTime)
		{
			if (LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				LiveTrace.Write(LiveTraceCategory.Tail, $"tailJudge barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} tailT={Child.MusicScoreInfo.time:F3} capped={_swipeTailCapped} deferred={_swipeTailDeferred} state={_bar.State}");
			}

			return JudgeTailCore(currentTime);
		}

		private bool JudgeTailCore(float currentTime)
		{
			if (!CanSettle())
			{
				return false;
			}

			// Every route into grading re-checks the window: a bar that is reused from the pool can
			// still carry a deferred swipe from the previous run, and grading that would finish the
			// bar before it ever reached the line.
			if (!IsTailWindowOpenAt(currentTime))
			{
				ResetDecision();
				return false;
			}

			NoteResult result = LongEndJudgement.Evaluate(currentTime - Child.MusicScoreInfo.time);

			// A malformed gesture keeps its timing grade but can never reach Perfect.
			if (_swipeTailCapped && (result == NoteResult.Perfect || result == NoteResult.JustPerfect))
			{
				result = NoteResult.Great;
			}

			if (Child.Result == NoteResult.None)
			{
				Child.FinishWith(result);
			}

			bool published = PublishTail(result);
			ResetDecision();
			return published;
		}

		internal bool DetachStandaloneTail(float currentTime)
		{
			if (_bar.State == NoteState.Done || Child == null)
			{
				return false;
			}

			if (_bar.HasIndependentTail)
			{
				if (currentTime < Child.MusicScoreInfo.time
					|| (_bar.PublishesHeadMiss && !_bar.Head.IsSettled && !_bar.Head.IsPastWindow(currentTime)))
				{
					return false;
				}

				_bar.CompleteTail();
				return true;
			}

			// The bar lives its own length: an end note that already judged itself does not shorten it.
			// (tail-classes.md §3 类 1 - an independent tail does not touch the bar, in either direction.)
			if (!IsPastTailWindow(currentTime))
			{
				return false;
			}

			// And the bar writes nothing onto that note: the verdict is the tail's own, so the bar only
			// reads it - and it does not wait for it either: a judged end note is ticked by the session
			// (LiveNoteSession.TickTails), so it keeps its own clock after this bar has gone and publishes
			// its own Miss. Detaching it here is exactly what decoupling is for.
			bool settled = Child.Result != NoteResult.None || Child.State == NoteState.Done;

			// Publish before touching anything else: if this tail already has a verdict, the refusal has
			// to happen while the bar and the note list are still exactly as they were.
			if (!PublishTail(settled ? Child.Result : NoteResult.Miss))
			{
				ResetDecision();
				return false;
			}

			ResetDecision();
			return true;
		}

		private bool _swipeTailDeferred;
		private bool _swipeTailCapped;
		private Vector2 _swipeAccum;
		private int? _swipeTouchId;
		private float _swipeWindowStart = -1f;
		private int _framesSinceRelease = -1;

		/// <summary>
		/// A swipe was seen inside this bar. Swiping before the tail time is only recorded: judging it
		/// right away would reward an early swipe exactly like an on time one, and the rule defers
		/// to the third frame after the release so that the swipe is graded closer to the tail time.
		/// </summary>
		internal bool NotifyLaneSwipe(float magnitude, float currentTime)
		{
			// The speed gate already ran in TrySwipeTail; a raw displacement threshold here would
			// reject a 5 cm/s swipe at high frame rates, where one frame moves under a millimetre.
			if (!CanSettle() || !_bar.TailBehaviour.AcceptsSwipe || magnitude <= 0f)
			{
				return false;
			}

			// The end point only exists once the bar is being held. Without this a swipe made right
			// after the bar appears would finish it while its head is still travelling towards the
			// line, which is also how a short bar could be ended before it was ever started.
			if (!CanReceiveSwipe)
			{
				return false;
			}

			if (!IsTailWindowOpenAt(currentTime))
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Swipe))
			{
				LiveTrace.Write(LiveTraceCategory.Swipe, $"swipe barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} tailT={Child.MusicScoreInfo.time:F3} mag={magnitude:F1}");
			}

			if (currentTime < Child.MusicScoreInfo.time)
			{
				// The rule defers an early swipe to the third frame after the release: the
				// countdown starts when the finger leaves, not when the swipe happens.
				_swipeTailDeferred = true;
				_framesSinceRelease = -1;
				return false;
			}

			return JudgeTail(currentTime);
		}

		/// <summary>A swipe was seen and is waiting for the release that starts the countdown.</summary>
		internal bool HasDeferredSwipe => _swipeTailDeferred;
		internal int? DeferredSwipeTouchId => _swipeTailDeferred ? _swipeTouchId : null;

		private bool _tailPressed;

		/// <summary>
		/// The finger actually clicked this bar. A decorative bar carries a real tail but its body
		/// takes no input, so the tail has to behave like the plain note it is: a drag that merely
		/// crosses it is not a flick until something pressed it.
		/// </summary>
		internal void MarkTailPressed()
		{
			_tailPressed = true;
		}

		/// <summary>An in lane finger lifted while a deferred swipe is pending; counting starts now.</summary>
		internal void NotifyLaneRelease()
		{
			if (_swipeTailDeferred && _framesSinceRelease < 0)
			{
				_framesSinceRelease = 0;
			}
		}

		/// <summary>Advances the deferred swipe; it is graded on the third frame after the release.</summary>
		internal bool TickSwipeTail(float currentTime)
		{
			if (!CanSettle() || !_bar.TailBehaviour.AcceptsSwipe)
			{
				return false;
			}

			// Reaching the tail's own time counts as a release: the three frame countdown starts now,
			// which is what turns an early swipe into a late — and therefore better — grade.
			if (_swipeTailDeferred && _framesSinceRelease < 0 && Child != null
				&& currentTime >= Child.MusicScoreInfo.time)
			{
				// The frame that notices the tail time is frame zero of the release, not frame one.
				NotifyLaneRelease();
				return false;
			}

			if (_swipeTailDeferred && _framesSinceRelease >= 0)
			{
				// A bar that has not been held yet cannot have a deferred swipe of its own; anything
				// left here is pool residue from the previous run.
				// The countdown starts exactly when the bar stops being held, so Last and Release must
				// keep counting: treating them as residue cleared every deferred swipe on its first frame.
				if (!_bar.HasFinger && _bar.State != NoteState.Last && _bar.State != NoteState.Release
					&& _bar.State != NoteState.Done)
				{
					ResetDecision();
					return false;
				}

				_framesSinceRelease++;
				if (_framesSinceRelease < LongNote.HoldGraceFrames)
				{
					return false;
				}

				// The cap has to survive until the judgement is written: a wrong way swipe that was
				// seen early is only graded once the release delay is over, and clearing the flag here
				// is what let a wrong way swipe be graded Perfect. JudgeTail clears it.
				_swipeTailDeferred = false;
				_framesSinceRelease = -1;
				return JudgeTail(currentTime);
			}

			// No swipe is pending and the tail window has closed. A flick ended bar is not finished by
			// lifting, so nothing else can end it: without this the tail note would sit on the judgement
			// line for the rest of the chart and the bar would never be scored.
			if (IsPastTailWindow(currentTime))
			{
				return FailTail();
			}

			return false;
		}

		/// <summary>
		/// Ends a bar whose tail can no longer be earned. The tail is graded Miss instead of being left
		/// in the air, so its note stops sticking to the judgement line.
		/// </summary>
		internal bool FailTail()
		{
			// Only a decisive end is the bar's to fail: a note that judges itself writes its own Miss,
			// and the bar must not do it for them (tail-classes.md §3 R1/R2).
			if (!CanSettle() || _bar.TailBehaviour.JudgesItself)
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Swipe))
			{
				LiveTrace.Write(LiveTraceCategory.Swipe, $"tailMiss barT={_bar.MusicScoreInfo.time:F3} tailT={Child.MusicScoreInfo.time:F3} state={_bar.State} deferred={_swipeTailDeferred}");
			}

			ResetDecision();
			if (Child.Result == NoteResult.None)
			{
				// Nothing was played here: grade it Miss. The judgement popup has to show, and the tap
				// effect decides on its own that a Miss earns no hit sound.
				Child.FinishWith(NoteResult.Miss);
			}

			return PublishTail(NoteResult.Miss);
		}

		private bool CanSettle() => Child != null && Child.State != NoteState.Done
			&& (_bar.State != NoteState.Done || _bar.HasPendingManagedTail);

		internal bool CanReceiveSwipe => _bar.State == NoteState.Done
			? _bar.WasHoldingWhenTerminated : _bar.HasFinger;

		private bool _tailPublished;

		/// <summary>
		/// Writes the bar's side of a tail verdict, once. Every route that can settle a tail - a release,
		/// a swipe, the window closing, a standalone tail that graded itself - ends up here, and the flag
		/// is what makes "one tail, one publication" a fact of its own rather than a consequence of
		/// <c>State == Done</c> and the child's own result, which are the indirect guards
		/// tail-classes.md §13.2 warns about.
		///
		/// The flag is per run. Only <see cref="Reset"/> clears it: <see cref="ResetDecision"/> runs on
		/// the very paths that are supposed to publish, so clearing it there would re-open the door.
		/// </summary>
		private bool PublishTail(NoteResult result)
		{
			if (_tailPublished)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Tail))
				{
					LiveTrace.Write(LiveTraceCategory.Tail, $"tailRepublish barT={_bar.MusicScoreInfo.time:F3} result={result} state={_bar.State}");
				}

				return false;
			}

			_tailPublished = true;
			_bar.SetTailResult(result);
			_bar.CompleteTail();

			return true;
		}

		/// <summary>
		/// Tests a finger's movement this frame as the swipe that finishes a flick ended bar. The swipe
		/// is judged by the tail flick's own rule (distance and direction), so a bar whose tail asks for
		/// a left flick is not finished by a swipe to the right, and a vertical drag inside a single
		/// lane is only a flick when the tail accepts any direction.
		/// </summary>
		internal bool TrySwipeTail(Vector2 delta, float deltaTime, float currentTime, int? touchId = null)
		{
			if (!CanSettle() || !_bar.TailBehaviour.AcceptsSwipe)
			{
				return false;
			}

			// The end point only exists while the bar is being held and inside its tail window.
			if (!CanReceiveSwipe)
			{
				return false;
			}

			// A decorative bar is not played, only its tail is: without a click on it this is just a
			// finger dragging across the screen. A guide bar is the decorative kind (it carries no
			// judgement of its own), and IsDecoration is not set on it.
			if (!_bar.HasJudgment && !_tailPressed)
			{
				return false;
			}

			if (!IsTailWindowOpenAt(currentTime))
			{
				return false;
			}

			if (_swipeTailDeferred && _swipeTouchId != touchId)
			{
				return false;
			}

			// Only a flick's end is graded by a swipe (SwipeTail is chosen for a FlickNote tail and nothing
			// else), so the distance comes from that note. The friction-flick arm that used to sit here was
			// dead: a friction-flick end judges itself, and its behaviour refuses swipes before this point.
			float flickDistance = Child is FlickNote flick ? flick.FlickDistance : 0f;

			// A flick is judged on one 60 fps frame of travel, not on a single 165 Hz sample: a held
			// finger jitters by a few tenths of a millimetre per frame, which reads as 5-14 cm/s and
			// would pass a per-frame speed gate on every frame. Accumulating the *net* displacement
			// over 16.7 ms keeps a real flick (one direction, large net travel) and drops jitter
			// (oscillating, net travel near zero).
			if (_swipeWindowStart < 0f || _swipeTouchId != touchId)
			{
				_swipeTouchId = touchId;
				_swipeWindowStart = currentTime - Mathf.Max(deltaTime, 0f);
				_swipeAccum = Vector2.zero;
			}

			_swipeAccum += delta;

			float frameSeconds = FlickGesture.ReferenceFrameSeconds;
			float elapsed = currentTime - _swipeWindowStart;

			// Wait for one 60 fps frame of travel, or stop early once the finger has already covered a
			// whole flick distance inside the window: that much travel in a few milliseconds cannot be
			// jitter, and waiting only adds latency to a real flick.
			if (elapsed + 1e-4f < frameSeconds && _swipeAccum.magnitude < flickDistance)
			{
				return false;
			}

			Vector2 netDelta = _swipeAccum;
			_swipeAccum = Vector2.zero;
			_swipeWindowStart = currentTime;

			float minSpeed = flickDistance / frameSeconds;
			float speed = netDelta.magnitude / elapsed;

			if (!FlickGesture.TryUpdate(netDelta, frameSeconds, Child.Direction, flickDistance, out bool trueDirection))
			{
				return false;
			}

			// A flick is a speed, not a displacement: the note's own flick distance is what 5 cm/s
			// looks like in one 60 fps frame, so requiring it in pixels per second is the same rule
			// the plain flick uses, only frame rate independent. A displacement gate on top of that
			// rejected legitimate swipes at 165 Hz, where 5 cm/s is under five pixels per frame.

			if (LiveTrace.IsOn(LiveTraceCategory.Swipe))
			{
				LiveTrace.Write(LiveTraceCategory.Swipe, $"tailSwipe barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} tailT={Child.MusicScoreInfo.time:F3} speed={speed:F0} min={minSpeed:F0} state={_bar.State}");
			}

			if (speed < minSpeed)
			{
				return false;
			}

			// The latest qualifying swipe supplies the direction cap for deferred grading.
			_swipeTailCapped = !trueDirection;
			return NotifyLaneSwipe(delta.magnitude, currentTime);
		}
	}
}
