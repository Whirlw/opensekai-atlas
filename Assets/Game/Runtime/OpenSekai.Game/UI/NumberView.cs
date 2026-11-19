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

		private int _digitsAtNormalScale;
		private Vector3 _normalScale;

		public void Setup(string color, string _surplusColor = "#ffffff00", int minimumDigits = 0)
		{
			Setup(ColorUtility.Create(color), ColorUtility.Create(_surplusColor), minimumDigits);
		}

		public void Setup(Color _defaultColor, Color _surplusColor, int minimumDigits = 0)
		{
			this._defaultColor = _defaultColor;
			this._surplusColor = _surplusColor;
			_alpha = 1f;
			_hasIcon = _plusIconSprite != null || _minusiconSprite != null;

			_numberSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
			int iconSlots = _hasIcon ? 1 : 0;
			int required = minimumDigits + iconSlots;
			if (minimumDigits > 0 && _numberSpriteRenderers.Length > iconSlots && _numberSpriteRenderers.Length < required)
			{
				_digitsAtNormalScale = _numberSpriteRenderers.Length - iconSlots;
				_normalScale = transform.localScale;
				var template = _numberSpriteRenderers[_numberSpriteRenderers.Length - 1];
				int existing = _numberSpriteRenderers.Length;
				System.Array.Resize(ref _numberSpriteRenderers, required);
				for (int i = existing; i < required; i++)
					_numberSpriteRenderers[i] = Instantiate(template, template.transform.parent);
			}
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
			if (_digitsAtNormalScale > 0)
				transform.localScale = _normalScale * Mathf.Min(1f, (float)_digitsAtNormalScale / digitCount);
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
			long remaining = System.Math.Abs((long)number);
			int digitCount = 0;
			do
			{
				if (digitCount < _maxDigitCount)
					_digitValues[digitCount] = (int)(remaining % 10);
				digitCount++;
				remaining /= 10;
			}
			while (remaining > 0);

			if (digitCount > _maxDigitCount)
			{
				CP.LogUtility.LogError("Not enough digits. Need to Add some SpriteRenderer.");
				return Mathf.Max(1, _maxDigitCount);
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
