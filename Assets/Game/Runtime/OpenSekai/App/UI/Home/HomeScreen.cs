using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using Sekai;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The app's front door: the splash on a cold start, then the home page with its way into the
    /// library and its cards for what comes next. It shares the Library scene with
    /// <see cref="LibraryScreen"/> and moves between the two: the home page hands over by growing
    /// its library card over the screen into the library's ground, and takes back over when the
    /// library's back button (or the Android back key) is pressed. Only one of the two pages is
    /// drawn at a time; each has its own camera.
    ///
    /// The library is always built, even under the splash, so anything that waits for the library
    /// page (the editor's checks and screenshot runs) still finds it ready.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        private const string THUMBNAIL_DIRECTORY = "JacketThumbnails";
        private const int MARQUEE_JACKETS = 8;

        /// <summary>Longest the splash waits for its emblem to be drawn before starting anyway.</summary>
        private const float BRAND_WAIT_SECONDS = 2f;

        private AppContext _context;
        private LibraryScreen _library;
        private ServerScreen _server;
        private Camera _camera;
        private GameObject _canvasRoot;
        private HomeBackdrop _backdrop;
        private HomeView _view;
        private HomeSplash _splash;
        private BrandEmblem _emblem;
        private HomeAboutPanel _about;
        private JacketThumbnails _thumbnails;
        private CancellationTokenSource _jacketLoad;
        private MotionHandle _idle;
        private int _idleLoops;
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

        /// <summary>True once the emblem's layers are drawn (they are drawn off the main thread).</summary>
        public bool IsBrandReady => BrandSprites.Loaded != null;

        /// <param name="library">The library page this home page hands over to and takes back from.</param>
        /// <param name="playIntro">True on a cold start: the splash plays, then the home page shows.
        /// False when the scene comes back from a live: the library shows and the home page waits.</param>
        public static HomeScreen Create(AppContext context, LibraryScreen library, bool playIntro)
        {
            var host = new GameObject("HomeScreen");
            HomeScreen screen = host.AddComponent<HomeScreen>();
            screen.Build(context, library);
            if (playIntro)
            {
                screen.StartIntroAsync().Forget();
            }
            else
            {
                screen.HideHome();
                library?.SetPresented(true, false);
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
            HideHome();
            _library?.SetPresented(true, false);
        }

        /// <summary>Shows the settled home page at once.</summary>
        public void ShowHomeImmediately()
        {
            _view.StopTransition();
            _splash.Stop();
            _navigating = false;
            _library?.SetPresented(false, false);
            HideServer();
            ShowHome();
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
            _view.ApplyExpansion(HomeDestination.Server, Mathf.Clamp01(amount));
        }

        /// <summary>What tapping the server card does.</summary>
        public void EnterServer() => OpenServer();

        /// <summary>What tapping the library card does.</summary>
        public void EnterLibrary() => OpenLibrary();

        /// <summary>What the library's back button does.</summary>
        public void ReturnHome() => GoHome();

        // ---------------------------------------------------------------- building

        private void Build(AppContext context, LibraryScreen library)
        {
            Current = this;
            _context = context;
            _library = library;
            if (_library != null)
            {
                _library.BackRequested += GoHome;
            }

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
            _view = new HomeView(root, OpenLibrary, OpenAbout, ShowComingSoon, OpenServer);
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
            LoadBrandAsync().Forget();
        }

        private async UniTaskVoid LoadBrandAsync()
        {
            BrandSprites sprites;
            try
            {
                sprites = await BrandSprites.LoadAsync(this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Home] Could not draw the emblem: " + exception.Message);
                return;
            }

            _emblem.ApplySprites(sprites);
            _about.ApplySprites(sprites);
        }

        private async UniTaskVoid StartIntroAsync()
        {
            _library?.SetPresented(false, false);
            ShowHome();
            _view.Interactable = false;
            _splash.Evaluate(0f);

            // The splash opens on the bare ground for a moment anyway; wait there for the emblem's
            // layers so it never flies in empty. A slow device starts on time without them.
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float deadline = Time.realtimeSinceStartup + BRAND_WAIT_SECONDS;
            while (BrandSprites.Loaded == null && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (this == null || !_homeShown || _splash.IsPlaying)
            {
                return;
            }

            _splash.Play(OnIntroDone);
        }

        private void OnIntroDone()
        {
            _view.Interactable = true;
        }

        // ---------------------------------------------------------------- navigation

        private void OpenLibrary()
        {
            if (_navigating || _library == null)
            {
                return;
            }

            _navigating = true;
            UiSound.Play(UiCue.Forward);
            _view.PlayExpand(() =>
            {
                HideHome();
                _library.SetPresented(true, true);
                _navigating = false;
            });
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

        /// <summary>A level downloaded from a server should start: the library takes over and starts it.</summary>
        private void PlayFromServer(string packageDirectory)
        {
            if (_navigating || _server == null || _library == null)
            {
                return;
            }

            _navigating = true;
            _server.PlayExit(() =>
            {
                _server.SetPresented(false, false);
                _library.SetPresented(true, false);
                _navigating = false;
                _library.PlayPackage(packageDirectory);
            });
        }

        private ServerScreen EnsureServer()
        {
            if (_server != null)
            {
                return _server;
            }

            _server = ServerScreen.Create();
            _server.transform.SetParent(transform, false);
            _server.BackRequested += ReturnFromServer;
            _server.PlayRequested += PlayFromServer;
            _server.LibraryChanged += OnServerImported;
            return _server;
        }

        private void HideServer()
        {
            if (_server != null && _server.IsPresented)
            {
                _server.SetPresented(false, false);
            }
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
        }

        private void ShowHome()
        {
            _homeShown = true;
            _canvasRoot.SetActive(true);
            _camera.enabled = true;
            _quitArmedUntil = -1f;
            RefreshLibrarySummary();
            StartIdle();
        }

        private void HideHome()
        {
            _homeShown = false;
            _idle.TryCancel();
            _idle = default;
            _about.Hide();
            _canvasRoot.SetActive(false);
            _camera.enabled = false;
        }

        private void Update()
        {
            if (!NativeInput.BackPressedThisFrame || _navigating)
            {
                return;
            }

            if (_homeShown)
            {
                HandleHomeBack();
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

            _idleLoops = 0;
            _idleLast = 0f;
            float period = HomeTheme.Motion.IDLE_PERIOD;
            _idle = LMotion.Create(0f, period, period)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(OnIdle);
        }

        private void OnIdle(float time)
        {
            if (time < _idleLast)
            {
                _idleLoops++;
            }

            _idleLast = time;
            TickIdle(_idleLoops * (double)HomeTheme.Motion.IDLE_PERIOD + time);
        }

        private void TickIdle(double time)
        {
            _backdrop.Tick(time);
            _view.Tick(time);
        }

        // ---------------------------------------------------------------- library summary

        /// <summary>
        /// Counts packages and plays, finds the last song played, and loads the jackets for the
        /// library card. Packages and history both come from caches the library page already
        /// filled, so nothing is read from disk here but thumbnails.
        /// </summary>
        private void RefreshLibrarySummary()
        {
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

            CancelJacketLoad();
            _jacketLoad = new CancellationTokenSource();
            LoadJacketsAsync(jackets, recent, _jacketLoad.Token).Forget();
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

            if (_server != null)
            {
                _server.BackRequested -= ReturnFromServer;
                _server.PlayRequested -= PlayFromServer;
                _server.LibraryChanged -= OnServerImported;
            }

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
