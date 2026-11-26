using System;
using System.Collections.Generic;
using System.Linq;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One category of 混搭. The left side lists every installed group of the category: the
    /// enabled ones on top in priority order (a group covers the ones below it; what none of them
    /// provides comes from the built-in bundle), the others below, a tap away from being enabled.
    /// A category is chosen as a whole, not slot by slot. The demo beside it plays the whole
    /// 混搭, so a change shows in play as soon as it is saved.
    /// </summary>
    internal sealed class PackCategoryPage : PackPage
    {
        private const float LEFT_WIDTH = 640f;
        private const float COLUMN_GAP = 32f;
        private const float GROUP_ROW = 88f;
        private const float ICON_BUTTON = 50f;
        private const float BUTTON_GAP = 8f;
        private const float ENABLE_WIDTH = 120f;

        private readonly string _category;
        private readonly MaterialSlotCatalog.CategoryInfo _info;
        private readonly RectTransform _left;
        private readonly List<GameObject> _leftItems = new List<GameObject>();
        private readonly PackDemoView _demo;
        private readonly ServerStatusView _status;

        internal PackCategoryPage(PackScreen screen, string category) : base(screen, "PackCategory-" + category)
        {
            _category = category;
            _info = MaterialSlotCatalog.Category(category);
            ServerWidgets.TopBar(Root, _info.Title, screen.Back, screen.OpenSettings, out _, out TextMeshProUGUI subtitle, out _);
            subtitle.text = _info.Eyebrow + "  ·  混搭  ·  改动立即保存，下一次开局生效";

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", Root);
            LibraryWidgets.Inset(body, margin, 30f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            RectTransform leftArea = LibraryUi.CreateRect("Left", body);
            leftArea.anchorMin = Vector2.zero;
            leftArea.anchorMax = new Vector2(0f, 1f);
            leftArea.pivot = new Vector2(0f, 0.5f);
            leftArea.offsetMin = Vector2.zero;
            leftArea.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _left = PackWidgets.Scroll(leftArea, out _);

            RectTransform right = LibraryUi.CreateRect("Demo", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _demo = new PackDemoView(right, screen.Demo, _info.Tint);
            LibraryUi.Stretch(_demo.Root);

            RectTransform statusArea = LibraryUi.CreateRect("Status", Root);
            LibraryWidgets.Inset(statusArea, margin, 30f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            _status = new ServerStatusView(statusArea);
            Refresh();
        }

        /// <summary>The whole 混搭, with this category as it is now.</summary>
        internal override MaterialPackConfiguration DemoConfiguration => Screen.State?.MixConfiguration();

        internal override void OnShown() => Refresh();

        internal override void OnStateChanged() => Refresh();

        protected override void OnDispose()
        {
            _status.Dispose();
            _demo.Dispose();
        }

        // ------------------------------------------------------------------ refresh

        private void Refresh()
        {
            MaterialPackState state = Screen.State;
            if (state == null)
            {
                _status.ShowError(Screen.LoadError ?? "无法读取资源包。", () => Screen.Reload());
                return;
            }

            _status.Hide();
            MaterialGroupSelection selection = state.Selection(_category);
            _demo.SetText("混搭 · " + _info.Title, DemoFooter(state, selection));
            _demo.Refresh();
            RebuildLeft(state, selection);
        }

        private string DemoFooter(MaterialPackState state, MaterialGroupSelection selection)
        {
            string current = selection.Groups.Count == 0
                ? "本类未启用资源包，使用" + BuiltinText(state)
                : "最上方 " + Screen.DescribeGroup(selection.Groups[0])
                    + (selection.Groups.Count > 1 ? "，共启用 " + selection.Groups.Count + " 个" : string.Empty);
            string note = state.IsPresetMode ? "现在用的是预设：这里的改动保存后切换到混搭。" : "演示播放混搭的全部九类，改动保存后重新演示。";
            return current + "\n" + note;
        }

        private void RebuildLeft(MaterialPackState state, MaterialGroupSelection selection)
        {
            foreach (GameObject item in _leftItems) UnityEngine.Object.Destroy(item);
            _leftItems.Clear();
            float top = 0f;

            // Enabled, in priority order: each one covers the ones below it.
            top = AddSectionCaption("已启用", selection.Groups.Count, top);
            int enabledRows = Math.Max(1, selection.Groups.Count);
            Image enabled = PackWidgets.Group(_left, "Enabled");
            LibraryWidgets.TopStrip(enabled.rectTransform, 0f, 0f, top, enabledRows * GROUP_ROW);
            _leftItems.Add(enabled.gameObject);
            if (selection.Groups.Count == 0)
                AddEmptyRow(enabled.rectTransform, "未启用资源包，本类全部使用" + BuiltinText(state) + "。");
            for (int i = 0; i < selection.Groups.Count; i++) AddEnabledRow(enabled.rectTransform, state, selection, i);
            top += enabledRows * GROUP_ROW + 10f;

            TextMeshProUGUI order = PackWidgets.Paragraph(_left, "Order",
                "上面的覆盖下面的，用箭头调整顺序。都没有提供的部分使用" + BuiltinText(state) + "。",
                17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(order.rectTransform, 4f, 4f, top, 64f);
            _leftItems.Add(order.gameObject);
            top += 64f + 16f;

            // Not enabled: every other installed group of this category.
            List<Candidate> others = Candidates(state).Where(c => !selection.Groups.Contains(c.Reference)).ToList();
            top = AddSectionCaption("未启用", others.Count, top);
            int otherRows = Math.Max(1, others.Count);
            Image disabled = PackWidgets.Group(_left, "Disabled");
            LibraryWidgets.TopStrip(disabled.rectTransform, 0f, 0f, top, otherRows * GROUP_ROW);
            _leftItems.Add(disabled.gameObject);
            if (others.Count == 0)
                AddEmptyRow(disabled.rectTransform, selection.Groups.Count == 0
                    ? "没有提供" + _info.Title + "的资源包。导入含此类别资源组的 ZIP 后会出现在这里。"
                    : "已安装的" + _info.Title + "资源组都已启用。");
            for (int i = 0; i < others.Count; i++) AddDisabledRow(disabled.rectTransform, others[i], i);
            top += otherRows * GROUP_ROW + 24f;

            // What the combination means for this category.
            List<string> notes = Notices(state, selection);
            if (notes.Count > 0)
            {
                _leftItems.Add(PackWidgets.Caption(_left, "说明", 0f, 0f, top).gameObject);
                top += LibraryTheme.Layout.SECTION_CAPTION_HEIGHT;
                float height = 30f + notes.Count * 52f;
                Image panel = PackWidgets.Group(_left, "Notes");
                LibraryWidgets.TopStrip(panel.rectTransform, 0f, 0f, top, height);
                TextMeshProUGUI text = PackWidgets.Paragraph(panel.transform, "Text", string.Join("\n", notes), 18f, LibraryTheme.Colors.TextSecondary);
                text.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Inset(text.rectTransform, 24f, 12f, 24f, 16f);
                _leftItems.Add(panel.gameObject);
                top += height + 24f;
            }

            _left.sizeDelta = new Vector2(0f, top);
        }

        /// <summary>A section caption with its count on the right; returns the top below it.</summary>
        private float AddSectionCaption(string title, int count, float top)
        {
            _leftItems.Add(PackWidgets.Caption(_left, title, 0f, 120f, top).gameObject);
            TextMeshProUGUI label = LibraryWidgets.Label("Count", _left, count + " 个", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.BottomRight);
            LibraryWidgets.TopStrip(label.rectTransform, 300f, 8f, top, LibraryTheme.Layout.SECTION_CAPTION_HEIGHT - 10f);
            _leftItems.Add(label.gameObject);
            return top + LibraryTheme.Layout.SECTION_CAPTION_HEIGHT;
        }

        private static void AddEmptyRow(RectTransform panel, string message)
        {
            TextMeshProUGUI none = PackWidgets.Paragraph(panel, "None", message, 19f, LibraryTheme.Colors.TextMuted);
            none.alignment = TextAlignmentOptions.MidlineLeft;
            LibraryWidgets.TopStrip(none.rectTransform, 24f, 24f, 0f, GROUP_ROW);
        }

        private RectTransform Row(RectTransform panel, string name, int index)
        {
            RectTransform row = LibraryUi.CreateRect(name, panel);
            LibraryWidgets.TopStrip(row, 0f, 0f, index * GROUP_ROW, GROUP_ROW);
            if (index > 0)
            {
                Image divider = LibraryUi.CreateImage("Divider", row, LibraryTheme.Colors.Hairline);
                divider.raycastTarget = false;
                LibraryWidgets.TopStrip(divider.rectTransform, 24f, 0f, 0f, 1f);
            }

            return row;
        }

        private static void AddNames(RectTransform row, InstalledMaterialPack pack, MaterialPackGroup group, string reference, float left, float right)
        {
            TextMeshProUGUI name = PackWidgets.UserLabel("Name", row, group != null ? PackWidgets.GroupName(group) : reference, 22f,
                LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(name.rectTransform, left, right, 14f, 32f);
            TextMeshProUGUI meta = PackWidgets.UserLabel("Meta", row, pack != null ? PackWidgets.PackName(pack) + "  " + PackWidgets.Revision(pack) : "已不在安装列表中",
                16f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(meta.rectTransform, left, right, 48f, 24f);
        }

        private void AddPreview(RectTransform row, InstalledMaterialPack pack, MaterialPackGroup group, float right)
        {
            Button preview = PackWidgets.SmallIconButton(row, "Preview", LibraryIcon.Search, () =>
            {
                UiSound.Play(UiCue.Open);
                Screen.Preview.OpenGroup(pack, group);
            }, ICON_BUTTON, UiCue.None);
            LibraryWidgets.Place((RectTransform)preview.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f),
                new Vector2(ICON_BUTTON, ICON_BUTTON));
            PackWidgets.SetEnabled(preview, pack != null && group != null);
        }

        private void AddEnabledRow(RectTransform panel, MaterialPackState state, MaterialGroupSelection selection, int index)
        {
            string reference = selection.Groups[index];
            RectTransform row = Row(panel, "Enabled" + index, index);
            RectTransform badge = PackWidgets.Tag(row, (index + 1).ToString(), _info.Tint, out float badgeWidth, 32f, filled: index == 0);
            badge.sizeDelta = new Vector2(Mathf.Max(badgeWidth, 40f), badge.sizeDelta.y);
            LibraryWidgets.Place(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), badge.sizeDelta);
            float textLeft = 20f + badge.sizeDelta.x + 16f;

            // From the right: 停用, down, up, preview.
            float step = ICON_BUTTON + BUTTON_GAP;
            Button remove = PackWidgets.SmallIconButton(row, "Disable", LibraryIcon.Close, () => Disable(index), ICON_BUTTON, UiCue.Delete);
            LibraryWidgets.Place((RectTransform)remove.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                new Vector2(ICON_BUTTON, ICON_BUTTON));
            Button down = PackWidgets.SmallIconButton(row, "Down", LibraryIcon.Back, () => Move(index, 1), ICON_BUTTON, UiCue.Select);
            down.transform.Find("Glyph").localEulerAngles = new Vector3(0f, 0f, 90f);
            LibraryWidgets.Place((RectTransform)down.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f - step, 0f),
                new Vector2(ICON_BUTTON, ICON_BUTTON));
            PackWidgets.SetEnabled(down, index < selection.Groups.Count - 1);
            Button up = PackWidgets.SmallIconButton(row, "Up", LibraryIcon.Back, () => Move(index, -1), ICON_BUTTON, UiCue.Select);
            up.transform.Find("Glyph").localEulerAngles = new Vector3(0f, 0f, -90f);
            LibraryWidgets.Place((RectTransform)up.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f - step * 2f, 0f),
                new Vector2(ICON_BUTTON, ICON_BUTTON));
            PackWidgets.SetEnabled(up, index > 0);

            state.TryFindGroup(reference, out InstalledMaterialPack pack, out MaterialPackGroup group);
            AddPreview(row, pack, group, 20f + step * 3f);
            AddNames(row, pack, group, reference, textLeft, 20f + step * 4f + 4f);
        }

        private void AddDisabledRow(RectTransform panel, Candidate candidate, int index)
        {
            RectTransform row = Row(panel, "Disabled" + index, index);
            Button enable = ServerWidgets.LabelledButton(row, "Enable", "启用", LibraryIcon.Add, () => Enable(candidate.Reference),
                ENABLE_WIDTH, ICON_BUTTON, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Select);
            LibraryWidgets.Place((RectTransform)enable.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                new Vector2(ENABLE_WIDTH, ICON_BUTTON));
            float right = 20f + ENABLE_WIDTH + BUTTON_GAP;
            AddPreview(row, candidate.Pack, candidate.Group, right);
            AddNames(row, candidate.Pack, candidate.Group, candidate.Reference, 24f, right + ICON_BUTTON + 12f);
        }

        private List<string> Notices(MaterialPackState state, MaterialGroupSelection selection)
        {
            var notes = new List<string>();
            if (_category == MaterialCategories.BACKGROUND)
                notes.Add("背景整组替换：启用的第一个组会一次换上它的全部背景图层。");
            if (_category == MaterialCategories.COMBO && selection.Groups.Count > 0)
            {
                string layout = MaterialPackResolver.ComboLayoutSource(state.Installed, selection);
                notes.Add(layout != null
                    ? "COMBO 布局来自 " + Screen.DescribeGroup(layout) + "（启用的组中第一个提供布局的组）。"
                    : "COMBO 布局使用内置：启用的组都没有提供布局。");
            }

            if (_category == MaterialCategories.AUDIO)
                notes.Add("演示默认静音，点演示右上角的「声音」可以听到打击音与结算语音。");
            return notes;
        }

        /// <summary>What this category falls back to last: the built-in bundle's set, or the game default.</summary>
        private string BuiltinText(MaterialPackState state) =>
            MaterialCategories.HasBuiltinSets(_category)
                ? "内置整合包的「" + MaterialSlotCatalog.BuiltinLabel(_category, state.Builtin) + "」"
                : "游戏默认资源";

        // ------------------------------------------------------------------ candidates

        private readonly struct Candidate
        {
            internal readonly string Reference;
            internal readonly InstalledMaterialPack Pack;
            internal readonly MaterialPackGroup Group;

            internal Candidate(InstalledMaterialPack pack, MaterialPackGroup group)
            {
                Pack = pack;
                Group = group;
                Reference = pack.GroupReference(group.Id);
            }
        }

        private IEnumerable<Candidate> Candidates(MaterialPackState state) =>
            state.Installed.SelectMany(p => p.Groups.Where(g => g.Category == _category).Select(g => new Candidate(p, g)));

        // ------------------------------------------------------------------ edits

        /// <summary>
        /// Changes this category of 混搭 and saves it. The category is chosen as a whole, so any
        /// per-slot choice left in it from before goes too.
        /// </summary>
        private void Edit(Action<MaterialGroupSelection> change, string success)
        {
            MaterialPackConfiguration next = Screen.BeginEdit();
            MaterialGroupSelection selection = next.Get(_category);
            change(selection);
            selection.Overrides.Clear();
            Screen.Save(next, success + "，下一次开局生效");
        }

        /// <summary>Enables a group on top, so it shows at once; the arrows move it down.</summary>
        private void Enable(string reference)
        {
            Edit(s =>
            {
                s.Groups.Remove(reference);
                s.Groups.Insert(0, reference);
            }, "已启用并排在最上方");
        }

        private void Move(int index, int step)
        {
            Edit(s =>
            {
                int target = index + step;
                if (target < 0 || target >= s.Groups.Count) return;
                string item = s.Groups[index];
                s.Groups.RemoveAt(index);
                s.Groups.Insert(target, item);
            }, "已调整顺序");
        }

        private void Disable(int index)
        {
            Edit(s =>
            {
                if (index >= 0 && index < s.Groups.Count) s.Groups.RemoveAt(index);
            }, "已停用");
        }
    }
}
