using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
    public abstract class LiveEffectSkin : LiveSpriteSkin
    {
        private readonly Dictionary<(Material original, Texture2D texture), Material> _materials = new();

        private protected readonly Dictionary<string, (MaterialSpriteSlot slot, HitEffectSequence.Layer[] layers, Texture2D[] textures)> _sequences = new();
        private readonly List<Sprite> _frames = new();

        public void BindSequence(string key, MaterialSpriteSlot slot)
        {
            var layers = new HitEffectSequence.Layer[slot.Layers.Length];
            var textures = new Texture2D[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                if (!TryGetBinding(key + "/" + i, out var binding)) return; // Any broken layer falls back to the template.
                textures[i] = binding.texture;
            }
            bool result = MaterialResultFxSlots.IsEvent(key);
            float unit = key switch
            {
                _ when result => 1f / MaterialGlyph.REFERENCE_PPU,
                "hitFx.screen.damage" => 1f,
                _ => LiveUtility.CalcLaneTransformX(1) - LiveUtility.CalcLaneTransformX(0)
            };
            Vector2 overall = Pair(slot.Scale, Vector2.one);
            for (int i = 0; i < layers.Length; i++)
            {
                var source = slot.Layers[i];
                int count = source.Frames.Length;
                var layer = new HitEffectSequence.Layer { Sprites = new Sprite[count], Sizes = new Vector2[count],
                    Ends = new int[count], Offset = Pair(source.Offset, Vector2.zero) * overall * unit };
                var pivot = Pair(source.Pivot, Vector2.one * .5f);
                for (int j = 0; j < count; j++)
                {
                    var frame = source.Frames[j];
                    var r = frame.Rect;
                    var rect = new Rect(r[0], source.Height - r[1] - r[3], r[2], r[3]);
                    var sprite = Sprite.Create(textures[i], rect, pivot, 1f, 0, SpriteMeshType.FullRect);
                    sprite.name = key + "/" + i + "/" + j;
                    _frames.Add(sprite);
                    layer.Sprites[j] = sprite;
                    layer.Sizes[j] = Pair(source.Size, result ? rect.size : new Vector2(1, rect.height / rect.width)) * Pair(source.Scale, Vector2.one) * overall * unit;
                    layer.Ends[j] = (j == 0 ? 0 : layer.Ends[j - 1]) + frame.DurationMs;
                }
                if (source.LoopFrameRange != null)
                {
                    int start = source.LoopFrameRange[0];
                    layer.LoopStart = start == 0 ? 0 : layer.Ends[start - 1];
                    layer.LoopEnd = layer.Ends[source.LoopFrameRange[1] - 1];
                }
                layers[i] = layer;
            }
            _sequences.Add(key, (slot, layers, textures));
        }

        private static Vector2 Pair(float[] pair, Vector2 fallback) => pair == null ? fallback : new Vector2(pair[0], pair[1]);

        private protected Material MaterialFor(Material original, Texture2D texture)
        {
            if (_materials.TryGetValue((original, texture), out var material)) return material;
            material = new Material(original) { mainTexture = texture };
            _materials.Add((original, texture), material);
            return material;
        }

        public override void Dispose()
        {
            foreach (var frame in _frames) Object.Destroy(frame);
            _frames.Clear();
            _sequences.Clear();
            foreach (var material in _materials.Values) Object.Destroy(material);
            _materials.Clear();
            base.Dispose();
        }
    }
}
