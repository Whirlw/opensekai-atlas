using System.Collections.Generic;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Orders and prunes the candidates each finger carries into judging.
	///
	/// A finger may touch several notes at once — overlapping lanes, a bar with a contact note laid
	/// over it — but a press only judges one candidate per frame. Who wins is decided here: rank by
	/// what the frame can still settle (a flick or a rub needs this frame, a held bar is kept alive by
	/// the refresh pass), then by score time, then by lane distance; and a note two fingers both
	/// claim goes to the one closer to it, except that the finger holding a bar keeps it.
	/// </summary>
	internal sealed class LiveCandidatePipeline
	{
		/// <summary>Maps a note and a finger to the lane that finger occupies for that note.</summary>
		public delegate float LaneOf(ref NoteBase note, ref LiveLogic.InputTmp input);

		/// <summary>
		/// Decides whether a note joins the list the judging pass walks, and whether it is the note
		/// itself or the one a bar ends with.
		/// </summary>
		/// <param name="note">The note the score just reached.</param>
		/// <param name="judgmentNotes">The list to add to.</param>
		/// <param name="frameTime">This frame's music time, already stepped to when the note must be ready.</param>
		public void Register(NoteBase note, List<NoteBase> judgmentNotes, float frameTime)
		{
			if (note == null || judgmentNotes == null)
			{
				return;
			}

			// Registration uses the JUDGEABLE lead, not the display lead. A fast note is drawn very
			// late (its display lead is divided by the speed ratio) but must still be judgeable 0.35 s
			// early; tying registration to the display lead put a rub behind a flick into the list only
			// ~130 ms before its time, already past the edge of the bad window.
			if (OpenSekai.Shared.Live.JudgeListLead.IsStillTooFarAhead(frameTime, note.MusicScoreInfo.time))
			{
				return;
			}

			// FrictionHideLongNote itself has no scoring judgment, but it is the hidden input carrier for trace long notes.
			// It must still receive held input frames so its generated LongHoldCombo points are judged by LongNote.Judgment.
			// Decoration notes are auto-judged and should not receive player input.
			// A decorative bar still owns a real tail, and that tail has to be playable, so such
			// a bar reaches the judgement list even though the body itself takes no input.
			NoteBase tailNote = note is LongNote barWithTail && barWithTail.ChildNote is NoteBase boundTail && boundTail.HasJudgment
				? boundTail
				: null;
			// A bar answers input only when it can grade itself: a Guide bar and a decoration-flagged
			// bar both cannot, and answering for them is what produced a bar hit effect with no note.
			bool barAnswersInput = (note.HasJudgment && !note.IsDecoration) || note is FrictionHideLongNote;
			if (barAnswersInput)
			{
				judgmentNotes.Add(note);
			}

			// A decoration bar speaks for nobody: the note at its end is the one the player plays, and
			// it is judged only through this list, never through its bar. Registering the bar instead
			// made the bar answer input (its own hit effect) while a flick or a rub at its end, whose
			// key lives inside the bar, never received a candidate slot at all.
			// A Guide bar cannot judge itself and is not flagged as decoration by the chart: its type
			// alone says "no scoring judgment". The note at its end is a real one, so that note is
			// registered instead of the bar - registering the bar let it answer input with its own hit
			// effect while a flick or a rub at its end never got a candidate slot.
			if (!barAnswersInput && tailNote != null && !judgmentNotes.Contains(tailNote))
			{
				judgmentNotes.Add(tailNote);
			}
		}

		/// <summary>
		/// Per finger: rank the candidates, then drop the farther note of any same-time pair.
		/// </summary>
		public void Sort(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, LaneOf laneOf)
		{
			for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (input.CandidateNotes.Count <= 1)
				{
					continue;
				}

				// The keys a comparison needs — what the note would score right now, its time, its
				// distance — do not change while the list is being sorted, so they are read once here
				// instead of inside the comparison, which cannot take the finger by reference.
				int count = input.CandidateNotes.Count;
				NoteBase[] notes = new NoteBase[count];
				int[] ranks = new int[count];
				NoteResult[] results = new NoteResult[count];
				float[] times = new float[count];
				float[] distances = new float[count];
				for (int i = 0; i < count; i++)
				{
					NoteBase note = input.CandidateNotes[i];
					notes[i] = note;
					if (note == null)
					{
						continue;
					}

					NoteBase laneNote = note;
					ranks[i] = CandidateRank(note);
					results[i] = note.CalcNoteResult(input.Touch.musicTime);
					times[i] = (float)note.MusicScoreInfo.time;
					distances[i] = note.LaneDistance(ref frame, laneOf(ref laneNote, ref input));
				}

				int[] order = new int[count];
				for (int i = 0; i < count; i++)
				{
					order[i] = i;
				}

				System.Array.Sort(order, (left, right) =>
				{
					NoteBase a = notes[left];
					NoteBase b = notes[right];
					if (ReferenceEquals(a, b))
					{
						return 0;
					}

					return OpenSekai.Shared.Live.CandidateOrder.Compare(
						ranks[left], results[left], times[left], distances[left],
						ranks[right], results[right], times[right], distances[right]);
				});

				for (int i = 0; i < count; i++)
				{
					input.CandidateNotes[i] = notes[order[i]];
				}

				// Same-time neighbours: keep the one that would score better now, or the closer one.
				for (int i = input.CandidateNotes.Count - 1; i > 0; i--)
				{
					NoteBase current = input.CandidateNotes[i];
					NoteBase previous = input.CandidateNotes[i - 1];
					if (current == null || previous == null || !LiveLogic.IsSameJudgmentTime(current, previous))
					{
						continue;
					}

					NoteBase currentLaneNote = current;
					NoteBase previousLaneNote = previous;
					float currentDistance = current.LaneDistance(ref frame, laneOf(ref currentLaneNote, ref input));
					float previousDistance = previous.LaneDistance(ref frame, laneOf(ref previousLaneNote, ref input));
					if (OpenSekai.Shared.Live.CandidateOrder.KeepsFirst(CandidateRank(previous), previousDistance, CandidateRank(current), currentDistance))
					{
						input.CandidateNotes.RemoveAt(i);
					}
					else
					{
						input.CandidateNotes.RemoveAt(i - 1);
					}
				}
			}

			ResolveSharedCandidates(inputs, frame, laneOf);
		}

		/// <summary>
		/// Drops the candidates a finger cannot use this frame: a second note with the same judgement
		/// time, or one so far behind that a better candidate already covers the frame.
		/// </summary>
		public void DropExtraFastGeneration(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, LaneOf laneOf)
		{
			for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					if (input.CandidateNotes.Count < 2)
					{
						break;
					}

					NoteBase first = ResolveFastGenerationNote(input.CandidateNotes[0], input, frame, laneOf);
					NoteBase current = ResolveFastGenerationNote(input.CandidateNotes[i], input, frame, laneOf);
					if (first == null || current == null)
					{
						continue;
					}

					int dropIndex;
					if (LiveLogic.IsSameJudgmentTime(first, current))
					{
						NoteBase firstNote = first;
						NoteBase currentNote = current;
						float firstDistance = first.LaneDistance(ref frame, laneOf(ref firstNote, ref input));
						float currentDistance = current.LaneDistance(ref frame, laneOf(ref currentNote, ref input));
						dropIndex = firstDistance < currentDistance ? i : 0;
					}
					else if (current.CalcNoteResult(input.Touch.musicTime) >= NoteResult.Great
						&& first.CalcNoteResult(input.Touch.musicTime) <= NoteResult.Good)
					{
						dropIndex = 0;
					}
					else if (first.MusicScoreInfo.time < current.MusicScoreInfo.time)
					{
						dropIndex = i;
					}
					else
					{
						dropIndex = 0;
					}

					int keepIndex = dropIndex == 0 ? i : 0;
					bool dropHeld = IsHeld(input.CandidateNotes[dropIndex]);
					bool keepHeld = IsHeld(input.CandidateNotes[keepIndex]);

					// A held note must keep receiving its finger every frame, otherwise it releases.
					// When both are held there is nothing safe to prune, so keep both for this pass.
					if (dropHeld && keepHeld)
					{
						continue;
					}

					if (dropHeld)
					{
						dropIndex = keepIndex;
					}

					input.CandidateNotes.RemoveAt(dropIndex);
					i = input.CandidateNotes.Count;
				}
			}
		}

		/// <summary>
		/// A long note that is currently held by a finger. Such a note must stay at the front of that
		/// finger's candidate list, because a press only judges one candidate per frame and a hold
		/// that stops being picked stops receiving input and releases.
		/// </summary>
		private static bool IsHeld(NoteBase note)
		{
			return note is LongNote longNote
				&& (longNote.State == NoteState.InputBegan || longNote.State == NoteState.Input);
		}

		/// <summary>
		/// Which candidate wins the frame's single judgment slot. A held long note ranks last: the
		/// refresh pass keeps it alive, while a flick or a rub/connection note can only be completed
		/// by running Judgment on this frame.
		/// </summary>
		internal static int CandidateRank(NoteBase note)
		{
			if (note is FlickNote or FrictionFlickNote)
			{
				return OpenSekai.Shared.Live.CandidateOrder.FlickRank;
			}

			if (note is LongNote held && held.HoldingTouchId >= 0)
			{
				return OpenSekai.Shared.Live.CandidateOrder.HeldRank;
			}

			return OpenSekai.Shared.Live.CandidateOrder.TouchRank;
		}

		private static NoteBase ResolveFastGenerationNote(NoteBase note, LiveLogic.InputTmp input, MusicScoreInfo frame, LaneOf laneOf)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			NoteBase resolved = note.Result != NoteResult.None || input.Touch.phase != UnityEngine.InputSystem.TouchPhase.Began
				? LiveLogic.GetLastNote(note)
				: LiveLogic.GetFirstNote(note);
			if (resolved == null)
			{
				return note;
			}

			NoteBase laneNote = resolved;
			float lane = laneOf(ref laneNote, ref input);
			if (lane < 0f || !resolved.IsJudgment(ref input.Touch, lane))
			{
				return note;
			}

			return resolved;
		}

		/// <summary>
		/// A note two fingers both claim goes to one of them: the holder keeps a bar it is holding,
		/// otherwise the closer finger wins and the other drops it.
		/// </summary>
		private static void ResolveSharedCandidates(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, LaneOf laneOf)
		{
			for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				if (input.CandidateNotes.Count == 0)
				{
					continue;
				}

				for (int otherInputIndex = inputIndex - 1; otherInputIndex >= 0; otherInputIndex--)
				{
					LiveLogic.InputTmp otherInput = inputs[otherInputIndex];
					if (otherInput.CandidateNotes.Count == 0)
					{
						continue;
					}

					for (int candidateIndex = input.CandidateNotes.Count - 1; candidateIndex >= 0; candidateIndex--)
					{
						NoteBase candidate = input.CandidateNotes[candidateIndex];
						int otherCandidateIndex = otherInput.CandidateNotes.IndexOf(candidate);
						if (candidate == null || otherCandidateIndex < 0)
						{
							continue;
						}

						NoteBase inputNote = ResolveJudgmentNote(candidate, input);
						NoteBase otherNote = ResolveJudgmentNote(candidate, otherInput);
						float inputDistance = float.MaxValue;
						if (inputNote != null)
						{
							NoteBase laneNote = inputNote;
							inputDistance = inputNote.LaneDistance(ref frame, laneOf(ref laneNote, ref input));
						}

						float otherDistance = float.MaxValue;
						if (otherNote != null)
						{
							NoteBase laneNote = otherNote;
							otherDistance = otherNote.LaneDistance(ref frame, laneOf(ref laneNote, ref otherInput));
						}

						int holdingTouchId = candidate is LongNote heldLongNote ? heldLongNote.HoldingTouchId : -1;
						bool inputHolds = holdingTouchId >= 0 && holdingTouchId == input.Touch.fingerId;
						bool otherInputHolds = holdingTouchId >= 0 && holdingTouchId == otherInput.Touch.fingerId;

						if (OpenSekai.Shared.Live.CandidateOwnership.Loser(inputHolds, otherInputHolds, inputDistance, otherDistance) == OpenSekai.Shared.Live.CandidateSide.Current)
						{
							input.CandidateNotes.RemoveAt(candidateIndex);
						}
						else
						{
							otherInput.CandidateNotes.RemoveAt(otherCandidateIndex);
						}
					}
				}
			}
		}

		/// <summary>
		/// Fills each finger's candidate list from the judgement list for this frame, and returns the
		/// notes that are only being connected to (they may still be picked up later).
		/// </summary>
		public void Collect(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs,
			List<NoteBase> connectionNotes, LiveHoldCoordinator holds, LaneOf laneOf,
			Dictionary<int, double> pressed)
		{
			if (judgmentNotes == null || inputs == null || connectionNotes == null)
			{
				return;
			}

			holds?.AssignContinuingHolds(judgmentNotes, inputs, new LiveHoldCoordinator.LaneOf(laneOf));

			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				NoteBase judgmentNote = judgmentNotes[noteIndex];
				if (judgmentNote == null)
				{
					continue;
				}

				bool isConnection = true;
				bool isJudged = false;
				for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
				{
					LiveLogic.InputTmp activeInput = inputs[inputIndex];
					NoteBase note = judgmentNote;
					// A finger that owns a hold is still free to judge other notes in its lane:
					// charts overlay a separate contact-only note on top of a bar. Only hold-vs-hold
					// contention needs the reservation.
					int heldNote = holds?.NoteOf(inputIndex) ?? -1;
					if (heldNote >= 0 && heldNote != noteIndex && judgmentNote is LongNote)
					{
						continue;
					}

					// Another finger is already holding this long note: leave it to that finger, otherwise
					// the second finger nominates it, steals the holder and the hold drops for a frame.
					if (IsReservedForAnotherInput(judgmentNote, activeInput, inputs, laneOf))
					{
						continue;
					}

					float lane = laneOf(ref note, ref activeInput);

					if (lane >= 0f && note.IsJudgment(ref activeInput.Touch, lane))
					{
						isJudged = true;
						int touchId = activeInput.Touch.touchId;
						NoteBase lastNote = LiveLogic.GetLastNote(judgmentNote);
						if (judgmentNote.NoteList != null && judgmentNote.NoteList.Count == 1)
						{
							AddCheckSingleInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId, laneOf, pressed);
						}
						else
						{
							AddCheckLongInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId, laneOf, pressed);
						}

						continue;
					}

					NoteBase firstNote = LiveLogic.GetFirstNote(judgmentNote);
					if (firstNote != null && TryAddConnectionCandidate(firstNote, activeInput, laneOf))
					{
						isConnection = true;
						isJudged = true;
					}
				}

				if (isJudged && isConnection)
				{
					connectionNotes.Add(judgmentNote);
				}
			}
		}

		/// <summary>
		/// Promotes notes that were only connected to on an earlier frame, once a finger is close
		/// enough to actually judge them.
		/// </summary>
		public void PickUp(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs,
			List<NoteBase> connectionNotes, LiveHoldCoordinator holds, LaneOf laneOf,
			Dictionary<int, double> pressed)
		{
			if (judgmentNotes == null || inputs == null || connectionNotes == null)
			{
				return;
			}

			for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
			{
				LiveLogic.InputTmp input = inputs[inputIndex];
				for (int noteIndex = connectionNotes.Count - 1; noteIndex >= 0; noteIndex--)
				{
					NoteBase note = connectionNotes[noteIndex];
					int heldNote = holds?.NoteOf(inputIndex) ?? -1;
					if (heldNote >= 0 && note is LongNote
						&& !ReferenceEquals(note, judgmentNotes[heldNote])) continue;
					// Allocation's ownership check must also apply to this deferred path. Otherwise
					// a newer overlapping input removes the hold before its owner can pick it up,
					// even though LongNote.Judgment later rejects that foreign input.
					if (IsReservedForAnotherInput(note, input, inputs, laneOf))
					{
						continue;
					}

					float lane = laneOf(ref note, ref input);
					if (lane >= 0f && note != null && note.IsJudgment(ref input.Touch, lane))
					{
						input.CandidateNotes.Add(note);
						connectionNotes.RemoveAt(noteIndex);
					}
				}
			}
		}

		/// <summary>Whether a hold already belongs to a different finger this frame.</summary>
		public static bool ShouldReserveHold(int holder, int candidate, NoteState state, bool ownerCanJudge)
		{
			return holder >= 0 && holder != candidate && ownerCanJudge
				&& (state == NoteState.InputBegan || state == NoteState.Input || state == NoteState.Release);
		}

		private static bool IsReservedForAnotherInput(NoteBase note, LiveLogic.InputTmp input,
			List<LiveLogic.InputTmp> inputs, LaneOf laneOf)
		{
			if (note is not LongNote held || held.HoldingTouchId < 0 || held.HoldingTouchId == input.Touch.fingerId)
			{
				return false;
			}

			// Excute can mark yesterday's hold Release before today's input pass (MissMesh is zero).
			// Reserve by the owner's actual eligible input this frame, not by yesterday's timestamp.
			for (int i = 0; i < inputs.Count; i++)
			{
				LiveLogic.InputTmp owner = inputs[i];
				if (owner.Touch.fingerId != held.HoldingTouchId || LiveLogic.IsEndedOrCanceled(owner.Touch.phase)) continue;
				float lane = laneOf(ref note, ref owner);
				if (ShouldReserveHold(held.HoldingTouchId, input.Touch.fingerId, held.State,
					lane >= 0f && held.IsJudgment(ref owner.Touch, lane))) return true;
			}

			return held.IsHeldByAnotherFinger(input.Touch.fingerId, input.Touch.musicTime);
		}

		private static void AddCheckSingleInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note,
			ref LiveLogic.InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId,
			LaneOf laneOf, Dictionary<int, double> pressed)
		{
			if (judgmentNote is FrictionNote)
			{
				float frictionLane = laneOf(ref note, ref activeInput);
				if (judgmentNote.IsJudgment(ref activeInput.Touch, frictionLane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			if (note is FrictionLongNote or FrictionHideLongNote)
			{
				float frictionLane = laneOf(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, frictionLane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			activeInput.CandidateNotes.Add(judgmentNote);
			isConnection = false;
			if (pressed != null)
			{
				pressed[touchId] = activeInput.Touch.musicTime;
			}
		}

		private static void AddCheckLongInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note,
			ref LiveLogic.InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId,
			LaneOf laneOf, Dictionary<int, double> pressed)
		{
			float lane = laneOf(ref note, ref activeInput);
			bool hadInput = pressed != null && pressed.ContainsKey(touchId);
			bool isLastNoteJudgment = note != null && note.IsJudgment(ref activeInput.Touch, lane);
			if (hadInput)
			{
				if (isLastNoteJudgment)
				{
					isConnection = false;
					if (note.CalcNoteResult(activeInput.Touch.musicTime) > NoteResult.Great
						|| activeInput.Touch.musicTime - pressed[touchId] > 1f / 6f)
					{
						activeInput.CandidateNotes.Add(judgmentNote);
						return;
					}
				}
			}
			else if (isLastNoteJudgment)
			{
				activeInput.CandidateNotes.Add(judgmentNote);
				isConnection = false;
				return;
			}

			note = LiveLogic.GetFirstNote(judgmentNote);
			lane = laneOf(ref note, ref activeInput);
			if ((note is FrictionLongNote or FrictionHideLongNote)
				&& note.IsJudgment(ref activeInput.Touch, lane))
			{
				isConnection = true;
				isJudged = true;
				return;
			}

			if (note != null && note.Result == NoteResult.None)
			{
				lane = laneOf(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					activeInput.CandidateNotes.Add(judgmentNote);
					isConnection = false;
					return;
				}
			}

			if (note is LongNote)
			{
				lane = laneOf(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
				}
			}
		}

		private static bool TryAddConnectionCandidate(NoteBase note, LiveLogic.InputTmp input, LaneOf laneOf)
		{
			if (note is not LongNote and not FrictionLongNote and not FrictionHideLongNote)
			{
				return false;
			}

			float lane = laneOf(ref note, ref input);
			if (lane < 0f || !note.IsJudgment(ref input.Touch, lane))
			{
				return false;
			}

			input.CandidateNotes.Add(note);
			return true;
		}

		private static NoteBase ResolveJudgmentNote(NoteBase note, LiveLogic.InputTmp input)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			if (note.Result != NoteResult.None || input.Touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
			{
				return LiveLogic.GetLastNote(note);
			}

			return LiveLogic.GetFirstNote(note);
		}
	}
}
