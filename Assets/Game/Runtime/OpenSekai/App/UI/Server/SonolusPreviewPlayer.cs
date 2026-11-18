using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Sonolus;
using Sekai;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Plays a level's preview clip from the server (falling back to the start of its music when
    /// the server has no preview). The clip is downloaded to the cache, decoded by Unity and looped
    /// at the 音乐音量 setting with a short fade at each end.
    /// </summary>
    internal sealed class SonolusPreviewPlayer : MonoBehaviour
    {
        private const long MAX_BYTES = 24L * 1024 * 1024;
        private const float FADE = 0.4f;

        private AudioSource _source;
        private AudioClip _clip;
        private CancellationTokenSource _load;
        private float _fade;
        private bool _stopping;

        /// <summary>Raised when playback starts, stops or fails; the argument is whether it is playing.</summary>
        internal event Action<bool> StateChanged;

        internal bool IsPlaying => _source != null && _source.isPlaying && !_stopping;

        internal bool IsLoading => _load != null;

        internal void Play(SonolusServerAddress server, SonolusLevel level)
        {
            Stop(false);
            SonolusSrl resource = level?.Preview ?? level?.Bgm;
            if (server == null || resource == null)
            {
                StateChanged?.Invoke(false);
                return;
            }

            _load = new CancellationTokenSource();
            LoadAsync(server, resource, _load.Token).Forget();
        }

        /// <param name="fade">Fade out rather than cut.</param>
        internal void Stop(bool fade = true)
        {
            bool wasLoading = _load != null;
            CancelLoad();
            if (_source == null || !_source.isPlaying)
            {
                ReleaseClip();
                if (wasLoading)
                {
                    StateChanged?.Invoke(false);
                }

                return;
            }

            if (fade)
            {
                _stopping = true;
            }
            else
            {
                _source.Stop();
                ReleaseClip();
            }

            StateChanged?.Invoke(false);
        }

        private async UniTaskVoid LoadAsync(SonolusServerAddress server, SonolusSrl resource, CancellationToken cancellationToken)
        {
            string path = null;
            try
            {
                byte[] bytes = await new SonolusClient(server).DownloadAsync(resource, MAX_BYTES, null, cancellationToken);
                string extension = SonolusLevelInstaller.AudioExtension(bytes) ?? throw new SonolusException("试听音频格式无法识别。");
                AudioType type = extension == ".ogg" ? AudioType.OGGVORBIS : extension == ".wav" ? AudioType.WAV : AudioType.MPEG;
                string directory = Path.Combine(Application.temporaryCachePath, "SonolusPreview");
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
                File.WriteAllBytes(path, bytes);

                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new SonolusException("试听音频无法播放。");
                    }

                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    cancellationToken.ThrowIfCancellationRequested();
                    StartClip(clip);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Preview failed: " + exception.Message);
                _load = null;
                StateChanged?.Invoke(false);
                return;
            }
            finally
            {
                if (path != null)
                {
                    try { File.Delete(path); } catch (IOException) { }
                }
            }

            _load = null;
        }

        private void StartClip(AudioClip clip)
        {
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                _source.loop = true;
            }

            ReleaseClip();
            _clip = clip;
            _source.clip = clip;
            _source.volume = 0f;
            _fade = 0f;
            _stopping = false;
            _source.Play();
            _load?.Dispose();
            _load = null;
            StateChanged?.Invoke(true);
        }

        private void Update()
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }

            float step = Time.unscaledDeltaTime / FADE;
            _fade = Mathf.Clamp01(_fade + (_stopping ? -step : step));
            _source.volume = MusicVolume() * _fade * _fade;
            if (_stopping && _fade <= 0f)
            {
                _source.Stop();
                _stopping = false;
                ReleaseClip();
            }
        }

        private void OnDisable() => Stop(false);

        private void OnDestroy()
        {
            CancelLoad();
            ReleaseClip();
        }

        private void CancelLoad()
        {
            if (_load == null)
            {
                return;
            }

            _load.Cancel();
            _load.Dispose();
            _load = null;
        }

        private void ReleaseClip()
        {
            if (_source != null && _source.clip == _clip)
            {
                _source.clip = null;
            }

            if (_clip != null)
            {
                Destroy(_clip);
                _clip = null;
            }
        }

        private static float MusicVolume()
        {
            ApplicationLocalSettings settings = ApplicationLocalSettings.LoadFromStorage();
            ApplicationLocalSettings.VolumeSettings music = settings.LiveVolume ?? settings.SystemVolume;
            return Mathf.Clamp01(music?.Bgm ?? 1f);
        }
    }
}
