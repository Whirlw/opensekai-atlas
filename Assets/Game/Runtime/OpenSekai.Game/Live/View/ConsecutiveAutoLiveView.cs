using OpenSekai.Shared.Settings;
using System;
using UnityEngine;

namespace Sekai
{
	public class ConsecutiveAutoLiveView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButton")]
		[SerializeField]
		private SpriteRenderer pauseButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButtonDisable")]
		[SerializeField]
		private SpriteRenderer pauseButtonDisable;

		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButtonController")]
		[SerializeField]
		private LiveSpriteButton pauseButtonController;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoLivePauseButton")]
		[SerializeField]
		private SpriteRenderer autoLivePauseButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoLivePauseButtonDisable")]
		[SerializeField]
		private SpriteRenderer autoLivePauseButtonDisable;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoLivePauseButtonController")]
		[SerializeField]
		private LiveSpriteButton autoLivePauseButtonController;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoLabel")]
		[SerializeField]
		private SpriteRenderer autoLabel;

		private LiveOutUIController _liveOutUIController;

		public void Setup(Action onClickButton, Action onClickDialogResume, Action onClickDialogCancel, Camera baseCamera)
		{
			Hide();
			_liveOutUIController.Initialize(LivePlayMode.Free, baseCamera);
			SetupButton(onClickButton);
		}

		private void SetupButton(Action callback)
		{
			SetActive(autoLivePauseButton, callback != null);
			SetActive(autoLivePauseButtonDisable, false);
			if (autoLivePauseButtonController != null)
			{
				autoLivePauseButtonController.Setup(callback);
			}
		}

		public void Pause()
		{
			autoLivePauseButtonController?.OnClick();
		}

		public void CalculateButtonBounds(Camera camera)
		{
			if (autoLivePauseButtonController != null)
			{
				autoLivePauseButtonController.CalculateBounds(camera);
			}
		}

		public void Show()
		{
			bool useFakePerfect = LiveSettingsStore.Shared.Load()?.UsesAutoFakePerfectMode ?? false;
			SetActive(pauseButton, false);
			SetActive(pauseButtonDisable, false);
			// Hide auto pause button in Fake Perfect mode
			SetActive(autoLivePauseButton, !useFakePerfect);
			SetActive(autoLivePauseButtonDisable, false);
			// Hide auto label in Fake Perfect mode
			SetActive(autoLabel, !useFakePerfect);
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			SetActive(pauseButton, true);
			SetActive(pauseButtonDisable, false);
			SetActive(autoLivePauseButton, true);
			SetActive(autoLivePauseButtonDisable, true);
			SetActive(autoLabel, true);
			gameObject.SetActive(false);
		}

		public ConsecutiveAutoLiveView()
		{
			_liveOutUIController = new LiveOutUIController();
		}

		private static void SetActive(SpriteRenderer renderer, bool active)
		{
			if (renderer != null)
			{
				renderer.gameObject.SetActive(active);
			}
		}
	}
}
