using System;
using System.Collections.Generic;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Live;
using Sekai.Live.Tails;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Keeps long notes alive while fingers rest on them.
	///
	/// A bar used to survive on the frame stamp that judging wrote, so its life depended on winning
	/// this frame's candidate slot: a second finger, or a second overlapping bar, could push the bar
	/// out of the slot and the bar would grey out with a finger still sitting on it. The rule here is
	/// different: a bar lives while any finger touches its lanes, a lift inside the bar is the release
	/// the tail listens for, and ownership only decides who is credited with the hold, never whether
	/// it survives.
	/// </summary>
	internal sealed class LiveHoldCoordinator
	{

		private readonly int[] _holdInputNotes;
		/// <summary>The bar each lifting finger finishes this frame, or -1.</summary>
		private readonly int[] _liftTargets;
		/// <summary>Music time of the frame the lift targets belong to, so a stale pick is ignored.</summary>
		private float _liftFrame = float.NaN;

		public LiveHoldCoordinator(int inputCapacity)
		{
			_holdInputNotes = new int[inputCapacity];
			_liftTargets = new int[inputCapacity];
		}

		/// <summary>
		/// The bar this finger is holding right now, or -1. Used to keep hold-versus-hold contention
		/// apart from a bar that merely shares a lane with another contact-only note.
		/// </summary>
		public int NoteOf(int inputIndex)
		{
			return inputIndex >= 0 && inputIndex < _holdInputNotes.Length ? _holdInputNotes[inputIndex] : -1;
		}

		/// <summary>
		/// Rebuilds the finger-to-bar assignment every frame. No bar belongs to a finger any more:
		/// each finger may drive one bar per frame, and the bar it drives is the nearest one it can
		/// reach, so two overlapping bars are shared out by distance instead of by who grabbed first.
		/// The quota is what keeps an overlapping pair from being driven twice on the same frame.
		/// </summary>
		public void AssignContinuingHolds(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs)
		{

			int count = inputs.Count;
			for (int i = 0; i < count; i++)
			{
				_holdInputNotes[i] = -1;
			}

			for (int n = 0; n < judgmentNotes.Count; n++)
			{
				if (judgmentNotes[n] is not LongNote held || !HasContinuingHold(held))
				{
					continue;
				}

				float middle = (held.JudgeLaneStart + held.JudgeLaneEnd) * 0.5f;
				int best = -1;
				float bestDistance = float.MaxValue;
				for (int i = 0; i < count; i++)
				{
					if (_holdInputNotes[i] >= 0 || IsEndedOrCanceled(inputs[i].Touch.phase))
					{
						continue;
					}

					LiveLogic.InputTmp candidate = inputs[i];
					float lane = candidate.Lane;
					if (lane < 0f || !held.IsJudgment(ref candidate.Touch, lane))
					{
						continue;
					}

					float distance = Math.Abs(lane - middle);
					if (best < 0 || distance < bestDistance)
					{
						best = i;
						bestDistance = distance;
					}
				}

				if (best >= 0)
				{
					_holdInputNotes[best] = n;
				}
			}
		}

		/// <summary>
		/// One frame of hold upkeep: cover every live bar with the fingers on it, then refresh each
		/// bar from its owner. Neither step consumes a candidate slot or moves ownership.
		/// </summary>
		public void Refresh(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, float deltaTime)
		{
			// A bar is not owned by a finger: any touch on it keeps it alive, and a touch that leaves
			// the screen inside it is the release the tail listens for. This runs before ownership so a
			// second finger can neither steal the hold nor swallow the tail.
			CoverHoldsWithAnyFinger(judgmentNotes, inputs, frame, deltaTime);

			int count = inputs.Count;
			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				if (judgmentNotes[noteIndex] is not LongNote held || held.State == NoteState.Done)
				{
					continue;
				}

				// "A finger is on this bar" is a fact about this frame only, so it is rebuilt here
				// rather than remembered: whatever the bar knew last frame is stale by now.
				held.ClearFinger();

				// A bar stays in this pass once the player has taken it - not only while the state machine still
				// says a finger is on it, because a stale hold has to reach its tail judgement too.
				if (!held.WasTouched && held.State != NoteState.Release)
				{
					continue;
				}

				// Any finger inside the bar refreshes it. There is no owner to hand it to and nobody
				// to take it from: the bar lives while a finger is on it, whoever that is.
				RefreshFromAnyFinger(held, count, inputs);
			}
		}


		private static bool HasContinuingHold(NoteBase note)
		{
			// A bar that was taken and has moved on from its untouched phase still has a holder to
			// continue with, whether a finger is on it this instant or it is waiting to be re-grabbed.
			return note is LongNote held && held.WasTouched
				&& (held.HasFinger || held.State == NoteState.Last || held.State == NoteState.Release);
		}

		/// <summary>
		/// The release channel: a finger that leaves the screen finishes the one bar it is standing on.
		///
		/// It runs on the frame's ended samples, before the resident upkeep, and it only settles the
		/// tail a release can settle (<see cref="Tails.ReleaseTail"/>). A flick end is finished by the
		/// swipe the resident pass hands it, and a rub, a rub flick or a hidden carrier grades itself,
		/// so none of those is touched here.
		/// </summary>
		public void JudgeLifts(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame)
		{
			int count = inputs.Count;
			_liftFrame = frame.time;

			// A lift is a judgement, so it gets the same treatment as a press: one finger may finish
			// one bar per frame, the nearest one. Lifting a finger that sits over two overlapping
			// bars used to grade both of them at once.
			for (int i = 0; i < count; i++)
			{
				_liftTargets[i] = -1;
			}

			for (int i = 0; i < count; i++)
			{
				LiveLogic.InputTmp input = inputs[i];
				if (input == null || !IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				// A lift belongs to the bar the finger is actually standing on, not to whichever bar's
				// widened judge range happens to reach it. Two bars end side by side, the finger lifts
				// in one of them, and the old rule handed the lift to the neighbour because that
				// neighbour's range was wider: the finger's own bar never got its release, and the
				// neighbour got one it never earned. Narrow lanes first, widened ranges only as a
				// fallback for a finger that is between two bars.
				int best = PickLiftTarget(judgmentNotes, input.Lane, frame.time);
				_liftTargets[i] = best;

				if (LiveTrace.IsOn(LiveTraceCategory.Tail))
				{
					float barTime = best < 0 ? -1f : ((LongNote)judgmentNotes[best]).MusicScoreInfo.time;
					LiveTrace.Write(LiveTraceCategory.Tail,
						$"liftPick finger={input.Touch.fingerId} lane={input.Lane:F2} phase={input.Touch.phase} barT={barTime:F3}");
				}

				if (best < 0)
				{
					continue;
				}

				LongNote bar = (LongNote)judgmentNotes[best];
				if (bar.TailBehaviour.AcceptsRelease)
				{
					bar.JudgeTailAsRelease(frame.time);
				}
			}
		}

		/// <summary>
		/// Feeds every live bar the fingers currently on it, and hands each bar the frame's cover and
		/// swipe facts. The lift that ends a bar is the release channel's business, already done.
		/// </summary>
		private void CoverHoldsWithAnyFinger(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, float deltaTime)
		{
			int count = inputs.Count;

			// The lifts were picked by the release pass, before this one ran. A target that is missing
			// or from another frame is not a lift of this frame, so a stale pick can never make a bar
			// look released inside it.
			bool liftsKnown = _liftFrame == frame.time;

			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				if (judgmentNotes[noteIndex] is not LongNote held || held.State == NoteState.Done)
				{
					continue;
				}

				bool covered = false;
				bool releasedInside = false;
				float swipeDeltaX = 0f;
				float swipeDeltaY = 0f;
				for (int inputIndex = 0; inputIndex < count; inputIndex++)
				{
					LiveLogic.InputTmp input = inputs[inputIndex];
					if (input == null)
					{
						continue;
					}

					// Every note is judged against the lane that already applies the vertical gate, and a
					// bar is covered by the same measure: a finger above the judgment line has not reached
					// the bar yet.
					bool endsNow = IsEndedOrCanceled(input.Touch.phase);
					if (endsNow)
					{
						// A quick flick is often reported as a single ended sample carrying the whole
						// movement, and by then the finger is above the judgement line, so the lane test
						// below would throw that delta away. Read it first: the lift is both the swipe
						// and the release that starts the three frame countdown.
						if (input.Touch.delta.sqrMagnitude > 0f)
						{
							swipeDeltaX += input.Touch.delta.x;
							swipeDeltaY += input.Touch.delta.y;
						}

						// The nearest bar to this lifting finger is the one it finishes.
						if (liftsKnown && _liftTargets[inputIndex] == noteIndex)
						{
							releasedInside = true;
						}

						continue;
					}

					if (!OpenSekai.Shared.Live.HoldSweep.TouchesLane(input.Lane, held.JudgeLaneStart, held.JudgeLaneEnd))
					{
						continue;
					}

					covered = true;

					// A decorative bar is not played, only its tail is: a click is what turns the drag
					// into a flick on it, matching the plain flick note it ends with.
					if (!held.HasJudgment && input.Touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
					{
						held.MarkTailPressed();
					}

					// A flick ended bar is finished by a swipe, so what matters is a finger moving inside
					// it. The delta goes to the bar, which applies the tail flick's own rule, so a drag
					// that does not go the way the flick asks for never ends the bar.
					if (input.Touch.delta.sqrMagnitude > 0f)
					{
						swipeDeltaX += input.Touch.delta.x;
						swipeDeltaY += input.Touch.delta.y;
					}
				}

				// A finger resting over a bar whose head has not been judged yet must not adopt it:
				// holding before the head arrives would otherwise let the head pass without a Miss.
				// Once the head is judged (hit or missed) any finger inside the lane may take over.
				bool headSettled = held.Result != NoteResult.None || held.WasTouched;
				if (covered && headSettled)
				{
					held.KeepAlive(frame.time);
				}

				if (LiveTrace.IsOn(LiveTraceCategory.Tail))
				{
					LiveTrace.Write(LiveTraceCategory.Tail,
						$"tailFrame barT={held.MusicScoreInfo.time:F3} now={frame.time:F3} state={held.State} covered={covered} released={releasedInside} held={held.HasFinger} settled={headSettled} child={(held.ChildNote == null ? -1f : held.ChildNote.MusicScoreInfo.time):F3} childRes={(held.ChildNote == null ? NoteResult.None : held.ChildNote.Result)}");
				}

				// The bar asks its tail how it ends. There is no switch here any more, and no way for a
				// new kind of end note to be drawn by one path and judged by another: the three shapes
				// (lift, swipe, self judged) each own their own answer.
				held.TailBehaviour.TryFinish(held, new TailFrame(
					frame.time,
					deltaTime,
					new UnityEngine.Vector2(swipeDeltaX, swipeDeltaY),
					releasedInside,
					covered));

				// A bar ends when its tail window closes, whatever closed it. Only the flick path had a
				// timeout before, so a bar ending with a friction or a hidden note could never end:
				// a finger in its lane kept reviving it and its parts stayed judgeable afterwards.
				held.ExpireTail(frame.time);
			}
		}

		private static bool RefreshFromAnyFinger(LongNote held, int count, List<LiveLogic.InputTmp> inputs)
		{
			bool refreshed = false;
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				float lane = input.Lane;
				if (lane < 0f || !held.ContainsLane(lane))
				{
					continue;
				}

				held.RefreshHeldInput(ref input.Touch, lane);
				refreshed = true;
			}

			return refreshed;
		}


		/// <summary>
		/// The bars a lifting finger may finish: its lane must hold the finger and its tail must be
		/// judgeable right now. Of those, the earliest tail wins - a lift finishes one bar, the one
		/// that is already due.
		/// </summary>
		private static int PickLiftTarget(List<NoteBase> judgmentNotes, float lane, float time)
		{
			int best = -1;
			float bestTime = float.MaxValue;
			float bestDistance = float.MaxValue;
			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				if (judgmentNotes[noteIndex] is not LongNote candidate || candidate.State == NoteState.Done)
				{
					continue;
				}

				// The lift finishes a tail, so it is the tail's own lane that decides - a tail may sit
				// on another lane than the body it hangs from, and the body's range is the widened
				// judge range anyway, which reaches into the neighbours.
				NoteBase tail = candidate.ChildNote as NoteBase;
				if (tail == null || lane < tail.JudgeLaneStart || lane > tail.JudgeLaneEnd)
				{
					continue;
				}

				if (!candidate.IsTailJudgeableAt(time))
				{
					continue;
				}

				float tailTime = tail.MusicScoreInfo.time;
				float distance = Math.Abs(lane - (tail.JudgeLaneStart + tail.JudgeLaneEnd) * 0.5f);
				if (best < 0 || tailTime < bestTime || (tailTime == bestTime && distance < bestDistance))
				{
					best = noteIndex;
					bestTime = tailTime;
					bestDistance = distance;
				}
			}

			return best;
		}

		private static bool IsEndedOrCanceled(UnityEngine.InputSystem.TouchPhase phase)
		{
			return phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
		}
	}
}