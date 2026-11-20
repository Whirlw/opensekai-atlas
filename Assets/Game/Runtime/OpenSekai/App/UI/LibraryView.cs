using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The library page: a top bar, the package list on the left (rows or a jacket grid, with
    /// grouping and sorting above it) and the selected package's details on the right. It draws
    /// what <see cref="LibraryPresenter"/> hands it and reports input back; it holds no library
    /// state of its own beyond what is on screen.
    /// </summary>
    internal sealed class LibraryView : ILibraryView, IDisposable
    {
        private const string THUMBNAIL_DIRECTORY = "JacketThumbnails";
        private const int DETAIL_THUMBNAIL_CAPACITY = 4;
        private const float LIST_COLUMN = 0.48f;
        private const float DETAIL_COLUMN = 0.52f;
        private const float CONTROLS_GAP = 14f;
        private const float BROWSE_CONTROL_HEIGHT = LibraryTheme.Layout.CONTROL_HEIGHT;
        private const float GROUP_DROPDOWN_WIDTH = 216f;
        private const float SORT_DROPDOWN_WIDTH = 236f;

        // The list's viewport reaches past its column so a selected row can slide and a selected
        // tile can grow without being clipped; the layout pads the cells back into the column.
        private const float VIEWPORT_BLEED_LEFT = 8f;
        private const float VIEWPORT_BLEED_RIGHT = 24f;
        private const float LIST_PADDING_TOP = 8f;
        private const float LIST_PADDING_BOTTOM = 0f;
        private const int MAX_STAGGERED_CELLS = 16;

        private readonly GameObject _page;
        private readonly Dictionary<RectTransform, LibraryCell> _cells = new Dictionary<RectTransform, LibraryCell>();
        private readonly Dictionary<SongPackageItem, int> _indexByItem = new Dictionary<SongPackageItem, int>();

        private LibraryPresenter _presenter;
        private LibraryViewState _state;
        private IReadOnlyList<SongPackageItem> _items = Array.Empty<SongPackageItem>();
        private SongPackageItem _selected;
        private Camera _uiCamera;

        private JacketThumbnails _thumbnails;
        private JacketThumbnails _detailThumbnails;
        private CancellationTokenSource _detailLoad;
        private CancellationTokenSource _panelArtworkLoad;

        private Image _glowMain;
        private Image _glowSecondary;
        private MotionHandle _glowMotion;
        private Color _glowColor;
        private Color _accentColor;

        private TextMeshProUGUI _countText;
        private RectTransform _browseControls;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _emptyText;
        private LibraryDropdown _groupDropdown;
        private LibraryDropdown _sortDropdown;
        private Button _listModeButton;
        private Button _gridModeButton;
        private Image _listModeIcon;
        private Image _gridModeIcon;
        private LibraryGroupBar _groupBar;
        private RectTransform _listArea;
        private VirtualListView _rowList;
        private VirtualListView _tileList;
        private LibraryDetailPanel _detail;
        private LibraryPreview _preview;
        private bool _startingPlayback;
        private bool _applicationPaused;
        private bool _visible = true;
        private bool _presented;
        private GraphicRaycaster _raycaster;
        private Image _curtain;
        private MotionHandle _curtainMotion;

        private LibrarySettingsPanel _settingsPanel;
        private LibraryResultPanel _resultPanel;
        private LibraryLoadingOverlay _loadingOverlay;
        private LibraryActionsPanel _actionsPanel;

        /// <param name="presented">Whether the page starts on screen; see <see cref="SetPresented"/>.</param>
        internal LibraryView(GameObject page, bool presented = true)
        {
            _page = page;
            _presented = presented;
        }

        public bool HasList => _rowList != null;

        internal bool IsPresented => _presented;

        /// <summary>The top bar's back button.</summary>
        internal Action BackRequested { get; set; }

        private VirtualListView ActiveList =>
            _state != null && _state.ViewMode == LibraryViewMode.Grid ? _tileList : _rowList;

        internal void BuildLayout(LibraryPresenter presenter)
        {
            _presenter = presenter;

            // A dedicated camera keeps the page self-contained (no ScreenManager camera stack),
            // and makes the UI capturable through camera-based screenshots.
            _uiCamera = CreateUiCamera();
            _page.AddComponent<AudioListener>();
            Sekai.SoundManager.NotifySceneAudioListenersChanged();
            Canvas canvas = _page.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            _raycaster = _page.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = _page.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)_page.transform;
            LibraryUi.Stretch(root);

            string cacheDirectory = Path.Combine(Application.temporaryCachePath, THUMBNAIL_DIRECTORY);
            _thumbnails = new JacketThumbnails(cacheDirectory);
            _detailThumbnails = new JacketThumbnails(cacheDirectory, JacketThumbnails.DETAIL_SIZE, DETAIL_THUMBNAIL_CAPACITY);

            BuildBackground(root);

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform main = LibraryUi.CreateRect("Main", root);
            const float COLUMN_BOTTOM = 40f;
            LibraryWidgets.Inset(main, margin, COLUMN_BOTTOM, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT);

            // The overlay hosts dropdown lists; it is moved above everything else once the page is built.
            RectTransform overlay = LibraryUi.CreateRect("Overlay", root);
            LibraryUi.Stretch(overlay);

            BuildTopBar(root, overlay, margin);
            BuildListColumn(main);

            RectTransform detailRoot = LibraryUi.CreateRect("DetailColumn", main);
            detailRoot.anchorMin = new Vector2(DETAIL_COLUMN, 0f);
            detailRoot.anchorMax = new Vector2(1f, 1f);
            detailRoot.offsetMin = Vector2.zero;
            detailRoot.offsetMax = Vector2.zero;
            _detail = new LibraryDetailPanel(detailRoot, LIST_PADDING_TOP, PlaySelected, presenter.OpenActionsForSelected);
            _preview = _page.AddComponent<LibraryPreview>();
            _preview.Initialize(_detail);

            _statusText = LibraryWidgets.Label(
                "Status", root, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(_statusText.rectTransform, margin, margin, 14f, 28f);

            overlay.SetAsLastSibling();

            // The curtain the page rises from and falls back into when it hands over to the home
            // page. It is the ground color, so the two pages meet on the same plain screen.
            _curtain = LibraryUi.CreateImage("Curtain", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(_curtain.rectTransform);
            _curtain.raycastTarget = true;
            _curtain.gameObject.SetActive(false);
            ApplyVisibility();
        }

        internal void BuildPanels(LibraryPresenter presenter)
        {
            var root = _page.transform;

            try
            {
                _settingsPanel = LibrarySettingsPanel.Create(root, presenter.OnSettingsChanged);
            }
            catch (Exception exception)
            {
                _settingsPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the settings panel: " + exception);
            }

            try
            {
                _resultPanel = LibraryResultPanel.Create(root, presenter.OnResultRetry);
                _resultPanel.OnClose = ResumePreview;
            }
            catch (Exception exception)
            {
                _resultPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the result panel: " + exception);
            }

            try
            {
                _loadingOverlay = LibraryLoadingOverlay.Create(root, presenter.OnCancelPlayback);
            }
            catch (Exception exception)
            {
                _loadingOverlay = null;
                Debug.LogWarning("[LibraryScreen] Could not build the loading overlay: " + exception);
            }

            try
            {
                _actionsPanel = LibraryActionsPanel.Create(root);
            }
            catch (Exception exception)
            {
                _actionsPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the actions panel: " + exception);
            }
        }

        public void Render(LibraryViewState state)
        {
            bool modeChanged = _state == null || _state.ViewMode != state.ViewMode;
            _state = state;
            _items = state.Listing.Items;
            _selected = state.Selected;
            _indexByItem.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                _indexByItem[_items[i]] = i;
            }

            LibraryListing listing = state.Listing;
            _countText.text = listing.TotalCount + " 首";
            RectTransform count = _countText.rectTransform;
            float countWidth = Mathf.Min(count.rect.width, Mathf.Ceil(_countText.GetPreferredValues(_countText.text).x));
            _browseControls.offsetMin = new Vector2(count.anchoredPosition.x + countWidth + 16f, -BROWSE_CONTROL_HEIGHT * 0.5f);

            RenderOptions(state);
            RenderGroups(listing);

            _rowList.gameObject.SetActive(state.ViewMode == LibraryViewMode.List);
            _tileList.gameObject.SetActive(state.ViewMode == LibraryViewMode.Grid);
            if (modeChanged)
            {
                // The list that just went off screen gives its cells and jackets back.
                (state.ViewMode == LibraryViewMode.Grid ? _rowList : _tileList).SetItemCount(0);
            }

            VirtualListView list = ActiveList;
            list.SetItemCount(_items.Count);
            if (_selected != null && _indexByItem.TryGetValue(_selected, out int selectedIndex))
            {
                list.ScrollTo(selectedIndex);
            }

            PlayEnter(list);

            _emptyText.gameObject.SetActive(_items.Count == 0);
            _emptyText.text = listing.TotalCount == 0 ? "还没有曲包" : "没有符合条件的曲包";
            ShowDetail();
        }

        public void RefreshScore(string scoreKey)
        {
            if (_state == null || string.IsNullOrEmpty(scoreKey))
            {
                return;
            }

            VirtualListView list = ActiveList;
            for (int i = 0; i < _items.Count; i++)
            {
                SongPackageItem item = _items[i];
                if (!string.Equals(item.ScoreKey, scoreKey, StringComparison.Ordinal))
                {
                    continue;
                }

                RectTransform root = list.CellAt(i);
                if (root != null && _cells.TryGetValue(root, out LibraryCell cell))
                {
                    cell.UpdateScore(LibraryModel.SummaryOf(item, _state.History));
                }
            }

            if (_selected != null && string.Equals(_selected.ScoreKey, scoreKey, StringComparison.Ordinal))
            {
                _detail.ShowScore(_selected, LibraryModel.SummaryOf(_selected, _state.History));
            }
        }

        public void SetSelection(SongPackageItem selected)
        {
            SongPackageItem previous = _selected;
            _selected = selected;
            CellOf(previous)?.SetSelected(false, true);
            CellOf(selected)?.SetSelected(true, true);
            ShowDetail();
        }

        public void ShowSettings()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            _settingsPanel.SetAccent(Accent);
            _settingsPanel.Show();
        }

        public void ShowResult(PlayResult result)
        {
            _preview?.StopPreview();
            if (_resultPanel == null || result == null)
            {
                return;
            }

            _resultPanel.Show(result, null, Accent);
            LoadPanelArtwork(result.PackageDirectory, _resultPanel.SetArtwork);
        }

        public void ShowLoading(string title, string packageDirectory)
        {
            _startingPlayback = true;
            _preview?.StopPreview();
            if (_loadingOverlay == null)
            {
                return;
            }

            _loadingOverlay.Show(title, "正在准备…", null, Accent);
            LoadPanelArtwork(packageDirectory, _loadingOverlay.SetArtwork);
        }

        /// <summary>The page's accent, which follows the selected jacket.</summary>
        private Color32 Accent => (Color32)_accentColor;

        /// <summary>
        /// Gives a modal panel the jacket of the package it reports on, and the accent taken from
        /// it. The package is looked up by folder, not taken from the selection: the result card
        /// opens as the page is rebuilt on the way back from a live, when the selection may not be
        /// restored yet and the detail side's jacket has not been decoded. A jacket already in a
        /// cache arrives at once; otherwise the list's small one stands in until the sharp one loads.
        /// </summary>
        private void LoadPanelArtwork(string packageDirectory, Action<Sprite, Color32> apply)
        {
            CancelPanelArtworkLoad();
            SongPackageItem item = FindItem(packageDirectory);
            if (item?.Entry == null || !item.HasJacket || _detailThumbnails == null)
            {
                return;
            }

            string jacketPath = item.Entry.JacketPath;
            long version = item.LastWriteTime.Ticks;
            if (_detailThumbnails.TryGetLoaded(jacketPath, version, out JacketThumbnail sharp))
            {
                apply(sharp.Sprite, sharp.Palette.Accent);
                return;
            }

            if (_thumbnails.TryGetLoaded(jacketPath, version, out JacketThumbnail small))
            {
                apply(small.Sprite, small.Palette.Accent);
            }

            _panelArtworkLoad = new CancellationTokenSource();
            LoadPanelArtworkAsync(jacketPath, version, apply, _panelArtworkLoad.Token).Forget();
        }

        private async UniTaskVoid LoadPanelArtworkAsync(
            string jacketPath, long version, Action<Sprite, Color32> apply, CancellationToken cancellationToken)
        {
            try
            {
                JacketThumbnail thumbnail = await _detailThumbnails.LoadAsync(jacketPath, version, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && thumbnail.Sprite != null)
                {
                    apply(thumbnail.Sprite, thumbnail.Palette.Accent);
                }
            }
            catch (OperationCanceledException)
            {
                // The panel closed, or another one opened, before the jacket arrived.
            }
        }

        private void CancelPanelArtworkLoad()
        {
            if (_panelArtworkLoad == null)
            {
                return;
            }

            _panelArtworkLoad.Cancel();
            _panelArtworkLoad.Dispose();
            _panelArtworkLoad = null;
        }

        /// <summary>The package in this folder: from the list on screen, else from the whole library.</summary>
        private SongPackageItem FindItem(string packageDirectory)
        {
            if (string.IsNullOrEmpty(packageDirectory))
            {
                return null;
            }

            string wanted = NormalizeDirectory(packageDirectory);
            if (_selected?.Entry != null && NormalizeDirectory(_selected.Entry.RootDirectory) == wanted)
            {
                return _selected;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                SongPackageItem item = _items[i];
                if (item?.Entry != null && NormalizeDirectory(item.Entry.RootDirectory) == wanted)
                {
                    return item;
                }
            }

            return SongPackageManager.Shared.FindCachedItem(packageDirectory);
        }

        private static string NormalizeDirectory(string directory)
        {
            try
            {
                directory = Path.GetFullPath(directory);
            }
            catch (Exception)
            {
                // Not a usable path; compare it as written.
            }

            return directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }

        private void ApplyDetailThumbnail(JacketThumbnail thumbnail)
        {
            _detail.SetThumbnail(thumbnail);
            SetPalette(thumbnail.Palette);
        }

        public void HideLoading()
        {
            _startingPlayback = false;
            _loadingOverlay?.Hide();
            ResumePreview();
        }

        internal void ResumePreview()
        {
            if (_visible && _presented && !_startingPlayback && !_applicationPaused && _preview != null
                && (_resultPanel == null || !_resultPanel.isActiveAndEnabled))
            {
                _preview.Select(_selected);
            }
        }

        internal void SetApplicationPaused(bool paused)
        {
            _applicationPaused = paused;
            if (paused)
            {
                _preview?.StopPreview();
            }
            else
            {
                ResumePreview();
            }
        }

        internal void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        /// <summary>
        /// Takes the page off screen while another page shows in its place: its camera stops
        /// drawing, taps stop reaching it and the song preview stops. Coming back with <paramref name="animate"/>,
        /// the page rises out of the ground-colored curtain while its list cascades in.
        /// </summary>
        internal void SetPresented(bool presented, bool animate)
        {
            _presented = presented;
            _curtainMotion.TryCancel();
            _curtainMotion = default;
            if (_curtain != null)
            {
                _curtain.gameObject.SetActive(false);
            }

            ApplyVisibility();
            if (!presented || !animate || _curtain == null)
            {
                return;
            }

            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(1f, 0f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithOnComplete(() => _curtain.gameObject.SetActive(false))
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
            if (_rowList != null)
            {
                PlayEnter(ActiveList);
            }
        }

        /// <summary>Lowers the curtain over the page, then runs <paramref name="onCovered"/>.</summary>
        internal void PlayExit(Action onCovered)
        {
            if (_curtain == null || !_presented)
            {
                onCovered?.Invoke();
                return;
            }

            _preview?.StopPreview();
            _curtainMotion.TryCancel();
            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                .WithEase(LibraryTheme.Motion.EXIT_EASE)
                .WithOnComplete(() =>
                {
                    _curtainMotion = default;
                    onCovered?.Invoke();
                })
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
        }

        /// <summary>
        /// Closes whatever sits on top of the page for the Android back key. True when something
        /// took the key; while playback is starting the key is swallowed.
        /// </summary>
        internal bool TryHandleBack()
        {
            if (_startingPlayback || (_loadingOverlay != null && _loadingOverlay.isActiveAndEnabled))
            {
                return true;
            }

            if (_groupDropdown != null && _groupDropdown.IsOpen)
            {
                _groupDropdown.Close();
                UiSound.Play(UiCue.Collapse);
                return true;
            }

            if (_sortDropdown != null && _sortDropdown.IsOpen)
            {
                _sortDropdown.Close();
                UiSound.Play(UiCue.Collapse);
                return true;
            }

            if (_actionsPanel != null && _actionsPanel.IsOpen)
            {
                _actionsPanel.Hide();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_settingsPanel != null && _settingsPanel.IsOpen)
            {
                _settingsPanel.Back();
                return true;
            }

            if (_resultPanel != null && _resultPanel.IsOpen)
            {
                _resultPanel.Dismiss();
                return true;
            }

            return false;
        }

        private void ApplyVisibility()
        {
            bool shown = _visible && _presented;
            if (_uiCamera != null)
            {
                _uiCamera.enabled = shown;
            }

            // The canvas itself stays enabled so its layout keeps the screen's size while hidden;
            // without its camera nothing draws it, and without its raycaster no tap reaches it.
            if (_raycaster != null)
            {
                _raycaster.enabled = shown;
            }

            if (shown)
            {
                ResumePreview();
            }
            else
            {
                _preview?.StopPreview();
            }
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }
        public void SetLoadingMessage(string message) => _loadingOverlay?.SetMessage(message);

        public void ShowActions(SongPackageItem item, Action export, Action delete)
        {
            if (_actionsPanel == null || item == null)
            {
                return;
            }

            Sprite cover = item == _selected ? _detail?.Cover : null;
            _actionsPanel.Show(LibraryModel.BuildTitle(item), DescribePackage(item), cover, Accent, export, delete);
        }

        /// <summary>Difficulty, level and charter, the line under a package's title in its action card.</summary>
        private static string DescribePackage(SongPackageItem item)
        {
            string text = LibraryModel.DifficultyLabel(item);
            int level = LibraryModel.PlayLevelOf(item);
            if (level > 0)
            {
                text += "  ·  LV " + level;
            }

            string charter = item.Entry?.Manifest?.userName;
            if (!string.IsNullOrWhiteSpace(charter))
            {
                text += "  ·  谱面 " + charter.Trim();
            }
            return text;
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }
        }

        public void Dispose()
        {
            _preview?.StopPreview();
            CancelDetailLoad();
            CancelPanelArtworkLoad();
            _curtainMotion.TryCancel();
            _glowMotion.TryCancel();
            _glowMotion = default;
            foreach (LibraryCell cell in _cells.Values)
            {
                cell.Release();
            }

            _detail?.Dispose();
            _thumbnails?.Dispose();
            _thumbnails = null;
            _detailThumbnails?.Dispose();
            _detailThumbnails = null;
            if (_uiCamera != null)
            {
                UnityEngine.Object.Destroy(_uiCamera.gameObject);
            }
        }

        private void BuildBackground(RectTransform root)
        {
            Image background = LibraryUi.CreateImage("Background", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(background.rectTransform);

            _glowMain = LibraryUi.CreateImage("GlowMain", root, Color.clear);
            _glowMain.sprite = LibraryShapes.Glow();
            _glowMain.raycastTarget = false;
            LibraryWidgets.Place(
                _glowMain.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -340f), new Vector2(1900f, 1900f));

            _glowSecondary = LibraryUi.CreateImage("GlowSecondary", root, Color.clear);
            _glowSecondary.sprite = LibraryShapes.Glow();
            _glowSecondary.raycastTarget = false;
            LibraryWidgets.Place(
                _glowSecondary.rectTransform, new Vector2(0.62f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -80f), new Vector2(1100f, 1100f));

            JacketPalette palette = JacketPalette.Default;
            _glowColor = (Color)palette.Glow;
            _accentColor = (Color)palette.Accent;
            ApplyGlow();
        }

        private void BuildTopBar(RectTransform root, RectTransform overlay, float margin)
        {
            RectTransform bar = LibraryUi.CreateRect("TopBar", root);
            LibraryWidgets.TopStrip(bar, margin, margin, 0f, LibraryTheme.Layout.TOP_BAR_HEIGHT);
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            Vector2 leftCenter = new Vector2(0f, 0.5f);

            Button back = CreateBarIconButton(bar, "Back", LibraryIcon.Back, () => BackRequested?.Invoke(),
                LibraryTheme.Colors.TextSecondary, UiCue.Back);
            LibraryWidgets.Place((RectTransform)back.transform, leftCenter, leftCenter, Vector2.zero, new Vector2(height, height));

            TextMeshProUGUI title = LibraryWidgets.Label(
                "Title", bar, "曲库", LibraryTheme.FontSize.PAGE_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(
                title.rectTransform, leftCenter, leftCenter, new Vector2(height + 16f, 0f), new Vector2(84f, height));
            _countText = LibraryWidgets.Label(
                "Count", bar, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(
                _countText.rectTransform, leftCenter, leftCenter, new Vector2(height + 112f, -5f), new Vector2(124f, height));
            BuildBrowseControls(bar, overlay);

            // Right-aligned actions, laid out from the edge inward.
            float right = AddSettingsButton(bar, _presenter.OpenSettings);
            right = AddBarButton(bar, "Refresh", LibraryIcon.Refresh, _presenter.Resync, right);

            const float IMPORT_WIDTH = 140f;
            bool canPickFolder = LibraryImport.SupportsFolderPick;
            Button import = LibraryWidgets.PillButton("Import", bar, "导入",
                canPickFolder ? null : _presenter.ImportZip, IMPORT_WIDTH, height,
                new Color32(255, 255, 255, 26), LibraryTheme.Colors.TextPrimary,
                sound: canPickFolder ? UiCue.None : UiCue.Press);
            LibraryWidgets.Place((RectTransform)import.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-right, 0f), new Vector2(IMPORT_WIDTH, height));
            LibraryWidgets.Inset(import.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 32f, 0f, 0f, 0f);
            Image add = LibraryWidgets.Icon(import.transform, LibraryIcon.Add, LibraryTheme.Colors.TextPrimary, 26f);
            add.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            if (canPickFolder)
            {
                LibraryDropdown.AttachActions(import, overlay, new[] { "导入 ZIP / OSKP", "导入文件夹" }, index =>
                {
                    if (index == 0)
                    {
                        _presenter.ImportZip();
                    }
                    else
                    {
                        _presenter.ImportFolder();
                    }
                });
            }

            right += IMPORT_WIDTH + 16f;
            TMP_InputField search = CreateSearchField(bar, height);
            RectTransform searchRect = (RectTransform)search.transform;
            searchRect.anchorMin = new Vector2(DETAIL_COLUMN, 0.5f);
            searchRect.anchorMax = new Vector2(1f, 0.5f);
            searchRect.offsetMin = new Vector2(0f, -height * 0.5f);
            searchRect.offsetMax = new Vector2(-right, height * 0.5f);
            search.onValueChanged.AddListener(_presenter.SetSearch);
        }

        internal static float AddSettingsButton(RectTransform bar, UnityEngine.Events.UnityAction openSettings) =>
            AddBarButton(bar, "Settings", LibraryIcon.Settings, openSettings, 0f, UiCue.None);

        private static float AddBarButton(
            RectTransform bar, string name, LibraryIcon icon, UnityEngine.Events.UnityAction onClick, float right,
            UiCue sound = UiCue.Press)
        {
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button button = CreateBarIconButton(bar, name, icon, onClick, LibraryTheme.Colors.TextSecondary, sound);
            LibraryWidgets.Place(
                (RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-right, 0f), new Vector2(height, height));
            return right + height + 16f;
        }

        private static Button CreateBarIconButton(
            Transform parent, string name, LibraryIcon icon, UnityEngine.Events.UnityAction onClick, Color32 color,
            UiCue sound = UiCue.Press)
        {
            float size = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button button = LibraryWidgets.PillButton(
                name, parent, string.Empty, onClick, size, size, LibraryTheme.Colors.Control, color, sound: sound);
            LibraryWidgets.Icon(button.transform, icon, color, 28f);
            return button;
        }

        private static TMP_InputField CreateSearchField(Transform parent, float height) =>
            LibraryWidgets.TextField("Search", parent, height, "搜索曲名、作者", LibraryIcon.Search);

        private void BuildBrowseControls(RectTransform bar, RectTransform overlay)
        {
            const float HEIGHT = BROWSE_CONTROL_HEIGHT;
            const float MODE_WIDTH = 72f;
            const float TOGGLE_WIDTH = MODE_WIDTH * 2f + 12f;
            _browseControls = LibraryUi.CreateRect("Controls", bar);
            _browseControls.anchorMin = new Vector2(0f, 0.5f);
            _browseControls.anchorMax = new Vector2(LIST_COLUMN, 0.5f);
            _browseControls.offsetMax = new Vector2(0f, HEIGHT * 0.5f);

            var layout = _browseControls.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _groupDropdown = LibraryDropdown.Create(_browseControls, overlay, "分组", GROUP_DROPDOWN_WIDTH, HEIGHT);
            SetBrowseControlWidth(_groupDropdown.gameObject, 140f, GROUP_DROPDOWN_WIDTH);
            SetBrowseControlWidth(LibraryUi.CreateRect("DropdownGap", _browseControls).gameObject, 10f, 10f);
            _sortDropdown = LibraryDropdown.Create(_browseControls, overlay, "排序", SORT_DROPDOWN_WIDTH, HEIGHT);
            SetBrowseControlWidth(_sortDropdown.gameObject, 152f, SORT_DROPDOWN_WIDTH);
            LayoutElement space = LibraryUi.CreateRect("Space", _browseControls).gameObject.AddComponent<LayoutElement>();
            space.minWidth = 6f;
            space.flexibleWidth = 1f;

            Image toggle = LibraryWidgets.Panel("ViewMode", _browseControls, HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            toggle.rectTransform.sizeDelta = new Vector2(TOGGLE_WIDTH, HEIGHT);
            SetBrowseControlWidth(toggle.gameObject, TOGGLE_WIDTH, TOGGLE_WIDTH);
            LibraryWidgets.Outline(toggle, HEIGHT * 0.5f);
            _listModeButton = LibraryWidgets.PillButton(
                "List", toggle.transform, string.Empty, () => _presenter.SetViewMode(LibraryViewMode.List), MODE_WIDTH, HEIGHT - 8f,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, LibraryTheme.FontSize.CAPTION, FontStyles.Bold,
                UiCue.None);
            _listModeIcon = LibraryWidgets.Icon(_listModeButton.transform, LibraryIcon.List, LibraryTheme.Colors.TextOnAccent, 28f);
            LibraryWidgets.Place(
                (RectTransform)_listModeButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 0f), new Vector2(MODE_WIDTH, HEIGHT - 8f));
            _gridModeButton = LibraryWidgets.PillButton(
                "Grid", toggle.transform, string.Empty, () => _presenter.SetViewMode(LibraryViewMode.Grid), MODE_WIDTH, HEIGHT - 8f,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, LibraryTheme.FontSize.CAPTION, FontStyles.Bold,
                UiCue.None);
            _gridModeIcon = LibraryWidgets.Icon(_gridModeButton.transform, LibraryIcon.Grid, LibraryTheme.Colors.TextSecondary, 28f);
            LibraryWidgets.Place(
                (RectTransform)_gridModeButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(MODE_WIDTH, HEIGHT - 8f));
        }

        private static void SetBrowseControlWidth(GameObject control, float minimum, float preferred)
        {
            LayoutElement size = control.AddComponent<LayoutElement>();
            size.minWidth = minimum;
            size.preferredWidth = preferred;
            size.flexibleWidth = 0f;
        }

        private void BuildListColumn(RectTransform main)
        {
            RectTransform column = LibraryUi.CreateRect("ListColumn", main);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(LIST_COLUMN, 1f);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;

            _groupBar = LibraryGroupBar.Create(column);
            LibraryWidgets.TopStrip((RectTransform)_groupBar.transform, 0f, 0f, 0f, LibraryGroupBar.HEIGHT);

            _listArea = LibraryUi.CreateRect("ListArea", column);
            LibraryUi.Stretch(_listArea);

            _rowList = CreateList(
                "Rows",
                width => VirtualListLayout.Rows(
                    width, LibraryTheme.Layout.ROW_HEIGHT, LibraryTheme.Layout.ROW_SPACING,
                    LIST_PADDING_TOP, LIST_PADDING_BOTTOM, VIEWPORT_BLEED_LEFT, VIEWPORT_BLEED_RIGHT),
                parent => Register(new LibraryRowCell(parent, OnCellClicked)));
            _tileList = CreateList(
                "Tiles",
                width => VirtualListLayout.Grid(
                    width, LibraryTheme.Layout.GRID_COLUMNS, LibraryTheme.Layout.GRID_SPACING_X,
                    LibraryTheme.Layout.GRID_SPACING_Y, LibraryTheme.Layout.GRID_TITLE_HEIGHT,
                    LIST_PADDING_TOP, LIST_PADDING_BOTTOM, VIEWPORT_BLEED_LEFT, VIEWPORT_BLEED_RIGHT),
                parent => Register(new LibraryTileCell(parent, OnCellClicked)));
            _tileList.gameObject.SetActive(false);

            _emptyText = LibraryWidgets.Label(
                "Empty", _listArea, string.Empty, LibraryTheme.FontSize.ROW_TITLE, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.Stretch(_emptyText.rectTransform);
            _emptyText.gameObject.SetActive(false);
        }

        private VirtualListView CreateList(
            string name, Func<float, VirtualListLayout> layoutForWidth, Func<RectTransform, RectTransform> createCell)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(_listArea, false);
            LibraryWidgets.Inset((RectTransform)go.transform, -VIEWPORT_BLEED_LEFT, 0f, -VIEWPORT_BLEED_RIGHT, 0f);

            // An invisible graphic lets a drag that starts between cells still scroll the list.
            Image dragSurface = go.AddComponent<Image>();
            dragSurface.color = new Color32(0, 0, 0, 0);

            RectTransform viewport = LibraryUi.CreateRect("Viewport", go.transform);
            viewport.gameObject.AddComponent<RectMask2D>();
            LibraryUi.Stretch(viewport);
            RectTransform content = LibraryUi.CreateRect("Content", viewport);

            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            return VirtualListView.Attach(scroll, layoutForWidth, createCell, BindCell, ReleaseCell);
        }

        private RectTransform Register(LibraryCell cell)
        {
            _cells[cell.Root] = cell;
            return cell.Root;
        }

        private void BindCell(RectTransform root, int index)
        {
            SongPackageItem item = _items[index];
            _cells[root].Bind(
                item, LibraryModel.SummaryOf(item, _state?.History), ReferenceEquals(item, _selected), _thumbnails);
        }

        private void ReleaseCell(RectTransform root)
        {
            _cells[root].Release();
        }

        private void OnCellClicked(LibraryCell cell)
        {
            if (cell.Item != null)
            {
                _presenter.Select(cell.Item);
            }
        }

        private LibraryCell CellOf(SongPackageItem item)
        {
            if (item == null || !_indexByItem.TryGetValue(item, out int index))
            {
                return null;
            }

            RectTransform root = ActiveList.CellAt(index);
            return root != null ? _cells[root] : null;
        }

        private void PlaySelected(bool isAuto)
        {
            if (_selected != null)
            {
                _presenter.StartPlayback(_selected, isAuto);
            }
        }

        private void RenderOptions(LibraryViewState state)
        {
            var groupLabels = new string[LibraryModel.GroupKeys.Count];
            int selected = 0;
            for (int i = 0; i < groupLabels.Length; i++)
            {
                groupLabels[i] = LibraryModel.Label(LibraryModel.GroupKeys[i]);
                if (LibraryModel.GroupKeys[i] == state.GroupKey)
                {
                    selected = i;
                }
            }

            _groupDropdown.SetOptions(groupLabels, selected, index => _presenter.SetGroupMode(LibraryModel.GroupKeys[index]));

            var sortLabels = new string[LibraryModel.SortKeys.Count];
            selected = 0;
            for (int i = 0; i < sortLabels.Length; i++)
            {
                sortLabels[i] = LibraryModel.Label(LibraryModel.SortKeys[i]);
                if (LibraryModel.SortKeys[i] == state.SortKey)
                {
                    selected = i;
                }
            }

            _sortDropdown.SetOptions(sortLabels, selected, index => _presenter.SetSort(LibraryModel.SortKeys[index]));

            ShowModeButton(_listModeButton, _listModeIcon, state.ViewMode == LibraryViewMode.List);
            ShowModeButton(_gridModeButton, _gridModeIcon, state.ViewMode == LibraryViewMode.Grid);
        }

        private static void ShowModeButton(Button button, Image icon, bool active)
        {
            button.targetGraphic.color = active ? LibraryTheme.Colors.TextPrimary : new Color32(255, 255, 255, 0);
            icon.color = active ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
        }

        private void RenderGroups(LibraryListing listing)
        {
            bool grouped = listing.Groups.Count > 0;
            _groupBar.gameObject.SetActive(grouped);
            float top = 0f;
            if (grouped)
            {
                _groupBar.Set(listing.MatchCount, listing.Groups, listing.SelectedGroup, _presenter.SelectGroup);
                top += LibraryGroupBar.HEIGHT + CONTROLS_GAP;
            }

            // Without a group bar the list takes the row it would have used.
            _listArea.offsetMax = new Vector2(0f, -top);
        }

        /// <summary>Fades the visible cells in one after another, top to bottom.</summary>
        private void PlayEnter(VirtualListView list)
        {
            int order = 0;
            for (int index = list.FirstActiveIndex; index < list.EndActiveIndex; index++)
            {
                RectTransform root = list.CellAt(index);
                if (root == null)
                {
                    continue;
                }

                _cells[root].PlayEnter(Mathf.Min(order, MAX_STAGGERED_CELLS) * LibraryTheme.Motion.STAGGER);
                order++;
            }
        }

        private void ShowDetail()
        {
            CancelDetailLoad();
            SongPackageItem item = _selected;
            _detail.Show(item, LibraryModel.SummaryOf(item, _state?.History), true);
            ResumePreview();
            if (item == null || !item.HasJacket || item.Entry == null)
            {
                ApplyDetailThumbnail(JacketThumbnail.None);
                return;
            }

            string jacketPath = item.Entry.JacketPath;
            long version = item.LastWriteTime.Ticks;
            if (_detailThumbnails.TryGetLoaded(jacketPath, version, out JacketThumbnail sharp))
            {
                ApplyDetailThumbnail(sharp);
                return;
            }

            // The list's small thumbnail fills in at once; the sharper one replaces it when ready.
            if (_thumbnails.TryGetLoaded(jacketPath, version, out JacketThumbnail small))
            {
                ApplyDetailThumbnail(small);
            }

            _detailLoad = new CancellationTokenSource();
            LoadDetailAsync(jacketPath, version, _detailLoad.Token).Forget();
        }

        private async UniTaskVoid LoadDetailAsync(string jacketPath, long version, CancellationToken cancellationToken)
        {
            try
            {
                JacketThumbnail thumbnail = await _detailThumbnails.LoadAsync(jacketPath, version, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    ApplyDetailThumbnail(thumbnail);
                }
            }
            catch (OperationCanceledException)
            {
                // Another package was selected, or the page closed, before the jacket arrived.
            }
        }

        private void CancelDetailLoad()
        {
            if (_detailLoad == null)
            {
                return;
            }

            _detailLoad.Cancel();
            _detailLoad.Dispose();
            _detailLoad = null;
        }

        /// <summary>Moves the background glow and the dropdown accents to a jacket's colors.</summary>
        private void SetPalette(JacketPalette palette)
        {
            _groupDropdown.SetAccent(palette.Accent);
            _sortDropdown.SetAccent(palette.Accent);

            Color fromGlow = _glowColor;
            Color fromAccent = _accentColor;
            _glowMotion.TryCancel();
            _glowMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithImmediateBind(false)
                .Bind(value =>
                {
                    _glowColor = Color.Lerp(fromGlow, palette.Glow, value);
                    _accentColor = Color.Lerp(fromAccent, palette.Accent, value);
                    ApplyGlow();
                });
        }

        private void ApplyGlow()
        {
            _glowMain.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0.62f);
            _glowSecondary.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.1f);
        }

        private static Camera CreateUiCamera()
        {
            GameObject cameraObject = new GameObject("LibraryScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }
    }
}
