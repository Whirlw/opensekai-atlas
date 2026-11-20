using Sekai.Core.Live;
using UnityEngine;

namespace Sekai
{
	public class ComboView : MonoBehaviour
	{
		private const float AP_EFFECT_SPEED = 4.1887903f;

		[UnityEngine.Serialization.FormerlySerializedAs("_numberRoot")]
		[SerializeField]
		private Transform numberRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_effectNumberRoot")]
		[SerializeField]
		private Transform effectNumberRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_allPerfectEffectRoot")]
		[SerializeField]
		private Transform allPerfectEffectRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_numberSprites")]
		[SerializeField]
		private Sprite[] numberSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_allPerfectNumberSprites")]
		[SerializeField]
		private Sprite[] allPerfectNumberSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_allPerfectEffectSprites")]
		[SerializeField]
		private Sprite[] allPerfectEffectSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_comboSpriteRenderer")]
		[SerializeField]
		private SpriteRenderer comboSpriteRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_apEffectComboRenderer")]
		[SerializeField]
		private SpriteRenderer apEffectComboRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_comboTextSprite")]
		[SerializeField]
		private Sprite comboTextSprite;

		[UnityEngine.Serialization.FormerlySerializedAs("_allPerfectComboTextSprite")]
		[SerializeField]
		private Sprite allPerfectComboTextSprite;

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
				if (_numberSpriteRenderers == null && numberRoot != null)
				{
					_numberSpriteRenderers = numberRoot.GetComponentsInChildren<SpriteRenderer>(true);
				}
				return _numberSpriteRenderers;
			}
		}

		public SpriteRenderer[] EffectNumberSpriteRenderers
		{
			get
			{
				if (_effectNumberSpriteRenderers == null && effectNumberRoot != null)
				{
					_effectNumberSpriteRenderers = effectNumberRoot.GetComponentsInChildren<SpriteRenderer>(true);
				}
				return _effectNumberSpriteRenderers;
			}
		}

		public void Setup(bool _useAllPerfectEffect)
		{
			this._useAllPerfectEffect = _useAllPerfectEffect;
			_numberSpriteRenderers = numberRoot != null ? numberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_effectNumberSpriteRenderers = effectNumberRoot != null ? effectNumberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_allPerfectEffectRenderers = allPerfectEffectRoot != null ? allPerfectEffectRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
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
			if (numberRoot != null)
			{
				numberRoot.localScale = Vector3.zero;
			}
			if (effectNumberRoot != null)
			{
				effectNumberRoot.localScale = Vector3.zero;
			}
			if (allPerfectEffectRoot != null)
			{
				allPerfectEffectRoot.localScale = Vector3.zero;
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

			Sprite[] digitSprites = numberSprites;
			Sprite textSprite = comboTextSprite;
			_isAllPerfect = isApScore && _useAllPerfectEffect;
			if (_isAllPerfect)
			{
				digitSprites = allPerfectNumberSprites;
				textSprite = allPerfectComboTextSprite;
			}
			if (comboSpriteRenderer != null)
			{
				comboSpriteRenderer.sprite = textSprite;
			}
			if (allPerfectEffectRoot != null)
			{
				allPerfectEffectRoot.gameObject.SetActive(_isAllPerfect);
			}
			if (apEffectComboRenderer != null)
			{
				apEffectComboRenderer.gameObject.SetActive(_isAllPerfect);
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
					ApplyComboDigit(_allPerfectEffectRenderers[i], visible, x, allPerfectEffectSprites, value % 10);
				}
				value /= 10;
			}
		}

		private void Update()
		{
			if (numberRoot != null)
			{
				float appear = Mathf.Clamp01(_time / 0.1f);
				numberRoot.localScale = Vector3.one * (0.6f + appear * 0.4f);
			}

			float burst = Mathf.Clamp01((_time - 0.1f) / 0.1f);
			float effectScale = _time >= 0.1f ? 1f + burst * 0.4f : 0f;
			if (effectNumberRoot != null)
			{
				effectNumberRoot.localScale = Vector3.one * effectScale;
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
				if (allPerfectEffectRoot != null && numberRoot != null)
				{
					allPerfectEffectRoot.localScale = numberRoot.localScale;
				}
				float alpha = (Mathf.Sin(Time.time * AP_EFFECT_SPEED) + 1f) * 0.5f;
				if (_allPerfectEffectRenderers != null)
				{
					for (int i = 0; i < _allPerfectEffectRenderers.Length; i++)
					{
						SetRendererAlpha(_allPerfectEffectRenderers[i], alpha);
					}
				}
				SetRendererAlpha(apEffectComboRenderer, alpha);
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
