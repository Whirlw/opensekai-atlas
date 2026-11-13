using System.Collections.Generic;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Live;
using UnityEngine;
using InputTmp = Sekai.Core.Live.LiveLogic.InputTmp;
namespace Sekai.Core.Live
{
	internal sealed class JudgmentScheduler
	{
		private readonly LiveLogic _logic;
		private readonly LiveSessionState _session;
		private readonly LiveInputRouter _input;
		private readonly LiveNoteFrame _noteFrame;
		private readonly LivePresentationBridge _presentation;
		private readonly List<int> _usedFingerList = new List<int>(10);

		internal JudgmentScheduler(LiveLogic logic, LiveSessionState session, LiveInputRouter input,
			LiveNoteFrame noteFrame, LivePresentationBridge presentation)
		{
			_logic = logic;
			_session = session;
			_input = input;
			_noteFrame = noteFrame;
			_presentation = presentation;
		}

		private ref MusicScoreInfo Frame => ref _logic.currentFrameInfo;

		internal void Run()
		{
			JudgeDueComboPoints();
			_input.CollectCandidates();
			DriveIndependentNotes();
			_input.SortPressCandidates();
			RefreshSustainedHolds();
			EvaluateJudgment();
			_noteFrame.TickViews();
		}

		// An interior point is not a press-slot note: it belongs to no finger. Every frame it is due,
		// any finger sitting in its lane settles it, and two overlapping points settle independently.
		private void JudgeDueComboPoints()
		{
			for (int noteIndex = 0; noteIndex < _input.JudgmentNotes.Count; noteIndex++)
			{
				NoteBase note = _input.JudgmentNotes[noteIndex];
				if (note == null || note.State != NoteState.Last || !(note is Sekai.Live.LongHoldCombo))
				{
					continue;
				}

				for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
				{
					LiveLogic.InputTmp input = _input.ActiveInputs[inputIndex];
					// The bar counts as in play while a finger holds it (InputBegan/Input) or during the
					// release grace (Last). Once it was in play anywhere inside this point's window, the
					// point stays judicable for the rest of that window.
					Sekai.Live.LongHoldCombo combo = (Sekai.Live.LongHoldCombo)note;
					LongNote bar = combo.ParentNote as LongNote;
					if (bar != null && (bar.HasFinger || bar.State == NoteState.Last || bar.State == NoteState.Release))
					{
						combo.MarkBarInPlay();
					}

					if (!combo.WasBarInPlay)
					{
						continue;
					}

					if (!note.IsJudgment(ref input.Touch, input.Lane))
					{
						continue;
					}

					note.Judgment(ref input.Touch, input.Lane);
					break;
				}
			}
		}

		internal void DriveIndependentNotes()
		{
			for (int n = _input.JudgmentNotes.Count - 1; n >= 0; n--)
			{
				NoteBase note = _input.JudgmentNotes[n];
				if (note == null || note.State == NoteState.Done || note is LongHoldCombo
					|| LiveCandidatePipeline.IsPressStep(note)) continue;
				int barInput = note is LongNote longNote ? PickBarInput(longNote) : -1;
				for (int i = 0; i < _input.ActiveInputs.Count; i++)
				{
					InputTmp input = _input.ActiveInputs[i];
					float lane = _input.ConvertLaneBy(ref note, ref input);
					if (lane < 0f) continue;
					if (note is LongNote)
					{
						if (i != barInput) continue;
						note.Judgment(ref input.Touch, lane);
					}
					else
					{
						if (!note.IsJudgment(ref input.Touch, lane)) continue;
						note.Judgment(ref input.Touch, lane);
					}
					break;
				}
					OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, $"unregDone id={note.Id} n={n}");
				if (note.State == NoteState.Done) _input.JudgmentNotes.RemoveAt(n);
			}
		}

		private int PickBarInput(LongNote bar)
		{
			int selected = -1;
			float nearest = float.MaxValue;
			for (int i = 0; i < _input.ActiveInputs.Count; i++)
			{
				InputTmp input = _input.ActiveInputs[i];
				if (LiveInputRouter.IsEndedOrCanceled(input.Touch.phase)) continue;
				NoteBase note = bar;
				float lane = _input.ConvertLaneBy(ref note, ref input);
				if (lane < 0f || !bar.ContainsLane(lane)) continue;
				if (!bar.IsOrdinaryHead && !bar.IsJudgment(ref input.Touch, lane)) continue;
				float distance = bar.LaneDistance(ref Frame, lane);
				if (selected < 0 || distance < nearest || (distance == nearest
					&& input.Touch.fingerId < _input.ActiveInputs[selected].Touch.fingerId))
				{
					selected = i;
					nearest = distance;
				}
			}
			return selected;
		}

		internal void RefreshSustainedHolds()
		{
			_input.Holds.Refresh(_input.JudgmentNotes, _input.ActiveInputs, Frame, UnityEngine.Time.deltaTime, _input.ConvertLaneBy);
		}

		internal void EvaluateJudgment()
		{
			_usedFingerList.Clear();
			for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
			{
				InputTmp input = _input.ActiveInputs[inputIndex];
				JudgmentPass pass = new JudgmentPass();
				pass.Scheduler = this;
				pass.Input = input;
				JudgmentSchedule.Run(input.CandidateNotes.Count, ref pass);
			}

			for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
			{
				InputTmp input = _input.ActiveInputs[inputIndex];
				bool used = false;
				for (int i = _usedFingerList.Count - 1; i >= 0; i--)
				{
					used |= _usedFingerList[i] == input.Touch.fingerId;
				}

				if (!used && input.Lane >= 0f)
				{
					_presentation.Unpicked(_session.Views, Mathf.RoundToInt(input.Lane), ref input.Touch);
				}

				if (LiveInputRouter.IsEndedOrCanceled(input.Touch.phase))
				{
					_input.ForgetJudgedTouch(input.Touch.touchId);
				}
			}

			_input.ActiveInputs.Clear();
		}

		/// <summary>
		/// Drives <see cref="JudgmentSchedule"/> for one finger: it judges the candidates in order and
		/// reports what each one became. It is a struct so the per-frame call does not allocate.
		/// </summary>
		private struct JudgmentPass : IJudgmentPass
		{
			public JudgmentScheduler Scheduler;

			public InputTmp Input;

			public JudgmentSchedule.Outcome Judge(int candidateIndex)
			{
				NoteBase note = Input.CandidateNotes[candidateIndex];
				float lane = Scheduler._input.ConvertLaneBy(ref note, ref Input);
				if (note == null)
				{
					return default(JudgmentSchedule.Outcome);
				}

				// One line per press delivery, so "a press happened but the note it should answer missed" can
				// be answered from the log instead of guessed: it says which note the press was handed to, the
				// lane it was converted to, and what the note became.
				if (LiveTrace.IsOn(LiveTraceCategory.Judge))
				{
					int idBefore = note.Id;
					string typeBefore = note.GetType().Name;
					note.Judgment(ref Input.Touch, lane);
					LiveTrace.Write(LiveTraceCategory.Judge, $"pressPick finger={Input.Touch.fingerId} touch={Input.Touch.touchId} id={idBefore} type={typeBefore} lane={lane:F2} touchT={Input.Touch.musicTime:F3} res={note.Result} now={MusicScore.CurrentFrameInfo.time:F3}");
				}
				else
				{
					note.Judgment(ref Input.Touch, lane);
				}

				if (note.Result == NoteResult.None || note.Result == NoteResult.Bad)
				{
					Scheduler._input.RecordJudgedTouch(Input.Touch);
				}

				// Notes that are still in flight have to keep receiving frames. Dropping a long note
				// stops LongNote.Judgment and its hold releases; dropping a rub note whose first
				// touch only set isTouched means it can never reach the line, so it fails silently.
				// A long note is judged once when its head lands and then again every frame until it
				// is Done, so a result alone must not evict it: the bar would go stale and release on
				// the very next frame, which looks like the bar vanishing right after the press.
				bool keepsReceivingFrames = note is LongNote && note.State != NoteState.Done;
				if (!keepsReceivingFrames && (note.State == NoteState.Done || note.Result != NoteResult.None))
				{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, $"unregPick id={note.Id} state={note.State} res={note.Result}");
					Scheduler._input.JudgmentNotes.Remove(note);
				}

				// The note knows whether the press went into it; the schedule only needs the answer.
				bool noteFinished = note.State == NoteState.Done || note.Result != NoteResult.None;
				return new JudgmentSchedule.Outcome(noteFinished);
			}

			public void Consumed(int candidateIndex)
			{
				Input.CandidateNotes.Clear();
				Scheduler._usedFingerList.Add(Input.Touch.fingerId);
			}
		}

		private void OnJudgmentNote(NoteBase note)
		{
			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, string.Concat(
					"judge id=", note.Id, " type=", note.GetType().Name, " cat=", note.Category,
					" lane=", note.LaneStartF.ToString("F2"), "..", note.LaneEndF.ToString("F2"),
					" t=", note.MusicScoreInfo.time.ToString("F3"), " res=", note.Result,
					" skip=", note.IsSkip, " now=", Frame.time.ToString("F3")));
			}

			_session.ScoreLogic?.UpdateNoteResult(note);

			// A marker that is not drawn is silent by construction: the half beat points inside a bar
			// must never talk for the player. Everything else still shows its judgement, a Miss
			// included: the tap effect decides for itself whether the result deserves a sound.
			if (note.IsSkip)
			{
				// Autoplay is the one place where a marker can be all there is to grade for a whole
				// stretch (the friction section of DEINOS), so its exclusion also switched the AUTO
				// popup off there. Only a marker on a carrier refreshes the text: a marker inside a bar
				// that speaks for itself already got its popup from that bar, and doubling it there
				// would make the popup faster than the notes.
				if (_session.IsAuto && note.ParentNote is NoteBase carrier && !carrier.HasJudgment)
				{
					_presentation.JudgmentText(_session.Views, note);
				}

				return;
			}

			_presentation.Judged(_session.Views, note);
		}

		private void OnUpdateCombo(NoteBase note)
		{
			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, string.Concat(
					"combo id=", note.Id, " res=", note.Result, " t=", note.MusicScoreInfo.time.ToString("F3"),
					" now=", Frame.time.ToString("F3")));
			}

			_session.ScoreLogic?.UpdateCombo(note);
			_presentation.UpdateCombo(_session.Views, _session.ScoreLogic?.Score ?? default);
		}

		private void OnUpdateScore(NoteBase note)
		{
			int addScore = _session.ScoreLogic?.CalculateAddScore(note, _session.SkillLogic?.ScoreFactor(note) ?? 1f) ?? 0;
			_presentation.UpdateScore(_session.Views, _session.ScoreLogic?.Score ?? default, addScore);
		}

		private void OnDamage(NoteBase note)
		{
			_session.ScoreLogic?.Damage(note);
			_presentation.UpdateLife(_session.Views, _session.ScoreLogic?.Score ?? default);
		}

		private (NoteResult result, NoteResultDescription description) OnUpdateResult((NoteResult result, NoteResultDescription description) result)
		{
			return _session.SkillLogic?.UpdateResult(result) ?? result;
		}

		internal void BindNoteCallbacks(NoteBase[] notes)
		{
			if (notes == null)
			{
				return;
			}

			HashSet<NoteBase> visited = new HashSet<NoteBase>();

			foreach (NoteBase note in notes)
			{
				BindNoteCallbacks(note, visited);
			}
		}

		private void BindNoteCallbacks(NoteBase note, HashSet<NoteBase> visited)
		{
			if (note == null || !visited.Add(note))
			{
				return;
			}

			note.OnSpawn = _noteFrame.Spawn;
			note.OnUnspawn = _noteFrame.Unspawn;
			note.OnJudgment = OnJudgmentNote;
			note.OnUpdateCombo = OnUpdateCombo;
			note.OnUpdateScore = OnUpdateScore;
			note.OnDamage = OnDamage;
			note.OnUpdateResult = OnUpdateResult;
			note.CalcTimeOffset = _noteFrame.CalcTimeOffset;

			if (note.NoteList == null)
			{
				return;
			}

			foreach (NoteBase childNote in note.NoteList)
			{
				BindNoteCallbacks(childNote, visited);
			}
		}
	}
}
