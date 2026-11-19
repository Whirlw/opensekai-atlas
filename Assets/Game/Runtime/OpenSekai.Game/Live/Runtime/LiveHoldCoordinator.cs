using System;
using System.Collections.Generic;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Live;
using Sekai.Live.Tails;
using UnityEngine;

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
		private readonly HashSet<int> _usedLiftFingers = new HashSet<int>();
		private readonly Dictionary<int, float> _keyPressTimes = new Dictionary<int, float>();
		private HashSet<int> _silencedTouches;

		public LiveHoldCoordinator(int inputCapacity)
		{
			_holdInputNotes = new int[inputCapacity];
		}

		internal void RecordKeyPress(LiveTouch touch) => _keyPressTimes[touch.touchId] = touch.musicTime;

		internal void ForgetKeyPress(int touchId) => _keyPressTimes.Remove(touchId);

		internal void ClearKeyPresses() => _keyPressTimes.Clear();

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

				float middle = (held.BodyLaneStart + held.BodyLaneEnd) * 0.5f;
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
					if (float.IsNegativeInfinity(lane) || !held.IsJudgment(ref candidate.Touch, lane))
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
		public void Refresh(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, float deltaTime,
			HashSet<int> silencedTouches = null)
		{
			_silencedTouches = silencedTouches;
			// A bar is not owned by a finger: any touch on it keeps it alive, and a touch that leaves
			// the screen inside it is the release the tail listens for. This runs before ownership so a
			// second finger can neither steal the hold nor swallow the tail.
			CoverHoldsWithAnyFinger(judgmentNotes, inputs, frame);

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
		/// tail a release can settle (<see cref="Tails.ReleaseTail"/>). A flick, a rub, a rub flick or a
		/// hidden carrier grades itself, so none of those is touched here.
		/// </summary>
		public void JudgeLifts(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame,
			LiveCandidatePipeline candidates)
		{
			foreach (LiveLogic.InputTmp input in inputs)
			{
				input.CandidateNotes.Clear();
				if (input.Touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && !float.IsNegativeInfinity(input.Lane))
				{
					CollectLiftCandidates(judgmentNotes, input);
				}
			}

			candidates.DeduplicateCandidates(inputs, ref frame);

			// A lift is a judgement, so it gets the same treatment as a press: one finger may finish
			// one ordinary tail per frame.
			_usedLiftFingers.Clear();
			for (int i = 0; i < inputs.Count; i++)
			{
				LiveLogic.InputTmp input = inputs[i];
				if (input == null || input.Touch.phase != UnityEngine.InputSystem.TouchPhase.Ended
					|| _usedLiftFingers.Contains(input.Touch.fingerId))
				{
					continue;
				}

				NoteBase tail = PickLiftTarget(input, ref frame);
				LongNote target = tail?.ParentNote as LongNote;

				if (LiveTrace.IsOn(LiveTraceCategory.Release))
				{
					float barTime = target == null ? -1f : target.MusicScoreInfo.time;
					LiveTrace.Write(LiveTraceCategory.Release,
						$"liftSelected finger={input.Touch.fingerId} touch={input.Touch.touchId} lane={input.Lane:F2} phase={input.Touch.phase} barT={barTime:F3} tailId={(tail == null ? -1 : tail.Id)} tailT={(tail == null ? -1f : tail.MusicScoreInfo.time):F3}");
				}

				bool settled = tail is NormalNote { IsIndependentRelease: true }
					? tail.Judgment(ref input.Touch, input.Lane)
					: target != null && target.JudgeTailAsRelease(input.Touch.musicTime);
				if (LiveTrace.IsOn(LiveTraceCategory.Release))
					LiveTrace.Write(LiveTraceCategory.Release, $"liftResult finger={input.Touch.fingerId} touch={input.Touch.touchId} settled={settled} result={tail?.Result}");
				if (settled)
				{
					if (LatencyCapture.Active)
						LatencyCapture.Record("release_settled", input.Touch.touchId, input.Touch.fingerId, input.Touch.phase,
							tail.Id, song: input.Touch.musicTime, note: tail.MusicScoreInfo.time, result: tail.Result);
					_usedLiftFingers.Add(input.Touch.fingerId);
				}
			}

			// Input timestamps can still land inside the window on a frame that overshoots it.
			foreach (NoteBase note in judgmentNotes)
			{
				if (note is NormalNote { IsIndependentRelease: true } release)
					release.SettleExpiredRelease(frame.time);
			}
		}

		/// <summary>
		/// Feeds every live bar the fingers currently on it, and hands each bar the frame's cover. The lift
		/// that ends a bar is the release channel's business, already done.
		/// </summary>
		private void CoverHoldsWithAnyFinger(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame)
		{
			int count = inputs.Count;
			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				if (judgmentNotes[noteIndex] is not LongNote held
					|| (held.State == NoteState.Done && !held.HasPendingManagedTail))
				{
					continue;
				}

				bool covered = false;
				for (int inputIndex = 0; inputIndex < count; inputIndex++)
				{
					LiveLogic.InputTmp input = inputs[inputIndex];
					if (input == null || IsEndedOrCanceled(input.Touch.phase) || !TouchesBar(input.Lane, held))
					{
						continue;
					}

					covered = true;
					break;
				}

				// A finger resting over a bar whose head has not been judged yet must not adopt it:
				// holding before the head arrives would otherwise let the head pass without a Miss.
				// Once the head is judged (hit or missed) any finger inside the lane may take over.
				bool headSettled = held.Result != NoteResult.None || held.WasTouched;
				if (held.State != NoteState.Done && covered && headSettled)
				{
					held.KeepAlive(frame.time);
				}

				// A finger on a bar in play has pressed a long note: it may not light the empty lanes
				// again until it leaves the screen.
				if (_silencedTouches != null && held.State != NoteState.Done && covered && (headSettled || !held.HasJudgment))
				{
					for (int inputIndex = 0; inputIndex < count; inputIndex++)
					{
						LiveLogic.InputTmp input = inputs[inputIndex];
						if (input != null && !IsEndedOrCanceled(input.Touch.phase) && TouchesBar(input.Lane, held))
						{
							_silencedTouches.Add(input.Touch.touchId);
						}
					}
				}

				if (LiveTrace.IsOn(LiveTraceCategory.Resident))
				{
					LiveTrace.Write(LiveTraceCategory.Resident,
						$"tailFrame barT={held.MusicScoreInfo.time:F3} now={frame.time:F3} state={held.State} covered={covered} held={held.HasFinger} settled={headSettled} child={(held.ChildNote == null ? -1f : held.ChildNote.MusicScoreInfo.time):F3} childRes={(held.ChildNote == null ? NoteResult.None : held.ChildNote.Result)}");
				}

				// Ordinary lifts already settled in JudgeLifts. Every other end grades itself (or is not
				// graded at all), and the bar only learns whether it may leave.
				if (!held.TailBehaviour.AcceptsRelease)
				{
					held.TailBehaviour.TryFinish(held, new TailFrame(frame.time, false));
				}

				// The tail can keep judging after the body reaches its endpoint.
				held.ExpireTail(frame.time);
				if (held.State != NoteState.Done && held.HasPendingManagedTail
					&& frame.time >= held.TailNote.MusicScoreInfo.time)
				{
					held.CompleteTail();
				}
			}
		}

		// Where the bar is now: a bending body leaves its head's lanes, and the finger that follows it
		// must keep covering it.
		private static bool TouchesBar(float lane, LongNote bar) =>
			!float.IsNegativeInfinity(lane) && HoldSweep.TouchesLane(lane, bar.BodyLaneStart, bar.BodyLaneEnd);

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
				if (float.IsNegativeInfinity(lane) || !held.ContainsLane(lane))
				{
					continue;
				}

				held.RefreshHeldInput(ref input.Touch, lane);
				refreshed = true;
			}

			return refreshed;
		}


		/// <summary>
		/// How soon after a consumed tap or flick press a lift of the same contact counts as the end of
		/// that tap rather than a deliberate release: 100 ms.
		/// </summary>
		internal const float QuickTapSeconds = 0.1f;
		private const float NextRushReleaseLockoutSeconds = 1f / 6f;

		/// <summary>
		/// Collects ordinary tails in reach. A contact whose press was consumed by a tap or by a flick's
		/// press step, and that lifts within <see cref="QuickTapSeconds"/> of it, is finishing that tap: it
		/// may only release a tail inside that tail's Perfect band, whichever tail it is. Whether a tail's
		/// window was open at the press does not matter.
		/// </summary>
		private void CollectLiftCandidates(List<NoteBase> judgmentNotes, LiveLogic.InputTmp lift)
		{
			float lane = lift.Lane;
			float time = lift.Touch.musicTime;
			bool followsKeyPress = _keyPressTimes.TryGetValue(lift.Touch.touchId, out float pressTime);
			bool tracing = LiveTrace.IsOn(LiveTraceCategory.Release);
			System.Text.StringBuilder candidates = tracing ? new System.Text.StringBuilder() : null;

			foreach (NoteBase note in judgmentNotes)
			{
				if (note is NormalNote { IsIndependentRelease: true } release)
				{
					if (release.IsJudgment(ref lift.Touch, lane)
						&& (!followsKeyPress || time - pressTime > NextRushReleaseLockoutSeconds
							|| LongEndJudgement.IsPerfect(time - release.MusicScoreInfo.time)))
					{
						lift.CandidateNotes.Add(release);
					}
					continue;
				}

				if (note is not LongNote candidate)
				{
					continue;
				}

				// The lift finishes a tail, so it is the tail's own lane that decides - a tail may sit
				// on another lane than the body it hangs from, and the body's range is the widened
				// judge range anyway, which reaches into the neighbours.
				NoteBase tail = candidate.TailNote;
				if (!IsReleaseKey(candidate, tail) || !InTailRange(lane, tail)
					|| !candidate.IsTailJudgeableAt(time) || lift.CandidateNotes.Contains(tail))
				{
					continue;
				}

				float tailTime = tail.MusicScoreInfo.time;
				float lockout = candidate.UsesNextRushJudgment ? NextRushReleaseLockoutSeconds : QuickTapSeconds;
				bool quickTap = followsKeyPress && time - pressTime <= lockout;
				if (quickTap && !LongEndJudgement.IsPerfect(time - tailTime))
				{
					if (tracing)
						LiveTrace.Write(LiveTraceCategory.Release,
							$"liftOutsidePerfect touch={lift.Touch.touchId} tailId={tail.Id} pressT={pressTime:F3} tailT={tailTime:F3} now={time:F3}");
					continue;
				}

				lift.CandidateNotes.Add(tail);
				if (tracing)
				{
					if (candidates.Length > 0) candidates.Append(';');
					candidates.Append($"id={tail.Id},type={tail.GetType().Name},t={tailTime:F3},lane={tail.JudgeLaneStart:F2}..{tail.JudgeLaneEnd:F2},perfectOnly={quickTap}");
				}
			}

			if (tracing)
				LiveTrace.Write(LiveTraceCategory.Release, $"liftCandidates finger={lift.Touch.fingerId} touch={lift.Touch.touchId} lane={lane:F2} now={time:F3} candidates=[{candidates}]");
		}

		private static NoteBase PickLiftTarget(LiveLogic.InputTmp lift, ref MusicScoreInfo frame)
		{
			NoteBase best = null;
			float bestTime = float.MaxValue;
			float bestDistance = float.MaxValue;
			foreach (NoteBase tail in lift.CandidateNotes)
			{
				// Samples for one finger share the lift credit. A previous sample may have already settled this tail.
				if (tail.State == NoteState.Done) continue;

				float time = tail.MusicScoreInfo.time;
				float distance = tail.LaneDistance(ref frame, lift.Lane);
				if (best == null || time < bestTime || (time == bestTime
					&& (distance < bestDistance || (distance == bestDistance && LiveCandidatePipeline.PrecedesForOneFinger(tail, best)))))
				{
					best = tail;
					bestTime = time;
					bestDistance = distance;
				}
			}

			return best;
		}

		/// <summary>
		/// The one kind of key a lift is offered: a bar's ordinary end note - a plain note, not a flick, a
		/// rub or a hidden carrier, and not a decoration - that the bar finishes on release. The lane
		/// (<see cref="InTailRange"/>) and the window (<see cref="LongNote.IsTailJudgeableAt"/>, 125 ms early
		/// to 141.7 ms late) are checked next to it.
		/// </summary>
		internal static bool IsReleaseKey(LongNote bar, NoteBase tail) =>
			tail is NormalNote && !tail.IsDecoration && tail.State != NoteState.Done
			&& bar.TailBehaviour.AcceptsRelease;

		private static bool InTailRange(float lane, NoteBase tail) =>
			lane >= tail.JudgeLaneStart && lane <= tail.JudgeLaneEnd;

		private static bool IsEndedOrCanceled(UnityEngine.InputSystem.TouchPhase phase)
		{
			return phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
		}
	}
}
