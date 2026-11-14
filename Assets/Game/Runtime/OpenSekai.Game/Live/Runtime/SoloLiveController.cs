using OpenSekai.Shared.Settings;
using System.Collections;
using Sekai.Live;
using UnityEngine;
using OpenSekai.Shared.Playback;
using OpenSekai.Shared.UI;
using CriWare;
using Sekai;

namespace Sekai.Core.Live
{
	public class SoloLiveController : BaseLiveController
	{
		private Coroutine _finishCoroutine;
		private Coroutine _resumeCoroutine;
		private bool _playHistoryRecorded;
		private bool _retiredByPlayer;
		private LivePauseOverlay _pauseOverlay;
		private LiveViewBase[] _liveViews;
		private LiveLogic _liveLogic;

		private LiveContext _context;

		protected override void OnAwake()
		{
			base.OnAwake();
			// The context arrives from the scene entry point right after AddComponent, so the boot body
			// runs from Initialize. Awake only prepares the base state.
			if (_context != null)
			{
				BeginLive();
			}
		}

		/// <summary>
		/// Takes the context the entry point assembled and starts the live. Called once, immediately
		/// after AddComponent, so the timing matches the old OnAwake body exactly.
		/// </summary>
		public void Initialize(LiveContext context)
		{
			if (context == null)
			{
				throw new System.ArgumentNullException(nameof(context));
			}
			if (_context != null)
			{
				throw new System.InvalidOperationException("The live has already been initialized.");
			}

			_context = context;
			BootData = context.BootData;
			if (BootData != null)
			{
				BeginLive();
			}
			else
			{
				Debug.LogWarning("FreeLiveBootData is null. Test play cannot start.");
			}
		}

		private void BeginLive()
		{
			currentMusicTimeMs = System.Math.Max(0L, BootData.MusicData?.StartMusicTimeMs ?? 0L);
			Setup();
			LoadSound();
			_liveViews = LiveViewFactory.Create(BootData, transform);
			LiveViewExt.Setup(_liveViews, this);
			RetryProcess();
			BuildTimelineSpan(BootData.MusicData?.MusicScore);
			liveOutUIController?.Initialize(BootData.LivePlayMode, BaseCamera);
			LiveViewExt.OnLoad(_liveViews);
			StartCoroutine(LiveStart(6f));
		}

		protected override void OnMusicStart()
		{
			LiveTransitioner.SafeFinish(null, null);
			base.OnMusicStart();
			LiveViewExt.MusicStart(_liveViews, currentAudioLatencyMusicTimeMs);
		}

		protected override bool IsIntroFinished() => LiveViewExt.IsIntroFinished(_liveViews);

		protected override float IntroDurationSeconds() => LiveViewExt.IntroDurationSeconds(_liveViews);

		protected override void OnMusicPlaybackStarted(float musicTime)
		{
			LiveViewExt.MusicPlaybackStart(_liveViews, musicTime);
		}

		protected override void OnRhythmGameStart()
		{
			LiveViewExt.RhythmGameStart(_liveViews);
			base.OnRhythmGameStart();
			_liveLogic?.RefreshInput();
			_forcedFinish = false;
			if (timelineSpan.HasIntroSkip)
			{
				ShowSkip("跳过前奏", SkipIntro);
			}
		}

		/// <summary>How long past the end of the timeline a live may stay open before it is ended by force.</summary>
		private const long StalledFinishGraceMs = 3000L;

		private const float LiveTimelineSpanMargin = OpenSekai.Shared.Live.Simulation.LiveTimelineSpan.SkipMarginSeconds;

		private LiveSkipButton _skipButton;
		private bool _forcedFinish;
		private long _outroSkipAtMs = -1L;

		private void ShowSkip(string label, System.Action onPress)
		{
			if (_skipButton == null)
			{
				_skipButton = LiveSkipButton.Create(transform);
			}

			_skipButton.Show(label, onPress);
		}

		private void HideSkip()
		{
			_skipButton?.Hide();
		}

		/// <summary>Jumps the music and the clock to the margin before the first note.</summary>
		private void SkipIntro()
		{
			long targetMs = (long)(timelineSpan.IntroSkipTargetSeconds * 1000f);
			if (state != LiveControllerState.Playing || musicPhase != MusicPhase.Playing || currentMusicTimeMs >= targetMs)
			{
				return;
			}

			TraceTimeline("intro skip from=" + currentMusicTimeMs + " to=" + targetMs);
			SoundManager.Instance.SeekIngame(targetMs);
			currentMusicTimeMs = targetMs;
			LiveViewExt.MusicSeek(_liveViews, targetMs / 1000f);
		}

		/// <summary>Cuts the wait for the rest of the music and goes to the result now.</summary>
		private void SkipOutro()
		{
			if (state != LiveControllerState.Finish)
			{
				return;
			}

			TraceTimeline("outro skip at=" + currentMusicTimeMs);
			if (_finishCoroutine != null)
			{
				StopCoroutine(_finishCoroutine);
				_finishCoroutine = null;
			}
			Exit();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (state == LiveControllerState.Finish && _outroSkipAtMs >= 0L)
			{
				// Nothing else reads the clock once the live is finished; it is kept moving only to
				// know when the outro skip is due.
				UpdateMusicTime();
				if (currentMusicTimeMs >= _outroSkipAtMs)
				{
					_outroSkipAtMs = -1L;
					if (currentMusicTimeMs < MusicEndMs)
					{
						ShowSkip("跳过，立即结算", SkipOutro);
					}
				}
			}

			if (state != LiveControllerState.Playing)
			{
				return;
			}

			UpdateMusicTime();
			if (_skipButton != null && _skipButton.IsVisible
				&& currentMusicTimeMs >= (long)(timelineSpan.IntroSkipTargetSeconds * 1000f))
			{
				HideSkip();
			}
			float fillerSec = BootData?.MusicData?.Music?.fillerSec ?? 0f;
			_liveLogic?.OnUpdate(currentLogicMusicTime - fillerSec, currentGameTime);
			if (BootData?.IsAuto == true)
			{
				_liveLogic?.OnAutoInput();
			}
			else
			{
				_liveLogic?.OnInput();
			}
			if (_liveLogic?.result == 2 && _liveLogic.IsNotesAllFinished)
			{
				OnFinished();
			}
			else if (!_forcedFinish && state == LiveControllerState.Playing && _liveLogic != null
				&& !_liveLogic.IsNotesAllFinished && currentMusicTimeMs > TimelineEndMs + StalledFinishGraceMs)
			{
				// The clock is well past the last note and the music, and something is still open: a
				// note whose state never reached Done. End the live rather than leave it hanging, and
				// say what was left so the note can be fixed.
				_forcedFinish = true;
				string pending = _liveLogic.DescribeUnfinishedNotes();
				Debug.LogWarning("[Live] forced finish: notes still open past the end. " + pending);
				TraceTimeline("forced finish at=" + currentMusicTimeMs + " " + pending);
				OnFinished();
			}

			// The view block, last: it draws what the two blocks above decided.
			_liveLogic?.OnView();
			LiveViewExt.OnUpdate(_liveViews, currentAudioLatencyMusicTimeMs);
		}

		private new void OnApplicationPause(bool pauseStatus)
		{
			SoundManager.Instance.TraceIngameSync(pauseStatus ? "app-pause" : "app-resume");
			if (pauseStatus)
			{
				OnPause();
			}
		}

		protected override void OnApplicationFocus(bool hasFocus)
		{
			base.OnApplicationFocus(hasFocus);
			SoundManager.Instance.TraceIngameSync(hasFocus ? "focus-gain" : "focus-lost");
			// The notification shade, an incoming-call overlay and the like take focus on Android
			// without pausing the app. The live has to stop for them too. Not in the Editor, where
			// clicking another window would pause every test play.
			if (!hasFocus && !Application.isEditor)
			{
				OnPause();
			}
		}

		protected override void OnPause()
		{
			if ((state == LiveControllerState.Playing || state == LiveControllerState.ResumeCountDown) && result == 0)
			{
				// A pause during the resume countdown has to cancel it, or the countdown runs out
				// behind the pause dialog and starts the music again.
				if (_resumeCoroutine != null)
				{
					StopCoroutine(_resumeCoroutine);
					_resumeCoroutine = null;
				}
				state = LiveControllerState.Pause;
				SoundManager.Instance.Pause();
				LiveViewExt.Pause(_liveViews);
				ShowPauseDialog();
			}
		}

		public void ShowPauseDialog()
		{
			if (IsLibraryPlaybackFlow())
			{
				ShowLibraryPauseOverlay();
				return;
			}

			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		private bool IsLibraryPlaybackFlow()
		{
			return BootData is FreeLiveBootData free && free.ReturnScene == SceneManager.Scene.Library;
		}

		private void ShowLibraryPauseOverlay()
		{
			if (_pauseOverlay == null)
			{
				_pauseOverlay = LivePauseOverlay.Create(transform);
			}

			string title = (BootData as FreeLiveBootData)?.CustomMusicScoreTitle;
			_pauseOverlay.Show(title, OnResume, Retry, OnRetireByMySelf);
		}

		public void PauseLive()
		{
			if ((state != LiveControllerState.Playing && state != LiveControllerState.ResumeCountDown) || result != 0)
			{
				return;
			}

			state = LiveControllerState.Pause;
			SoundManager.Instance.Pause();
			LiveViewExt.Pause(_liveViews);
		}

		protected override void OnResume()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			_pauseOverlay?.Hide();
			liveOutUIController?.Destroy();
			LiveViewExt.Countdown(_liveViews);
			if (_resumeCoroutine != null)
			{
				StopCoroutine(_resumeCoroutine);
			}
			_resumeCoroutine = StartCoroutine(ResumeCoroutine());
		}

		public void ResumeNoCountDown()
		{
			if (state != LiveControllerState.Pause)
			{
				return;
			}

			state = LiveControllerState.ResumeCountDown;
			liveOutUIController?.Destroy();
			ResumeMusicPlayback();
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(_liveViews, currentAudioLatencyMusicTimeMs);
			_liveLogic?.RefreshInput();
		}

		public void ShowConsecutiveAutoLiveRetireDialog()
		{
			CallPreExit();
		}

		protected override void OnRetry()
		{
			_pauseOverlay?.Hide();
			HideSkip();
			_outroSkipAtMs = -1L;
			liveOutUIController?.Destroy();
			SoundManager.Instance.StopIngame();
			_playHistoryRecorded = false;
			result = 0;
			state = LiveControllerState.None;
			currentMusicTimeMs = System.Math.Max(0L, BootData?.MusicData?.StartMusicTimeMs ?? 0L);
			RetryProcess();
			LiveViewExt.Retry(_liveViews);
			StartCoroutine(LiveStart(6f));
		}

		private void RetryProcess()
		{
			if (_liveLogic != null)
			{
				_liveLogic.OnFinished -= OnFinished;
				_liveLogic.OnFailure -= OnFailure;
			}

			_liveLogic = new LiveLogic(liveBundleBuildData);
			_liveLogic.Setup(BootData, _liveViews);
			_liveLogic.SetSkillLogic(new SkillLogic());
			_liveLogic.SetScoreLogic(new ScoreLogic(liveBundleBuildData));
			_liveLogic.OnFinished += OnFinished;
			_liveLogic.OnFailure += OnFailure;
		}

		protected override void OnRetire()
		{
			_retiredByPlayer = true;
			liveOutUIController?.Destroy();
			result = 2;
			PreExit(0f, 4f);
		}

		private void OnRetireByMySelf()
		{
			_retiredByPlayer = true;
			liveOutUIController?.Destroy();
			result = 1;
			PreExit(0f, 0f);
		}

		private void OnConfirmCancel()
		{
			liveOutUIController?.ShowPauseDialog(OnResume, OnRetireConfirm, OnRetryConfirm);
		}

		private void OnRetireConfirm()
		{
			liveOutUIController?.ShowConfirmRetireDialog(OnRetireByMySelf, OnConfirmCancel);
		}

		private void OnRetryConfirm()
		{
			liveOutUIController?.ShowConfirmRetryDialog(Retry, OnConfirmCancel);
		}

		private System.Collections.IEnumerator ResumeCoroutine()
		{
			_liveLogic?.RefreshInput();
			yield return new WaitForSeconds(3f);
			ResumeMusicPlayback();
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(_liveViews, currentAudioLatencyMusicTimeMs);
			SoundManager.Instance.SetAudioSyncedUnityTimer(cueId);
			_resumeCoroutine = null;
		}

		private void OnFailure()
		{
			// Only while the audio drives the clock: before and after it the timer reads 0, which used
			// to throw the clock back to the start of the song.
			long audioTime = musicPhase == MusicPhase.Playing ? SoundManager.Instance.GetAudioSyncedUnityTimer() : -1L;
			if (audioTime > 0L)
			{
				currentMusicTimeMs = audioTime;
			}
		}

		protected override void OnFinished()
		{
			base.OnFinished();
		}

		protected override void PreExit(float delay, float waitTime)
		{
			if (state == LiveControllerState.Finish)
			{
				return;
			}

			if (result == 0)
			{
				result = 3;
			}

			TryAppendPlayHistory();
			PublishPlayResultForLibrary();
			state = LiveControllerState.Finish;
			SetFinish(delay, waitTime, 2f);

			// The chart is done but the music runs on: the result waits for it unless the player skips.
			// The button only comes up the margin after the last note, so the end of the chart is seen.
			HideSkip();
			_outroSkipAtMs = result == 3 && timelineSpan.HasOutroSkip && musicPhase == MusicPhase.Playing
				? (long)System.Math.Ceiling((timelineSpan.LastNoteSeconds + LiveTimelineSpanMargin) * 1000d)
				: -1L;
		}

		private void TryAppendPlayHistory()
		{
			// The recorder is the App layer's; without one the run simply is not written down. Both
			// halves of the guard are checked before _playHistoryRecorded is set, so a recorder that
			// arrives later still gets the run.
			if (_playHistoryRecorded || result != 3 || BootData?.IsAuto == true || _context?.PlayHistory == null)
			{
				return;
			}

			_playHistoryRecorded = true;
			if (BootData is FreeLiveBootData freeLiveBootData)
			{
				_context.PlayHistory.AppendLiveResult(
					freeLiveBootData,
					_liveLogic?.Score ?? default,
					currentMusicTimeMs,
					musicLength,
					_context.SessionId,
					_context.ChartHash,
					_context.RulesVersion);
			}
		}

		/// <summary>
		/// Hand the finished session to the lightweight library scene. Only the Library return
		/// path is covered: the editor's test-play flow keeps its own dialogs.
		/// result: 1 = retired by the player, 2 = failed (life zero / forced retire), 3 = completed.
		/// </summary>
		private void PublishPlayResultForLibrary()
		{
			if (!(BootData is FreeLiveBootData free) || free.ReturnScene != SceneManager.Scene.Library)
			{
				return;
			}

			PlayResultKind kind = result == 3
				? PlayResultKind.Completed
				: _retiredByPlayer ? PlayResultKind.Retired : PlayResultKind.Failed;
			LiveScore score = _liveLogic != null ? _liveLogic.Score : default;
			PlayResultHandoff.Publish(PlayResult.FromBootData(free, kind, score));
		}

		protected override void OnExit()
		{
			LiveViewExt.Finish3D(_liveViews);
			LiveViewExt.OnUnload(_liveViews);
			SceneManager.Scene returnScene = (BootData as FreeLiveBootData)?.ReturnScene ?? SceneManager.Scene.Library;
			base.OnExit();
			SceneManager.Instance.RequestScene(returnScene);
		}

		private void SetFinish(float delay, float waitTime, float finishWaitSeconds)
		{
			if (_finishCoroutine != null)
			{
				StopCoroutine(_finishCoroutine);
			}
			if (_liveLogic != null)
			{
				_liveLogic.OnFinished -= OnFinished;
				_liveLogic.OnFailure -= OnFailure;
			}
			_finishCoroutine = StartCoroutine(SetFinishCoroutine(delay, waitTime, finishWaitSeconds));
		}

		private System.Collections.IEnumerator SetFinishCoroutine(float delay, float waitTime, float finishWaitSeconds)
		{
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}
			yield return ResultAnim(waitTime, finishWaitSeconds);
		}

		private System.Collections.IEnumerator ResultAnim(float waitTime, float finishWaitSeconds)
		{
			LiveViewExt.Result(_liveViews, (int)GetLiveResultAnimationType());
			if (waitTime > 0f)
			{
				yield return new WaitForSeconds(waitTime);
			}
			LiveViewExt.Finish(_liveViews, finishWaitSeconds);
			if (finishWaitSeconds > 0f)
			{
				yield return new WaitForSeconds(finishWaitSeconds);
			}
			yield return null;
			Exit();
		}

		private LiveResultAnimationType GetLiveResultAnimationType()
		{
			if (result < 2)
			{
				return LiveResultAnimationType.None;
			}

			if (result == 2)
			{
				return LiveResultAnimationType.Failure;
			}

			if (result != 3)
			{
				Debug.LogErrorFormat("Unsupported live result: {0}", result);
				return LiveResultAnimationType.None;
			}

			LiveScore score = _liveLogic?.Score ?? default;
			// Check if this is an Auto play
			bool isAutoPlay = BootData?.IsAuto == true;
			int autoResultAnimation = LiveSettingsStore.Shared.Load()?.AutoResultAnimationMode ?? LiveSettingData.AUTO_RESULT_ANIMATION_NONE;

			// If all notes were Auto, handle based on settings
			if (score.totalComboCount > 0 && score.autoCount == score.totalComboCount)
			{
				if (isAutoPlay)
				{
					// Auto play with all Auto notes - use custom result animation setting
					switch (autoResultAnimation)
					{
						case LiveSettingData.AUTO_RESULT_ANIMATION_NONE:
							return LiveResultAnimationType.None;
						case LiveSettingData.AUTO_RESULT_ANIMATION_ALL_PERFECT:
							return LiveResultAnimationType.AllPerfect;
						case LiveSettingData.AUTO_RESULT_ANIMATION_FULL_COMBO:
							return LiveResultAnimationType.FullCombo;
						case LiveSettingData.AUTO_RESULT_ANIMATION_CLEAR:
							return LiveResultAnimationType.Clear;
						case LiveSettingData.AUTO_RESULT_ANIMATION_FINISH:
							return LiveResultAnimationType.LifeZero;
					}
				}
				return LiveResultAnimationType.Clear;
			}

			if (score.totalComboCount == score.perfectCount)
			{
				return LiveResultAnimationType.AllPerfect;
			}
			if (score.totalComboCount == score.maxCombo)
			{
				return LiveResultAnimationType.FullCombo;
			}
			return score.life > 0 ? LiveResultAnimationType.Clear : LiveResultAnimationType.LifeZero;
		}

	}
}
