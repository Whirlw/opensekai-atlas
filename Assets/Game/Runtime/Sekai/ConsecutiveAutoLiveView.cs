using System;
using UnityEngine;

namespace Sekai
{
	public class ConsecutiveAutoLiveView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _pauseButton;

		[SerializeField]
		private SpriteRenderer _pauseButtonDisable;

		[SerializeField]
		private LiveSpriteButton _pauseButtonController;

		[SerializeField]
		private SpriteRenderer _autoLivePauseButton;

		[SerializeField]
		private SpriteRenderer _autoLivePauseButtonDisable;

		[SerializeField]
		private LiveSpriteButton _autoLivePauseButtonController;

		[SerializeField]
		private SpriteRenderer _autoLabel;

		private LiveOutUIController _liveOutUIController;

		public void Setup(Action onClickButton, Action onClickDialogResume, Action onClickDialogCancel, Camera baseCamera)
		{
			Hide();
			_liveOutUIController.Initialize(LivePlayMode.Free, baseCamera);
			SetupButton(onClickButton);
		}

		private void SetupButton(Action callback)
		{
			SetActive(_autoLivePauseButton, callback != null);
			SetActive(_autoLivePauseButtonDisable, false);
			if (_autoLivePauseButtonController != null)
			{
				_autoLivePauseButtonController.Setup(callback);
			}
		}

		public void Pause()
		{
			_autoLivePauseButtonController?.OnClick();
		}

		public void CalculateButtonBounds(Camera camera)
		{
			if (_autoLivePauseButtonController != null)
			{
				_autoLivePauseButtonController.CalculateBounds(camera);
			}
		}

		public void Show()
		{
			bool useFakePerfect = LiveSettingData.LoadFromStorage()?.UsesAutoFakePerfectMode ?? false;
			SetActive(_pauseButton, false);
			SetActive(_pauseButtonDisable, false);
			// Hide auto pause button in Fake Perfect mode
			SetActive(_autoLivePauseButton, !useFakePerfect);
			SetActive(_autoLivePauseButtonDisable, false);
			// Hide auto label in Fake Perfect mode
			SetActive(_autoLabel, !useFakePerfect);
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			SetActive(_pauseButton, true);
			SetActive(_pauseButtonDisable, false);
			SetActive(_autoLivePauseButton, true);
			SetActive(_autoLivePauseButtonDisable, true);
			SetActive(_autoLabel, true);
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
