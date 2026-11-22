using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
    public sealed class LiveNoteSkin : LiveSpriteSkin
    {
        internal sealed class Part
        {
            internal Sprite Sprite;
            internal Vector2 Scale, Offset;
            internal Vector4 Slice;
        }

        private readonly Dictionary<Sprite, Part> _parts = new();

        internal Part ResolvePart(Sprite reference)
        {
            if (reference == null) return null;
            if (_parts.TryGetValue(reference, out var part)) return part;
            if (TryGetBinding("notes." + reference.name, out var b))
            {
                var slot = b.slot;
                var r = slot.NoteSlice;
                var rect = r == null ? new Rect(0, 0, slot.Width, slot.Height) :
                    new Rect(r[0], Mathf.Max(0, slot.Height - r[1] - r[3]),
                        Mathf.Min(r[2], slot.Width - r[0]), Mathf.Min(r[3], slot.Height - r[1]));
                Vector2 pivot = Pair(slot.Pivot, reference.pivot / reference.rect.size);
                Vector2 ratio = rect.size / reference.rect.size;
                var border = Vector4.Scale(reference.border, new Vector4(ratio.x, ratio.y, ratio.x, ratio.y));
                float ppu = reference.pixelsPerUnit * ratio.x;
                var sprite = Sprite.Create(b.texture, rect, pivot, ppu, 0, SpriteMeshType.FullRect, border);
                sprite.name = reference.name;
                Vector2 scale = Pair(slot.Scale, Vector2.one);
                var offset = Pair(slot.Position, Vector2.zero) * LiveConfig.widthX;
                part = new Part { Sprite = sprite, Scale = scale, Offset = offset,
                    Slice = new Vector4((rect.y + pivot.y * rect.height) / slot.Height,
                        slot.Height / rect.height * reference.rect.height / reference.pixelsPerUnit * scale.y, offset.y, 0) };
            }
            _parts.Add(reference, part);
            return part;
        }

        internal void BindLine(string key, Material material, out Vector2 scale, out Vector2 offset)
        {
            bool bound = TryGetBinding(key, out var b);
            if (bound) material.mainTexture = b.texture;
            scale = Pair(b.slot?.Scale, Vector2.one);
            offset = Pair(b.slot?.Position, Vector2.zero) * LiveConfig.widthX;
        }

        private static Vector2 Pair(float[] pair, Vector2 fallback) =>
            pair == null ? fallback : new Vector2(pair[0], pair[1]);

        public override void Dispose()
        {
            foreach (var part in _parts.Values)
                if (part != null) Object.Destroy(part.Sprite);
            _parts.Clear();
            base.Dispose();
        }
    }

    // Spawn chooses the actual width-dependent arrow and damage sprite before binding.
    // Restore the prefab pose before each animation step and the source sprite before reuse.
    internal sealed class NoteSkinRenderer
    {
        private static readonly int _sliceProperty = Shader.PropertyToID("_NoteSkinSlice");
        private readonly SpriteRenderer _renderer;
        private readonly bool _root;
        private readonly bool _usesSlice;
        private MaterialPropertyBlock _block;
        private Sprite _reference;
        private LiveNoteSkin.Part _part;
        private Vector2 _scale;
        private Vector3 _rawPosition, _rawScale;
        private bool _placed;

        internal NoteSkinRenderer(SpriteRenderer renderer, Transform root)
        {
            _renderer = renderer;
            _root = renderer.transform == root;
            _usesSlice = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(_sliceProperty);
        }

        internal void Bind(LiveNoteSkin skin)
        {
            _reference = _renderer.sprite;
            _part = skin.ResolvePart(_reference);
            if (_part == null) return;
            Vector2 size = _renderer.size;
            _renderer.sprite = _part.Sprite;
            _scale = _part.Scale;
            if (_renderer.drawMode == SpriteDrawMode.Simple)
                _scale.y *= (_reference.rect.height / _reference.pixelsPerUnit) / (_part.Sprite.rect.height / _part.Sprite.pixelsPerUnit);
            else
                _renderer.size = size;
            SetSlice(_part.Slice);
        }

        internal void Place()
        {
            if (_part == null || (_part.Offset == Vector2.zero && _scale == Vector2.one)) return;
            var t = _renderer.transform;
            _rawScale = t.localScale;
            _rawPosition = t.localPosition;
            t.localScale = Vector3.Scale(_rawScale, new Vector3(_scale.x, _scale.y, 1));
            t.localPosition = _rawPosition + (Vector3)_part.Offset * (_root ? _rawScale.y : 1);
            _placed = true;
        }

        internal void RestorePose()
        {
            if (!_placed) return;
            _renderer.transform.localPosition = _rawPosition;
            _renderer.transform.localScale = _rawScale;
            _placed = false;
        }

        internal void Unbind()
        {
            RestorePose();
            if (_part == null) return;
            _renderer.sprite = _reference;
            SetSlice(Vector4.zero);
            _part = null;
        }

        private void SetSlice(Vector4 slice)
        {
            if (!_usesSlice) return;
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(_sliceProperty, slice);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
