using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace Sekai
{
    public sealed class LiveResultEffectSkin : LiveEffectSkin
    {
        private readonly Dictionary<string, MaterialSpriteSlot> _layouts = new();

        public void BindLayout(string key, MaterialSpriteSlot layout) => _layouts.Add(key, layout);

        internal bool HasSequence(string key) => _sequences.ContainsKey(key);

        internal HitEffectSequence CreateSequence(string key, Transform root, int order)
        {
            if (!_sequences.TryGetValue(key, out var sequence)) return null;
            // Frame sizes already include the event scale. Position moves every layer together.
            SetPose(root, sequence.slot.Position, null);
            var player = new HitEffectSequence(null, root, sequence.layers, null, root.gameObject.layer, 0, order, false);
            player.Play(0);
            return player;
        }

        internal void ApplyTemplate(string key, GameObject instance)
        {
            if (_layouts.TryGetValue(key + ".layout", out var layout))
            {
                SetPose(instance.transform, layout.Position, layout.Scale);
                if (layout.Scale != null && (layout.Scale[0] != 1f || layout.Scale[1] != 1f))
                    foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = particle.main;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    }
            }
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer sprite)
                {
                    var glyph = Resolve(key + "." + MaterialResultFxSlots.SpritePart(sprite.name), sprite.sprite);
                    if (glyph == null) continue;
                    var scale = sprite.transform.localScale;
                    glyph.Draw(sprite, sprite.transform.localPosition);
                    sprite.transform.localScale = scale;
                    continue;
                }
                string part = renderer is ParticleSystemRenderer ? ".particles" : ".mesh";
                if (!TryGetBinding(key + part, out var binding)) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].HasProperty("_MainTex"))
                        materials[i] = MaterialFor(materials[i], binding.texture);
                renderer.sharedMaterials = materials;
            }
        }

        internal bool DrawBackground(SpriteRenderer renderer, SpriteRendererBaseline baseline)
        {
            if (!TryGetBinding(MaterialResultFxSlots.BACKGROUND, out var binding)) return false;
            baseline.Draw(renderer, Resolve(MaterialResultFxSlots.BACKGROUND, baseline.Sprite), baseline.Sprite, true);
            if (binding.slot.Size == null && baseline.Mode != SpriteDrawMode.Simple)
            {
                var scale = binding.slot.Scale;
                renderer.size = scale == null ? baseline.Size : Vector2.Scale(baseline.Size, new Vector2(scale[0], scale[1]));
            }
            return true;
        }

        private static void SetPose(Transform root, float[] position, float[] scale)
        {
            if (position != null)
                root.localPosition = new Vector3(position[0] / MaterialGlyph.REFERENCE_PPU, position[1] / MaterialGlyph.REFERENCE_PPU, root.localPosition.z);
            if (scale != null) root.localScale = Vector3.Scale(root.localScale, new Vector3(scale[0], scale[1], 1));
        }
    }
}
