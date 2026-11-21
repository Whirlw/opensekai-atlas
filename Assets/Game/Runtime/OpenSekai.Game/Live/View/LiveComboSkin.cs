using System;
using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace Sekai
{
    /// <summary>Textures belong to the live session. Bind each slot once against its prefab reference box.</summary>
    public sealed class LiveComboSkin : IDisposable
    {
        private readonly HashSet<Texture2D> _textures = new();
        private readonly Dictionary<string, (Texture2D texture, MaterialSpriteSlot slot)> _slots = new();
        private readonly Dictionary<(string key, Sprite reference), ComboGlyph> _glyphs = new();

        public void Bind(string key, Texture2D texture, MaterialSpriteSlot slot)
        {
            _textures.Add(texture);
            _slots.Add(key, (texture, slot));
        }

        internal MaterialComboLayout Layout { get; }
        public LiveComboSkin(MaterialComboLayout layout = null) => Layout = layout;

        internal ComboGlyph Resolve(string key, Sprite reference)
        {
            if (!_slots.TryGetValue(key, out var binding)) return null;
            if (_glyphs.TryGetValue((key, reference), out var cached)) return cached;
            var slot = binding.slot;
            var r = slot.Rect;
            var rect = new Rect(r[0], slot.Height - r[1] - r[3], r[2], r[3]);
            var pivot = reference != null ? reference.pivot / reference.rect.size : Vector2.one * .5f;
            if (slot.Pivot != null) pivot = new Vector2(slot.Pivot[0], slot.Pivot[1]);
            var sprite = Sprite.Create(binding.texture, rect, pivot, ComboGlyph.REFERENCE_PPU, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            var size = reference != null ? reference.rect.size / reference.pixelsPerUnit : rect.size / ComboGlyph.REFERENCE_PPU;
            if (slot.Size != null) size = ComboGlyph.Point(slot.Size);
            if (slot.Scale != null) size *= new Vector2(slot.Scale[0], slot.Scale[1]);
            var offset = slot.Offset == null ? Vector2.zero : new Vector2(slot.Offset[0], slot.Offset[1]) / ComboGlyph.REFERENCE_PPU;
            Vector2? position = slot.Position == null ? (Vector2?)null : ComboGlyph.Point(slot.Position);
            var glyph = new ComboGlyph(sprite, size, offset, slot.Advance / ComboGlyph.REFERENCE_PPU, position, slot.Opacity ?? 1f);
            _glyphs.Add((key, reference), glyph);
            return glyph;
        }

        public void Dispose()
        {
            foreach (var glyph in _glyphs.Values) UnityEngine.Object.Destroy(glyph.Sprite);
            foreach (var texture in _textures) UnityEngine.Object.Destroy(texture);
            _glyphs.Clear();
            _textures.Clear();
            _slots.Clear();
        }
    }

    internal sealed class ComboGlyph
    {
        internal const float REFERENCE_PPU = 108f;
        internal readonly Sprite Sprite;
        internal readonly Vector2 Size;
        internal readonly Vector2 Offset;
        internal readonly float? Advance;
        internal readonly float Opacity;
        private readonly Vector2? _position;

        internal static Vector2 Point(float[] pair) => new Vector2(pair[0], pair[1]) / REFERENCE_PPU;

        internal ComboGlyph(Sprite sprite, Vector2 size, Vector2 offset, float? advance, Vector2? position, float opacity)
        {
            Sprite = sprite;
            Size = size;
            Offset = offset;
            Advance = advance;
            _position = position;
            Opacity = opacity;
        }

        internal void Draw(SpriteRenderer renderer, Vector3 anchor)
        {
            renderer.sprite = Sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = Size;
            if (_position.HasValue) anchor = new Vector3(_position.Value.x, _position.Value.y, anchor.z);
            renderer.transform.localPosition = anchor + (Vector3)Offset;
        }
    }
}
