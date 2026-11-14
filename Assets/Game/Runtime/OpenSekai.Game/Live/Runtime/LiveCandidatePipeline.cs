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
		/// <summary>
		/// Whether a note spends a finger's single press slot. Only notes the player presses are here: an
		/// ordinary note, a flick, and a long note's HEAD as its own candidate. A bar's body is never a press
		/// step - the resident hold pass drives it every frame instead - which is what keeps a press on an
		/// ordinary note that overlaps a hidden head from touching the bar.
		/// </summary>
		internal static bool IsPressStep(NoteBase note) => note != null && !note.IsDecoration
			&& (note is LongNoteHeadNote head ? head.IsAnswerable
				: note.ParentRole == OpenSekai.Shared.Live.NoteParentRole.None &&
				  (note is NormalNote && note.Category == NoteCategory.Normal || note is FlickNote && !note.FinishFirstAction));

		internal void SortPressCandidates(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame, LaneOf laneOf)
		{
			// Resolve every shared note before reducing any finger's list to its earliest note.
			foreach (var input in inputs)
			{
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					NoteBase note = input.CandidateNotes[i];
					var currentInput = input;
					float distance = note.LaneDistance(ref frame, laneOf(ref note, ref currentInput));
					bool loses = false;
					foreach (var other in inputs)
					{
						if (ReferenceEquals(input, other) || !other.CandidateNotes.Contains(note)) continue;
						NoteBase otherNote = note;
						var contender = other;
						float otherDistance = note.LaneDistance(ref frame, laneOf(ref otherNote, ref contender));
						if (otherDistance < distance || (otherDistance == distance && other.Touch.fingerId < input.Touch.fingerId))
						{
							loses = true;
							break;
						}
					}
					if (loses) input.CandidateNotes.RemoveAt(i);
				}
			}
			foreach (var input in inputs)
			{
				input.CandidateNotes.Sort((a, b) => a.MusicScoreInfo.time.CompareTo(b.MusicScoreInfo.time));

				// The finger's single press slot goes to the earliest note the press channel can actually
				// answer. A bar's body is always earlier than the notes it overlaps and the press loop skips
				// it, so taking it "because it is earliest" threw the press away: a second press in a lane
				// where a bar is being held never reached the next head, which then missed. Device log
				// live-20261113-155947.log line 212781: a Began at lane 9.49 delivered to the held bar
				// barT=39.158 instead of the head at t=40.421, which missed at 40.565.
				int pressIndex = -1;
				for (int i = 0; i < input.CandidateNotes.Count; i++)
				{
					if (IsPressStep(input.CandidateNotes[i]))
					{
						pressIndex = i;
						break;
					}
				}

				if (pressIndex > 0)
				{
					NoteBase press = input.CandidateNotes[pressIndex];
					input.CandidateNotes.RemoveAt(pressIndex);
					input.CandidateNotes.Insert(0, press);
				}

				if (input.CandidateNotes.Count > 1)
				{
					input.CandidateNotes.RemoveRange(1, input.CandidateNotes.Count - 1);
				}
			}
		}
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
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, $"regSkip id={note.Id} t={note.MusicScoreInfo.time:F3} now={frameTime:F3}");
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

				// A bar answers input for its own head and hold, but it does not grade a tail that judges
				// itself: a rub, a rub flick or a hidden carrier is played exactly like a note that hangs
				// on no bar at all, so it needs a slot of its own. Without this the note could only ever
				// time out, never be played.
				if (note is LongNote selfJudgingBar && selfJudgingBar.TailBehaviour.JudgesItself
					&& tailNote != null && !judgmentNotes.Contains(tailNote))
				{
					judgmentNotes.Add(tailNote);
				}
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
		/// Fills each finger's press candidates from the judgement list for this frame.
		///
		/// Only a press step is ever nominated: an ordinary note, a flick's press part, or an ordinary bar
		/// head. A bar's body, a hidden point or an interior combo point is never a press candidate - they are
		/// driven by their own per-frame passes - so nothing here can spend a finger's press on something the
		/// press cannot answer.
		/// </summary>
		public void Collect(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs,
			LiveHoldCoordinator holds, LaneOf laneOf, Dictionary<int, double> pressed)
		{
			if (judgmentNotes == null || inputs == null)
			{
				return;
			}

			holds?.AssignContinuingHolds(judgmentNotes, inputs, new LiveHoldCoordinator.LaneOf(laneOf));

			for (int noteIndex = 0; noteIndex < judgmentNotes.Count; noteIndex++)
			{
				NoteBase judgmentNote = judgmentNotes[noteIndex];
				if (judgmentNote == null || !IsPressStep(judgmentNote))
				{
					continue;
				}

				for (int inputIndex = inputs.Count - 1; inputIndex >= 0; inputIndex--)
				{
					LiveLogic.InputTmp activeInput = inputs[inputIndex];
					if (activeInput.Touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

					// A finger that owns a hold is still free to judge other notes in its lane: charts
					// overlay a separate contact-only note on top of a bar. Only hold-vs-hold contention
					// needs the reservation.
					int heldNote = holds?.NoteOf(inputIndex) ?? -1;
					if (heldNote >= 0 && heldNote != noteIndex && judgmentNote is LongNote)
					{
						continue;
					}

					NoteBase note = judgmentNote;
					float lane = laneOf(ref note, ref activeInput);
					if (lane < 0f || !note.IsJudgment(ref activeInput.Touch, lane))
					{
						continue;
					}

					// One touch judges one note. Another note may claim the same touch only when its grade
					// would be worse than Great or a sixth of a second has passed, which is what lets a fast
					// run of notes still be played with one finger.
					int touchId = activeInput.Touch.touchId;
					bool spent = pressed != null && pressed.ContainsKey(touchId);
					if (spent
						&& note.CalcNoteResult(activeInput.Touch.musicTime) <= NoteResult.Great
						&& activeInput.Touch.musicTime - pressed[touchId] <= 1f / 6f)
					{
						continue;
					}

					activeInput.CandidateNotes.Add(judgmentNote);
					if (pressed != null)
					{
						pressed[touchId] = activeInput.Touch.musicTime;
					}
				}
			}
		}

	}
}
