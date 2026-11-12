using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sekai;
using Sekai.Live;
using OpenSekai.Infrastructure.Charts;
using UnityEngine;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using OpenSekai.Live.Conversion;
using Sekai.Core;

namespace OpenSekai.App.Playback
{
    /// <summary>Shared main-thread entry for manual and automatic package playback.</summary>
    public sealed class PlayService
    {
        private readonly IPlayBootPublisher _boot;
        private readonly IPlaySurface _surface;
        private readonly IPackageAudioRegistrar _audio;
        private readonly ILiveDeckSelection _deckSelection;
        private bool _releaseOnSceneExit;

        /// <summary>The process-wide service, wired to the retained singletons.</summary>
        public static PlayService Instance { get; } = new PlayService(
            new LegacyPlayBootPublisher(),
            new LegacyPlaySurface(),
            new LegacyPackageAudioRegistrar(),
            new LegacyLiveDeckSelection());

        public bool IsStarting { get; private set; }

        /// <summary>Used by tests and by the composition root to supply substitutes.</summary>
        internal PlayService(
            IPlayBootPublisher boot,
            IPlaySurface surface,
            IPackageAudioRegistrar audio,
            ILiveDeckSelection deckSelection)
        {
            _boot = boot ?? throw new ArgumentNullException(nameof(boot));
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _deckSelection = deckSelection ?? throw new ArgumentNullException(nameof(deckSelection));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            Instance.ReleaseStartLock();
        }

        public async UniTask<PlayStartResult> StartAsync(
            PlayRequest request, CancellationToken cancellationToken, Action<string> progress = null)
        {
            if (IsStarting)
            {
                return Result(PlayStartStatus.Busy, "正在准备另一场游玩。");
            }

            IsStarting = true;
            bool transitionAccepted = false;
            bool bootPublished = false;
            LiveBootState previousBoot = default;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (request == null || string.IsNullOrWhiteSpace(request.PackageDirectory))
                {
                    return Result(PlayStartStatus.Failed, "请选择一个谱面。");
                }
                if (request.ReturnScene != SceneManager.Scene.Library)
                {
                    return Result(PlayStartStatus.Failed, "无法从当前场景开始游玩。");
                }
                if (!_surface.Exists || _surface.CurrentScene != request.ReturnScene)
                {
                    return Result(PlayStartStatus.Failed, "曲库界面尚未就绪。");
                }
                if (!_surface.IsLibraryReady)
                {
                    return Result(PlayStartStatus.Failed, "曲库界面尚未就绪。");
                }
                if (!_surface.IsLiveLoadingReady)
                {
                    return Result(PlayStartStatus.Failed, "打歌加载界面尚未就绪。");
                }

                progress?.Invoke("正在读取谱面...");
                SongPackage entry = SongPackageStorage.LoadEntry(request.PackageDirectory);
                if (entry == null)
                {
                    return Result(PlayStartStatus.Failed, "无法加载曲包信息。");
                }
                if (!File.Exists(entry.ScorePath))
                {
                    return Result(PlayStartStatus.Failed, "找不到谱面文件。");
                }
                if (!File.Exists(entry.AudioPath))
                {
                    return Result(PlayStartStatus.Failed, "找不到音频文件。");
                }
                var chart = ChartFileReader.Read(entry);
                if (chart == null || !chart.HasPlayableNotes)
                {
                    return Result(PlayStartStatus.Failed, "没有可游玩的音符。");
                }
                _surface.BeginSession(ComputeFileHash(entry.ScorePath));
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke("正在加载音频...");
                if (!await _audio.RegisterAsync(entry, cancellationToken))
                {
                    return Result(PlayStartStatus.Failed, "无法加载音频文件。");
                }
                cancellationToken.ThrowIfCancellationRequested();
                PlaybackSettingsSnapshot settings = LiveSettingsStore.Shared.CaptureSnapshot();
                LiveBundleBuildData config = ResourceCatalog.LiveBundleConfig.Load();
                if(config == null) throw new InvalidOperationException("Live bundle configuration is missing.");
                PlaySession session = PlaySession.Create(request, entry, chart, config, settings);
                FreeLiveBootData boot = LegacyLiveBootAdapter.Create(session, _deckSelection.SelectedDeckId);
                cancellationToken.ThrowIfCancellationRequested();
                string message = request.IsAuto ? "正在开始自动游玩..." : "正在开始游玩...";
                progress?.Invoke(message);
                cancellationToken.ThrowIfCancellationRequested();
                if (!_surface.Exists || _surface.CurrentScene != request.ReturnScene || !_surface.IsLibraryReady)
                {
                    return Result(PlayStartStatus.Failed, "曲库状态已变化，请重新开始游玩。");
                }

                _surface.SubscribeSceneExit(ReleaseStartLock);
                _releaseOnSceneExit = true;
                previousBoot = _boot.Capture();
                _boot.Publish(boot);
                bootPublished = true;
                _surface.ForceFinishTransition();
                _surface.PushLiveLoading();
                transitionAccepted = true;
                return Result(PlayStartStatus.Started, message);
            }
            catch (OperationCanceledException)
            {
                return Result(PlayStartStatus.Cancelled, "已取消开局。");
            }
            catch (JsonException)
            {
                return Result(PlayStartStatus.Failed, "谱面文件格式无效，无法开始游玩。");
            }
            catch (InvalidDataException exception)
            {
                return Result(PlayStartStatus.Failed, exception.Message);
            }
            catch (IOException)
            {
                return Result(PlayStartStatus.Failed, "无法读取曲包文件，请检查文件是否可用。");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PlayService] Could not start playback: " + exception.Message);
                return Result(PlayStartStatus.Failed, "无法开始游玩，请检查曲包及运行日志。");
            }
            finally
            {
                // Once loading owns the request, page destruction must not cancel the live.
                // Keep the lock until the scene transition actually starts.
                if (!transitionAccepted)
                {
                    if (bootPublished && _surface.CurrentScene == request.ReturnScene)
                    {
                        _boot.Restore(previousBoot);
                    }
                    ReleaseStartLock();
                }
            }
        }

        private void ReleaseStartLock()
        {
            // Only touch the surface when this service actually subscribed. ResetForNewRun runs at
            // subsystem registration, before any scene exists, and asking the surface for the scene
            // manager there makes SceneManager's lazy Instance create one: that phantom survives as
            // the DontDestroyOnLoad singleton with the wrong CurrentScene, and every later start is
            // refused as "library not ready".
            if (_releaseOnSceneExit)
            {
                _surface.UnsubscribeSceneExit(ReleaseStartLock);
                _releaseOnSceneExit = false;
            }
            IsStarting = false;
        }

        private static PlayStartResult Result(PlayStartStatus status, string message)
        {
            return new PlayStartResult(status, message);
        }

        /// <summary>SHA-256 of the chart file; empty when the file cannot be read (history still writes).</summary>
        private static string ComputeFileHash(string path)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    var builder = new System.Text.StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        builder.Append(hash[i].ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PlayService] Could not hash chart file: " + exception.Message);
                return string.Empty;
            }
        }
    }
}
