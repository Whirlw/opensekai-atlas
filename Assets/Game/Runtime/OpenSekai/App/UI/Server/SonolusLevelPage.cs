using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One level of a Sonolus server, laid out like the library's detail side: the cover and the
    /// buttons that act on it on the left (游玩, 下载 / 更新, 试听), the title, credits and tags on
    /// the right, and three tabs under them: 简介 (the description), 社区 (comments, paged) and
    /// 排行榜 (each leaderboard the server lists, paged). 游玩 downloads first when the level is
    /// not in the library yet, then hands the package to the library to start.
    /// </summary>
    internal sealed class SonolusLevelPage : ServerPage
    {
        private const float COVER = 440f;
        private const float COLUMN_GAP = 56f;
        private const float PLAY_HEIGHT = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
        private const float SMALL_HEIGHT = 72f;
        private const float TAB_WIDTH = 168f;
        private const float TAB_HEIGHT = 60f;
        private static readonly Color32 CardColor = new Color32(16, 20, 27, 184);
        private static readonly Color32 Gold = new Color32(245, 206, 110, 255);

        private readonly SonolusSession _session;
        private SonolusLevel _level;
        private SonolusLevelDetails _details;

        private readonly Image _cover;
        private readonly RectTransform _chips;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _artists;
        private readonly TextMeshProUGUI _author;
        private readonly RectTransform _tags;
        private readonly Button _play;
        private readonly Button _download;
        private readonly Button _preview;
        private readonly Image _progressTrack;
        private readonly Image _progressFill;
        private readonly TextMeshProUGUI _state;
        private readonly Tab[] _tabs = new Tab[3];
        private readonly RectTransform[] _panels = new RectTransform[3];

        private readonly RectTransform _descriptionContent;
        private readonly RectTransform _commentsContent;
        private readonly ScrollRect _commentsScroll;
        private readonly ServerStatusView _commentsStatus;
        private readonly RectTransform _recordsContent;
        private readonly ScrollRect _recordsScroll;
        private readonly ServerStatusView _recordsStatus;
        private readonly RectTransform _boardBar;
        private readonly LibraryDropdown _boardDropdown;
        private readonly TextMeshProUGUI _boardInfo;
        private readonly ServerStatusView _detailStatus;

        private CancellationTokenSource _detailLoad;
        private CancellationTokenSource _coverLoad;
        private CancellationTokenSource _install;
        private CancellationTokenSource _commentLoad;
        private CancellationTokenSource _recordLoad;
        private bool _playAfterInstall;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;
        private int _tab;
        private bool _commentsStarted;
        private int _commentPage;
        private string _commentCursor;
        private bool _commentsMore;
        private Button _commentsMoreButton;
        private int _board = -1;
        private int _recordPage;
        private string _recordCursor;
        private bool _recordsMore;
        private Button _recordsMoreButton;
        private int _recordCount;

        internal SonolusLevelPage(ServerScreen screen, SonolusSession session, SonolusLevel level) : base(screen, "SonolusLevel")
        {
            _session = session;
            _level = level;
            RectTransform bar = ServerWidgets.TopBar(Root, "谱面详情", screen.Back, out _, out TextMeshProUGUI subtitle);
            subtitle.text = session.Title;
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button refresh = ServerWidgets.IconButton(bar, "Refresh", LibraryIcon.Refresh, LoadDetails);
            LibraryWidgets.Place((RectTransform)refresh.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(height, height));

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform main = LibraryUi.CreateRect("Main", Root);
            LibraryWidgets.Inset(main, margin, margin - 16f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            // ------------------------------------------------ left: cover and actions
            RectTransform left = LibraryUi.CreateRect("Left", main);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 1f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(COVER, 0f);

            RectTransform coverFrame = LibraryUi.CreateRect("Cover", left);
            LibraryWidgets.Place(coverFrame, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(COVER, COVER));
            LibraryWidgets.Shadow(coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.CoverShadow.BLUR, LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.SurfaceSelected, 120f);
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            _cover.enabled = false;
            LibraryUi.Stretch(_cover.rectTransform);

            float y = COVER + 32f;
            _play = ServerWidgets.LabelledButton(left, "Play", "游玩", LibraryIcon.Play, Play, COVER, PLAY_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, UiCue.None, 26f);
            LibraryWidgets.Place((RectTransform)_play.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(COVER, PLAY_HEIGHT));
            y += PLAY_HEIGHT + 16f;
            float half = (COVER - 16f) * 0.5f;
            _download = ServerWidgets.LabelledButton(left, "Download", "下载", LibraryIcon.Download, Download, half, SMALL_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Outline((Image)_download.targetGraphic, SMALL_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)_download.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(half, SMALL_HEIGHT));
            _preview = ServerWidgets.LabelledButton(left, "Preview", "试听", LibraryIcon.Wave, TogglePreview, half, SMALL_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Outline((Image)_preview.targetGraphic, SMALL_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)_preview.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(half + 16f, -y), new Vector2(half, SMALL_HEIGHT));
            y += SMALL_HEIGHT + 22f;

            _progressTrack = LibraryWidgets.Panel("Progress", left, 3f, new Color32(255, 255, 255, 31));
            LibraryWidgets.Place(_progressTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(COVER, 6f));
            _progressFill = LibraryWidgets.Panel("Fill", _progressTrack.transform, 3f, LibraryTheme.Colors.DefaultAccent);
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _progressFill.rectTransform.offsetMin = Vector2.zero;
            _progressFill.rectTransform.offsetMax = Vector2.zero;
            _progressTrack.gameObject.SetActive(false);
            _state = LibraryWidgets.Label("State", left, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            _state.textWrappingMode = TextWrappingModes.Normal;
            _state.overflowMode = TextOverflowModes.Ellipsis;
            _state.maxVisibleLines = 2;
            LibraryWidgets.Place(_state.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(y + 16f)), new Vector2(COVER, 56f));

            // ------------------------------------------------ right: info, tags, tabs
            RectTransform right = LibraryUi.CreateRect("Right", main);
            LibraryWidgets.Inset(right, COVER + COLUMN_GAP, 0f, 0f, 0f);

            _chips = LibraryUi.CreateRect("Chips", right);
            LibraryWidgets.TopStrip(_chips, 0f, 0f, 0f, 40f);

            _title = LibraryUi.CreateText("Title", right, string.Empty, LibraryTheme.FontSize.DETAIL_TITLE, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            _title.color = LibraryTheme.Colors.TextPrimary;
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _title.maxVisibleLines = 2;
            LibraryWidgets.TopStrip(_title.rectTransform, 0f, 0f, 54f, 120f);

            _artists = CreditLine(right, "艺术家", 184f);
            _author = CreditLine(right, "谱面", 222f);

            _tags = LibraryUi.CreateRect("Tags", right);
            LibraryWidgets.TopStrip(_tags, 0f, 0f, 272f, 40f);
            _tags.gameObject.AddComponent<RectMask2D>();

            RectTransform tabBar = LibraryUi.CreateRect("Tabs", right);
            LibraryWidgets.TopStrip(tabBar, 0f, 0f, 336f, TAB_HEIGHT);
            Image track = LibraryWidgets.Panel("Track", tabBar, TAB_HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            LibraryWidgets.Outline(track, TAB_HEIGHT * 0.5f);
            LibraryWidgets.Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(TAB_WIDTH * 3f + 8f, TAB_HEIGHT));
            string[] tabNames = { "简介", "社区", "排行榜" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _tabs[i] = new Tab(track.transform, tabNames[i], i, () => SelectTab(index, true));
            }

            Image panel = LibraryWidgets.Panel("Panel", right, LibraryTheme.Radius.PANEL, CardColor);
            LibraryWidgets.Inset(panel.rectTransform, 0f, 0f, 0f, 336f + TAB_HEIGHT + 20f);
            LibraryWidgets.Outline(panel, LibraryTheme.Radius.PANEL);
            RectTransform inner = LibraryUi.CreateRect("Inner", panel.transform);
            LibraryWidgets.Inset(inner, 32f, 12f, 20f, 12f);
            for (int i = 0; i < 3; i++)
            {
                _panels[i] = LibraryUi.CreateRect(tabNames[i], inner);
                LibraryUi.Stretch(_panels[i]);
            }

            _descriptionContent = ServerWidgets.VerticalList(_panels[0], out _, 14f, new RectOffset(0, 12, 16, 24));
            _detailStatus = new ServerStatusView(_panels[0]);

            _commentsContent = ServerWidgets.VerticalList(_panels[1], out _commentsScroll, 14f, new RectOffset(0, 12, 16, 24));
            _commentsStatus = new ServerStatusView(_panels[1]);

            _boardBar = LibraryUi.CreateRect("Boards", _panels[2]);
            LibraryWidgets.TopStrip(_boardBar, 0f, 12f, 16f, 60f);
            _boardDropdown = LibraryDropdown.Create(_boardBar, screen.Overlay, "榜单", 460f, 60f);
            LibraryWidgets.Place((RectTransform)_boardDropdown.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(460f, 60f));
            _boardInfo = LibraryWidgets.Label("Info", _boardBar, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(_boardInfo.rectTransform);
            RectTransform records = LibraryUi.CreateRect("Records", _panels[2]);
            LibraryWidgets.Inset(records, 0f, 0f, 0f, 88f);
            _recordsContent = ServerWidgets.VerticalList(records, out _recordsScroll, 6f, new RectOffset(0, 12, 4, 24));
            _recordsStatus = new ServerStatusView(records);

            screen.Preview.StateChanged += OnPreviewState;
            ShowLevel(level);
            SelectTab(0, false);
            LoadDetails();
            LoadCover();
        }

        // ---------------------------------------------------------------- tools (screenshots)

        internal bool HasDetails => _details != null;

        internal bool IsTabLoaded => _tab == 0 || (_tab == 1 ? _commentLoad == null : _recordLoad == null);

        internal void ShowTab(int index) => SelectTab(index, false);

        internal override void OnShown()
        {
            UpdateActions();
        }

        internal override void OnHidden()
        {
            Screen.Preview.Stop();
        }

        internal override bool TryHandleBack()
        {
            if (_boardDropdown.IsOpen)
            {
                _boardDropdown.Close();
                UiSound.Play(UiCue.Collapse);
                return true;
            }

            return false;
        }

        protected override void OnDispose()
        {
            Screen.Preview.StateChanged -= OnPreviewState;
            Cancel(ref _detailLoad);
            Cancel(ref _coverLoad);
            Cancel(ref _install);
            Cancel(ref _commentLoad);
            Cancel(ref _recordLoad);
            _detailStatus.Dispose();
            _commentsStatus.Dispose();
            _recordsStatus.Dispose();
        }

        // ---------------------------------------------------------------- level

        private void ShowLevel(SonolusLevel level)
        {
            _level = level;
            _title.text = level.Title;
            _artists.text = string.IsNullOrWhiteSpace(level.Artists) ? "—" : level.Artists;
            _author.text = string.IsNullOrWhiteSpace(level.Author) ? "—" : level.Author;
            BuildChips();
            BuildTags();
            UpdateActions();
        }

        private void BuildChips()
        {
            Clear(_chips);
            float x = 0f;
            x = PlaceChip(_chips, ServerWidgets.Chip(_chips, "LV " + _level.RatingText, null, LibraryTheme.Colors.Hairline, _accent, out float w0,
                40f, 22f, true), x, w0);
            string difficulty = SonolusLevelInstaller.DifficultyOf(_level);
            bool tagged = false;
            foreach (SonolusTag tag in _level.Tags)
            {
                tagged |= string.Equals(tag.Title?.Trim(), difficulty, StringComparison.OrdinalIgnoreCase);
            }

            if (tagged)
            {
                x = PlaceChip(_chips, ServerWidgets.Chip(_chips, difficulty.ToUpperInvariant(), null, LibraryTheme.Colors.Hairline,
                    LibraryTheme.Colors.Difficulty(difficulty), out float w1, 40f, 22f, true), x, w1);
            }

            string engine = _level.EngineTitle ?? _level.EngineName;
            if (!string.IsNullOrEmpty(engine))
            {
                bool supported = SonolusLevelInstaller.IsSupported(_level, out _);
                PlaceChip(_chips, ServerWidgets.Chip(_chips, engine, null, LibraryTheme.Colors.Hairline,
                    supported ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.Warning, out float w2), x, w2);
            }
        }

        private void BuildTags()
        {
            Clear(_tags);
            float x = 0f;
            foreach (SonolusTag tag in _level.Tags)
            {
                string text = ServerWidgets.Localize(tag.Title);
                LibraryIcon? icon = ServerWidgets.IconFor(tag.Icon) ?? (string.IsNullOrEmpty(text) ? (LibraryIcon?)LibraryIcon.Tag : null);
                if (string.IsNullOrEmpty(text) && tag.Icon == "trophy")
                {
                    text = "精选";
                }

                RectTransform chip = ServerWidgets.Chip(_tags, text ?? string.Empty, icon, LibraryTheme.Colors.Control,
                    LibraryTheme.Colors.TextSecondary, out float width);
                x = PlaceChip(_tags, chip, x, width);
            }
        }

        private static float PlaceChip(RectTransform row, RectTransform chip, float x, float width)
        {
            chip.anchorMin = new Vector2(0f, 0.5f);
            chip.anchorMax = new Vector2(0f, 0.5f);
            chip.pivot = new Vector2(0f, 0.5f);
            chip.anchoredPosition = new Vector2(x, 0f);
            return x + width + 10f;
        }

        private static TextMeshProUGUI CreditLine(RectTransform parent, string caption, float top)
        {
            TextMeshProUGUI name = LibraryWidgets.Label("Caption-" + caption, parent, caption, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(name.rectTransform, 0f, 0f, top, 34f);
            TextMeshProUGUI value = LibraryWidgets.Label("Credit-" + caption, parent, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(value.rectTransform, 92f, 0f, top, 34f);
            return value;
        }

        private void LoadDetails()
        {
            Cancel(ref _detailLoad);
            _detailLoad = new CancellationTokenSource();
            LoadDetailsAsync(_detailLoad.Token).Forget();
        }

        private async UniTaskVoid LoadDetailsAsync(CancellationToken cancellationToken)
        {
            if (_details == null)
            {
                _detailStatus.ShowLoading();
            }

            try
            {
                SonolusLevelDetails details = await _session.Client.GetLevelAsync(_level.Name, cancellationToken);
                _details = details;
                _detailStatus.Hide();
                bool coverChanged = details.Level.Cover?.Url != _level.Cover?.Url;
                ShowLevel(details.Level);
                ShowDescription();
                SetupBoards();
                if (coverChanged || !_cover.enabled)
                {
                    LoadCover();
                }

                if (_tab == 1)
                {
                    StartComments();
                }
                else if (_tab == 2)
                {
                    StartRecords();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                if (_details == null)
                {
                    _detailStatus.ShowError(exception.Message, LoadDetails);
                    UpdateActions();
                }
                else
                {
                    Screen.ShowToast(exception.Message);
                }
            }
        }

        private void ShowDescription()
        {
            Clear(_descriptionContent);
            string description = _details?.Description;
            TextMeshProUGUI body = ServerWidgets.Paragraph(_descriptionContent, "Description",
                string.IsNullOrWhiteSpace(description) ? "作者没有写简介。" : description.Trim(),
                22f, string.IsNullOrWhiteSpace(description) ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.TextSecondary);
            body.lineSpacing = 18f;

            RectTransform divider = ServerWidgets.ListItem(_descriptionContent, "Divider", 1f);
            Image line = LibraryUi.CreateImage("Line", divider, LibraryTheme.Colors.Hairline);
            line.raycastTarget = false;
            LibraryUi.Stretch(line.rectTransform);

            AddMeta("引擎", (_level.EngineTitle ?? _level.EngineName ?? "未知") + (_level.EngineVersion > 0 ? "（v" + _level.EngineVersion + "）" : string.Empty));
            AddMeta("谱面 ID", _level.Name);
            AddMeta("来源", _session.Address.LevelPage(_level.Name));
        }

        private void AddMeta(string caption, string value)
        {
            RectTransform row = ServerWidgets.ListItem(_descriptionContent, "Meta-" + caption, 32f);
            TextMeshProUGUI name = LibraryWidgets.Label("Caption", row, caption, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Inset(name.rectTransform, 0f, 0f, 0f, 0f);
            TextMeshProUGUI text = LibraryWidgets.Label("Value", row, value, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.Inset(text.rectTransform, 120f, 0f, 0f, 0f);
        }

        private void LoadCover()
        {
            Cancel(ref _coverLoad);
            if (_level.Cover == null)
            {
                return;
            }

            _coverLoad = new CancellationTokenSource();
            LoadCoverAsync(_coverLoad.Token).Forget();
        }

        private async UniTaskVoid LoadCoverAsync(CancellationToken cancellationToken)
        {
            // The list's small cover fills in at once; the sharper one replaces it.
            if (Screen.Covers.TryGetLoaded(_session.Address, _level.Cover, out JacketThumbnail small))
            {
                ApplyCover(small);
            }

            try
            {
                JacketThumbnail sharp = await Screen.Covers.LoadAsync(_session.Address, _level.Cover, true, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    ApplyCover(sharp);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ApplyCover(JacketThumbnail thumbnail)
        {
            if (thumbnail.Sprite == null)
            {
                return;
            }

            _cover.sprite = thumbnail.Sprite;
            _cover.enabled = true;
            _accent = thumbnail.Palette.Accent;
            Screen.SetPalette(thumbnail.Palette);
            BuildChips();
            UpdateActions();
        }

        // ---------------------------------------------------------------- download and play

        private SonolusInstallState InstallState => Screen.Installer.StateOf(_session.Address, _level);

        private void UpdateActions()
        {
            bool supported = SonolusLevelInstaller.IsSupported(_level, out string reason);
            bool installing = _install != null;
            SonolusInstallState state = InstallState;

            Color32 accent = _accent;
            ServerWidgets.SetLabelledButton(_play, installing && _playAfterInstall ? "下载后开始…" : "游玩", LibraryIcon.Play,
                supported ? accent : LibraryTheme.Colors.ControlActive, supported ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextMuted);
            _play.interactable = supported && _details != null || state != SonolusInstallState.Missing;

            if (installing)
            {
                ServerWidgets.SetLabelledButton(_download, "取消", LibraryIcon.Close, LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger);
            }
            else if (state == SonolusInstallState.Installed)
            {
                ServerWidgets.SetLabelledButton(_download, "已下载", LibraryIcon.Check, LibraryTheme.Colors.Control, new Color32(123, 216, 143, 255));
            }
            else if (state == SonolusInstallState.Outdated)
            {
                ServerWidgets.SetLabelledButton(_download, "更新", LibraryIcon.Refresh, LibraryTheme.Colors.Control, LibraryTheme.Colors.Warning);
            }
            else
            {
                ServerWidgets.SetLabelledButton(_download, "下载", LibraryIcon.Download, LibraryTheme.Colors.Control,
                    supported ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted);
            }

            _download.interactable = installing || (supported && _details != null) || state != SonolusInstallState.Missing;
            _progressFill.color = accent;
            OnPreviewState(Screen.Preview.IsPlaying);

            if (installing)
            {
                return;
            }

            _progressTrack.gameObject.SetActive(false);
            if (!supported)
            {
                SetState(reason, LibraryTheme.Colors.Warning);
            }
            else if (state == SonolusInstallState.Installed)
            {
                SetState("已在曲库中，可离线游玩。", LibraryTheme.Colors.TextMuted);
            }
            else if (state == SonolusInstallState.Outdated)
            {
                SetState("服务器上的谱面已更新，点“更新”替换曲库中的旧版本。", LibraryTheme.Colors.Warning);
            }
            else
            {
                SetState(_details == null ? "正在读取谱面信息…" : "下载后加入曲库，可离线游玩。", LibraryTheme.Colors.TextMuted);
            }
        }

        private void SetState(string text, Color32 color)
        {
            _state.text = text ?? string.Empty;
            _state.color = color;
        }

        private void Play()
        {
            if (_install != null)
            {
                return;
            }

            string installed = Screen.Installer.FindInstalled(_session.Address, _level.Name);
            if (installed != null)
            {
                UiSound.Play(UiCue.Start);
                Screen.RequestPlay(installed);
                return;
            }

            if (!SonolusLevelInstaller.IsSupported(_level, out string reason))
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast(reason);
                return;
            }

            if (_details == null)
            {
                UiSound.Play(UiCue.Blocked);
                return;
            }

            UiSound.Play(UiCue.Press);
            StartInstall(true);
        }

        private void Download()
        {
            if (_install != null)
            {
                UiSound.Play(UiCue.Cancel);
                Cancel(ref _install);
                _playAfterInstall = false;
                UpdateActions();
                SetState("已取消下载。", LibraryTheme.Colors.TextMuted);
                return;
            }

            SonolusInstallState state = InstallState;
            if (state == SonolusInstallState.Installed)
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast("已在曲库中");
                return;
            }

            if (!SonolusLevelInstaller.IsSupported(_level, out string reason))
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast(reason);
                return;
            }

            if (_details == null)
            {
                UiSound.Play(UiCue.Blocked);
                return;
            }

            UiSound.Play(UiCue.Press);
            StartInstall(false);
        }

        private void StartInstall(bool thenPlay)
        {
            _playAfterInstall = thenPlay;
            _install = new CancellationTokenSource();
            UpdateActions();
            _progressTrack.gameObject.SetActive(true);
            SetProgress(0f, "准备下载…");
            InstallAsync(_install.Token).Forget();
        }

        private async UniTaskVoid InstallAsync(CancellationToken cancellationToken)
        {
            try
            {
                SongPackage package = await Screen.Installer.InstallAsync(_session.Client, _details, SetProgress, cancellationToken);
                _install?.Dispose();
                _install = null;
                Screen.NotifyLibraryChanged();
                UiSound.Play(UiCue.Success);
                bool play = _playAfterInstall;
                _playAfterInstall = false;
                UpdateActions();
                if (play)
                {
                    Screen.RequestPlay(package.RootDirectory);
                }
                else
                {
                    Screen.ShowToast("“" + _level.Title + "”已加入曲库");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                Fail(exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Fail("下载失败：" + exception.Message);
            }
        }

        private void Fail(string message)
        {
            _install?.Dispose();
            _install = null;
            _playAfterInstall = false;
            UpdateActions();
            UiSound.Play(UiCue.Error);
            SetState(message, LibraryTheme.Colors.Danger);
        }

        private void SetProgress(float progress, string message)
        {
            if (_install == null)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);
            _progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
            SetState(message + "  " + Mathf.RoundToInt(progress * 100f) + "%", LibraryTheme.Colors.TextSecondary);
        }

        private void TogglePreview()
        {
            SonolusPreviewPlayer preview = Screen.Preview;
            if (preview.IsPlaying || preview.IsLoading)
            {
                UiSound.Play(UiCue.Press);
                preview.Stop();
                OnPreviewState(false);
                return;
            }

            if (_level.Preview == null && _level.Bgm == null)
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast("服务器没有提供试听音频");
                return;
            }

            UiSound.Play(UiCue.Press);
            preview.Play(_session.Address, _level);
            ServerWidgets.SetLabelledButton(_preview, "加载中…", LibraryIcon.Wave, LibraryTheme.Colors.ControlActive, LibraryTheme.Colors.TextPrimary);
        }

        private void OnPreviewState(bool playing)
        {
            if (playing)
            {
                ServerWidgets.SetLabelledButton(_preview, "停止试听", LibraryIcon.Close, LibraryTheme.Colors.ControlActive, _accent);
            }
            else if (!Screen.Preview.IsLoading)
            {
                ServerWidgets.SetLabelledButton(_preview, "试听", LibraryIcon.Wave, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary);
            }
        }

        // ---------------------------------------------------------------- tabs

        private void SelectTab(int index, bool sound)
        {
            if (sound && index != _tab)
            {
                UiSound.Play(UiCue.Select);
            }

            _tab = index;
            for (int i = 0; i < 3; i++)
            {
                _tabs[i].Set(i == index);
                _panels[i].gameObject.SetActive(i == index);
            }

            if (index == 1)
            {
                StartComments();
            }
            else if (index == 2)
            {
                StartRecords();
            }
        }

        private void StartComments()
        {
            if (_commentsStarted || _details == null)
            {
                if (_details == null)
                {
                    _commentsStatus.ShowLoading();
                }

                return;
            }

            _commentsStarted = true;
            if (!_details.HasCommunity)
            {
                _commentsStatus.ShowMessage("该谱面没有开放社区");
                return;
            }

            _commentPage = 0;
            _commentCursor = null;
            _commentsMore = true;
            Clear(_commentsContent);
            _commentsStatus.ShowLoading("正在加载评论…");
            LoadComments();
        }

        private void LoadComments()
        {
            Cancel(ref _commentLoad);
            _commentLoad = new CancellationTokenSource();
            LoadCommentsAsync(_commentLoad.Token).Forget();
        }

        private async UniTaskVoid LoadCommentsAsync(CancellationToken cancellationToken)
        {
            SetMoreButton(_commentsMoreButton, "加载中…", false);
            try
            {
                SonolusPage<SonolusComment> page = await _session.Client.ListCommentsAsync(_level.Name, _commentPage, _commentCursor, cancellationToken);
                _commentLoad?.Dispose();
                _commentLoad = null;
                if (_commentsMoreButton != null)
                {
                    UnityEngine.Object.Destroy(_commentsMoreButton.transform.parent.gameObject);
                    _commentsMoreButton = null;
                }

                foreach (SonolusComment comment in page.Items)
                {
                    AddComment(comment);
                }

                _commentsMore = page.Items.Count > 0 && page.HasMoreAfter(_commentPage);
                _commentCursor = page.UsesCursor ? page.Cursor : null;
                _commentPage++;
                if (_commentsContent.childCount == 0)
                {
                    _commentsStatus.ShowMessage("还没有评论");
                }
                else
                {
                    _commentsStatus.Hide();
                }

                if (_commentsMore)
                {
                    _commentsMoreButton = AddMoreButton(_commentsContent, LoadComments);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                _commentLoad?.Dispose();
                _commentLoad = null;
                if (_commentsContent.childCount == 0)
                {
                    _commentsStatus.ShowError(exception.Message, () =>
                    {
                        _commentsStarted = false;
                        StartComments();
                    });
                }
                else
                {
                    SetMoreButton(_commentsMoreButton, "重试", true);
                    Screen.ShowToast(exception.Message);
                }
            }
        }

        private void AddComment(SonolusComment comment)
        {
            RectTransform item = LibraryUi.CreateRect("Comment", _commentsContent);
            var layout = item.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 20);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Image background = LibraryWidgets.Panel("Background", item, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            LibraryUi.Stretch(background.rectTransform);

            RectTransform header = ServerWidgets.ListItem(item, "Header", 30f);
            Image avatar = LibraryWidgets.Icon(header, LibraryIcon.User, LibraryTheme.Colors.TextMuted, 24f);
            LibraryWidgets.Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            TextMeshProUGUI author = LibraryWidgets.Label("Author", header, comment.Author, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Inset(author.rectTransform, 34f, 0f, 200f, 0f);
            TextMeshProUGUI time = LibraryWidgets.Label("Time", header, ServerWidgets.DescribeTime(comment.Time), LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(time.rectTransform);

            ServerWidgets.Paragraph(item, "Content", comment.Content, 21f, LibraryTheme.Colors.TextSecondary);
        }

        private void SetupBoards()
        {
            IReadOnlyList<SonolusLeaderboard> boards = _details?.Leaderboards;
            if (boards == null || boards.Count == 0)
            {
                _boardBar.gameObject.SetActive(false);
                return;
            }

            _boardBar.gameObject.SetActive(true);
            var titles = new List<string>();
            foreach (SonolusLeaderboard board in boards)
            {
                titles.Add(ServerWidgets.Localize(board.Title));
            }

            if (_board < 0 || _board >= boards.Count)
            {
                _board = 0;
            }

            _boardDropdown.SetOptions(titles, _board, index =>
            {
                if (index != _board)
                {
                    _board = index;
                    SetupBoards();
                    RestartRecords();
                }
            });
            _boardDropdown.SetAccent(_accent);
        }

        private void StartRecords()
        {
            if (_details == null)
            {
                _recordsStatus.ShowLoading();
                return;
            }

            if (_recordLoad != null || _recordCount > 0 || _recordPage > 0)
            {
                return;
            }

            RestartRecords();
        }

        private void RestartRecords()
        {
            Cancel(ref _recordLoad);
            Clear(_recordsContent);
            _recordsMoreButton = null;
            _recordPage = 0;
            _recordCursor = null;
            _recordCount = 0;
            _boardInfo.text = string.Empty;
            if (_details.Leaderboards.Count == 0)
            {
                _recordsStatus.ShowMessage("该谱面没有排行榜");
                return;
            }

            _recordsStatus.ShowLoading("正在加载排行榜…");
            LoadRecords();
        }

        private void LoadRecords()
        {
            Cancel(ref _recordLoad);
            _recordLoad = new CancellationTokenSource();
            LoadRecordsAsync(_details.Leaderboards[_board].Name, _recordLoad.Token).Forget();
        }

        private async UniTaskVoid LoadRecordsAsync(string board, CancellationToken cancellationToken)
        {
            SetMoreButton(_recordsMoreButton, "加载中…", false);
            try
            {
                SonolusPage<SonolusRecord> page = await _session.Client.ListRecordsAsync(_level.Name, board, _recordPage, _recordCursor, cancellationToken);
                IReadOnlyList<SonolusRecord> records = page.Items;
                if (records.Count == 0 && _recordPage == 0)
                {
                    // Some servers only fill the leaderboard's top records.
                    records = await _session.Client.GetTopRecordsAsync(_level.Name, board, cancellationToken);
                }

                _recordLoad?.Dispose();
                _recordLoad = null;
                if (_recordsMoreButton != null)
                {
                    UnityEngine.Object.Destroy(_recordsMoreButton.transform.parent.gameObject);
                    _recordsMoreButton = null;
                }

                foreach (SonolusRecord record in records)
                {
                    AddRecord(record);
                }

                _recordsMore = page.Items.Count > 0 && page.HasMoreAfter(_recordPage);
                _recordCursor = page.UsesCursor ? page.Cursor : null;
                _recordPage++;
                _boardInfo.text = _recordCount > 0
                    ? _recordCount + " 条记录" + (page.PageCount > 0 ? "  ·  共 " + page.PageCount + " 页" : string.Empty)
                    : string.Empty;
                if (_recordCount == 0)
                {
                    _recordsStatus.ShowMessage("还没有人上榜");
                }
                else
                {
                    _recordsStatus.Hide();
                }

                if (_recordsMore)
                {
                    _recordsMoreButton = AddMoreButton(_recordsContent, LoadRecords);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (SonolusException exception)
            {
                _recordLoad?.Dispose();
                _recordLoad = null;
                if (_recordCount == 0)
                {
                    _recordsStatus.ShowError(exception.Message, RestartRecords);
                }
                else
                {
                    SetMoreButton(_recordsMoreButton, "重试", true);
                    Screen.ShowToast(exception.Message);
                }
            }
        }

        private void AddRecord(SonolusRecord record)
        {
            _recordCount++;
            RectTransform row = ServerWidgets.ListItem(_recordsContent, "Record", 64f);
            Image background = LibraryWidgets.Panel("Background", row, 14f,
                _recordCount % 2 == 1 ? LibraryTheme.Colors.Control : new Color32(255, 255, 255, 6));
            LibraryUi.Stretch(background.rectTransform);

            int position = _recordCount;
            Color32 rankColor = position == 1 ? Gold : position <= 3 ? _accent : LibraryTheme.Colors.TextSecondary;
            TextMeshProUGUI rank = LibraryWidgets.Label("Rank", row, string.IsNullOrEmpty(record.Rank) ? "#" + position : record.Rank,
                30f, rankColor, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(rank);
            LibraryWidgets.Place(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(96f, 64f));
            TextMeshProUGUI player = LibraryWidgets.Label("Player", row, record.Player, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Inset(player.rectTransform, 120f, 0f, 380f, 0f);
            TextMeshProUGUI value = LibraryWidgets.Label("Value", row, record.Value, 24f, LibraryTheme.Colors.TextSecondary,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(value);
            value.characterSpacing = 2f;
            LibraryWidgets.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(360f, 64f));
        }

        private static Button AddMoreButton(RectTransform content, Action load)
        {
            RectTransform item = ServerWidgets.ListItem(content, "More", 76f);
            Button button = LibraryWidgets.PillButton("More", item, "加载更多", () => load(), 240f, 60f,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.Outline((Image)button.targetGraphic, 30f);
            LibraryWidgets.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 60f));
            return button;
        }

        private static void SetMoreButton(Button button, string label, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;
        }

        private static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
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

        /// <summary>One of the 简介 / 社区 / 排行榜 segments.</summary>
        private sealed class Tab
        {
            private readonly Image _background;
            private readonly TextMeshProUGUI _label;

            internal Tab(Transform track, string label, int index, Action onClick)
            {
                float inner = TAB_HEIGHT - 8f;
                Button button = LibraryWidgets.PillButton("Tab-" + label, track, label, () => onClick(), TAB_WIDTH, inner,
                    LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, LibraryTheme.FontSize.BODY, sound: UiCue.None);
                LibraryWidgets.Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(4f + index * TAB_WIDTH, 0f), new Vector2(TAB_WIDTH, inner));
                _background = (Image)button.targetGraphic;
                _label = button.GetComponentInChildren<TextMeshProUGUI>();
            }

            internal void Set(bool current)
            {
                _background.color = current ? LibraryTheme.Colors.TextPrimary : new Color32(255, 255, 255, 0);
                _label.color = current ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
                LibraryUi.ApplyCjkTextStyle(_label, current ? FontStyles.Bold : FontStyles.Normal);
            }
        }
    }
}
