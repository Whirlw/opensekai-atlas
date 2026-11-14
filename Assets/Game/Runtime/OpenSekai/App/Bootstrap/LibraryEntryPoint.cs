using CP;
using Sekai;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using SekaiSceneManager = Sekai.SceneManager;
using OpenSekai.Shared.Settings;
using CriWare;
using OpenSekai.App.UI;
using Sekai.Core.Live;
using Sekai.UI;

namespace OpenSekai.App.Bootstrap
{
    /// <summary>
    /// Starts the player library without the chart editor's boot or return state.
    /// The existing screen and infrastructure services are reused during migration.
    /// </summary>
    public sealed class LibraryEntryPoint : SceneEntryPoint
    {
        private SekaiSceneManager _sceneManager;

        protected override void Awake()
        {
            base.Awake();
            _sceneManager = SekaiSceneManager.Instance;
            EnsureEventSystem();
            TextMeshProUtility.SetupBuiltinFontAsset();
            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;

            AssetBundleMetaManager.Instance.Initialize();
            AssetBundleManager.Instance.Initialize();
            SoundUtility.SetupGlobalSeSettings(GlobalSeSettings.Normal);

            LiveSettingData settings = LiveSettingsStore.Shared.Load();
            FramerateUtility.ApplyFrameRateLimit(settings.FrameRateLimit);
        }

        protected override void Start()
        {
            ApplicationLocalSettings settings = ApplicationLocalSettings.LoadFromStorage();
            SoundManager.Instance.SetupVolume(
                1f, settings.SystemVolume.Bgm, settings.SystemVolume.Se, settings.SystemVolume.Voice);

            // A delivered sound pack lives in StreamingAssets/sound; it replaces the cue sheets.
            SoundManager.Instance.LoadExternalSoundPack();

            // No menu sound bundle: the library page has no UI sounds, and loading it pulled a 6.2 MB
            // third-party cue sheet that nothing ever played.

            ScreenManager screens = ScreenManager.Instance;
            screens.CreateEmptyBaseCamera();
            bool returningFromTransition = _sceneManager.PrevScene == SekaiSceneManager.Scene.TransitionBlank;
            if (returningFromTransition)
            {
                screens.SetScreenCoverDirect(Color.black);
            }
            screens.FadeIn(0f, returningFromTransition ? 0.3f : 0f, null);
            screens.AddScreen(MenuScreenType.Header);
            screens.AddScreen(MenuScreenType.TouchEffect);
            LiveTransitioner.SafeForceFinish(null);

            // Lightweight library page: no ScreenLayerData registration and no editor page.
            // The composition root for this scene: everything the page needs is assembled here and
            // handed over, so no App-layer type has to reach for a singleton.
            AppContext context = AppContext.CreateDefault();
            LibraryScreen library = LibraryScreen.Create(context);
            if (library == null)
            {
                Debug.LogError("[Library] Could not create the lightweight library screen.");
            }

            Debug.Log("[Library] Player library started (lightweight screen).");
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                NativeInput.ConfigureEventSystem(existing);
                return;
            }

            GameObject prefab = ResourceCatalog.EventSystem.Load();
            GameObject input = prefab != null
                ? Instantiate(prefab)
                : new GameObject("EventSystem", typeof(EventSystem));
            input.name = "EventSystem";
            NativeInput.ConfigureEventSystem(input.GetComponent<EventSystem>());
            DontDestroyOnLoad(input);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SoundManager.Instance.Pause();
            }
            else
            {
                SoundManager.Instance.Resume();
            }
        }

        private void OnDestroy()
        {
            if (_sceneManager != null)
            {
                _sceneManager.OnExitScene -= ExitScene;
            }
        }
    }
}