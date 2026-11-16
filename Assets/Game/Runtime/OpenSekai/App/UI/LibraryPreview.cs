using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Plays a short preview of the selected package. A new selection fades the old preview out
    /// while the new one loads and fades in, so two voices can briefly overlap.
    /// </summary>
    internal sealed class LibraryPreview : MonoBehaviour
    {
        private const float PreviewLength = 15f;

        private readonly List<Voice> _fading = new List<Voice>();
        private readonly Stack<AudioSource> _freeSources = new Stack<AudioSource>();
        private Voice _current;
        private CancellationTokenSource _load;
        private LibraryDetailPanel _detail;
        private SongPackageItemKey _selection;
        private long _lengthMs;

        internal void Initialize(LibraryDetailPanel detail)
        {
            _detail = detail;
        }

        internal void Select(SongPackageItem item)
        {
            var selection = LibraryModel.IsPlayable(item) && item?.Entry != null
                ? new SongPackageItemKey(item.Entry.AudioPath, item.LastWriteTime)
                : default;
            if (_selection.Equals(selection))
            {
                _detail.SetAudioLength(_lengthMs);
                return;
            }

            FadeOutCurrent();
            if (selection.Path == null || !isActiveAndEnabled)
            {
                return;
            }

            _selection = selection;
            _load = new CancellationTokenSource();
            PlayAsync(item.Entry, _load.Token).Forget();
        }

        /// <summary>Silences every voice at once; used when the page leaves the library.</summary>
        internal void StopPreview()
        {
            CancelLoad();
            ReleaseVoice(_current);
            _current = null;
            foreach (Voice voice in _fading)
            {
                ReleaseVoice(voice);
            }

            _fading.Clear();
            _detail?.SetPreviewProgress(null);
        }

        /// <summary>Hands the playing preview to the fade-out list and forgets the selection.</summary>
        private void FadeOutCurrent()
        {
            CancelLoad();
            _detail?.SetPreviewProgress(null);
            if (_current != null)
            {
                _current.Target = 0f;
                _fading.Add(_current);
                _current = null;
            }
        }

        private void CancelLoad()
        {
            _selection = default;
            _lengthMs = 0;
            _load?.Cancel();
            _load?.Dispose();
            _load = null;
        }

        private async UniTaskVoid PlayAsync(SongPackage package, CancellationToken token)
        {
            AudioClip clip = null;
            try
            {
                clip = await PackageAudio.LoadClipAsync(package, token, stream: true);
                if (token.IsCancellationRequested || clip == null || clip.samples == 0)
                {
                    Destroy(clip);
                    return;
                }

                _lengthMs = (long)(clip.length * 1000);
                _detail.SetAudioLength(_lengthMs);

                float start = PreviewStart(package.Manifest.previewStartTimeSec, clip.length);
                AudioSource source = TakeSource();
                source.clip = clip;
                source.volume = 0f;
                source.timeSamples = Mathf.Min(clip.samples - 1, Mathf.FloorToInt(start * clip.frequency));
                source.Play();
                _current = new Voice(source, clip, start, Mathf.Min(start + PreviewLength, clip.length));
                clip = null;
                _detail.SetPreviewProgress(0f);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Selection changed or the page stopped previewing.
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Library] Could not play the preview: " + exception.Message);
            }
            finally
            {
                Destroy(clip);
            }
        }

        /// <summary>
        /// Where the preview starts: the package's own preview point when it set one, otherwise the
        /// middle <see cref="PreviewLength"/> seconds, since songs usually open on a quiet intro.
        /// </summary>
        internal static float PreviewStart(float manifestStart, float clipLength)
        {
            if (!float.IsNaN(manifestStart) && manifestStart > 0f && manifestStart < clipLength)
            {
                return manifestStart;
            }

            return clipLength > PreviewLength ? (clipLength - PreviewLength) * 0.5f : 0f;
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / LibraryTheme.Motion.PREVIEW_FADE;
            float volume = Mathf.Clamp01(ApplicationLocalSettings.LoadFromStorage().SystemVolume?.Bgm ?? 1f);

            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                Voice voice = _fading[i];
                if (!voice.Step(step, volume))
                {
                    ReleaseVoice(voice);
                    _fading.RemoveAt(i);
                }
            }

            if (_current == null)
            {
                return;
            }

            float time = _current.Time;
            if (time >= _current.End - LibraryTheme.Motion.PREVIEW_FADE)
            {
                // Ease out at the end of the segment instead of cutting off.
                _current.Target = 0f;
            }

            if (!_current.Step(step, volume) || time >= _current.End)
            {
                ReleaseVoice(_current);
                _current = null;
                _detail.SetPreviewProgress(null);
                return;
            }

            _detail.SetPreviewProgress(Mathf.InverseLerp(_current.Start, _current.End, time));
        }

        private void OnDisable() => StopPreview();

        private AudioSource TakeSource()
        {
            if (_freeSources.Count > 0)
            {
                return _freeSources.Pop();
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void ReleaseVoice(Voice voice)
        {
            if (voice == null)
            {
                return;
            }

            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                _freeSources.Push(voice.Source);
            }

            Destroy(voice.Clip);
        }

        private sealed class Voice
        {
            internal readonly AudioSource Source;
            internal readonly AudioClip Clip;
            internal readonly float Start;
            internal readonly float End;
            internal float Target = 1f;
            private float _gain;

            internal Voice(AudioSource source, AudioClip clip, float start, float end)
            {
                Source = source;
                Clip = clip;
                Start = start;
                End = end;
            }

            internal float Time => (float)Source.timeSamples / Clip.frequency;

            /// <summary>Moves the gain toward its target; false once the voice is silent or stopped.</summary>
            internal bool Step(float step, float volume)
            {
                _gain = Mathf.MoveTowards(_gain, Target, step);
                // Squared gain sounds closer to an even fade than a linear ramp of the amplitude.
                Source.volume = volume * _gain * _gain;
                return Source.isPlaying && !(Target <= 0f && _gain <= 0f);
            }
        }

        private readonly struct SongPackageItemKey
        {
            internal readonly string Path;
            private readonly DateTime _version;

            internal SongPackageItemKey(string path, DateTime version)
            {
                Path = path;
                _version = version;
            }

            internal bool Equals(SongPackageItemKey other) =>
                string.Equals(Path, other.Path, StringComparison.Ordinal) && _version == other._version;
        }
    }
}
