using Sekai.Core.Live;
using UnityEngine;

namespace Sekai
{
	public class ComboView : MonoBehaviour
	{
		private const float AP_EFFECT_SPEED = 4.1887903f;

		[SerializeField]
		private Transform _numberRoot;

		[SerializeField]
		private Transform _effectNumberRoot;

		[SerializeField]
		private Transform _allPerfectEffectRoot;

		[SerializeField]
		private Sprite[] _numberSprites;

		[SerializeField]
		private Sprite[] _allPerfectNumberSprites;

		[SerializeField]
		private Sprite[] _allPerfectEffectSprites;

		[SerializeField]
		private SpriteRenderer _comboSpriteRenderer;

		[SerializeField]
		private SpriteRenderer _apEffectComboRenderer;

		[SerializeField]
		private Sprite _comboTextSprite;

		[SerializeField]
		private Sprite _allPerfectComboTextSprite;

		private SpriteRenderer[] _numberSpriteRenderers;

		private SpriteRenderer[] _effectNumberSpriteRenderers;

		private SpriteRenderer[] _allPerfectEffectRenderers;

		private float _time;

		private int _currentCombo;

		private bool _isAllPerfect;

		private bool _useAllPerfectEffect;

		public SpriteRenderer[] NumberSpriteRenderers
		{
			get
			{
				if (_numberSpriteRenderers == null && _numberRoot != null)
				{
					_numberSpriteRenderers = _numberRoot.GetComponentsInChildren<SpriteRenderer>(true);
				}
				return _numberSpriteRenderers;
			}
		}

		public SpriteRenderer[] EffectNumberSpriteRenderers
		{
			get
			{
				if (_effectNumberSpriteRenderers == null && _effectNumberRoot != null)
				{
					_effectNumberSpriteRenderers = _effectNumberRoot.GetComponentsInChildren<SpriteRenderer>(true);
				}
				return _effectNumberSpriteRenderers;
			}
		}

		public void Setup(bool _useAllPerfectEffect)
		{
			this._useAllPerfectEffect = _useAllPerfectEffect;
			_numberSpriteRenderers = _numberRoot != null ? _numberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_effectNumberSpriteRenderers = _effectNumberRoot != null ? _effectNumberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_allPerfectEffectRenderers = _allPerfectEffectRoot != null ? _allPerfectEffectRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			foreach (SpriteRenderer renderer in _numberSpriteRenderers)
			{
				OpenSekai.Shared.UI.SpriteQuad.DrawAsRectangle(renderer);
			}
			foreach (SpriteRenderer renderer in _effectNumberSpriteRenderers)
			{
				OpenSekai.Shared.UI.SpriteQuad.DrawAsRectangle(renderer);
			}
			foreach (SpriteRenderer renderer in _allPerfectEffectRenderers)
			{
				OpenSekai.Shared.UI.SpriteQuad.DrawAsRectangle(renderer);
			}
			Clear();
		}

		public void Clear()
		{
			transform.localScale = Vector3.zero;
			if (_numberRoot != null)
			{
				_numberRoot.localScale = Vector3.zero;
			}
			if (_effectNumberRoot != null)
			{
				_effectNumberRoot.localScale = Vector3.zero;
			}
			if (_allPerfectEffectRoot != null)
			{
				_allPerfectEffectRoot.localScale = Vector3.zero;
			}
			_currentCombo = 0;
			_time = 0f;
		}

		public void Excute(LiveScore score)
		{
			Excute(score.combo, score.IsAllPerfect);
		}

		public void Excute(int combo, bool isApScore)
		{
			if (_currentCombo == combo)
			{
				return;
			}
			_time = 0f;
			_currentCombo = combo;
			transform.localScale = combo > 0 ? Vector3.one : Vector3.zero;

			Sprite[] digitSprites = _numberSprites;
			Sprite textSprite = _comboTextSprite;
			_isAllPerfect = isApScore && _useAllPerfectEffect;
			if (_isAllPerfect)
			{
				digitSprites = _allPerfectNumberSprites;
				textSprite = _allPerfectComboTextSprite;
			}
			if (_comboSpriteRenderer != null)
			{
				_comboSpriteRenderer.sprite = textSprite;
			}
			if (_allPerfectEffectRoot != null)
			{
				_allPerfectEffectRoot.gameObject.SetActive(_isAllPerfect);
			}
			if (_apEffectComboRenderer != null)
			{
				_apEffectComboRenderer.gameObject.SetActive(_isAllPerfect);
			}

			int digitCount = combo > 0 ? Mathf.FloorToInt(Mathf.Log10(combo)) + 1 : 1;
			int value = combo;
			SpriteRenderer[] mainRenderers = NumberSpriteRenderers ?? new SpriteRenderer[0];
			SpriteRenderer[] effectRenderers = EffectNumberSpriteRenderers ?? new SpriteRenderer[0];
			for (int i = 0; i < mainRenderers.Length; i++)
			{
				bool visible = i < digitCount && combo > 0;
				float x = (digitCount - 1) * 0.475f + i * -0.95f;
				ApplyComboDigit(mainRenderers[i], visible, x, digitSprites, value % 10);
				if (i < effectRenderers.Length)
				{
					ApplyComboDigit(effectRenderers[i], visible, x, digitSprites, value % 10);
				}
				if (_allPerfectEffectRenderers != null && i < _allPerfectEffectRenderers.Length)
				{
					ApplyComboDigit(_allPerfectEffectRenderers[i], visible, x, _allPerfectEffectSprites, value % 10);
				}
				value /= 10;
			}
		}

		private void Update()
		{
			if (_numberRoot != null)
			{
				float appear = Mathf.Clamp01(_time / 0.1f);
				_numberRoot.localScale = Vector3.one * (0.6f + appear * 0.4f);
			}

			float burst = Mathf.Clamp01((_time - 0.1f) / 0.1f);
			float effectScale = _time >= 0.1f ? 1f + burst * 0.4f : 0f;
			if (_effectNumberRoot != null)
			{
				_effectNumberRoot.localScale = Vector3.one * effectScale;
			}
			SpriteRenderer[] effectRenderers = EffectNumberSpriteRenderers;
			if (effectRenderers != null)
			{
				float alpha = _time >= 0.1f ? (1f - burst) * 0.5f : 0f;
				for (int i = 0; i < effectRenderers.Length; i++)
				{
					SetRendererAlpha(effectRenderers[i], alpha);
				}
			}

			if (_isAllPerfect)
			{
				if (_allPerfectEffectRoot != null && _numberRoot != null)
				{
					_allPerfectEffectRoot.localScale = _numberRoot.localScale;
				}
				float alpha = (Mathf.Sin(Time.time * AP_EFFECT_SPEED) + 1f) * 0.5f;
				if (_allPerfectEffectRenderers != null)
				{
					for (int i = 0; i < _allPerfectEffectRenderers.Length; i++)
					{
						SetRendererAlpha(_allPerfectEffectRenderers[i], alpha);
					}
				}
				SetRendererAlpha(_apEffectComboRenderer, alpha);
			}

			_time += Time.deltaTime;
		}

		public ComboView()
		{
		}

		private static void ApplyComboDigit(SpriteRenderer renderer, bool visible, float localX, Sprite[] sprites, int digit)
		{
			if (renderer == null)
			{
				return;
			}
			renderer.transform.localScale = visible ? Vector3.one : Vector3.zero;
			renderer.transform.localPosition = Vector3.right * localX;
			if (visible && sprites != null && digit >= 0 && digit < sprites.Length)
			{
				renderer.sprite = sprites[digit];
				OpenSekai.Shared.UI.SpriteQuad.DrawAsRectangle(renderer);
			}
		}

		private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
		{
			if (renderer == null)
			{
				return;
			}
			Color color = renderer.color;
			color.a = alpha;
			renderer.color = color;
		}
	}
}
