using System;
using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.App.Server;
using OpenSekai.Infrastructure.Sonolus;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 服务器 part of the app: the list of saved servers, and for a Sonolus server its level
    /// home page, searches and level pages, all drawn by the game. A website server opens in the
    /// system WebView instead. Like the home page and the library it builds its own camera and
    /// canvas and shares the Library scene with them; only one of the three is drawn at a time.
    /// The home page decides when it comes and goes (<see cref="SetPresented"/>,
    /// <see cref="PlayExit"/>); inside, pages stack and slide.
    /// </summary>
    public sealed class ServerScreen : MonoBehaviour
    {
        private const float PUSH_OFFSET = 64f;

        private readonly List<ServerPage> _pages = new List<ServerPage>();
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private CanvasGroup _pagesGroup;
        private Image _glowMain;
        private Image _glowSecondary;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private MotionHandle _glowMotion;
        private MotionHandle _pushMotion;
        private Color _glow;
        private Color _accent;
        private UiToast _toast;
        private bool _presented;

        /// <summary>The list page's back button (and back from it): leave the server part.</summary>
        internal event Action BackRequested;

        /// <summary>A level is in the library and should start; the argument is its package folder.</summary>
        internal event Action<string> PlayRequested;

        /// <summary>A download added or replaced a package in the library.</summary>
        internal event Action LibraryChanged;

        internal bool IsPresented => _presented;

        internal RectTransform PageHost { get; private set; }

        /// <summary>Above the pages: modal cards.</summary>
        internal RectTransform ModalLayer { get; private set; }

        /// <summary>Above the modals: dropdown lists.</summary>
        internal RectTransform Overlay { get; private set; }

        internal ServerRegistry Registry { get; private set; }
        internal SonolusLevelInstaller Installer { get; private set; }
        internal RemoteCovers Covers { get; private set; }
        internal SonolusPreviewPlayer Preview { get; private set; }

        /// <summary>Accent of the current cover, for buttons and selection.</summary>
        internal Color32 Accent => (Color32)_accent;

        internal static ServerScreen Create()
        {
            var host = new GameObject("ServerScreen");
            ServerScreen screen = host.AddComponent<ServerScreen>();
            screen.Build();
            return screen;
        }

        // ---------------------------------------------------------------- presentation

        /// <summary>Brings the server part on screen (on the list page) or takes it off.</summary>
        /// <param name="animate">Coming on: rise out of the ground-colored curtain.</param>
        internal void SetPresented(bool presented, bool animate)
        {
            _presented = presented;
            _curtainMotion.TryCancel();
            _curtainMotion = default;
            _curtain.gameObject.SetActive(false);
            _camera.enabled = presented;
            _raycaster.enabled = presented;
            _canvasRoot.SetActive(presented);
            if (!presented)
            {
                Preview.Stop(false);
                _toast.Hide();
                Top?.OnHidden();
                return;
            }

            if (_pages.Count == 0)
            {
                Push(new ServerListPage(this), false);
            }
            else
            {
                Top.OnShown();
            }

            if (!animate)
            {
                return;
            }

            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(1f, 0f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _curtain.gameObject.SetActive(false))
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
            Top?.PlayEnter();
        }

        /// <summary>Lowers the curtain over the page, then runs <paramref name="onCovered"/>.</summary>
        internal void PlayExit(Action onCovered)
        {
            if (!_presented)
            {
                onCovered?.Invoke();
                return;
            }

            Preview.Stop();
            _curtainMotion.TryCancel();
            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtain.raycastTarget = true;
            _curtainMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                .WithEase(LibraryTheme.Motion.EXIT_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() =>
                {
                    _curtainMotion = default;
                    onCovered?.Invoke();
                })
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
        }

        /// <summary>
        /// The Android back key: closes what is open on the top page, else goes back a page.
        /// False on the list page with nothing open, where back leaves the server part.
        /// </summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive() || _pushMotion.IsActive())
            {
                return true;
            }

            ServerPage top = Top;
            if (top != null && top.TryHandleBack())
            {
                return true;
            }

            if (_pages.Count > 1)
            {
                UiSound.Play(UiCue.Back);
                Pop();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- navigation for pages

        internal ServerPage Top => _pages.Count > 0 ? _pages[_pages.Count - 1] : null;

        internal void Push(ServerPage page, bool animate = true)
        {
            ServerPage previous = Top;
            _pages.Add(page);
            previous?.OnHidden();
            page.Root.gameObject.SetActive(true);
            page.OnShown();
            if (previous == null || !animate)
            {
                if (previous != null)
                {
                    previous.Root.gameObject.SetActive(false);
                }

                page.Group.alpha = 1f;
                page.Root.anchoredPosition = Vector2.zero;
                return;
            }

            Slide(previous, page, true);
        }

        /// <summary>Goes back one page; from the list page, leaves the server part.</summary>
        internal void Back()
        {
            if (_pages.Count <= 1)
            {
                BackRequested?.Invoke();
                return;
            }

            Pop();
        }

        internal void Pop()
        {
            if (_pages.Count <= 1)
            {
                return;
            }

            ServerPage leaving = Top;
            _pages.RemoveAt(_pages.Count - 1);
            leaving.OnHidden();
            ServerPage showing = Top;
            showing.Root.gameObject.SetActive(true);
            showing.OnShown();
            Slide(leaving, showing, false, () => leaving.Dispose());
        }

        /// <summary>Opens a saved server: a Sonolus server in its own pages, a website in the WebView.</summary>
        internal void OpenServer(ServerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.Kind == ServerKind.WebView)
            {
                UiSound.Play(UiCue.Forward);
                ServerChartDownloads.Open(entry.Address, entry.Name);
                return;
            }

            if (!SonolusServerAddress.TryParse(entry.Address, out SonolusServerAddress address, out _, out string error))
            {
                ShowToast(error);
                UiSound.Play(UiCue.Error);
                return;
            }

            UiSound.Play(UiCue.Forward);
            Push(new SonolusBrowsePage(this, new SonolusSession(entry, address)));
        }

        internal void OpenLevel(SonolusSession session, SonolusLevel level)
        {
            if (level == null)
            {
                return;
            }

            UiSound.Play(UiCue.Forward);
            Push(new SonolusLevelPage(this, session, level));
        }

        internal void RequestPlay(string packageDirectory) => PlayRequested?.Invoke(packageDirectory);

        internal void NotifyLibraryChanged() => LibraryChanged?.Invoke();

        internal void ShowToast(string message) => _toast.Show(message);

        /// <summary>Moves the background glow to a cover's colors.</summary>
        internal void SetPalette(JacketPalette palette)
        {
            Color fromGlow = _glow;
            Color fromAccent = _accent;
            _glowMotion.TryCancel();
            _glowMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(value =>
                {
                    _glow = Color.Lerp(fromGlow, palette.Glow, value);
                    _accent = Color.Lerp(fromAccent, palette.Accent, value);
                    ApplyGlow();
                });
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            Registry = ServerRegistry.Shared;
            Installer = SonolusLevelInstaller.Shared;
            Covers = new RemoteCovers();
            Preview = gameObject.AddComponent<SonolusPreviewPlayer>();

            _camera = CreateCamera();
            _canvasRoot = new GameObject("ServerCanvas", typeof(RectTransform));
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
            _raycaster = _canvasRoot.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)_canvasRoot.transform;
            Image background = LibraryUi.CreateImage("Background", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(background.rectTransform);
            _glowMain = LibraryUi.CreateImage("GlowMain", root, Color.clear);
            _glowMain.sprite = LibraryShapes.Glow();
            _glowMain.raycastTarget = false;
            LibraryWidgets.Place(_glowMain.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -340f), new Vector2(1900f, 1900f));
            _glowSecondary = LibraryUi.CreateImage("GlowSecondary", root, Color.clear);
            _glowSecondary.sprite = LibraryShapes.Glow();
            _glowSecondary.raycastTarget = false;
            LibraryWidgets.Place(_glowSecondary.rectTransform, new Vector2(0.2f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f), new Vector2(1200f, 1200f));
            JacketPalette palette = JacketPalette.Default;
            _glow = (Color)HomeTheme.Colors.Periwinkle * 0.45f;
            _glow.a = 1f;
            _accent = (Color)palette.Accent;
            ApplyGlow();

            PageHost = LibraryUi.CreateRect("Pages", root);
            LibraryUi.Stretch(PageHost);
            _pagesGroup = PageHost.gameObject.AddComponent<CanvasGroup>();
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            Overlay = LibraryUi.CreateRect("Overlay", root);
            LibraryUi.Stretch(Overlay);
            _toast = new UiToast(root, 48f, HomeTheme.Colors.Periwinkle);

            _curtain = LibraryUi.CreateImage("Curtain", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(_curtain.rectTransform);
            _curtain.raycastTarget = true;
            _curtain.gameObject.SetActive(false);

            if (ServerChartDownloads.Current != null)
            {
                ServerChartDownloads.Current.StatusChanged += OnWebDownloadStatus;
                ServerChartDownloads.Current.Imported += NotifyLibraryChanged;
            }

            _camera.enabled = false;
            _raycaster.enabled = false;
            _canvasRoot.SetActive(false);
        }

        private void OnWebDownloadStatus(string message)
        {
            if (_presented)
            {
                ShowToast(message);
            }
        }

        private void Slide(ServerPage from, ServerPage to, bool forward, Action onDone = null)
        {
            _pushMotion.TryCancel();
            _pagesGroup.blocksRaycasts = false;
            float direction = forward ? 1f : -1f;
            _pushMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD + 0.06f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(Finish)
                .WithOnCancel(Finish)
                .Bind(t =>
                {
                    float fade = Mathf.Clamp01(t * 1.6f);
                    from.Group.alpha = 1f - fade;
                    from.Root.anchoredPosition = new Vector2(-direction * PUSH_OFFSET * 0.5f * t, 0f);
                    to.Group.alpha = t;
                    to.Root.anchoredPosition = new Vector2(direction * PUSH_OFFSET * (1f - t), 0f);
                });

            void Finish()
            {
                _pagesGroup.blocksRaycasts = true;
                from.Root.anchoredPosition = Vector2.zero;
                from.Group.alpha = 1f;
                if (from.Root != null)
                {
                    from.Root.gameObject.SetActive(false);
                }

                to.Group.alpha = 1f;
                to.Root.anchoredPosition = Vector2.zero;
                onDone?.Invoke();
            }
        }

        private void ApplyGlow()
        {
            _glowMain.color = new Color(_glow.r, _glow.g, _glow.b, 0.62f);
            _glowSecondary.color = new Color(_accent.r, _accent.g, _accent.b, 0.08f);
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("ServerScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 102f;
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
                ServerChartDownloads.Current.StatusChanged -= OnWebDownloadStatus;
                ServerChartDownloads.Current.Imported -= NotifyLibraryChanged;
            }

            _curtainMotion.TryCancel();
            _glowMotion.TryCancel();
            _pushMotion.TryCancel();
            _toast?.Dispose();
            for (int i = _pages.Count - 1; i >= 0; i--)
            {
                _pages[i].Dispose();
            }

            _pages.Clear();
            Covers?.Dispose();
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
            }
        }
    }

    /// <summary>One page of the server part: a full-screen rect the screen slides in and out.</summary>
    internal abstract class ServerPage : IDisposable
    {
        private bool _disposed;

        protected ServerPage(ServerScreen screen, string name)
        {
            Screen = screen;
            Root = LibraryUi.CreateRect(name, screen.PageHost);
            LibraryUi.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
        }

        protected ServerScreen Screen { get; }
        internal RectTransform Root { get; }
        internal CanvasGroup Group { get; }

        /// <summary>The page became the top page.</summary>
        internal virtual void OnShown()
        {
        }

        /// <summary>Another page covered it, or it is leaving.</summary>
        internal virtual void OnHidden()
        {
        }

        /// <summary>The page's content cascades in, as the screen rises.</summary>
        internal virtual void PlayEnter()
        {
        }

        /// <summary>Closes something open on the page for the back key; false when nothing was.</summary>
        internal virtual bool TryHandleBack() => false;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OnDispose();
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
            }
        }

        protected virtual void OnDispose()
        {
        }
    }

    /// <summary>A Sonolus server opened from the list: its client and what was read from it.</summary>
    internal sealed class SonolusSession
    {
        internal SonolusSession(ServerEntry entry, SonolusServerAddress address)
        {
            Entry = entry;
            Address = address;
            Client = new SonolusClient(address);
        }

        internal ServerEntry Entry { get; }
        internal SonolusServerAddress Address { get; }
        internal SonolusClient Client { get; }
        internal SonolusServerInfo Info { get; set; }
        internal SonolusLevelsInfo LevelsInfo { get; set; }

        internal string Title => !string.IsNullOrWhiteSpace(Info?.Title) ? Info.Title : Entry.Name;
    }
}
