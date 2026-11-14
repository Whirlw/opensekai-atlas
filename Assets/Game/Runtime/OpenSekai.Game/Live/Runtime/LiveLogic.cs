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
			internal float PreviousLane { get; set; } = -1f;
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
				PreviousLane = source.PreviousLane;
				CandidateNotes.AddRange(source.CandidateNotes);
			}

			public void Reset(ref LiveTouch touch, float lane)
			{
				Touch = touch;
				Lane = lane;
				PreviousLane = -1f;
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

		public LiveLogic(LiveBundleBuildData data)
		{
			Input = new LiveInputRouter(this, data);
			NoteFrame = new LiveNoteFrame(this, Session, Input, _presentation);
			Scheduler = new JudgmentScheduler(this, Session, Input, NoteFrame, _presentation);
		}

		public event Action OnFinished;
		public event Action OnFailure;

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

		public int result;
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
		public bool IsNotesAllFinished => Session.AreNotesFinished;
		public bool IsPerfectCombo => scoreLogic?.IsPerfectCombo ?? false;
		public bool IsAllPerfectCombo => scoreLogic?.IsAllPerfectCombo ?? false;

		public void Setup(LiveBootDataBase _bootData, LiveViewBase[] _liveViews)
		{
			Session.Bind(_bootData, _liveViews);
			Scheduler.BindNoteCallbacks(Notes.Notes);
			_presentation.CreatePool(Session.Views, LiveUtility.CalculateMaxSimultaneousNotes(Session.MusicScore, Session.NoteDisplayTimeOffset));
			Camera camera = CameraUtility.GetFrontCamera();
			if (camera == null)
			{
				camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
			}
			Input.SetCamera(camera);
			result = 0;
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
			_presentation.SetupScore(Session.Views, Score);
		}

		public void RefreshInput() => Input.Reset();

		/// <summary>
		/// The state block of the frame. It lives in <see cref="LiveNoteFrame"/> because that is what
		/// owns the note walk; this only connects the call.
		/// </summary>
		public void OnUpdate(float scoreInfoTime, double currentGameTime) =>
			NoteFrame.UpdateState(scoreInfoTime, currentGameTime);

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

		/// <summary>Autoplay has no touch block: the automatic answer stands in for it.</summary>
		public void OnAutoInput() => NoteFrame.AutoUpdate();

		/// <summary>
		/// The view block, the last one of the frame: what the two blocks above decided is drawn now,
		/// once.
		/// </summary>
		public void OnView() => NoteFrame.TickViews();

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

			if (IsNotesAllFinished && result == 0)
			{
				result = 3;
				OnFinished?.Invoke();
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
