using OpenSekai.Shared.Settings;
using System;
using System.Collections;
using CP;
using CriWare;
using Sekai.Live;
using UnityEngine;
using UnityEngine.Scripting;
using Sekai;
using Sekai.Core;

namespace Sekai.Core.Live
{
	public class BaseLiveController : BaseController
	{
		protected enum LiveControllerState
		{
			None = 0,
			Playing = 1,
			Pause = 2,
			ResumeCountDown = 3,
			Finish = 6
		}

		protected LiveControllerState state;
		protected int result;
		protected long currentMusicTimeMs;
		protected long musicLength = 120000L;
		protected uint cueId;
		protected double currentGameTime;
		protected OpenSekai.Shared.Live.Simulation.TimingCompensation timingCompensation;
		protected float accumulateTimeApartFromPause;
		protected LiveBundleBuildData liveBundleBuildData;

		public LiveBootDataBase BootData { get; protected set; }

		public LiveSettingData Settings { get; protected set; }

		public RenderTexture BackgroundTexture { get; protected set; }

		public Camera BaseCamera { get; set; }

		public LiveOutUIController liveOutUIController { get; protected set; }

		public bool JustPlayingState => state == LiveControllerState.Playing;

		public bool IsPlayableState => state == LiveControllerState.Playing || state == LiveControllerState.ResumeCountDown;

		public bool HasActiveTimeline => IsPlayableState || state == LiveControllerState.Pause;

		/// <summary>The clock the views draw with, in seconds: the audio clock moved by the audio and visual latency.</summary>
		/// Not clamped at zero: a chart that starts before its music runs the clock from below zero.
		protected float currentAudioLatencyMusicTimeMs =>
			currentMusicTimeMs / 1000f - (timingCompensation.AudioLatencyMs - timingCompensation.VisualLatencyMs) / 1000f;

		/// <summary>The signed chart clock used for note execution and judgment.</summary>
		protected float CurrentLogicChartTime
		{
			get
			{
				double audioSeconds = currentMusicTimeMs / 1000d - timingCompensation.LogicDelaySeconds;
				return (float)(BootData?.MusicData?.AudioToChartTime(audioSeconds) ?? audioSeconds);
			}
		}

		/// <summary>Where the audio is in its life, as the live clock sees it.</summary>
		protected internal enum MusicPhase
		{
			/// <summary>The clock has not started (loading, the info page).</summary>
			NotStarted = 0,
			/// <summary>The clock runs on its own: the chart starts before the music.</summary>
			RunIn = 1,
			/// <summary>The audio plays and the clock follows it.</summary>
			Playing = 2,
			/// <summary>The audio is over and the clock runs on its own until the chart is done.</summary>
			Ended = 3
		}

		/// <summary>A clock step longer than this (a hitch) is cut short while the clock runs on its own.</summary>
		private const float MAX_FREE_CLOCK_STEP_SECONDS = 0.1f;

		/// <summary>How long the result effect stays up before the fade to the result.</summary>
		private const float RESULT_EFFECT_HOLD_SECONDS = 4f;

		/// <summary>How much longer than the start wait the info page may take before the live goes on without it.</summary>
		private const float INTRO_WAIT_SLACK_SECONDS = 4f;

		protected MusicPhase musicPhase;

#if UNITY_EDITOR
		internal bool CaptureFinished => state == LiveControllerState.Finish;
		internal long CaptureTimeMs { get => currentMusicTimeMs; set => currentMusicTimeMs = value; }
		internal MusicPhase CapturePhase { get => musicPhase; set => musicPhase = value; }
#endif

		/// <summary>The music and chart spans read when the chart loaded. See <see cref="BuildTimelineSpan"/>.</summary>
		protected OpenSekai.Shared.Live.Simulation.LiveTimelineSpan timelineSpan;

		protected internal long TimelineStartMs => (long)Math.Floor(timelineSpan.StartSeconds * 1000d);

		protected long TimelineEndMs => (long)Math.Ceiling(timelineSpan.EndSeconds * 1000d);

		/// <summary>When the result effect is due on the clock. See <see cref="OpenSekai.Shared.Live.Simulation.LiveTimelineSpan.ResultEffectSeconds"/>.</summary>
		protected long ResultEffectMs => (long)Math.Ceiling(timelineSpan.ResultEffectSeconds * 1000d);

		/// <summary>Where the audio file starts playing (its leading silence included).</summary>
		protected long MusicStartMs => (long)Math.Round(timelineSpan.PlaybackStartSeconds * 1000d);

		/// <summary>
		/// Where the music's sound ends: the result waits for it, not for the silence after it. The
		/// clip length when no span was read.
		/// </summary>
		protected long MusicEndMs => timelineSpan.MusicEndSeconds > 0f
			? (long)Math.Round(timelineSpan.MusicEndSeconds * 1000d)
			: musicLength;

		private long _preparedMusicStartMs;
		private double _freeClockRemainderMs;

		protected override void OnAwake()
		{
			Application.backgroundLoadingPriority = ThreadPriority.High;
			liveBundleBuildData = ResourceCatalog.LiveBundleConfig.Load();
			Settings = LiveSettingsStore.Shared.Load();
			LiveConfig.LoadMasterConfig();
			LiveConfig.LoadOptionData();
			LiveConfig.CacheSpeedTime = 0f;
			liveOutUIController = new LiveOutUIController();
		}

		protected virtual void Setup()
		{
			FramerateUtility.ApplyFrameRateLimit(Settings?.FrameRateLimit ?? FramerateUtility.FOLLOW_DISPLAY);
			Screen.sleepTimeout = SleepTimeout.NeverSleep;

			LiveBootDataBase bootData = BootData;
			if (bootData != null)
			{
				EnsureBackgroundTextureSizeForCurrentScreen();
				ScreenConfig.DownResolution(ConvertQualityType(bootData.MVQualityType));
			}

			ApplicationLocalSettings localSettings = ApplicationLocalSettings.LoadFromStorage();
			ApplicationLocalSettings.VolumeSettings liveVolume = localSettings.LiveVolume ?? localSettings.SetupLiveVolume();
			SoundManager.Instance.SetupVolume(1f, liveVolume.Bgm, liveVolume.Se, liveVolume.Voice);

			// Autoplay has no habit to correct, so the judge offset is a player's setting only.
			bool auto = BootData?.IsAuto == true;
			timingCompensation = new OpenSekai.Shared.Live.Simulation.TimingCompensation(
				Settings?.AudioLatencyMs ?? 0f,
				Settings?.VisualLatencyMs ?? 0f,
				auto ? 0f : Settings?.JudgeOffsetMs ?? 0f);
			LiveConfig.ViewLeadSeconds = timingCompensation.ViewLeadSeconds;
		}

		public bool EnsureBackgroundTextureSizeForCurrentScreen()
		{
			if (BootData == null)
			{
				return false;
			}

			ScreenConfig.ScreenSize renderTextureSize = GetBackgroundRenderTextureSizeForCurrentScreen();
			if (BackgroundTexture != null && BackgroundTexture.width == renderTextureSize.width && BackgroundTexture.height == renderTextureSize.height)
			{
				return false;
			}

			if (BackgroundTexture == null)
			{
				BackgroundTexture = new RenderTexture(renderTextureSize.width, renderTextureSize.height, 24, RenderTextureFormat.ARGB32);
				return true;
			}

			BackgroundTexture.Release();
			BackgroundTexture.width = renderTextureSize.width;
			BackgroundTexture.height = renderTextureSize.height;
			BackgroundTexture.Create();
			return true;
		}

		private ScreenConfig.ScreenSize GetBackgroundRenderTextureSizeForCurrentScreen()
		{
			Sekai.Live.MVQualityType qualityType = ConvertQualityType(BootData.MVQualityType);
			ScreenConfig.ScreenSize renderTextureSize = LiveConfig.GetRenderTextureSize(qualityType, BootData.LivePlayMode);
			int screenWidth = Screen.width;
			int screenHeight = Screen.height;
			if (screenWidth <= 0 || screenHeight <= 0 || renderTextureSize.width <= 0 || renderTextureSize.height <= 0)
			{
				return renderTextureSize;
			}

			// OpenSekai: live background baking must keep the current display aspect.
			// Otherwise a 16:9 baked jacket texture is stretched on wide or narrow screens.
			float screenAspect = (float)screenWidth / screenHeight;
			int width = Mathf.Max(1, Mathf.RoundToInt(renderTextureSize.height * screenAspect));
			return new ScreenConfig.ScreenSize(width, renderTextureSize.height);
		}

		protected virtual void LoadSound()
		{
			SoundManager.Instance.LoadSoundBundle("live/sound/live_se/default", true);
			LoadTapSeSoundBundle();

			string cueName = ResolveMusicCueName();
			if (!string.IsNullOrEmpty(cueName))
			{
				SoundManager.Instance.LoadSoundBundle(GetLiveMusicBundleName(cueName), true);
				UpdateMusicLength(cueName);
			}
		}

		private void LoadTapSeSoundBundle()
		{
			int noteSeIndex = Settings?.NoteSeIndex ?? 0;
			LiveConfig.SetNoteSeName(noteSeIndex);
			SoundManager.Instance.LoadSoundBundle("live/tap_se/" + LiveConfig.NoteSeName, true);
			SoundManager.Instance.ResumeIngameSe();
		}

		protected virtual IEnumerator LiveStart(float waitTime)
		{
			Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
			yield return null;

			GC.Collect(0, GCCollectionMode.Forced, true, true);
			GC.WaitForPendingFinalizers();
#if !UNITY_EDITOR
			GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
#endif

			bool keepTransitionUntilMusicStart = ShouldKeepTransitionUntilMusicStart();
			if (!keepTransitionUntilMusicStart)
			{
				LiveTransitioner.SafeForceFinish(null);
			}

			yield return StartCoroutine(MusicReady());

			OnMusicStart();

			if (ShouldWaitBeforeRhythmGameStart())
			{
				float introSeconds = IntroDurationSeconds();
				StartTimelineClock(timelineSpan.ClockAtIntroStart(introSeconds));
				bool heldAtStart = false;

				// Let the file's silence play during the info page, but wait for the actual fade-out
				// callback before letting the clock advance past the playable start.
				float deadline = Time.realtimeSinceStartup + Mathf.Max(waitTime, introSeconds) + INTRO_WAIT_SLACK_SECONDS;
				while (!IsIntroFinished() && Time.realtimeSinceStartup < deadline)
				{
					yield return null;
					if (heldAtStart) continue;

					UpdateMusicTime(TimelineStartMs);
					if (currentMusicTimeMs >= TimelineStartMs && !IsIntroFinished())
					{
						if (musicPhase == MusicPhase.Playing) SoundManager.Instance.Pause();
						heldAtStart = true;
					}
				}

				if (currentMusicTimeMs != TimelineStartMs) SeekTimeline(TimelineStartMs);
				if (heldAtStart) ResumeMusicPlayback();
			}
			else
			{
				float until = Time.realtimeSinceStartup + 0.05f;
				while (Time.realtimeSinceStartup < until) yield return null;
				StartTimelineClock(timelineSpan.StartSeconds);
			}

			OnRhythmGameStart();
		}

		protected virtual bool ShouldKeepTransitionUntilMusicStart()
		{
			return BootData?.MusicData?.PlayStartEffectEnabled == true && BootData?.ReleaseTransitionBeforeMusicStart != true;
		}

		protected virtual bool ShouldWaitBeforeRhythmGameStart()
		{
			return BootData?.MusicData?.PlayStartEffectEnabled == true;
		}

		protected virtual IEnumerator MusicReady()
		{
			string cueName = ResolveMusicCueName();
			if (!string.IsNullOrEmpty(cueName))
			{
				cueId = SoundManager.Instance.PrepareIngameBGM(cueName, currentMusicTimeMs / 1000f);
			}

			musicPhase = MusicPhase.NotStarted;
			_preparedMusicStartMs = currentMusicTimeMs;
			yield return null;
		}

		/// <summary>True once every view is done with the info page.</summary>
		protected virtual bool IsIntroFinished() => true;

		/// <summary>The info page goes up. The music stays prepared and paused until the clock reaches it.</summary>
		protected virtual void OnMusicStart()
		{
			accumulateTimeApartFromPause = 0f;
		}

		/// <summary>How long the views' info page stays up.</summary>
		protected virtual float IntroDurationSeconds() => 0f;

		/// <summary>Starts the clock at a music time; the audio follows when the clock reaches the file.</summary>
		private void StartTimelineClock(float clockSeconds)
		{
			currentMusicTimeMs = (long)Math.Floor(clockSeconds * 1000d);
			_freeClockRemainderMs = 0d;
			musicPhase = MusicPhase.RunIn;
			TraceTimeline("clock start at=" + currentMusicTimeMs + " point=" + TimelineStartMs);
			if (currentMusicTimeMs >= MusicStartMs)
			{
				StartMusicPlayback();
			}
		}

		/// <summary>The info page is gone: the clock is at the start point and the game takes input.</summary>
		protected virtual void OnRhythmGameStart()
		{
			result = 0;
			TraceTimeline("game start at=" + currentMusicTimeMs + " point=" + TimelineStartMs);
			state = LiveControllerState.Playing;
		}

		/// <summary>The clock reached the music: start the audio there.</summary>
		private void StartMusicPlayback()
		{
			musicPhase = MusicPhase.Playing;
			_freeClockRemainderMs = 0d;
			if (currentMusicTimeMs != _preparedMusicStartMs)
			{
				// The run-in overshot the start of the file by part of a frame; begin the audio where the
				// clock is.
				SoundManager.Instance.SeekIngame(Math.Max(0L, currentMusicTimeMs));
			}
			SoundManager.Instance.ResumePreparedPlaybackIngame();
			TraceTimeline("music start at=" + currentMusicTimeMs);
			OnMusicPlaybackStarted(currentMusicTimeMs / 1000f);
		}

		protected virtual void OnMusicPlaybackStarted(float musicTime)
		{
		}

		/// <summary>
		/// Moves the clock forward to a music time, and the audio with it. Before the audio has started
		/// (the run-in), the clock moves alone and the audio starts there if the clock has reached it.
		/// </summary>
		protected void SeekTimeline(long targetMs)
		{
			_freeClockRemainderMs = 0d;
			if (musicPhase == MusicPhase.RunIn)
			{
				currentMusicTimeMs = targetMs;
				if (currentMusicTimeMs >= MusicStartMs)
				{
					StartMusicPlayback();
				}
				return;
			}

			SoundManager.Instance.SeekIngame(targetMs);
			currentMusicTimeMs = targetMs;
		}

		/// <summary>Resumes the audio after a pause, unless the clock has not reached it yet or it is over.</summary>
		protected void ResumeMusicPlayback()
		{
			if (musicPhase == MusicPhase.Playing)
			{
				SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			}
		}

		/// <summary>
		/// Reads the four times the live runs over: the music's start and end, and the first and last
		/// note, all on the audio clock. Map note time onto the file clock and add the logic delay
		/// to place each target where it is actually judged.
		/// </summary>
		protected void BuildTimelineSpan(MusicScore musicScore)
		{
			LiveMusicData music = BootData?.MusicData;
			float first = float.NaN;
			float last = float.NaN;
			if (musicScore?.HiddenSimLines?.Length > 0)
			{
				first = (float)((music?.ChartToAudioTime(musicScore.FirstHiddenLineTime) ?? musicScore.FirstHiddenLineTime)
					+ timingCompensation.LogicDelaySeconds);
				last = (float)((music?.ChartToAudioTime(musicScore.LastHiddenLineTime) ?? musicScore.LastHiddenLineTime)
					+ timingCompensation.LogicDelaySeconds);
			}
			NoteBase[] roots = musicScore?.NoteArray;
			if (roots != null)
			{
				foreach (NoteBase root in roots)
				{
					if (root?.NoteList == null)
					{
						continue;
					}

					foreach (NoteBase note in root.NoteList)
					{
						if (note == null)
						{
							continue;
						}

						double audioTime = music?.ChartToAudioTime(note.MusicScoreInfo.time) ?? note.MusicScoreInfo.time;
						float time = (float)(audioTime + timingCompensation.LogicDelaySeconds);
						if (float.IsNaN(first) || time < first)
						{
							first = time;
						}
						if (float.IsNaN(last) || time > last)
						{
							last = time;
						}
					}
				}
			}

			// The music counts from its first audible sample to its last: that decides the start and end
			// points and the skips, so silence at either end of a file changes nothing. The silence is
			// not cut, though: the file is played from its top, early enough for the start point to land
			// where it should (see LiveStart).
			float playbackStart = Math.Max(0L, BootData?.MusicData?.StartMusicTimeMs ?? 0L) / 1000f;
			float playbackEnd = musicLength / 1000f;
			float musicStart = playbackStart;
			float musicEnd = playbackEnd;
			string cueName = ResolveMusicCueName();
			if (!string.IsNullOrEmpty(cueName)
				&& SoundManager.Instance.TryGetIngameAudibleRange(cueName, out float audibleStart, out float audibleEnd))
			{
				TraceTimeline("audible " + audibleStart.ToString("F3") + ".." + audibleEnd.ToString("F3") + " clip=" + musicEnd.ToString("F3"));
				musicStart = Math.Max(musicStart, audibleStart);
				musicEnd = audibleEnd;
			}

			float leadIn = LiveConfig.GetNoteDisplayOffsetTime(BootData?.LiveSettingData?.NoteSpeed ?? 6f);
			timelineSpan = new OpenSekai.Shared.Live.Simulation.LiveTimelineSpan(
				musicStart, musicEnd, first, last, leadIn, playbackStart, playbackEnd);
			TraceTimeline("span " + timelineSpan);
		}

		protected static void TraceTimeline(string message)
		{
			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Audio))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Audio, "timeline " + message);
			}
		}

		protected override void OnUpdate()
		{
			currentGameTime = Time.realtimeSinceStartupAsDouble;
			base.OnUpdate();

			if (NativeInput.BackPressedThisFrame)
			{
				OnBackKey();
			}

			if (state != LiveControllerState.Pause)
			{
				accumulateTimeApartFromPause += Time.unscaledDeltaTime;
			}
		}

		/// <summary>
		/// Moves the live clock. While the audio plays the clock is the audio's; before it starts (a
		/// chart that starts first) and after it ends (a chart that runs past it) the clock runs on its
		/// own, so the notes at either end still get their time instead of a clock pinned to the song's
		/// edge, which left the last notes waiting for a moment that never came.
		/// </summary>
		protected virtual void UpdateMusicTime() => UpdateMusicTime(long.MaxValue);

		private void UpdateMusicTime(long runInLimit)
		{
			switch (musicPhase)
			{
				case MusicPhase.RunIn:
					AdvanceFreeClock();
					currentMusicTimeMs = Math.Min(currentMusicTimeMs, runInLimit);
					if (currentMusicTimeMs >= MusicStartMs)
					{
						StartMusicPlayback();
					}
					return;

				case MusicPhase.Playing:
					if (SoundManager.Instance.GetIngameStatus() == CriAtomExPlayback.Status.Playing)
					{
						long audioTime = SoundManager.Instance.GetAudioSyncedUnityTimer();
						if (audioTime >= 0L)
						{
							currentMusicTimeMs = audioTime;
						}
						return;
					}

					musicPhase = MusicPhase.Ended;
					TraceTimeline("music end at=" + currentMusicTimeMs);
					AdvanceFreeClock();
					return;

				case MusicPhase.Ended:
					AdvanceFreeClock();
					return;
			}
		}

		private void AdvanceFreeClock()
		{
			double stepMs = Math.Min(Time.unscaledDeltaTime, MAX_FREE_CLOCK_STEP_SECONDS) * 1000d + _freeClockRemainderMs;
			long wholeMs = (long)stepMs;
			currentMusicTimeMs += wholeMs;
			_freeClockRemainderMs = stepMs - wholeMs;
		}

		public void Retry()
		{
			OnRetry();
		}

		public void Retire()
		{
			OnRetire();
		}

		public void Pause()
		{
			OnPause();
		}

		public void Resume()
		{
			OnResume();
		}

		public void CallPreExit()
		{
			if (BootData?.MusicData?.IsTestPlay == true)
			{
				PreExit(0f, 0f);
				return;
			}

			if (ShouldReturnCustomMusicScoreManagerAfterResult())
			{
				PreExit(1f, 4f);
				return;
			}

			// The result effect comes a breath after the music's sound (not the silence after it) or the
			// last note, whichever ends last. The outro skip brings it forward.
			if (state == LiveControllerState.Playing)
			{
				PreExit(0f, RESULT_EFFECT_HOLD_SECONDS, ResultEffectMs);
			}
			else
			{
				// Explicit exits can also arrive from a pause menu with the playback clock stopped.
				float remainingSeconds = Math.Max(0L, ResultEffectMs - currentMusicTimeMs) / 1000f;
				PreExit(remainingSeconds, RESULT_EFFECT_HOLD_SECONDS);
			}
		}

		private bool ShouldReturnCustomMusicScoreManagerAfterResult()
		{
			if (BootData?.IsCustomMusicScore != true)
			{
				return false;
			}

			// The library builds every play this way too, but it returns to its own scene and wants the
			// full ending: the result effect at the end of the music, and the outro skip.
			return BootData is FreeLiveBootData freeLiveBootData
				&& freeLiveBootData.ReturnScreenType == MenuScreenType.MusicScoreMakerTop
				&& freeLiveBootData.ReturnScene != SceneManager.Scene.Library;
		}

		protected virtual void OnBackKey()
		{
			if (state == LiveControllerState.Pause)
			{
				OnResume();
			}
			else if (state == LiveControllerState.Playing)
			{
				OnPause();
			}
		}

		protected virtual void OnPause()
		{
		}

		protected virtual void OnResume()
		{
		}

		protected virtual void OnRetry()
		{
		}

		protected virtual void OnRetire()
		{
		}

		protected virtual void OnFinished()
		{
			CallPreExit();
		}

		protected virtual void PreExit(float delay, float waitTime, long? resultEffectAtMs = null)
		{
		}

		protected override void OnExit()
		{
			LiveConfig.ViewLeadSeconds = 0f;
			SoundManager.Instance.StopAll();
			GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
			Screen.sleepTimeout = SleepTimeout.SystemSetting;
			ScreenConfig.ResetResolution();

			if (BackgroundTexture != null)
			{
				BackgroundTexture.Release();
				BackgroundTexture = null;
			}
		}

		protected string ResolveMusicCueName()
		{
			if (BootData is FreeLiveBootData freeLiveBootData && !string.IsNullOrEmpty(freeLiveBootData.CustomMusicScoreId))
			{
				return "custom_" + freeLiveBootData.CustomMusicScoreId;
			}

			if (!string.IsNullOrEmpty(BootData?.MusicData?.Vocal?.assetbundleName))
			{
				return BootData.MusicData.Vocal.assetbundleName;
			}

			return BootData?.MusicData?.Music?.assetbundleName;
		}

		private void UpdateMusicLength(string cueName)
		{
			CriAtomEx.CueInfo[] cueInfos = SoundManager.Instance.GetCueInfos(cueName);
			foreach (CriAtomEx.CueInfo cueInfo in cueInfos)
			{
				if ((string.IsNullOrEmpty(cueInfo.name) || cueInfo.name == cueName) && cueInfo.length > 0)
				{
					// A native package's registered cue is its actual audio file, including clips under six seconds.
					bool legacyShortCue = cueInfo.length < 6000 && BootData?.MusicData?.NativeBgmOffsetSeconds == null;
					musicLength = legacyShortCue ? 120000L : cueInfo.length;
					return;
				}
			}
		}

		private static string GetLiveMusicBundleName(string cueName)
		{
			string normalized = cueName.Replace('\\', '/');
			if (normalized.StartsWith("music/long/", StringComparison.OrdinalIgnoreCase))
			{
				return normalized;
			}

			return normalized.Contains("/") ? normalized : "music/long/" + normalized;
		}

		private static Sekai.Live.MVQualityType ConvertQualityType(Sekai.MVQualityType qualityType)
		{
			return qualityType == Sekai.MVQualityType.High ? Sekai.Live.MVQualityType.High : Sekai.Live.MVQualityType.Low;
		}
	}
}
