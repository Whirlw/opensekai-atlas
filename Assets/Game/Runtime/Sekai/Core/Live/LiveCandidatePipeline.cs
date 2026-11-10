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
