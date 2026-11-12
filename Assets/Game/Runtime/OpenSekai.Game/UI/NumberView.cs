using UnityEngine;
using UnityEngine.Serialization;
using CP;

namespace Sekai
{
	[ExecuteInEditMode]
	public class NumberView : MonoBehaviour
	{
		public enum Align
		{
			Right = 0,
			Left = 1
		}

		private struct SpriteStatus
		{
			public static readonly int IconSpriteId;

			public int id;

			public bool isSurplus;

			public bool isColorDirty;

			static SpriteStatus()
			{
				IconSpriteId = -1;
			}
		}

		[SerializeField]
		private Align _align;

		[SerializeField]
		private float _numberSpacing;

		[FormerlySerializedAs("color")]
		[SerializeField]
		private Color _defaultColor;

		[SerializeField]
		private Color _surplusColor;

		[FormerlySerializedAs("plusiconSprite")]
		[FormerlySerializedAs("iconSprite")]
		[SerializeField]
		private Sprite _plusIconSprite;

		[SerializeField]
		private Sprite _minusiconSprite;

		[SerializeField]
		private Sprite[] _numberSprites;

		private SpriteRenderer[] _numberSpriteRenderers;

		private int _maxDigitCount;

		private int[] _digitValues;

		private SpriteStatus[] _spriteStatus;

		private float _alpha;

		private bool _hasIcon;

		public void Setup(string color, string _surplusColor = "#ffffff00")
		{
			Setup(ColorUtility.Create(color), ColorUtility.Create(_surplusColor));
		}

		public void Setup(Color _defaultColor, Color _surplusColor)
		{
			this._defaultColor = _defaultColor;
			this._surplusColor = _surplusColor;
			_alpha = 1f;
			_hasIcon = _plusIconSprite != null || _minusiconSprite != null;

			_numberSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
			_spriteStatus = new SpriteStatus[_numberSpriteRenderers.Length];
			_maxDigitCount = Mathf.Max(0, _numberSpriteRenderers.Length - (_hasIcon ? 1 : 0));
			_digitValues = new int[Mathf.Max(1, _numberSpriteRenderers.Length)];

			for (int i = 0; i < _numberSpriteRenderers.Length; i++)
			{
				int alignIndex = _align == Align.Left ? i : -i;
				Transform rendererTransform = _numberSpriteRenderers[i].transform;
				rendererTransform.localPosition = Vector3.right * (_numberSpacing * alignIndex);
				if (_numberSprites != null && _numberSprites.Length > 0)
				{
					_numberSpriteRenderers[i].sprite = _numberSprites[0];
				}
				_spriteStatus[i].id = 0;
				_spriteStatus[i].isSurplus = true;
				_spriteStatus[i].isColorDirty = true;
				_numberSpriteRenderers[i].enabled = _surplusColor.a > 0.01f;
			}
			UpdateColor();
		}

		public void UpdateNumber(int number)
		{
			if (_numberSpriteRenderers == null || _numberSpriteRenderers.Length == 0)
			{
				Setup(_defaultColor, _surplusColor);
			}
			if (_numberSpriteRenderers == null || _numberSpriteRenderers.Length == 0 || _numberSprites == null || _numberSprites.Length == 0)
			{
				return;
			}

			int digitCount = CalculateDigit(number);
			int iconIndex = _align == Align.Left ? 0 : digitCount;
			if (_hasIcon && iconIndex >= 0 && iconIndex < _numberSpriteRenderers.Length)
			{
				SpriteRenderer renderer = _numberSpriteRenderers[iconIndex];
				if (number > 0)
				{
					renderer.sprite = _plusIconSprite;
					_spriteStatus[iconIndex].id = SpriteStatus.IconSpriteId;
				}
				else if (number < 0)
				{
					renderer.sprite = _minusiconSprite;
					_spriteStatus[iconIndex].id = SpriteStatus.IconSpriteId;
				}
				else
				{
					renderer.sprite = null;
					_spriteStatus[iconIndex].id = 0;
				}
				if (_spriteStatus[iconIndex].isSurplus)
				{
					_spriteStatus[iconIndex].isSurplus = false;
					_spriteStatus[iconIndex].isColorDirty = true;
				}
			}

			int offset = _align == Align.Left && _hasIcon && number != 0 ? 1 : 0;
			for (int displayed = 0; displayed < digitCount; displayed++)
			{
				int digitIndex = _align == Align.Left ? digitCount - displayed - 1 : displayed;
				int rendererIndex = offset + displayed;
				if (rendererIndex < 0 || rendererIndex >= _numberSpriteRenderers.Length || digitIndex < 0 || digitIndex >= _digitValues.Length)
				{
					continue;
				}

				int digit = Mathf.Clamp(_digitValues[digitIndex], 0, _numberSprites.Length - 1);
				if (_spriteStatus[rendererIndex].id != digit)
				{
					_numberSpriteRenderers[rendererIndex].sprite = _numberSprites[digit];
					_spriteStatus[rendererIndex].id = digit;
				}
				_numberSpriteRenderers[rendererIndex].enabled = true;
				if (_spriteStatus[rendererIndex].isSurplus)
				{
					_spriteStatus[rendererIndex].isSurplus = false;
					_spriteStatus[rendererIndex].isColorDirty = true;
				}
			}

			int usedCount = digitCount + (_hasIcon && number != 0 ? 1 : 0);
			for (int i = usedCount; i < _numberSpriteRenderers.Length; i++)
			{
				if (_numberSprites.Length > 0 && _spriteStatus[i].id != 0)
				{
					_numberSpriteRenderers[i].sprite = _numberSprites[0];
					_spriteStatus[i].id = 0;
				}
				// Always refresh surplus digits: a renderer can be marked surplus in an earlier
				// update while its colour still holds the value it had as a leading zero, and the
				// dirty flag alone is not enough to guarantee the colour gets reapplied.
				_spriteStatus[i].isSurplus = true;
				_spriteStatus[i].isColorDirty = true;

				// When the surplus style is fully transparent the renderer is switched off as well;
				// vertex colour alone was not enough to keep the glyph off screen on device.
				_numberSpriteRenderers[i].enabled = _surplusColor.a > 0.01f;
			}

			UpdateColor();
		}

		public void UpdateAlpha(float _alpha)
		{
			float clamped = Mathf.Clamp01(_alpha);
			if (Mathf.Approximately(this._alpha, clamped))
			{
				return;
			}
			this._alpha = clamped;
			if (_spriteStatus != null)
			{
				for (int i = 0; i < _spriteStatus.Length; i++)
				{
					_spriteStatus[i].isColorDirty = true;
				}
			}
			UpdateColor();
		}

		private void UpdateColor()
		{
			if (_numberSpriteRenderers == null || _spriteStatus == null)
			{
				return;
			}
			for (int i = 0; i < _numberSpriteRenderers.Length && i < _spriteStatus.Length; i++)
			{
				if (!_spriteStatus[i].isColorDirty || _numberSpriteRenderers[i] == null)
				{
					continue;
				}
				Color color = _spriteStatus[i].isSurplus ? _surplusColor : _defaultColor;
				color.a *= _alpha;
				_numberSpriteRenderers[i].color = color;
				_spriteStatus[i].isColorDirty = false;
			}
		}

		private int CalculateDigit(int number)
		{
			if (_digitValues == null || _digitValues.Length == 0)
			{
				_digitValues = new int[Mathf.Max(1, _maxDigitCount)];
			}
			if (number == 0)
			{
				_digitValues[0] = 0;
				return 1;
			}

			int absNumber = Mathf.Abs(number);
			int digitCount = 0;
			int divisor = 1;
			do
			{
				divisor *= 10;
				digitCount++;
			}
			while (absNumber >= divisor && divisor > 0);

			if (digitCount > _maxDigitCount)
			{
				CP.LogUtility.LogError("Not enough digits. Need to Add some SpriteRenderer.");
				int trimCount = digitCount - _maxDigitCount;
				for (int i = 0; i < trimCount; i++)
				{
					absNumber %= Mathf.Max(1, divisor / 10);
					divisor /= 10;
				}
				digitCount = Mathf.Max(1, _maxDigitCount);
			}

			int workingDivisor = 1;
			for (int i = 1; i < digitCount; i++)
			{
				workingDivisor *= 10;
			}
			for (int i = digitCount - 1; i >= 0; i--)
			{
				_digitValues[i] = absNumber / workingDivisor;
				absNumber %= workingDivisor;
				workingDivisor = Mathf.Max(1, workingDivisor / 10);
			}
			return digitCount;
		}

		public NumberView()
		{
			_align = Align.Right;
			_numberSpacing = 0.95f;
			_defaultColor = Color.white;
			_surplusColor = new Color(0f, 0f, 0f, 0f);
		}
	}
}
