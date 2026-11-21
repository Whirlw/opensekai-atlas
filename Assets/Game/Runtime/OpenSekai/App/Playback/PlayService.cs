using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenSekai.Charts;
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
        private readonly SongPackageManager _packages;
        private readonly LiveSettingsStore _settings;
        private readonly IPlayBootPublisher _boot;
        private readonly IPlaySurface _surface;
        private readonly IPackageAudioRegistrar _audio;
        private readonly ILiveDeckSelection _deckSelection;
        private readonly IPlayHistoryRecorder _playHistory;
        private readonly Func<MaterialPackStore> _materialStore;
        private bool _releaseOnSceneExit;

        /// <summary>The process-wide service, wired to the retained singletons.</summary>
        public static PlayService Instance { get; } = CreateDefault(SongPackageManager.Shared, LiveSettingsStore.Shared);

        internal static PlayService CreateDefault(SongPackageManager packages, LiveSettingsStore settings) => new PlayService(
            packages, settings, new LegacyPlayBootPublisher(),
            new LegacyPlaySurface(),
            new LegacyPackageAudioRegistrar(packages),
            new LegacyLiveDeckSelection(),
            new Library.CustomScorePlayHistoryRecorder(), () => MaterialPackStore.Shared);

        public bool IsStarting { get; private set; }

        /// <summary>Used by tests and by the composition root to supply substitutes.</summary>
        internal PlayService(
            SongPackageManager packages, LiveSettingsStore settings, IPlayBootPublisher boot,
            IPlaySurface surface,
            IPackageAudioRegistrar audio,
            ILiveDeckSelection deckSelection,
            IPlayHistoryRecorder playHistory, Func<MaterialPackStore> materialStore = null)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _boot = boot ?? throw new ArgumentNullException(nameof(boot));
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _deckSelection = deckSelection ?? throw new ArgumentNullException(nameof(deckSelection));
            _playHistory = playHistory ?? throw new ArgumentNullException(nameof(playHistory));
            _materialStore = materialStore;
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
            MaterialAudioPreparation material = null;
            var materialDiagnostics = new List<string>();
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
                progress?.Invoke("正在读取谱面...");
                await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
                if (!await _surface.PrepareLiveLoadingAsync(cancellationToken))
                {
                    return Result(PlayStartStatus.Failed, "打歌加载界面尚未就绪。");
                }

                SongPackage cached = temporary?.Metadata ?? _packages.FindCachedItem(request.PackageDirectory)?.Entry;
                PlaybackSettingsSnapshot settings = _settings.CaptureSnapshot();
                if (_materialStore != null)
                {
                    try { material = new MaterialAudioPreparation(_materialStore()); }
                    catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
                    {
                        materialDiagnostics.Add("资源包索引：" + e.Message);
                    }
                }
                LiveBundleBuildData config = ResourceCatalog.LiveBundleConfig.Load();
                if (config == null) throw new InvalidOperationException("Live bundle configuration is missing.");
                var conversion = new ChartConversionSettings(config);
                var (entry, chart, chartHash) = await Task.Run(
                    () => PrepareChart(request, cached, conversion, settings, cancellationToken), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRequestSceneReady(request))
                    return Result(PlayStartStatus.Failed, "曲库状态已变化，请重新开始游玩。");
                if (chart == null || !chart.HasPlayableNotes)
                {
                    if (chart?.NextRush == null || chart.Score?.HasVisualNotes != true)
                        return Result(PlayStartStatus.Failed, "没有可游玩的音符。");
                    if (!request.IsAuto)
                        return Result(PlayStartStatus.Failed, "此谱面仅含装饰，请选择自动游玩。");
                }
                _surface.BeginSession(chartHash);
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
                if (material != null) await material.PrepareAsync(materialDiagnostics, cancellationToken);
                PlaySession session = PlaySession.FromChart(request, entry, chart, config, settings, material?.Audio, material?.Combo, material?.Judgment, material?.HitFx, material?.Notes, material?.Lane);
                FreeLiveBootData boot = LegacyLiveBootAdapter.Create(session, _deckSelection.SelectedDeckId);
                if (temporary == null) boot.CustomMusicScoreKey = _packages.FindCachedItem(entry.RootDirectory)?.ScoreKey ?? chartHash;
                cancellationToken.ThrowIfCancellationRequested();
                string message = request.IsAuto ? "正在开始自动游玩..." : "正在开始游玩...";
                progress?.Invoke(message);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRequestSceneReady(request))
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
                if (materialDiagnostics.Count > 0)
                    Debug.LogWarning("[MaterialPack] 使用内置资源：" + string.Join(" / ", materialDiagnostics));
                return new PlayStartResult(PlayStartStatus.Started, message, materialDiagnostics.ToArray());
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
                    material?.Lane?.Dispose();
                    material?.Notes?.Dispose();
                    material?.HitFx?.Dispose();
                    material?.Judgment?.Dispose();
                    material?.Combo?.Dispose();
                    material?.Audio.Dispose();
                    if (bootPublished && _surface.CurrentScene == request.ReturnScene)
                    {
                        _boot.Restore(previousBoot);
                    }
                    if (audioRegistrationStarted) _audio.Release();
                    ReleaseStartLock();
                }
            }
        }

        private bool IsRequestSceneReady(PlayRequest request) =>
            _surface.Exists && _surface.CurrentScene == request.ReturnScene && _surface.IsLibraryReady;

        private static (SongPackage entry, RuntimeChartDefinition chart, string hash) PrepareChart(
            PlayRequest request, SongPackage cached, ChartConversionSettings config, PlaybackSettingsSnapshot settings,
            CancellationToken token)
        {
            var temporary = request.TemporaryPackage;
            SongPackage entry = cached ?? SongPackageStorage.LoadEntry(request.PackageDirectory);
            if (entry == null) throw new InvalidDataException("无法加载曲包信息。");
            if (temporary == null && !File.Exists(entry.ScorePath)) throw new InvalidDataException("找不到谱面文件。");
            if (temporary == null && !File.Exists(entry.AudioPath)) throw new InvalidDataException("找不到音频文件。");
            token.ThrowIfCancellationRequested();
            var chart = temporary == null
                ? RuntimeChartLoader.Load(entry, config, settings.ShowSimultaneousLine, settings.IsMirror)
                : NextRushChartLoader.Load(temporary.Chart, config, settings.ShowSimultaneousLine, settings.IsMirror);
            token.ThrowIfCancellationRequested();
            return (entry, chart, temporary?.ChartHash ?? SongPackageHash.Compute(entry.ScorePath));
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
