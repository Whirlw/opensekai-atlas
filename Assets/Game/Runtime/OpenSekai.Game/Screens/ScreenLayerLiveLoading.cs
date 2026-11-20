using System;
using System.Collections;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.Shared.UI;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class ScreenLayerLiveLoading : ScreenLayer
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_transitionerPrefab")]
		[SerializeField]
		private GameObject transitionerPrefab;

		[UnityEngine.Serialization.FormerlySerializedAs("_background")]
		[SerializeField]
		private SekaiBG background;

		[UnityEngine.Serialization.FormerlySerializedAs("_frontGroup")]
		[SerializeField]
		private CanvasGroup frontGroup;

		[UnityEngine.Serialization.FormerlySerializedAs("_progressBar")]
		[SerializeField]
		private CustomImage progressBar;

		[UnityEngine.Serialization.FormerlySerializedAs("_gaugeAnimation")]
		[SerializeField]
		private LoadingIndicatorAnimation gaugeAnimation;

		[UnityEngine.Serialization.FormerlySerializedAs("_whiteOutImage")]
		[SerializeField]
		private CustomImage whiteOutImage;

		private bool _transitionStarted;
		private LiveTransitioner _transitioner;
		private MotionHandle _frontFade;
		private MotionHandle _whiteFade;
		private Coroutine _whiteOutDelay;

		// The library has already read the chart and registered audio before this screen is pushed,
		// so the animation only covers the scene swap. Keep that cover short.
		private const float START_FADE_OUT_DELAY = 0.15f;
		private const float START_FADE_OUT_DURATION = 0.35f;
		private const float TRANSITION_DURATION = 0.2f;

		protected override void OnInitComponent()
		{
			base.OnInitComponent();

			// From the library, its loading screen already covers the page and stays up into the
			// live scene: the white-out and the transitioner would only add a blank flash and time.
			if (LiveEntryCurtain.IsHeld)
			{
				_transitionStarted = true;
				if (frontGroup != null)
				{
					frontGroup.alpha = 0f;
				}
				if (whiteOutImage != null)
				{
					whiteOutImage.gameObject.SetActive(false);
				}
				if (gaugeAnimation != null)
				{
					gaugeAnimation.gameObject.SetActive(false);
				}
				LiveEntryCurtain.WhenCovered(OnFinishTransitionFadeOut);
				return;
			}

			if (frontGroup != null)
			{
				_frontFade.TryCancel();
				frontGroup.alpha = 0f;
				_frontFade = LMotion.Create(0f, 1f, 0.2f)
					.WithEase(Ease.OutQuad)
					.WithDelay(0.1f)
					.BindToAlpha(frontGroup);
			}
			if (whiteOutImage != null)
			{
				Color color = whiteOutImage.color;
				color.a = 1f;
				whiteOutImage.color = color;
			}
			if (background != null)
			{
				background.Initialize();
				background.gameObject.SetActive(false);
			}
			UpdateProgress(0f);
			if (gaugeAnimation != null)
			{
				var gaugeCanvas = gaugeAnimation.GetComponent<Canvas>();
				if (gaugeCanvas != null)
				{
					gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
					gaugeCanvas.overrideSorting = true;
					gaugeCanvas.sortingOrder = 1001;
				}
			}

			_whiteOutDelay = StartCoroutine(PlayStartWhiteOut());
			StartTransition(OnFinishTransitionFadeOut);
		}

		private void OnDisable()
		{
			_frontFade.TryCancel();
			_whiteFade.TryCancel();
			if (_whiteOutDelay != null)
			{
				StopCoroutine(_whiteOutDelay);
				_whiteOutDelay = null;
			}
		}

		private IEnumerator PlayStartWhiteOut()
		{
			yield return new WaitForSeconds(START_FADE_OUT_DELAY);
			if (background != null)
			{
				background.gameObject.SetActive(true);
			}

			if (whiteOutImage == null)
			{
				yield break;
			}

			_whiteFade = LMotion.Create(1f, 0f, START_FADE_OUT_DURATION)
				.Bind(alpha =>
				{
					Color color = whiteOutImage.color;
					color.a = alpha;
					whiteOutImage.color = color;
				});
		}

		private void UpdateProgress(float progress)
		{
			progress = Mathf.Clamp01(progress);
			if (progressBar != null)
			{
				progressBar.fillAmount = progress;
			}

			if (gaugeAnimation != null)
			{
				gaugeAnimation.PlayAnimation(progress);
			}
		}

		private void StartTransition(Action onFinished)
		{
			if (_transitionStarted)
			{
				return;
			}

			_transitionStarted = true;
			if (background != null)
			{
				background.Transition();
			}

			UpdateProgress(1f);
			if (transitionerPrefab == null)
			{
				onFinished?.Invoke();
				return;
			}

			GameObject transitionerObject = Instantiate(transitionerPrefab);
			transitionerObject.name = transitionerPrefab.name;
			_transitioner = transitionerObject.GetComponent<LiveTransitioner>();
			if (_transitioner == null)
			{
				Destroy(transitionerObject);
				onFinished?.Invoke();
				return;
			}

			_transitioner.Play(onFinished, "SE_AREA_TRANSITION_SEKAI", false, 0f, 0f, TRANSITION_DURATION);
		}

		private void OnFinishTransitionFadeOut()
		{
			SceneManager.Instance.RequestScene(SceneManager.Scene.Core);
		}
	}
}
