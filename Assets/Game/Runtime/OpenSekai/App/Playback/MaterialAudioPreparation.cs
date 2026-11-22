using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.Playback
{
    internal sealed class MaterialAudioPreparation
    {
        private readonly IReadOnlyList<MaterialAudioBinding> _bindings;
        private readonly IReadOnlyList<MaterialSpriteBinding> _sprites;
        internal LiveMaterialAudio Audio { get; }
        private readonly IReadOnlyList<MaterialSpriteBinding> _judgmentSprites;
        private readonly IReadOnlyList<MaterialSpriteBinding> _hitSprites;
        private readonly IReadOnlyList<MaterialSpriteBinding> _noteSprites;
        private readonly IReadOnlyList<MaterialSpriteBinding> _laneSprites;
        private readonly MaterialBackgroundBinding _background;
        internal LiveBackgroundSkin Background { get; }
        private readonly IReadOnlyList<MaterialSpriteBinding> _hudSprites;
        private readonly IReadOnlyList<MaterialSpriteBinding> _resultSprites;
        internal LiveResultEffectSkin ResultFx { get; }
        internal LiveHudSkin Hud { get; }
        internal LiveSpriteSkin Lane { get; }
        internal LiveNoteSkin Notes { get; }
        internal LiveHitEffectSkin HitFx { get; }
        internal LiveSpriteSkin Judgment { get; }
        internal LiveComboSkin Combo { get; }

        /// <summary>The built-in fallback sets fixed for this live, merged into its settings snapshot.</summary>
        internal MaterialBuiltinSelection Builtin { get; }

        /// <summary>A live's preparation: holds the store until the live's audio is released.</summary>
        internal MaterialAudioPreparation(MaterialPackStore store) : this(store.Capture(), store.Release)
        {
        }

        /// <summary>
        /// Prepares <paramref name="bindings"/>; <paramref name="release"/> runs when the audio is
        /// disposed (null for the resource pack page's demo, which holds nothing).
        /// </summary>
        internal MaterialAudioPreparation(MaterialPackBindings bindings, Action release)
        {
            _bindings = bindings.Audio;
            _sprites = bindings.Combo;
            var layout = bindings.ComboLayout;
            _judgmentSprites = bindings.Judgment;
            _hitSprites = bindings.HitFx;
            _noteSprites = bindings.Notes;
            _laneSprites = bindings.Lane;
            _background = bindings.Background;
            _hudSprites = bindings.Hud;
            _resultSprites = bindings.ResultFx;
            // The bindings were taken under one lock, so the sets match the groups.
            Builtin = bindings.Builtin.Copy();
            if (_resultSprites.Any(x => x.Source != null)) ResultFx = new LiveResultEffectSkin();
            if (_hudSprites.Any(x => x.Source != null)) Hud = new LiveHudSkin();
            if (_background?.Source != null) Background = new LiveBackgroundSkin(_background.Media);
            if (_laneSprites.Any(x => x.Source != null)) Lane = new LiveSpriteSkin();
            if (_noteSprites.Any(x => x.Source != null)) Notes = new LiveNoteSkin();
            if (_hitSprites.Any(x => x.Source != null)) HitFx = new LiveHitEffectSkin();
            if (_judgmentSprites.Any(x => x.Source != null)) Judgment = new LiveSpriteSkin();
            if (layout != null || _sprites.Any(x => x.Source != null)) Combo = new LiveComboSkin(layout);
            Audio = new LiveMaterialAudio(release);
        }

        internal async Task PrepareAsync(List<string> diagnostics, CancellationToken token)
        {
            if (Hud != null)
            {
                foreach (var binding in _hudSprites.Where(b => b.Source != null && MaterialHudSlots.IsLayout(b.Slot)))
                    Hud.BindLayout(binding.Slot, binding.Media);
                await PrepareSpritesAsync(_hudSprites.Where(b => !MaterialHudSlots.IsLayout(b.Slot)).ToArray(), Hud, diagnostics, token);
            }
            if (Background != null) await PrepareSpritesAsync(_background.Images().ToArray(), Background, diagnostics, token);
            await PrepareSpritesAsync(_laneSprites, Lane, diagnostics, token);
            await PrepareSpritesAsync(_noteSprites, Notes, diagnostics, token);
            await PrepareSpritesAsync(_sprites, Combo, diagnostics, token);
            await PrepareSpritesAsync(_judgmentSprites, Judgment, diagnostics, token);
            if (HitFx != null) await PrepareEffectsAsync(_hitSprites, HitFx, diagnostics, token);
            if (ResultFx != null)
            {
                foreach (var binding in _resultSprites.Where(b => b.Source != null && MaterialResultFxSlots.IsLayout(b.Slot)))
                    ResultFx.BindLayout(binding.Slot, binding.Media);
                await PrepareEffectsAsync(_resultSprites, ResultFx, diagnostics, token);
            }
            var cache = new Dictionary<(string hash, int start, int end), AudioClip>();
            foreach (var file in _bindings.Where(x => x.Source != null).GroupBy(x => x.Path))
            {
                token.ThrowIfCancellationRequested();
                byte[] bytes;
                MaterialPackWave wave;
                try
                {
                    bytes = await Task.Run(() => Read(file.Key, file.First().Media), token);
                    wave = MaterialPackWave.Read(bytes);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    diagnostics.Add(string.Join(", ", file.Select(x => x.Slot)) + " (" + file.First().Media.File + ")：" + e.Message);
                    continue;
                }
                foreach (var binding in file)
                {
                    try
                    {
                        var media = binding.Media;
                        var clip = await GetClipAsync(media, media.Start, media.End);
                        var hit = MaterialPackAudio.AlsoUsesOneShot(binding.Slot) ? await GetClipAsync(media, 0, wave.Frames) : null;
                        string cue = MaterialPackAudio.Cue(binding.Slot);
                        Audio.Bind(cue, MaterialPackAudio.UsesLoop(binding.Slot), clip);
                        if (hit != null) Audio.Bind(cue, false, hit);
                    }
                    catch (Exception e) when (e is InvalidDataException or UnityException)
                    {
                        diagnostics.Add(binding.Slot + " (" + binding.Media.File + ")：" + e.Message);
                    }
                }

                async Task<AudioClip> GetClipAsync(MaterialAudioSlot media, int start, int end)
                {
                    var key = (media.Hash, start, end);
                    if (cache.TryGetValue(key, out var clip)) return clip;
                    float[] samples = await Task.Run(() => wave.Decode(bytes, start, end), token);
                    token.ThrowIfCancellationRequested();
                    clip = Audio.CreateClip(media.File, end - start, wave.Channels, wave.Rate, samples);
                    cache.Add(key, clip);
                    return clip;
                }
            }
        }

        private static async Task PrepareEffectsAsync(IReadOnlyList<MaterialSpriteBinding> bindings, LiveEffectSkin skin,
            List<string> diagnostics, CancellationToken token)
        {
            var selected = bindings.Where(b => b.Source != null).ToArray();
            await PrepareSpritesAsync(selected.SelectMany(b => b.Images()).ToArray(), skin, diagnostics, token);
            foreach (var binding in selected)
                if (binding.Media.Layers != null) skin.BindSequence(binding.Slot, binding.Media);
        }

        private static async Task PrepareSpritesAsync(IReadOnlyList<MaterialSpriteBinding> bindings, LiveSpriteSkin skin,
            List<string> diagnostics, CancellationToken token)
        {
            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var file in bindings.Where(x => x.Source != null).GroupBy(x => x.Path))
            {
                token.ThrowIfCancellationRequested();
                var media = file.First().Media;
                try
                {
                    byte[] bytes = await Task.Run(() => Read(file.Key, media), token);
                    token.ThrowIfCancellationRequested();
                    if (!textures.TryGetValue(media.Hash, out var texture))
                    {
                        texture = MaterialPackTexture.Decode(bytes, media.File);
                        textures.Add(media.Hash, texture);
                    }
                    foreach (var binding in file) skin.Bind(binding.Slot, texture, binding.Media);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or UnityException)
                {
                    diagnostics.Add(string.Join(", ", file.Select(x => x.Slot)) + " (" + media.File + ")：" + e.Message);
                }
            }
        }

        private static byte[] Read(string path, MaterialMediaSlot expected) => MaterialPackArchive.ReadVerified(path, expected.Hash);
    }
}
