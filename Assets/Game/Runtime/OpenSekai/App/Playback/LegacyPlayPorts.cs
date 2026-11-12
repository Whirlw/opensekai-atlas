using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core;
using Sekai.Live;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// Writes the live boot through the retained legacy singletons. This is the compatibility facade
    /// named in P4-1: call sites that are not migrated yet keep reading the same statics, and the
    /// Game layer keeps seeing one boot source per start.
    /// </summary>
    internal sealed class LegacyPlayBootPublisher : IPlayBootPublisher
    {
        public LiveBootState Capture()
        {
            return new LiveBootState(UserDataManager.Instance.FreeLiveBootData, EntryPoint.PlayMode);
        }

        public void Publish(FreeLiveBootData boot)
        {
            UserDataManager.Instance.FreeLiveBootData = boot;
            EntryPoint.PlayMode = PlayMode.SoloLive;
        }

        public void Restore(LiveBootState state)
        {
            UserDataManager.Instance.FreeLiveBootData = state.Boot;
            EntryPoint.PlayMode = state.Mode;
        }
    }

    /// <summary>Screen, scene and transition state read from the retained screen stack.</summary>
    internal sealed class LegacyPlaySurface : IPlaySurface
    {
        public bool Exists
        {
            get { return ScreenManager.ExistsInstance && SceneManager.Instance != null; }
        }

        public SceneManager.Scene CurrentScene
        {
            get
            {
                SceneManager sceneManager = SceneManager.Instance;
                return sceneManager != null ? sceneManager.CurrentScene : SceneManager.Scene.None;
            }
        }

        public bool IsLibraryReady
        {
            get
            {
                ILibraryPage page = LibraryPageRegistry.Current;
                if (page != null)
                {
                    return page.IsReady;
                }

                return ScreenManager.ExistsInstance && ScreenManager.Instance.IsActiveScreen(MenuScreenType.MusicScoreMakerTop);
            }
        }

        public bool IsLiveLoadingReady
        {
            get
            {
                if (!ScreenManager.ExistsInstance)
                {
                    return false;
                }

                ScreenManager screens = ScreenManager.Instance;
                return screens != null
                    && screens.TryGetScreenLayerData(MenuScreenType.LiveLoading, out ScreenLayerData loading)
                    && loading != null
                    && loading.LoadScreenLayerPrefab() != null;
            }
        }

        public void BeginSession(string chartHash)
        {
            PlaySessionContext.Begin(chartHash);
        }

        public void ForceFinishTransition()
        {
            LiveTransitioner.SafeForceFinish(null);
        }

        public void PushLiveLoading()
        {
            ScreenManager screens = ScreenManager.Instance;
            if (screens == null)
            {
                throw new InvalidOperationException("The screen manager is not available.");
            }
            screens.PushUIScreen(MenuScreenType.LiveLoading, false);
        }

        public void SubscribeSceneExit(Action handler)
        {
            SceneManager sceneManager = SceneManager.Instance;
            if (sceneManager == null)
            {
                throw new InvalidOperationException("The scene manager is not available.");
            }
            sceneManager.OnExitScene += handler;
        }

        public void UnsubscribeSceneExit(Action handler)
        {
            SceneManager sceneManager = SceneManager.Instance;
            if (sceneManager != null)
            {
                sceneManager.OnExitScene -= handler;
            }
        }
    }

    /// <summary>Registers package audio through the retained external-audio path.</summary>
    internal sealed class LegacyPackageAudioRegistrar : IPackageAudioRegistrar
    {
        public UniTask<bool> RegisterAsync(SongPackage package, CancellationToken cancellationToken)
        {
            return new PackageAudio(package).RegisterAudioAsync(cancellationToken);
        }
    }
}
