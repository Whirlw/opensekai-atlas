using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
    /// <summary>Keep the prefab's emitters, UV animation and blend modes. Replace only selected material textures.</summary>
    public sealed class LiveHitEffectSkin : LiveSpriteSkin
    {
        private readonly Dictionary<(Material original, Texture2D texture), Material> _materials = new();

        private readonly Dictionary<string, (MaterialSpriteSlot slot, HitEffectSequence.Layer[] layers, Texture2D[] textures)> _sequences = new();
        private readonly List<Sprite> _frames = new();
        private readonly List<ParticleSystemController> _controllers = new();
        internal bool Paused { get; private set; }

        internal static GameObject LoadPrefab(string fallbackBundle, string prefabName, LiveHitEffectSkin skin)
        {
            string bundle = fallbackBundle;
            if (skin != null && MaterialHitFxSlots.TryGetTemplate(prefabName, out string slot))
            {
                int? templateSet = null;
                if (skin._sequences.TryGetValue(slot, out var sequence)) templateSet = sequence.slot.TemplateSet;
                else if (skin.TryGetBinding(slot, out var binding)) templateSet = binding.slot.TemplateSet;
                if (templateSet.HasValue) bundle = LiveConfig.GetEffectBundleName(templateSet.Value - 1);
            }
            return AssetBundleUtility.LoadAsset<GameObject>(bundle, prefabName);
        }

        internal void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            foreach (var controller in _controllers)
                if (controller != null) controller.SetPackPaused(paused);
        }

        internal void ResetPlayback()
        {
            Paused = false;
            foreach (var controller in _controllers)
                if (controller != null) controller.ResetPackPlayback();
        }

        public void BindSequence(string key, MaterialSpriteSlot slot)
        {
            var layers = new HitEffectSequence.Layer[slot.Layers.Length];
            var textures = new Texture2D[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                if (!TryGetBinding(key + "/" + i, out var binding)) return; // Any broken layer falls back to the template.
                textures[i] = binding.texture;
            }
            float unit = key == "hitFx.screen.damage" ? 1 : LiveUtility.CalcLaneTransformX(1) - LiveUtility.CalcLaneTransformX(0);
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
                    layer.Sizes[j] = Pair(source.Size, new Vector2(1, rect.height / rect.width)) * Pair(source.Scale, Vector2.one) * overall * unit;
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

        internal void Apply(ParticleSystemController controller, string prefabName)
        {
            if (!MaterialHitFxSlots.TryGetTemplate(prefabName, out string key)) return;
            if (_sequences.TryGetValue(key, out var sequence))
            {
                var particles = controller.GetComponentsInChildren<ParticleSystemRenderer>(true);
                var reference = System.Array.Find(particles, r => r.sharedMaterial != null && r.sharedMaterial.name != "mat_note_tail_01_v2");
                if (reference != null)
                {
                    var materials = System.Array.ConvertAll(sequence.textures, texture => MaterialFor(reference.sharedMaterial, texture));
                    controller.SetHitLayout(sequence.slot, key == "hitFx.screen.damage");
                    _controllers.Add(controller);
                    controller.Sequence = new HitEffectSequence(this, controller.transform, sequence.layers, materials, reference, key.Contains(".hold."));
                    foreach (var particle in particles) particle.enabled = false;
                    return;
                }
            }
            TryGetBinding(key, out var primary);
            TryGetBinding(key + ".trail", out var trail);
            if (primary.texture == null && trail.texture == null) return;

            _controllers.Add(controller);
            controller.SetHitLayout(primary.slot, key == "hitFx.screen.damage");
            foreach (var renderer in controller.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;
                    var texture = original.name switch
                    {
                        "mat_note_common_all_v2" or "mat_ove_damage_add_01" => primary.texture,
                        "mat_note_tail_01_v2" => trail.texture,
                        _ => null
                    };
                    if (texture == null) continue;
                    materials[i] = MaterialFor(original, texture);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private Material MaterialFor(Material original, Texture2D texture)
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
            _controllers.Clear();
            _sequences.Clear();
            foreach (var material in _materials.Values) Object.Destroy(material);
            _materials.Clear();
            base.Dispose();
        }
    }
}
