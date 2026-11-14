using System.Collections.Generic;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Orders and prunes the candidates each finger carries into judging.
	///
	/// A finger may touch several notes at once - overlapping lanes, a bar with a contact note laid
	/// over it - but a press only judges one candidate per frame. Each finger starts with its own list
	/// of the keys it can judge; the lists are de-duplicated in three steps, and then each finger picks
	/// one key from what it has left (<see cref="SortPressCandidates"/>). A finger whose list ends up
	/// empty does not judge.
	///
	/// Nothing here spends a press. The scheduler records a press when a note consumes it.
	/// </summary>
	internal sealed class LiveCandidatePipeline
	{
		private readonly List<int> _group = new List<int>(10);
		private readonly List<NoteBase> _groupKeys = new List<NoteBase>(10);
		private readonly List<bool> _assigned = new List<bool>(10);

		/// <summary>
		/// Whether a note takes presses at all. There are exactly three kinds, and a key that has been
		/// judged leaves at once:
		/// <list type="bullet">
		/// <item>an ordinary tap note on no bar, until it is done;</item>
		/// <item>a flick on no bar, until a press arms it - from then on it only reads that finger's swipe,
		/// in the resident pass;</item>
		/// <item>a long note's ordinary head as its own candidate, until it is settled.</item>
		/// </list>
		/// A bar's body, a rub, a hidden point, an interior point and every tail are never press keys, which
		/// is what keeps a press on an ordinary note that overlaps a hidden head from touching the bar.
		/// </summary>
		internal static bool IsPressStep(NoteBase note)
		{
			if (note == null || note.IsDecoration || note.State == NoteState.Done)
			{
				return false;
			}

			if (note is LongNoteHeadNote head)
			{
				return head.IsAnswerable;
			}

			if (note.ParentRole != OpenSekai.Shared.Live.NoteParentRole.None || note.ParentNote != null)
			{
				return false;
			}

			if (note is FlickNote)
			{
				return note.State == NoteState.Scrolling;
			}

			return note is NormalNote && note.Category == NoteCategory.Normal;
		}

		/// <summary>
		/// The hard rule for offering a press to a key: it is one of the three press kinds
		/// (<see cref="IsPressStep"/>), the finger is inside the key's judge lanes - a head uses its bar's
		/// widened lanes - and the press is inside <see cref="OpenSekai.Shared.Live.PressCandidateWindow"/>:
		/// 125 ms either side for a tap or a head, 125 ms early to 141.7 ms late for a flick.
		/// </summary>
		internal static bool AdmitsPress(NoteBase note, float touchTime, float lane)
		{
			if (!IsPressStep(note) || lane < 0f)
			{
				return false;
			}

			float laneStart = note is LongNoteHeadNote head ? head.Bar.JudgeLaneStart : note.JudgeLaneStart;
			float laneEnd = note is LongNoteHeadNote headEnd ? headEnd.Bar.JudgeLaneEnd : note.JudgeLaneEnd;
			if (lane < laneStart || lane > laneEnd)
			{
				return false;
			}

			float offset = touchTime - note.MusicScoreInfo.time;
			return note is FlickNote
				? OpenSekai.Shared.Live.PressCandidateWindow.AcceptsFlick(offset)
				: OpenSekai.Shared.Live.PressCandidateWindow.AcceptsTap(offset);
		}

		/// <summary>
		/// Leaves every finger at most one press candidate.
		///
		/// <list type="number">
		/// <item>A finger that can judge only one key keeps it: the key leaves the list of every other
		/// finger that still has something else (<see cref="KeepSoleOptions"/>). When every finger sharing
		/// the key has only that key, this step leaves them alone.</item>
		/// <item>When exactly two fingers can judge exactly the same two keys, the keys are handed out in
		/// chart order: the earlier one to the nearer finger, the later one to the other
		/// (<see cref="AssignMatchedPairs"/>). The two keys are then assigned for good and leave every
		/// other finger's list. Larger groups are left to step 3.</item>
		/// <item>What is still shared goes by distance (<see cref="ClaimsSharedNote"/>).</item>
		/// <item>Each finger judges the earliest key it has left, unless that key would grade Good or
		/// worse and a later one would grade Great or better (<see cref="PickForOneFinger"/>).</item>
		/// </list>
		/// </summary>
		internal void SortPressCandidates(List<LiveLogic.InputTmp> inputs, MusicScoreInfo frame)
		{
			KeepSoleOptions(inputs);
			AssignMatchedPairs(inputs, ref frame);
			ResolveByDistance(inputs, ref frame);

			foreach (var input in inputs)
			{
				SortForOneFinger(input.CandidateNotes);
				int pick = PickForOneFinger(input.CandidateNotes, input.Touch.musicTime);
				if (pick < 0)
				{
					input.CandidateNotes.Clear();
					continue;
				}

				NoteBase chosen = input.CandidateNotes[pick];
				input.CandidateNotes.Clear();
				input.CandidateNotes.Add(chosen);
			}
		}

		/// <summary>
		/// Step 1. A key that some finger can judge alone leaves every other finger that still has another
		/// key. Removing a key can leave that finger with only one key in turn, so this repeats until
		/// nothing changes. A finger is never emptied here.
		/// </summary>
		internal static void KeepSoleOptions(List<LiveLogic.InputTmp> inputs)
		{
			bool changed = true;
			while (changed)
			{
				changed = false;
				foreach (var input in inputs)
				{
					for (int i = input.CandidateNotes.Count - 1; i >= 0 && input.CandidateNotes.Count > 1; i--)
					{
						if (i >= input.CandidateNotes.Count) continue;
						if (IsSoleOptionOfAnother(inputs, input, input.CandidateNotes[i]))
						{
							input.CandidateNotes.RemoveAt(i);
							changed = true;
						}
					}
				}
			}
		}

		private static bool IsSoleOptionOfAnother(List<LiveLogic.InputTmp> inputs, LiveLogic.InputTmp self, NoteBase note)
		{
			foreach (var other in inputs)
			{
				if (!ReferenceEquals(other, self) && other.CandidateNotes.Count == 1
					&& ReferenceEquals(other.CandidateNotes[0], note))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Step 2. Finds every pair of fingers whose lists are exactly the same two keys, with no third
		/// finger holding that same list, and hands the keys out in chart order: the earlier key goes to
		/// the nearer finger by <see cref="ClaimsSharedNote"/>, the later key to the other. Each finger of
		/// the pair is left with its own key only. The two keys are then assigned: they leave the list of
		/// every other finger, even when that empties it. Any other group size is left to step 3.
		/// </summary>
		internal void AssignMatchedPairs(List<LiveLogic.InputTmp> inputs, ref MusicScoreInfo frame)
		{
			for (int i = 0; i < inputs.Count; i++)
			{
				List<NoteBase> keys = inputs[i].CandidateNotes;
				if (keys.Count != 2)
				{
					continue;
				}

				_group.Clear();
				for (int j = 0; j < inputs.Count; j++)
				{
					if (SameKeys(keys, inputs[j].CandidateNotes))
					{
						_group.Add(j);
					}
				}

				// Exactly two fingers, and only the first of them does the work.
				if (_group.Count != 2 || _group[0] != i)
				{
					continue;
				}

				_groupKeys.Clear();
				_groupKeys.AddRange(keys);
				SortForOneFinger(_groupKeys);
				_assigned.Clear();
				for (int g = 0; g < _group.Count; g++) _assigned.Add(false);

				for (int k = 0; k < _groupKeys.Count; k++)
				{
					NoteBase key = _groupKeys[k];
					int nearest = -1;
					for (int g = 0; g < _group.Count; g++)
					{
						if (_assigned[g]) continue;
						if (nearest < 0 || ClaimsSharedNote(key, inputs[_group[g]], inputs[_group[nearest]], ref frame))
						{
							nearest = g;
						}
					}

					_assigned[nearest] = true;
					List<NoteBase> list = inputs[_group[nearest]].CandidateNotes;
					list.Clear();
					list.Add(key);
				}

				for (int j = 0; j < inputs.Count; j++)
				{
					if (j == _group[0] || j == _group[1]) continue;
					List<NoteBase> other = inputs[j].CandidateNotes;
					other.Remove(_groupKeys[0]);
					other.Remove(_groupKeys[1]);
				}
			}
		}

		private static bool SameKeys(List<NoteBase> a, List<NoteBase> b)
		{
			if (a.Count != b.Count)
			{
				return false;
			}

			for (int i = 0; i < a.Count; i++)
			{
				if (!b.Contains(a[i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Step 3. A key still on several lists stays only with the finger that claims it.</summary>
		private static void ResolveByDistance(List<LiveLogic.InputTmp> inputs, ref MusicScoreInfo frame)
		{
			foreach (var input in inputs)
			{
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					NoteBase note = input.CandidateNotes[i];
					foreach (var other in inputs)
					{
						if (ReferenceEquals(input, other) || !other.CandidateNotes.Contains(note)) continue;
						if (ClaimsSharedNote(note, other, input, ref frame))
						{
							input.CandidateNotes.RemoveAt(i);
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Step 4. The key one finger judges from its sorted list: the earliest press step, unless that key
		/// would grade Good or worse at this touch and a later press step would grade Great or better - then
		/// the earliest such later key. Only press steps are ever picked: a bar's body is always earlier
		/// than the notes it overlaps and the press loop cannot answer it (device log
		/// live-20261113-155947.log line 212781: a Began delivered to a held bar instead of the next head,
		/// which then missed).
		/// </summary>
		/// <returns>The index of the key, or -1 when the list has no press step.</returns>
		internal static int PickForOneFinger(List<NoteBase> sorted, float touchTime)
		{
			int earliest = -1;
			for (int i = 0; i < sorted.Count; i++)
			{
				if (!IsPressStep(sorted[i])) continue;
				if (sorted[i].CalcNoteResult(touchTime) >= NoteResult.Great)
				{
					return i;
				}

				if (earliest < 0)
				{
					earliest = i;
				}
			}

			return earliest;
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
				if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Resident))
					OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Resident, $"regSkip id={note.Id} t={note.MusicScoreInfo.time:F3} now={frameTime:F3}");
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
		/// A key is nominated only under the hard rule of <see cref="AdmitsPress"/>: a tap, an unarmed flick
		/// or an ordinary bar head, in the finger's lane, inside the press window. A bar's body, a hidden
		/// point or an interior combo point is never a press candidate - they are driven by their own
		/// per-frame passes - so nothing here can spend a finger's press on something the press cannot answer.
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

					// The hard rule: kind, lane and window. The note's own gate is asked as well, so a key
					// is never offered a press it would refuse.
					float lane = activeInput.Lane;
					if (!AdmitsPress(judgmentNote, activeInput.Touch.musicTime, lane)
						|| !judgmentNote.IsJudgment(ref activeInput.Touch, lane))
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
