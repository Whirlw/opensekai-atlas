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
		private readonly int[] _holdInputNotes = new int[INPUT_TMP_COUNT];
		private readonly bool[] _holdInputVisited = new bool[INPUT_TMP_COUNT];
		private readonly Func<int, int, bool> _holdInputEligible;
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
			_holdInputEligible = CanContinueHold;
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
			_tailKeyViews.Clear();
			_retiringViews.Clear();
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
			for (int i = noteStartIndex; i < _notes.Notes.Length; i++)
			{
				NoteBase note = _notes.Notes[i];
				if (note == null)
				{
					if (i == noteStartIndex)
					{
						noteStartIndex++;
					}
					continue;
				}

				if (note.State == NoteState.Done)
				{
					if (i == noteStartIndex)
					{
						noteStartIndex++;
					}
					continue;
				}

				// Decoration notes need to be processed after Excute to check OffsetJudgeTime
				// Skip early decoration check here - will be handled after Excute

				if (!Mathf.Approximately(note.speedRatio, 1f))
				{
					if (i == noteStartIndex)
					{
						noteStartIndex++;
					}
					continue;
				}

				float offsetTime = CalcTimeOffset(note);
				if (IsTooFarAheadForDisplayHighSpeed(offsetTime, note))
				{
					break;
				}
				if (offsetTime < 0f && currentFrameInfo.time >= note.MusicScoreInfo.time)
				{
					offsetTime = -offsetTime;
				}
				note.Excute(currentFrameInfo, offsetTime);
				
				// Handle decoration notes: auto-judge at precise judgment time (like autoplay)
				// For long notes, trigger at both start and end times
				if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
				{
					DecorationJudgment(note);
				}
				
				if (note.State == NoteState.Done && i == noteStartIndex)
				{
					noteStartIndex++;
				}
				else if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
				{
					UpdateJudgmentNoteArray(note);
				}
			}

			for (int i = highSpeedNoteStartIndex; i < _notes.HighSpeedNotes.Length; i++)
			{
				NoteBase note = _notes.HighSpeedNotes[i];
				if (note == null)
				{
					if (i == highSpeedNoteStartIndex)
					{
						highSpeedNoteStartIndex++;
					}
					continue;
				}

				if (note.State == NoteState.Done)
				{
					if (i == highSpeedNoteStartIndex)
					{
						highSpeedNoteStartIndex++;
					}
					continue;
				}

				// Decoration notes need to be processed after Excute to check OffsetJudgeTime
				// Skip early decoration check here - will be handled after Excute

				float offsetTime = currentFrameInfo.time >= note.MusicScoreInfo.time
					? _noteDisplayTimeOffset
					: CalcTimeOffset(note);
				if (IsTooFarAheadForDisplay(offsetTime, note))
				{
					break;
				}

				note.Excute(currentFrameInfo, offsetTime);
				
				// Handle decoration notes: auto-judge at precise judgment time (like autoplay)
				// For long notes, trigger at both start and end times
				if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
				{
					DecorationJudgment(note);
				}
				
				if (note.State == NoteState.Done && i == highSpeedNoteStartIndex)
				{
					highSpeedNoteStartIndex++;
				}
				else if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
				{
					UpdateJudgmentNoteArray(note);
				}
			}
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

		private void AdvanceFinishedNotes()
		{
			while (noteStartIndex < _notes.Notes.Length)
			{
				NoteBase note = _notes.Notes[noteStartIndex];
				if (note != null && note.State != NoteState.Done)
				{
					return;
				}
				noteStartIndex++;
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

		/// <summary>
		/// SlidingOut and Retired keys keep moving past the line until Progress reaches 1.35.
		/// This queue owns only views; judgment candidates and result publication are independent.
		/// </summary>
		private readonly List<NoteBase> _retiringViews = new List<NoteBase>();

		// Only spawned tail views are tracked; no retirement callback is bound to the notes.
		private readonly HashSet<NoteBase> _tailKeyViews = new HashSet<NoteBase>();

		private void TickTailKeyViews()
		{
			List<NoteBase> judged = null;
			foreach (NoteBase tail in _tailKeyViews)
			{
				var bar = (LongNote)tail.ParentNote;
				TailKeyPhase phase = bar.GetTailKeyPhase(currentFrameInfo.time);

				// A key that was judged leaves with its result. Letting a graded key keep riding down
				// reads as a second, phantom key hanging under the line. A key nobody judged still
				// slides away, which is what "the bar ended while the finger was elsewhere" should look
				// like: the note is gone, not a key stuck on the line.
				if (tail.Result != NoteResult.None)
				{
					(judged ??= new List<NoteBase>()).Add(tail);
					continue;
				}

				if ((phase is TailKeyPhase.SlidingOut or TailKeyPhase.Retired) && !_retiringViews.Contains(tail))
				{
					_retiringViews.Add(tail);
				}
			}

			if (judged == null)
			{
				return;
			}

			for (int index = 0; index < judged.Count; index++)
			{
				NoteBase tail = judged[index];
				tail.RemoveView();
				_tailKeyViews.Remove(tail);
				_retiringViews.Remove(tail);
			}
		}

		private void TickRetiringViews(MusicScoreInfo currentFrameInfo)
		{
			for (int index = _retiringViews.Count - 1; index >= 0; index--)
			{
				NoteBase retiring = _retiringViews[index];
				if (retiring == null)
				{
					_retiringViews.RemoveAt(index);
					continue;
				}

				retiring.TickRetire(currentFrameInfo.time);
				if (retiring.Progress >= 1.35f)
				{
					retiring.RemoveView();
					_tailKeyViews.Remove(retiring);
					_retiringViews.RemoveAt(index);
				}
			}
		}

		private void UpdateJudgmentNoteArray(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			// Registration uses the JUDGEABLE lead, not the display lead. A fast note is drawn very
			// late (its display lead is divided by the speed ratio) but must still be judgeable 0.35 s
			// early; tying registration to the display lead put a rub behind a flick into the list only
			// ~130 ms before its time, already past the edge of the bad window.
			if (JudgeListLead.IsStillTooFarAhead(
				currentFrameInfo.time + TARGET_FRAME_TIME,
				note.MusicScoreInfo.time))
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
				_judgmentNoteList.Add(note);
			}

			// A decoration bar speaks for nobody: the note at its end is the one the player plays, and
			// it is judged only through this list, never through its bar. Registering the bar instead
			// made the bar answer input (its own hit effect) while a flick or a rub at its end, whose
			// key lives inside the bar, never received a candidate slot at all.
			// A Guide bar cannot judge itself and is not flagged as decoration by the chart: its type
			// alone says "no scoring judgment". The note at its end is a real one, so that note is
			// registered instead of the bar - registering the bar let it answer input with its own hit
			// effect while a flick or a rub at its end never got a candidate slot.
			if (!barAnswersInput && tailNote != null && !_judgmentNoteList.Contains(tailNote))
			{
				_judgmentNoteList.Add(tailNote);
			}
		}

		private void OnProcessTouch(ref LiveTouch processTouch, ref int activeInputCount)
		{
			float judgmentLane = GetJudgmentLane(processTouch.worldPosition, true);
			if (activeInputCount <= INPUT_TMP_COUNT - 1)
			{
				InputTmp input = _inputTmpArray[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				_activeInputList.Add(input);
			}

			if (processTouch.phase == InputTouchPhase.Began)
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
			AssignContinuingHoldInputs();
			for (int noteIndex = 0; noteIndex < _judgmentNoteList.Count; noteIndex++)
			{
				NoteBase judgmentNote = _judgmentNoteList[noteIndex];
				if (judgmentNote == null)
				{
					continue;
				}

				bool isConnection = true;
				bool isJudged = false;
				for (int inputIndex = _activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
				{
					InputTmp activeInput = _activeInputList[inputIndex];
					NoteBase note = judgmentNote;
					// A finger that owns a hold is still free to judge other notes in its lane:
					// charts overlay a separate contact-only note on top of a bar. Only hold-vs-hold
					// contention needs the reservation.
					if (_holdInputNotes[inputIndex] >= 0 && _holdInputNotes[inputIndex] != noteIndex && judgmentNote is LongNote)
					{
						continue;
					}
					// Another finger is already holding this long note: leave it to that finger, otherwise
					// the second finger nominates it, steals the holder and the hold drops for a frame.
					if (IsReservedForAnotherInput(judgmentNote, activeInput))
					{
						continue;
					}

					float lane = ConvertLaneBy(ref note, ref activeInput);

					if (lane >= 0f && note.IsJudgment(ref activeInput.Touch, lane))
					{
						isJudged = true;
						int touchId = activeInput.Touch.touchId;
						NoteBase lastNote = GetLastNote(judgmentNote);
						if (judgmentNote.NoteList != null && judgmentNote.NoteList.Count == 1)
						{
							AddCheckSingleInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId);
						}
						else
						{
							AddCheckLongInputedNormalNoteTouch(ref judgmentNote, ref lastNote, ref activeInput, ref isConnection, ref isJudged, ref touchId);
						}
						continue;
					}

					NoteBase firstNote = GetFirstNote(judgmentNote);
					if (firstNote != null && TryAddConnectionCandidate(firstNote, activeInput))
					{
						isConnection = true;
						isJudged = true;
					}
				}

				if (isJudged && isConnection)
				{
					_connectionNoteList.Add(judgmentNote);
				}
			}
		}

		private bool CanContinueHold(int noteIndex, int inputIndex)
		{
			NoteBase note = _judgmentNoteList[noteIndex];
			InputTmp input = _activeInputList[inputIndex];
			float lane = ConvertLaneBy(ref note, ref input);
			return lane >= 0f && note.IsJudgment(ref input.Touch, lane);
		}

		private static bool HasContinuingHold(NoteBase note)
		{
			return note is LongNote held && held.HoldingTouchId >= 0
				&& (held.State == NoteState.InputBegan || held.State == NoteState.Input || held.State == NoteState.Release);
		}

		private void AssignContinuingHoldInputs()
		{
			int count = _activeInputList.Count;
			for (int i = 0; i < count; i++) _holdInputNotes[i] = -1;
			// First preserve every still-valid owner. Then augment the assignment for holds whose
			// owner left their lane, allowing a wide and a narrow crossing hold to exchange fingers.
			for (int n = 0; n < _judgmentNoteList.Count; n++)
			{
				if (!HasContinuingHold(_judgmentNoteList[n])) continue;
				var held = (LongNote)_judgmentNoteList[n];
				for (int i = 0; i < count; i++)
				{
					if (_holdInputNotes[i] < 0
						&& _activeInputList[i].Touch.fingerId == held.HoldingTouchId
						&& !IsEndedOrCanceled(_activeInputList[i].Touch.phase)
						&& CanContinueHold(n, i))
					{
						_holdInputNotes[i] = n;
						break;
					}
				}
			}
			for (int n = 0; n < _judgmentNoteList.Count; n++)
			{
				if (!HasContinuingHold(_judgmentNoteList[n]) || Array.IndexOf(_holdInputNotes, n, 0, count) >= 0) continue;
				Array.Clear(_holdInputVisited, 0, count);
				OpenSekai.Shared.Live.HoldInputMatching.TryAssign(n, count, _holdInputEligible, _holdInputNotes, _holdInputVisited);
			}
			for (int i = 0; i < count; i++)
			{
				if (_holdInputNotes[i] >= 0)
					((LongNote)_judgmentNoteList[_holdInputNotes[i]]).AssignHoldingInput(_activeInputList[i].Touch.fingerId);
			}
		}

		private void RefreshSustainedHolds()
		{
			// A hold survives on lastInputFrame, which only Judgment() refreshes, and EvaluateJudgment
			// spends one finger on one note per frame. That couples survival to candidate ownership:
			// when a second finger or a second overlapping long note takes this frame's slot, the hold
			// nobody judged goes stale and releases. Refresh every hold whose own finger is still in its
			// lane, without consuming a candidate slot or touching ownership.
			int count = _activeInputList.Count;

			// A bar is not owned by a finger: any touch on it keeps it alive, and a touch that leaves
			// the screen inside it is the release the tail listens for. This runs before ownership so a
			// second finger can neither steal the hold nor swallow the tail.
			CoverHoldsWithAnyFinger(count);

			for (int noteIndex = 0; noteIndex < _judgmentNoteList.Count; noteIndex++)
			{
				NoteBase note = _judgmentNoteList[noteIndex];
				if (note is not LongNote held || held.State == NoteState.Done)
				{
					continue;
				}

				if (held.State != NoteState.InputBegan && held.State != NoteState.Input && held.State != NoteState.Release)
				{
					continue;
				}

				if (TryRefreshFromOwner(held, count))
				{
					continue;
				}

				// The owner is gone (lifted or cancelled) while another finger already sits inside this
				// bar. Hand the hold over on this frame instead of waiting for the old refresh to go
				// stale: otherwise the bar greys for a moment before the new finger is allowed in.
				if (!HasLiveOwner(held, count))
				{
					AdoptHoldFromAnyFinger(held, count);
				}
			}
		}

		/// <summary>
		/// Feeds every live bar the fingers currently on it, and finishes the bar when a finger leaves
		/// the screen inside it — the tail's normal judgement.
		/// </summary>
		private void CoverHoldsWithAnyFinger(int count)
		{
			for (int noteIndex = 0; noteIndex < _judgmentNoteList.Count; noteIndex++)
			{
				if (_judgmentNoteList[noteIndex] is not LongNote held || held.State == NoteState.Done)
				{
					continue;
				}

				bool covered = false;
				bool releasedInside = false;
				float swipeDeltaX = 0f;
				float swipeDeltaY = 0f;
				for (int inputIndex = 0; inputIndex < count; inputIndex++)
				{
					InputTmp input = _activeInputList[inputIndex];
					if (input == null)
					{
						continue;
					}

					// Every note is judged against the lane that already applies the vertical gate, and a
					// bar is covered by the same measure: a finger above the judgment line has not reached
					// the bar yet.
					bool endsNow = UnityEngine.InputSystem.InputExtensions.IsEndedOrCanceled(input.Touch.phase);
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
				bool headSettled = held.Result != NoteResult.None
					|| held.State == NoteState.InputBegan
					|| held.State == NoteState.Input;
				if (covered && headSettled)
				{
					held.KeepAlive(currentFrameInfo.time);
				}
				bool isBeingHeld = held.State == NoteState.InputBegan || held.State == NoteState.Input;

				// The bar asks its tail how it ends. There is no switch here any more, and no way for a
				// new kind of end note to be drawn by one path and judged by another: the three shapes
				// (lift, swipe, self judged) each own their own answer.
				held.TailBehaviour.TryFinish(held, new TailFrame(
					currentFrameInfo.time,
					UnityEngine.Time.deltaTime,
					new UnityEngine.Vector2(swipeDeltaX, swipeDeltaY),
					releasedInside,
					covered,
					isBeingHeld));


				// A bar ends when its tail window closes, whatever closed it. Only the flick path had a
				// timeout before, so a bar ending with a friction or a hidden note could never end:
				// a finger in its lane kept reviving it and its parts stayed judgeable afterwards.
				held.ExpireTail(currentFrameInfo.time);
			}
		}

		private bool TryRefreshFromOwner(LongNote held, int count)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				InputTmp input = _activeInputList[inputIndex];
				if (input.Touch.fingerId != held.HoldingTouchId || IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				NoteBase laneNote = held;
				float lane = ConvertLaneBy(ref laneNote, ref input);
				if (lane >= 0f && held.ContainsLane(lane))
				{
					held.RefreshHeldInput(ref input.Touch, lane);
					return true;
				}
			}

			return false;
		}

		private bool HasLiveOwner(LongNote held, int count)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				InputTmp input = _activeInputList[inputIndex];
				if (input.Touch.fingerId == held.HoldingTouchId && !IsEndedOrCanceled(input.Touch.phase))
				{
					return true;
				}
			}

			return false;
		}

		private void AdoptHoldFromAnyFinger(LongNote held, int count)
		{
			for (int inputIndex = 0; inputIndex < count; inputIndex++)
			{
				InputTmp input = _activeInputList[inputIndex];
				if (IsEndedOrCanceled(input.Touch.phase))
				{
					continue;
				}

				NoteBase laneNote = held;
				float lane = ConvertLaneBy(ref laneNote, ref input);
				if (lane < 0f || !held.ContainsLane(lane))
				{
					continue;
				}

				held.AssignHoldingInput(input.Touch.fingerId);
				held.RefreshHeldInput(ref input.Touch, lane);
				return;
			}
		}


		private void OverlapJudgmentSorting()
		{
			// Original: per finger, rank candidates by the result they would get right now, then by
			// score time, then by lane distance, and drop the farther note of any same-time pair.
			for (int inputIndex = _activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = _activeInputList[inputIndex];
				if (input.CandidateNotes.Count <= 1)
				{
					continue;
				}

				input.CandidateNotes.Sort((a, b) =>
				{
					if (ReferenceEquals(a, b))
					{
						return 0;
					}

					return OpenSekai.Shared.Live.CandidateOrder.Compare(
						CandidateRank(a), a.CalcNoteResult(input.Touch.musicTime), a.MusicScoreInfo.time,
						a.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref a, ref input)),
						CandidateRank(b), b.CalcNoteResult(input.Touch.musicTime), b.MusicScoreInfo.time,
						b.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref b, ref input)));
				});

				for (int i = input.CandidateNotes.Count - 1; i > 0; i--)
				{
					NoteBase current = input.CandidateNotes[i];
					NoteBase previous = input.CandidateNotes[i - 1];
					if (current == null || previous == null || !IsSameJudgmentTime(current, previous))
					{
						continue;
					}

					float currentDistance = current.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref current, ref input));
					float previousDistance = previous.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref previous, ref input));
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

			ResolveOverlappingCandidatesAcrossInputs();
		}

		private void ResolveOverlappingCandidatesAcrossInputs()
		{
			for (int inputIndex = _activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = _activeInputList[inputIndex];
				if (input.CandidateNotes.Count == 0)
				{
					continue;
				}

				for (int otherInputIndex = inputIndex - 1; otherInputIndex >= 0; otherInputIndex--)
				{
					InputTmp otherInput = _activeInputList[otherInputIndex];
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
						float inputDistance = inputNote != null
							? inputNote.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref inputNote, ref input))
							: float.MaxValue;
						float otherDistance = otherNote != null
							? otherNote.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref otherNote, ref otherInput))
							: float.MaxValue;

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
		/// A long note that is currently held by a finger. Such a note must stay at the front of that
		/// finger's candidate list, because a press only judges one candidate per frame and a hold
		/// that stops being picked stops receiving input and releases.
		/// </summary>
		private static bool IsHeld(NoteBase note)
		{
			return note is LongNote longNote
				&& (longNote.State == NoteState.InputBegan || longNote.State == NoteState.Input);
		}
		private void LeaveNotesFastGeneration()
		{
			for (int inputIndex = _activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = _activeInputList[inputIndex];
				for (int i = input.CandidateNotes.Count - 1; i >= 0; i--)
				{
					if (input.CandidateNotes.Count < 2)
					{
						break;
					}

					NoteBase first = ResolveFastGenerationNote(input.CandidateNotes[0], input);
					NoteBase current = ResolveFastGenerationNote(input.CandidateNotes[i], input);
					if (first == null || current == null)
					{
						continue;
					}

					int dropIndex;
					if (IsSameJudgmentTime(first, current))
					{
						float firstDistance = first.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref first, ref input));
						float currentDistance = current.LaneDistance(ref currentFrameInfo, ConvertLaneBy(ref current, ref input));
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
		private void PickUpNotBeginInputNotes()
		{
			for (int inputIndex = _activeInputList.Count - 1; inputIndex >= 0; inputIndex--)
			{
				InputTmp input = _activeInputList[inputIndex];
				for (int noteIndex = _connectionNoteList.Count - 1; noteIndex >= 0; noteIndex--)
				{
					NoteBase note = _connectionNoteList[noteIndex];
					if (_holdInputNotes[inputIndex] >= 0 && note is LongNote
						&& !ReferenceEquals(note, _judgmentNoteList[_holdInputNotes[inputIndex]])) continue;
					// Allocation's ownership check must also apply to this deferred path. Otherwise
					// a newer overlapping input removes the hold before its owner can pick it up,
					// even though LongNote.Judgment later rejects that foreign input.
					if (IsReservedForAnotherInput(note, input))
					{
						continue;
					}
					float lane = ConvertLaneBy(ref note, ref input);
					if (lane >= 0f && note != null && note.IsJudgment(ref input.Touch, lane))
					{
						input.CandidateNotes.Add(note);
						_connectionNoteList.RemoveAt(noteIndex);
					}
				}
			}
		}

		internal static bool ShouldReserveHold(int holder, int candidate, NoteState state, bool ownerCanJudge)
		{
			return holder >= 0 && holder != candidate && ownerCanJudge
				&& (state == NoteState.InputBegan || state == NoteState.Input || state == NoteState.Release);
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
		private bool IsReservedForAnotherInput(NoteBase note, InputTmp input)
		{
			if (note is not LongNote held || held.HoldingTouchId < 0 || held.HoldingTouchId == input.Touch.fingerId)
			{
				return false;
			}

			// Excute can mark yesterday's hold Release before today's input pass (MissMesh is zero).
			// Reserve by the owner's actual eligible input this frame, not by yesterday's timestamp.
			for (int i = 0; i < _activeInputList.Count; i++)
			{
				InputTmp owner = _activeInputList[i];
				if (owner.Touch.fingerId != held.HoldingTouchId || IsEndedOrCanceled(owner.Touch.phase)) continue;
				float lane = ConvertLaneBy(ref note, ref owner);
				if (ShouldReserveHold(held.HoldingTouchId, input.Touch.fingerId, held.State,
					lane >= 0f && held.IsJudgment(ref owner.Touch, lane))) return true;
			}

			return held.IsHeldByAnotherFinger(input.Touch.fingerId, input.Touch.musicTime);
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

		private void AddCheckSingleInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note, ref InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId)
		{
			if (judgmentNote is FrictionNote)
			{
				float lane = ConvertLaneBy(ref note, ref activeInput);
				if (judgmentNote.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			if (note is FrictionLongNote or FrictionHideLongNote)
			{
				float lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
					return;
				}
			}

			activeInput.CandidateNotes.Add(judgmentNote);
			isConnection = false;
			InputedNormalNoteTouch[touchId] = activeInput.Touch.musicTime;
		}

		private void AddCheckLongInputedNormalNoteTouch(ref NoteBase judgmentNote, ref NoteBase note, ref InputTmp activeInput, ref bool isConnection, ref bool isJudged, ref int touchId)
		{
			float lane = ConvertLaneBy(ref note, ref activeInput);
			bool hadInput = InputedNormalNoteTouch.ContainsKey(touchId);
			bool isLastNoteJudgment = note != null && note.IsJudgment(ref activeInput.Touch, lane);
			if (hadInput)
			{
				if (isLastNoteJudgment)
				{
					isConnection = false;
					if (note.CalcNoteResult(activeInput.Touch.musicTime) > NoteResult.Great
						|| activeInput.Touch.musicTime - InputedNormalNoteTouch[touchId] > 1f / 6f)
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

			note = GetFirstNote(judgmentNote);
			lane = ConvertLaneBy(ref note, ref activeInput);
			if ((note is FrictionLongNote or FrictionHideLongNote)
				&& note.IsJudgment(ref activeInput.Touch, lane))
			{
				isConnection = true;
				isJudged = true;
				return;
			}

			if (note != null && note.Result == NoteResult.None)
			{
				lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					activeInput.CandidateNotes.Add(judgmentNote);
					isConnection = false;
					return;
				}
			}


			if (note is LongNote)
			{
				lane = ConvertLaneBy(ref note, ref activeInput);
				if (note.IsJudgment(ref activeInput.Touch, lane))
				{
					isConnection = true;
					isJudged = true;
				}
			}
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

		private bool TryAddConnectionCandidate(NoteBase note, InputTmp input)
		{
			if (note is not LongNote and not FrictionLongNote and not FrictionHideLongNote)
			{
				return false;
			}

			float lane = ConvertLaneBy(ref note, ref input);
			if (lane < 0f || !note.IsJudgment(ref input.Touch, lane))
			{
				return false;
			}

			input.CandidateNotes.Add(note);
			return true;
		}

		private NoteBase ResolveJudgmentNote(NoteBase note, InputTmp input)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			if (note.Result != NoteResult.None || input.Touch.phase != InputTouchPhase.Began)
			{
				return GetLastNote(note);
			}

			return GetFirstNote(note);
		}

		private NoteBase ResolveFastGenerationNote(NoteBase note, InputTmp input)
		{
			if (note?.NoteList == null || note.NoteList.Count == 1)
			{
				return note;
			}

			NoteBase resolved = note.Result != NoteResult.None || input.Touch.phase != InputTouchPhase.Began
				? GetLastNote(note)
				: GetFirstNote(note);
			float lane = ConvertLaneBy(ref resolved, ref input);
			if (lane < 0f || resolved == null || !resolved.IsJudgment(ref input.Touch, lane))
			{
				return note;
			}

			return resolved;
		}

		private static NoteBase GetFirstNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[0] : note;
		}

		private static NoteBase GetLastNote(NoteBase note)
		{
			return note?.NoteList != null && note.NoteList.Count > 0 ? note.NoteList[note.NoteList.Count - 1] : note;
		}

		private static bool IsSameJudgmentTime(NoteBase a, NoteBase b)
		{
			return a != null && b != null && a.MusicScoreInfo.time.Equals(b.MusicScoreInfo.time);
		}

		private static bool IsEndedOrCanceled(InputTouchPhase phase)
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
			if (note.ParentRole == NoteParentRole.Tail)
			{
				var bar = (LongNote)note.ParentNote;
				if (!bar.ViewNoteList.Contains(note) || bar.GetTailKeyPhase(currentFrameInfo.time) != TailKeyPhase.Following) return;
				_tailKeyViews.Add(note);
			}
			LiveViewExt.SpawnNote(_liveViews, note);
		}

		private void OnUnSpawnNote(NoteBase note)
		{
			LiveViewExt.UnspawnNote(_liveViews, note);
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