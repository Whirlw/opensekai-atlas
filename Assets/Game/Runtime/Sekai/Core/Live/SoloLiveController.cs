using System.Collections;
using Sekai.Live;
using Sekai.MusicScoreMaker.Common;
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

		protected override void OnAwake()
		{
			base.OnAwake();
			BootData = UserDataManager.Instance.FreeLiveBootData;
			if (BootData == null)
			{
				Debug.LogWarning("FreeLiveBootData is null. Test play cannot start.");
				return;
			}

			currentMusicTimeMs = System.Math.Max(0L, BootData.MusicData?.StartMusicTimeMs ?? 0L);
			Setup();
			LoadSound();
			_liveViews = LiveViewFactory.Create(BootData, transform);
			LiveViewExt.Setup(_liveViews, this);
			RetryProcess();
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

		protected override void OnRhythmGameStart()
		{
			LiveViewExt.RhythmGameStart(_liveViews);
			base.OnRhythmGameStart();
			_liveLogic?.RefreshInput();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (state != LiveControllerState.Playing)
			{
				return;
			}

			UpdateMusicTime();
			float fillerSec = BootData?.MusicData?.Music?.fillerSec ?? 0f;
			_liveLogic?.OnUpdate(currentAudioLatencyMusicTimeMs - fillerSec, currentGameTime);
			if (_liveLogic?.result == 2 && _liveLogic.IsNotesAllFinished)
			{
				OnFinished();
			}
			if (BootData?.IsAuto == true)
			{
				_liveLogic?.OnAutoInput();
			}
			else
			{
				_liveLogic?.OnInput();
			}
			LiveViewExt.OnUpdate(_liveViews, currentAudioLatencyMusicTimeMs);
		}

		private new void OnApplicationPause(bool pauseStatus)
		{
			if (pauseStatus)
			{
				OnPause();
			}
		}

		protected override void OnPause()
		{
			if ((state == LiveControllerState.Playing || state == LiveControllerState.ResumeCountDown) && result == 0)
			{
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
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
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
			SoundManager.Instance.ResumeIngame(currentMusicTimeMs);
			state = LiveControllerState.Playing;
			LiveViewExt.Resume(_liveViews, currentAudioLatencyMusicTimeMs);
			SoundManager.Instance.SetAudioSyncedUnityTimer(cueId);
			_resumeCoroutine = null;
		}

		private void OnFailure()
		{
			currentMusicTimeMs = SoundManager.Instance.GetAudioSyncedUnityTimer();
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
		}

		private void TryAppendPlayHistory()
		{
			if (_playHistoryRecorded || result != 3 || BootData?.IsAuto == true)
			{
				return;
			}

			_playHistoryRecorded = true;
			if (BootData is FreeLiveBootData freeLiveBootData)
			{
				CustomMusicScorePlayHistoryStorage.AppendLiveResult(
					freeLiveBootData,
					_liveLogic?.Score ?? default,
					currentMusicTimeMs,
					musicLength,
					PlaySessionContext.SessionId,
					PlaySessionContext.ChartHash,
					PlaySessionContext.RULES_VERSION);
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
			int autoResultAnimation = LiveSettingData.LoadFromStorage()?.AutoResultAnimationMode ?? LiveSettingData.AUTO_RESULT_ANIMATION_NONE;

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
