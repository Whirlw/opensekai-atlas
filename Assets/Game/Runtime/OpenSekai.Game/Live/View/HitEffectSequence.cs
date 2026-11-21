using System;
using OpenSekai.MaterialPacks;
using UnityEngine;

namespace Sekai
{
    internal sealed class HitEffectSequence
    {
        // Frame sprites and durations belong to the session. Pool instances only own renderers and clocks.
        internal sealed class Layer
        {
            internal Sprite[] Sprites;
            internal int[] Ends;
            internal int LoopStart = -1;
            internal int LoopEnd;
            internal Vector2 Offset;
            internal Vector2[] Sizes;
        }

        private sealed class Player
        {
            internal Layer Layer;
            internal SpriteRenderer Renderer;
            internal double TimeMs;
            internal int LastFrame = -1;
        }

        private readonly Player[] _players;
        private readonly Renderer[] _renderers;
        private readonly LiveHitEffectSkin _skin;
        private readonly bool _hold;
        private int _style;
        private MaterialPropertyBlock _palette;
        private bool _releasing;
        internal bool Active { get; private set; }
        internal bool Holding => Active && !_releasing;

        internal HitEffectSequence(LiveHitEffectSkin skin, Transform parent, Layer[] layers, Material[] materials,
            ParticleSystemRenderer reference, bool hold)
        {
            _skin = skin;
            _hold = hold;
            _players = new Player[layers.Length];
            _renderers = new Renderer[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var go = new GameObject("Hit effect layer " + i) { layer = reference.gameObject.layer };
                go.transform.SetParent(parent, false);
                go.transform.localPosition = layers[i].Offset;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                renderer.sharedMaterial = materials[i];
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder + i;
                _players[i] = new Player { Layer = layers[i], Renderer = renderer };
                _renderers[i] = renderer;
            }
        }

        internal void Play(int style)
        {
            _releasing = false;
            Active = true;
            foreach (var player in _players)
            {
                player.TimeMs = 0;
                player.LastFrame = -1;
            }
            SetStyle(style);
            Advance(0);
        }

        internal void SetStyle(int style) => NextRushPalette.Apply(_renderers, style, ref _style, ref _palette);

        internal void Release()
        {
            if (!Active || _releasing) return;
            _releasing = true;
            foreach (var player in _players)
                if (player.Layer.LoopStart >= 0) player.TimeMs = player.Layer.LoopEnd;
            Advance(0);
        }

        internal void Advance(float dt)
        {
            if (!Active || _skin.Paused) return;
            bool visible = false;
            foreach (var p in _players)
            {
                var layer = p.Layer;
                p.TimeMs += dt * 1000d;
                if (!_releasing && layer.LoopStart >= 0 && p.TimeMs >= layer.LoopEnd)
                    p.TimeMs = layer.LoopStart + (p.TimeMs - layer.LoopStart) % (layer.LoopEnd - layer.LoopStart);
                if (p.TimeMs >= layer.Ends[layer.Ends.Length - 1])
                {
                    p.Renderer.enabled = false;
                    continue;
                }
                visible = true;
                int frame = Array.BinarySearch(layer.Ends, (int)p.TimeMs);
                frame = frame < 0 ? ~frame : frame + 1;
                if (frame == p.LastFrame) continue;
                p.LastFrame = frame;
                var sprite = layer.Sprites[frame];
                var scale = layer.Sizes[frame] / sprite.rect.size;
                p.Renderer.sprite = sprite;
                p.Renderer.transform.localScale = new Vector3(scale.x, scale.y, 1);
                p.Renderer.enabled = true;
            }
            // A hold layer without a loop plays once per press, even when every layer is already hidden.
            Active = visible || _hold && !_releasing;
        }

        internal void Stop()
        {
            Active = false;
            foreach (var player in _players) player.Renderer.enabled = false;
        }
    }
}
