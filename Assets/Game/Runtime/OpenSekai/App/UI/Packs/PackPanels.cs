using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A card listing the choices for one decision (a category's main group, a fallback to add,
    /// one slot's source). The current choice is marked; tapping any other confirms it at once,
    /// tapping the current one or closing the card changes nothing.
    /// </summary>
    internal sealed class PackChoicePanel : IDisposable
    {
        private const float ROW_HEIGHT = 96f;

        internal sealed class Option
        {
            internal string Label;
            internal string Detail;
            internal bool Selected;
            internal bool Enabled = true;
            internal string Badge;
            internal Color32 BadgeTint = HomeTheme.Colors.Periwinkle;
        }

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly List<GameObject> _rows = new List<GameObject>();

        internal PackChoicePanel(RectTransform layer)
        {
            _root = LibraryUi.CreateRect("PackChoices", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1060f, 840f), string.Empty, Close, withSubtitle: true);
            _modal.Title.richText = false;
            _modal.Subtitle.richText = false;
            _content = ServerWidgets.VerticalList(_modal.Body, out _scroll, 10f, new RectOffset(0, 0, 6, 6));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(string title, string subtitle, IReadOnlyList<Option> options, Action<int> onPick)
        {
            foreach (GameObject row in _rows) UnityEngine.Object.Destroy(row);
            _rows.Clear();
            _modal.Title.text = title;
            _modal.Subtitle.text = subtitle ?? string.Empty;
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                Option option = options[i];
                RectTransform item = ServerWidgets.ListItem(_content, "Option" + i, ROW_HEIGHT);
                Image surface = LibraryWidgets.Panel("Surface", item, PackWidgets.ROW_RADIUS,
                    option.Selected ? LibraryTheme.Colors.SurfaceSelected : LibraryTheme.Colors.Control);
                surface.raycastTarget = true;
                LibraryUi.Stretch(surface.rectTransform);
                LibraryWidgets.Outline(surface, PackWidgets.ROW_RADIUS);
                if (option.Selected)
                {
                    Image rim = LibraryWidgets.Shape("Rim", surface.transform, LibraryShapes.Ring(PackWidgets.ROW_RADIUS, 2f),
                        HomeTheme.Colors.WithAlpha(PackWidgets.Accent, 200));
                    LibraryUi.Stretch(rim.rectTransform);
                }

                Image check = LibraryWidgets.Icon(item, option.Selected ? LibraryIcon.Check : LibraryIcon.ChevronRight,
                    option.Selected ? PackWidgets.Accent : LibraryTheme.Colors.TextMuted, 28f);
                LibraryWidgets.Place(check.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(28f, 28f));

                float right = 28f;
                if (!string.IsNullOrEmpty(option.Badge))
                {
                    RectTransform badge = PackWidgets.Tag(item, option.Badge, option.BadgeTint, out float width);
                    LibraryWidgets.Place(badge, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), badge.sizeDelta);
                    right = width + 48f;
                }

                TextMeshProUGUI label = PackWidgets.UserLabel("Label", item, option.Label, 24f,
                    option.Enabled ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted, FontStyles.Bold);
                LibraryWidgets.TopStrip(label.rectTransform, 76f, right, 16f, 34f);
                TextMeshProUGUI detail = PackWidgets.UserLabel("Detail", item, option.Detail ?? string.Empty, 18f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(detail.rectTransform, 76f, right, 52f, 28f);

                PackWidgets.RowButton((RectTransform)surface.transform, () =>
                {
                    if (!option.Enabled)
                    {
                        UiSound.Play(UiCue.Blocked);
                        return;
                    }

                    Close();
                    if (!option.Selected) UiSound.Play(UiCue.Select);
                    onPick?.Invoke(index);
                }, UiCue.None);
                _rows.Add(item.gameObject);
            }

            _scroll.verticalNormalizedPosition = 1f;
            _modal.Open();
            _root.SetAsLastSibling();
        }

        internal void Close() => _modal.Close();

        public void Dispose() => _modal.Dispose();
    }

    /// <summary>"正在导入…" over the page while a ZIP is checked and installed, with a way to cancel.</summary>
    internal sealed class PackImportPanel : IDisposable
    {
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly TextMeshProUGUI _file;
        private readonly CanvasGroup _pulse;
        private MotionHandle _breathe;
        private Action _cancel;

        internal PackImportPanel(RectTransform layer)
        {
            _root = LibraryUi.CreateRect("PackImport", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(780f, 420f), "正在导入…", null, withSubtitle: true);
            _modal.Subtitle.text = "校验清单、素材与容量后安装，不会改变当前选择";
            _file = PackWidgets.UserLabel("File", _modal.Body, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_file.rectTransform, 0f, 0f, 22f, 36f);
            TextMeshProUGUI status = LibraryWidgets.Label("Status", _modal.Body, "正在读取 ZIP…", LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(status.rectTransform, 0f, 0f, 66f, 30f);
            _pulse = status.gameObject.AddComponent<CanvasGroup>();
            Button cancel = ServerWidgets.LabelledButton(_modal.Body, "Cancel", "取消导入", LibraryIcon.Close, () => _cancel?.Invoke(),
                260f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Cancel);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero,
                new Vector2(260f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT));
            _root.gameObject.SetActive(false);
        }

        internal void Show(string file, Action cancel)
        {
            _cancel = cancel;
            _file.text = file ?? string.Empty;
            _modal.Open(UiCue.None);
            _breathe.TryCancel();
            _breathe = LMotion.Create(0f, Mathf.PI * 2f, 1.6f)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(phase => _pulse.alpha = 0.5f + 0.5f * (0.5f + 0.5f * Mathf.Cos(phase)));
        }

        internal void Hide()
        {
            _breathe.TryCancel();
            _breathe = default;
            _cancel = null;
            _modal.Close();
        }

        public void Dispose()
        {
            _breathe.TryCancel();
            _modal.Dispose();
        }
    }

    /// <summary>
    /// Read-only look at what a source provides: the images of an installed group (each cut from
    /// its own verified PNG), its sounds to audition, its layouts and background layers, or the
    /// game's built-in set. It says what is shown and what comes from where; it never changes
    /// the configuration, and closing it stops any audition.
    /// </summary>
    internal sealed class PackPreviewPanel : IDisposable
    {
        private const float TILE_WIDTH = 236f;
        private const float TILE_HEIGHT = 272f;
        private const float AUDIO_HEIGHT = 84f;

        private readonly PackScreen _screen;
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _sourceTag;
        private readonly TextMeshProUGUI _coverage;
        private readonly RectTransform _gridArea;
        private readonly RectTransform _grid;
        private readonly ScrollRect _gridScroll;
        private readonly RectTransform _listArea;
        private readonly RectTransform _list;
        private readonly ScrollRect _listScroll;
        private readonly ServerStatusView _status;
        private readonly List<GameObject> _items = new List<GameObject>();
        private readonly List<(string Key, GameObject Play, GameObject Stop)> _playButtons = new List<(string, GameObject, GameObject)>();
        private System.Threading.CancellationTokenSource _load;
        private string _category;
        private int _pending;
        private int _failures;

        internal PackPreviewPanel(PackScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("PackPreview", screen.ModalLayer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1600f, 900f), string.Empty, Close, withSubtitle: true);
            _modal.Title.richText = false;
            _modal.Subtitle.richText = false;

            RectTransform info = LibraryUi.CreateRect("Info", _modal.Body);
            LibraryWidgets.TopStrip(info, 0f, 0f, 0f, 44f);
            _sourceTag = PackWidgets.Tag(info, "来自此资源包", PackWidgets.Accent, out float tagWidth, 36f, filled: true);
            LibraryWidgets.Place(_sourceTag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, _sourceTag.sizeDelta);
            _coverage = PackWidgets.UserLabel("Coverage", info, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Inset(_coverage.rectTransform, 220f, 0f, 0f, 0f);

            _gridArea = LibraryUi.CreateRect("GridArea", _modal.Body);
            LibraryWidgets.Inset(_gridArea, -12f, 0f, -12f, 60f);
            _grid = PackWidgets.Scroll(_gridArea, out _gridScroll);
            GridLayoutGroup layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TILE_WIDTH, TILE_HEIGHT);
            layout.spacing = new Vector2(18f, 18f);
            layout.padding = new RectOffset(12, 12, 8, 16);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _listArea = LibraryUi.CreateRect("ListArea", _modal.Body);
            LibraryWidgets.Inset(_listArea, 0f, 0f, 0f, 60f);
            _list = ServerWidgets.VerticalList(_listArea, out _listScroll, 10f, new RectOffset(0, 0, 6, 12));

            RectTransform statusArea = LibraryUi.CreateRect("StatusArea", _modal.Body);
            LibraryWidgets.Inset(statusArea, 0f, 0f, 0f, 60f);
            _status = new ServerStatusView(statusArea);
            _screen.Media.PlaybackChanged += RefreshPlayButtons;
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>Everything one installed group provides.</summary>
        internal void OpenGroup(InstalledMaterialPack pack, MaterialPackGroup group)
        {
            MaterialGroupPreview preview;
            try
            {
                preview = _screen.Store.DescribeGroup(pack.PackId, pack.Digest, group.Id);
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                UiSound.Play(UiCue.Error);
                _screen.ShowToast("无法预览：" + e.Message);
                return;
            }

            MaterialSlotCatalog.CategoryInfo info = MaterialSlotCatalog.Category(group.Category);
            string coverage = group.Category == MaterialCategories.BACKGROUND
                ? "背景按整组替换：选用此组会一次替换全部 " + preview.Items.Count + " 个图层"
                : "此组提供 " + preview.ProvidedSlots + " / " + preview.TotalSlots + " 个槽位，其余部分由回退的组或本类内置补上";
            Begin(PackWidgets.GroupName(group), PackWidgets.PackName(pack) + "  ·  " + PackWidgets.Revision(pack) + "  ·  " + info.Title,
                "来自此资源包", PackWidgets.Accent, coverage, group.Category);
            Show(preview.Items);
        }

        /// <summary>The game's own set for a category, or for one of its slots.</summary>
        internal void OpenBuiltin(string category, int set, string onlySlot = null)
        {
            MaterialSlotCatalog.CategoryInfo info = MaterialSlotCatalog.Category(category);
            string label = MaterialCategories.HasBuiltinSets(category) ? MaterialSlotCatalog.BuiltinLabel(category, set) : "游戏默认";
            string title = onlySlot != null ? MaterialSlotCatalog.Slot(category, onlySlot).Name + " · 内置" : info.Title + " · " + label;
            Begin(title, "游戏内置资源，无需安装资源包  ·  " + info.Title, "游戏内置", HomeTheme.Colors.Periwinkle,
                "未被资源包替换的槽位使用这些内置资源", category);
            IEnumerable<string> slots = onlySlot != null ? new[] { onlySlot } : MaterialCategories.Slots(category);
            if (category == MaterialCategories.NOTES)
            {
                ShowGrid();
                foreach (string slot in slots)
                {
                    Sprite sprite = null;
                    try { sprite = PackMedia.LoadBuiltinNote(set, slot); }
                    catch (Exception e) when (PackMedia.IsMediaFailure(e)) { }
                    if (sprite == null) continue;
                    Tile tile = AddTile(slot, null);
                    tile.SetSprite(sprite);
                }

                if (_items.Count == 0) ShowNothing("内置" + info.Title + "暂时无法预览，打歌时照常使用。");
                return;
            }

            if (category == MaterialCategories.AUDIO)
            {
                ShowList();
                foreach (string slot in slots)
                {
                    string key = "builtin/" + set + "/" + slot;
                    AddAudioRow(slot, "内置 · " + label, key, () =>
                    {
                        if (!_screen.Media.ToggleBuiltin(key, set, slot))
                        {
                            UiSound.Play(UiCue.Error);
                            _screen.ShowToast("这个内置音效暂时无法试听，打歌时照常使用。");
                        }
                    });
                }

                return;
            }

            ShowNothing("内置" + info.Title + "是打歌画面中的动态演出与界面元素，此处没有可单独预览的图片。\n未被资源包替换的部分会照常使用它们。");
        }

        internal void Close()
        {
            _load?.Cancel();
            _load = null;
            _screen.Media.Stop();
            ClearItems();
            _screen.Media.ReleasePreview();
            _status.Hide();
            _modal.Close();
        }

        public void Dispose()
        {
            _load?.Cancel();
            if (_screen != null && _screen.Media != null) _screen.Media.PlaybackChanged -= RefreshPlayButtons;
            _status.Dispose();
            _modal.Dispose();
        }

        // ------------------------------------------------------------------ building

        private void Begin(string title, string subtitle, string source, Color32 sourceTint, string coverage, string category)
        {
            Close();
            _category = category;
            _failures = 0;
            _pending = 0;
            _load = new System.Threading.CancellationTokenSource();
            _modal.Title.text = title;
            _modal.Subtitle.text = subtitle;
            PackWidgets.SetTag(_sourceTag, source, sourceTint, filled: true);
            _coverage.text = coverage;
            LibraryWidgets.Inset(_coverage.rectTransform, _sourceTag.sizeDelta.x + 18f, 0f, 0f, 0f);
            _modal.Open();
            _root.SetAsLastSibling();
        }

        private void Show(IReadOnlyList<MaterialPreviewItem> items)
        {
            if (items.Count == 0)
            {
                ShowNothing("此来源没有可预览的内容。");
                return;
            }

            if (items.All(i => i.Kind == MaterialPreviewKind.Audio))
            {
                ShowList();
                foreach (MaterialPreviewItem item in items)
                {
                    string key = item.Path + "#" + item.Slot;
                    AddAudioRow(item.Slot, item.File, key, () => Play(key, item).Forget());
                }

                return;
            }

            ShowGrid();
            foreach (MaterialPreviewItem item in items)
            {
                Tile tile = AddTile(item.Slot, item);
                if (item.Kind is MaterialPreviewKind.Image or MaterialPreviewKind.Sequence) LoadTile(tile, item).Forget();
            }
        }

        private async UniTaskVoid Play(string key, MaterialPreviewItem item)
        {
            bool ok = await _screen.Media.ToggleAsync(key, item);
            if (!ok && _modal.IsOpen)
            {
                UiSound.Play(UiCue.Error);
                _screen.ShowToast("无法试听 " + MaterialSlotCatalog.Slot(_category, item.Slot).Name + "：文件缺失或已改变");
            }
        }

        private async UniTaskVoid LoadTile(Tile tile, MaterialPreviewItem item)
        {
            System.Threading.CancellationTokenSource load = _load;
            _pending++;
            try
            {
                Sprite sprite = await _screen.Media.LoadPreviewAsync(item, load.Token);
                if (load.IsCancellationRequested || tile.Root == null) return;
                tile.SetSprite(sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e) when (PackMedia.IsMediaFailure(e))
            {
                if (load.IsCancellationRequested || tile.Root == null) return;
                _failures++;
                tile.SetFailed();
                Debug.LogWarning("[PackPreview] " + item.Slot + "：" + e.Message);
            }
            finally
            {
                if (!load.IsCancellationRequested)
                {
                    _pending--;
                    if (_pending == 0 && _failures > 0)
                        _coverage.text = _failures + " 项无法预览（文件缺失或与安装修订不符），其余内容可以继续浏览";
                }
            }
        }

        private void ShowGrid()
        {
            _gridArea.gameObject.SetActive(true);
            _listArea.gameObject.SetActive(false);
            _status.Hide();
            _gridScroll.verticalNormalizedPosition = 1f;
        }

        private void ShowList()
        {
            _gridArea.gameObject.SetActive(false);
            _listArea.gameObject.SetActive(true);
            _status.Hide();
            _listScroll.verticalNormalizedPosition = 1f;
        }

        private void ShowNothing(string message)
        {
            _gridArea.gameObject.SetActive(false);
            _listArea.gameObject.SetActive(false);
            _status.ShowMessage(message);
        }

        private void ClearItems()
        {
            foreach (GameObject item in _items)
            {
                if (item != null) UnityEngine.Object.Destroy(item);
            }

            _items.Clear();
            _playButtons.Clear();
        }

        private Tile AddTile(string slot, MaterialPreviewItem item)
        {
            var tile = new Tile(_grid, _category, slot, item);
            _items.Add(tile.Root.gameObject);
            return tile;
        }

        private void AddAudioRow(string slot, string detail, string key, Action play)
        {
            RectTransform row = ServerWidgets.ListItem(_list, "Audio-" + slot, AUDIO_HEIGHT);
            Image surface = LibraryWidgets.Panel("Surface", row, PackWidgets.ROW_RADIUS, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, PackWidgets.ROW_RADIUS);
            Button button = PackWidgets.SmallIconButton(row, "Play", LibraryIcon.Play, () => play(), 58f, UiCue.None);
            LibraryWidgets.Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(58f, 58f));
            GameObject playGlyph = button.transform.Find("Glyph").gameObject;
            Image stop = LibraryWidgets.Panel("Stop", button.transform, 3f, PackWidgets.Accent);
            LibraryWidgets.Place(stop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            stop.gameObject.SetActive(false);
            _playButtons.Add((key, playGlyph, stop.gameObject));

            MaterialSlotCatalog.SlotInfo info = MaterialSlotCatalog.Slot(MaterialCategories.AUDIO, slot);
            TextMeshProUGUI name = LibraryWidgets.Label("Name", row, info.Name, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(name.rectTransform, 92f, 220f, 14f, 30f);
            TextMeshProUGUI meta = PackWidgets.UserLabel("Meta", row, slot + "  ·  " + detail, 16f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(meta.rectTransform, 92f, 220f, 46f, 24f);
            if (MaterialPackAudio.UsesLoop(slot))
            {
                RectTransform tag = PackWidgets.Tag(row, "循环", HomeTheme.Colors.Violet, out _);
                LibraryWidgets.Place(tag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), tag.sizeDelta);
            }

            _items.Add(row.gameObject);
        }

        private void RefreshPlayButtons()
        {
            string playing = _screen.Media.PlayingKey;
            foreach ((string key, GameObject play, GameObject stop) in _playButtons)
            {
                if (play == null || stop == null) continue;
                bool active = key == playing;
                play.SetActive(!active);
                stop.SetActive(active);
            }
        }

        /// <summary>One image (or layout, or jacket layer) on the preview grid.</summary>
        private sealed class Tile
        {
            private readonly Image _picture;
            private readonly TextMeshProUGUI _state;

            internal Tile(RectTransform parent, string category, string slot, MaterialPreviewItem item)
            {
                Root = LibraryUi.CreateRect("Tile-" + slot, parent);
                Image surface = LibraryWidgets.Panel("Surface", Root, 18f, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(surface.rectTransform);
                LibraryWidgets.Outline(surface, 18f);

                Image well = LibraryWidgets.Panel("Well", Root, 14f, new Color32(0, 0, 0, 70));
                LibraryWidgets.TopStrip(well.rectTransform, 10f, 10f, 10f, 180f);
                Image checker = LibraryWidgets.Shape("Light", well.transform, HomeShapes.FadeUp(), new Color32(255, 255, 255, 10));
                checker.type = Image.Type.Simple;
                LibraryUi.Stretch(checker.rectTransform);
                _picture = LibraryUi.CreateImage("Picture", well.transform, Color.white);
                _picture.preserveAspect = true;
                _picture.raycastTarget = false;
                LibraryWidgets.Inset(_picture.rectTransform, 10f, 10f, 10f, 10f);
                _picture.enabled = false;
                _state = LibraryWidgets.Label("State", well.transform, string.Empty, 18f, LibraryTheme.Colors.TextMuted,
                    FontStyles.Normal, TextAlignmentOptions.Center);
                LibraryUi.Stretch(_state.rectTransform);

                string tag = null;
                switch (item?.Kind)
                {
                    case MaterialPreviewKind.Image:
                    case MaterialPreviewKind.Sequence:
                        _state.text = "加载中…";
                        if (item.Kind == MaterialPreviewKind.Sequence) tag = "序列 · " + item.FrameCount + " 帧";
                        break;
                    case MaterialPreviewKind.Layout:
                        _state.text = "布局参数\n位置与缩放";
                        tag = "布局";
                        break;
                    case MaterialPreviewKind.Jacket:
                        _state.text = "曲目封面\n（打歌时使用当前曲目的封面）";
                        tag = "封面图层";
                        break;
                }

                if (tag != null)
                {
                    RectTransform chip = PackWidgets.Tag(Root, tag, HomeTheme.Colors.Violet, out _, 30f);
                    LibraryWidgets.Place(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), chip.sizeDelta);
                }

                string name = slot.StartsWith("background.layers.", StringComparison.Ordinal)
                    ? "图层 " + (int.TryParse(slot.Substring("background.layers.".Length), out int index) ? index + 1 : 0)
                    : MaterialSlotCatalog.Slot(category, slot).Name;
                TextMeshProUGUI title = LibraryWidgets.Label("Name", Root, name, 20f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(title.rectTransform, 16f, 12f, 198f, 30f);
                TextMeshProUGUI key = PackWidgets.UserLabel("Key", Root, item?.File != null ? slot + " · " + item.File : slot, 14f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(key.rectTransform, 16f, 12f, 230f, 22f);
            }

            internal RectTransform Root { get; }

            internal void SetSprite(Sprite sprite)
            {
                if (sprite == null)
                {
                    SetFailed();
                    return;
                }

                _picture.sprite = sprite;
                _picture.enabled = true;
                _state.text = string.Empty;
            }

            internal void SetFailed()
            {
                _picture.enabled = false;
                _state.text = "无法预览";
                _state.color = LibraryTheme.Colors.Warning;
            }
        }
    }

    /// <summary>
    /// What a pack's preset sets in each of the nine categories, checked against the budgets before
    /// anything is saved, and the button that uses it (预设 mode with this preset).
    /// </summary>
    internal sealed class PackPresetPanel : IDisposable
    {
        private const float ROW_HEIGHT = 74f;

        private readonly PackScreen _screen;
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly TextMeshProUGUI _note;
        private readonly Button _apply;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private InstalledMaterialPack _pack;

        internal PackPresetPanel(PackScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("PackPreset", screen.ModalLayer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1240f, 900f), string.Empty, Close, withSubtitle: true);
            _modal.Title.richText = false;
            _modal.Subtitle.richText = false;
            float button = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            RectTransform list = LibraryUi.CreateRect("List", _modal.Body);
            LibraryWidgets.Inset(list, 0f, button + 70f, 0f, 0f);
            _content = ServerWidgets.VerticalList(list, out _scroll, 8f, new RectOffset(0, 0, 4, 4));
            _note = PackWidgets.Paragraph(_modal.Body, "Note", string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(_note.rectTransform, 0f, 340f, 0f, button + 56f);
            _note.alignment = TextAlignmentOptions.MidlineLeft;
            _apply = ServerWidgets.LabelledButton(_modal.Body, "Apply", "应用预设", LibraryIcon.Check, Apply, 300f, button,
                PackWidgets.Accent, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_apply.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(300f, button));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(InstalledMaterialPack pack)
        {
            if (pack?.Preset == null) return;
            _pack = pack;
            foreach (GameObject row in _rows) UnityEngine.Object.Destroy(row);
            _rows.Clear();
            _modal.Title.text = pack.Preset.Name;
            _modal.Subtitle.text = "随包预设  ·  来自 " + PackWidgets.PackName(pack) + "  " + PackWidgets.Revision(pack);

            MaterialPackConfiguration candidate = null;
            string error = null;
            try
            {
                candidate = _screen.Store.ResolvePreset(pack.PackId, pack.Digest);
            }
            catch (InvalidDataException e)
            {
                error = e.Message;
                try { candidate = MaterialPresets.Build(pack, _screen.State.Preset.GetBuiltinSets()); }
                catch (InvalidDataException) { }
            }

            MaterialPackConfiguration current = SafeActive(_screen.State);
            bool isCurrent = _screen.State.IsPresetMode ? _screen.State.Preset.IsFrom(pack.PackId, pack.Digest) :
                candidate?.SameChoicesAs(current) == true;
            foreach (string category in MaterialCategories.All)
            {
                AddRow(category, pack, candidate, current);
            }

            if (error != null)
            {
                _note.text = "无法使用：" + error + "\n当前选择保持不变。";
                _note.color = LibraryTheme.Colors.Danger;
                ServerWidgets.SetLabelledButton(_apply, "无法使用", LibraryIcon.Close, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextMuted);
            }
            else if (isCurrent)
            {
                _note.text = "当前配置已使用这个整合包。";
                _note.color = LibraryTheme.Colors.TextMuted;
                ServerWidgets.SetLabelledButton(_apply, "正在使用", LibraryIcon.Check, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary);
            }
            else
            {
                _note.text = "缺少的资源跟随游戏内置整合包。下一次开局生效。";
                _note.color = LibraryTheme.Colors.TextMuted;
                ServerWidgets.SetLabelledButton(_apply, _screen.State.IsPresetMode ? "使用整合包" : "用作混搭起点",
                    LibraryIcon.Check, PackWidgets.Accent, LibraryTheme.Colors.TextOnAccent);
            }

            _apply.interactable = error == null;
            _scroll.verticalNormalizedPosition = 1f;
            _modal.Open();
            _root.SetAsLastSibling();
        }

        internal void Close() => _modal.Close();

        public void Dispose()
        {
            _modal.Dispose();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        private void Apply()
        {
            if (_pack == null) return;
            if (_screen.UseBundle(MaterialPresetChoice.FromPack(_pack.PackId, _pack.Digest))) Close();
        }

        private static MaterialPackConfiguration SafeActive(MaterialPackState state)
        {
            try { return state.ActiveConfiguration(); }
            catch (InvalidDataException) { return MaterialPresets.BuildBuiltin(1); }
        }

        private void AddRow(string category, InstalledMaterialPack pack, MaterialPackConfiguration candidate, MaterialPackConfiguration current)
        {
            MaterialSlotCatalog.CategoryInfo info = MaterialSlotCatalog.Category(category);
            RectTransform row = ServerWidgets.ListItem(_content, "Row-" + category, ROW_HEIGHT);
            Image surface = LibraryWidgets.Panel("Surface", row, PackWidgets.ROW_RADIUS, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, PackWidgets.ROW_RADIUS);
            Image dot = LibraryWidgets.Panel("Dot", row, 5f, info.Tint);
            LibraryWidgets.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28f, 0f), new Vector2(10f, 10f));
            TextMeshProUGUI title = LibraryWidgets.Label("Title", row, info.Title, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(170f, 40f));

            bool declared = pack.Preset.Categories.TryGetValue(category, out MaterialPresetCategory value);
            string text = declared ? DescribeDeclared(category, pack, value, candidate) : "内置整合包 · " + MaterialSlotCatalog.BuiltinLabel(category, candidate?.Builtin);
            TextMeshProUGUI detail = PackWidgets.UserLabel("Detail", row, text, 19f,
                declared ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Inset(detail.rectTransform, 230f, 0f, 150f, 0f);

            bool changes = candidate == null || !candidate.Get(category).SameAs(current.Get(category))
                || MaterialCategories.HasBuiltinSets(category) && candidate.Builtin.Get(category) != current.Builtin.Get(category);
            RectTransform tag = PackWidgets.Tag(row, changes ? "与现在不同" : "与现在相同", changes ? PackWidgets.Accent : LibraryTheme.Colors.TextMuted, out _);
            LibraryWidgets.Place(tag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), tag.sizeDelta);
            _rows.Add(row.gameObject);
        }

        private static string DescribeDeclared(string category, InstalledMaterialPack pack, MaterialPresetCategory value, MaterialPackConfiguration candidate)
        {
            var parts = new List<string>();
            if (value.Groups.Count == 0) parts.Add("仅用内置");
            for (int i = 0; i < value.Groups.Count; i++)
            {
                MaterialPackGroup group = pack.Groups.Find(g => g.Id == value.Groups[i]);
                parts.Add((i == 0 ? "主组 " : "回退 ") + PackWidgets.GroupName(group));
            }

            if (MaterialCategories.HasBuiltinSets(category))
            {
                parts.Add("内置整合包 · " + MaterialSlotCatalog.BuiltinLabel(category, candidate?.Builtin));
            }

            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>
    /// Confirms removing one installed revision, listing what it takes out of 预设 and 混搭 before
    /// anything is deleted.
    /// </summary>
    internal sealed class PackUninstallPanel : IDisposable
    {
        private readonly PackScreen _screen;
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly TextMeshProUGUI _body;
        private InstalledMaterialPack _pack;

        internal PackUninstallPanel(PackScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("PackUninstall", screen.ModalLayer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(1000f, 700f), string.Empty, Close, withSubtitle: true);
            _modal.Title.richText = false;
            _modal.Subtitle.richText = false;
            float button = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _body = PackWidgets.Paragraph(_modal.Body, "Impact", string.Empty, 21f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.Inset(_body.rectTransform, 0f, button + 30f, 0f, 16f);

            Button cancel = ServerWidgets.LabelledButton(_modal.Body, "Cancel", "取消", LibraryIcon.Close, Close, 220f, button,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Close);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 0f), new Vector2(220f, button));
            Button confirm = ServerWidgets.LabelledButton(_modal.Body, "Confirm", "确认卸载", LibraryIcon.Trash, Confirm, 280f, button,
                LibraryTheme.Colors.Danger, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)confirm.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(280f, button));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(InstalledMaterialPack pack)
        {
            MaterialUninstallImpact impact;
            try
            {
                impact = _screen.Store.PreviewUninstall(pack.PackId, pack.Digest);
            }
            catch (InvalidDataException e)
            {
                UiSound.Play(UiCue.Error);
                _screen.ShowToast(e.Message);
                return;
            }

            _pack = pack;
            _modal.Title.text = "卸载 " + PackWidgets.PackName(pack);
            _modal.Subtitle.text = PackWidgets.Revision(pack) + "  ·  " + pack.PackId;
            var text = new StringBuilder();
            if (!impact.ChangesConfiguration)
            {
                text.AppendLine("预设与混搭都没有使用这个修订，卸载不会改变任何选择。");
            }
            else
            {
                if (impact.Categories.Count > 0)
                {
                    text.AppendLine("混搭中受影响的类别：" + string.Join("、", impact.Categories.Select(c => MaterialSlotCatalog.Category(c).Title)));
                    text.AppendLine("这些类别会移除此修订的资源组，由剩下的回退或本类内置补上。");
                }

                if (impact.Overrides.Count > 0)
                {
                    var names = impact.Overrides.Take(6).Select(o => MaterialSlotCatalog.Slot(o.Category, o.Slot).Name).ToList();
                    text.AppendLine("将移除 " + impact.Overrides.Count + " 项单独指定：" + string.Join("、", names)
                        + (impact.Overrides.Count > names.Count ? " 等" : string.Empty) + "。");
                }

                if (impact.ClearedPreset != null)
                    text.AppendLine("选中的预设「" + impact.ClearedPreset + "」来自这个修订，卸载后预设改为「" + PackScreen.BUILTIN_BUNDLE_NAME + "」"
                        + (_screen.State?.IsPresetMode == true ? "，下一次开局使用游戏内置资源。" : "。"));
            }

            if (_screen.State?.Installed.Count(p => p.PackId == pack.PackId) > 1)
                text.AppendLine("同一资源包的其他修订保留，不受影响。");
            text.AppendLine();
            text.Append("卸载会删除这个修订的文件，之后可以重新导入。");
            _body.text = text.ToString();
            _modal.Open(UiCue.Warning);
            _root.SetAsLastSibling();
        }

        internal void Close() => _modal.Close();

        public void Dispose()
        {
            _modal.Dispose();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        private void Confirm()
        {
            InstalledMaterialPack pack = _pack;
            Close();
            _screen.Uninstall(pack);
        }
    }
}
