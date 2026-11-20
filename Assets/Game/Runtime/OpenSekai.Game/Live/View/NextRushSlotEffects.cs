using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	internal sealed class NextRushSlotEffects
	{
		private sealed class Slot
		{
			internal SpriteRenderer Strike;
			internal SpriteRenderer Glow;
			internal float Start;
			internal float Width;
			internal Vector2 Center;
			internal Color Color;
			internal float StrikeDuration;
			internal float GlowDuration;
		}

		private readonly Transform _parent;
		private readonly List<SpriteRenderer> _holdGlows = new List<SpriteRenderer>();
		private Slot[] _slots;
		private Texture2D _texture;
		private Sprite _strike;
		private Sprite _glow;

		internal NextRushSlotEffects(Transform parent) => _parent = parent;

		internal static bool Supports(NoteCategory category) =>
			category is NoteCategory.Normal or NoteCategory.Long or NoteCategory.Flick;

		internal void Play(NoteCategory category, NoteType type, int style, float start, float end, float time)
		{
			if (!Supports(category) || start >= end) return;
			var positions = LiveConfig.JudgmentPositions;
			_slots ??= new Slot[positions.Length];
			bool critical = type == NoteType.Critical;
			var color = SlotColor(category, critical, style);
			var (strikeDuration, glowDuration) = category switch
			{
				NoteCategory.Long => critical ? (0.65f, 0.30f) : (0.45f, 0.40f),
				NoteCategory.Flick when !critical => (0.50f, 0.35f),
				_ => (0.48f, 0.25f)
			};
			int first = Mathf.Max(0, Mathf.FloorToInt(start + 0.5f));
			int last = Mathf.Min(positions.Length - 1, Mathf.CeilToInt(end - 0.5f));
			for (int lane = first; lane <= last; lane++)
			{
				float left = Mathf.Max(start, lane - 0.5f), right = Mathf.Min(end, lane + 0.5f);
				if (right <= left) continue;
				var slot = _slots[lane] ??= CreateSlot();
				slot.Start = time;
				slot.Color = color;
				slot.Width = (right - left) * LiveConfig.widthX;
				slot.Center = positions[lane] + Vector2.right * (((left + right) * 0.5f - lane) * LiveConfig.widthX);
				slot.StrikeDuration = strikeDuration;
				slot.GlowDuration = glowDuration;
				Draw(slot, 0f);
			}
		}

		internal SpriteRenderer CreateHoldGlow()
		{
			EnsureSprites();
			var renderer = CreateRenderer(_glow, "Hold slot glow", 34);
			renderer.enabled = false;
			_holdGlows.Add(renderer);
			return renderer;
		}

		internal static void DrawHoldGlow(SpriteRenderer renderer, float left, float right, bool critical, int style, float age)
		{
			float pulse = Mathf.Sin(Mathf.Repeat(age, 0.25f) * (Mathf.PI * 4f));
			float height = 1.8f + 0.5f * pulse;
			float alpha = Mathf.Clamp01(age / 0.25f) * (0.21f + 0.08f * pulse);
			var center = LiveConfig.NativeLanePosition((left + right) * 0.5f) + Vector2.up * (height * 0.5f);
			float width = (right - left + 1f) * LiveConfig.widthX;
			Set(renderer, center, width, height, SlotColor(NoteCategory.Long, critical, style), alpha);
		}

		private static Color SlotColor(NoteCategory category, bool critical, int style) => style switch
		{
			1 => Color.gray,
			> 1 => NextRushConnectorSegment.PaletteColor(style),
			_ when critical => NextRushConnectorSegment.PaletteColor(5),
			_ when category == NoteCategory.Long => NextRushConnectorSegment.PaletteColor(3),
			_ when category == NoteCategory.Flick => NextRushConnectorSegment.PaletteColor(2),
			_ => NextRushConnectorSegment.PaletteColor(7)
		};

		internal void Update(float time)
		{
			if (_slots == null) return;
			foreach (var slot in _slots)
				if (slot != null && (slot.Strike.enabled || slot.Glow.enabled))
					Draw(slot, Mathf.Max(0f, time - slot.Start));
		}

		private static void Draw(Slot slot, float elapsed)
		{
			float strikeTime = Mathf.Clamp01(elapsed / slot.StrikeDuration);
			float glowTime = Mathf.Clamp01(elapsed / slot.GlowDuration);
			float height = Mathf.Lerp(1.15f, 2.3f, Mathf.Sin(Mathf.PI * 0.5f * glowTime));
			Set(slot.Strike, slot.Center, slot.Width * 0.97f, 0.32f, slot.Color, 0.72f * (1f - strikeTime) * (1f - strikeTime));
			Set(slot.Glow, slot.Center + Vector2.up * (height * 0.5f), slot.Width, height, slot.Color, 0.35f * (1f - glowTime));
		}

		private static void Set(SpriteRenderer renderer, Vector2 center, float width, float height, Color color, float alpha)
		{
			renderer.enabled = alpha > 0f;
			if (!renderer.enabled) return;
			renderer.transform.localPosition = new Vector3(center.x, center.y, 0f);
			renderer.transform.localScale = new Vector3(width, height, 1f);
			color.a = alpha;
			renderer.color = color;
		}

		private Slot CreateSlot()
		{
			EnsureSprites();
			return new Slot
			{
				Strike = CreateRenderer(_strike, "Slot strike", -12),
				Glow = CreateRenderer(_glow, "Slot glow", 34)
			};
		}

		private void EnsureSprites()
		{
			const int size = 32;
			if (_texture == null)
			{
				_texture = new Texture2D(size, size * 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
				var pixels = new Color[size * size * 2];
				for (int y = 0; y < size; y++)
				{
					float height = y / (size - 1f);
					for (int x = 0; x < size; x++)
					{
						float edge = Mathf.Min(x, size - 1 - x) / (size - 1f);
						float side = Mathf.Clamp01(edge * 8f);
						pixels[y * size + x] = new Color(1f, 1f, 1f, side * (1f - Mathf.Abs(height * 2f - 1f)));
						pixels[(y + size) * size + x] = new Color(1f, 1f, 1f, side * (1f - height) * (1f - height));
					}
				}
				_texture.SetPixels(pixels);
				_texture.Apply(false, true);
				_strike = Sprite.Create(_texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
				_glow = Sprite.Create(_texture, new Rect(0, size, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
			}
		}

		private SpriteRenderer CreateRenderer(Sprite sprite, string name, int sortingOrder)
		{
			var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
			renderer.gameObject.layer = _parent.gameObject.layer;
			renderer.transform.SetParent(_parent, false);
			renderer.sprite = sprite;
			renderer.sortingOrder = sortingOrder;
			return renderer;
		}

		internal void Clear()
		{
			foreach (var glow in _holdGlows) glow.enabled = false;
			if (_slots == null) return;
			foreach (var slot in _slots)
				if (slot != null) slot.Strike.enabled = slot.Glow.enabled = false;
		}

		internal void Dispose()
		{
			Clear();
			foreach (var glow in _holdGlows) Object.Destroy(glow.gameObject);
			_holdGlows.Clear();
			if (_slots != null)
				foreach (var slot in _slots)
					if (slot != null)
					{
						Object.Destroy(slot.Strike.gameObject);
						Object.Destroy(slot.Glow.gameObject);
					}
			Object.Destroy(_strike);
			Object.Destroy(_glow);
			Object.Destroy(_texture);
			_slots = null;
		}
	}
}
