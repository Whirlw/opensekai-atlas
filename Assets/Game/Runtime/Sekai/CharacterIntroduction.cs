using Beebyte.Obfuscator;
using DG.Tweening;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class CharacterIntroduction : MonoBehaviour
	{
		private static float AUTO_CHANGE_DELAY;

		private const float VALUES_MAX = 10f;

		[SerializeField]
		private Sprite[] _sprites;

		[SerializeField]
		private CustomImage _bgImage;

		[SerializeField]
		private CustomButton _leftArrow;

		[SerializeField]
		private CustomButton _rightArrow;

		[SerializeField]
		private UIPartsGauge _progressGauge;

		[SerializeField]
		private Texture2D _maskTexNext;

		[SerializeField]
		private Texture2D _maskTexPrev;

		[SerializeField]
		private CustomTextMesh _progressText;

		private int _currentIndex;

		private bool _isAnimationRunning;

		private Tween _autoChangeTweenHandle;

		private float _swapValue;

		public Material BgMaterial
		{
			get
			{
				return _bgImage != null ? _bgImage.material : null;
			}
		}

		public void Setup()
		{
			_currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, (_sprites?.Length ?? 1) - 1));
			ApplyCurrentSprite();
			UpdateProgressGauge(_currentIndex + 1, _sprites != null && _sprites.Length > 0 ? _sprites.Length : 1);
			StartAutoChange();
		}

		public void UpdateProgressGauge(float currentVal, float maxVal)
		{
			if (_progressText != null)
			{
				_progressText.SetText($"{Mathf.RoundToInt(currentVal)}/{Mathf.RoundToInt(maxVal)}");
			}
		}

		public void Unload()
		{
			StopAutoChange();
			if (BgMaterial != null)
			{
				BgMaterial.SetTexture("_MaskTex", null);
			}
		}

		private void SetSwapValue(float val)
		{
			_swapValue = val;
			if (BgMaterial != null)
			{
				BgMaterial.SetFloat("_SwapValue", _swapValue);
			}
		}

		[Skip]
		public void Next()
		{
			if (_sprites == null || _sprites.Length == 0 || _isAnimationRunning)
			{
				return;
			}

			_currentIndex = (_currentIndex + 1) % _sprites.Length;
			PlayChange(_maskTexNext);
		}

		[Skip]
		public void Prev()
		{
			if (_sprites == null || _sprites.Length == 0 || _isAnimationRunning)
			{
				return;
			}

			_currentIndex = (_currentIndex - 1 + _sprites.Length) % _sprites.Length;
			PlayChange(_maskTexPrev);
		}

		[Skip]
		public void OnFlick(PointerGestureData gestureData)
		{
			if (gestureData == null)
			{
				return;
			}

			if (gestureData.IsFlickLeft)
			{
				Next();
			}
			else if (gestureData.IsFlickRight)
			{
				Prev();
			}
		}

		private void StartAutoChange()
		{
			StopAutoChange();
			if (_sprites == null || _sprites.Length <= 1)
			{
				return;
			}

			_autoChangeTweenHandle = DOVirtual.DelayedCall(AUTO_CHANGE_DELAY, Next).SetLoops(-1, LoopType.Restart);
		}

		public void StopAutoChange()
		{
			if (_autoChangeTweenHandle != null)
			{
				_autoChangeTweenHandle.Kill(false);
				_autoChangeTweenHandle = null;
			}
		}

		private void ResetAutoChange()
		{
			StartAutoChange();
		}

		private void PlayChange(Texture2D maskTexture)
		{
			ResetAutoChange();
			_isAnimationRunning = true;
			if (BgMaterial != null)
			{
				BgMaterial.SetTexture("_MaskTex", maskTexture);
			}

			SetSwapValue(0f);
			DOTween.To(() => _swapValue, SetSwapValue, VALUES_MAX, 0.25f)
				.OnComplete(() =>
				{
					ApplyCurrentSprite();
					SetSwapValue(0f);
					_isAnimationRunning = false;
				});
			UpdateProgressGauge(_currentIndex + 1, _sprites.Length);
		}

		private void ApplyCurrentSprite()
		{
			if (_bgImage != null && _sprites != null && _sprites.Length > 0)
			{
				_bgImage.sprite = _sprites[_currentIndex];
			}
		}

		public CharacterIntroduction()
		{
		}

		static CharacterIntroduction()
		{
			AUTO_CHANGE_DELAY = 5f;
		}
	}
}
