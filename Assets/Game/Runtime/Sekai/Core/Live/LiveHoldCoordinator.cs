using System;
using System.Collections.Generic;
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
		/// <summary>Maps a note and a finger to the lane that finger occupies for that note.</summary>
		public delegate float LaneOf(ref NoteBase note, ref LiveLogic.InputTmp input);

		private readonly int[] _holdInputNotes;

		private readonly bool[] _holdInputVisited;

		/// <summary>The call the eligibility delegate reads; set for the duration of one assignment pass.</summary>
		private List<NoteBase> _contextNotes;

		private List<LiveLogic.InputTmp> _contextInputs;

		private LaneOf _contextLaneOf;

		private readonly Func<int, int, bool> _holdInputEligible;

		public LiveHoldCoordinator(int inputCapacity)
		{
			_holdInputNotes = new int[inputCapacity];
			_holdInputVisited = new bool[inputCapacity];
			_holdInputEligible = CanContinueHold;
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
		/// Rebuilds the finger-to-bar assignment: first keep every owner that is still valid, then
		/// augment it for bars whose owner left their lane, so a wide and a narrow crossing bar can
		/// exchange fingers.
		/// </summary>
		public void AssignContinuingHolds(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, LaneOf laneOf)
		{
			_contextNotes = judgmentNotes;
			_contextInputs = inputs;
			_contextLaneOf = laneOf;

			int count = inputs.Count;
			for (int i = 0; i < count; i++)
			{
				_holdInputNotes[i] = -1;
			}

			for (int n = 0; n < judgmentNotes.Count; n++)
			{
				if (!HasContinuingHold(judgmentNotes[n]))
				{
					continue;
				}

				LongNote held = (LongNote)judgmentNotes[n];
				for (int i = 0; i < count; i++)
				{
					if (_holdInputNotes[i] < 0
						&& inputs[i].Touch.fingerId == held.HoldingTouchId
						&& !IsEndedOrCanceled(inputs[i].Touch.phase)
						&& CanContinueHold(n, i))
					{
						_holdInputNotes[i] = n;
						break;
					}
				}
			}

			for (int n = 0; n < judgmentNotes.Count; n++)
			{
				if (!HasContinuingHold(judgmentNotes[n]) || Array.IndexOf(_holdInputNotes, n, 0, count) >= 0)
				{
					continue;
				}

				Array.Clear(_holdInputVisited, 0, count);
				OpenSekai.Shared.Live.HoldInputMatching.TryAssign(n, count, _holdInputEligible, _holdInputNotes, _holdInputVisited);
			}

			for (int i = 0; i < count; i++)
			{
				if (_holdInputNotes[i] >= 0)
				{
					((LongNote)judgmentNotes[_holdInputNotes[i]]).AssignHoldingInput(inputs[i].Touch.fingerId);
				}
			}
		}

		/// <summary>
		/// One frame of hold upkeep: cover every live bar with the fingers on it, then refresh each
		/// bar from its owner. Neither step consumes a candidate slot or moves ownership.
		/// </summary>
		public void Refresh(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, float deltaTime, LaneOf laneOf)
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

				// A bar stays in this pass once the player has taken it - not only while the state machine still
				// says a finger is on it, because a stale hold has to reach its tail judgement too.
				if (!held.WasTouched && held.State != NoteState.Release)
				{
					continue;
				}

				if (TryRefreshFromOwner(held, count, inputs, laneOf))
				{
					continue;
				}

				// The owner is gone (lifted or cancelled) while another finger already sits inside this
				// bar. Hand the hold over on this frame instead of waiting for the old refresh to go
				// stale: otherwise the bar greys for a moment before the new finger is allowed in.
				if (!HasLiveOwner(held, count, inputs))
				{
					AdoptHoldFromAnyFinger(held, count, inputs, laneOf);
				}
			}
		}

		private bool CanContinueHold(int noteIndex, int inputIndex)
		{
			NoteBase note = _contextNotes[noteIndex];
			LiveLogic.InputTmp input = _contextInputs[inputIndex];
			float lane = _contextLaneOf(ref note, ref input);
			return lane >= 0f && note.IsJudgment(ref input.Touch, lane);
		}

		private static bool HasContinuingHold(NoteBase note)
		{
			// A bar that was taken and has moved on from its untouched phase still has a holder to
			// continue with, whether a finger is on it this instant or it is waiting to be re-grabbed.
			return note is LongNote held && held.WasTouched && held.State != NoteState.Scrolling;
		}

		/// <summary>
		/// Feeds every live bar the fingers currently on it, and finishes the bar when a finger leaves
		/// the screen inside it — the tail's normal judgement.
		/// </summary>
		private void CoverHoldsWithAnyFinger(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, float deltaTime)
		{
			int count = inputs.Count;
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

						// Only the finger that was holding this bar releases it. A lift anywhere else -
						// another lane, another finger - used to count as "released inside" and graded the
						// tail on release, so lifting anywhere finished any bar. The lane test stays for
						// the holder whose ended sample sits above the line.
						if (input.Touch.touchId == held.HoldingTouchId
							|| OpenSekai.Shared.Live.HoldSweep.TouchesLane(input.Lane, held.JudgeLaneStart, held.JudgeLaneEnd))
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

				bool isBeingHeld = held.IsHeld(frame.time);

				// The bar asks its tail how it ends. There is no switch here any more, and no way for a
				// new kind of end note to be drawn by one path and judged by another: the three shapes
				// (lift, swipe, self judged) each own their own answer.
				held.TailBehaviour.TryFinish(held, new TailFrame(
					frame.time,
					deltaTime,
					new UnityEngine.Vector2(swipeDeltaX, swipeDeltaY),
					releasedInside,
					covered,
					isBeingHeld));

				// A bar ends when its tail window closes, whatever closed it. Only the flick path had a
				// timeout before, so a bar ending with a friction or a hidden note could never end:
				// a finger in its lane kept reviving it and its parts stayed judgeable afterwards.
				held.ExpireTail(frame.time);
			}
		}

		private static bool TryRefreshFromOwner(LongNote held, int count, List<LiveLogic.InputTmp> inputs, LaneOf laneOf)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (input.Touch.fingerId != held.HoldingTouchId || IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				NoteBase laneNote = held;
				float lane = laneOf(ref laneNote, ref input);
				if (lane >= 0f && held.ContainsLane(lane))
				{
					held.RefreshHeldInput(ref input.Touch, lane);
					return true;
				}
			}

			return false;
		}

		private static bool HasLiveOwner(LongNote held, int count, List<LiveLogic.InputTmp> inputs)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (input.Touch.fingerId == held.HoldingTouchId && !IsEndedOrCanceled(input.Touch.phase))
				{
					return true;
				}
			}

			return false;
		}

		private static void AdoptHoldFromAnyFinger(LongNote held, int count, List<LiveLogic.InputTmp> inputs, LaneOf laneOf)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				NoteBase laneNote = held;
				float lane = laneOf(ref laneNote, ref input);
				if (lane < 0f || !held.ContainsLane(lane))
				{
					continue;
				}

				held.AssignHoldingInput(input.Touch.fingerId);
				held.RefreshHeldInput(ref input.Touch, lane);
				return;
			}
		}

		private static bool IsEndedOrCanceled(UnityEngine.InputSystem.TouchPhase phase)
		{
			return phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
		}
	}
}