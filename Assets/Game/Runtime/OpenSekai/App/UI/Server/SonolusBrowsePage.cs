using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A Sonolus server's levels. 首页 shows the server's own sections (staff picks, newest,
    /// random, popular...) as rows of covers; 全部谱面 and a search show a grid that loads the next
    /// page as it is scrolled. The search field runs the server's quick search; 筛选 opens the
    /// server's advanced search form, built from what the server says it accepts.
    /// </summary>
    internal sealed class SonolusBrowsePage : ServerPage
    {
        private const int GRID_COLUMNS = 6;
        private const float GRID_SPACING_X = 26f;
        private const float GRID_SPACING_Y = 30f;
        private const float SECTION_TILE = 236f;
        private const float SECTION_GAP = 26f;
        private const float SECTION_HEADER = 66f;
        private const float MODE_HEIGHT = 48f;
        private const float SEARCH_WIDTH = 520f;

        private readonly SonolusSession _session;
        private readonly Func<IEnumerable<KeyValuePair<string, string>>, int, string, CancellationToken, UniTask<SonolusPage<SonolusLevel>>> _listLevels;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _subtitle;
        private readonly TMP_InputField _search;
        private readonly Button _filter;
        private readonly Image _filterDot;
        private readonly RectTransform _modes;
        private readonly ModeChip _homeChip;
        private readonly ModeChip _allChip;
        private readonly ModeChip _resultChip;
        private readonly TextMeshProUGUI _pageInfo;
        private readonly RectTransform _homeArea;
        private readonly RectTransform _homeContent;
        private readonly ScrollRect _homeScroll;
        private readonly RectTransform _gridArea;
        private readonly ScrollRect _gridScroll;
        private readonly VirtualListView _grid;
        private readonly ServerStatusView _status;
        private readonly SonolusSearchPanel _searchPanel;
        private readonly List<SonolusLevelTile> _homeTiles = new List<SonolusLevelTile>();
        private readonly Dictionary<RectTransform, SonolusLevelTile> _gridTiles = new Dictionary<RectTransform, SonolusLevelTile>();
        private readonly List<SonolusLevel> _results = new List<SonolusLevel>();

        private CancellationTokenSource _infoLoad;
        private CancellationTokenSource _listLoad;
        private Mode _mode = Mode.Home;
        private List<KeyValuePair<string, string>> _query;
        private string _resultLabel;
        private int _nextPage;
        private string _cursor;
        private int _pageCount;
        private bool _hasMore;
        private bool _loadingMore;
        private bool _homeBuilt;
        private bool _advancedActive;

        private enum Mode
        {
            Home,
            All,
            Results
        }

        internal SonolusBrowsePage(ServerScreen screen, SonolusSession session,
            Func<IEnumerable<KeyValuePair<string, string>>, int, string, CancellationToken, UniTask<SonolusPage<SonolusLevel>>> listLevels = null)
            : base(screen, "SonolusBrowse")
        {
            _session = session;
            _listLevels = listLevels ?? session.Client.ListLevelsAsync;
            RectTransform bar = ServerWidgets.TopBar(Root, session.Title, screen.Back, out _title, out _subtitle);
            _subtitle.text = session.Address.Host + session.Address.Prefix;
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            Vector2 right = new Vector2(1f, 0.5f);

            Button refresh = ServerWidgets.IconButton(bar, "Refresh", LibraryIcon.Refresh, Reload);
            LibraryWidgets.Place((RectTransform)refresh.transform, right, right, Vector2.zero, new Vector2(height, height));

            const float filterWidth = 150f;
            _filter = ServerWidgets.LabelledButton(bar, "Filter", "筛选", LibraryIcon.Settings, OpenAdvanced, filterWidth, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Outline((Image)_filter.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)_filter.transform, right, right, new Vector2(-(height + 16f), 0f), new Vector2(filterWidth, height));
            _filterDot = LibraryUi.CreateImage("Dot", _filter.transform, LibraryTheme.Colors.DefaultAccent);
            _filterDot.sprite = LibraryShapes.RoundedRect(6f);
            _filterDot.type = Image.Type.Sliced;
            _filterDot.raycastTarget = false;
            LibraryWidgets.Place(_filterDot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -16f), new Vector2(12f, 12f));
            _filterDot.enabled = false;

            _search = LibraryWidgets.TextField("Search", bar, height, "搜索谱面", LibraryIcon.Search);
            _search.characterLimit = 100;
            LibraryWidgets.Place((RectTransform)_search.transform, right, right,
                new Vector2(-(height + 16f + filterWidth + 16f), 0f), new Vector2(SEARCH_WIDTH, height));
            _search.onSubmit.AddListener(RunQuickSearch);
            _search.onEndEdit.AddListener(text =>
            {
                // A phone keyboard's done key ends the edit without a submit event.
                if (TouchScreenKeyboard.isSupported && _search.wasCanceled == false)
                {
                    RunQuickSearch(text);
                }
            });

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            float top = LibraryTheme.Layout.TOP_BAR_HEIGHT;
            _modes = LibraryUi.CreateRect("Modes", Root);
            LibraryWidgets.TopStrip(_modes, margin, margin, top, MODE_HEIGHT);
            _homeChip = new ModeChip(_modes, "首页", () => SetMode(Mode.Home));
            _allChip = new ModeChip(_modes, "全部谱面", () => ShowAll());
            _resultChip = new ModeChip(_modes, "搜索结果", () => SetMode(Mode.Results));
            _pageInfo = LibraryWidgets.Label("PageInfo", _modes, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(_pageInfo.rectTransform);

            RectTransform body = LibraryUi.CreateRect("Body", Root);
            LibraryWidgets.Inset(body, margin - 20f, 0f, margin - 20f, top + MODE_HEIGHT + 14f);

            _homeArea = LibraryUi.CreateRect("Home", body);
            LibraryUi.Stretch(_homeArea);
            _homeContent = ServerWidgets.VerticalList(_homeArea, out _homeScroll, 18f, new RectOffset(20, 20, 16, 48));

            _gridArea = LibraryUi.CreateRect("Grid", body);
            LibraryUi.Stretch(_gridArea);
            _gridScroll = CreateGridScroll(_gridArea);
            _grid = VirtualListView.Attach(
                _gridScroll,
                width => VirtualListLayout.Grid(width, GRID_COLUMNS, GRID_SPACING_X, GRID_SPACING_Y, SonolusLevelTile.TEXT_HEIGHT, 16f, 72f, 20f, 20f),
                parent =>
                {
                    var tile = new SonolusLevelTile(parent, OnTileClicked);
                    _gridTiles[tile.Root] = tile;
                    return tile.Root;
                },
                BindGridTile,
                root => _gridTiles[root].Release());
            _gridScroll.onValueChanged.AddListener(_ => MaybeLoadMore());

            _status = new ServerStatusView(body);
            _searchPanel = new SonolusSearchPanel(screen, RunAdvancedSearch);
            ApplyMode();
        }

        // ---------------------------------------------------------------- tools (screenshots)

        /// <summary>The server's sections have arrived and are on screen.</summary>
        internal bool IsHomeReady => _homeBuilt && _infoLoad == null;

        internal SonolusSession Session => _session;

        /// <summary>The first level of the first section, for tools that open a level page.</summary>
        internal SonolusLevel FirstLevel =>
            _session.LevelsInfo != null && _session.LevelsInfo.Sections.Count > 0 ? _session.LevelsInfo.Sections[0].Levels[0] : null;

        internal bool IsListReady => _mode != Mode.Home && !_loadingMore && _results.Count > 0;

        internal void OpenAdvancedSearch() => OpenAdvanced();

        internal void CloseAdvancedSearch() => _searchPanel.Close();

        internal void ShowAllLevels() => ShowAll();

        internal void Search(string keywords) => RunQuickSearch(keywords);

        internal override void OnShown()
        {
            if (_session.LevelsInfo == null && _infoLoad == null)
            {
                LoadInfo();
            }

            RefreshInstalled();
        }

        internal override void OnHidden()
        {
            if (!_search.isFocused)
            {
                return;
            }

            _search.DeactivateInputField();
        }

        internal override void PlayEnter()
        {
            for (int i = 0; i < _homeTiles.Count && i < 18; i++)
            {
                _homeTiles[i].PlayEnter(i * LibraryTheme.Motion.STAGGER);
            }
        }

        internal override bool TryHandleBack()
        {
            if (_searchPanel.IsOpen)
            {
                _searchPanel.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_mode != Mode.Home)
            {
                UiSound.Play(UiCue.Back);
                SetMode(Mode.Home);
                return true;
            }

            return false;
        }

        protected override void OnDispose()
        {
            Cancel(ref _infoLoad);
            Cancel(ref _listLoad);
            foreach (SonolusLevelTile tile in _homeTiles)
            {
                tile.Release();
            }

            foreach (SonolusLevelTile tile in _gridTiles.Values)
            {
                tile.Release();
            }

            _status.Dispose();
            _searchPanel.Dispose();
        }

        // ---------------------------------------------------------------- loading

        private void Reload()
        {
            if (_mode == Mode.Home || _session.LevelsInfo == null)
            {
                _session.LevelsInfo = null;
                _homeBuilt = false;
                LoadInfo();
            }
            else
            {
                StartList(_query, _resultLabel, _mode);
            }
        }

        private void LoadInfo()
        {
            Cancel(ref _infoLoad);
            _infoLoad = new CancellationTokenSource();
            LoadInfoAsync(_infoLoad.Token).Forget();
        }

        private async UniTaskVoid LoadInfoAsync(CancellationToken cancellationToken)
        {
            if (_mode == Mode.Home)
            {
                _status.ShowLoading("正在连接 " + _session.Title + "…");
            }

            try
            {
                if (_session.Info == null)
                {
                    _session.Info = await _session.Client.GetServerInfoAsync(cancellationToken);
                    _title.text = _session.Title;
                }

                _session.LevelsInfo = await _session.Client.GetLevelsInfoAsync(cancellationToken);
                _infoLoad?.Dispose();
                _infoLoad = null;
                _searchPanel.SetSearch(FindAdvancedSearch());
                _filter.gameObject.SetActive(FindAdvancedSearch() != null);
                BuildHome();
                if (_mode == Mode.Home)
                {
                    _status.Hide();
                    if (_session.LevelsInfo.Sections.Count == 0)
                    {
                        // Nothing curated: go straight to every level.
                        ShowAll();
                    }
                    else
                    {
                        PlayEnter();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                _infoLoad?.Dispose();
                _infoLoad = null;
                if (_mode == Mode.Home)
                {
                    _status.ShowError(exception.Message, LoadInfo);
                }
            }
        }

        private SonolusSearch FindAdvancedSearch()
        {
            IReadOnlyList<SonolusSearch> searches = _session.LevelsInfo?.Searches;
            if (searches == null)
            {
                return null;
            }

            foreach (SonolusSearch search in searches)
            {
                if (search.Type != "quick")
                {
                    return search;
                }
            }

            return null;
        }

        private void ShowAll()
        {
            if (_mode == Mode.All && _results.Count > 0)
            {
                return;
            }

            StartList(null, "全部谱面", Mode.All);
        }

        private void RunQuickSearch(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                if (_mode == Mode.Results && !_advancedActive)
                {
                    SetMode(Mode.Home);
                }

                return;
            }

            if (_mode == Mode.Results && _resultLabel == "“" + text + "”" && !_advancedActive)
            {
                return;
            }

            UiSound.Play(UiCue.Press);
            _advancedActive = false;
            _filterDot.enabled = false;
            _searchPanel.ResetValues();
            var query = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("type", "quick"),
                new KeyValuePair<string, string>("keywords", text)
            };
            StartList(query, "“" + text + "”", Mode.Results);
        }

        private void RunAdvancedSearch(List<KeyValuePair<string, string>> query, bool changed)
        {
            _advancedActive = changed;
            _filterDot.enabled = changed;
            _search.SetTextWithoutNotify(string.Empty);
            StartList(query, changed ? "筛选结果" : "全部谱面", changed ? Mode.Results : Mode.All);
        }

        private void StartList(List<KeyValuePair<string, string>> query, string label, Mode mode)
        {
            Cancel(ref _listLoad);
            _query = query;
            _resultLabel = label;
            _results.Clear();
            _nextPage = 0;
            _cursor = null;
            _pageCount = 0;
            _hasMore = true;
            _loadingMore = false;
            _mode = mode;
            ApplyMode();
            _grid.SetItemCount(0);
            LoadPage();
        }

        private void LoadPage()
        {
            if (_loadingMore || !_hasMore)
            {
                return;
            }

            _loadingMore = true;
            _listLoad = new CancellationTokenSource();
            if (_nextPage == 0)
            {
                _status.ShowLoading("正在加载谱面…");
            }

            LoadPageAsync(_listLoad.Token).Forget();
            UpdatePageInfo();
        }

        private async UniTaskVoid LoadPageAsync(CancellationToken cancellationToken)
        {
            int page = _nextPage;
            try
            {
                SonolusPage<SonolusLevel> result = await _listLevels(_query, page, _cursor, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _listLoad.Dispose();
                _listLoad = null;
                _loadingMore = false;
                int before = _results.Count;
                _results.AddRange(result.Items);
                _pageCount = result.PageCount;
                _cursor = result.UsesCursor ? result.Cursor : null;
                _hasMore = result.Items.Count > 0 && result.HasMoreAfter(page);
                _nextPage = page + 1;
                _grid.SetItemCount(_results.Count, keepScrollPosition: before > 0);
                if (before == 0)
                {
                    PlayGridEnter();
                }

                if (_results.Count == 0)
                {
                    _status.ShowMessage(_mode == Mode.Results ? "没有找到符合条件的谱面" : "服务器上还没有谱面");
                }
                else
                {
                    _status.Hide();
                }

                UpdatePageInfo();
                MaybeLoadMore();
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _listLoad.Dispose();
                _listLoad = null;
                _loadingMore = false;
                if (_results.Count == 0)
                {
                    _status.ShowError(exception.Message, () => StartList(_query, _resultLabel, _mode));
                }
                else
                {
                    Screen.ShowToast(exception.Message);
                }

                UpdatePageInfo(exception.Message);
            }
        }

        private void MaybeLoadMore()
        {
            if (_mode == Mode.Home || _loadingMore || !_hasMore || _results.Count == 0)
            {
                return;
            }

            if (_grid.EndActiveIndex >= _results.Count - GRID_COLUMNS)
            {
                LoadPage();
            }
        }

        private void UpdatePageInfo(string error = null)
        {
            if (_mode == Mode.Home)
            {
                _pageInfo.text = _session.LevelsInfo != null ? _session.LevelsInfo.Sections.Count + " 个栏目" : string.Empty;
                return;
            }

            string text = _results.Count + " 首";
            if (_pageCount > 0)
            {
                text += "  ·  第 " + Mathf.Min(_nextPage, _pageCount) + " / " + _pageCount + " 页";
            }

            if (_loadingMore)
            {
                text += "  ·  加载中…";
            }
            else if (error != null)
            {
                text += "  ·  加载失败，滚动到底部重试";
            }
            else if (!_hasMore && _results.Count > 0)
            {
                text += "  ·  已全部加载";
            }

            _pageInfo.text = text;
        }

        // ---------------------------------------------------------------- modes

        private void SetMode(Mode mode)
        {
            if (mode == Mode.Results && _query == null)
            {
                return;
            }

            _mode = mode;
            ApplyMode();
            if (mode == Mode.Home)
            {
                Cancel(ref _listLoad);
                _loadingMore = false;
                if (_session.LevelsInfo == null)
                {
                    LoadInfo();
                }
                else
                {
                    _status.Hide();
                }
            }
            else if (_nextPage == 0)
            {
                LoadPage();
            }
            else if (_results.Count == 0)
            {
                _status.ShowMessage("没有找到符合条件的谱面");
            }
            else
            {
                MaybeLoadMore();
            }
        }

        private void ApplyMode()
        {
            bool home = _mode == Mode.Home;
            _homeArea.gameObject.SetActive(home);
            _gridArea.gameObject.SetActive(!home);
            _homeChip.Set(home);
            _allChip.Set(_mode == Mode.All);
            bool hasResults = _mode == Mode.Results || (_query != null && _resultLabel != "全部谱面");
            _resultChip.Root.gameObject.SetActive(hasResults);
            _resultChip.SetLabel("搜索：" + (_resultLabel ?? string.Empty));
            _resultChip.Set(_mode == Mode.Results);
            float x = 0f;
            x = _homeChip.Place(x);
            x = _allChip.Place(x);
            if (hasResults)
            {
                _resultChip.Place(x);
            }

            if (home)
            {
                _status.Hide();
            }

            UpdatePageInfo();
        }

        // ---------------------------------------------------------------- home sections

        private void BuildHome()
        {
            if (_homeBuilt)
            {
                return;
            }

            _homeBuilt = true;
            foreach (SonolusLevelTile tile in _homeTiles)
            {
                tile.Release();
            }

            _homeTiles.Clear();
            for (int i = _homeContent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_homeContent.GetChild(i).gameObject);
            }

            foreach (SonolusSection section in _session.LevelsInfo.Sections)
            {
                BuildSection(section);
            }

            RectTransform more = ServerWidgets.ListItem(_homeContent, "BrowseAll", 88f);
            Button all = ServerWidgets.LabelledButton(more, "All", "浏览全部谱面", LibraryIcon.Forward, ShowAll, 300f, 72f,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Forward);
            LibraryWidgets.Outline((Image)all.targetGraphic, 36f);
            LibraryWidgets.Place((RectTransform)all.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 72f));
            UpdatePageInfo();
        }

        private void BuildSection(SonolusSection section)
        {
            float tileHeight = SECTION_TILE + SonolusLevelTile.TEXT_HEIGHT;
            RectTransform block = ServerWidgets.ListItem(_homeContent, "Section-" + section.Title, SECTION_HEADER + tileHeight + 12f);

            LibraryIcon? icon = ServerWidgets.IconFor(section.Icon);
            float textLeft = 0f;
            if (icon.HasValue)
            {
                Image glyph = LibraryWidgets.Icon(block, icon.Value, HomeTheme.Colors.Periwinkle, 30f);
                LibraryWidgets.Place(glyph.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -24f), new Vector2(30f, 30f));
                textLeft = 44f;
            }

            TextMeshProUGUI title = LibraryWidgets.Label("Title", block, ServerWidgets.Localize(section.Title), 30f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            float titleWidth = Mathf.Ceil(title.GetPreferredValues(title.text).x) + 4f;
            LibraryWidgets.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(textLeft, -24f), new Vector2(titleWidth, 44f));
            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                TextMeshProUGUI description = LibraryWidgets.Label("Description", block, section.Description.Replace('\n', ' '),
                    LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
                description.rectTransform.anchorMin = new Vector2(0f, 1f);
                description.rectTransform.anchorMax = new Vector2(1f, 1f);
                description.rectTransform.pivot = new Vector2(0f, 0.5f);
                description.rectTransform.offsetMin = new Vector2(textLeft + titleWidth + 20f, -40f);
                description.rectTransform.offsetMax = new Vector2(0f, -8f);
            }

            // One horizontal row per section; it scrolls sideways when the covers do not fit.
            var rowObject = new GameObject("Row", typeof(RectTransform), typeof(RectMask2D));
            rowObject.transform.SetParent(block, false);
            RectTransform row = (RectTransform)rowObject.transform;
            LibraryWidgets.Inset(row, -20f, 0f, -20f, SECTION_HEADER - 8f);
            Image drag = rowObject.AddComponent<Image>();
            drag.color = new Color32(0, 0, 0, 0);
            RectTransform content = LibraryUi.CreateRect("Content", row);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            float pad = 20f;
            int count = section.Levels.Count;
            content.sizeDelta = new Vector2(pad * 2f + count * SECTION_TILE + Mathf.Max(0, count - 1) * SECTION_GAP, 0f);
            NestedScrollRect scroll = rowObject.AddComponent<NestedScrollRect>();
            // A vertical drag that starts on a row still scrolls the page.
            scroll.Outer = _homeScroll;
            scroll.viewport = row;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            for (int i = 0; i < count; i++)
            {
                var tile = new SonolusLevelTile(content, OnTileClicked);
                RectTransform rect = tile.Root;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(SECTION_TILE, tileHeight);
                rect.anchoredPosition = new Vector2(pad + i * (SECTION_TILE + SECTION_GAP), -8f);
                SonolusLevel level = section.Levels[i];
                tile.Bind(_session, level, Screen.Covers, IsInstalled(level));
                _homeTiles.Add(tile);
            }
        }

        // ---------------------------------------------------------------- grid

        private static ScrollRect CreateGridScroll(RectTransform parent)
        {
            var go = new GameObject("Levels", typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            LibraryUi.Stretch((RectTransform)go.transform);
            Image drag = go.AddComponent<Image>();
            drag.color = new Color32(0, 0, 0, 0);
            RectTransform viewport = LibraryUi.CreateRect("Viewport", go.transform);
            viewport.gameObject.AddComponent<RectMask2D>();
            LibraryUi.Stretch(viewport);
            RectTransform content = LibraryUi.CreateRect("Content", viewport);
            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        private void BindGridTile(RectTransform root, int index)
        {
            SonolusLevel level = _results[index];
            _gridTiles[root].Bind(_session, level, Screen.Covers, IsInstalled(level));
        }

        private void PlayGridEnter()
        {
            int order = 0;
            for (int index = _grid.FirstActiveIndex; index < _grid.EndActiveIndex; index++)
            {
                RectTransform root = _grid.CellAt(index);
                if (root != null)
                {
                    _gridTiles[root].PlayEnter(Mathf.Min(order++, 18) * LibraryTheme.Motion.STAGGER);
                }
            }
        }

        private void OnTileClicked(SonolusLevelTile tile)
        {
            if (tile.Level != null)
            {
                Screen.OpenLevel(_session, tile.Level);
            }
        }

        private bool IsInstalled(SonolusLevel level) =>
            Screen.Installer.StateOf(_session.Address, level) != SonolusInstallState.Missing;

        private void RefreshInstalled()
        {
            foreach (SonolusLevelTile tile in _homeTiles)
            {
                if (tile.Level != null)
                {
                    tile.SetInstalled(IsInstalled(tile.Level));
                }
            }

            foreach (SonolusLevelTile tile in _gridTiles.Values)
            {
                if (tile.Level != null)
                {
                    tile.SetInstalled(IsInstalled(tile.Level));
                }
            }
        }

        private void OpenAdvanced()
        {
            if (FindAdvancedSearch() == null)
            {
                Screen.ShowToast("该服务器没有提供高级搜索");
                return;
            }

            _searchPanel.Open();
        }

        private static void Cancel(ref CancellationTokenSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Cancel();
            source.Dispose();
            source = null;
        }

        /// <summary>A chip in the 首页 / 全部谱面 / 搜索 row.</summary>
        private sealed class ModeChip
        {
            private readonly Image _background;
            private readonly TextMeshProUGUI _label;

            internal ModeChip(RectTransform parent, string label, Action onClick)
            {
                Button button = LibraryWidgets.PillButton("Mode-" + label, parent, label, () => onClick(), 120f, MODE_HEIGHT,
                    LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 19f, sound: UiCue.Select);
                _background = (Image)button.targetGraphic;
                _label = button.GetComponentInChildren<TextMeshProUGUI>();
                Root = (RectTransform)button.transform;
                Root.anchorMin = new Vector2(0f, 0.5f);
                Root.anchorMax = new Vector2(0f, 0.5f);
                Root.pivot = new Vector2(0f, 0.5f);
                SetLabel(label);
            }

            internal RectTransform Root { get; }

            internal void SetLabel(string label)
            {
                _label.text = label;
                float width = Mathf.Min(420f, Mathf.Ceil(_label.GetPreferredValues(label).x) + 44f);
                Root.sizeDelta = new Vector2(width, MODE_HEIGHT);
            }

            internal void Set(bool current)
            {
                _background.color = current ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.Control;
                _label.color = current ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextPrimary;
            }

            internal float Place(float x)
            {
                Root.anchoredPosition = new Vector2(x, 0f);
                return x + Root.sizeDelta.x + 10f;
            }
        }
    }

    /// <summary>
    /// A sideways scroll row that sits inside a vertical page: a drag that starts mostly
    /// vertical (and the mouse wheel) goes to the outer scroll instead of the row.
    /// </summary>
    internal sealed class NestedScrollRect : ScrollRect
    {
        internal ScrollRect Outer;
        private bool _forward;

        public override void OnInitializePotentialDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (Outer != null)
            {
                Outer.OnInitializePotentialDrag(eventData);
            }

            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _forward = Outer != null && Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x);
            if (_forward)
            {
                Outer.OnBeginDrag(eventData);
            }
            else
            {
                base.OnBeginDrag(eventData);
            }
        }

        public override void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_forward)
            {
                Outer.OnDrag(eventData);
            }
            else
            {
                base.OnDrag(eventData);
            }
        }

        public override void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_forward)
            {
                Outer.OnEndDrag(eventData);
                _forward = false;
            }
            else
            {
                base.OnEndDrag(eventData);
            }
        }

        public override void OnScroll(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (Outer != null)
            {
                Outer.OnScroll(eventData);
            }
            else
            {
                base.OnScroll(eventData);
            }
        }
    }
}
