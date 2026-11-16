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
		[SerializeField]
		private GameObject _transitionerPrefab;

		[SerializeField]
		private SekaiBG _background;

		[SerializeField]
		private CanvasGroup _frontGroup;

		[SerializeField]
		private CustomImage _progressBar;

		[SerializeField]
		private LoadingIndicatorAnimation _gaugeAnimation;

		[SerializeField]
		private CustomImage _whiteOutImage;

		private bool _transitionStarted;
		private LiveTransitioner _transitioner;
		private MotionHandle _frontFade;

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
				if (_frontGroup != null)
				{
					_frontGroup.alpha = 0f;
				}
				if (_whiteOutImage != null)
				{
					_whiteOutImage.gameObject.SetActive(false);
				}
				if (_gaugeAnimation != null)
				{
					_gaugeAnimation.gameObject.SetActive(false);
				}
				LiveEntryCurtain.WhenCovered(OnFinishTransitionFadeOut);
				return;
			}

			if (_frontGroup != null)
			{
				_frontFade.TryCancel();
				_frontGroup.alpha = 0f;
				_frontFade = LMotion.Create(0f, 1f, 0.2f)
					.WithEase(Ease.OutQuad)
					.WithDelay(0.1f)
					.BindToAlpha(_frontGroup);
			}
			if (_whiteOutImage != null)
			{
				Color color = _whiteOutImage.color;
				color.a = 1f;
				_whiteOutImage.color = color;
			}
			if (_background != null)
			{
				_background.Initialize();
				_background.gameObject.SetActive(false);
			}
			UpdateProgress(0f);
			if (_gaugeAnimation != null)
			{
				var gaugeCanvas = _gaugeAnimation.GetComponent<Canvas>();
				if (gaugeCanvas != null)
				{
					gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
					gaugeCanvas.overrideSorting = true;
					gaugeCanvas.sortingOrder = 1001;
				}
			}

			StartCoroutine(PlayStartWhiteOut());
			StartTransition(OnFinishTransitionFadeOut);
		}

		private void OnDestroy()
		{
			_frontFade.TryCancel();
		}

		private IEnumerator PlayStartWhiteOut()
		{
			yield return new WaitForSeconds(START_FADE_OUT_DELAY);
			if (_background != null)
			{
				_background.gameObject.SetActive(true);
			}

			if (_whiteOutImage == null)
			{
				yield break;
			}

			var color = _whiteOutImage.color;
			var elapsed = 0f;
			while (elapsed < START_FADE_OUT_DURATION)
			{
				elapsed += Time.deltaTime;
				color.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / START_FADE_OUT_DURATION));
				_whiteOutImage.color = color;
				yield return null;
			}

			color.a = 0f;
			_whiteOutImage.color = color;
		}

		private void UpdateProgress(float progress)
		{
			progress = Mathf.Clamp01(progress);
			if (_progressBar != null)
			{
				_progressBar.fillAmount = progress;
			}

			if (_gaugeAnimation != null)
			{
				_gaugeAnimation.PlayAnimation(progress);
			}
		}

		private void StartTransition(Action onFinished)
		{
			if (_transitionStarted)
			{
				return;
			}

			_transitionStarted = true;
			if (_background != null)
			{
				_background.Transition();
			}

			UpdateProgress(1f);
			if (_transitionerPrefab == null)
			{
				onFinished?.Invoke();
				return;
			}

			GameObject transitionerObject = Instantiate(_transitionerPrefab);
			transitionerObject.name = _transitionerPrefab.name;
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
