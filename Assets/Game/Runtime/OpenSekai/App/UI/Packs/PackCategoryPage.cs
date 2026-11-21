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
    /// One category of 混搭: its groups in priority order (the first is the main group; when it lacks
    /// something, the next one in turn fills in), and the built-in set the category falls back to
    /// last. A category is chosen as a whole, not slot by slot. The demo beside it plays the whole
    /// 混搭, so a change shows in play as soon as it is saved.
    /// </summary>
    internal sealed class PackCategoryPage : PackPage
    {
        private const float LEFT_WIDTH = 640f;
        private const float COLUMN_GAP = 32f;
        private const float GROUP_ROW = 88f;
        private const float BUTTON_HEIGHT = 60f;

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
            subtitle.text = _info.Eyebrow + "  ·  混搭  ·  选择后立即保存，下一次开局生效";

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
                ? "本类使用" + BuiltinText(state)
                : "本类主组 " + Screen.DescribeGroup(selection.Groups[0])
                    + (selection.Groups.Count > 1 ? "，另有 " + (selection.Groups.Count - 1) + " 个回退" : string.Empty)
                    + "，缺项用" + BuiltinText(state);
            string note = state.IsPresetMode ? "现在用的是预设：这里的改动保存后切换到混搭。" : "演示播放混搭的全部九类，改动保存后重新演示。";
            return current + "\n" + note;
        }

        private void RebuildLeft(MaterialPackState state, MaterialGroupSelection selection)
        {
            foreach (GameObject item in _leftItems) UnityEngine.Object.Destroy(item);
            _leftItems.Clear();
            float top = 0f;

            // Groups in priority order.
            _leftItems.Add(PackWidgets.Caption(_left, "资源组与优先级", 0f, 0f, top).gameObject);
            top += LibraryTheme.Layout.SECTION_CAPTION_HEIGHT;
            int rows = Math.Max(1, selection.Groups.Count);
            Image groups = PackWidgets.Group(_left, "Groups");
            LibraryWidgets.TopStrip(groups.rectTransform, 0f, 0f, top, rows * GROUP_ROW);
            _leftItems.Add(groups.gameObject);
            if (selection.Groups.Count == 0)
            {
                TextMeshProUGUI none = PackWidgets.Paragraph(groups.transform, "None",
                    "未选用资源包，本类使用" + BuiltinText(state) + "。", 20f, LibraryTheme.Colors.TextMuted);
                none.alignment = TextAlignmentOptions.MidlineLeft;
                LibraryWidgets.Inset(none.rectTransform, 24f, 0f, 24f, 0f);
            }

            for (int i = 0; i < selection.Groups.Count; i++) AddGroupRow(groups.rectTransform, state, selection, i);
            top += rows * GROUP_ROW + 14f;

            Button main = ServerWidgets.LabelledButton(_left, "Main", selection.Groups.Count > 0 ? "更换主组" : "选择主组", LibraryIcon.Refresh,
                OpenMainChoices, 300f, BUTTON_HEIGHT, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Open);
            LibraryWidgets.Place((RectTransform)main.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -top), new Vector2(300f, BUTTON_HEIGHT));
            _leftItems.Add(main.gameObject);
            Button add = ServerWidgets.LabelledButton(_left, "Add", "添加回退", LibraryIcon.Add, OpenFallbackChoices, 300f, BUTTON_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Open);
            LibraryWidgets.Place((RectTransform)add.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -top), new Vector2(300f, BUTTON_HEIGHT));
            PackWidgets.SetEnabled(add, selection.Groups.Count > 0 && Candidates(state).Any(c => !selection.Groups.Contains(c.Reference)));
            _leftItems.Add(add.gameObject);
            top += BUTTON_HEIGHT + 14f;

            TextMeshProUGUI order = PackWidgets.Paragraph(_left, "Order",
                "主组缺少的部分，按顺序由回退的组补上；都没有时使用下方的内置。用箭头调整顺序。", 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(order.rectTransform, 4f, 4f, top, 50f);
            _leftItems.Add(order.gameObject);
            top += 50f + 20f;

            // The built-in fallback.
            _leftItems.Add(PackWidgets.Caption(_left, "内置回退", 0f, 0f, top).gameObject);
            top += LibraryTheme.Layout.SECTION_CAPTION_HEIGHT;
            Image builtin = PackWidgets.Group(_left, "Builtin");
            const float BUILTIN_HEIGHT = 176f;
            LibraryWidgets.TopStrip(builtin.rectTransform, 0f, 0f, top, BUILTIN_HEIGHT);
            _leftItems.Add(builtin.gameObject);
            BuildBuiltin(builtin.rectTransform, state);
            top += BUILTIN_HEIGHT + 24f;

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

        private void AddGroupRow(RectTransform panel, MaterialPackState state, MaterialGroupSelection selection, int index)
        {
            string reference = selection.Groups[index];
            RectTransform row = LibraryUi.CreateRect("Group" + index, panel);
            LibraryWidgets.TopStrip(row, 0f, 0f, index * GROUP_ROW, GROUP_ROW);
            if (index > 0)
            {
                Image divider = LibraryUi.CreateImage("Divider", row, LibraryTheme.Colors.Hairline);
                divider.raycastTarget = false;
                LibraryWidgets.TopStrip(divider.rectTransform, 24f, 0f, 0f, 1f);
            }

            RectTransform badge = PackWidgets.Tag(row, index == 0 ? "主组" : "回退 " + index, _info.Tint, out _, 32f, filled: index == 0);
            LibraryWidgets.Place(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), badge.sizeDelta);
            float textLeft = 20f + badge.sizeDelta.x + 16f;

            state.TryFindGroup(reference, out InstalledMaterialPack pack, out MaterialPackGroup group);
            TextMeshProUGUI name = PackWidgets.UserLabel("Name", row, group != null ? PackWidgets.GroupName(group) : reference, 22f,
                LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(name.rectTransform, textLeft, 200f, 14f, 32f);
            TextMeshProUGUI meta = PackWidgets.UserLabel("Meta", row, pack != null ? PackWidgets.PackName(pack) + "  " + PackWidgets.Revision(pack) : string.Empty,
                16f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(meta.rectTransform, textLeft, 200f, 48f, 24f);

            const float SIZE = 50f;
            Button up = PackWidgets.SmallIconButton(row, "Up", LibraryIcon.Back, () => Move(index, -1), SIZE, UiCue.Select);
            up.transform.Find("Glyph").localEulerAngles = new Vector3(0f, 0f, -90f);
            LibraryWidgets.Place((RectTransform)up.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-136f, 0f), new Vector2(SIZE, SIZE));
            PackWidgets.SetEnabled(up, index > 0);
            Button down = PackWidgets.SmallIconButton(row, "Down", LibraryIcon.Back, () => Move(index, 1), SIZE, UiCue.Select);
            down.transform.Find("Glyph").localEulerAngles = new Vector3(0f, 0f, 90f);
            LibraryWidgets.Place((RectTransform)down.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-78f, 0f), new Vector2(SIZE, SIZE));
            PackWidgets.SetEnabled(down, index < selection.Groups.Count - 1);
            Button remove = PackWidgets.SmallIconButton(row, "Remove", LibraryIcon.Close, () => Remove(index), SIZE, UiCue.Delete);
            LibraryWidgets.Place((RectTransform)remove.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(SIZE, SIZE));
        }

        private void BuildBuiltin(RectTransform panel, MaterialPackState state)
        {
            bool twoSets = MaterialCategories.HasBuiltinSets(_category);
            TextMeshProUGUI text = PackWidgets.Paragraph(panel, "Text", twoSets
                ? "资源组都没有提供的部分，使用这里选中的内置套装。"
                : "此类只有一套内置资源。资源组都没有提供的部分使用游戏默认。", 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(text.rectTransform, 24f, 24f, 18f, 52f);
            if (twoSets)
            {
                int current = state.Builtin.Get(_category);
                for (int set = 1; set <= MaterialCategories.BUILTIN_SET_COUNT; set++)
                {
                    int choice = set;
                    bool selected = current == set;
                    Button option = LibraryWidgets.PillButton("Set" + set, panel, MaterialSlotCatalog.BuiltinLabel(_category, set), () => SetBuiltin(choice),
                        180f, 60f, selected ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.Control,
                        selected ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary, LibraryTheme.FontSize.BODY,
                        selected ? FontStyles.Bold : FontStyles.Normal, UiCue.None);
                    if (!selected) LibraryWidgets.Outline((Image)option.targetGraphic, 30f);
                    LibraryWidgets.Place((RectTransform)option.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(24f + (set - 1) * 196f, 24f), new Vector2(180f, 60f));
                }
            }
            else
            {
                RectTransform tag = PackWidgets.Tag(panel, "游戏默认", LibraryTheme.Colors.TextSecondary, out _, 44f);
                LibraryWidgets.Place(tag, Vector2.zero, Vector2.zero, new Vector2(24f, 32f), tag.sizeDelta);
            }

            int previewSet = twoSets ? state.Builtin.Get(_category) : 1;
            Button preview = ServerWidgets.LabelledButton(panel, "Preview", "查看内置", LibraryIcon.Search, () =>
            {
                UiSound.Play(UiCue.Open);
                Screen.Preview.OpenBuiltin(_category, previewSet);
            }, 180f, 60f, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Place((RectTransform)preview.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(180f, 60f));
        }

        private List<string> Notices(MaterialPackState state, MaterialGroupSelection selection)
        {
            var notes = new List<string>();
            if (_category == MaterialCategories.BACKGROUND)
                notes.Add("背景整组替换：选用一个组会一次换上它的全部背景图层。");
            if (_category == MaterialCategories.COMBO && selection.Groups.Count > 0)
            {
                string layout = MaterialPackResolver.ComboLayoutSource(state.Installed, selection);
                notes.Add(layout != null
                    ? "COMBO 布局来自 " + Screen.DescribeGroup(layout) + "（顺序中第一个提供布局的组）。"
                    : "COMBO 布局使用内置：选用的组都没有提供布局。");
            }

            if (_category == MaterialCategories.AUDIO)
                notes.Add("演示默认静音，点演示右上角的「声音」可以听到打击音与结算语音。");
            return notes;
        }

        private string BuiltinText(MaterialPackState state) =>
            MaterialCategories.HasBuiltinSets(_category) ? "内置" + MaterialSlotCatalog.BuiltinLabel(_category, state.Builtin) : "游戏默认";

        // ------------------------------------------------------------------ choices

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

        internal void OpenMainChoices()
        {
            MaterialPackState state = Screen.State;
            if (state == null) return;
            MaterialGroupSelection selection = state.Selection(_category);
            var candidates = Candidates(state).ToList();
            var options = new List<PackChoicePanel.Option>
            {
                new PackChoicePanel.Option
                {
                    Label = "仅用内置", Selected = selection.Groups.Count == 0,
                    Detail = "不选用资源包，本类使用" + BuiltinText(state)
                }
            };
            options.AddRange(candidates.Select(c => GroupOption(c, selection, selection.Groups.Count > 0 && selection.Groups[0] == c.Reference)));
            if (candidates.Count == 0)
                options.Add(new PackChoicePanel.Option { Label = "没有提供" + _info.Title + "的资源包", Detail = "导入含此类别资源组的 ZIP 后可在这里选用", Enabled = false });
            Screen.Choices.Open("选择" + _info.Title + "的主组", "主组排在最前，原有的组顺延为回退", options, index =>
            {
                if (index == 0)
                {
                    Edit(s => s.Groups.Clear(), "已改为仅用内置");
                    return;
                }

                if (index - 1 >= candidates.Count) return;
                string reference = candidates[index - 1].Reference;
                Edit(s =>
                {
                    s.Groups.Remove(reference);
                    s.Groups.Insert(0, reference);
                }, "已设为主组");
            });
        }

        private void OpenFallbackChoices()
        {
            MaterialPackState state = Screen.State;
            if (state == null) return;
            MaterialGroupSelection selection = state.Selection(_category);
            var candidates = Candidates(state).Where(c => !selection.Groups.Contains(c.Reference)).ToList();
            if (candidates.Count == 0)
            {
                UiSound.Play(UiCue.Blocked);
                Screen.ShowToast("没有可添加的资源组");
                return;
            }

            var options = candidates.Select(c => GroupOption(c, selection, false)).ToList();
            Screen.Choices.Open("添加" + _info.Title + "回退", "回退排在已选用的组之后，前面的组缺少的部分由它补上", options, index =>
            {
                string reference = candidates[index].Reference;
                Edit(s => s.Groups.Add(reference), "已添加回退");
            });
        }

        private PackChoicePanel.Option GroupOption(Candidate candidate, MaterialGroupSelection selection, bool selected)
        {
            int order = selection.Groups.IndexOf(candidate.Reference);
            return new PackChoicePanel.Option
            {
                Label = PackWidgets.GroupName(candidate.Group),
                Detail = PackWidgets.PackName(candidate.Pack) + "  " + PackWidgets.Revision(candidate.Pack),
                Selected = selected,
                Badge = order == 0 ? "主组" : order > 0 ? "回退 " + order : null,
                BadgeTint = _info.Tint
            };
        }

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

        private void Move(int index, int step)
        {
            Edit(s =>
            {
                int target = index + step;
                if (target < 0 || target >= s.Groups.Count) return;
                string item = s.Groups[index];
                s.Groups.RemoveAt(index);
                s.Groups.Insert(target, item);
            }, "已调整优先级");
        }

        private void Remove(int index)
        {
            Edit(s =>
            {
                if (index >= 0 && index < s.Groups.Count) s.Groups.RemoveAt(index);
            }, "已移除资源组");
        }

        private void SetBuiltin(int set)
        {
            MaterialPackConfiguration next = Screen.BeginEdit();
            if (next.Builtin.Get(_category) == set && Screen.State?.IsPresetMode != true) return;
            next.Builtin.Set(_category, set);
            UiSound.Play(UiCue.Select);
            Screen.Save(next, "内置回退已改为" + MaterialSlotCatalog.BuiltinLabel(_category, set) + "，下一次开局生效");
        }
    }
}
