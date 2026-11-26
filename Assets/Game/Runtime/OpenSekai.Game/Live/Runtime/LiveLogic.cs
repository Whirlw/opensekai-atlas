using System;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	public class LiveLogic
	{
		[Serializable]
		public class InputTmp
		{
			public LiveTouch Touch;
			internal bool IsResidentSample { get; set; }
			internal float PreviousLane { get; set; } = OpenSekai.Shared.Live.TouchLane.OUTSIDE;
			/// <summary>Which press pass of the frame may judge this Began; see <see cref="JudgmentScheduler"/>.</summary>
			internal int PressWave { get; set; }
			private float _lane;
			public float Lane
			{
			    get => _lane;
			    set => _lane = value;
			}
			private List<NoteBase> _candidateNotes;
			public List<NoteBase> CandidateNotes
			{
			    get => _candidateNotes;
			    set => _candidateNotes = value;
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
				IsResidentSample = source.IsResidentSample;
				Lane = source.Lane;
				PreviousLane = source.PreviousLane;
				PressWave = source.PressWave;
				CandidateNotes.AddRange(source.CandidateNotes);
			}

			public void Reset(ref LiveTouch touch, float lane)
			{
				Touch = touch;
				IsResidentSample = false;
				Lane = lane;
				PreviousLane = OpenSekai.Shared.Live.TouchLane.OUTSIDE;
				PressWave = 0;
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

		private readonly LiveClock _clock = new LiveClock();
		private readonly LivePresentationBridge _presentation = new LivePresentationBridge();

		internal LiveSessionState Session { get; } = new LiveSessionState();
		internal LiveInputRouter Input { get; }
		internal LiveNoteFrame NoteFrame { get; }
		internal JudgmentScheduler Scheduler { get; }
		internal NextRushConnectorAudio ConnectorAudio { get; } = new();

		public LiveLogic(LiveBundleBuildData data)
		{
			Input = new LiveInputRouter(this, data);
			NoteFrame = new LiveNoteFrame(this, Session, Input, _presentation);
			Scheduler = new JudgmentScheduler(this, Session, Input, NoteFrame, _presentation);
		}

		public event Action Finished;
		public event Action Failed;

		private void RaiseFailed() => Failed?.Invoke();
		private void RaiseFinished() => Finished?.Invoke();

		public ScoreLogic scoreLogic
		{
			get => Session.ScoreLogic;
			set => Session.ScoreLogic = value;
		}

		public SkillLogic skillLogic
		{
			get => Session.SkillLogic;
			set => Session.SkillLogic = value;
		}

		public int noteStartIndex
		{
			get => Notes.NoteIndex;
			set => Notes.NoteIndex = value;
		}

		public int highSpeedNoteStartIndex
		{
			get => Notes.HighSpeedIndex;
			set => Notes.HighSpeedIndex = value;
		}

		public LiveNoteSession Notes => Session.Notes;

		private int _result;
		private int _lastShownRemainingNotes;
		public int TapCount
		{
			get => Session.TapCount;
			set => Session.TapCount = value;
		}

		public double currentGameTime
		{
			get => _clock.GameTime;
			set => _clock.GameTime = value;
		}

		public MusicScoreInfo currentFrameInfo;

		public LiveScore Score => Session.Score;
		public bool IsNotesAllFinished => Session.AreNotesFinished
			&& (Session.MusicScore == null || currentFrameInfo.time >= Session.MusicScore.LastHiddenLineTime);

		/// <summary>What is still holding the live open, for the trace of a live that had to be ended by force.</summary>
		internal string DescribeUnfinishedNotes()
		{
			LiveNoteSession notes = Session.Notes;
			string tail = "none";
			foreach (NoteBase pending in notes.PendingTails)
			{
				tail = Describe(pending);
				break;
			}

			return string.Concat(
				"cursor=", notes.NoteIndex.ToString(), "/", notes.Notes.Length.ToString(), " ", DescribeAt(notes.Notes, notes.NoteIndex),
				" hs=", notes.HighSpeedIndex.ToString(), "/", notes.HighSpeedNotes.Length.ToString(), " ", DescribeAt(notes.HighSpeedNotes, notes.HighSpeedIndex),
				" heads=", notes.PendingHeads.Count.ToString(),
				" tails=", notes.PendingTailCount.ToString(), " firstTail=", tail,
				" now=", currentFrameInfo.time.ToString("F3"));
		}

		private static string DescribeAt(NoteBase[] array, int index) =>
			index < array.Length ? Describe(array[index]) : "end";

		private static string Describe(NoteBase note) =>
			note == null
				? "null"
				: string.Concat(note.GetType().Name, "@", note.MusicScoreInfo.time.ToString("F3"),
					" state=", note.State.ToString(), " res=", note.Result.ToString());
		public bool IsPerfectCombo => scoreLogic?.IsPerfectCombo ?? false;
		public bool IsAllPerfectCombo => scoreLogic?.IsAllPerfectCombo ?? false;

		public void Setup(LiveBootDataBase _bootData, LiveViewBase[] _liveViews)
		{
			MusicScore.CurrentFrameInfo.speedRatio = 1f;
			ConnectorAudio.Reset();
			Session.Bind(_bootData, _liveViews);
			Scheduler.BindNoteCallbacks(Notes.Notes);
			_presentation.CreatePool(Session.Views, LiveUtility.CalculateMaxSimultaneousNotes(Session.MusicScore, Session.NoteDisplayTimeOffset));
			Camera camera = CameraUtility.GetFrontCamera();
			if (camera == null)
			{
				camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
			}
			Input.SetCamera(camera);
			Result = 0;
			Session.Reset(_presentation);
			RefreshInput();
		}

		public void SetSkillLogic(SkillLogic logic)
		{
			skillLogic = logic ?? new NullSkillLogic();
			skillLogic.Setup(Session.BootData, Score);
		}

		public void SetScoreLogic(ScoreLogic logic)
		{
			scoreLogic = logic;
			scoreLogic?.Setup(Session.BootData, Session.MusicScore);
			_lastShownRemainingNotes = logic?.RemainingNoteCount ?? 0;
			_presentation.SetupScore(Session.Views, logic?.HudScore ?? default);
		}

		public void RefreshInput()
		{
			Input.Reset();
			Scheduler.ResetDamageInputs();
			Clock.ResetFrameHistory();
		}

		/// <summary>
		/// The state block of the frame. It lives in <see cref="LiveNoteFrame"/> because that is what
		/// owns the note walk; this only connects the call.
		/// </summary>
		public void OnUpdate(float scoreInfoTime, double currentGameTime) =>
			NoteFrame.UpdateState(scoreInfoTime, currentGameTime);

		public int Result
		{
			get => _result;
			set
			{
				if (_result == value)
				{
					return;
				}

				_result = value;
				if (value == 2)
				{
					RaiseFailed();
				}
				if (value >= 3)
				{
					RaiseFinished();
				}
			}
		}

		/// <summary>Autoplay has no touch block: the automatic answer stands in for it.</summary>
		public void OnAutoInput()
		{
			NoteFrame.AutoUpdate();
			if (_result < 3) ConnectorAudio.Update(Session.MusicScore, currentFrameInfo.time, true, null);
			else ConnectorAudio.Silence();
		}

		/// <summary>
		/// The view block, the last one of the frame: what the two blocks above decided is drawn now,
		/// once.
		/// </summary>
		public void OnView()
		{
			NoteFrame.TickViews();
			// Combo and score callbacks can arrive in either order. Draw after both have settled.
			var board = scoreLogic;
			if (board?.UseSubtractiveScore == true && board.RemainingNoteCount != _lastShownRemainingNotes)
			{
				_lastShownRemainingNotes = board.RemainingNoteCount;
				_presentation.UpdateScore(Session.Views, board.HudScore, 0);
			}
		}

		/// <summary>
		/// The touch block: collect this frame's raw samples, then let the judge answer them. Both steps
		/// belong to the block; this only connects them, and keeps them separate so a caller can drive
		/// the judge with inputs it built itself.
		/// </summary>
		public void OnInput()
		{
			Input.CollectTouches();
			Scheduler.Run();
			ApplyFrameOutcome();
		}

		/// <summary>Reads the frame that just ran and moves the session out of play when it has to.</summary>
		internal void ApplyFrameOutcome()
		{
			if (scoreLogic != null && scoreLogic.Score.life <= 0)
			{
				Result = 2;
			}

			if (IsNotesAllFinished && _result == 0)
			{
				Result = 3;
			}
		}

		/// <summary>The music clock, for the blocks that sample it.</summary>
		internal LiveClock Clock => _clock;

		public float ResolveInputMusicTime(double inputTime) => _clock.ResolveInputMusicTime(currentFrameInfo, inputTime);

		/// <summary>
		/// The legacy formula. See <see cref="OpenSekai.Shared.Live.Simulation.InputTimeMapping.FromLegacyClock"/>.
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

		internal void SetTouchSource(ILiveTouchSource source) => Input.SetTouchSource(source);

	}
}
