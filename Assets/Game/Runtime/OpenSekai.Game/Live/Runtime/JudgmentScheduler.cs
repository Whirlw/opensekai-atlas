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
		private readonly System.Func<bool> _hasDanLife;
		private readonly LiveSessionState _session;
		private readonly LiveInputRouter _input;
		private readonly LiveNoteFrame _noteFrame;
		private readonly LivePresentationBridge _presentation;
		private readonly List<int> _usedFingerList = new List<int>(10);
		private readonly List<int> _frameUsedFingers = new List<int>(10);
		private readonly LiveNoteSession.NoteVisitor _judgeDamage;
		private readonly Dictionary<int, (LiveTouch Touch, float Lane)> _previousDamageInputs = new(10);
		private readonly Dictionary<int, (LiveTouch Touch, float Lane)> _damageContacts = new(10);
		private float _previousDamageFrameTime = float.NegativeInfinity;

		/// <summary>
		/// One 60 fps frame. Under frame matching (<see cref="LiveConfig.UseFramePressMatching"/>, or legacy
		/// judge timing) presses closer than this are one moment and compete for their notes as a chord;
		/// presses further apart, which only a late frame carries together, are judged in time order.
		/// In either mode two Began samples of one finger closer than this are one contact reported twice
		/// and share one press credit: no finger lifts and presses again inside a 60 fps frame.
		/// </summary>
		internal const float PRESS_PASS_SECONDS = 1f / 60f;
		private readonly HashSet<int> _canceledTouches = new HashSet<int>(10);

		internal JudgmentScheduler(LiveLogic logic, LiveSessionState session, LiveInputRouter input,
			LiveNoteFrame noteFrame, LivePresentationBridge presentation)
		{
			_logic = logic;
			_hasDanLife = () => _session.ScoreLogic?.Score.life > 0;
			_session = session;
			_input = input;
			_noteFrame = noteFrame;
			_presentation = presentation;
			_judgeDamage = JudgeDamageNote;
		}

		private ref MusicScoreInfo Frame => ref _logic.currentFrameInfo;

		internal void Run()
		{

			// The three channels answer in a fixed order, and each one is decided on the raw samples of
			// this frame before the next one looks at them: a press is a Began, a release is an Ended,
			// and whatever is left over is the resident upkeep.
			JudgePressChannel();
			_noteFrame.SettleExpiredPresses();
			JudgeReleaseChannel();
			JudgeResidentChannel();
			_session.Notes.SettleTailLinkedComboPoints(Frame);
			foreach (var note in _input.JudgmentNotes)
			{
				if (note is ConnectionNote { UsesNextRushContactWindow: true, ParentNote: null } tick)
					tick.SettleFromCompletedTail();
			}
			if (!_session.IsAuto && _logic.Result < 3)
				_logic.ConnectorAudio.Update(_session.MusicScore, Frame.time, false, _input.ActiveInputs, _session.NoteDisplayTimeOffset);

			// Returns the frame's inputs and forgets the touches that ended. It runs after every channel
			// has read them, including the release that a lift feeds.
			EndFrame();
		}

		/// <summary>
		/// The press channel, decided on the frame's raw samples: only a Began can be a press, and one
		/// finger spends one press per frame. The candidates are collected from the touch list, not
		/// from the notes, so a press that no note can answer leaves the finger free.
		/// </summary>
		private void JudgePressChannel()
		{
			// The frame's presses are judged in passes, in time order. By default every press is its own
			// pass - a tie goes in input order - so who gets a note never depends on how presses fell into
			// frames, and no chord matching runs: a later press collects again after the earlier one took
			// its note. Frame matching groups presses closer than a 60 fps frame into one pass, where they
			// compete as a chord; a late frame's presses further apart are still judged in order.
			bool frameMatching = LiveConfig.UseFramePressMatching || LiveConfig.UseLegacyJudgeTiming;
			int waves = AssignPressWaves(_input.ActiveInputs, frameMatching ? PRESS_PASS_SECONDS : 0f);
			_frameUsedFingers.Clear();
			for (int wave = 0; wave < waves; wave++)
			{
				if (wave > 0)
				{
					foreach (InputTmp input in _input.ActiveInputs)
						input.CandidateNotes.Clear();
				}

				_input.CollectCandidates(wave);
				TracePressCandidates("pressCandidates");
				_input.SortPressCandidates(deduplicate: frameMatching);
				TracePressCandidates("pressSelected");
				EvaluateJudgment();
			}

			JudgeNextRushFlickInputs();
			JudgeDamageConnectors();
			if (_previousDamageInputs.Count > 0)
			{
				foreach (InputTmp input in _input.ActiveInputs)
					if (input.Touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
						_previousDamageInputs.Remove(input.Touch.touchId);
			}
			_session.Notes.WalkDamageNotes(_judgeDamage);
			_previousDamageInputs.Clear();
			if (_session.Notes.HasPendingDamageNotes)
			{
				foreach (InputTmp input in _input.ActiveInputs)
					_previousDamageInputs[input.Touch.touchId] = (input.Touch, input.Lane);
				_previousDamageFrameTime = Frame.time;
			}
			ReportUnpickedLanes();
		}

		private void JudgeNextRushFlickInputs()
		{
			foreach (NoteBase note in _input.JudgmentNotes)
			{
				switch (note)
				{
					case FlickNote { IsNextRushHead: true } flick:
						foreach (InputTmp input in _input.ActiveInputs)
						{
							if (!flick.AcceptsNextRushHeadSample(ref input.Touch, input.Lane)) continue;
							_input.SilencedTouches.Add(input.Touch.touchId);
							if (flick.Judgment(ref input.Touch, input.Lane)) break;
						}
						flick.SettleNextRushHead(Frame.time);
						break;
					case FrictionFlickNote traceFlick:
						bool hasContact = false;
						foreach (InputTmp input in _input.ActiveInputs)
						{
							float sampleTime = input.IsResidentSample ? Frame.time : input.Touch.musicTime;
							if (!traceFlick.ReadNextRushHeadSample(ref input.Touch, input.Lane, sampleTime)) continue;
							_input.SilencedTouches.Add(input.Touch.touchId);
							hasContact = true;
						}
						traceFlick.SettleNextRushHead(Frame.time, hasContact);
						break;
				}
			}
		}

		private void JudgeDamageConnectors()
		{
			foreach (var bar in _input.DamageConnectors)
			{
				bool touched = false;
				if (bar.TryDamageBounds(Frame.time, out float left, out float right))
				{
					foreach (InputTmp input in _input.ActiveInputs)
					{
						var touch = input.Touch;
						float x = touch.worldPosition.x;
						if (float.IsNegativeInfinity(input.Lane) || LiveInputRouter.IsEndedOrCanceled(touch.phase) || x < left || x > right)
							continue;
						touched = true;
						if (bar.DamageContact || _input.SilencedTouches.Add(touch.touchId)) break;
					}
				}
				bar.SetDamageContact(touched);
			}
		}

		private LiveNoteSession.Scan JudgeDamageNote(NoteBase note)
		{
			var damage = (NextRushDamageNote)note;
			if (Frame.time < damage.InputStart) return LiveNoteSession.Scan.Stop;
			if (damage.IsVisible)
			{
				damage.Resolve(HasVisibleDamageContact(damage));
				return LiveNoteSession.Scan.Handle;
			}

			bool touched = false;
			foreach (InputTmp input in _input.ActiveInputs)
			{
				var touch = input.Touch;
				if (float.IsNegativeInfinity(input.Lane)) continue;
				float sampleTime = input.IsResidentSample ? Frame.time : touch.musicTime;
				if (sampleTime < damage.InputStart || sampleTime > damage.MusicScoreInfo.time) continue;
				if (!damage.Contains(touch.worldPosition.x, sampleTime)) continue;
				_input.SilencedTouches.Add(touch.touchId);
				touched = true;
			}

			if (touched || Frame.time >= damage.MusicScoreInfo.time) damage.Resolve(touched);
			return LiveNoteSession.Scan.Handle;
		}

		private bool HasVisibleDamageContact(NextRushDamageNote damage)
		{
			float target = damage.MusicScoreInfo.time;
			_damageContacts.Clear();
			// A late frame can carry a movement or release after the note. Its touch ID establishes
			// continuity with the preceding frame, whose position still applied at the note time.
			foreach (InputTmp input in _input.ActiveInputs)
			{
				int touchId = input.Touch.touchId;
				if (_previousDamageFrameTime < target && !_damageContacts.ContainsKey(touchId)
					&& _previousDamageInputs.TryGetValue(touchId, out var sample))
					RecordDamageContact(sample.Touch, sample.Lane, target);
				RecordDamageContact(input.Touch, input.Lane, target);
			}

			bool touched = false;
			foreach (var sample in _damageContacts.Values)
			{
				var touch = sample.Touch;
				if (float.IsNegativeInfinity(sample.Lane) || LiveInputRouter.IsEndedOrCanceled(touch.phase)
					|| !damage.Contains(touch.worldPosition.x, target)) continue;
				_input.SilencedTouches.Add(touch.touchId);
				touched = true;
			}
			return touched;
		}

		private void RecordDamageContact(LiveTouch touch, float lane, float target)
		{
			if (touch.musicTime <= target && (!_damageContacts.TryGetValue(touch.touchId, out var previous)
				|| touch.musicTime >= previous.Touch.musicTime))
				_damageContacts[touch.touchId] = (touch, lane);
		}

		internal void ResetDamageInputs()
		{
			_previousDamageInputs.Clear();
			_damageContacts.Clear();
			_previousDamageFrameTime = float.NegativeInfinity;
		}

		/// <summary>
		/// Numbers each Began by pass, walking the frame's presses in time order. A press starts the next
		/// pass when it is later than the previous press by at least <paramref name="passSeconds"/>; when
		/// that is zero every press starts one, ties included. A sample of a finger that pressed less than
		/// <see cref="PressPassSeconds"/> before joins that press's pass and shares its credit. Returns the
		/// number of passes, at least one. Allocation free; a frame has a handful of Began.
		/// </summary>
		internal static int AssignPressWaves(List<InputTmp> inputs, float passSeconds)
		{
			for (int i = 0; i < inputs.Count; i++)
				inputs[i].PressWave = inputs[i].Touch.phase == UnityEngine.InputSystem.TouchPhase.Began ? -1 : 0;

			int wave = 0;
			int previous = -1;
			while (true)
			{
				int next = -1;
				for (int j = 0; j < inputs.Count; j++)
				{
					if (inputs[j].PressWave != -1 || (next >= 0 && !PressPrecedes(inputs, j, next)))
						continue;
					next = j;
				}

				if (next < 0) break;
				float time = inputs[next].Touch.musicTime;
				int joined = -1;
				for (int j = 0; j < inputs.Count; j++)
				{
					InputTmp earlier = inputs[j];
					if (j == next || earlier.PressWave < 0 || earlier.Touch.phase != UnityEngine.InputSystem.TouchPhase.Began
						|| earlier.Touch.fingerId != inputs[next].Touch.fingerId
						|| time - earlier.Touch.musicTime >= PRESS_PASS_SECONDS)
					{
						continue;
					}

					joined = j;
				}

				if (joined >= 0)
				{
					inputs[next].PressWave = inputs[joined].PressWave;
				}
				else
				{
					float gap = previous >= 0 ? time - inputs[previous].Touch.musicTime : 0f;
					if (previous >= 0 && (passSeconds <= 0f || gap >= passSeconds))
						wave++;
					inputs[next].PressWave = wave;
				}

				previous = next;
			}

			return wave + 1;
		}

		private static bool PressPrecedes(List<InputTmp> inputs, int a, int b)
		{
			float ta = inputs[a].Touch.musicTime;
			float tb = inputs[b].Touch.musicTime;
			return ta < tb || (ta == tb && a < b);
		}

		private void TracePressCandidates(string stage)
		{
			if (LiveTrace.IsOn(LiveTraceCategory.Press))
			{
				foreach (InputTmp input in _input.ActiveInputs)
				{
					if (input != null && input.Touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
					{
						var candidates = new System.Text.StringBuilder();
						foreach (NoteBase note in input.CandidateNotes)
						{
							if (candidates.Length > 0) candidates.Append(';');
							if (note == null) { candidates.Append("null"); continue; }
							candidates.Append($"id={note.Id},type={note.GetType().Name},t={note.MusicScoreInfo.time:F3},lane={note.LaneStartF:F2}..{note.LaneEndF:F2},grade={note.CalcNoteResult(input.Touch.musicTime)}");
						}
						LiveTrace.Write(LiveTraceCategory.Press, $"{stage} finger={input.Touch.fingerId} touch={input.Touch.touchId} lane={input.Lane:F2} time={input.Touch.musicTime:F3} candidates=[{candidates}]");
					}
				}
			}
		}

		/// <summary>
		/// The release channel: the ended samples finish the one bar each finger is standing on. It
		/// runs after the press channel so a press that ended in the same sample cannot be swallowed
		/// by the lift, and before the resident pass so the bar it finishes is already done when that
		/// pass looks at the holds.
		/// </summary>
		private void JudgeReleaseChannel()
		{
			_input.JudgeLifts();
		}

		/// <summary>
		/// The resident channel: the state that has no event of its own - combo points, bar bodies,
		/// self-judged tails, hold upkeep and the timeouts.
		/// </summary>
		private void JudgeResidentChannel()
		{
			_canceledTouches.Clear();
			foreach (InputTmp input in _input.ActiveInputs)
			{
				if (input.Touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
					_canceledTouches.Add(input.Touch.touchId);
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Resident) && _input.ActiveInputs.Count > 0)
				LiveTrace.Write(LiveTraceCategory.Resident, $"resident now={Frame.time:F3} inputs={_input.ActiveInputs.Count} notes={_input.JudgmentNotes.Count}");
			DriveIndependentNotes();
			RefreshSustainedHolds();
		}

		/// <summary>
		/// Hands the frame's inputs back: an ended touch is forgotten, and the list is emptied so the
		/// next frame starts from its own samples only.
		/// </summary>
		private void EndFrame()
		{
			for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
			{
				InputTmp input = _input.ActiveInputs[inputIndex];
				if (input != null && LiveInputRouter.IsEndedOrCanceled(input.Touch.phase))
				{
					_input.ForgetJudgedTouch(input.Touch.touchId);
				}
			}

			_input.ActiveInputs.Clear();
		}

		internal void DriveIndependentNotes()
		{
			for (int n = _input.JudgmentNotes.Count - 1; n >= 0; n--)
			{
				NoteBase note = _input.JudgmentNotes[n];
				if (note == null || note.State == NoteState.Done || note is LongHoldCombo or NextRushDamageNote
					|| note is FlickNote { IsNextRushHead: true } or FrictionFlickNote
					|| note is NormalNote { IsIndependentRelease: true }
					|| LiveCandidatePipeline.IsPressStep(note)) continue;
				if (note is FlickNote flickTail && flickTail.JudgesAsBarTail)
				{
					DriveFlickTail(flickTail);
					if (flickTail.State == NoteState.Done) _input.JudgmentNotes.RemoveAt(n);
					continue;
				}
				int barInput = note is LongNote longNote ? PickBarInput(longNote) : -1;
				for (int i = 0; i < _input.ActiveInputs.Count; i++)
				{
					InputTmp input = _input.ActiveInputs[i];
					float lane = input.Lane;
					if (float.IsNegativeInfinity(lane)) continue;
					if (note is LongNote bar)
					{
						if (i != barInput) continue;
						// The body's frame only: the head was the press channel's to answer.
						bar.DriveResidentFrame(ref input.Touch, lane);
					}
					else
					{
						if (!note.IsJudgment(ref input.Touch, lane)) continue;
						note.Judgment(ref input.Touch, lane);
						SilenceIfContactNote(note, input);
					}
					// An armed flick takes a swipe from any finger, so every finger of the frame is offered
					// to it until one completes it; a resting finger listed first must not shut out the one
					// that swipes.
					if (note is FlickNote && note.ParentNote == null && note.State != NoteState.Done) continue;
					break;
				}
				if (LiveTrace.IsOn(LiveTraceCategory.Resident))
					LiveTrace.Write(LiveTraceCategory.Resident, $"unregDone id={note.Id} n={n}");
				if (note.State == NoteState.Done) _input.JudgmentNotes.RemoveAt(n);
			}
		}

		/// <summary>
		/// A bar's end flick reads every finger of the frame - a lift and a slide out of its lanes matter as
		/// much as a swipe, and the final sample of a lift may be off the playfield - and then closes the
		/// frame so a finger that sent nothing counts as gone (<see cref="FlickTailJudge"/>).
		/// </summary>
		private void DriveFlickTail(FlickNote tail)
		{
			FlickTailJudge judge = tail.TailJudge;
			// Remove canceled contacts before any other swipe can settle the armed candidates.
			foreach (int touchId in _canceledTouches)
				judge.Cancel(touchId);

			float deltaTime = judge.FrameDeltaTime(Frame.time);
			for (int i = 0; i < _input.ActiveInputs.Count && tail.State != NoteState.Done; i++)
			{
				InputTmp input = _input.ActiveInputs[i];
				if (input == null || _canceledTouches.Contains(input.Touch.touchId)) continue;
				judge.Offer(ref input.Touch, input.Lane, input.PreviousLane, deltaTime);
			}

			judge.EndInputFrame(Frame.time);
		}

		private int PickBarInput(LongNote bar)
		{
			int selected = -1;
			float nearest = float.MaxValue;
			for (int i = 0; i < _input.ActiveInputs.Count; i++)
			{
				InputTmp input = _input.ActiveInputs[i];
				if (LiveInputRouter.IsEndedOrCanceled(input.Touch.phase)) continue;
				float lane = input.Lane;
				if (float.IsNegativeInfinity(lane) || !bar.ContainsLane(lane)) continue;
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

		/// <summary>
		/// A finger used by a contact key may not light the empty lanes again
		/// until it leaves the screen.
		/// </summary>
		private void SilenceIfContactNote(NoteBase note, InputTmp input)
		{
			if (note is FrictionFlickNote or ConnectionNote { UsesContactWindow: true })
			{
				_input.SilencedTouches.Add(input.Touch.touchId);
			}
		}

		internal void RefreshSustainedHolds()
		{
			_input.Holds.Refresh(_input.JudgmentNotes, _input.ActiveInputs, Frame, UnityEngine.Time.deltaTime, _input.SilencedTouches);
		}

		internal void EvaluateJudgment()
		{
			// The credit belongs to one press pass; the lanes are lit once for the whole frame.
			_usedFingerList.Clear();
			for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
			{
				InputTmp input = _input.ActiveInputs[inputIndex];

				// One finger has one press credit per frame. A platform that delivers several Began samples
				// for the same finger in one frame (a tap, a lift and a tap again inside the frame) has them
				// share it: once one of them was consumed, the others are not pressed.
				if (input.CandidateNotes.Count > 0 && _usedFingerList.Contains(input.Touch.fingerId))
				{
					input.CandidateNotes.Clear();
					continue;
				}

				JudgmentPass pass = new JudgmentPass();
				pass.Scheduler = this;
				pass.Input = input;
				JudgmentSchedule.Run(input.CandidateNotes.Count, ref pass);
			}
		}

		private void ReportUnpickedLanes()
		{
			for (int inputIndex = 0; inputIndex < _input.ActiveInputs.Count; inputIndex++)
			{
				InputTmp input = _input.ActiveInputs[inputIndex];
				bool used = false;
				for (int i = _frameUsedFingers.Count - 1; i >= 0; i--)
				{
					used |= _frameUsedFingers[i] == input.Touch.fingerId;
				}

				if (!used && input.Lane >= 0f && input.Lane <= LiveConfig.JudgmentPositions.Length - 1 && !_input.SilencedTouches.Contains(input.Touch.touchId))
				{
					_presentation.Unpicked(_session.Views, Mathf.RoundToInt(input.Lane), ref input.Touch);
				}
			}
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
				float lane = Input.Lane;
				if (note == null)
				{
					return default(JudgmentSchedule.Outcome);
				}
				if (note is FrictionFlickNote { IsTraceFlickOwner: true })
					return new JudgmentSchedule.Outcome(note.Judgment(ref Input.Touch, lane));

				// One line per press delivery, so "a press happened but the note it should answer missed" can
				// be answered from the log instead of guessed: it says which note the press was handed to, the
				// lane it was converted to, and what the note became.
				if (LiveTrace.IsOn(LiveTraceCategory.Press))
				{
					int idBefore = note.Id;
					string typeBefore = note.GetType().Name;
					note.Judgment(ref Input.Touch, lane);
					LiveTrace.Write(LiveTraceCategory.Press, $"pressPick finger={Input.Touch.fingerId} touch={Input.Touch.touchId} id={idBefore} type={typeBefore} lane={lane:F2} touchT={Input.Touch.musicTime:F3} res={note.Result} now={MusicScore.CurrentFrameInfo.time:F3}");
				}
				else
				{
					note.Judgment(ref Input.Touch, lane);
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
					if (LiveTrace.IsOn(LiveTraceCategory.Press))
						LiveTrace.Write(LiveTraceCategory.Press, $"unregPick id={note.Id} state={note.State} res={note.Result}");
					Scheduler._input.JudgmentNotes.Remove(note);
				}

				// The note knows whether the press went into it; the schedule only needs the answer. A flick
				// is judged by the press when the press arms it: it moves straight to reading this finger's
				// swipe, and the press is spent on it like on any other key.
				bool noteFinished = note.State == NoteState.Done || note.Result != NoteResult.None
					|| (note is FlickNote flick && flick.IsArmedBy(Input.Touch.touchId));
				if (noteFinished && LatencyCapture.Active)
				{
					LatencyCapture.Record("press_spent", Input.Touch.touchId, Input.Touch.fingerId, Input.Touch.phase,
						note.Id, song: Input.Touch.musicTime, note: note.MusicScoreInfo.time,
						value: (Input.Touch.musicTime - note.MusicScoreInfo.time) * 1000d, result: note.Result);
				}
				return new JudgmentSchedule.Outcome(noteFinished);
			}

			/// <summary>
			/// The press went into a note: this is the one place a press is spent. The touch is recorded
			/// for the collection of later frames, and the finger's credit for this frame is gone.
			/// </summary>
			public void Consumed(int candidateIndex)
			{
				if (Input.CandidateNotes[candidateIndex] is NormalNote or FlickNote)
				{
					Scheduler._input.Holds.RecordKeyPress(Input.Touch);
				}

				Input.CandidateNotes.Clear();
				Scheduler._input.RecordJudgedTouch(Input.Touch);
				Scheduler._usedFingerList.Add(Input.Touch.fingerId);
				Scheduler._frameUsedFingers.Add(Input.Touch.fingerId);
				// A finger whose press went into a key may not light the empty lanes again until it lifts.
				Scheduler._input.SilencedTouches.Add(Input.Touch.touchId);
			}
		}

		private void OnJudgmentNote(NoteBase note)
		{
			LatencyCapture.Record("judged", noteId: note.Id, song: Frame.time,
				note: note.MusicScoreInfo.time, detail: note.IsSkip ? "skip" : null, result: note.Result);
			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge, string.Concat(
					"judge id=", note.Id, " type=", note.GetType().Name, " cat=", note.Category,
					" lane=", note.LaneStartF.ToString("F2"), "..", note.LaneEndF.ToString("F2"),
					" t=", note.MusicScoreInfo.time.ToString("F3"), " res=", note.Result,
					" skip=", note.IsSkip, " now=", Frame.time.ToString("F3")));
			}

			_session.ScoreLogic?.UpdateNoteResult(note);
			// A missed deadline is silent. Touching a hazard is an actual damage hit.
			bool playSound = (note.Result != NoteResult.Miss || note.HasNextRushHit || note is NextRushDamageNote)
				&& (note is not (LongHoldCombo or NextRushHiddenTickNote) || _session.IsAuto);
			var cue = playSound ? LiveSoundDefine.NextRushCue(note.NativeEffectKind,
				_session.IsAuto ? NoteResult.Auto : note.Result) : null;
			if (cue != null && _session.Views != null)
				foreach (var view in _session.Views) view?.NativeHitSound(note, cue);

			// Avoidance and autoplay count towards the result without playing a Friction hit effect.
			if (note is NextRushHiddenTickNote || note is NextRushDamageNote && note.Result != NoteResult.Miss) return;

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
			if (_session.ScoreLogic?.UseSubtractiveScore != true)
				_presentation.UpdateScore(_session.Views, _session.Score, addScore);
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

			var events = _session.MusicScore?.musicScoreInfoArray;
			var reversalEvents = events != null && System.Array.Exists(events, info => info.speedRatio < 0f)
				? events : null;
			HashSet<NoteBase> visited = new HashSet<NoteBase>();

			foreach (NoteBase note in notes)
			{
				BindNoteCallbacks(note, visited, reversalEvents);
			}
		}

		private void BindNoteCallbacks(NoteBase note, HashSet<NoteBase> visited, MusicScoreInfo[] reversalEvents)
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
			note.CanPublishResult = _session.BootData is FreeLiveBootData { DanSong: { FailOnZero: true } } ? _hasDanLife : null;
			note.OnUpdateResult = OnUpdateResult;
			note.CalcTimeOffset = _noteFrame.CalcTimeOffset;
			if (note.NativeScroll == null)
				note.ScrollApproachStart = NoteSpeedRatio.FindApproachStart(reversalEvents,
					note.MusicScoreInfo, _session.NoteDisplayTimeOffset, note.speedRatio);

			if (note.NoteList == null)
			{
				return;
			}

			foreach (NoteBase childNote in note.NoteList)
			{
				BindNoteCallbacks(childNote, visited, reversalEvents);
			}
		}
	}
}
