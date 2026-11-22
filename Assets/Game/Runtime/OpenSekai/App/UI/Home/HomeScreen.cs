using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using OpenSekai.Infrastructure.Packages;
using Sekai;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The app's front door: the splash on a cold start, then the home page with its way into the
    /// library and its cards for the server and resource pack parts. It shares the Library scene with
    /// <see cref="LibraryScreen"/> and moves between the two: the home page hands over by growing
    /// its library card over the screen into the library's ground, and takes back over when the
    /// library's back button (or the Android back key) is pressed. Only one of the two pages is
    /// drawn at a time; each has its own camera.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        private const string THUMBNAIL_DIRECTORY = "JacketThumbnails";
        private const int MARQUEE_JACKETS = 8;

        // The hidden server page stack is carried only for a server-started live.
        private static ServerScreen _serverReturningFromLive;

        private AppContext _context;
        private LibraryScreen _library;
        private bool _libraryCovered;
        private int _homeShownFrame;
        private bool _introSummaryPending;
        private bool _showHomeWhenReady;
        private bool _homeJacketsReady;
        private ServerScreen _server;
        private PackScreen _packs;
        private PackOrigin _packsOrigin;
        private ProfileScreen _profile;
        private DanScreen _dan;
        private AchievementScreen _achievements;
        private PlayerProfileStore _profileStore;
        private CancellationTokenSource _serverPlayCancellation;
        private LibraryLoadingOverlay _serverLoading;
        private Camera _camera;
        private GameObject _canvasRoot;
        private HomeBackdrop _backdrop;
        private HomeView _view;
        private HomeSplash _splash;
        private BrandEmblem _emblem;
        private HomeAboutPanel _about;
        private LibrarySettingsPanel _settings;
        private JacketThumbnails _thumbnails;
        private CancellationTokenSource _jacketLoad;
        private MotionHandle _idle;
        private double _idleTime;
        private float _idleLast;
        private float _quitArmedUntil = -1f;
        private bool _homeShown;
        private bool _navigating;

        /// <summary>The home page of the running Library scene, if it has one.</summary>
        public static HomeScreen Current { get; private set; }

        /// <summary>Seconds from the first frame of the splash until the home page has settled.</summary>
        public static float IntroLength => HomeSplash.Length;

        public bool IsHomeShown => _homeShown;

        /// <summary>True while the server part is on screen.</summary>
        public bool IsServerShown => _server != null && _server.IsPresented;

        /// <summary>True while the resource pack part is on screen.</summary>
        public bool IsPacksShown => _packs != null && _packs.IsPresented;

        /// <summary>True while the 个人信息 part is on screen.</summary>
        public bool IsProfileShown => _profile != null && _profile.IsPresented;

        /// <summary>True while the 段位 part is on screen.</summary>
        public bool IsDanShown => _dan != null && _dan.IsPresented;

        /// <summary>True while the 成就 part is on screen.</summary>
        public bool IsAchievementsShown => _achievements != null && _achievements.IsPresented;

        /// <summary>The home page that is on screen once it settles: 0 the first, 1 the second.</summary>
        public int HomePage => _view.Page;

        /// <summary>
        /// The profile the 个人信息 card and part read; the local one unless a tool swapped in another
        /// (screenshots use a sample so they never show or change the player's own).
        /// </summary>
        internal PlayerProfileStore ProfileStore
        {
            get => _profileStore ?? PlayerProfileStore.Local;
            set
            {
                _profileStore = value;
                if (_profile != null) _profile.Store = value;
                if (_dan != null) _dan.Store = value;
                if (_achievements != null) _achievements.Store = value;
            }
        }

        /// <summary>Where back from the resource pack part leads: the page it was opened from.</summary>
        private enum PackOrigin
        {
            Home,
            Library,
            Server,
            Profile,
            Dan,
            Achievements
        }

        /// <summary>True once the emblem has its bundled layers.</summary>
        public bool IsBrandReady => _emblem != null;

        /// <param name="coldStart">Open the home page on launch, or restore the previous page after a live.</param>
        public static HomeScreen Create(AppContext context, bool coldStart, bool playIntro)
        {
            var host = new GameObject("HomeScreen");
            HomeScreen screen = host.AddComponent<HomeScreen>();
            screen.Build(context);
            if (coldStart)
            {
                if (playIntro) screen.StartIntro();
                else screen.StartWithoutIntro();
            }
            else
            {
                screen.HideHome();
                if (!screen.RestoreDanAfterCourse() && !screen.RestoreServerAfterLive()) screen.ShowReadyLibrary(false);
            }

            return screen;
        }

        // ---------------------------------------------------------------- tools (editor checks, screenshots)

        /// <summary>Shows the library page at once, as if the player had gone in.</summary>
        public void ShowLibraryImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            HideServer();
            HidePacks();
            HideProfile();
            HideDan();
            HideAchievements();
            HideHome();
            _view.TurnTo(0, false);
            ShowReadyLibrary(false);
        }

        /// <summary>Shows the settled home page at once.</summary>
        public void ShowHomeImmediately() => ShowSettledHome(refreshSummary: true);

        private void ShowSettledHome(bool refreshSummary)
        {
            _view.StopTransition();
            _splash.Stop();
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            HidePacks();
            HideProfile();
            HideDan();
            HideAchievements();
            ShowHome(refreshSummary);
            _about.Hide();
            _splash.Evaluate(HomeSplash.Length);
            _view.ApplyExpansion(0f);
            _view.Interactable = true;
        }

        /// <summary>
        /// Holds the splash at <paramref name="seconds"/> (see <see cref="IntroLength"/>) with the
        /// backdrop's drift frozen at the same time, so the same time always gives the same frame.
        /// </summary>
        public void SeekIntro(float seconds)
        {
            _view.TurnTo(0, false);
            if (!_homeShown)
            {
                _library?.SetPresented(false, false);
                ShowHome();
            }

            _idle.TryCancel();
            _idle = default;
            TickIdle(seconds);
            _splash.Seek(seconds);
        }

        /// <summary>
        /// Holds the hand-over to the library at <paramref name="amount"/> (0 the page at rest, 1 the
        /// library card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(0, false);
            _view.ApplyExpansion(Mathf.Clamp01(amount));
        }

        /// <summary>Shows the server list at once, as if the player had gone in.</summary>
        public void ShowServerImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            _library?.SetPresented(false, false);
            HidePacks();
            HideProfile();
            HideDan();
            HideAchievements();
            HideHome();
            EnsureServer().SetPresented(true, false);
        }

        /// <summary>
        /// Holds the hand-over to the server part at <paramref name="amount"/> (0 the page at rest,
        /// 1 the server card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekServerHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(0, false);
            _view.ApplyExpansion(HomeDestination.Server, Mathf.Clamp01(amount));
        }

        /// <summary>Shows the resource pack part at once, as if the player had gone in.</summary>
        public void ShowPacksImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            HideProfile();
            HideDan();
            HideAchievements();
            HideHome();
            _packsOrigin = PackOrigin.Home;
            EnsurePacks().SetPresented(true, false);
        }

        /// <summary>
        /// Holds the hand-over to the resource pack part at <paramref name="amount"/> (0 the page at
        /// rest, 1 the 资源包 card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekPacksHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(0, false);
            _view.ApplyExpansion(HomeDestination.Packs, Mathf.Clamp01(amount));
        }

        /// <summary>Shows the 个人信息 part at once, as if the player had gone in.</summary>
        public void ShowProfileImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            HidePacks();
            HideDan();
            HideAchievements();
            HideHome();
            _view.TurnTo(1, false);
            EnsureProfile().SetPresented(true, false);
        }

        /// <summary>Shows the 段位 part at once, as if the player had gone in.</summary>
        public void ShowDanImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            HidePacks();
            HideProfile();
            HideAchievements();
            HideHome();
            _view.TurnTo(1, false);
            EnsureDan().SetPresented(true, false);
        }

        /// <summary>Shows the 成就 part at once, as if the player had gone in.</summary>
        public void ShowAchievementsImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _splash.Evaluate(HomeSplash.Length);
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            HidePacks();
            HideProfile();
            HideDan();
            HideHome();
            _view.TurnTo(1, false);
            EnsureAchievements().SetPresented(true, false);
        }

        /// <summary>
        /// Holds the hand-over to the 成就 part at <paramref name="amount"/> (0 the second page at
        /// rest, 1 the 成就 card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekAchievementsHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(1, false);
            _view.ApplyExpansion(HomeDestination.Achievements, Mathf.Clamp01(amount));
        }

        /// <summary>
        /// Holds the hand-over to the 段位 part at <paramref name="amount"/> (0 the second page at
        /// rest, 1 the 段位 card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekDanHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(1, false);
            _view.ApplyExpansion(HomeDestination.Dan, Mathf.Clamp01(amount));
        }

        /// <summary>
        /// Holds the hand-over to the 个人信息 part at <paramref name="amount"/> (0 the second page at
        /// rest, 1 the 个人信息 card covering the screen), for screenshots of the transition.
        /// </summary>
        public void SeekProfileHandoff(float amount)
        {
            ShowHomeImmediately();
            _idle.TryCancel();
            _idle = default;
            TickIdle(IntroLength);
            _view.TurnTo(1, false);
            _view.ApplyExpansion(HomeDestination.Profile, Mathf.Clamp01(amount));
        }

        /// <summary>
        /// Holds the home page between its two pages: 0 the first, 1 the second, in between part of
        /// each, as a swipe leaves them. Whole numbers settle on that page.
        /// </summary>
        public void SeekHomePage(float progress)
        {
            if (Mathf.Approximately(progress, Mathf.Round(progress)))
            {
                _view.TurnTo(Mathf.RoundToInt(progress), false);
                return;
            }

            _view.SeekPage(progress);
        }

        /// <summary>What a swipe or the footer's tabs do: turn the home page to <paramref name="page"/>.</summary>
        public void TurnHomePage(int page) => _view.TurnTo(page, true);

        /// <summary>What tapping the 个人信息 card does.</summary>
        public void EnterProfile() => OpenProfile();

        /// <summary>What tapping the 段位 card does.</summary>
        public void EnterDan() => OpenDan();

        /// <summary>What tapping the 成就 card does.</summary>
        public void EnterAchievements() => OpenAchievements();

        /// <summary>What tapping the 资源包 card does.</summary>
        public void EnterPacks() => OpenPacks();

        /// <summary>What tapping the server card does.</summary>
        public void EnterServer() => OpenServer();

        /// <summary>What tapping the library card does.</summary>
        public void EnterLibrary() => OpenLibrary();

        /// <summary>What the library's back button does.</summary>
        public void ReturnHome() => GoHome();

        // ---------------------------------------------------------------- building

        private void Build(AppContext context)
        {
            Current = this;
            _context = context;

            _camera = CreateCamera();
            _canvasRoot = new GameObject("HomeCanvas", typeof(RectTransform));
            _canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRoot.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)_canvasRoot.transform;
            _backdrop = new HomeBackdrop(root);
            _view = new HomeView(root, OpenLibrary, OpenAbout, OpenSettings, ShowComingSoon, OpenServer, OpenPacks, OpenProfile, OpenDan,
                OpenAchievements);
            if (ServerChartDownloads.Current != null)
            {
                ServerChartDownloads.Current.StatusChanged += ShowServerStatus;
                ServerChartDownloads.Current.Imported += OnServerImported;
            }
            _emblem = new BrandEmblem(_view.BrandSlot, "Emblem");
            LibraryUi.Stretch(_emblem.Root);
            _splash = new HomeSplash(root, _backdrop, _emblem, _view);
            _about = new HomeAboutPanel(root);

            _thumbnails = new JacketThumbnails(Path.Combine(Application.temporaryCachePath, THUMBNAIL_DIRECTORY));
        }

        private void StartIntro()
        {
            _library?.SetPresented(false, false);
            _view.TurnTo(0, false);
            ShowHome(refreshSummary: false);
            _introSummaryPending = true;
            _view.Interactable = false;
            _splash.Play(OnIntroDone);
        }

        private void StartWithoutIntro()
        {
            _canvasRoot.SetActive(false);
            _showHomeWhenReady = true;
            RefreshLibrarySummary();
        }

        private void OnIntroDone()
        {
            _view.Interactable = true;
        }

        // ---------------------------------------------------------------- navigation

        private void ShowReadyLibrary(bool animate)
        {
            var library = EnsureLibrary();
            library.FinishBuild();
            library.SetPresented(true, animate);
        }

        private LibraryScreen EnsureLibrary()
        {
            if (_library == null)
            {
                _library = LibraryScreen.BeginBuild(_context);
                _library.BackRequested += GoHome;
            }

            return _library;
        }

        private void OpenLibrary()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            if (_splash.IsPlaying)
            {
                // The card accepts taps during the last moments of the home entrance.
                _splash.Stop();
                _splash.Evaluate(HomeSplash.Length);
            }
            UiSound.Play(UiCue.Forward);
            _view.PlayExpand(() => _libraryCovered = true);
        }

        private void GoHome()
        {
            if (_navigating || _homeShown || _library == null)
            {
                return;
            }

            _navigating = true;
            _library.PlayExit(() =>
            {
                _library.SetPresented(false, false);
                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(() => _navigating = false);
            });
        }

        /// <summary>The server card grows over the page, then the server list rises out of it.</summary>
        private void OpenServer()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            ServerScreen server = EnsureServer();
            _view.PlayExpand(HomeDestination.Server, () =>
            {
                HideHome();
                server.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>The server part falls into the ground, then its card shrinks back into the page.</summary>
        private void ReturnFromServer()
        {
            if (_navigating || _server == null || !_server.IsPresented)
            {
                return;
            }

            _navigating = true;
            _server.PlayExit(() =>
            {
                _server.SetPresented(false, false);
                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(HomeDestination.Server, () => _navigating = false);
            });
        }

        /// <summary>The 资源包 card grows over the page, then the resource pack part rises out of it.</summary>
        private void OpenPacks()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            PackScreen packs = EnsurePacks();
            _packsOrigin = PackOrigin.Home;
            _view.PlayExpand(HomeDestination.Packs, () =>
            {
                HideHome();
                packs.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>
        /// Opens the resource pack part from wherever the player is: from the home page through its
        /// card, from the library or the server part through the ground-colored curtain. Back then
        /// returns to that page. The settings panel's 资源包 row uses this.
        /// </summary>
        internal void OpenResourcePacks()
        {
            if (_navigating || IsPacksShown)
            {
                return;
            }

            if (_homeShown)
            {
                _settings?.Hide();
                if (_splash.IsPlaying)
                {
                    _splash.Stop();
                    _splash.Evaluate(HomeSplash.Length);
                }

                // The 资源包 card is on the first page.
                _view.TurnTo(0, false);
                OpenPacks();
                return;
            }

            if (_profile != null && _profile.IsPresented)
            {
                CoverThenShowPacks(PackOrigin.Profile, _profile.PlayExit, () => _profile.SetPresented(false, false));
                return;
            }

            if (_dan != null && _dan.IsPresented)
            {
                CoverThenShowPacks(PackOrigin.Dan, _dan.PlayExit, () => _dan.SetPresented(false, false));
                return;
            }

            if (_achievements != null && _achievements.IsPresented)
            {
                CoverThenShowPacks(PackOrigin.Achievements, _achievements.PlayExit, () => _achievements.SetPresented(false, false));
                return;
            }

            if (_server != null && _server.IsPresented)
            {
                CoverThenShowPacks(PackOrigin.Server, _server.PlayExit, () => _server.SetPresented(false, false));
                return;
            }

            if (_library != null && _library.IsPresented)
            {
                CoverThenShowPacks(PackOrigin.Library, _library.PlayExit, () => _library.SetPresented(false, false));
            }
        }

        private void CoverThenShowPacks(PackOrigin origin, Action<Action> playExit, Action hide)
        {
            _navigating = true;
            UiSound.Play(UiCue.Forward);
            PackScreen packs = EnsurePacks();
            playExit(() =>
            {
                hide();
                _packsOrigin = origin;
                packs.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>The resource pack part falls into the ground, then the page it came from returns.</summary>
        private void ReturnFromPacks()
        {
            if (_navigating || _packs == null || !_packs.IsPresented)
            {
                return;
            }

            _navigating = true;
            PackOrigin origin = _packsOrigin;
            _packs.PlayExit(() =>
            {
                _packs.SetPresented(false, false);
                _packsOrigin = PackOrigin.Home;
                if (origin == PackOrigin.Library && _library != null)
                {
                    _library.SetPresented(true, true);
                    _navigating = false;
                    return;
                }

                if (origin == PackOrigin.Server && _server != null)
                {
                    _server.SetPresented(true, true);
                    _navigating = false;
                    return;
                }

                if (origin == PackOrigin.Profile && _profile != null)
                {
                    _profile.SetPresented(true, true);
                    _navigating = false;
                    return;
                }

                if (origin == PackOrigin.Dan && _dan != null)
                {
                    _dan.SetPresented(true, true);
                    _navigating = false;
                    return;
                }

                if (origin == PackOrigin.Achievements && _achievements != null)
                {
                    _achievements.SetPresented(true, true);
                    _navigating = false;
                    return;
                }

                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(HomeDestination.Packs, () => _navigating = false);
            });
        }

        private PackScreen EnsurePacks()
        {
            if (_packs != null)
            {
                return _packs;
            }

            _packs = PackScreen.Create();
            _packs.transform.SetParent(transform, false);
            _packs.BackRequested += ReturnFromPacks;
            return _packs;
        }

        private void HidePacks()
        {
            if (_packs != null && _packs.IsPresented)
            {
                _packs.SetPresented(false, false);
            }

            _packsOrigin = PackOrigin.Home;
        }

        /// <summary>The 个人信息 card grows over the second page, then the part rises out of it.</summary>
        private void OpenProfile()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            ProfileScreen profile = EnsureProfile();
            _view.PlayExpand(HomeDestination.Profile, () =>
            {
                HideHome();
                profile.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>The 个人信息 part falls into the ground, then its card shrinks back into the second page.</summary>
        private void ReturnFromProfile()
        {
            if (_navigating || _profile == null || !_profile.IsPresented)
            {
                return;
            }

            _navigating = true;
            _profile.PlayExit(() =>
            {
                _profile.SetPresented(false, false);
                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(HomeDestination.Profile, () => _navigating = false);
            });
        }

        private ProfileScreen EnsureProfile()
        {
            if (_profile != null)
            {
                return _profile;
            }

            _profile = ProfileScreen.Create(() => _context.Packages.LoadItems());
            _profile.transform.SetParent(transform, false);
            _profile.Store = _profileStore;
            _profile.BackRequested += ReturnFromProfile;
            _profile.DataChanged += OnProfileChanged;
            return _profile;
        }

        private void HideProfile()
        {
            if (_profile != null && _profile.IsPresented)
            {
                _profile.SetPresented(false, false);
            }
        }

        /// <summary>The 段位 card grows over the second page, then the part rises out of it.</summary>
        private void OpenDan()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            DanScreen dan = EnsureDan();
            _view.PlayExpand(HomeDestination.Dan, () =>
            {
                HideHome();
                dan.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>The 段位 part falls into the ground, then its card shrinks back into the second page.</summary>
        private void ReturnFromDan()
        {
            if (_navigating || _dan == null || !_dan.IsPresented)
            {
                return;
            }

            _navigating = true;
            _dan.PlayExit(() =>
            {
                _dan.SetPresented(false, false);
                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(HomeDestination.Dan, () => _navigating = false);
            });
        }

        private DanScreen EnsureDan()
        {
            if (_dan != null)
            {
                return _dan;
            }

            _dan = DanScreen.Create();
            _dan.transform.SetParent(transform, false);
            _dan.Store = _profileStore;
            _dan.BackRequested += ReturnFromDan;
            return _dan;
        }

        private void HideDan()
        {
            if (_dan != null && _dan.IsPresented)
            {
                _dan.SetPresented(false, false);
            }
        }

        /// <summary>The 成就 card grows over the second page, then the part rises out of it.</summary>
        private void OpenAchievements()
        {
            if (_navigating || !_homeShown)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            AchievementScreen achievements = EnsureAchievements();
            _view.PlayExpand(HomeDestination.Achievements, () =>
            {
                HideHome();
                achievements.SetPresented(true, true);
                _navigating = false;
            });
        }

        /// <summary>The 成就 part falls into the ground, then its card shrinks back into the second page.</summary>
        private void ReturnFromAchievements()
        {
            if (_navigating || _achievements == null || !_achievements.IsPresented)
            {
                return;
            }

            _navigating = true;
            _achievements.PlayExit(() =>
            {
                _achievements.SetPresented(false, false);
                ShowHome();
                _splash.Evaluate(HomeSplash.Length);
                _view.PlayCollapse(HomeDestination.Achievements, () => _navigating = false);
            });
        }

        private AchievementScreen EnsureAchievements()
        {
            if (_achievements != null)
            {
                return _achievements;
            }

            _achievements = AchievementScreen.Create();
            _achievements.transform.SetParent(transform, false);
            _achievements.Store = _profileStore;
            _achievements.BackRequested += ReturnFromAchievements;
            return _achievements;
        }

        private void HideAchievements()
        {
            if (_achievements != null && _achievements.IsPresented)
            {
                _achievements.SetPresented(false, false);
            }
        }

        /// <summary>
        /// After a 段位 course the library scene opens on the 段位 part and its result, once; the
        /// second home page waits underneath for the part's back button.
        /// </summary>
        private bool RestoreDanAfterCourse()
        {
            if (!DanResultHandoff.TakeFinished(DanChallengeService.Local)) return false;
            _library?.SetPresented(false, false);
            _view.TurnTo(1, false);
            DanScreen dan = EnsureDan();
            dan.SetPresented(true, false);
            dan.ShowCourseResult();
            return true;
        }

        /// <summary>Redraws the library after a history change. The home cards refresh on return.</summary>
        private void OnProfileChanged(bool history)
        {
            if (history)
            {
                _library?.Refresh();
            }
        }

        private void OnServerPlayRequested(ServerScreen.PlayRequest request)
        {
            if (_navigating || _server == null || (request.Temporary == null && string.IsNullOrEmpty(request.PackageDirectory))) return;
            StartServerLive(request.PackageDirectory, request.Title, request.IsAuto, request.Temporary).Forget();
        }

        private async UniTaskVoid StartServerLive(string packageDirectory, string title, bool isAuto, TemporarySongPackage temporary)
        {
            _navigating = true;
            var cancellation = new CancellationTokenSource();
            _serverPlayCancellation = cancellation;
            ServerScreen server = _server;
            bool started = false;
            try
            {
                server.Preview.Stop(false);
                if (_serverLoading == null)
                    _serverLoading = LibraryLoadingOverlay.Create(transform, () => _serverPlayCancellation?.Cancel());
                _serverLoading.Show(title, "正在准备…", server.CurrentCover, server.Accent);

                // Keep the page objects, including the search query, virtual list and cover cache.
                // Detach before starting because playback can begin unloading the menu scene.
                server.transform.SetParent(null, false);
                DontDestroyOnLoad(server.gameObject);
                _serverReturningFromLive = server;

                PlayStartResult result = await _context.Playback.StartAsync(
                    new PlayRequest(packageDirectory, isAuto, Sekai.SceneManager.Scene.Library, temporary),
                    cancellation.Token, _serverLoading.SetMessage);
                started = result.Started;
                if (started)
                {
                    server.SetPresented(false, false);
                    _serverLoading.SetMessage("正在进入…");
                }
                else if (result.Status == PlayStartStatus.Failed)
                {
                    server.ShowResult(PlayResult.FromLoadError(title, isAuto, packageDirectory, result.Message));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null)
                    server.ShowResult(PlayResult.FromLoadError(title, isAuto, packageDirectory, "开局失败，请检查运行日志。"));
            }
            finally
            {
                _serverPlayCancellation = null;
                cancellation.Dispose();
                if (!started)
                {
                    if (_serverReturningFromLive == server)
                    {
                        _serverReturningFromLive = null;
                        if (this != null) AttachServerToScene();
                        else if (server != null) Destroy(server.gameObject);
                    }

                    if (this != null)
                    {
                        _serverLoading?.Hide();
                        _navigating = false;
                    }
                }
            }
        }

        private bool RestoreServerAfterLive()
        {
            if (_serverReturningFromLive == null) return false;

            _server = _serverReturningFromLive;
            _serverReturningFromLive = null;
            AttachServerToScene();
            BindServer();
            _library?.SetPresented(false, false);
            _server.SetPresented(true, false);
            _server.ShowResult(PlayResultHandoff.Take());
            return true;
        }

        private void AttachServerToScene()
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_server.gameObject, gameObject.scene);
            _server.transform.SetParent(transform, false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetServerReturn() => _serverReturningFromLive = null;

        private ServerScreen EnsureServer()
        {
            if (_server != null)
            {
                return _server;
            }

            _server = ServerScreen.Create();
            _server.transform.SetParent(transform, false);
            BindServer();
            return _server;
        }

        private void BindServer()
        {
            _server.BackRequested += ReturnFromServer;
            _server.PlayRequested += OnServerPlayRequested;
            _server.LibraryChanged += OnServerImported;
        }

        private void HideServer()
        {
            if (_server != null && _server.IsPresented)
            {
                _server.SetPresented(false, false);
            }
        }

        private void OpenSettings()
        {
            if (_navigating || _splash.IsPlaying) return;
            if (_settings == null) _settings = LibrarySettingsPanel.Create(_canvasRoot.transform, null);
            _settings.Show();
        }

        private void OpenAbout()
        {
            if (!_navigating)
            {
                _about.Show();
            }
        }

        private void ShowComingSoon(string part)
        {
            UiSound.Play(UiCue.Blocked);
            _view.ShowToast(part + "还在路上，敬请期待");
        }

        private void ShowServerStatus(string message) => _view.ShowToast(message);

        private void OnServerImported()
        {
            _library?.Refresh();
            RefreshLibrarySummary();
            Debug.Log("[HomeScreen] Library refreshed after server import.");
        }

        private void ShowHome(bool refreshSummary = true)
        {
            _showHomeWhenReady = false;
            _homeShown = true;
            _homeShownFrame = Time.frameCount;
            _introSummaryPending = false;
            _libraryCovered = false;
            _canvasRoot.SetActive(true);
            _camera.enabled = true;
            _quitArmedUntil = -1f;
            if (refreshSummary) RefreshLibrarySummary();
            StartIdle();
        }

        private void HideHome()
        {
            _showHomeWhenReady = false;
            _homeShown = false;
            _libraryCovered = false;
            _splash.Stop();
            _idle.TryCancel();
            _idle = default;
            _about.Hide();
            _settings?.Hide();
            _canvasRoot.SetActive(false);
            _camera.enabled = false;
        }

        private void Update()
        {
            if (_showHomeWhenReady)
            {
                if (_homeJacketsReady) ShowSettledHome(refreshSummary: false);
                return;
            }
            if (_homeShown)
            {
                if (_introSummaryPending && Time.frameCount > _homeShownFrame)
                {
                    _introSummaryPending = false;
                    RefreshLibrarySummary();
                }
                if (_splash.IsPlaying && !_libraryCovered)
                {
                    if (Application.isFocused)
                    {
                        _splash.Tick(Time.unscaledDeltaTime, _homeJacketsReady);
                    }
                }
                else if (_homeShownFrame < Time.frameCount)
                {
                    // Let the settled home page render before spending frames on the hidden library.
                    var library = EnsureLibrary();
                    library.AdvanceBuild();
                    if (_libraryCovered && library.IsReady)
                    {
                        HideHome();
                        library.SetPresented(true, true);
                        _navigating = false;
                    }
                }
            }

            if (!NativeInput.BackPressedThisFrame || DialogBase.OwnsHardwareBack) return;
            if (_serverPlayCancellation != null)
            {
                _serverPlayCancellation.Cancel();
                return;
            }

            if (_navigating) return;

            if (_homeShown)
            {
                HandleHomeBack();
                return;
            }

            if (_packs != null && _packs.IsPresented)
            {
                if (!_packs.TryHandleBack())
                {
                    UiSound.Play(UiCue.Back);
                    ReturnFromPacks();
                }

                return;
            }

            if (_profile != null && _profile.IsPresented)
            {
                if (!_profile.TryHandleBack())
                {
                    UiSound.Play(UiCue.Back);
                    ReturnFromProfile();
                }

                return;
            }

            if (_dan != null && _dan.IsPresented)
            {
                if (!_dan.TryHandleBack())
                {
                    UiSound.Play(UiCue.Back);
                    ReturnFromDan();
                }

                return;
            }

            if (_achievements != null && _achievements.IsPresented)
            {
                if (!_achievements.TryHandleBack())
                {
                    UiSound.Play(UiCue.Back);
                    ReturnFromAchievements();
                }

                return;
            }

            if (_server != null && _server.IsPresented)
            {
                if (!_server.TryHandleBack())
                {
                    UiSound.Play(UiCue.Back);
                    ReturnFromServer();
                }

                return;
            }

            if (_library != null && _library.isActiveAndEnabled && !_library.TryHandleBack())
            {
                UiSound.Play(UiCue.Back);
                GoHome();
            }
        }

        private void HandleHomeBack()
        {
            if (_splash.IsPlaying)
            {
                _splash.Skip();
                return;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return;
            }

            if (_about.IsOpen)
            {
                _about.Hide();
                UiSound.Play(UiCue.Close);
                return;
            }

            if (Time.unscaledTime < _quitArmedUntil)
            {
                Application.Quit();
                return;
            }

            _quitArmedUntil = Time.unscaledTime + HomeTheme.Motion.QUIT_WINDOW;
            _view.ShowToast("再按一次返回键退出");
        }

        // ---------------------------------------------------------------- idle clock

        /// <summary>
        /// One looping motion drives every idle animation on the page. Its value wraps each period;
        /// the page gets a continuous clock built from the loops, so nothing jumps at the wrap.
        /// </summary>
        private void StartIdle()
        {
            if (_idle.IsActive())
            {
                return;
            }

            _idleTime = 0d;
            _idleLast = 0f;
            float period = HomeTheme.Motion.IDLE_PERIOD;
            _idle = LMotion.Create(0f, period, period)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(OnIdle);
        }

        private void OnIdle(float time)
        {
            float elapsed = time - _idleLast;
            if (elapsed < 0f) elapsed += HomeTheme.Motion.IDLE_PERIOD;
            // A long frame or resuming the app should not fling the covers up the card.
            _idleTime += Mathf.Min(elapsed, 0.05f);
            _idleLast = time;
            TickIdle(_idleTime);
        }

        private void TickIdle(double time)
        {
            _backdrop.Tick(time);
            _view.Tick(time);
        }

        // ---------------------------------------------------------------- library summary

        /// <summary>
        /// Counts packages and plays, finds the last song played, and loads the jackets for the
        /// library card independently of the library page.
        /// </summary>
        private void RefreshLibrarySummary()
        {
            RefreshProfileSummary();
            _homeJacketsReady = false;
            CancelJacketLoad();
            IReadOnlyList<SongPackageItem> items;
            IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history;
            try
            {
                items = _context.Packages.LoadItems();
                history = CustomMusicScorePlayHistoryStorage.LoadSummaries();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Home] Could not read the library: " + exception.Message);
                _view.SetSummary(HomeLibrarySummary.Empty);
                _homeJacketsReady = true;
                return;
            }

            var played = new List<(SongPackageItem Item, DateTimeOffset When)>();
            int plays = 0;
            for (int i = 0; i < items.Count; i++)
            {
                SongPackageItem item = items[i];
                if (item?.ScoreKey == null || history == null || !history.TryGetValue(item.ScoreKey, out CustomMusicScorePlayHistorySummary summary)
                    || summary == null)
                {
                    continue;
                }

                plays += summary.PlayCount;
                if (DateTimeOffset.TryParse(summary.LastPlayedAtUtc, out DateTimeOffset when))
                {
                    played.Add((item, when));
                }
            }

            played.Sort((a, b) => b.When.CompareTo(a.When));
            SongPackageItem recent = played.Count > 0 ? played[0].Item : null;
            string recentWhen = played.Count > 0 ? DescribeWhen(played[0].When) : null;
            _view.SetSummary(new HomeLibrarySummary(
                items.Count, plays, recent != null ? LibraryModel.BuildTitle(recent) : null, recentWhen));

            // Jackets: the songs played most recently first, then the newest imports.
            var jackets = new List<SongPackageItem>();
            foreach ((SongPackageItem item, DateTimeOffset _) in played)
            {
                AddJacket(jackets, item);
            }

            var rest = new List<SongPackageItem>(items);
            rest.Sort((a, b) => b.ImportedAt.CompareTo(a.ImportedAt));
            foreach (SongPackageItem item in rest)
            {
                AddJacket(jackets, item);
            }

            _jacketLoad = new CancellationTokenSource();
            LoadJacketsAsync(jackets, recent, _jacketLoad.Token).Forget();
        }

        /// <summary>Reads the profile for the 个人信息 card; a profile that cannot be read says so on the card.</summary>
        private void RefreshProfileSummary()
        {
            try
            {
                PlayerProfileStore store = ProfileStore;
                PlayerProfileSnapshot snapshot = store.Read();
                int plays = store.ReadHistory(0, 1).TotalCount;
                _view.SetProfile(new HomeProfileSummary(snapshot.Appearance.Nickname, snapshot.Appearance.AvatarId,
                    snapshot.Best30.Total, snapshot.Best30.TierId, snapshot.Best30.Entries.Count, plays, ProfileCopy.DanLabel(snapshot), false,
                    snapshot.EquippedDan?.Color));
                int courses = store.ReadDanDefinitions().Count;
                _view.SetDanStatus(courses == 0 ? "开始挑战" : courses + " 个段位  ·  已通过 " + snapshot.EarnedDanIds.Count);
                IReadOnlyList<AchievementProgress> goals = store.ReadAchievements();
                int unlocked = 0;
                foreach (AchievementProgress goal in goals)
                {
                    if (goal.UnlockedAtUtc.HasValue) unlocked++;
                }

                _view.SetAchievementStatus("已解锁 " + unlocked + " / " + goals.Count);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Home] Could not read the player profile: " + exception.Message);
                _view.SetProfile(new HomeProfileSummary(HomeProfileSummary.Empty.Nickname, ProfileAvatars.DEFAULT_ID, 0L, 0, 0, 0, null, true));
            }
        }

        private static void AddJacket(List<SongPackageItem> jackets, SongPackageItem item)
        {
            if (jackets.Count < MARQUEE_JACKETS && item?.Entry != null && item.HasJacket && !jackets.Contains(item))
            {
                jackets.Add(item);
            }
        }

        private async UniTaskVoid LoadJacketsAsync(List<SongPackageItem> items, SongPackageItem recent, CancellationToken cancellationToken)
        {
            var sprites = new List<Sprite>(items.Count);
            try
            {
                foreach (SongPackageItem item in items)
                {
                    JacketThumbnail thumbnail = await _thumbnails.LoadAsync(item.Entry.JacketPath, item.LastWriteTime.Ticks, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (thumbnail.Sprite == null)
                    {
                        continue;
                    }

                    sprites.Add(thumbnail.Sprite);
                    if (item == recent || (recent == null && sprites.Count == 1))
                    {
                        _view.SetRecentJacket(item == recent ? thumbnail.Sprite : null);
                        _view.SetAccent(thumbnail.Palette.Accent, thumbnail.Palette.Glow);
                        _backdrop.SetAccent((Color)thumbnail.Palette.Glow, 0.35f);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _view.SetMarquee(sprites.ToArray());
            _homeJacketsReady = true;
        }

        private void CancelJacketLoad()
        {
            if (_jacketLoad == null)
            {
                return;
            }

            _jacketLoad.Cancel();
            _jacketLoad.Dispose();
            _jacketLoad = null;
        }

        /// <summary>"刚刚", "12 分钟前", "3 小时前", "2 天前", or the date for anything older than a week.</summary>
        internal static string DescribeWhen(DateTimeOffset when)
        {
            TimeSpan ago = DateTimeOffset.UtcNow - when;
            if (ago.TotalMinutes < 1d)
            {
                return "刚刚";
            }

            if (ago.TotalHours < 1d)
            {
                return (int)ago.TotalMinutes + " 分钟前";
            }

            if (ago.TotalDays < 1d)
            {
                return (int)ago.TotalHours + " 小时前";
            }

            if (ago.TotalDays < 7d)
            {
                return (int)ago.TotalDays + " 天前";
            }

            return when.ToLocalTime().ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------------- lifetime

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("HomeScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 101f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            if (ServerChartDownloads.Current != null)
            {
                ServerChartDownloads.Current.StatusChanged -= ShowServerStatus;
                ServerChartDownloads.Current.Imported -= OnServerImported;
            }
            if (Current == this)
            {
                Current = null;
            }

            if (_library != null)
            {
                _library.BackRequested -= GoHome;
            }

            if (_packs != null)
            {
                _packs.BackRequested -= ReturnFromPacks;
            }

            if (_profile != null)
            {
                _profile.BackRequested -= ReturnFromProfile;
                _profile.DataChanged -= OnProfileChanged;
            }

            if (_dan != null)
            {
                _dan.BackRequested -= ReturnFromDan;
            }

            if (_achievements != null)
            {
                _achievements.BackRequested -= ReturnFromAchievements;
            }

            if (_server != null)
            {
                _server.BackRequested -= ReturnFromServer;
                _server.PlayRequested -= OnServerPlayRequested;
                _server.LibraryChanged -= OnServerImported;
            }

            _serverPlayCancellation?.Cancel();
            _idle.TryCancel();
            CancelJacketLoad();
            _splash?.Dispose();
            _view?.Dispose();
            _about?.Dispose();
            _thumbnails?.Dispose();
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
            }
        }
    }
}
