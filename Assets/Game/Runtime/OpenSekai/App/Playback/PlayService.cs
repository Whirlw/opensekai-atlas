using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sekai;
using Sekai.Live;
using OpenSekai.Infrastructure.Charts;
using UnityEngine;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using OpenSekai.Shared.Settings;
using OpenSekai.Live.Conversion;
using Sekai.Core;

namespace OpenSekai.App.Playback
{
    /// <summary>Shared main-thread entry for manual and automatic package playback.</summary>
    public sealed class PlayService
    {
        public static PlayService Instance { get; } = new PlayService();
        public bool IsStarting { get; private set; }
        private SceneManager _transitionOwner;

        private PlayService() { }

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
            FreeLiveBootData previousBoot = null;
            Sekai.Core.PlayMode previousMode = default;
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
                if (!ScreenManager.ExistsInstance || SceneManager.Instance.CurrentScene != request.ReturnScene)
                {
                    return Result(PlayStartStatus.Failed, "曲库界面尚未就绪。");
                }
                ScreenManager screens = ScreenManager.Instance;
                if (!IsLibraryAlive(screens)
                    || !screens.TryGetScreenLayerData(MenuScreenType.LiveLoading, out ScreenLayerData loading)
                    || loading.LoadScreenLayerPrefab() == null)
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
                PlaySessionContext.Begin(ComputeFileHash(entry.ScorePath));
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke("正在加载音频...");
                if (!await new PackageAudio(entry).RegisterAudioAsync(cancellationToken))
                {
                    return Result(PlayStartStatus.Failed, "无法加载音频文件。");
                }
                cancellationToken.ThrowIfCancellationRequested();
                PlaybackSettingsSnapshot settings = LiveSettingsStore.Shared.CaptureSnapshot();
                LiveBundleBuildData config = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
                if(config == null) throw new InvalidOperationException("Live bundle configuration is missing.");
                PlaySession session = PlaySession.Create(request, entry, chart, config, settings);
                FreeLiveBootData boot = LegacyLiveBootAdapter.Create(session);
                cancellationToken.ThrowIfCancellationRequested();
                string message = request.IsAuto ? "正在开始自动游玩..." : "正在开始游玩...";
                progress?.Invoke(message);
                cancellationToken.ThrowIfCancellationRequested();
                if (screens == null || SceneManager.Instance.CurrentScene != request.ReturnScene
                    || !IsLibraryAlive(screens))
                {
                    return Result(PlayStartStatus.Failed, "曲库状态已变化，请重新开始游玩。");
                }

                _transitionOwner = SceneManager.Instance;
                _transitionOwner.OnExitScene += ReleaseStartLock;
                previousBoot = UserDataManager.Instance.FreeLiveBootData;
                previousMode = Sekai.Core.EntryPoint.PlayMode;
                UserDataManager.Instance.FreeLiveBootData = boot;
                Sekai.Core.EntryPoint.PlayMode = Sekai.Core.PlayMode.SoloLive;
                bootPublished = true;
                LiveTransitioner.SafeForceFinish(null);
                screens.PushUIScreen(MenuScreenType.LiveLoading, false);
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
                    if (bootPublished && SceneManager.Instance.CurrentScene == request.ReturnScene)
                    {
                        UserDataManager.Instance.FreeLiveBootData = previousBoot;
                        Sekai.Core.EntryPoint.PlayMode = previousMode;
                    }
                    ReleaseStartLock();
                }
            }
        }

        /// <summary>
        /// True when the page that owns the start button is still alive. The lightweight
        /// library screen registers itself; the legacy editor home page keeps the old check.
        /// </summary>
        private static bool IsLibraryAlive(ScreenManager screens)
        {
            ILibraryPage page = LibraryPageRegistry.Current;
            if (page != null)
            {
                return page.IsReady;
            }
            return screens != null && screens.IsActiveScreen(MenuScreenType.MusicScoreMakerTop);
        }

        private void ReleaseStartLock()
        {
            if (_transitionOwner != null)
            {
                _transitionOwner.OnExitScene -= ReleaseStartLock;
            }
            _transitionOwner = null;
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
