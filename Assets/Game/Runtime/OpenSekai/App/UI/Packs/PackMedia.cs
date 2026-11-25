using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Media the resource pack page shows: pack icons (kept while the page lives), preview
    /// textures (released when a preview card closes) and one audition at a time. Every file is
    /// read back and checked against the hash its installed revision recorded, the same check the
    /// live makes, so a preview shows exactly what a live would use. Nothing here writes the
    /// configuration.
    /// </summary>
    internal sealed class PackMedia : IDisposable
    {
        private readonly Dictionary<string, UniTask<Texture2D>> _previewTextures = new Dictionary<string, UniTask<Texture2D>>(StringComparer.Ordinal);
        private readonly List<Texture2D> _loadedPreview = new List<Texture2D>();
        private readonly List<Sprite> _previewSprites = new List<Sprite>();
        private readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> _iconFailures = new HashSet<string>(StringComparer.Ordinal);
        private readonly AudioSource _source;
        private CancellationTokenSource _playLoad;
        private AudioClip _clip;
        private uint _builtinPlayback;
        private float _playingUntil;

        internal PackMedia(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
        }

        /// <summary>Key of what is being auditioned, or null.</summary>
        internal string PlayingKey { get; private set; }

        /// <summary>Raised when an audition starts or stops, so buttons can switch between play and stop.</summary>
        internal event Action PlaybackChanged;

        // ------------------------------------------------------------------ icons

        /// <summary>The pack's icon, or null when it has none or it cannot be read (the view draws its default).</summary>
        internal async UniTask<Sprite> LoadIconAsync(MaterialPackStore store, InstalledMaterialPack pack, CancellationToken token)
        {
            if (store == null || pack?.Icon == null) return null;
            string key = pack.Icon.Hash;
            if (_icons.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            if (_iconFailures.Contains(key)) return null;
            try
            {
                string path = store.IconPath(pack);
                Texture2D texture = await DecodeAsync(path, pack.Icon.Hash, pack.Icon.File, token);
                if (texture == null) return null;
                // Another request for the same icon may have finished while this one decoded.
                if (_icons.TryGetValue(key, out cached) && cached != null)
                {
                    MaterialPackTexture.Release(texture);
                    return cached;
                }

                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "PackIcon-" + pack.PackId;
                _icons[key] = sprite;
                return sprite;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e) when (IsMediaFailure(e))
            {
                _iconFailures.Add(key);
                Debug.LogWarning("[PackMedia] 无法读取图标 " + pack.PackId + "：" + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ preview images

        /// <summary>
        /// A sprite of <paramref name="item"/>'s rect inside its verified PNG. Throws for a file that
        /// is missing, changed or undecodable; the caller shows that tile as not previewable.
        /// </summary>
        internal async UniTask<Sprite> LoadPreviewAsync(MaterialPreviewItem item, CancellationToken token)
        {
            if (item?.Media == null || item.Path == null) return null;
            string key = item.Media.Hash;
            if (!_previewTextures.TryGetValue(key, out UniTask<Texture2D> load))
            {
                load = DecodeAsync(item.Path, item.Media.Hash, item.File, CancellationToken.None).Preserve();
                _previewTextures[key] = load;
            }

            Texture2D texture = await load;
            token.ThrowIfCancellationRequested();
            if (texture == null) return null;
            if (!_loadedPreview.Contains(texture)) _loadedPreview.Add(texture);
            return CreateSprite(texture, item.Rect);
        }

        /// <summary>A built-in sprite or texture from the note skin resources, for the built-in preview.</summary>
        internal static Sprite LoadBuiltinNote(int set, string slot)
        {
            string bundle = LiveConfig.GetNoteBundleName(Mathf.Clamp(set - 1, 0, 1));
            string name = BuiltinNoteResource(slot);
            if (name == null) return null;
            Sprite sprite = AssetBundleUtility.LoadAsset<Sprite>(bundle, name);
            if (sprite != null) return sprite;
            Texture2D texture = AssetBundleUtility.LoadAsset<Texture2D>(bundle, name);
            return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static string BuiltinNoteResource(string slot)
        {
            switch (slot)
            {
                case "notes.line.long": return "longNoteLine";
                case "notes.line.guide": return "traceLine_eff";
                case "notes.line.simultaneous": return "simultaneousLine";
                default:
                    return slot.StartsWith("notes.", StringComparison.Ordinal) ? slot.Substring("notes.".Length) : null;
            }
        }

        private Sprite CreateSprite(Texture2D texture, float[] rect)
        {
            Rect area = new Rect(0f, 0f, texture.width, texture.height);
            if (rect != null && rect.Length == 4)
            {
                float x = Mathf.Clamp(rect[0], 0f, texture.width - 1f);
                float width = Mathf.Clamp(rect[2], 1f, texture.width - x);
                float height = Mathf.Clamp(rect[3], 1f, texture.height);
                // Pack rects are measured from the top edge; Unity's from the bottom.
                float y = Mathf.Clamp(texture.height - rect[1] - height, 0f, texture.height - height);
                area = new Rect(x, y, width, height);
            }

            Sprite sprite = Sprite.Create(texture, area, new Vector2(0.5f, 0.5f), 100f);
            _previewSprites.Add(sprite);
            return sprite;
        }

        /// <summary>Destroys the preview card's textures and sprites; icons stay.</summary>
        internal void ReleasePreview()
        {
            foreach (Sprite sprite in _previewSprites)
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            }

            _previewSprites.Clear();
            foreach (KeyValuePair<string, UniTask<Texture2D>> pending in _previewTextures)
            {
                ReleaseWhenDone(pending.Value).Forget();
            }

            _previewTextures.Clear();
            _loadedPreview.Clear();
        }

        private static async UniTaskVoid ReleaseWhenDone(UniTask<Texture2D> load)
        {
            try
            {
                Texture2D texture = await load;
                if (texture != null) MaterialPackTexture.Release(texture);
            }
            catch (Exception)
            {
                // A failed load has nothing to release.
            }
        }

        // ------------------------------------------------------------------ audition

        /// <summary>Plays one installed sound from the start; a second call with the same key stops it.</summary>
        internal async UniTask<bool> ToggleAsync(string key, MaterialPreviewItem item)
        {
            if (PlayingKey == key)
            {
                Stop();
                return true;
            }

            Stop();
            if (item?.Media is not MaterialAudioSlot audio || item.Path == null) return false;
            var cancellation = new CancellationTokenSource();
            _playLoad = cancellation;
            PlayingKey = key;
            PlaybackChanged?.Invoke();
            try
            {
                var (wave, samples) = await Task.Run(() =>
                {
                    byte[] bytes = MaterialPackArchive.ReadVerified(item.Path, audio.Hash);
                    MaterialPackWave read = MaterialPackWave.Read(bytes);
                    return (read, read.Decode(bytes, 0, read.Frames));
                }, cancellation.Token);
                if (cancellation.IsCancellationRequested || _playLoad != cancellation) return true;
                _clip = AudioClip.Create(item.File, wave.Frames, wave.Channels, wave.Rate, false);
                _clip.SetData(samples, 0);
                _source.clip = _clip;
                _source.volume = PreviewVolume();
                _source.Play();
                _playingUntil = Time.unscaledTime + _clip.length + 0.05f;
                return true;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
            catch (Exception e) when (IsMediaFailure(e))
            {
                Debug.LogWarning("[PackMedia] 无法试听 " + item.Slot + "：" + e.Message);
                if (_playLoad == cancellation) Stop();
                return false;
            }
            finally
            {
                if (_playLoad == cancellation) _playLoad = null;
                cancellation.Dispose();
            }
        }

        /// <summary>Plays the built-in sound of <paramref name="slot"/>; false when the game cannot.</summary>
        internal bool ToggleBuiltin(string key, string slot)
        {
            if (PlayingKey == key)
            {
                Stop();
                return true;
            }

            Stop();
            SoundManager sound = SoundManager.Instance;
            if (sound == null || !MaterialPackAudio.HasSlot(slot)) return false;
            try
            {
                sound.LoadSoundBundle(LiveConfig.BUILTIN_HIT_SOUND_BUNDLE, false);
                sound.LoadSoundBundle(LiveConfig.LIVE_COMMON_SOUND_BUNDLE, false);
                _builtinPlayback = sound.PlaySE(MaterialPackAudio.Cue(slot), PreviewVolume());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PackMedia] 无法试听内置 " + slot + "：" + e.Message);
                _builtinPlayback = 0;
            }

            if (_builtinPlayback == 0) return false;
            PlayingKey = key;
            _playingUntil = Time.unscaledTime + 4f;
            PlaybackChanged?.Invoke();
            return true;
        }

        internal void Stop()
        {
            _playLoad?.Cancel();
            _playLoad = null;
            if (_source != null) _source.Stop();
            if (_builtinPlayback != 0)
            {
                try { SoundManager.Instance?.StopSE(_builtinPlayback); }
                catch (Exception) { /* The player may already be gone. */ }
                _builtinPlayback = 0;
            }

            if (_clip != null)
            {
                if (_source != null) _source.clip = null;
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }

            bool changed = PlayingKey != null;
            PlayingKey = null;
            if (changed) PlaybackChanged?.Invoke();
        }

        /// <summary>Clears the playing state once a sound has run out, so its button turns back to play.</summary>
        internal void Tick()
        {
            if (PlayingKey == null || _playLoad != null) return;
            bool sourceDone = _clip != null && !_source.isPlaying;
            if (sourceDone || Time.unscaledTime > _playingUntil) Stop();
        }

        public void Dispose()
        {
            Stop();
            ReleasePreview();
            foreach (Sprite sprite in _icons.Values)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                if (texture != null) MaterialPackTexture.Release(texture);
            }

            _icons.Clear();
        }

        private static float PreviewVolume()
        {
            ApplicationLocalSettings settings = ApplicationLocalSettings.LoadFromStorage();
            float se = settings.LiveVolume?.Se ?? 1f;
            return Mathf.Clamp01(se);
        }

        private static async UniTask<Texture2D> DecodeAsync(string path, string hash, string name, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path)) return null;
            byte[] bytes = await Task.Run(() => MaterialPackArchive.ReadVerified(path, hash), token);
            token.ThrowIfCancellationRequested();
            return MaterialPackTexture.Decode(bytes, name);
        }

        internal static bool IsMediaFailure(Exception e) =>
            e is IOException or InvalidDataException or UnauthorizedAccessException or UnityException or ArgumentException;
    }
}
