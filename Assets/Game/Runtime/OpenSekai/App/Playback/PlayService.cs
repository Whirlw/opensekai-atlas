using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sekai;
using Sekai.Live;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using UnityEngine;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using OpenSekai.Shared.Audio;
using OpenSekai.Live.Conversion;
using Sekai.Core;
using Sekai.Core.Live;

namespace OpenSekai.App.Playback
{
    /// <summary>Shared main-thread entry for manual and automatic package playback.</summary>
    public sealed class PlayService
    {
        private readonly IPlayBootPublisher _boot;
        private readonly IPlaySurface _surface;
        private readonly IPackageAudioRegistrar _audio;
        private readonly ILiveDeckSelection _deckSelection;
        private readonly IPlayHistoryRecorder _playHistory;
        private bool _releaseOnSceneExit;

        /// <summary>The process-wide service, wired to the retained singletons.</summary>
        public static PlayService Instance { get; } = new PlayService(
            new LegacyPlayBootPublisher(),
            new LegacyPlaySurface(),
            new LegacyPackageAudioRegistrar(),
            new LegacyLiveDeckSelection(),
            new Library.CustomScorePlayHistoryRecorder());

        public bool IsStarting { get; private set; }

        /// <summary>Used by tests and by the composition root to supply substitutes.</summary>
        internal PlayService(
            IPlayBootPublisher boot,
            IPlaySurface surface,
            IPackageAudioRegistrar audio,
            ILiveDeckSelection deckSelection,
            IPlayHistoryRecorder playHistory)
        {
            _boot = boot ?? throw new ArgumentNullException(nameof(boot));
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _deckSelection = deckSelection ?? throw new ArgumentNullException(nameof(deckSelection));
            _playHistory = playHistory ?? throw new ArgumentNullException(nameof(playHistory));
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
            bool audioRegistrationStarted = false;
            bool bootPublished = false;
            LiveBootState previousBoot = default;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var temporary = request?.TemporaryPackage;
                if (temporary == null && string.IsNullOrWhiteSpace(request?.PackageDirectory))
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
                // The cached package when the library holds it; a folder outside the library is read directly.
                SongPackage entry = temporary?.Metadata
                    ?? SongPackageManager.Shared.FindCachedItem(request.PackageDirectory)?.Entry
                    ?? SongPackageStorage.LoadEntry(request.PackageDirectory);
                if (entry == null)
                {
                    return Result(PlayStartStatus.Failed, "无法加载曲包信息。");
                }
                if (temporary == null && !File.Exists(entry.ScorePath))
                {
                    return Result(PlayStartStatus.Failed, "找不到谱面文件。");
                }
                if (temporary == null && !File.Exists(entry.AudioPath))
                {
                    return Result(PlayStartStatus.Failed, "找不到音频文件。");
                }
                PlaybackSettingsSnapshot settings = LiveSettingsStore.Shared.CaptureSnapshot();
                LiveBundleBuildData config = ResourceCatalog.LiveBundleConfig.Load();
                if(config == null) throw new InvalidOperationException("Live bundle configuration is missing.");
                var chart = temporary == null
                    ? RuntimeChartLoader.Load(entry, config, settings.ShowSimultaneousLine, settings.IsMirror)
                    : NextRushChartLoader.Load(temporary.Chart, config, settings.ShowSimultaneousLine, settings.IsMirror);
                if (chart == null || !chart.HasPlayableNotes)
                {
                    if (chart?.NextRush == null || chart.Score?.HasVisualNotes != true)
                        return Result(PlayStartStatus.Failed, "没有可游玩的音符。");
                    if (!request.IsAuto)
                        return Result(PlayStartStatus.Failed, "此谱面仅含装饰，请选择自动游玩。");
                }
                _surface.BeginSession(temporary?.ChartHash ?? SongPackageHash.Compute(entry.ScorePath));
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke("正在加载音频...");
                audioRegistrationStarted = true;
                if (temporary != null)
                {
                    await RegisterTemporaryAudio(temporary, cancellationToken);
                }
                else if (!await _audio.RegisterAsync(entry, cancellationToken))
                {
                    return Result(PlayStartStatus.Failed, "无法加载音频文件。");
                }
                cancellationToken.ThrowIfCancellationRequested();
                PlaySession session = PlaySession.FromChart(request, entry, chart, config, settings);
                FreeLiveBootData boot = LegacyLiveBootAdapter.Create(session, _deckSelection.SelectedDeckId);
                if (temporary == null) boot.CustomMusicScoreKey = SongPackageManager.Shared.ScoreKeyOf(entry);
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
                // The same boundary that hands the boot over hands the recorder over: the live scene
                // builds its own context, so the App-side implementation travels through the Game's
                // registration slot, never through a Game -> App reference.
                PlayHistoryRecorder.Current = _playHistory;
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
                    if (audioRegistrationStarted) _audio.Release();
                    ReleaseStartLock();
                }
            }
        }

        private static async UniTask RegisterTemporaryAudio(TemporarySongPackage package, CancellationToken token)
        {
            if (package.Audio == null) throw new InvalidDataException("临时音频已释放，请返回服务器重新游玩。");
            var sound = SoundManager.Instance;
            sound.IngameBgmGain = 1f;
            PackageAudio.RegisterClip(package.Metadata, package.Audio, ownsClip: false);
            if (!package.LoudnessMeasured)
            {
                package.Loudness = await LoudnessMeter.MeasureAsync(package.Audio, token);
                package.LoudnessMeasured = true;
            }

            sound.IngameBgmGain = LoudnessNormalization.GainFor(package.Loudness);
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
    }
}
