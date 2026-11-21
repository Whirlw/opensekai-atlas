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

		private const float DEFAULT_ADVANCE = 0.95f;
		private ComboGlyph[] _normalGlyphs, _apGlyphs, _glowGlyphs;
		private ComboGlyph _label, _apLabel, _glowLabel;
		private LabelBaseline _textBaseline, _glowBaseline;
		private LayoutNode _layoutRoot, _digitsRow, _burstRow, _glowRow;
		private float[] _activeGlowOpacity;

		private sealed class LayoutNode
		{
			private readonly Transform _node;
			private readonly Vector3 _position, _scale;

			internal LayoutNode(Transform node)
			{
				_node = node;
				_position = node.localPosition;
				_scale = node.localScale;
			}

			internal void Place(float[] position)
			{
				var point = position == null ? (Vector2)_position : ComboGlyph.Point(position);
				_node.localPosition = new Vector3(point.x, point.y, _position.z);
			}

			internal void Scale(float[] scale)
			{
				_node.localScale = scale == null ? _scale : new Vector3(_scale.x * scale[0], _scale.y * scale[1], _scale.z);
			}
		}

		private sealed class LabelBaseline
		{
			private readonly Sprite _sprite;
			private readonly Vector3 _position;
			private readonly SpriteDrawMode _mode;
			private readonly Vector2 _size;

			internal LabelBaseline(SpriteRenderer renderer)
			{
				_sprite = renderer.sprite;
				_position = renderer.transform.localPosition;
				_mode = renderer.drawMode;
				_size = renderer.size;
			}

			internal Sprite Sprite => _sprite;

			internal void Draw(SpriteRenderer renderer, ComboGlyph glyph, Sprite fallback)
			{
				if (glyph != null) glyph.Draw(renderer, _position);
				else
				{
					renderer.sprite = fallback;
					renderer.drawMode = _mode;
					if (_mode != SpriteDrawMode.Simple) renderer.size = _size;
					renderer.transform.localPosition = _position;
				}
			}
		}

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

		public void Setup(bool _useAllPerfectEffect, LiveComboSkin skin = null)
		{
			this._useAllPerfectEffect = _useAllPerfectEffect;
			var layout = skin?.Layout;
			if (numberRoot != null && numberRoot.parent != null) _layoutRoot ??= new LayoutNode(numberRoot.parent);
			if (numberRoot != null) _digitsRow ??= new LayoutNode(numberRoot);
			if (effectNumberRoot != null) _burstRow ??= new LayoutNode(effectNumberRoot);
			if (allPerfectEffectRoot != null) _glowRow ??= new LayoutNode(allPerfectEffectRoot);
			_layoutRoot?.Place(layout?.Position);
			_layoutRoot?.Scale(layout?.Scale);
			_digitsRow?.Place(layout?.DigitsPosition);
			_burstRow?.Place(layout?.BurstPosition);
			_glowRow?.Place(layout?.GlowPosition);

			_normalGlyphs = BindDigits("combo.digit.", numberSprites);
			_apGlyphs = BindDigits("combo.ap.digit.", allPerfectNumberSprites);
			_glowGlyphs = BindDigits("combo.glow.digit.", allPerfectEffectSprites);
			_label = skin?.Resolve("combo.label", comboTextSprite);
			_apLabel = skin?.Resolve("combo.ap.label", allPerfectComboTextSprite);
			if (comboSpriteRenderer != null) _textBaseline ??= new LabelBaseline(comboSpriteRenderer);
			if (apEffectComboRenderer != null)
			{
				_glowBaseline ??= new LabelBaseline(apEffectComboRenderer);
				_glowLabel = skin?.Resolve("combo.glow.label", _glowBaseline.Sprite);
				_glowBaseline.Draw(apEffectComboRenderer, _glowLabel, _glowBaseline.Sprite);
			}
			_numberSpriteRenderers = numberRoot != null ? numberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_effectNumberSpriteRenderers = effectNumberRoot != null ? effectNumberRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_allPerfectEffectRenderers = allPerfectEffectRoot != null ? allPerfectEffectRoot.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
			_activeGlowOpacity = new float[_allPerfectEffectRenderers.Length];
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

			ComboGlyph[] BindDigits(string prefix, Sprite[] sprites)
			{
				if (skin == null) return null;
				var glyphs = new ComboGlyph[10];
				for (int i = 0; i < glyphs.Length; i++)
					glyphs[i] = skin.Resolve(prefix + i, sprites != null && i < sprites.Length ? sprites[i] : null);
				return glyphs;
			}
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
			combo = Mathf.Max(0, combo);
			bool ap = isApScore && _useAllPerfectEffect;
			if (_currentCombo == combo && _isAllPerfect == ap)
			{
				return;
			}
			_time = 0f;
			_currentCombo = combo;
			transform.localScale = combo > 0 ? Vector3.one : Vector3.zero;

			Sprite[] digitSprites = numberSprites;
			Sprite textSprite = comboTextSprite;
			_isAllPerfect = ap;
			var glyphs = ap ? _apGlyphs : _normalGlyphs;
			if (_isAllPerfect)
			{
				digitSprites = allPerfectNumberSprites;
				textSprite = allPerfectComboTextSprite;
			}
			if (comboSpriteRenderer != null)
			{
				if (_textBaseline == null) comboSpriteRenderer.sprite = textSprite;
				else _textBaseline.Draw(comboSpriteRenderer, ap ? _apLabel : _label, textSprite);
			}
			if (allPerfectEffectRoot != null)
			{
				allPerfectEffectRoot.gameObject.SetActive(_isAllPerfect);
			}
			float pulse = GlowPulse;
			if (apEffectComboRenderer != null)
			{
				apEffectComboRenderer.gameObject.SetActive(_isAllPerfect);
				SetRendererAlpha(apEffectComboRenderer, pulse * (_glowLabel?.Opacity ?? 1f));
			}

			int digitCount = combo > 0 ? Mathf.FloorToInt(Mathf.Log10(combo)) + 1 : 1;
			float right = 0f;
			int value = combo;
			for (int i = 0; i < digitCount; i++)
			{
				right += (glyphs?[value % 10]?.Advance ?? DEFAULT_ADVANCE) * .5f;
				value /= 10;
			}
			value = combo;
			SpriteRenderer[] mainRenderers = NumberSpriteRenderers ?? new SpriteRenderer[0];
			SpriteRenderer[] effectRenderers = EffectNumberSpriteRenderers ?? new SpriteRenderer[0];
			for (int i = 0; i < mainRenderers.Length; i++)
			{
				bool visible = i < digitCount && combo > 0;
				int digit = value % 10;
				var glyph = glyphs?[digit];
				float advance = glyph?.Advance ?? DEFAULT_ADVANCE;
				float x = right - advance * .5f;
				right -= advance;
				ApplyComboDigit(mainRenderers[i], visible, x, digitSprites, digit, glyph);
				if (i < effectRenderers.Length)
				{
					ApplyComboDigit(effectRenderers[i], visible, x, digitSprites, digit, glyph);
				}
				if (_allPerfectEffectRenderers != null && i < _allPerfectEffectRenderers.Length)
				{
					var glow = _glowGlyphs?[digit];
					_activeGlowOpacity[i] = glow?.Opacity ?? 1f;
					ApplyComboDigit(_allPerfectEffectRenderers[i], visible, x, allPerfectEffectSprites, digit, glow);
					SetRendererAlpha(_allPerfectEffectRenderers[i], pulse * _activeGlowOpacity[i]);
				}
				value /= 10;
			}
		}

		private static float GlowPulse => (Mathf.Sin(Time.time * AP_EFFECT_SPEED) + 1f) * 0.5f;

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
				float alpha = GlowPulse;
				if (_allPerfectEffectRenderers != null)
				{
					for (int i = 0; i < _allPerfectEffectRenderers.Length; i++)
					{
						SetRendererAlpha(_allPerfectEffectRenderers[i], alpha * _activeGlowOpacity[i]);
					}
				}
				SetRendererAlpha(apEffectComboRenderer, alpha * (_glowLabel?.Opacity ?? 1f));
			}

			_time += Time.deltaTime;
		}

		public ComboView()
		{
		}

		private static void ApplyComboDigit(SpriteRenderer renderer, bool visible, float localX, Sprite[] sprites, int digit, ComboGlyph glyph)
		{
			if (renderer == null)
			{
				return;
			}
			renderer.transform.localScale = visible ? Vector3.one : Vector3.zero;
			renderer.transform.localPosition = Vector3.right * localX;
			if (visible && glyph != null) glyph.Draw(renderer, Vector3.right * localX);
			else if (visible && sprites != null && digit >= 0 && digit < sprites.Length)
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
