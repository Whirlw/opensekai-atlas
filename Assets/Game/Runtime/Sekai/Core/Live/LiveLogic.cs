using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using Sekai.Live.Tails;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using Sekai;
using Sekai.Core;
using OpenSekai.Shared.Live;

namespace Sekai.Core.Live
{
	public class LiveLogic
	{
		private const int INPUT_TMP_COUNT = 128;
		private const int MAX_FINGER_COUNT = 10;
		private const float TARGET_FRAME_TIME = 1f / 60f;

		[Serializable]
		public class InputTmp
		{
			public LiveTouch Touch;
						private float _Lane;
			public float Lane
			{
			    get => _Lane;
			    set => _Lane = value;
			}
						private List<NoteBase> _CandidateNotes;
			public List<NoteBase> CandidateNotes
			{
			    get => _CandidateNotes;
			    set => _CandidateNotes = value;
			}

			public InputTmp()
			{
				CandidateNotes = new List<NoteBase>(4);
			}

			public InputTmp(InputTmp source)
				: this()
			{
				if (source == null)
				{
					return;
				}

				Touch = source.Touch;
				Lane = source.Lane;
				CandidateNotes.AddRange(source.CandidateNotes);
			}

			public void Reset(ref LiveTouch touch, float lane)
			{
				Touch = touch;
				Lane = lane;
				CandidateNotes.Clear();
			}
		}

		[Serializable]
		public class InputLogInfo
		{
						private List<LiveTouch> _buffer;
			public List<LiveTouch> buffer
			{
			    get => _buffer;
			    set => _buffer = value;
			}

			public InputLogInfo(LiveTouch[] buffer)
			{
				this.buffer = buffer != null ? new List<LiveTouch>(buffer) : new List<LiveTouch>();
			}
		}

		private readonly LiveBundleBuildData _liveBundleBuildData;
		private LiveBootDataBase _bootData;
		private LiveViewBase[] _liveViews;
		private MusicScore _musicScore;
		private readonly LiveNoteSession _notes = new LiveNoteSession();
		private EventBase[] _eventArray = Array.Empty<EventBase>();
		private readonly LiveTimeline _timeline = new LiveTimeline();
		private readonly List<NoteBase> _judgmentNoteList;
		private readonly InputTmp[] _inputTmpArray;
		private readonly List<InputTmp> _activeInputList;
		private Camera _calcCamera;
		private readonly InputSystemTouchSource _deviceTouchSource = new InputSystemTouchSource();
		private ILiveTouchSource _touchSource;

		internal void SetTouchSource(ILiveTouchSource source)
		{
			_touchSource = source ?? _deviceTouchSource;
		}
		private InputLogInfo _recordedInputLogger;
		private readonly List<NoteBase> _connectionNoteList;
		private readonly LiveInputCollector _inputCollector;
		private readonly List<int> _usedFingerList;
		private readonly LiveHoldCoordinator _holds = new LiveHoldCoordinator(INPUT_TMP_COUNT);

		/// <summary>Orders and prunes each finger's candidates before judging.</summary>
		private readonly LiveCandidatePipeline _candidates = new LiveCandidatePipeline();
		private readonly Dictionary<int, double> InputedNormalNoteTouch;
		private float _noteDisplayTimeOffset;
		private float _seBaseVolume = 1f;
		private float _seScoreVolume = 1f;

		public event Action OnFinished;
		public event Action OnFailure;

				private ScoreLogic _scoreLogic;
		public ScoreLogic scoreLogic
		{
		    get => _scoreLogic;
		    set => _scoreLogic = value;
		}
				private SkillLogic _skillLogic = new NullSkillLogic();
		public SkillLogic skillLogic
		{
		    get => _skillLogic;
		    set => _skillLogic = value;
		}
		public int noteStartIndex
		{
		    get => _notes.NoteIndex;
		    set => _notes.NoteIndex = value;
		}
		public int highSpeedNoteStartIndex
		{
		    get => _notes.HighSpeedIndex;
		    set => _notes.HighSpeedIndex = value;
		}
		/// <summary>The notes this session walks, and how far it has walked.</summary>
		public LiveNoteSession Notes => _notes;

		public int result;
		public int TapCount { get; set; }
				private double _currentGameTime;
		public double currentGameTime
		{
		    get => _currentGameTime;
		    set => _currentGameTime = value;
		}
		public MusicScoreInfo currentFrameInfo;

		public LiveScore Score => scoreLogic?.Score ?? default;

		public bool IsNotesAllFinished => noteStartIndex >= _notes.Notes.Length && highSpeedNoteStartIndex >= _notes.HighSpeedNotes.Length;

		public bool IsPerfectCombo => scoreLogic?.IsPerfectCombo ?? false;

		public bool IsAllPerfectCombo => scoreLogic?.IsAllPerfectCombo ?? false;

		public LiveLogic(LiveBundleBuildData data)
		{
			_touchSource = _deviceTouchSource;
			NativeInput.Enable();
			int fingerCapacity = Mathf.Max(NativeInput.Fingers.Count, MAX_FINGER_COUNT);
			_judgmentNoteList = new List<NoteBase>();
			_inputTmpArray = new InputTmp[INPUT_TMP_COUNT];
			for (int i = 0; i < _inputTmpArray.Length; i++)
			{
				_inputTmpArray[i] = new InputTmp();
			}
			_activeInputList = new List<InputTmp>(INPUT_TMP_COUNT);
			_connectionNoteList = new List<NoteBase>(5);
			_inputCollector = new LiveInputCollector(MAX_FINGER_COUNT);
			_usedFingerList = new List<int>(MAX_FINGER_COUNT);
			InputedNormalNoteTouch = new Dictionary<int, double>(MAX_FINGER_COUNT);
			_seScoreVolume = 1f;
			_liveBundleBuildData = data;
		}

		public void Setup(LiveBootDataBase _bootData, LiveViewBase[] _liveViews)
		{
			this._bootData = _bootData;
			this._liveViews = _liveViews;
			_seBaseVolume = ApplicationLocalSettings.LoadFromStorage().LiveVolume?.Se ?? 1f;
			_noteDisplayTimeOffset = LiveConfig.GetNoteDisplayOffsetTime(_bootData?.LiveSettingData?.NoteSpeed ?? 6f);

			_musicScore = _bootData?.MusicData?.MusicScore ?? new MusicScore();
			if (_bootData?.MusicData != null)
			{
				_bootData.MusicData.MusicScore = _musicScore;
			}

			_musicScore.InjectSkillFeverForCreatorScore(_bootData?.MusicData?.Vocal?.musicId ?? _bootData?.MusicData?.Music?.id ?? 0);
			_notes.Bind(SortNotes(_musicScore.NoteArray));

			_eventArray = SortEvents(_musicScore.EventArray);
			BindNoteCallbacks(_notes.Notes);
			BindEventCallbacks(_eventArray);
			LiveViewExt.CreateNotePool(_liveViews, LiveUtility.CalculateMaxSimultaneousNotes(_musicScore, _noteDisplayTimeOffset));
			_calcCamera = CameraUtility.GetFrontCamera();
			if (_calcCamera == null)
			{
				_calcCamera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
			}
			_deviceTouchSource.Camera = _calcCamera;
			ResetScoreState();
			RefreshInput();
		}

		public void SetSkillLogic(SkillLogic logic)
		{
			skillLogic = logic ?? new NullSkillLogic();
			skillLogic.Setup(_bootData, scoreLogic?.Score ?? default);
		}

		public void SetScoreLogic(ScoreLogic logic)
		{
			scoreLogic = logic;
			scoreLogic?.Setup(_bootData, _musicScore);
			LiveViewExt.SetupScore(_liveViews, scoreLogic?.Score ?? default);
		}

		public void RefreshInput()
		{
			NativeInput.ResetPointerButtons();
			foreach (var device in InputSystem.devices)
			{
				InputSystem.ResetDevice(device);
			}

			NativeInput.Enable();
			_inputCollector.Reset();
			_activeInputList.Clear();
			InputedNormalNoteTouch.Clear();
		}

		private void ResetScoreState()
		{
			noteStartIndex = 0;
			highSpeedNoteStartIndex = 0;
			_timeline.Reset();
			result = 0;
			TapCount = 0;
			_presentation.Reset();
			_musicScore?.ResetNote();
			_musicScore?.ResetEvent();
		}

		public void OnUpdate(float scoreInfoTime, double currentGameTime)
		{
			if (result > 2)
			{
				_timeline.Update(_musicScore, scoreInfoTime, currentGameTime);
				currentFrameInfo = _timeline.Frame;
				TickTailKeyViews();
				TickRetiringViews(currentFrameInfo);
				return;
			}

			_timeline.Update(_musicScore, scoreInfoTime, currentGameTime);
			currentFrameInfo = _timeline.Frame;
			this.currentGameTime = currentGameTime;

			UpdateSeVolume();
			InitializeUpdate();
			UpdateEvent();
			UpdateSkill();
			UpdateNote();
			TickTailKeyViews();
			TickRetiringViews(currentFrameInfo);

			if (scoreLogic.HasValue() && scoreLogic.Score.life <= 0)
			{
				Result = 2;
			}

			if (IsNotesAllFinished && result == 0)
			{
				result = 3;
				OnFinished?.Invoke();
			}
		}

		public int Result
		{
			get => result;
			set
			{
				if (result == value)
				{
					return;
				}

				result = value;
				if (value == 2)
				{
					OnFailure?.Invoke();
				}
				if (value >= 3)
				{
					OnFinished?.Invoke();
				}
			}
		}

		public void OnAutoInput()
		{
			_timeline.AdvanceAutoJudgment(_notes);
		}

		public void OnInput()
		{
			int activeInputCount = 0;
			_activeInputList.Clear();
			_inputCollector.Collect(_touchSource, _calcCamera, ResolveInputMusicTime, OnProcessTouch, ref activeInputCount);
			Judgment();
		}

		// Wall clock and music clock both advance in seconds at 1x, so a touch sampled N seconds
		// before this frame must map to "current music time - N". The mapping itself, including how
		// far back a stale touch may reach, now lives in InputTimeMapping.
		public float ResolveInputMusicTime(double inputTime)
		{
			if (LiveConfig.UseLegacyJudgeTiming)
			{
				return ComputeLegacyInputMusicTime(currentFrameInfo.time, currentGameTime, inputTime);
			}

			return ComputeInputMusicTime(currentFrameInfo.time, currentGameTime, inputTime);
		}

		/// <summary>
		/// The original formula. See <see cref="OpenSekai.Shared.Live.Simulation.InputTimeMapping.FromLegacyClock"/>.
		/// </summary>
		public static float ComputeLegacyInputMusicTime(double frameMusicTime, double frameGameTime, double inputTime)
		{
			return OpenSekai.Shared.Live.Simulation.InputTimeMapping.FromLegacyClock(frameMusicTime, frameGameTime, inputTime);
		}

		/// <summary>
		/// Maps an Input System timestamp (seconds, same clock as <see cref="Time.realtimeSinceStartupAsDouble"/>)
		/// onto the music timeline. See <see cref="OpenSekai.Shared.Live.Simulation.InputTimeMapping.FromClock"/>.
		/// </summary>
		public static float ComputeInputMusicTime(double frameMusicTime, double frameGameTime, double inputTime)
		{
			return OpenSekai.Shared.Live.Simulation.InputTimeMapping.FromClock(frameMusicTime, frameGameTime, inputTime);
		}

		public void Continue()
		{
			Result = 0;
		}

		private void InitializeUpdate()
		{
			_judgmentNoteList.Clear();
		}

		private void UpdateEvent()
		{
			_timeline.AdvanceEvents(_eventArray, ExecuteEvent);
		}

		private void UpdateSkill()
		{
			skillLogic?.Update(currentGameTime);
		}

		private void UpdateNote()
		{
			_notes.WalkNotes(ScanNote);
			_notes.WalkHighSpeedNotes(ScanHighSpeedNote);
		}

		/// <summary>
		/// One normal note on its display frame. Returns <see cref="LiveNoteSession.Scan.Stop"/> once the
		/// array has reached notes that are still too far ahead, which ends the walk for this frame.
		/// </summary>
		private LiveNoteSession.Scan ScanNote(NoteBase note)
		{
			if (!Mathf.Approximately(note.speedRatio, 1f))
			{
				return LiveNoteSession.Scan.Skip;
			}

			float offsetTime = CalcTimeOffset(note);
			if (IsTooFarAheadForDisplayHighSpeed(offsetTime, note))
			{
				return LiveNoteSession.Scan.Stop;
			}
			if (offsetTime < 0f && currentFrameInfo.time >= note.MusicScoreInfo.time)
			{
				offsetTime = -offsetTime;
			}

			note.Excute(currentFrameInfo, offsetTime);

			// Decoration notes need to be processed after Excute to check OffsetJudgeTime
			if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
			{
				DecorationJudgment(note);
			}

			if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
			{
				UpdateJudgmentNoteArray(note);
			}

			return LiveNoteSession.Scan.Handle;
		}

		/// <summary>One high speed note: same shape, its own display lead.</summary>
		private LiveNoteSession.Scan ScanHighSpeedNote(NoteBase note)
		{
			float offsetTime = currentFrameInfo.time >= note.MusicScoreInfo.time
				? _noteDisplayTimeOffset
				: CalcTimeOffset(note);
			if (IsTooFarAheadForDisplay(offsetTime, note))
			{
				return LiveNoteSession.Scan.Stop;
			}

			note.Excute(currentFrameInfo, offsetTime);

			if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
			{
				DecorationJudgment(note);
			}

			if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
			{
				UpdateJudgmentNoteArray(note);
			}

			return LiveNoteSession.Scan.Handle;
		}

		private bool ShouldTriggerDecorationJudgment(NoteBase note)
		{
			if (note == null)
			{
				return false;
			}

			// For long notes, trigger at both start and end times
			if (note is LongNote longNote)
			{
				// Trigger at start time (head)
				if (currentFrameInfo.time >= longNote.MusicScoreInfo.time && longNote.State == NoteState.Playing)
				{
					return true;
				}

				// Trigger at end time (tail)
				if (longNote.NoteList != null && longNote.NoteList.Count > 0)
				{
					NoteBase lastChild = longNote.NoteList[longNote.NoteList.Count - 1];
					if (lastChild != null && currentFrameInfo.time >= lastChild.MusicScoreInfo.time)
					{
						return true;
					}
				}

				return false;
			}

			// For normal notes, trigger at the judgment time
			return currentFrameInfo.time >= note.MusicScoreInfo.time;
		}

		private void DecorationJudgment(NoteBase note)
		{
			if (note == null || note.State == NoteState.Done)
			{
				return;
			}

			// Decoration notes are auto-judged when reaching judgment time
			// Play tap effect and SE, but do not trigger score/combo callbacks or judgment text

			// For long notes, handle specially: play head effect at start, keep body showing, play end effect at end
			if (note is LongNote longNote)
			{
				DecorationLongNoteJudgment(longNote);
				return;
			}

			// Set JudgeInfo to Perfect for effect playback (TapEffectView checks Result)
			note.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
			// For normal notes: play effect and remove
			LiveViewExt.JudgmentNote(_liveViews, note);
			note.SetStateUnnotice(NoteState.Done);
			LiveViewExt.UnspawnNote(_liveViews, note);
		}

		private void DecorationLongNoteJudgment(LongNote longNote)
		{
			if (longNote == null)
			{
				return;
			}

			// Check if we are at the start or end of the long note
			NoteBase lastChild = longNote.NoteList != null && longNote.NoteList.Count > 0
				? longNote.NoteList[longNote.NoteList.Count - 1]
				: null;

			bool isAtEnd = lastChild != null && currentFrameInfo.time >= lastChild.MusicScoreInfo.time;
			bool isAtStart = currentFrameInfo.time >= longNote.MusicScoreInfo.time && longNote.State == NoteState.Playing;

			// At start: play head effect, but keep the long note showing (don't mark as Done)
			if (isAtStart && !isAtEnd && longNote.State != NoteState.InputBegan)
			{
				// Set JudgeInfo to Perfect for effect playback (TapEffectView checks Result)
				longNote.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
				// Play head judgment effect (tap effect and SE)
				LiveViewExt.JudgmentNote(_liveViews, longNote);
				// Set state to InputBegan to simulate holding (without actual input)
				longNote.SetStateUnnotice(NoteState.InputBegan);
			}

			// 头部触发之后，处理中间的LongHoldCombo自动判定
			if (longNote.NoteList != null && longNote.State == NoteState.InputBegan)
			{
				foreach (NoteBase childNote in longNote.NoteList)
				{
					// 跳过LongNote自身和尾部，已经处理过
					if (childNote == longNote || childNote == lastChild)
					{
						continue;
					}

					// 只处理LongHoldCombo
					if (childNote is LongHoldCombo holdCombo && childNote.State != NoteState.Done)
					{
						// 检查该combo点是否到达判定时间
						if (childNote.MusicScoreInfo.time <= currentFrameInfo.time)
						{
							// 自动判定该combo点；它只是隐形刻度，不发声也不出特效
							holdCombo.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
							holdCombo.SetStateUnnotice(NoteState.Done);
						}
					}
				}
			}

			// At end: play end effect using the last child note's type and remove the long note
			// The end note is a real note now and judges itself; settling it here would take the
			// hit away from the player and end the bar before it could be made.
			if (isAtEnd && lastChild != null && lastChild.IsDecoration)
			{
				// The bar ends here; the key at its end is only a decoration and nobody played it, so
				// no tap effect and no hit sound: the bar must never speak for the player.
				longNote.SetStateUnnotice(NoteState.Done);
				LiveViewExt.UnspawnNote(_liveViews, longNote);
			}
		}


		private bool IsTooFarAheadForDisplayHighSpeed(float timeOffset, NoteBase note)
		{
			return note != null && note.State != NoteState.Playing && IsTooFarAheadForDisplay(timeOffset, note);
		}

		/// <summary>
		/// True while a note is still too far ahead to be drawn or updated this frame. This uses the
		/// DISPLAY lead, which shrinks as the scroll speed rises: a fast note is drawn late, so stopping
		/// the scan earlier than its own display lead would make it pop in mid-lane.
		/// Registration into the judgement list is a different question with a fixed lead; see
		/// <see cref="OpenSekai.Shared.Live.JudgeListLead"/> in UpdateJudgmentNoteArray. This gate must
		/// never be reused for registration: doing that is what put a note behind a flick into the list
		/// already past its bad window.
		/// </summary>
		private bool IsTooFarAheadForDisplay(float timeOffset, NoteBase note)
		{
			if (note == null || Mathf.Approximately(timeOffset, 0f))
			{
				return false;
			}

			return currentFrameInfo.time + timeOffset + TARGET_FRAME_TIME <= note.MusicScoreInfo.time;
		}

		/// <summary>
		/// A decorative bar takes no input itself, but the note at its end is a real note of its
		/// own kind (flick, slide, rub) and has to stay playable. Such a bar therefore keeps being
		/// fed to the judgement list even though it is decoration.
		/// </summary>
		private static bool PlayableTailOf(NoteBase note)
		{
			return note is LongNote bar && bar.ChildNote is NoteBase tail && tail.HasJudgment;
		}

		private readonly LivePresentationBridge _presentation = new LivePresentationBridge();

		private void TickTailKeyViews()
		{
			_presentation.TickTailKeys(currentFrameInfo.time);
		}

		private void TickRetiringViews(MusicScoreInfo currentFrameInfo)
		{
			_presentation.TickRetiring(currentFrameInfo);
		}

		private void UpdateJudgmentNoteArray(NoteBase note)
		{
			_candidates.Register(note, _judgmentNoteList, currentFrameInfo.time + TARGET_FRAME_TIME);
		}

		private void OnProcessTouch(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch, ref int activeInputCount)
		{
			float judgmentLane = GetJudgmentLane(simTouch.WorldPosition, true);
			if (activeInputCount <= INPUT_TMP_COUNT - 1)
			{
				LiveTouch processTouch = new LiveTouch(simTouch.FingerId, simTouch.TouchId, simTouch.Delta, simTouch.WorldPosition, simTouch.Phase, simTouch.MusicTime);
				InputTmp input = _inputTmpArray[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				_activeInputList.Add(input);
			}

			if (simTouch.Phase == InputTouchPhase.Began)
			{
				TapCount++;
			}
		}


		private void Judgment()
		{
			_connectionNoteList.Clear();
			AllocateInputJudgableEachNotes();
			OverlapJudgmentSorting();
			LeaveNotesFastGeneration();
			PickUpNotBeginInputNotes();
			RefreshSustainedHolds();
			EvaluateJudgment();
			TickTailKeyViews();
			TickRetiringViews(currentFrameInfo);
		}

		private void AllocateInputJudgableEachNotes()
		{
			_candidates.Collect(_judgmentNoteList, _activeInputList, _connectionNoteList, _holds, ConvertLaneBy, InputedNormalNoteTouch);
		}

		private void AssignContinuingHoldInputs()
		{
			_holds.AssignContinuingHolds(_judgmentNoteList, _activeInputList, ConvertLaneBy);
		}

		private void RefreshSustainedHolds()
		{
			_holds.Refresh(_judgmentNoteList, _activeInputList, currentFrameInfo, UnityEngine.Time.deltaTime, ConvertLaneBy);
		}



		private void OverlapJudgmentSorting()
		{
			_candidates.Sort(_activeInputList, currentFrameInfo, ConvertLaneBy);
		}



		/// <summary>
		/// A long note that is currently held by a finger. Such a note must stay at the front of that
		/// finger's candidate list, because a press only judges one candidate per frame and a hold
		/// that stops being picked stops receiving input and releases.
		/// </summary>
		private void LeaveNotesFastGeneration()
		{
			_candidates.DropExtraFastGeneration(_activeInputList, currentFrameInfo, ConvertLaneBy);
		}
		private void PickUpNotBeginInputNotes()
		{
			_candidates.PickUp(_judgmentNoteList, _activeInputList, _connectionNoteList, _holds, ConvertLaneBy, InputedNormalNoteTouch);
		}

		internal static bool ShouldReserveHold(int holder, int candidate, NoteState state, bool ownerCanJudge)
		{
			return LiveCandidatePipeline.ShouldReserveHold(holder, candidate, state, ownerCanJudge);
		}

		/// <summary>
		/// Which candidate wins the frame's single judgment slot. A held long note ranks last: the
		/// refresh pass keeps it alive, while a flick or a rub/connection note can only be completed
		/// by running Judgment on this frame.
		/// </summary>

		internal static int CandidateRank(NoteBase note)
		{
			return LiveCandidatePipeline.CandidateRank(note);
		}


		private void EvaluateJudgment()
		{
			_usedFingerList.Clear();
			for (int inputIndex = 0; inputIndex < _activeInputList.Count; inputIndex++)
			{
				InputTmp input = _activeInputList[inputIndex];
				JudgmentPass pass = new JudgmentPass();
				pass.Logic = this;
				pass.Input = input;
				JudgmentSchedule.Run(input.CandidateNotes.Count, ref pass);
			}

			for (int inputIndex = 0; inputIndex < _activeInputList.Count; inputIndex++)
			{
				InputTmp input = _activeInputList[inputIndex];
				bool used = false;
				for (int i = _usedFingerList.Count - 1; i >= 0; i--)
				{
					used |= _usedFingerList[i] == input.Touch.fingerId;
				}

				if (!used && input.Lane >= 0f)
				{
					LiveViewExt.Unpicked(_liveViews, Mathf.RoundToInt(input.Lane), ref input.Touch);
				}

				if (IsEndedOrCanceled(input.Touch.phase))
				{
					InputedNormalNoteTouch.Remove(input.Touch.touchId);
				}
			}

			_activeInputList.Clear();
		}


		private float ConvertLaneBy(ref NoteBase note, ref InputTmp input)
		{
			if (note == null || input == null)
			{
				return input?.Lane ?? -1f;
			}

			if ((note.Category == NoteCategory.Flick || note.Category == NoteCategory.FrictionFlick)
				&& note.Direction == NoteDirection.Left)
			{
				return GetJudgmentLane(input.Touch.worldPosition, !note.FinishFirstAction);
			}

			return input.Lane;
		}

		/// <summary>
		/// Drives <see cref="JudgmentSchedule"/> for one finger: it judges the candidates in order and
		/// reports what each one became. It is a struct so the per-frame call does not allocate.
		/// </summary>
		private struct JudgmentPass : IJudgmentPass
		{
			public LiveLogic Logic;

			public InputTmp Input;

			public JudgmentSchedule.Outcome Judge(int candidateIndex)
			{
				NoteBase note = Input.CandidateNotes[candidateIndex];
				float lane = Logic.ConvertLaneBy(ref note, ref Input);
				if (note == null)
				{
					return default(JudgmentSchedule.Outcome);
				}

				note.Judgment(ref Input.Touch, lane);

				if (note.Result == NoteResult.None || note.Result == NoteResult.Bad)
				{
					Logic.InputedNormalNoteTouch[Input.Touch.touchId] = Input.Touch.musicTime;
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
					Logic._judgmentNoteList.Remove(note);
				}

				// The note knows whether the press went into it; the schedule only needs the answer.
				return new JudgmentSchedule.Outcome(note.ClosesSlotAfterPress);
			}

			public void Consumed(int candidateIndex)
			{
				Input.CandidateNotes.Clear();
				Logic._usedFingerList.Add(Input.Touch.fingerId);
			}
		}

		private float GetJudgmentLane(Vector3 touchPosition, bool checkJudgmentY)
		{			Vector2[] positions = LiveConfig.JudgmentPositions;
			if (positions == null || positions.Length == 0)
			{
				return OpenSekai.Shared.Live.TouchLane.Outside;
			}

			float judgmentOffsetX = _liveBundleBuildData?.JudgmentOffsetX ?? 0f;
			float judgmentOffsetY = _liveBundleBuildData?.JudgmentOffsetY ?? 0f;

			return OpenSekai.Shared.Live.TouchLane.Resolve(
				positions, touchPosition, checkJudgmentY, judgmentOffsetX, judgmentOffsetY);
		}




		internal static NoteBase GetFirstNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[0] : note;
		}

		internal static NoteBase GetLastNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[note.NoteList.Count - 1] : note;
		}

		internal static bool IsSameJudgmentTime(NoteBase a, NoteBase b)
		{
			return a != null && b != null && a.MusicScoreInfo.time.Equals(b.MusicScoreInfo.time);
		}

		internal static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}

		private void ExecuteEvent(EventBase eventBase)
		{
			scoreLogic?.ExecuteEvent(eventBase);
			skillLogic?.ExecuteEvent(eventBase);
		}

		private void OnSpawnNote(NoteBase note)
		{
			_presentation.Spawn(_liveViews, note, currentFrameInfo.time);
		}

		private void OnUnSpawnNote(NoteBase note)
		{
			_presentation.Unspawn(_liveViews, note);
		}

		private void OnJudgmentNote(NoteBase note)
		{
			scoreLogic?.UpdateNoteResult(note);

			// A marker that is not drawn is silent by construction: the half beat points inside a bar
			// must never talk for the player. Everything else still shows its judgement, a Miss
			// included: the tap effect decides for itself whether the result deserves a sound.
			if (note.IsSkip)
			{
				return;
			}

			LiveViewExt.JudgmentNote(_liveViews, note);
		}

		private void OnUpdateCombo(NoteBase note)
		{
			scoreLogic?.UpdateCombo(note);
			LiveViewExt.UpdateCombo(_liveViews, scoreLogic?.Score ?? default);
		}

		private void OnUpdateScore(NoteBase note)
		{
			int addScore = scoreLogic?.CalculateAddScore(note, skillLogic?.ScoreFactor(note) ?? 1f) ?? 0;
			LiveScore score = scoreLogic?.Score ?? default;
			LiveViewExt.UpdateScore(_liveViews, ref score, addScore);
		}

		private void OnDamage(NoteBase note)
		{
			scoreLogic?.Damage(note);
			LiveViewExt.UpdateLife(_liveViews, scoreLogic?.Score ?? default);
		}

		private (NoteResult result, NoteResultDescription description) OnUpdateResult((NoteResult result, NoteResultDescription description) result)
		{
			return skillLogic?.UpdateResult(result) ?? result;
		}

		private float CalcTimeOffset(NoteBase note)
		{
			if (note == null)
			{
				return 0f;
			}

			float currentProgress = currentFrameInfo.bar + currentFrameInfo.barProgress;
			float noteProgress = note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress;
			float speedRatio = CalcNoteSpeedRatio(currentProgress, noteProgress);

			bool isNegativeSpeed = note.IsNegativeSpeed;
			float timeOffset = OpenSekai.Shared.Live.NoteDisplayOffset.Resolve(
				_noteDisplayTimeOffset, speedRatio, note.speedRatio, ref isNegativeSpeed);
			note.IsNegativeSpeed = isNegativeSpeed;
			return timeOffset;
		}

		private float CalcNoteSpeedRatio(float currentProgress, float noteProgress)
		{
			return OpenSekai.Shared.Live.NoteSpeedRatio.Resolve(
				_musicScore?.musicScoreInfoArray, currentProgress, noteProgress, currentFrameInfo.speedRatio);
		}

		private void BindNoteCallbacks(NoteBase[] notes)
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

			note.OnSpawn = OnSpawnNote;
			note.OnUnspawn = OnUnSpawnNote;
			note.OnJudgment = OnJudgmentNote;
			note.OnUpdateCombo = OnUpdateCombo;
			note.OnUpdateScore = OnUpdateScore;
			note.OnDamage = OnDamage;
			note.OnUpdateResult = OnUpdateResult;
			note.CalcTimeOffset = CalcTimeOffset;

			if (note.NoteList == null)
			{
				return;
			}

			foreach (NoteBase childNote in note.NoteList)
			{
				BindNoteCallbacks(childNote, visited);
			}
		}

		private static void BindEventCallbacks(EventBase[] events)
		{
		}

		private void UpdateSeVolume()
		{
			if (Mathf.Approximately(_seScoreVolume, currentFrameInfo.seVolume))
			{
				return;
			}

			_seScoreVolume = currentFrameInfo.seVolume;
			// TODO(original): route to SoundManager.SetVolumeSEPlayer once the CRI player pool is fully mirrored.
		}

		private static NoteBase[] SortNotes(NoteBase[] notes)
		{
			return notes == null
				? Array.Empty<NoteBase>()
				: notes.Where(note => note != null).OrderBy(note => note.MusicScoreInfo.time).ToArray();
		}

		private static EventBase[] SortEvents(EventBase[] events)
		{
			return events == null
				? Array.Empty<EventBase>()
				: events.Where(eventBase => eventBase != null).OrderBy(eventBase => eventBase.MusicScoreInfo.time).ToArray();
		}
	}

	internal static class ScoreLogicExtensions
	{
		public static bool HasValue(this ScoreLogic logic)
		{
			return logic != null;
		}
	}
}