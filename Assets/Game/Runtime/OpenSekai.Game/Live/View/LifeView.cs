using System;
using Sekai.Live;
using UnityEngine;
using Sekai.RankLive;

namespace Sekai
{
	public class LifeView : MonoBehaviour
	{
		[SerializeField]
		private Transform _lifeDefault;

		[SerializeField]
		private Transform _lifeDamage;

		[SerializeField]
		private SpriteRenderer _lifeDefaultGauge;

		[SerializeField]
		private SpriteRenderer _lifeDamageGauge;

		[SerializeField]
		private NumberView _number;

		[SerializeField]
		private NumberView _outlineNumber;

		[SerializeField]
		private SpriteRenderer _pauseButton;

		[SerializeField]
		private SpriteRenderer _pauseButtonDisable;

		[SerializeField]
		private SpriteRenderer _skipButton;

		[SerializeField]
		private LiveSpriteButton _pauseButtonController;

		[SerializeField]
		private LiveSpriteButton _skipButtonController;

		private int _maxLife;

		private Vector2 _lifeDefaultGaugeSize;

		private Vector2 _lifeDamageGaugeSize;

		private LiveBundleBuildData _liveBundleBuildData;

		private void Awake()
		{
			_liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
		}

		public void Setup(Action callback = null)
		{
			if (_liveBundleBuildData == null)
			{
				_liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
			}
			_maxLife = _liveBundleBuildData != null && _liveBundleBuildData.Life > 0 ? _liveBundleBuildData.Life : 1000;
			_lifeDefaultGaugeSize = _lifeDefaultGauge != null ? _lifeDefaultGauge.size : Vector2.one;
			_lifeDamageGaugeSize = _lifeDamageGauge != null ? _lifeDamageGauge.size : Vector2.one;
			if (_number != null)
			{
				_number.Setup("#ffffff", "#ffffff00");
			}
			if (_outlineNumber != null)
			{
				_outlineNumber.Setup("#ffffff", "#ffffff00");
			}
			SetupButton(callback);
			Clear();
		}

		private void SetupButton(Action callback = null)
		{
			bool enabled = callback != null;
			SetActive(_pauseButton, enabled);
			SetActive(_pauseButtonDisable, !enabled);
			SetActive(_skipButton, false);
			if (enabled && _pauseButtonController != null)
			{
				_pauseButtonController.Setup(callback);
			}
			if (enabled && _skipButtonController != null)
			{
				_skipButtonController.Setup(callback);
			}
		}

		public void CalculateButtonBounds(Camera camera)
		{
			if (_pauseButtonController != null)
			{
				_pauseButtonController.CalculateBounds(camera);
			}
			if (_skipButtonController != null)
			{
				_skipButtonController.CalculateBounds(camera);
			}
		}

		public void Clear()
		{
			if (_lifeDefault != null)
			{
				_lifeDefault.localScale = Vector3.one;
			}
			if (_lifeDamage != null)
			{
				_lifeDamage.localScale = Vector3.zero;
			}
			if (_lifeDefaultGauge != null)
			{
				_lifeDefaultGauge.size = _lifeDefaultGaugeSize;
			}
			if (_lifeDamageGauge != null)
			{
				_lifeDamageGauge.size = _lifeDamageGaugeSize;
			}
			if (_number != null)
			{
				_number.UpdateNumber(_maxLife);
			}
			if (_outlineNumber != null)
			{
				_outlineNumber.UpdateNumber(_maxLife);
			}
		}

		public void Excute(int life)
		{
			float rate = _maxLife <= 0 ? 0f : Mathf.Clamp01((float)life / _maxLife);
			bool isDefault = rate > 0.2f;
			if (_lifeDefault != null)
			{
				_lifeDefault.localScale = isDefault ? Vector3.one : Vector3.zero;
			}
			if (_lifeDamage != null)
			{
				_lifeDamage.localScale = isDefault ? Vector3.zero : Vector3.one;
			}
			if (_lifeDefaultGauge != null)
			{
				_lifeDefaultGauge.size = new Vector2(_lifeDefaultGaugeSize.x * rate, _lifeDefaultGaugeSize.y);
			}
			if (_lifeDamageGauge != null)
			{
				_lifeDamageGauge.size = new Vector2(_lifeDamageGaugeSize.x * rate, _lifeDamageGaugeSize.y);
			}
			if (_number != null)
			{
				_number.UpdateNumber(life);
			}
			if (_outlineNumber != null)
			{
				_outlineNumber.UpdateNumber(life);
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
