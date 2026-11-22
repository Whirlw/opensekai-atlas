using System;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class LifeView : MonoBehaviour
	{
		private const int MAX_LIFE_DIGITS = 10;

		[UnityEngine.Serialization.FormerlySerializedAs("_lifeDefault")]
		[SerializeField]
		private Transform lifeDefault;

		[UnityEngine.Serialization.FormerlySerializedAs("_lifeDamage")]
		[SerializeField]
		private Transform lifeDamage;

		[UnityEngine.Serialization.FormerlySerializedAs("_lifeDefaultGauge")]
		[SerializeField]
		private SpriteRenderer lifeDefaultGauge;

		[UnityEngine.Serialization.FormerlySerializedAs("_lifeDamageGauge")]
		[SerializeField]
		private SpriteRenderer lifeDamageGauge;

		[UnityEngine.Serialization.FormerlySerializedAs("_number")]
		[SerializeField]
		private NumberView number;

		[UnityEngine.Serialization.FormerlySerializedAs("_outlineNumber")]
		[SerializeField]
		private NumberView outlineNumber;

		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButton")]
		[SerializeField]
		private SpriteRenderer pauseButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButtonDisable")]
		[SerializeField]
		private SpriteRenderer pauseButtonDisable;

		[UnityEngine.Serialization.FormerlySerializedAs("_skipButton")]
		[SerializeField]
		private SpriteRenderer skipButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_pauseButtonController")]
		[SerializeField]
		private LiveSpriteButton pauseButtonController;

		[UnityEngine.Serialization.FormerlySerializedAs("_skipButtonController")]
		[SerializeField]
		private LiveSpriteButton skipButtonController;

		private int _maxLife;

		private Vector2 _lifeDefaultGaugeSize;

		private Vector2 _lifeDamageGaugeSize;

		private LiveBundleBuildData _liveBundleBuildData;

		private void Awake()
		{
			_liveBundleBuildData = ResourceCatalog.LiveBundleConfig.Load();
		}

		public void Setup(Action callback = null, int? initialLife = null)
		{
			if (_liveBundleBuildData == null)
			{
				_liveBundleBuildData = ResourceCatalog.LiveBundleConfig.Load();
			}
			_maxLife = initialLife ?? (_liveBundleBuildData != null && _liveBundleBuildData.Life > 0 ? _liveBundleBuildData.Life : 1000);
			_lifeDefaultGaugeSize = lifeDefaultGauge != null ? lifeDefaultGauge.size : Vector2.one;
			_lifeDamageGaugeSize = lifeDamageGauge != null ? lifeDamageGauge.size : Vector2.one;
			if (number != null)
			{
				number.Setup("#ffffff", minimumDigits: MAX_LIFE_DIGITS);
			}
			if (outlineNumber != null)
			{
				outlineNumber.Setup("#ffffff", minimumDigits: MAX_LIFE_DIGITS);
			}
			SetupButton(callback);
			Clear();
		}

		private void SetupButton(Action callback = null)
		{
			_pauseEnabled = callback != null;
			_onSkip = null;
			ShowPauseButton();
			if (_pauseEnabled && pauseButtonController != null)
			{
				pauseButtonController.Setup(callback);
			}
			if (skipButtonController != null)
			{
				skipButtonController.Setup(PressSkip);
			}
		}

		public void CalculateButtonBounds(Camera camera)
		{
			_buttonCamera = camera;
			if (pauseButtonController != null)
			{
				pauseButtonController.CalculateBounds(camera);
			}
			if (skipButtonController != null && skipButton != null && skipButton.gameObject.activeInHierarchy)
			{
				skipButtonController.CalculateBounds(camera);
			}
		}

		private bool _pauseEnabled;
		private Action _onSkip;
		private Camera _buttonCamera;

		public bool IsSkipShown => _onSkip != null;

		/// <summary>
		/// Puts the skip button where the pause button is, until it is pressed or <see cref="HideSkip"/>.
		/// The two share a spot and an outline, so only one is ever on screen.
		/// </summary>
		public void ShowSkip(Action onSkip)
		{
			if (onSkip == null || skipButton == null)
			{
				return;
			}

			_onSkip = onSkip;
			SetActive(pauseButton, false);
			SetActive(pauseButtonDisable, false);
			SetActive(skipButton, true);
			// The bounds of a renderer that was switched off are empty, so they are taken now.
			if (skipButtonController != null && _buttonCamera != null)
			{
				skipButtonController.CalculateBounds(_buttonCamera);
			}
		}

		/// <summary>Puts the pause button back.</summary>
		public void HideSkip()
		{
			_onSkip = null;
			ShowPauseButton();
		}

		private void ShowPauseButton()
		{
			SetActive(skipButton, false);
			SetActive(pauseButton, _pauseEnabled);
			SetActive(pauseButtonDisable, !_pauseEnabled);
		}

		private void PressSkip()
		{
			Action callback = _onSkip;
			HideSkip();
			callback?.Invoke();
		}

		public void Clear()
		{
			if (lifeDefault != null)
			{
				lifeDefault.localScale = Vector3.one;
			}
			if (lifeDamage != null)
			{
				lifeDamage.localScale = Vector3.zero;
			}
			if (lifeDefaultGauge != null)
			{
				lifeDefaultGauge.size = _lifeDefaultGaugeSize;
			}
			if (lifeDamageGauge != null)
			{
				lifeDamageGauge.size = _lifeDamageGaugeSize;
			}
			if (number != null)
			{
				number.UpdateNumber(_maxLife);
			}
			if (outlineNumber != null)
			{
				outlineNumber.UpdateNumber(_maxLife);
			}
		}

		public void Excute(int life)
		{
			float rate = _maxLife <= 0 ? 0f : Mathf.Clamp01((float)life / _maxLife);
			bool isDefault = rate > 0.2f;
			if (lifeDefault != null)
			{
				lifeDefault.localScale = isDefault ? Vector3.one : Vector3.zero;
			}
			if (lifeDamage != null)
			{
				lifeDamage.localScale = isDefault ? Vector3.zero : Vector3.one;
			}
			if (lifeDefaultGauge != null)
			{
				lifeDefaultGauge.size = new Vector2(_lifeDefaultGaugeSize.x * rate, _lifeDefaultGaugeSize.y);
			}
			if (lifeDamageGauge != null)
			{
				lifeDamageGauge.size = new Vector2(_lifeDamageGaugeSize.x * rate, _lifeDamageGaugeSize.y);
			}
			if (number != null)
			{
				number.UpdateNumber(life);
			}
			if (outlineNumber != null)
			{
				outlineNumber.UpdateNumber(life);
			}
		}

		public LifeView()
		{
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
