using OpenSekai.Shared.UI;
using OpenSekai.Shared.Settings;
using System;
using System.Collections.Generic;
using Sekai.Core;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LitMotion;
using CriWare;
using Sekai;

namespace Sekai.Live
{
	public class LiveFrontView : LiveViewBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_liveRoot")]
		[SerializeField]
		private Transform liveRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_fade")]
		[SerializeField]
		private SpriteRenderer fade;

		[UnityEngine.Serialization.FormerlySerializedAs("_deadMask")]
		[SerializeField]
		private SpriteRenderer deadMask;

		[UnityEngine.Serialization.FormerlySerializedAs("_laneView")]
		[SerializeField]
		private LaneView laneView;

		[UnityEngine.Serialization.FormerlySerializedAs("_musicInfo")]
		[SerializeField]
		private MusicInfoView musicInfo;

		[UnityEngine.Serialization.FormerlySerializedAs("_backgroundRenderer")]
		[SerializeField]
		private Renderer backgroundRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_judgmentView")]
		[SerializeField]
		private JudgmentView judgmentView;

		[UnityEngine.Serialization.FormerlySerializedAs("_judgmentDescriptionView")]
		[SerializeField]
		private JudgmentDescriptionView judgmentDescriptionView;

		[UnityEngine.Serialization.FormerlySerializedAs("_comboView")]
		[SerializeField]
		private ComboView comboView;

		[UnityEngine.Serialization.FormerlySerializedAs("_scoreView")]
		[SerializeField]
		private ScoreView scoreView;

		[UnityEngine.Serialization.FormerlySerializedAs("_lifeView")]
		[SerializeField]
		private LifeView lifeView;

		[UnityEngine.Serialization.FormerlySerializedAs("_liveResultView")]
		[SerializeField]
		private LiveResultView liveResultView;

		[UnityEngine.Serialization.FormerlySerializedAs("_cutinManager")]
		[SerializeField]
		private CutinManager cutinManager;

		[UnityEngine.Serialization.FormerlySerializedAs("_countdownView")]
		[SerializeField]
		private CountdownView countdownView;

		[UnityEngine.Serialization.FormerlySerializedAs("_screenEffectView")]
		[SerializeField]
		private ScreenEffectView screenEffectView;

		[UnityEngine.Serialization.FormerlySerializedAs("_effectRoot")]
		[SerializeField]
		private Transform effectRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_effectCamera")]
		[SerializeField]
		private Camera effectCamera;

		[UnityEngine.Serialization.FormerlySerializedAs("_cameraSizeUpdater")]
		[SerializeField]
		private CameraSizeUpdater cameraSizeUpdater;

		[UnityEngine.Serialization.FormerlySerializedAs("_frontViewCamera")]
		[SerializeField]
		private Camera frontViewCamera;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoLabel")]
		[SerializeField]
		private GameObject autoLabel;

		[UnityEngine.Serialization.FormerlySerializedAs("_consecutiveAutoLiveView")]
		[SerializeField]
		private ConsecutiveAutoLiveView consecutiveAutoLiveView;

		[UnityEngine.Serialization.FormerlySerializedAs("_noteShowRateView")]
		[SerializeField]
		private NoteShowRateView noteShowRateView;

		private Material _backgroundMaterial;

		private BaseLiveController _baseController;
		private NotesViewManager _notesViewManager;
		private LiveHitEffectSkin _hitFxSkin;
		private TapEffectView _tapEffect;
		private LongTapEffectView _longTapEffectView;
		private MusicInfoView _resolvedMusicInfo;
		private MotionHandle _musicStartMotion;
		private MotionHandle _backgroundMotion;
		private MotionHandle _startVoiceMotion;
		private MotionHandle _spriteAlphaMotion;
		private MotionHandle _fadeMotion;
		private readonly Dictionary<SpriteRenderer, float> _spriteRendererAlphaDict = new Dictionary<SpriteRenderer, float>();
		private LiveMusicData.CollaborationModeState _collaborationModeState;
		private bool _isAuto;
		private bool _isPlayedHaptic;
		private int _life;
		private int _lastScreenWidth;
		private int _lastScreenHeight;
		private float _effectCameraBaseFieldOfView = -1f;

		private static readonly int _tapEffectViewMatrixId = Shader.PropertyToID("_TapEffectViewMatrix");
		private static readonly int _tapEffectProjectionMatrixId = Shader.PropertyToID("_TapEffectProjectionMatrix");

		public ConsecutiveAutoLiveView ConsecutiveAutoLiveView => consecutiveAutoLiveView;

		public ScoreView ScoreView => scoreView;

		private readonly HudViewBindings _hudBindings = new HudViewBindings();

		internal void SetupHud(LiveHudSkin skin, LiveMusicData musicData)
		{
			_hudBindings.Setup(transform, skin);
			lifeView?.Setup(OnPause, (_baseController?.BootData as FreeLiveBootData)?.DanSong?.InitialLife);
			scoreView?.Setup(musicData, skin);
		}

		public override void Setup(BaseLiveController controller)
		{
			base.Setup(controller);
			_baseController = controller;
			_collaborationModeState = controller?.BootData?.MusicData?.CollaboModeState ?? LiveMusicData.CollaborationModeState.Off;
			_life = LiveConfig.Life;
			_isAuto = controller?.BootData?.IsAuto == true;
			SetupCameras();
			SoundManager.NotifySceneAudioListenersChanged();
			SetupBackgroundMaterial(controller);
			SetupDeadMask();
			ResolveChildViews();
			var boot = controller?.BootData as FreeLiveBootData;
			SetupHud(boot?.HudSkin, controller?.BootData?.MusicData);
			comboView?.Setup(controller?.Settings?.UseAllPerfectEffect ?? false, boot?.ComboSkin);
			judgmentView?.Setup(boot?.JudgmentSkin, controller?.Settings?.UsesAutoFakePerfectMode ?? false);
			judgmentDescriptionView?.Setup(boot?.JudgmentSkin, controller?.Settings?.IsFastLateFlick ?? true);
			countdownView?.Setup();
			_hitFxSkin = boot?.HitFxSkin;
			screenEffectView?.Setup(_hitFxSkin);
			cutinManager?.Setup(controller?.BootData);
			musicInfo?.Setup(controller?.BootData);
			noteShowRateView?.Setup(controller?.Settings);
			consecutiveAutoLiveView?.Setup(OnConsecutiveAutoLivePause, OnConsecutiveAutoLiveResume, OnConsecutiveAutoLiveFinish, frontViewCamera);
			laneView?.Setup(controller?.Settings, boot?.LaneSkin);
			SetupNotesView();
			SetupTapEffect();
			RefreshScreenDependentLayout(force: true);
			CacheSpriteRendererAlpha();
			UpdateSpriteAlpha(0f);
			if (autoLabel != null)
			{
				autoLabel.SetActive(false);
			}
			liveResultView?.Setup(ShouldPlayVoiceSE(), boot?.ResultFxSkin);
		}

		public override void OnLoad()
		{
			SetupCameras();
			UpdateSpriteAlpha(0f);
			cutinManager?.Load();
		}

		public override void OnUnload()
		{
			_hitFxSkin?.ResetPlayback();
			_longTapEffectView?.Clear();
			CancelMotions();
			liveResultView?.Unload();
			cutinManager?.Dispose();
			_tapEffect?.Dispose();
		}

		public override void MusicStart(float musicTime)
		{
			bool skipMusicInfo = _baseController?.BootData?.IsCustomMusicScore == true &&
				_baseController?.BootData?.LiveSettingData?.SkipsCustomMusicScoreMusicInfo == true;

			_musicStartMotion.TryCancel();
			_introFinished = false;
			if (skipMusicInfo)
			{
				_introFinished = true;
				// Skip mode: immediately disable MusicInfoView and set brightness
				LiveEntryCurtain.Release();
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
				_musicStartMotion = LiveMotion.Delay(MUSIC_INFO_START_DELAY_SECONDS, () =>
				{
					MusicInfoView info = ResolveMusicInfo();
					// The library's loading screen fades out as the page fades in over it.
					LiveEntryCurtain.Release();
					if (info == null)
					{
						_introFinished = true;
					}
					else
					{
						// The callback fires once the page has faded out completely.
						info.Play(1f, false, 0f, () => _introFinished = true, null);
					}
					FadeInBackground(() => PlayStartVoice(1f));
				});
				return;
			}

			// Without the start effect the page is switched off straight away and never calls back.
			LiveEntryCurtain.Release();
			ResolveMusicInfo()?.Play(1f, false, 0f, null, null);
			_introFinished = true;
			SetBackgroundBrightness(GetTargetBackgroundBrightness());
		}

		private bool _introFinished = true;

		public override bool IsIntroFinished => _introFinished;

		/// <summary>The 0.1 s before the page is asked to play, then the page itself.</summary>
		private const float MUSIC_INFO_START_DELAY_SECONDS = 0.1f;

		public override float IntroDurationSeconds
		{
			get
			{
				bool skipMusicInfo = _baseController?.BootData?.IsCustomMusicScore == true &&
					_baseController?.BootData?.LiveSettingData?.SkipsCustomMusicScoreMusicInfo == true;
				if (skipMusicInfo || _baseController?.BootData?.MusicData?.PlayStartEffectEnabled != true)
				{
					return 0f;
				}

				MusicInfoView info = ResolveMusicInfo();
				return info != null ? MUSIC_INFO_START_DELAY_SECONDS + info.PlayDurationSeconds : 0f;
			}
		}

		/// <summary>The skip button takes the pause button's place until it is pressed or hidden.</summary>
		public override void ShowSkip(Action onSkip)
		{
			lifeView?.ShowSkip(onSkip);
		}

		public override void HideSkip()
		{
			lifeView?.HideSkip();
		}

		public override void RhythmGameStart()
		{
			lifeView?.CalculateButtonBounds(frontViewCamera);
			consecutiveAutoLiveView?.CalculateButtonBounds(frontViewCamera);
			FadeIn();
		}

		public override void OnUpdate(float musicTime)
		{
			_isPlayedHaptic = false;
			float viewTime = (float)(_baseController?.BootData?.MusicData?.AudioToChartTime(musicTime) ?? musicTime);
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
			if (note is LongNote && note.State != NoteState.Done)
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

		public override void NativeHitSound(NoteBase note, string cue) => _tapEffect?.PlayNativeCue(note, cue);

		public override void JudgmentNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			// A note without judgement shows nothing at all. Publication already stops it; this guards
			// direct callers.
			if (!note.HasJudgment)
			{
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
			if (note == null || !note.HasJudgment)
			{
				return;
			}

			ShowJudgmentText(note);
		}

		private void ShowJudgmentText(NoteBase note)
		{
			if (judgmentView == null || judgmentView.Excute(note.JudgeInfo))
			{
				judgmentDescriptionView?.Excute(note.JudgeInfo);
			}
		}

		public override void Unpicked(int lane, ref LiveTouch touch)
		{
			_tapEffect?.Unpicked(lane, ref touch);
		}

		public override void UpdateCombo(LiveScore score)
		{
			cutinManager?.UpdateCombo(score);
			comboView?.Excute(score);
		}

		public override void SetupScore(LiveScore score)
		{
			scoreView?.SetupScore(score);
			UpdateLife(score);
		}

		public override void UpdateScore(LiveScore score)
		{
			UpdateScore(ref score, 0);
		}

		public override void UpdateScore(ref LiveScore score, int addScore)
		{
			scoreView?.UpdateScore(ref score, addScore);
		}

		public override void UpdateLife(LiveScore score)
		{
			lifeView?.Excute(score.life);
			_life = score.life;
		}

		public override void Result(int result)
		{
			liveResultView?.Execute((LiveResultAnimationType)result);
		}

		public override void Pause()
		{
			_hitFxSkin?.SetPaused(true);
			_longTapEffectView?.Silence();
			countdownView?.StopCountdown();
		}

		public override void Resume()
		{
			_hitFxSkin?.SetPaused(false);
		}

		public override void Retry()
		{
			_hitFxSkin?.ResetPlayback();
			CancelMotions();
			comboView?.Clear();
			scoreView?.Clear();
			lifeView?.Clear();
			cutinManager?.Clear();
			_notesViewManager?.Clear();
			_tapEffect?.Clear();
			_longTapEffectView?.Clear();
			_life = LiveConfig.Life;
			if (autoLabel != null)
			{
				autoLabel.SetActive(false);
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
			countdownView?.StartCountdown();
		}

		public void UpdateSkill(SkillData skillData, float time, LiveScore score, bool isEncore)
		{
			cutinManager?.UpdateSkill(skillData, time, score, isEncore);
		}

		private void SetupBackgroundMaterial(BaseLiveController controller)
		{
			_backgroundMotion.TryCancel();
			ResolveBackgroundRenderer();
			if (backgroundRenderer == null)
			{
				return;
			}

			UpdateBackgroundScale();

			if (_backgroundMaterial != null)
			{
				Destroy(_backgroundMaterial);
			}

			Material sourceMaterial = backgroundRenderer.sharedMaterial ?? backgroundRenderer.material;
			if (sourceMaterial != null)
			{
				_backgroundMaterial = new Material(sourceMaterial);
				backgroundRenderer.material = _backgroundMaterial;
				_backgroundMaterial.mainTexture = controller?.BackgroundTexture;
				SetBackgroundBrightness(0f);
			}

			backgroundRenderer.enabled = true;
		}

		private void UpdateBackgroundScale()
		{
			if (backgroundRenderer == null || cameraSizeUpdater == null)
			{
				return;
			}

			float height = cameraSizeUpdater.OrthographicSize * 2f;
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : ScreenConfig.Aspect;
			backgroundRenderer.transform.localScale = new Vector3(height * aspect, height, 1f);
		}

		private void SetupDeadMask()
		{
			if (deadMask == null || cameraSizeUpdater == null)
			{
				return;
			}

			float orthographicSize = cameraSizeUpdater.OrthographicSize;
			float height = orthographicSize * 2f;
			float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
			deadMask.size = new Vector2(height * aspect, height);
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
			cameraSizeUpdater?.ForceUpdate();
			UpdateBackgroundScale();
			SetupDeadMask();
			RefreshTapEffectProjection();
			screenEffectView?.RefreshScreenSize();
			lifeView?.CalculateButtonBounds(frontViewCamera);
			consecutiveAutoLiveView?.CalculateButtonBounds(frontViewCamera);
		}

		private void UpdateLaneMask()
		{
			if (deadMask == null)
			{
				return;
			}

			Color color = deadMask.color;
			float targetAlpha = _life == 0 ? 1f : 0f;
			color.a += (targetAlpha - color.a) * 0.1f;
			deadMask.color = color;
		}

		private void ResolveBackgroundRenderer()
		{
			if (backgroundRenderer != null)
			{
				return;
			}

			Transform backgroundTransform = transform.Find("Background");
			backgroundRenderer = backgroundTransform != null ? backgroundTransform.GetComponent<Renderer>() : null;
		}

		private void FadeInBackground(Action onComplete)
		{
			float brightness = GetTargetBackgroundBrightness();
			if (_backgroundMaterial == null)
			{
				onComplete?.Invoke();
				return;
			}

			_backgroundMotion.TryCancel();
			Color from = _backgroundMaterial.color;
			Color to = new Color(brightness, brightness, brightness, 1f);
			_backgroundMotion = LMotion.Create(0f, 1f, 2f)
				.WithEase(Ease.OutQuad)
				.WithScheduler(LiveMotion.Scheduler())
				.WithOnComplete(onComplete)
				.Bind(progress => _backgroundMaterial.color = Color.LerpUnclamped(from, to, progress));
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
			if (fade == null)
			{
				return;
			}

			Color color = fade.color;
			float clamped = Mathf.Clamp01(alpha);
			color.r = 0f;
			color.g = 0f;
			color.b = 0f;
			color.a = clamped;
			fade.color = color;
		}

		private void FadeIn()
		{
			_fadeMotion.TryCancel();
			if (fade != null)
			{
				fade.enabled = false;
				SetFadeAlpha(0f);
			}

			if (_baseController?.BootData?.MusicData?.PlayStartEffectEnabled == true)
			{
				_spriteAlphaMotion.TryCancel();
				_spriteAlphaMotion = LMotion.Create(0f, 1f, 2f)
					.WithEase(Ease.OutQuad)
					.WithScheduler(LiveMotion.Scheduler())
					.WithOnComplete(UpdateAutoLabel)
					.Bind(UpdateSpriteAlpha);
				return;
			}

			UpdateSpriteAlpha(1f);
			UpdateAutoLabel();
		}

		private void FadeOut(float duration)
		{
			_hitFxSkin?.ResetPlayback();
			if (autoLabel != null)
			{
				autoLabel.SetActive(false);
			}
			if (fade == null)
			{
				return;
			}

			fade.enabled = true;
			_fadeMotion.TryCancel();
			_fadeMotion = LMotion.Create(0f, 1f, Mathf.Max(0f, duration))
				.WithEase(Ease.OutQuad)
				.WithScheduler(LiveMotion.Scheduler())
				.Bind(SetFadeAlpha);
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
			if (liveRoot == null)
			{
				return;
			}

			SpriteRenderer[] renderers = liveRoot.GetComponentsInChildren<SpriteRenderer>(true);
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
			if (autoLabel != null)
			{
				bool useFakePerfect = _baseController?.Settings?.UsesAutoFakePerfectMode ?? false;
				autoLabel.SetActive(_isAuto && !useFakePerfect);
			}
		}

		private void SetupCameras()
		{
			ResolveCameraReferences();
			if (frontViewCamera == null || effectCamera == null)
			{
				return;
			}

			var frontCameraData = GetOrAddCameraData(frontViewCamera);
			var effectCameraData = GetOrAddCameraData(effectCamera);
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
			effectCamera.clearFlags = CameraClearFlags.Depth;
			if (!frontCameraData.cameraStack.Contains(effectCamera))
			{
				frontCameraData.cameraStack.Add(effectCamera);
			}

			frontViewCamera.GetSekaiAdditionalCameraData()?.Setup(0, 2);
			effectCamera.GetSekaiAdditionalCameraData()?.Setup(1, 2);
			if (_baseController != null)
			{
				_baseController.BaseCamera = frontViewCamera;
			}
		}

		private void SetupNotesView()
		{
			Transform parent = liveRoot != null ? liveRoot : transform;
			_notesViewManager = new NotesViewManager();
			_notesViewManager.Setup(parent);
		}

		private void SetupTapEffect()
		{
			Transform parent = effectRoot != null ? effectRoot : transform;
			if (_tapEffect == null)
			{
				_tapEffect = parent.GetComponent<TapEffectView>();
				if (_tapEffect == null)
				{
					_tapEffect = parent.gameObject.AddComponent<TapEffectView>();
				}
			}
			_tapEffect.Setup(_baseController, _hitFxSkin);

			if (_longTapEffectView == null)
			{
				GameObject longTapObject = new GameObject("LongTapEffectView", typeof(LongTapEffectView));
				longTapObject.transform.SetParent(parent, false);
				_longTapEffectView = longTapObject.GetComponent<LongTapEffectView>();
			}
			_longTapEffectView.Setup(_hitFxSkin, _isAuto);

			parent.position = transform.position;
			if (effectCamera != null)
			{
				RefreshTapEffectProjection();
				effectCamera.enabled = false;
			}
		}

		private MusicInfoView ResolveMusicInfo()
		{
			if (_resolvedMusicInfo != null)
			{
				return _resolvedMusicInfo;
			}

			_resolvedMusicInfo = musicInfo as MusicInfoView;
			if (_resolvedMusicInfo == null)
			{
				_resolvedMusicInfo = GetComponentInChildren<MusicInfoView>(true);
			}
			return _resolvedMusicInfo;
		}

		private void ResolveChildViews()
		{
			laneView ??= GetComponentInChildren<LaneView>(true);
			_resolvedMusicInfo = musicInfo != null ? musicInfo : GetComponentInChildren<MusicInfoView>(true);
			musicInfo = _resolvedMusicInfo;
			judgmentView ??= GetComponentInChildren<JudgmentView>(true);
			judgmentDescriptionView ??= GetComponentInChildren<JudgmentDescriptionView>(true);
			comboView ??= GetComponentInChildren<ComboView>(true);
			scoreView ??= GetComponentInChildren<ScoreView>(true);
			lifeView ??= GetComponentInChildren<LifeView>(true);
			liveResultView ??= GetComponentInChildren<LiveResultView>(true);
			cutinManager ??= GetComponentInChildren<CutinManager>(true);
			countdownView ??= GetComponentInChildren<CountdownView>(true);
			screenEffectView ??= GetComponentInChildren<ScreenEffectView>(true);
			consecutiveAutoLiveView ??= GetComponentInChildren<ConsecutiveAutoLiveView>(true);
			noteShowRateView ??= GetComponentInChildren<NoteShowRateView>(true);
		}

		private void PlayStartVoice(float durationScale)
		{
			if (_collaborationModeState != LiveMusicData.CollaborationModeState.On)
			{
				return;
			}

			_startVoiceMotion.TryCancel();
			_startVoiceMotion = LiveMotion.Delay(Mathf.Max(0f, durationScale) * 0.1f, () =>
			{
				SoundManager.Instance.PlayIngameVoiceOneShot(LiveSoundDefine.SE_LIVE_START);
			});
		}

		private void ResolveCameraReferences()
		{
			if (frontViewCamera == null)
			{
				var frontCameraTransform = transform.Find("FrontCamera");
				frontViewCamera = frontCameraTransform != null ? frontCameraTransform.GetComponent<Camera>() : null;
			}

			if (effectCamera == null)
			{
				var effectCameraTransform = transform.Find("EffectCameraRoot/Camera");
				effectCamera = effectCameraTransform != null ? effectCameraTransform.GetComponent<Camera>() : null;
			}

			if (effectRoot == null)
			{
				effectRoot = transform.Find("EffectCameraRoot");
			}

			if (cameraSizeUpdater == null)
			{
				cameraSizeUpdater = GetComponent<CameraSizeUpdater>();
			}

			if (effectCamera != null && _effectCameraBaseFieldOfView < 0f)
			{
				_effectCameraBaseFieldOfView = effectCamera.fieldOfView;
			}
		}

		private void RefreshTapEffectProjection()
		{
			if (effectCamera == null)
			{
				return;
			}

			// OpenSekai: the effect camera is disabled after setup, so keep its
			// projection in sync when desktop/mobile resolution changes at runtime.
			if (Screen.height > 0)
			{
				effectCamera.aspect = (float)Screen.width / Screen.height;
			}
			if (_effectCameraBaseFieldOfView > 0f)
			{
				effectCamera.fieldOfView = SekaiCameraAspect.CalculateVerticalFov(_effectCameraBaseFieldOfView);
			}
			effectCamera.ResetProjectionMatrix();
			if (_longTapEffectView != null && frontViewCamera != null)
			{
				Vector3 judgmentPoint = (liveRoot != null ? liveRoot : transform).TransformPoint(LiveConfig.JudgmentPositions[0]);
				_longTapEffectView.AlignBeamToLine(frontViewCamera, effectCamera, judgmentPoint);
			}
			Shader.SetGlobalMatrix(_tapEffectViewMatrixId, effectCamera.worldToCameraMatrix);
			Shader.SetGlobalMatrix(_tapEffectProjectionMatrixId, GL.GetGPUProjectionMatrix(effectCamera.projectionMatrix, false));
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
			countdownView?.StopCountdown();
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
			consecutiveAutoLiveView?.Hide();
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
			screenEffectView?.Excute(note);
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

		private void CancelMotions()
		{
			_musicStartMotion.TryCancel();
			_backgroundMotion.TryCancel();
			_startVoiceMotion.TryCancel();
			_spriteAlphaMotion.TryCancel();
			_fadeMotion.TryCancel();
			_resolvedMusicInfo?.CancelAnimation();
		}

		private void OnDisable() => CancelMotions();

		private void OnDestroy()
		{
			if (_backgroundMaterial != null)
			{
				Destroy(_backgroundMaterial);
				_backgroundMaterial = null;
			}
		}
	}
}

