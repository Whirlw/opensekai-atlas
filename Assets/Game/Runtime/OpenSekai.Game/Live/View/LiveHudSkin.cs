using System.Collections.Generic;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace Sekai
{
    public sealed class LiveHudSkin : LiveSpriteSkin
    {
        private readonly Dictionary<string, MaterialSpriteSlot> _layouts = new();
        public void BindLayout(string key, MaterialSpriteSlot layout) => _layouts.Add(key, layout);
        internal MaterialSpriteSlot Layout(string key) => _layouts.TryGetValue(key, out var spec) ? spec : null;

        internal Texture2D Ring(string key, out MaterialSpriteSlot spec)
        {
            TryGetBinding(key, out var binding);
            spec = binding.slot;
            return binding.texture;
        }

        internal bool HasScoreBackdrop => TryGetBinding("hud.score.base", out _) || TryGetBinding("hud.score.gaugeBase", out _);

        internal Vector3 Position(string key, Vector3 anchor)
        {
            if (!TryGetBinding(key, out var binding) || binding.slot.Position == null) return anchor;
            var p = MaterialGlyph.Point(binding.slot.Position);
            return new Vector3(p.x, p.y, anchor.z);
        }

        internal bool Draw(string key, SpriteRenderer renderer, SpriteRendererBaseline original, Sprite fallback, bool cell = false)
        {
            if (!TryGetBinding(key, out var binding)) return false;
            var glyph = Resolve(key, fallback);
            glyph.Draw(renderer, original.Position, cell);
            var spec = binding.slot;
            if (spec.DrawMode == "tile" || spec.DrawMode == null && original.Mode == SpriteDrawMode.Tiled)
                renderer.drawMode = SpriteDrawMode.Tiled;
            if (original.Mode != SpriteDrawMode.Simple && spec.Size == null)
                renderer.size = Vector2.Scale(original.Size, spec.Scale == null ? Vector2.one : new Vector2(spec.Scale[0], spec.Scale[1]));
            renderer.transform.localScale = original.Scale;
            return true;
        }
    }

    // These baselines belong to the instantiated prefab, not to a particular pack or attempt.
    internal sealed class HudViewBindings
    {
        private readonly Dictionary<SpriteRenderer, SpriteRendererBaseline> _sprites = new();
        private readonly Dictionary<Transform, (Vector3 position, Vector3 scale)> _poses = new();
        private readonly Dictionary<ParticleSystemRenderer, MaterialPropertyBlock> _ringBlocks = new();
        private static readonly int _mainTex = Shader.PropertyToID("_MainTex");

        internal void Setup(Transform root, LiveHudSkin skin)
        {
            foreach (var entry in MaterialHudSlots.Nodes)
            {
                var node = root.Find(entry.Value);
                if (node == null) continue;
                if (MaterialHudSlots.IsLayout(entry.Key))
                {
                    ApplyPose(node, skin?.Layout(entry.Key));
                    if (node.TryGetComponent<NumberView>(out var number))
                        number.SetHudSkin(skin, entry.Key.Substring(0, entry.Key.Length - ".layout".Length));
                }
                else if (MaterialHudSlots.IsRing(entry.Key))
                {
                    if (!node.TryGetComponent<ParticleSystemRenderer>(out var ring)) continue;
                    MaterialSpriteSlot spec = null;
                    var texture = skin?.Ring(entry.Key, out spec);
                    ApplyPose(node, spec);
                    var groupScale = skin?.Layout("hud.countdown.layout")?.Scale;
                    if (groupScale != null && node.TryGetComponent<ParticleSystem>(out var particle)
                        && particle.main.scalingMode == ParticleSystemScalingMode.Local)
                        node.localScale = Vector3.Scale(node.localScale, new Vector3(groupScale[0], groupScale[1], 1));
                    if (!_ringBlocks.TryGetValue(ring, out var original))
                    {
                        original = new MaterialPropertyBlock();
                        ring.GetPropertyBlock(original);
                        _ringBlocks.Add(ring, original);
                    }
                    ring.SetPropertyBlock(original.isEmpty ? null : original);
                    if (texture == null) continue;
                    var block = new MaterialPropertyBlock();
                    ring.GetPropertyBlock(block);
                    block.SetTexture(_mainTex, texture);
                    ring.SetPropertyBlock(block);
                }
                else if (node.TryGetComponent<SpriteRenderer>(out var renderer))
                {
                    if (!_sprites.TryGetValue(renderer, out var baseline))
                        _sprites.Add(renderer, baseline = new SpriteRendererBaseline(renderer));
                    if (skin?.Draw(entry.Key, renderer, baseline, baseline.Sprite) != true)
                        baseline.Draw(renderer, null, baseline.Sprite, true);
                    // The score plate and its trough share a prefab order. Separate textures can reverse their batch order.
                    if (entry.Key is "hud.score.base" or "hud.score.skill")
                        renderer.sortingOrder = baseline.Order - (skin?.HasScoreBackdrop == true ? 1 : 0);
                }
            }
        }

        private void ApplyPose(Transform node, MaterialSpriteSlot spec)
        {
            if (!_poses.TryGetValue(node, out var pose))
                _poses.Add(node, pose = (node.localPosition, node.localScale));
            Vector3 position = pose.position;
            if (spec?.Position != null)
            {
                var xy = MaterialGlyph.Point(spec.Position);
                position = new Vector3(xy.x, xy.y, position.z);
            }
            node.localPosition = position;
            node.localScale = spec?.Scale == null ? pose.scale :
                Vector3.Scale(pose.scale, new Vector3(spec.Scale[0], spec.Scale[1], 1));
        }
    }
}
