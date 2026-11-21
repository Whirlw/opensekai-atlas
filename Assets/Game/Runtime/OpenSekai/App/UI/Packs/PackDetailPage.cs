using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One installed revision: its name, icon, version, ID and digest; its preset, if it ships
    /// one, with a way to review and apply it; every group it provides, grouped by category, with
    /// a preview and where each is used now; and uninstalling it.
    /// </summary>
    internal sealed class PackDetailPage : PackPage
    {
        private const float LEFT_WIDTH = 600f;
        private const float COLUMN_GAP = 32f;
        private const float HERO_HEIGHT = 320f;
        private const float ICON_SIZE = 124f;
        private const float GROUP_ROW = 104f;

        private readonly string _packId;
        private readonly string _digest;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _subtitle;
        private readonly PackIconView _icon;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _meta;
        private readonly TextMeshProUGUI _counts;
        private readonly RectTransform _presetPanel;
        private readonly TextMeshProUGUI _presetName;
        private readonly TextMeshProUGUI _presetText;
        private readonly RectTransform _presetTag;
        private readonly Button _presetButton;
        private readonly TextMeshProUGUI _legacyHint;
        private readonly RectTransform _groups;
        private readonly ScrollRect _groupScroll;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly PackPresetPanel _presetCard;
        private readonly PackUninstallPanel _uninstallCard;
        private readonly CancellationTokenSource _iconLoad = new CancellationTokenSource();
        private InstalledMaterialPack _pack;

        internal PackDetailPage(PackScreen screen, string packId, string digest) : base(screen, "PackDetail")
        {
            _packId = packId;
            _digest = digest;
            RectTransform bar = ServerWidgets.TopBar(Root, string.Empty, screen.Back, screen.OpenSettings, out _title, out _subtitle, out _);
            _title.richText = false;
            _subtitle.richText = false;

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", Root);
            LibraryWidgets.Inset(body, margin, 40f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);

            // The pack itself.
            var hero = new HomeCardFrame(left, "Hero", HomeTheme.Layout.CARD_RADIUS, null, 1f);
            LibraryWidgets.TopStrip(hero.Root, 0f, 0f, 0f, HERO_HEIGHT);
            Image glow = LibraryUi.CreateImage("Glow", hero.Content, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 40));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, -110f), new Vector2(560f, 560f));
            float pad = HomeTheme.Layout.CARD_PADDING;
            _icon = new PackIconView(hero.Content, ICON_SIZE);
            LibraryWidgets.Place(_icon.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(ICON_SIZE, ICON_SIZE));
            float textLeft = pad + ICON_SIZE + 24f;
            TextMeshProUGUI eyebrow = PackWidgets.Eyebrow(hero.Content, "RESOURCE PACK", HomeTheme.Colors.Rose, 18f, 22f);
            LibraryWidgets.TopStrip(eyebrow.rectTransform, textLeft, pad, pad, 26f);
            // IDs have no spaces to wrap at, so a long name shrinks to fit its two lines instead.
            _name = PackWidgets.UserLabel("Name", hero.Content, string.Empty, 38f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _name.textWrappingMode = TextWrappingModes.Normal;
            _name.alignment = TextAlignmentOptions.TopLeft;
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 22f;
            _name.fontSizeMax = 38f;
            LibraryWidgets.TopStrip(_name.rectTransform, textLeft, pad, pad + 28f, ICON_SIZE - 28f);
            _meta = PackWidgets.UserLabel("Meta", hero.Content, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            _meta.textWrappingMode = TextWrappingModes.Normal;
            _meta.alignment = TextAlignmentOptions.TopLeft;
            _meta.lineSpacing = 6f;
            LibraryWidgets.TopStrip(_meta.rectTransform, pad, pad, pad + ICON_SIZE + 18f, 56f);
            _counts = LibraryWidgets.Label("Counts", hero.Content, string.Empty, 18f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(_counts.rectTransform, pad, pad, pad + ICON_SIZE + 80f, 28f);

            // Its preset.
            PackWidgets.Caption(left, "随包预设", 0f, 0f, HERO_HEIGHT + 10f);
            Image preset = PackWidgets.Group(left, "Preset");
            _presetPanel = preset.rectTransform;
            LibraryWidgets.TopStrip(_presetPanel, 0f, 0f, HERO_HEIGHT + 10f + LibraryTheme.Layout.SECTION_CAPTION_HEIGHT, 220f);
            _presetName = PackWidgets.UserLabel("Name", _presetPanel, string.Empty, 28f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_presetName.rectTransform, 26f, 160f, 20f, 40f);
            _presetTag = PackWidgets.Tag(_presetPanel, "正在使用", HomeTheme.Colors.Rose, out _, 32f, filled: true);
            LibraryWidgets.Place(_presetTag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -24f), _presetTag.sizeDelta);
            _presetText = PackWidgets.Paragraph(_presetPanel, "Text", string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(_presetText.rectTransform, 26f, 26f, 66f, 62f);
            _presetButton = ServerWidgets.LabelledButton(_presetPanel, "Review", "查看预设", LibraryIcon.Forward, OpenPreset, 240f, 60f,
                PackWidgets.Accent, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_presetButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(240f, 60f));

            _legacyHint = PackWidgets.Paragraph(left, "Legacy",
                "此修订安装于旧版本，暂未读取包名、图标与预设。重新导入同一 ZIP 即可显示。", 17f, LibraryTheme.Colors.Warning);
            LibraryWidgets.TopStrip(_legacyHint.rectTransform, 8f, 0f, HERO_HEIGHT + 286f, 50f);

            Button uninstall = ServerWidgets.LabelledButton(left, "Uninstall", "卸载此修订", LibraryIcon.Trash, OpenUninstall, 260f, 64f,
                LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger, UiCue.None);
            LibraryWidgets.Place((RectTransform)uninstall.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(260f, 64f));

            // Its groups.
            RectTransform right = LibraryUi.CreateRect("Groups", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            PackWidgets.Caption(right, "资源组", 0f, 0f, 0f);
            RectTransform listArea = LibraryUi.CreateRect("List", right);
            LibraryWidgets.Inset(listArea, -6f, 0f, -6f, LibraryTheme.Layout.SECTION_CAPTION_HEIGHT);
            _groups = ServerWidgets.VerticalList(listArea, out _groupScroll, 10f, new RectOffset(6, 6, 0, 16));

            _presetCard = new PackPresetPanel(screen);
            _uninstallCard = new PackUninstallPanel(screen);
            Refresh();
        }

        internal override void OnShown() => Refresh();

        internal override void OnStateChanged() => Refresh();

        internal override bool TryHandleBack()
        {
            if (_presetCard.IsOpen)
            {
                _presetCard.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_uninstallCard.IsOpen)
            {
                _uninstallCard.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            return false;
        }

        protected override void OnDispose()
        {
            _iconLoad.Cancel();
            _iconLoad.Dispose();
            _presetCard.Dispose();
            _uninstallCard.Dispose();
        }

        private void Refresh()
        {
            MaterialPackState state = Screen.State;
            _pack = state?.FindPack(_packId, _digest);
            if (_pack == null)
            {
                // Uninstalled (here or by a reload): nothing left to show.
                if (Screen.Top == this) Screen.Pop();
                return;
            }

            string name = PackWidgets.PackName(_pack);
            _title.text = name;
            _subtitle.text = "v" + _pack.Version + "  ·  " + _pack.PackId + "  ·  " + _pack.ShortDigest;
            _name.text = name;
            int revisions = state.Installed.Count(p => p.PackId == _pack.PackId);
            _meta.text = "版本 " + _pack.Version + "  ·  包 ID " + _pack.PackId + "\n摘要 " + _pack.Digest.Substring(0, 16) + "…"
                + (revisions > 1 ? "  ·  同一包共 " + revisions + " 个修订" : string.Empty);
            int categories = _pack.Groups.Select(g => g.Category).Distinct().Count();
            _counts.text = categories + " 个类别  ·  " + _pack.Groups.Count + " 个资源组" + (_pack.Icon == null ? "  ·  无图标" : string.Empty);
            LoadIcon().Forget();

            bool current = state.IsPresetMode && state.Preset.IsFrom(_pack.PackId, _pack.Digest);
            if (_pack.Preset != null)
            {
                _presetName.text = _pack.Preset.Name;
                string declared = string.Join("、", MaterialCategories.All.Where(_pack.Preset.Categories.ContainsKey)
                    .Select(c => MaterialSlotCatalog.Category(c).Title));
                _presetText.text = (declared.Length > 0 ? "设置 " + declared + "；" : string.Empty) + "其余类别使用游戏默认。使用后切换到预设，九类一次生效。";
                _presetTag.gameObject.SetActive(current);
                _presetButton.gameObject.SetActive(true);
            }
            else
            {
                _presetName.text = "未附带预设";
                _presetText.text = "可以在混搭的各类别中选用此包的资源组。";
                _presetTag.gameObject.SetActive(false);
                _presetButton.gameObject.SetActive(false);
            }

            _legacyHint.gameObject.SetActive(_pack.MetadataVersion < InstalledMaterialPack.CURRENT_METADATA_VERSION);
            RebuildGroups(state);
        }

        private async UniTaskVoid LoadIcon()
        {
            InstalledMaterialPack pack = _pack;
            Sprite sprite = await Screen.Media.LoadIconAsync(Screen.Store, pack, _iconLoad.Token);
            if (!IsDisposed && _pack == pack) _icon.SetSprite(sprite);
        }

        private void RebuildGroups(MaterialPackState state)
        {
            foreach (GameObject row in _rows) Object.Destroy(row);
            _rows.Clear();
            foreach (string category in MaterialCategories.All)
            {
                List<MaterialPackGroup> groups = _pack.Groups.Where(g => g.Category == category).ToList();
                if (groups.Count == 0) continue;
                MaterialSlotCatalog.CategoryInfo info = MaterialSlotCatalog.Category(category);
                RectTransform caption = ServerWidgets.ListItem(_groups, "Caption-" + category, 44f);
                Image dot = LibraryWidgets.Panel("Dot", caption, 5f, info.Tint);
                LibraryWidgets.Place(dot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 12f), new Vector2(10f, 10f));
                TextMeshProUGUI label = LibraryWidgets.Label("Label", caption, info.Title + "  ·  " + info.Description, 19f,
                    LibraryTheme.Colors.TextSecondary, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
                LibraryWidgets.Inset(label.rectTransform, 30f, 4f, 0f, 0f);
                _rows.Add(caption.gameObject);
                foreach (MaterialPackGroup group in groups) _rows.Add(GroupRow(state, group).gameObject);
            }
        }

        private RectTransform GroupRow(MaterialPackState state, MaterialPackGroup group)
        {
            string reference = _pack.GroupReference(group.Id);
            MaterialGroupSelection selection = state.Selection(group.Category);
            int order = selection.Groups.IndexOf(reference);
            int total = MaterialCategories.Slots(group.Category).Count();
            int provided = MaterialCategories.Slots(group.Category).Count(group.Provides);

            RectTransform row = ServerWidgets.ListItem(_groups, "Group-" + group.Id, GROUP_ROW);
            Image surface = LibraryWidgets.Panel("Surface", row, 22f, HomeTheme.Colors.Card);
            surface.raycastTarget = true;
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, 22f);

            TextMeshProUGUI name = PackWidgets.UserLabel("Name", row, PackWidgets.GroupName(group), 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(name.rectTransform, 28f, 420f, 16f, 34f);
            string coverage = group.Category == MaterialCategories.BACKGROUND
                ? group.Background.Layers.Length + " 个图层，整组替换"
                : "提供 " + provided + " / " + total + " 个槽位";
            if (group.Layout != null) coverage += "  ·  含 COMBO 布局";
            if (group.Sprites.Values.Any(s => s.TemplateSet.HasValue)) coverage += "  ·  模板套装 " + group.Sprites.Values.First(s => s.TemplateSet.HasValue).TemplateSet;
            TextMeshProUGUI meta = PackWidgets.UserLabel("Meta", row, group.Id + "  ·  " + coverage, 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(meta.rectTransform, 28f, 420f, 54f, 26f);

            string usage = order == 0 ? "混搭主组" : order > 0 ? "混搭回退 " + order : null;
            if (usage != null)
            {
                RectTransform tag = PackWidgets.Tag(row, usage, MaterialSlotCatalog.Category(group.Category).Tint, out _, 32f, filled: order == 0);
                LibraryWidgets.Place(tag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-300f, 0f), tag.sizeDelta);
            }

            Button preview = ServerWidgets.LabelledButton(row, "Preview", "预览", LibraryIcon.Search, () =>
            {
                UiSound.Play(UiCue.Open);
                Screen.Preview.OpenGroup(_pack, group);
            }, 124f, 56f, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Place((RectTransform)preview.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-156f, 0f), new Vector2(124f, 56f));
            Button use = ServerWidgets.LabelledButton(row, "Use", "选用", LibraryIcon.Forward, () => Screen.OpenCategory(group.Category),
                124f, 56f, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)use.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(124f, 56f));
            return row;
        }

        private void OpenPreset()
        {
            if (_pack?.Preset == null) return;
            _presetCard.Open(_pack);
        }

        private void OpenUninstall()
        {
            if (_pack == null) return;
            if (Screen.IsImporting)
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast("正在导入资源包，请稍候。");
                return;
            }

            _uninstallCard.Open(_pack);
        }
    }
}
