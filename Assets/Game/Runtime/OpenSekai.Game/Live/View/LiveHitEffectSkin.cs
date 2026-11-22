using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
    /// <summary>Keep the prefab's emitters, UV animation and blend modes. Replace only selected material textures.</summary>
    public sealed class LiveHitEffectSkin : LiveEffectSkin
    {
        private readonly List<ParticleSystemController> _controllers = new();
        internal bool Paused { get; private set; }

        internal bool HasTemplateTexture(string prefabName) =>
            MaterialHitFxSlots.TryGetTemplate(prefabName, out string slot) &&
            TryGetBinding(slot, out var binding) && binding.texture != null;

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

        public override void Dispose()
        {
            _controllers.Clear();
            base.Dispose();
        }
    }
}
