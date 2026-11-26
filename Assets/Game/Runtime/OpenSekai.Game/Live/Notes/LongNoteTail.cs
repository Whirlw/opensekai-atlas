using System;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;

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

		internal void Reset()
		{
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

				if (tail == null || candidate.MusicScoreInfo.time > tail.MusicScoreInfo.time
					|| (candidate.MusicScoreInfo.time == tail.MusicScoreInfo.time && ReferenceEquals(candidate, EndNote)))
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
			if (!CanSettle() || !_bar.IsReleaseTailReady || _bar.NoteList == null || _bar.NoteList.Count == 0)
			{
				return false;
			}

			if (!TryGetTail(out NoteBase tail) || !ReferenceEquals(Child, tail) || !_bar.TailBehaviour.AcceptsRelease)
			{
				return false;
			}

			// A release only ever finishes a ReleaseTail, which the check above already established: a
			// flick, a rub, a rub flick or a hidden carrier grades itself, and all of them refuse a release
			// before the question of which window is open can be asked. The category used to be pinned to Long here, which silently disabled the
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
		/// Whether this bar's tail is the current child and its grading window is open right now, whatever
		/// kind of end it is (<see cref="IsTailJudgeableAt"/> adds "and a release may finish it").
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

			// Past the last window nothing about the bar may survive: no hold keeps it alive. A standalone tail owns its own result, so the bar just
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
				LiveTrace.Write(LiveTraceCategory.Tail, $"tailJudge barT={_bar.MusicScoreInfo.time:F3} now={currentTime:F3} tailT={Child.MusicScoreInfo.time:F3} state={_bar.State}");
			}

			return JudgeTailCore(currentTime);
		}

		private bool JudgeTailCore(float currentTime)
		{
			if (!CanSettle())
			{
				return false;
			}

			// Every route into grading re-checks the window.
			if (!IsTailWindowOpenAt(currentTime))
			{
				return false;
			}

			var hit = LongEndJudgement.EvaluateHit(currentTime - Child.MusicScoreInfo.time);
			if (Child.Result == NoteResult.None)
			{
				Child.TimingSample = currentTime;
				Child.FinishWith(hit);
			}

			return PublishTail(hit.result);
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
			return PublishTail(settled ? Child.Result : NoteResult.Miss);
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
				LiveTrace.Write(LiveTraceCategory.Swipe, $"tailMiss barT={_bar.MusicScoreInfo.time:F3} tailT={Child.MusicScoreInfo.time:F3} state={_bar.State}");
			}

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

		private bool _tailPublished;

		/// <summary>
		/// Writes the bar's side of a tail verdict, once. Every route that can settle a tail - a release,
		/// the window closing, a standalone tail that graded itself - ends up here, and the flag
		/// is what makes "one tail, one publication" a fact of its own rather than a consequence of
		/// <c>State == Done</c> and the child's own result, which are the indirect guards
		/// tail-classes.md §13.2 warns about.
		///
		/// The flag is per run. Only <see cref="Reset"/> clears it.
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
	}
}
