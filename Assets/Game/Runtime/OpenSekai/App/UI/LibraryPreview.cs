using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.UI
{
    internal sealed class LibraryPreview : MonoBehaviour
    {
        private const float PreviewLength = 15f;

        private AudioSource _source;
        private AudioClip _clip;
        private CancellationTokenSource _load;
        private LibraryDetailPanel _detail;
        private SongPackageItemKey _selection;
        private float _start;
        private float _end;
        private long _lengthMs;

        internal void Initialize(LibraryDetailPanel detail)
        {
            _detail = detail;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
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

            StopPreview();
            if (selection.Path == null || !isActiveAndEnabled)
            {
                return;
            }

            _selection = selection;
            _load = new CancellationTokenSource();
            PlayAsync(item.Entry, _load.Token).Forget();
        }

        internal void StopPreview()
        {
            _selection = default;
            _lengthMs = 0;
            _load?.Cancel();
            _load?.Dispose();
            _load = null;
            ReleaseClip();
        }

        private async UniTaskVoid PlayAsync(SongPackage package, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(LibraryTheme.Motion.PREVIEW_DELAY), ignoreTimeScale: true,
                    cancellationToken: token);
                AudioClip clip = await PackageAudio.LoadClipAsync(package, token);
                if (token.IsCancellationRequested || clip == null || clip.samples == 0)
                {
                    Destroy(clip);
                    return;
                }

                _clip = clip;
                _lengthMs = (long)(clip.length * 1000);
                _detail.SetAudioLength(_lengthMs);
                float start = package.Manifest.previewStartTimeSec;
                if (float.IsNaN(start) || start < 0f || start >= clip.length)
                {
                    start = 0f;
                }

                _start = start;
                _end = Mathf.Min(start + PreviewLength, clip.length);
                _source.clip = clip;
                _source.volume = Mathf.Clamp01(ApplicationLocalSettings.LoadFromStorage().SystemVolume?.Bgm ?? 1f);
                _source.timeSamples = Mathf.Min(clip.samples - 1, Mathf.FloorToInt(start * clip.frequency));
                _source.Play();
                _source.SetScheduledEndTime(AudioSettings.dspTime + (_end - _start));
                _detail.SetPreviewProgress(0f);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Selection changed or the page stopped previewing.
            }
            catch (Exception exception)
            {
                ReleaseClip();
                Debug.LogWarning("[Library] Could not play the preview: " + exception.Message);
            }
        }

        private void Update()
        {
            if (_clip == null)
            {
                return;
            }

            float time = (float)_source.timeSamples / _clip.frequency;
            if (!_source.isPlaying || time >= _end)
            {
                ReleaseClip();
                return;
            }

            _detail.SetPreviewProgress(Mathf.InverseLerp(_start, _end, time));
        }

        private void OnDisable() => StopPreview();

        private void ReleaseClip()
        {
            _detail?.SetPreviewProgress(null);
            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }

            Destroy(_clip);
            _clip = null;
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
