using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
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
                if (UI.HomeScreen.Current != null && UI.HomeScreen.Current.IsServerShown) return true;
                ILibraryPage page = LibraryPageRegistry.Current;
                if (page != null)
                {
                    return page.IsReady;
                }

                return ScreenManager.ExistsInstance && ScreenManager.Instance.IsActiveScreen(MenuScreenType.MusicScoreMakerTop);
            }
        }

        public async UniTask<bool> PrepareLiveLoadingAsync(CancellationToken token)
        {
            if (!ScreenManager.ExistsInstance) return false;
            ScreenManager screens = ScreenManager.Instance;
            return screens != null && screens.TryGetScreenLayerData(MenuScreenType.LiveLoading, out var loading)
                && await loading.LoadScreenLayerPrefabAsync(token) != null;
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
            if (screens.IsUILayerWorking || screens.IsActiveScreen(MenuScreenType.LiveLoading))
            {
                throw new InvalidOperationException("The live loading screen could not be opened.");
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

    /// <summary>
    /// Registers package audio through the retained external-audio path, and sets the live's music
    /// gain from the song's loudness. A song the library has not measured yet is measured here from
    /// the clip already decoded for the live, and the result goes into the library cache.
    /// </summary>
    internal sealed class LegacyPackageAudioRegistrar : IPackageAudioRegistrar
    {
        private readonly SongPackageManager _packages;
        internal LegacyPackageAudioRegistrar(SongPackageManager packages) => _packages = packages;

        public async UniTask<bool> RegisterAsync(SongPackage package, CancellationToken cancellationToken)
        {
            SoundManager.Instance.IngameBgmGain = 1f;
            var audio = new PackageAudio(package);
            if (!await audio.RegisterAudioAsync(cancellationToken))
            {
                return false;
            }

            SongPackageItem item = _packages.FindCachedItem(package.RootDirectory);
            float? lufs = item?.LoudnessLufs;
            if (item == null || !item.LoudnessMeasured)
            {
                lufs = await LoudnessMeter.MeasureAsync(audio.GetAudioClip(), cancellationToken);
                if (item != null && _packages.SetLoudness(item, lufs))
                {
                    _packages.SaveLoudness();
                }
            }

            SoundManager.Instance.IngameBgmGain = LoudnessNormalization.GainFor(lufs);
            return true;
        }

        public void Release() => SoundManager.Instance.ReleasePackageAudio();
    }

    /// <summary>Deck selection still lives on the retained player-data singleton.</summary>
    internal sealed class LegacyLiveDeckSelection : ILiveDeckSelection
    {
        public int SelectedDeckId
        {
            get { return UserDataManager.Instance.SelectedDeckId; }
        }
    }
}
