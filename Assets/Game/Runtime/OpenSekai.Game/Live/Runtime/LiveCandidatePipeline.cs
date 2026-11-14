using System.Collections.Generic;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Orders and prunes the candidates each finger carries into judging.
	///
	/// A finger may touch several notes at once — overlapping lanes, a bar with a contact note laid
	/// over it — but a press only judges one candidate per frame. Who wins is decided here, by two
	/// orders written out as functions: a note two fingers both reach goes to the finger nearer its
	/// lane centre, then the smaller finger id (<see cref="ClaimsSharedNote"/>); what one finger has
	/// left is ranked by chart time, then chart order (<see cref="PrecedesForOneFinger"/>).
	///
	/// Nothing here spends a press. The scheduler records a press when a note consumes it.
	/// </summary>
	internal sealed class LiveCandidatePipeline
	{
		/// <summary>
		/// Whether a note spends a finger's single press slot. Only notes a press answers on one frame are
		/// here: an ordinary note and a long note's HEAD as its own candidate. A flick is not one of them -
		/// it reads a swipe over several frames, so the resident pass drives it instead. A bar's body is
		/// never a press step either, which is what keeps a press on an ordinary note that overlaps a
		/// hidden head from touching the bar.
		/// </summary>
		internal static bool IsPressStep(NoteBase note) => note != null && !note.IsDecoration
			&& (note is LongNoteHeadNote head ? head.IsAnswerable
				: note.ParentRole == OpenSekai.Shared.Live.NoteParentRole.None &&
				  note is NormalNote && note.Category == NoteCategory.Normal);

		/// <summary>
		/// Leaves every finger at most one press candidate, by two explicit orders.
		///
		/// A note two fingers both reach goes to one of them first: <see cref="ClaimsSharedNote"/> says
		/// which. What each finger has left is then ranked by <see cref="PrecedesForOneFinger"/>, and the
		/// first press step is the one it keeps. A finger that lost a shared note is not offered another
		/// one in its place: that is the single-slot policy, and re-pairing it is a separate decision.
		/// </summary>
		internal void SortPressCandidates(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame)
		{
			// Resolve every shared note before reducing any finger's list to its earliest note.
			foreach (var input in inputs)
			{
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					NoteBase note = input.CandidateNotes[i];
					bool loses = false;
					foreach (var other in inputs)
					{
						if (ReferenceEquals(input, other) || !other.CandidateNotes.Contains(note)) continue;
						if (ClaimsSharedNote(note, other, input, ref frame))
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
				SortForOneFinger(input.CandidateNotes);

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

		/// <summary>
		/// Whether <paramref name="claimant"/> takes a note it shares with <paramref name="other"/>: the
		/// finger nearer the note's lane centre wins, and at the same distance the smaller finger id.
		/// Two samples of one finger never take a note from each other; they share one press credit,
		/// which the scheduler spends.
		/// </summary>
		internal static bool ClaimsSharedNote(NoteBase note, LiveLogic.InputTmp claimant, LiveLogic.InputTmp other,
			ref MusicScoreInfo frame)
		{
			float claimantDistance = note.LaneDistance(ref frame, claimant.Lane);
			float otherDistance = note.LaneDistance(ref frame, other.Lane);
			if (claimantDistance != otherDistance)
			{
				return claimantDistance < otherDistance;
			}

			return claimant.Touch.fingerId < other.Touch.fingerId;
		}

		/// <summary>
		/// The order of one finger's own candidates: the earlier chart time first, and at the same time
		/// the note that comes first in the chart.
		/// </summary>
		internal static bool PrecedesForOneFinger(NoteBase a, NoteBase b)
		{
			float aTime = a.MusicScoreInfo.time;
			float bTime = b.MusicScoreInfo.time;
			if (aTime != bTime)
			{
				return aTime < bTime;
			}

			return a.ChartOrder < b.ChartOrder;
		}

		/// <summary>
		/// A stable insertion sort by <see cref="PrecedesForOneFinger"/>. The lists hold a handful of notes,
		/// and stability keeps two notes the order cannot tell apart in the order they were collected.
		/// </summary>
		private static void SortForOneFinger(List<NoteBase> notes)
		{
			for (int i = 1; i < notes.Count; i++)
			{
				NoteBase current = notes[i];
				int j = i - 1;
				while (j >= 0 && PrecedesForOneFinger(current, notes[j]))
				{
					notes[j + 1] = notes[j];
					j--;
				}

				notes[j + 1] = current;
			}
		}

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
				if (note is LongNote selfJudgingBar && selfJudgingBar.HasIndependentTail
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
		///
		/// Collecting spends nothing. <paramref name="pressed"/> holds only the touches whose press a note
		/// already consumed, and the scheduler writes it at that point; a candidate that has not been hit
		/// cannot keep a later candidate out.
		/// </summary>
		public void Collect(List<NoteBase> judgmentNotes, List<LiveLogic.InputTmp> inputs,
			LiveHoldCoordinator holds, IReadOnlyDictionary<int, double> pressed)
		{
			if (judgmentNotes == null || inputs == null)
			{
				return;
			}

			holds?.AssignContinuingHolds(judgmentNotes, inputs);

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

					float lane = activeInput.Lane;
					if (lane < 0f || !judgmentNote.IsJudgment(ref activeInput.Touch, lane))
					{
						continue;
					}

					// One touch judges one note. A touch whose press a note already consumed may be offered
					// another note only when that note would grade better than Great, or a sixth of a second
					// has passed, which is what lets a fast run of notes still be played with one finger.
					int touchId = activeInput.Touch.touchId;
					if (pressed != null && pressed.TryGetValue(touchId, out double consumedAt)
						&& judgmentNote.CalcNoteResult(activeInput.Touch.musicTime) <= NoteResult.Great
						&& activeInput.Touch.musicTime - consumedAt <= 1f / 6f)
					{
						continue;
					}

					// A head can be registered twice in one frame (from the open-head list and from the
					// walk); it is still one candidate.
					if (!activeInput.CandidateNotes.Contains(judgmentNote))
					{
						activeInput.CandidateNotes.Add(judgmentNote);
					}				}
			}
		}

	}
}
