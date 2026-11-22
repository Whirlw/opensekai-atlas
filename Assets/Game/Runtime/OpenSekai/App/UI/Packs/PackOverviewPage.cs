using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// The first page of the 资源包 part. The mode card on the left says what the next live uses
    /// and switches between the two modes. Both modes list the bundles below the card. The right
    /// side plays a small live in preset mode and opens each of the nine categories in mix mode.
    /// </summary>
    internal sealed class PackOverviewPage : PackPage
    {
        private const float LEFT_WIDTH = 600f;
        private const float COLUMN_GAP = 32f;
        private const float MODE_HEIGHT = 316f;
        private const float ROW_HEIGHT = 116f;
        private const float GRID_GAP = 22f;

        private readonly TextMeshProUGUI _subtitle;
        private readonly ModeCard _mode;
        private readonly TextMeshProUGUI _listCaption;
        private readonly TextMeshProUGUI _listCount;
        private readonly RectTransform _list;
        private readonly ScrollRect _listScroll;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<CategoryTile> _tiles = new List<CategoryTile>();
        private readonly RectTransform _grid;
        private readonly PackDemoView _demo;
        private readonly TextMeshProUGUI _hint;
        private readonly RectTransform _body;
        private readonly ServerStatusView _status;
        private readonly CancellationTokenSource _icons = new CancellationTokenSource();
        private MotionHandle _enter;
        private bool? _listedPresets;

        internal PackOverviewPage(PackScreen screen) : base(screen, "PackOverview")
        {
            RectTransform bar = ServerWidgets.TopBar(Root, "资源包", screen.Back, screen.OpenSettings, out _, out _subtitle, out float rightInset);
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            const float IMPORT_WIDTH = 220f;
            Button import = ServerWidgets.LabelledButton(bar, "Import", "导入 ZIP", LibraryIcon.Add, screen.Import, IMPORT_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)import.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-rightInset, 0f),
                new Vector2(IMPORT_WIDTH, height));

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            _body = LibraryUi.CreateRect("Body", Root);
            LibraryWidgets.Inset(_body, margin, 76f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            // Left column: the mode, then the presets or the installed packs.
            RectTransform left = LibraryUi.CreateRect("Left", _body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _mode = new ModeCard(left, screen);
            LibraryWidgets.TopStrip(_mode.Root, 0f, 0f, 0f, MODE_HEIGHT);

            _listCaption = PackWidgets.Caption(left, string.Empty, 0f, 160f, MODE_HEIGHT + 14f);
            _listCount = LibraryWidgets.Label("Count", left, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.BottomRight);
            LibraryWidgets.TopStrip(_listCount.rectTransform, 300f, 8f, MODE_HEIGHT + 14f, LibraryTheme.Layout.SECTION_CAPTION_HEIGHT - 10f);
            RectTransform listArea = LibraryUi.CreateRect("List", left);
            LibraryWidgets.Inset(listArea, 0f, 0f, 0f, MODE_HEIGHT + 14f + LibraryTheme.Layout.SECTION_CAPTION_HEIGHT);
            _list = ServerWidgets.VerticalList(listArea, out _listScroll, 10f, new RectOffset(0, 0, 0, 12));

            // Right column: 混搭's nine categories, three by three in fractions of the area so the
            // grid follows the screen's shape; or 预设's demo.
            RectTransform right = LibraryUi.CreateRect("Right", _body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _grid = LibraryUi.CreateRect("Categories", right);
            LibraryUi.Stretch(_grid);
            IReadOnlyList<string> categories = MaterialCategories.All;
            for (int i = 0; i < categories.Count; i++)
            {
                var tile = new CategoryTile(_grid, categories[i], screen.OpenCategory);
                int row = i / 3;
                int column = i % 3;
                RectTransform rect = tile.Root;
                rect.anchorMin = new Vector2(column / 3f, 1f - (row + 1) / 3f);
                rect.anchorMax = new Vector2((column + 1) / 3f, 1f - row / 3f);
                rect.offsetMin = new Vector2(column == 0 ? 0f : GRID_GAP * 0.5f, row == 2 ? 0f : GRID_GAP * 0.5f);
                rect.offsetMax = new Vector2(column == 2 ? 0f : -GRID_GAP * 0.5f, row == 0 ? 0f : -GRID_GAP * 0.5f);
                _tiles.Add(tile);
            }

            _demo = new PackDemoView(right, screen.Demo, HomeTheme.Colors.Rose);
            LibraryUi.Stretch(_demo.Root);

            _hint = LibraryWidgets.Label("Hint", Root, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryWidgets.BottomStrip(_hint.rectTransform, margin, margin, 22f, 30f);

            RectTransform statusArea = LibraryUi.CreateRect("Status", Root);
            LibraryWidgets.Inset(statusArea, margin, 76f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            _status = new ServerStatusView(statusArea);
            Refresh();
        }

        /// <summary>预设 plays the chosen preset; 混搭 shows its categories instead.</summary>
        internal override MaterialPackConfiguration DemoConfiguration
        {
            get
            {
                MaterialPackState state = Screen.State;
                if (state == null || !state.IsPresetMode) return null;
                try { return state.ActiveConfiguration(); }
                catch (InvalidDataException) { return null; }
            }
        }

        internal override void OnShown() => Refresh();

        internal override void OnStateChanged() => Refresh();

        internal override void PlayEnter()
        {
            _enter.TryCancel();
            var cards = new List<HomeCardFrame> { _mode.Frame };
            if (Screen.State?.IsPresetMode == true) cards.Add(_demo.Frame);
            else cards.AddRange(_tiles.Select(t => t.Frame));
            _enter = LMotion.Create(0f, 1f, 0.7f)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => cards.ForEach(card => Rise(card, 1f)))
                .WithOnCancel(() => cards.ForEach(card => Rise(card, 1f)))
                .Bind(time =>
                {
                    for (int i = 0; i < cards.Count; i++)
                    {
                        Rise(cards[i], HomeMotion.Span(time, 0.04f + i * 0.035f, 0.42f, Ease.OutCubic));
                    }
                });
        }

        protected override void OnDispose()
        {
            _enter.TryCancel();
            _icons.Cancel();
            _icons.Dispose();
            _status.Dispose();
            _demo.Dispose();
        }

        private static void Rise(HomeCardFrame card, float progress)
        {
            card.Group.alpha = progress;
            card.Root.anchoredPosition = new Vector2(0f, -HomeTheme.Motion.ENTER_RISE * 0.6f * (1f - progress));
        }

        private void Refresh()
        {
            MaterialPackState state = Screen.State;
            if (state == null)
            {
                _body.gameObject.SetActive(false);
                _subtitle.text = string.Empty;
                _hint.text = string.Empty;
                _status.ShowError(Screen.LoadError ?? "无法读取资源包。", () =>
                {
                    Screen.Reload();
                    Refresh();
                });
                return;
            }

            _status.Hide();
            _body.gameObject.SetActive(true);
            bool preset = state.IsPresetMode;
            MaterialPackConfiguration active = SafeActive(state);
            int external = MaterialCategories.All.Count(c => active.Get(c).Groups.Count > 0);
            _subtitle.text = (preset ? "当前：预设「" + Screen.ActiveName + "」" : "当前：混搭")
                + "  ·  " + external + " / 9 类使用资源包  ·  下一次开局生效";
            _hint.text = preset
                ? "点整合包即可使用。点内置整合包右侧的箭头，可选择各模块使用的套装。"
                : "点左侧整合包载入整套配置，再点右侧类别调整资源组和回退顺序。";
            _mode.Refresh(state, active, external);
            _grid.gameObject.SetActive(!preset);
            _demo.Root.gameObject.SetActive(preset);
            if (preset)
            {
                _demo.SetText(Screen.ActiveName, Composition(active));
                _demo.Refresh();
            }
            else
            {
                foreach (CategoryTile tile in _tiles) tile.Refresh(Screen, state);
            }

            RebuildBundles(state, active);
            if (_listedPresets != preset) _listScroll.verticalNormalizedPosition = 1f;
            _listedPresets = preset;
        }

        private static MaterialPackConfiguration SafeActive(MaterialPackState state)
        {
            try { return state.ActiveConfiguration(); }
            catch (InvalidDataException) { return MaterialPresets.BuildBuiltin(1); }
        }

        /// <summary>Under the demo: which categories the preset takes from packs, and the built-in sets.</summary>
        private static string Composition(MaterialPackConfiguration configuration)
        {
            var external = MaterialCategories.All.Where(c => configuration.Get(c).Groups.Count > 0)
                .Select(c => MaterialSlotCatalog.Category(c).Title).ToList();
            string sets = string.Join(" / ", MaterialCategories.All.Where(MaterialCategories.HasBuiltinSets)
                .Select(c => MaterialSlotCatalog.BuiltinLabel(c, configuration.Builtin)));
            string from = external.Count == 0 ? "全部使用游戏内置资源" : external.Count == 9
                ? "九类都来自资源包" : "来自资源包：" + string.Join("、", external) + "，其余使用游戏内置";
            return from + "  ·  内置套装：" + sets;
        }

        private void ClearRows()
        {
            foreach (GameObject row in _rows) Object.Destroy(row);
            _rows.Clear();
        }

        private void RebuildBundles(MaterialPackState state, MaterialPackConfiguration active)
        {
            ClearRows();
            _listCaption.text = "选择整合包";
            var builtin = MaterialPresetChoice.Builtin(state.Preset.GetBuiltinSets());
            AddBundle(builtin, null, state.IsPresetMode ? state.Preset.IsBuiltin : active.SameChoicesAs(state.BundleConfiguration(builtin)));

            int count = 1;
            foreach (InstalledMaterialPack pack in state.Installed.Where(p => p.Preset != null))
            {
                var choice = MaterialPresetChoice.FromPack(pack.PackId, pack.Digest);
                bool selected = state.IsPresetMode ? state.Preset.SameAs(choice) : active.SameChoicesAs(state.BundleConfiguration(choice));
                AddBundle(choice, pack, selected);
                count++;
            }
            _listCount.text = count + " 个整合包";

            if (state.IsPresetMode) return;
            var standalone = state.Installed.Where(p => p.Preset == null).ToList();
            if (standalone.Count == 0) return;
            RectTransform heading = ServerWidgets.ListItem(_list, "OtherPacks", LibraryTheme.Layout.SECTION_CAPTION_HEIGHT);
            PackWidgets.Caption(heading, "其他资源包", 0f, 0f, 0f);
            _rows.Add(heading.gameObject);
            var used = new HashSet<string>(MaterialCategories.All.SelectMany(c => active.Get(c).Groups));
            foreach (InstalledMaterialPack pack in standalone)
            {
                var row = new PackRow(_list, pack, pack.Groups.Any(g => used.Contains(pack.GroupReference(g.Id))), () => Screen.OpenPack(pack));
                _rows.Add(row.Root.gameObject);
                LoadIcon(row.Icon, pack).Forget();
            }
        }

        private void AddBundle(MaterialPresetChoice choice, InstalledMaterialPack pack, bool selected)
        {
            RectTransform row = ServerWidgets.ListItem(_list, "Bundle-" + (pack == null ? "builtin" : pack.PackId + "-" + pack.ShortDigest), ROW_HEIGHT);
            Image surface = LibraryWidgets.Panel("Surface", row, 24f, selected ? HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 34) : HomeTheme.Colors.Card);
            surface.raycastTarget = true;
            LibraryUi.Stretch(surface.rectTransform);
            Image outline = LibraryWidgets.Shape("Outline", row, LibraryShapes.Ring(24f, selected ? 2f : 1f),
                selected ? HomeTheme.Colors.Rose : LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
            outline.raycastTarget = false;
            PackWidgets.RowButton(surface.rectTransform, () => Screen.UseBundle(choice), UiCue.None, row);

            const float ICON = 80f;
            if (pack != null)
            {
                var icon = new PackIconView(row, ICON);
                LibraryWidgets.Place(icon.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(ICON, ICON));
                LoadIcon(icon, pack).Forget();
            }
            else
            {
                Image badge = LibraryWidgets.Panel("Badge", row, 22f, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 38));
                LibraryWidgets.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(ICON, ICON));
                LibraryWidgets.Icon(badge.transform, LibraryIcon.Bundle, HomeTheme.Colors.Rose, 50f);
            }

            Button info = PackWidgets.SmallIconButton(row, "Info", LibraryIcon.ChevronRight, () => Screen.OpenPack(pack), 52f, UiCue.None);
            LibraryWidgets.Place((RectTransform)info.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(52f, 52f));
            float right = 80f;

            if (selected)
            {
                RectTransform tag = PackWidgets.Tag(row, "使用中", HomeTheme.Colors.Rose, out float width, 32f, filled: true);
                LibraryWidgets.Place(tag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-right, -18f), tag.sizeDelta);
                right += width + 12f;
            }

            string title = pack == null ? PackScreen.BUILTIN_BUNDLE_NAME : PackWidgets.PackName(pack);
            TextMeshProUGUI name = PackWidgets.UserLabel("Name", row, title, 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(name.rectTransform, 116f, right, 14f, 34f);
            string meta = pack == null ? "游戏自带资源" : pack.Preset.Name + "  " + PackWidgets.Revision(pack);
            TextMeshProUGUI metaLabel = PackWidgets.UserLabel("Meta", row, meta, 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(metaLabel.rectTransform, 116f, 80f, 50f, 24f);
            string holds;
            if (pack == null)
            {
                var sets = choice.GetBuiltinSets();
                holds = string.Join(" · ", MaterialCategories.All.Where(MaterialCategories.HasBuiltinSets)
                    .Select(c => MaterialSlotCatalog.BuiltinLabel(c, sets)));
            }
            else
            {
                var categories = MaterialCategories.All
                    .Where(c => pack.Preset.Categories.TryGetValue(c, out MaterialPresetCategory value) && value.Groups.Count > 0)
                    .Select(c => MaterialSlotCatalog.Category(c).Title).ToList();
                holds = categories.Count == 9 ? "九类都由资源包提供"
                    : categories.Count == 0 ? "使用游戏内置资源" : string.Join(" · ", categories);
            }

            TextMeshProUGUI summary = PackWidgets.UserLabel("Holds", row, holds, 17f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(summary.rectTransform, 116f, 80f, 78f, 24f);
            _rows.Add(row.gameObject);
        }

        private async UniTaskVoid LoadIcon(PackIconView icon, InstalledMaterialPack pack)
        {
            Sprite sprite = await Screen.Media.LoadIconAsync(Screen.Store, pack, _icons.Token);
            if (!IsDisposed && icon.Root != null) icon.SetSprite(sprite);
        }

        // ------------------------------------------------------------------ parts

        /// <summary>
        /// What the next live uses: 预设 (with the preset's name) or 混搭, a light per category that
        /// takes resources from a pack, and the switch between the two. Tapping the card switches.
        /// </summary>
        private sealed class ModeCard
        {
            private const float SWITCH_WIDTH = 232f;
            private const float SWITCH_HEIGHT = 52f;

            private readonly PackScreen _screen;
            private readonly TextMeshProUGUI _eyebrow;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _source;
            private readonly TextMeshProUGUI _external;
            private readonly RectTransform _mixIcon;
            private readonly Image _presetIcon;
            private readonly Image[] _lights = new Image[9];
            private readonly Image[] _segments = new Image[2];
            private readonly TextMeshProUGUI[] _segmentLabels = new TextMeshProUGUI[2];

            internal ModeCard(RectTransform parent, PackScreen screen)
            {
                _screen = screen;
                Frame = new HomeCardFrame(parent, "Mode", HomeTheme.Layout.CARD_RADIUS, OnClick, 0.985f);
                RectTransform content = Frame.Content;
                Image glow = LibraryUi.CreateImage("Glow", content, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 46));
                glow.sprite = LibraryShapes.Glow();
                glow.raycastTarget = false;
                LibraryWidgets.Place(glow.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -100f), new Vector2(620f, 620f));

                float pad = HomeTheme.Layout.CARD_PADDING;
                _eyebrow = PackWidgets.Eyebrow(content, "PRESET", HomeTheme.Colors.Rose, 20f, 26f);
                LibraryWidgets.TopStrip(_eyebrow.rectTransform, pad, 200f, pad - 4f, 28f);
                _name = PackWidgets.UserLabel("Name", content, string.Empty, 52f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_name.rectTransform, pad - 2f, 200f, pad + 28f, 70f);
                _source = PackWidgets.UserLabel("Source", content, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
                LibraryWidgets.TopStrip(_source.rectTransform, pad, 200f, pad + 102f, 30f);

                _external = LibraryWidgets.Label("External", content, "0", 44f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_external);
                _external.richText = true;
                LibraryWidgets.Place(_external.rectTransform, Vector2.zero, Vector2.zero, new Vector2(pad, pad + 12f), new Vector2(170f, 54f));
                TextMeshProUGUI caption = LibraryWidgets.Label("ExternalCaption", content, "类使用资源包", 17f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.Place(caption.rectTransform, Vector2.zero, Vector2.zero, new Vector2(pad, pad - 14f), new Vector2(180f, 26f));

                // The switch: the current mode filled, the other one a tap away.
                Image track = LibraryWidgets.Panel("Switch", content, SWITCH_HEIGHT * 0.5f, HomeTheme.Colors.Chip);
                track.raycastTarget = false;
                LibraryWidgets.Place(track.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad, pad - 10f),
                    new Vector2(SWITCH_WIDTH, SWITCH_HEIGHT));
                string[] labels = { "预设", "混搭" };
                for (int i = 0; i < 2; i++)
                {
                    Image segment = LibraryWidgets.Panel("Segment" + i, track.transform, (SWITCH_HEIGHT - 8f) * 0.5f, HomeTheme.Colors.Rose);
                    segment.raycastTarget = false;
                    segment.rectTransform.anchorMin = new Vector2(i * 0.5f, 0f);
                    segment.rectTransform.anchorMax = new Vector2((i + 1) * 0.5f, 1f);
                    segment.rectTransform.offsetMin = new Vector2(4f, 4f);
                    segment.rectTransform.offsetMax = new Vector2(-4f, -4f);
                    TextMeshProUGUI label = LibraryWidgets.Label("Label", segment.transform, labels[i], 20f, LibraryTheme.Colors.TextOnAccent,
                        FontStyles.Bold, TextAlignmentOptions.Center);
                    LibraryUi.Stretch(label.rectTransform);
                    _segments[i] = segment;
                    _segmentLabels[i] = label;
                }

                // Lights in the same order as the category grid.
                Vector2 iconOffset = new Vector2(-pad, -pad - 6f);
                var iconSize = new Vector2(120f, 120f);
                _presetIcon = LibraryWidgets.Icon(content, LibraryIcon.Bundle, HomeTheme.Colors.Rose, iconSize.x);
                LibraryWidgets.Place(_presetIcon.rectTransform, Vector2.one, Vector2.one, iconOffset, iconSize);
                _mixIcon = LibraryUi.CreateRect("Lights", content);
                LibraryWidgets.Place(_mixIcon, Vector2.one, Vector2.one, iconOffset, iconSize);
                for (int i = 0; i < 9; i++)
                {
                    Image light = LibraryWidgets.Panel("Light" + i, _mixIcon, 9f, LibraryTheme.Colors.Control);
                    LibraryWidgets.Place(light.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(i % 3 * 42f, -(i / 3) * 42f), new Vector2(34f, 34f));
                    _lights[i] = light;
                }
            }

            internal HomeCardFrame Frame { get; }
            internal RectTransform Root => Frame.Root;

            internal void Refresh(MaterialPackState state, MaterialPackConfiguration active, int external)
            {
                bool preset = state.IsPresetMode;
                _mixIcon.gameObject.SetActive(!preset);
                _presetIcon.gameObject.SetActive(preset);
                _eyebrow.text = preset ? "PRESET" : "MIX";
                _name.text = preset ? "预设" : "混搭";
                InstalledMaterialPack source = preset ? state.PresetPack : null;
                _source.text = preset
                    ? "「" + _screen.PresetName(state.Preset) + "」" + (source != null ? "  ·  " + PackWidgets.PackName(source) : string.Empty)
                    : "按类别选择资源组与回退顺序";
                _external.text = external + "<size=60%><color=#9AA6B5> / 9</color></size>";
                for (int i = 0; i < MaterialCategories.All.Count; i++)
                {
                    string category = MaterialCategories.All[i];
                    _lights[i].color = active.Get(category).Groups.Count > 0 ? MaterialSlotCatalog.Category(category).Tint : LibraryTheme.Colors.Control;
                }

                for (int i = 0; i < 2; i++)
                {
                    bool on = (i == 0) == preset;
                    _segments[i].color = on ? (Color)HomeTheme.Colors.Rose : Color.clear;
                    _segmentLabels[i].color = on ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
                }
            }

            private void OnClick()
            {
                MaterialPackState state = _screen.State;
                if (state == null) return;
                _screen.SetMode(state.IsPresetMode ? MaterialPackModes.MIX : MaterialPackModes.PRESET);
            }
        }

        /// <summary>One installed revision: icon, name, version, ID and digest, what it holds.</summary>
        private sealed class PackRow
        {
            internal PackRow(RectTransform list, InstalledMaterialPack pack, bool inUse, System.Action open)
            {
                Root = ServerWidgets.ListItem(list, "Pack-" + pack.PackId + "-" + pack.ShortDigest, ROW_HEIGHT);
                Image surface = LibraryWidgets.Panel("Surface", Root, 24f, HomeTheme.Colors.Card);
                surface.raycastTarget = true;
                LibraryUi.Stretch(surface.rectTransform);
                LibraryWidgets.Outline(surface, 24f);
                PackWidgets.RowButton(surface.rectTransform, () => open(), UiCue.None, Root);

                Icon = new PackIconView(Root, 80f);
                LibraryWidgets.Place(Icon.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(80f, 80f));

                float right = 60f;
                Image chevron = LibraryWidgets.Icon(Root, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 24f);
                LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(24f, 24f));
                if (inUse)
                {
                    RectTransform tag = PackWidgets.Tag(Root, "混搭使用中", HomeTheme.Colors.Periwinkle, out float width, 32f);
                    LibraryWidgets.Place(tag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -18f), tag.sizeDelta);
                    right = width + 70f;
                }

                TextMeshProUGUI name = PackWidgets.UserLabel("Name", Root, PackWidgets.PackName(pack), 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(name.rectTransform, 116f, right, 14f, 34f);
                string id = string.IsNullOrWhiteSpace(pack.DisplayName) ? string.Empty : "  ·  " + pack.PackId;
                TextMeshProUGUI meta = PackWidgets.UserLabel("Meta", Root, PackWidgets.Revision(pack) + id, 17f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(meta.rectTransform, 116f, 60f, 50f, 24f);
                string holds = string.Join("  ·  ", pack.Groups.GroupBy(g => g.Category)
                    .OrderBy(g => IndexOf(g.Key))
                    .Select(g => MaterialSlotCatalog.Category(g.Key).Title + (g.Count() > 1 ? " ×" + g.Count() : string.Empty)));
                TextMeshProUGUI summary = PackWidgets.UserLabel("Holds", Root, holds, 17f, LibraryTheme.Colors.TextSecondary);
                LibraryWidgets.TopStrip(summary.rectTransform, 116f, 60f, 78f, 24f);
            }

            internal RectTransform Root { get; }
            internal PackIconView Icon { get; }

            private static int IndexOf(string category)
            {
                for (int i = 0; i < MaterialCategories.All.Count; i++)
                    if (MaterialCategories.All[i] == category) return i;
                return int.MaxValue;
            }
        }

        /// <summary>One category: its name, where its resources come from in 混搭, and a little drawing.</summary>
        private sealed class CategoryTile
        {
            private const float ART = 128f;
            private const float ART_SCALE = 0.74f;

            private readonly string _category;
            private readonly TextMeshProUGUI _source;
            private readonly TextMeshProUGUI _detail;
            private readonly RectTransform _tag;

            internal CategoryTile(RectTransform parent, string category, System.Action<string> open)
            {
                _category = category;
                MaterialSlotCatalog.CategoryInfo info = MaterialSlotCatalog.Category(category);
                Frame = new HomeCardFrame(parent, "Category-" + category, 26f, () => open(category), 0.975f);
                RectTransform content = Frame.Content;

                RectTransform art = LibraryUi.CreateRect("Art", content);
                float extent = ART * ART_SCALE * 0.5f;
                LibraryWidgets.Place(art, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-22f - extent, -14f - extent), new Vector2(ART, ART));
                art.localScale = new Vector3(ART_SCALE, ART_SCALE, 1f);
                PackWidgets.CategoryGlyph(art, category, info.Tint);

                const float PAD = 30f;
                TextMeshProUGUI eyebrow = PackWidgets.Eyebrow(content, info.Eyebrow, info.Tint, 18f, 20f);
                LibraryWidgets.TopStrip(eyebrow.rectTransform, PAD, ART * ART_SCALE + 30f, PAD - 4f, 26f);
                TextMeshProUGUI title = LibraryWidgets.Label("Title", content, info.Title, 40f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(title.rectTransform, PAD - 1f, ART * ART_SCALE + 24f, PAD + 24f, 54f);

                _source = PackWidgets.UserLabel("Source", content, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
                _source.rectTransform.anchorMin = Vector2.zero;
                _source.rectTransform.anchorMax = new Vector2(1f, 0f);
                _source.rectTransform.pivot = new Vector2(0.5f, 0f);
                _source.rectTransform.offsetMin = new Vector2(PAD, 92f);
                _source.rectTransform.offsetMax = new Vector2(-PAD, 122f);
                _detail = PackWidgets.UserLabel("Detail", content, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
                _detail.rectTransform.anchorMin = Vector2.zero;
                _detail.rectTransform.anchorMax = new Vector2(1f, 0f);
                _detail.rectTransform.pivot = new Vector2(0.5f, 0f);
                _detail.rectTransform.offsetMin = new Vector2(PAD, 64f);
                _detail.rectTransform.offsetMax = new Vector2(-PAD, 90f);

                _tag = PackWidgets.Tag(content, "内置", LibraryTheme.Colors.TextMuted, out _, 34f);
                LibraryWidgets.Place(_tag, Vector2.zero, Vector2.zero, new Vector2(PAD, 22f), _tag.sizeDelta);
                Image chevron = LibraryWidgets.Icon(content, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 24f);
                LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-PAD + 4f, 28f), new Vector2(24f, 24f));
            }

            internal HomeCardFrame Frame { get; }
            internal RectTransform Root => Frame.Root;

            internal void Refresh(PackScreen screen, MaterialPackState state)
            {
                MaterialGroupSelection selection = state.Selection(_category);
                string builtin = MaterialSlotCatalog.BuiltinLabel(_category, state.Builtin);
                bool hasBuiltinSets = MaterialCategories.HasBuiltinSets(_category);
                if (selection.Groups.Count > 0)
                {
                    _source.text = screen.DescribeGroup(selection.Groups[0]);
                    var parts = new List<string>();
                    if (selection.Groups.Count > 1) parts.Add("另有 " + (selection.Groups.Count - 1) + " 个回退");
                    parts.Add("缺项用" + (hasBuiltinSets ? "内置" + builtin : "游戏默认"));
                    _detail.text = string.Join("  ·  ", parts);
                    PackWidgets.SetTag(_tag, "资源包", MaterialSlotCatalog.Category(_category).Tint, filled: true);
                }
                else
                {
                    _source.text = hasBuiltinSets ? "内置 · " + builtin : "游戏默认";
                    _detail.text = "未选用资源包";
                    PackWidgets.SetTag(_tag, "内置", LibraryTheme.Colors.TextMuted);
                }
            }
        }
    }
}
