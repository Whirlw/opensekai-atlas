using OpenSekai.Shared.Settings;
using System;
using System.Collections.Generic;
using Sekai.Core;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;
using CriWare;
using Sekai;

namespace Sekai.Live
{
	public class LiveFrontView : LiveViewBase
	{
		[SerializeField]
		private Transform _liveRoot;

		[SerializeField]
		private SpriteRenderer _fade;

		[SerializeField]
		private SpriteRenderer _deadMask;

		[SerializeField]
		private LaneView _laneView;

		[SerializeField]
		private MusicInfoView _musicInfo;

		[SerializeField]
		private Renderer _backgroundRenderer;

		[SerializeField]
		private JudgmentView _judgmentView;

		[SerializeField]
		private JudgmentDescriptionView _judgmentDescriptionView;

		[SerializeField]
		private ComboView _comboView;

		[SerializeField]
		private ScoreView _scoreView;

		[SerializeField]
		private LifeView _lifeView;

		[SerializeField]
		private LiveResultView _liveResultView;

		[SerializeField]
		private CutinManager _cutinManager;

		[SerializeField]
		private CountdownView _countdownView;

		[SerializeField]
		private ScreenEffectView _screenEffectView;

		[SerializeField]
		private Transform _effectRoot;

		[SerializeField]
		private Camera _effectCamera;

		[SerializeField]
		private CameraSizeUpdater _cameraSizeUpdater;

		[SerializeField]
		private Camera _frontViewCamera;

		[SerializeField]
		private GameObject _autoLabel;

		[SerializeField]
		private ConsecutiveAutoLiveView _consecutiveAutoLiveView;

		[SerializeField]
		private NoteShowRateView _noteShowRateView;

		private Material _backgroundMaterial;

		private BaseLiveController _baseController;
		private NotesViewManager _notesViewManager;
		private TapEffectView _tapEffect;
		private LongTapEffectView _longTapEffectView;
		private MusicInfoView _resolvedMusicInfo;
		private Tween _musicStartTween;
		private Tween _backgroundTween;
		private Tween _startVoiceTween;
		private Tween _spriteAlphaTween;
		private Tween _fadeTween;
		private readonly Dictionary<SpriteRenderer, float> _spriteRendererAlphaDict = new Dictionary<SpriteRenderer, float>();
		private LiveMusicData.CollaborationModeState _collaborationModeState;
		private bool _isAuto;
		private bool _isPlayedHaptic;
		private int _life;
		private int _lastScreenWidth;
		private int _lastScreenHeight;
		private float _effectCameraBaseFieldOfView = -1f;

		private static readonly int TapEffectViewMatrixId = Shader.PropertyToID("_TapEffectViewMatrix");
		private static readonly int TapEffectProjectionMatrixId = Shader.PropertyToID("_TapEffectProjectionMatrix");

		public ConsecutiveAutoLiveView ConsecutiveAutoLiveView => _consecutiveAutoLiveView;

		public ScoreView ScoreView => _scoreView;

		public override void Setup(BaseLiveController controller)
		{
			base.Setup(controller);
			_baseController = controller;
			_collaborationModeState = controller?.BootData?.MusicData?.CollaboModeState ?? LiveMusicData.CollaborationModeState.Off;
			_life = LiveConfig.Life;
			_isAuto = controller?.BootData?.IsAuto == true;
			SetupCameras();
			CriWare.CriAtomAudioRuntime.ReleaseFallbackAudioListenerIfExternalExists();
			SetupBackgroundMaterial(controller);
			SetupDeadMask();
			ResolveChildViews();
			_lifeView?.Setup(OnPause);
			_scoreView?.Setup(controller?.BootData?.MusicData);
			_comboView?.Setup(controller?.Settings?.UseAllPerfectEffect ?? false);
			_countdownView?.Setup();
			_screenEffectView?.Setup();
			_cutinManager?.Setup(controller?.BootData);
			_musicInfo?.Setup(controller?.BootData);
			_noteShowRateView?.Setup(controller?.Settings);
			_consecutiveAutoLiveView?.Setup(OnConsecutiveAutoLivePause, OnConsecutiveAutoLiveResume, OnConsecutiveAutoLiveFinish, _frontViewCamera);
			_laneView?.Setup(controller?.Settings);
			SetupNotesView();
			SetupTapEffect();
			RefreshScreenDependentLayout(force: true);
			CacheSpriteRendererAlpha();
			UpdateSpriteAlpha(0f);
			if (_autoLabel != null)
			{
				_autoLabel.SetActive(false);
			}
			_liveResultView?.Setup(ShouldPlayVoiceSE());
		}

		public override void OnLoad()
		{
			SetupCameras();
			UpdateSpriteAlpha(0f);
			_cutinManager?.Load();
		}

		public override void OnUnload()
		{
			_liveResultView?.Unload();
			_cutinManager?.Dispose();
			_tapEffect?.Dispose();
		}

		public override void MusicStart(float musicTime)
		{
			bool skipMusicInfo = _baseController?.BootData?.IsCustomMusicScore == true &&
				_baseController?.BootData?.LiveSettingData?.SkipsCustomMusicScoreMusicInfo == true;

			if (skipMusicInfo)
			{
				// Skip mode: immediately disable MusicInfoView and set brightness
				MusicInfoView musicInfoView = ResolveMusicInfo();
				if (musicInfoView != null)
				{
					musicInfoView.gameObject.SetActive(false);
				}
				SetBackgroundBrightness(GetTargetBackgroundBrightness());
				return;
			}

			if (_baseController?.BootData?.MusicData?.PlayStartEffectEnabled == true)
			{
				UpdateSpriteAlpha(0f);
				_musicStartTween?.Kill();
				_musicStartTween = DOVirtual.DelayedCall(0.1f, () =>
				{
					ResolveMusicInfo()?.Play(1f, false, 0f, null, null);
					FadeInBackground(() => PlayStartVoice(1f));
				}, true);
				return;
			}

			ResolveMusicInfo()?.Play(1f, false, 0f, null, null);
			SetBackgroundBrightness(GetTargetBackgroundBrightness());
		}

		public override void RhythmGameStart()
		{
			_lifeView?.CalculateButtonBounds(_frontViewCamera);
			_consecutiveAutoLiveView?.CalculateButtonBounds(_frontViewCamera);
			FadeIn();
		}

		public override void OnUpdate(float musicTime)
		{
			_isPlayedHaptic = false;
			float viewTime = musicTime - (_baseController?.BootData?.MusicData?.Music?.fillerSec ?? 0f);
			_notesViewManager?.OnUpdate(viewTime);
			_longTapEffectView?.Excute(viewTime);
			UpdateLaneMask();
		}

		private void Update()
		{
			RefreshScreenDependentLayout(force: false);
		}

		public override void CreateNotePool(Dictionary<(NoteCategory, NoteType), int> notePoolCount)
		{
			_notesViewManager?.CreateNotePool(_baseController, notePoolCount);
		}

		public override void SpawnNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			_notesViewManager?.SpawnNote(note);
			if (note is LongNote)
			{
				_longTapEffectView?.Add(note);
			}
		}

		public override void UnspawnNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			_notesViewManager?.UnspawnNote(note);
			if (note is LongNote)
			{
				_longTapEffectView?.Remove(note);
			}
		}

		public override void JudgmentNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			// Decoration notes: play tap effect and SE, but do not show judgment text
			if (note.IsDecoration)
			{
				Effect(note);
				return;
			}

			// Normal notes: play effect and show judgment text. Several notes can land on the same frame;
			// only the worst of them owns the popup, and the fast/late caption follows that same note.
			Effect(note);
			ShowJudgmentText(note);
		}

		/// <summary>
		/// The same popup without the effect. An autoplay session grades the interior markers of a bar
		/// through stretches where nothing else is graded (the friction section of DEINOS); those
		/// markers are not drawn, so the popup was the one thing that stopped refreshing.
		/// </summary>
		public override void JudgmentText(NoteBase note)
		{
			if (note == null || note.IsDecoration)
			{
				return;
			}

			ShowJudgmentText(note);
		}

		private void ShowJudgmentText(NoteBase note)
		{
			if (_judgmentView == null || _judgmentView.Excute(note.JudgeInfo))
			{
				_judgmentDescriptionView?.Excute(note.JudgeInfo);
			}
		}

		public override void Unpicked(int lane, ref LiveTouch touch)
		{
			_tapEffect?.Unpicked(lane, ref touch);
		}

		public override void UpdateCombo(LiveScore score)
		{
			_cutinManager?.UpdateCombo(score);
			_comboView?.Excute(score);
		}

		public override void SetupScore(LiveScore score)
		{
			_scoreView?.SetupScore(score);
		}

		public override void UpdateScore(LiveScore score)
		{
			UpdateScore(ref score, 0);
		}

		public override void UpdateScore(ref LiveScore score, int addScore)
		{
			_scoreView?.UpdateScore(ref score, addScore);
		}

		public override void UpdateLife(LiveScore score)
		{
			_lifeView?.Excute(score.life);
			_life = score.life;
		}

		public override void Result(int result)
		{
			_liveResultView?.Execute((LiveResultAnimationType)result);
		}

		public override void Pause()
		{
			_countdownView?.StopCountdown();
		}

		public override void Resume()
		{
			_countdownView?.StartCountdown();
		}

		public override void Retry()
		{
			_musicStartTween?.Kill();
			_backgroundTween?.Kill();
			_startVoiceTween?.Kill();
			_spriteAlphaTween?.Kill();
			_fadeTween?.Kill();
			_comboView?.Clear();
			_scoreView?.Clear();
			_lifeView?.Clear();
			_cutinManager?.Clear();
			_notesViewManager?.Clear();
			_tapEffect?.Clear();
			_longTapEffectView?.Clear();
			_life = LiveConfig.Life;
			if (_autoLabel != null)
			{
				_autoLabel.SetActive(false);
			}
			UpdateSpriteAlpha(0f);
		}

		public override void Finish()
		{
			FadeOut(1f);
		}

		public override void Finish(float duration)
		{
			FadeOut(duration);
		}

		public override void Countdown()
		{
			_countdownView?.StartCountdown();
		}

		public void UpdateSkill(SkillData skillData, float time, LiveScore score, bool isEncore)
		{
			_cutinManager?.UpdateSkill(skillData, time, score, isEncore);
		}

		private void SetupBackgroundMaterial(BaseLiveController controller)
		{
			ResolveBackgroundRenderer();
			if (_backgroundRenderer == null)
			{
				return;
			}

			UpdateBackgroundScale();

			if (_backgroundMaterial != null)
			{
				_backgroundMaterial.DOKill();
				Destroy(_backgroundMaterial);
			}

			Material sourceMaterial = _backgroundRenderer.sharedMaterial ?? _backgroundRenderer.material;
			if (sourceMaterial != null)
			{
				_backgroundMaterial = new Material(sourceMaterial);
				_backgroundRenderer.material = _backgroundMaterial;
				_backgroundMaterial.mainTexture = controller?.BackgroundTexture;
				SetBackgroundBrightness(0f);
			}

			_backgroundRenderer.enabled = true;
		}

		private void UpdateBackgroundScale()
		{
			if (_backgroundRenderer == null || _cameraSizeUpdater == null)
			{
				return;
			}

			float height = _cameraSizeUpdater.OrthographicSize * 2f;
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : ScreenConfig.Aspect;
			_backgroundRenderer.transform.localScale = new Vector3(height * aspect, height, 1f);
		}

		private void SetupDeadMask()
		{
			if (_deadMask == null || _cameraSizeUpdater == null)
			{
				return;
			}

			float orthographicSize = _cameraSizeUpdater.OrthographicSize;
			float height = orthographicSize * 2f;
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
			_deadMask.size = new Vector2(height * aspect, height);
		}

		private void RefreshScreenDependentLayout(bool force)
		{
			int currentWidth = Screen.width;
			int currentHeight = Screen.height;
			if (currentWidth <= 0 || currentHeight <= 0)
			{
				return;
			}
			if (!force && currentWidth == _lastScreenWidth && currentHeight == _lastScreenHeight)
			{
				return;
			}

			_lastScreenWidth = currentWidth;
			_lastScreenHeight = currentHeight;
			// OpenSekai: desktop windows can be resized during live; keep camera/world-space effects aligned.
			ResolveCameraReferences();
			_cameraSizeUpdater?.ForceUpdate();
			UpdateBackgroundScale();
			SetupDeadMask();
			RefreshTapEffectProjection();
			_screenEffectView?.RefreshScreenSize();
			_lifeView?.CalculateButtonBounds(_frontViewCamera);
			_consecutiveAutoLiveView?.CalculateButtonBounds(_frontViewCamera);
		}

		private void UpdateLaneMask()
		{
			if (_deadMask == null)
			{
				return;
			}

			Color color = _deadMask.color;
			float targetAlpha = _life == 0 ? 1f : 0f;
			color.a += (targetAlpha - color.a) * 0.1f;
			_deadMask.color = color;
		}

		private void ResolveBackgroundRenderer()
		{
			if (_backgroundRenderer != null)
			{
				return;
			}

			Transform backgroundTransform = transform.Find("Background");
			_backgroundRenderer = backgroundTransform != null ? backgroundTransform.GetComponent<Renderer>() : null;
		}

		private void FadeInBackground(TweenCallback onComplete = null)
		{
			float brightness = GetTargetBackgroundBrightness();
			if (_backgroundMaterial == null)
			{
				onComplete?.Invoke();
				return;
			}

			_backgroundTween?.Kill();
			_backgroundTween = _backgroundMaterial.DOColor(new Color(brightness, brightness, brightness, 1f), 2f).OnComplete(onComplete);
		}

		private float GetTargetBackgroundBrightness()
		{
			return Mathf.Clamp01(_baseController?.Settings?.Brightness ?? 1f);
		}

		private void SetBackgroundBrightness(float brightness)
		{
			if (_backgroundMaterial == null)
			{
				return;
			}

			float clamped = Mathf.Clamp01(brightness);
			_backgroundMaterial.color = new Color(clamped, clamped, clamped, 1f);
		}

		private void SetFadeAlpha(float alpha)
		{
			if (_fade == null)
			{
				return;
			}

			Color color = _fade.color;
			float clamped = Mathf.Clamp01(alpha);
			color.r = 0f;
			color.g = 0f;
			color.b = 0f;
			color.a = clamped;
			_fade.color = color;
		}

		private void FadeIn()
		{
			_fadeTween?.Kill();
			if (_fade != null)
			{
				_fade.enabled = false;
				SetFadeAlpha(0f);
			}

			if (_baseController?.BootData?.MusicData?.PlayStartEffectEnabled == true)
			{
				_spriteAlphaTween?.Kill();
				_spriteAlphaTween = DOVirtual.Float(0f, 1f, 2f, UpdateSpriteAlpha)
					.OnComplete(UpdateAutoLabel);
				return;
			}

			UpdateSpriteAlpha(1f);
			UpdateAutoLabel();
		}

		private void FadeOut(float duration)
		{
			if (_autoLabel != null)
			{
				_autoLabel.SetActive(false);
			}
			if (_fade == null)
			{
				return;
			}

			_fade.enabled = true;
			_fadeTween?.Kill();
			_fadeTween = DOVirtual.Float(0f, 1f, Mathf.Max(0f, duration), SetFadeAlpha);
		}

		private void UpdateSpriteAlpha(float alpha)
		{
			if (_spriteRendererAlphaDict.Count == 0)
			{
				return;
			}

			foreach (KeyValuePair<SpriteRenderer, float> pair in _spriteRendererAlphaDict)
			{
				SpriteRenderer renderer = pair.Key;
				if (renderer == null)
				{
					continue;
				}

				Color color = renderer.color;
				color.a = pair.Value * alpha;
				renderer.color = color;
			}
		}

		private void CacheSpriteRendererAlpha()
		{
			_spriteRendererAlphaDict.Clear();
			if (_liveRoot == null)
			{
				return;
			}

			SpriteRenderer[] renderers = _liveRoot.GetComponentsInChildren<SpriteRenderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				SpriteRenderer renderer = renderers[i];
				if (renderer != null && !_spriteRendererAlphaDict.ContainsKey(renderer))
				{
					_spriteRendererAlphaDict.Add(renderer, renderer.color.a);
				}
			}
		}

		private void UpdateAutoLabel()
		{
			if (_autoLabel != null)
			{
				bool useFakePerfect = LiveSettingsStore.Shared.Load()?.UsesAutoFakePerfectMode ?? false;
				_autoLabel.SetActive(_isAuto && !useFakePerfect);
			}
		}

		private void SetupCameras()
		{
			ResolveCameraReferences();
			if (_frontViewCamera == null || _effectCamera == null)
			{
				return;
			}

			var frontCameraData = GetOrAddCameraData(_frontViewCamera);
			var effectCameraData = GetOrAddCameraData(_effectCamera);
			if (frontCameraData == null || effectCameraData == null)
			{
				return;
			}

			frontCameraData.renderType = CameraRenderType.Base;
			effectCameraData.renderType = CameraRenderType.Overlay;

			// The prefab is built for the legacy multi-camera path. In URP,
			// leaving this camera as a standalone Base camera makes its transparent
			// blue clear color overwrite the screen, so it has to render as an overlay
			// stacked after FrontCamera.
			_effectCamera.clearFlags = CameraClearFlags.Depth;
			if (!frontCameraData.cameraStack.Contains(_effectCamera))
			{
				frontCameraData.cameraStack.Add(_effectCamera);
			}

			_frontViewCamera.GetSekaiAdditionalCameraData()?.Setup(0, 2);
			_effectCamera.GetSekaiAdditionalCameraData()?.Setup(1, 2);
			if (_baseController != null)
			{
				_baseController.BaseCamera = _frontViewCamera;
			}
		}

		private void SetupNotesView()
		{
			Transform parent = _liveRoot != null ? _liveRoot : transform;
			_notesViewManager = new NotesViewManager();
			_notesViewManager.Setup(parent);
		}

		private void SetupTapEffect()
		{
			Transform parent = _effectRoot != null ? _effectRoot : transform;
			if (_tapEffect == null)
			{
				_tapEffect = parent.GetComponent<TapEffectView>();
				if (_tapEffect == null)
				{
					_tapEffect = parent.gameObject.AddComponent<TapEffectView>();
				}
			}
			_tapEffect.Setup(_baseController);

			if (_longTapEffectView == null)
			{
				GameObject longTapObject = new GameObject("LongTapEffectView", typeof(LongTapEffectView));
				longTapObject.transform.SetParent(parent, false);
				_longTapEffectView = longTapObject.GetComponent<LongTapEffectView>();
			}
			_longTapEffectView.Setup();

			parent.position = transform.position;
			if (_effectCamera != null)
			{
				RefreshTapEffectProjection();
				_effectCamera.enabled = false;
			}
		}

		private MusicInfoView ResolveMusicInfo()
		{
			if (_resolvedMusicInfo != null)
			{
				return _resolvedMusicInfo;
			}

			_resolvedMusicInfo = _musicInfo as MusicInfoView;
			if (_resolvedMusicInfo == null)
			{
				_resolvedMusicInfo = GetComponentInChildren<MusicInfoView>(true);
			}
			return _resolvedMusicInfo;
		}

		private void ResolveChildViews()
		{
			_laneView ??= GetComponentInChildren<LaneView>(true);
			_resolvedMusicInfo = _musicInfo != null ? _musicInfo : GetComponentInChildren<MusicInfoView>(true);
			_musicInfo = _resolvedMusicInfo;
			_judgmentView ??= GetComponentInChildren<JudgmentView>(true);
			_judgmentDescriptionView ??= GetComponentInChildren<JudgmentDescriptionView>(true);
			_comboView ??= GetComponentInChildren<ComboView>(true);
			_scoreView ??= GetComponentInChildren<ScoreView>(true);
			_lifeView ??= GetComponentInChildren<LifeView>(true);
			_liveResultView ??= GetComponentInChildren<LiveResultView>(true);
			_cutinManager ??= GetComponentInChildren<CutinManager>(true);
			_countdownView ??= GetComponentInChildren<CountdownView>(true);
			_screenEffectView ??= GetComponentInChildren<ScreenEffectView>(true);
			_consecutiveAutoLiveView ??= GetComponentInChildren<ConsecutiveAutoLiveView>(true);
			_noteShowRateView ??= GetComponentInChildren<NoteShowRateView>(true);
		}

		private void PlayStartVoice(float durationScale)
		{
			if (_collaborationModeState != LiveMusicData.CollaborationModeState.On)
			{
				return;
			}

			_startVoiceTween?.Kill();
			_startVoiceTween = DOVirtual.DelayedCall(Mathf.Max(0f, durationScale) * 0.1f, () =>
			{
				SoundManager.Instance.PlayIngameVoiceOneShot(LiveSoundDefine.SE_LIVE_START);
			}, true);
		}

		private void ResolveCameraReferences()
		{
			if (_frontViewCamera == null)
			{
				var frontCameraTransform = transform.Find("FrontCamera");
				_frontViewCamera = frontCameraTransform != null ? frontCameraTransform.GetComponent<Camera>() : null;
			}

			if (_effectCamera == null)
			{
				var effectCameraTransform = transform.Find("EffectCameraRoot/Camera");
				_effectCamera = effectCameraTransform != null ? effectCameraTransform.GetComponent<Camera>() : null;
			}

			if (_effectRoot == null)
			{
				_effectRoot = transform.Find("EffectCameraRoot");
			}

			if (_cameraSizeUpdater == null)
			{
				_cameraSizeUpdater = GetComponent<CameraSizeUpdater>();
			}

			if (_effectCamera != null && _effectCameraBaseFieldOfView < 0f)
			{
				_effectCameraBaseFieldOfView = _effectCamera.fieldOfView;
			}
		}

		private void RefreshTapEffectProjection()
		{
			if (_effectCamera == null)
			{
				return;
			}

			// OpenSekai: the effect camera is disabled after setup, so keep its
			// projection in sync when desktop/mobile resolution changes at runtime.
			if (Screen.height > 0)
			{
				_effectCamera.aspect = (float)Screen.width / Screen.height;
			}
			if (_effectCameraBaseFieldOfView > 0f)
			{
				_effectCamera.fieldOfView = SekaiCameraAspect.CalculateVerticalFov(_effectCameraBaseFieldOfView);
			}
			_effectCamera.ResetProjectionMatrix();
			Shader.SetGlobalMatrix(TapEffectViewMatrixId, _effectCamera.worldToCameraMatrix);
			Shader.SetGlobalMatrix(TapEffectProjectionMatrixId, GL.GetGPUProjectionMatrix(_effectCamera.projectionMatrix, false));
		}

		private static UniversalAdditionalCameraData GetOrAddCameraData(Camera camera)
		{
			if (camera == null)
			{
				return null;
			}

			var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
			if (cameraData == null)
			{
				cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
			}

			return cameraData;
		}

		private void OnPause()
		{
			_countdownView?.StopCountdown();
			_baseController?.Pause();
		}

		private void OnConsecutiveAutoLivePause()
		{
			if (_baseController is SoloLiveController soloLiveController && soloLiveController.IsPlayableState)
			{
				soloLiveController.PauseLive();
			}
		}

		private void OnConsecutiveAutoLiveResume()
		{
			if (_baseController is SoloLiveController soloLiveController)
			{
				soloLiveController.ResumeNoCountDown();
			}
			else
			{
				_baseController?.Resume();
			}
		}

		private void OnConsecutiveAutoLiveFinish()
		{
			_consecutiveAutoLiveView?.Hide();
			if (_baseController is SoloLiveController soloLiveController)
			{
				soloLiveController.ShowConsecutiveAutoLiveRetireDialog();
			}
			else
			{
				_baseController?.CallPreExit();
			}
		}

		private void Effect(INote note)
		{
			_tapEffect?.Excute(note, CheckPlayedHaptic);
			_screenEffectView?.Excute(note);
		}

		private bool CheckPlayedHaptic()
		{
			if (_isPlayedHaptic)
			{
				return true;
			}

			_isPlayedHaptic = true;
			return false;
		}

		private bool ShouldPlayVoiceSE()
		{
			return _collaborationModeState == LiveMusicData.CollaborationModeState.On && _baseController?.BootData?.IsAuto != true;
		}

		private void OnDestroy()
		{
			_musicStartTween?.Kill();
			_backgroundTween?.Kill();
			_startVoiceTween?.Kill();
			_spriteAlphaTween?.Kill();
			_fadeTween?.Kill();
			if (_backgroundMaterial != null)
			{
				_backgroundMaterial.DOKill();
				Destroy(_backgroundMaterial);
				_backgroundMaterial = null;
			}
		}
	}
}

