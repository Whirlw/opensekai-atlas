using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>The two lists on the 个人信息 page.</summary>
    internal enum ProfileTab
    {
        Best30,
        History
    }

    /// <summary>
    /// Layout and binding of the 个人信息 page. On the left, the player's card: avatar and nickname
    /// (tap either to edit them), the Ranking Score with its tier badge, and the worn 段位 plate.
    /// On the right, the Best 30 (one line per chart, by contribution) or the whole history (one
    /// line per run, newest first) in a recycling list. The page reads everything from its
    /// <see cref="ProfileScreen"/> and never computes a score itself. Sizes are canvas units at the
    /// 1920×1080 reference; the left card anchors its parts to its top and bottom so a short
    /// phone screen only narrows the gap between them.
    /// </summary>
    internal sealed class ProfileView : IDisposable
    {
        private const float LEFT_WIDTH = 560f;
        private const float COLUMN_GAP = 28f;
        private const float CARD_RADIUS = 30f;
        private const float PAD = 36f;
        private const float AVATAR_SIZE = 128f;
        private const float BADGE_SIZE = 118f;
        private const float PLATE_HEIGHT = 76f;
        private const float TAB_HEIGHT = 56f;
        private const float LIST_PAD = 16f;

        private static readonly Color32[] _podium =
        {
            new Color32(255, 214, 120, 255),
            new Color32(214, 222, 235, 255),
            new Color32(232, 170, 128, 255)
        };

        private static readonly Color32 _standing = new Color32(194, 174, 255, 255);

        private readonly ProfileScreen _screen;
        private readonly RectTransform _root;
        private readonly CanvasGroup _leftGroup;
        private readonly RectTransform _leftCard;
        private readonly CanvasGroup _rightGroup;
        private readonly RectTransform _rightCard;
        private readonly TextMeshProUGUI _subtitle;
        private readonly ProfileAvatarView _avatar;
        private readonly TextMeshProUGUI _nickname;
        private readonly TextMeshProUGUI _meta;
        private readonly RankingBadge _badge;
        private readonly TextMeshProUGUI _total;
        private readonly TextMeshProUGUI _tier;
        private readonly TextMeshProUGUI _coverage;
        private readonly DanPlate _plate;
        private readonly TextMeshProUGUI _plateNote;
        private readonly HomePageTabs _tabs;
        private readonly TextMeshProUGUI _summary;
        private readonly Button _clearHistory;
        private readonly VirtualListView _list;
        private readonly ServerStatusView _status;
        private readonly Dictionary<RectTransform, ProfileRow> _rows = new Dictionary<RectTransform, ProfileRow>();
        private MotionHandle _enter;
        private ProfileTab _tab;

        internal ProfileView(RectTransform parent, ProfileScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("ProfilePage", parent);
            LibraryUi.Stretch(_root);

            RectTransform bar = ServerWidgets.TopBar(_root, "个人信息", screen.Back, screen.OpenSettings, out _, out _subtitle,
                out float rightInset);
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            const float BUTTON_WIDTH = 212f;
            Button import = ServerWidgets.LabelledButton(bar, "Import", "导入备份", LibraryIcon.Download, screen.Import, BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None);
            LibraryWidgets.Outline((Image)import.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)import.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-rightInset, 0f),
                new Vector2(BUTTON_WIDTH, height));
            Button export = ServerWidgets.LabelledButton(bar, "Export", "导出备份", LibraryIcon.Export, screen.Export, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)export.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-(rightInset + BUTTON_WIDTH + 16f), 0f), new Vector2(BUTTON_WIDTH, height));

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", _root);
            LibraryWidgets.Inset(body, margin, 48f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            // ---------------------------------------------------------------- the player's card
            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _leftGroup = left.gameObject.AddComponent<CanvasGroup>();
            _leftCard = left;
            RectTransform card = Card(left, "PlayerCard");
            Vector2 topLeft = new Vector2(0f, 1f);

            _avatar = new ProfileAvatarView(card, "Avatar", AVATAR_SIZE);
            LibraryWidgets.Place(_avatar.Root, topLeft, topLeft, new Vector2(PAD, -PAD), new Vector2(AVATAR_SIZE, AVATAR_SIZE));
            Image avatarHit = LibraryUi.CreateImage("Hit", _avatar.Root, Color.clear);
            avatarHit.raycastTarget = true;
            LibraryUi.Stretch(avatarHit.rectTransform);
            Button avatarButton = avatarHit.gameObject.AddComponent<Button>();
            avatarButton.targetGraphic = avatarHit;
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.onClick.AddListener(screen.OpenEditor);
            LibraryPressFeedback.Attach(avatarHit.gameObject, _avatar.Root);

            float nameLeft = PAD + AVATAR_SIZE + 24f;
            _nickname = PackWidgets.UserLabel("Nickname", card, string.Empty, 42f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            Strip(_nickname.rectTransform, nameLeft, PAD, PAD + 2f, 56f);
            _meta = LibraryWidgets.Label("Meta", card, string.Empty, 19f, LibraryTheme.Colors.TextMuted);
            Strip(_meta.rectTransform, nameLeft, PAD, PAD + 60f, 28f);
            const float EDIT_WIDTH = 170f;
            const float EDIT_HEIGHT = 46f;
            Button edit = ServerWidgets.LabelledButton(card, "Edit", "编辑资料", LibraryIcon.User, screen.OpenEditor, EDIT_WIDTH, EDIT_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 18f);
            LibraryWidgets.Outline((Image)edit.targetGraphic, EDIT_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)edit.transform, topLeft, topLeft, new Vector2(nameLeft, -(PAD + 96f)),
                new Vector2(EDIT_WIDTH, EDIT_HEIGHT));

            float rankTop = PAD + AVATAR_SIZE + 34f;
            Divider(card, rankTop - 16f, true);
            TextMeshProUGUI rankCaption = LibraryWidgets.Label("RankingCaption", card, "RANKING SCORE", 20f,
                LibraryTheme.Colors.TextMuted, alignment: TextAlignmentOptions.CenterGeoAligned);
            LibraryUi.UseSpacedCondensedFont(rankCaption, -4f);
            LibraryWidgets.Place(rankCaption.rectTransform, topLeft, topLeft, new Vector2(PAD, -(rankTop + 8f)),
                new Vector2(BADGE_SIZE, 26f));
            float badgeTop = rankTop + 46f;
            _badge = new RankingBadge(card, BADGE_SIZE);
            LibraryWidgets.Place(_badge.Root, topLeft, new Vector2(0.5f, 0.5f),
                new Vector2(PAD + BADGE_SIZE * 0.5f, -(badgeTop + BADGE_SIZE * 0.5f)), new Vector2(BADGE_SIZE, BADGE_SIZE));
            float totalLeft = PAD + BADGE_SIZE + 26f;
            _total = LibraryWidgets.Label("Total", card, "0", 88f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(_total);
            _total.overflowMode = TextOverflowModes.Overflow;
            _total.alignment = TextAlignmentOptions.BottomLeft;
            Strip(_total.rectTransform, totalLeft - 4f, PAD, badgeTop - 14f, 96f);
            _tier = LibraryWidgets.Label("Tier", card, string.Empty, 24f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
            Strip(_tier.rectTransform, totalLeft, PAD, badgeTop + 84f, 34f);
            _coverage = LibraryWidgets.Label("Coverage", card, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            Strip(_coverage.rectTransform, PAD, PAD, badgeTop + BADGE_SIZE + 18f, 26f);

            // The worn plate, from the bottom up: note, plate, caption.
            _plateNote = LibraryWidgets.Label("DanNote", card, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(_plateNote.rectTransform, PAD, PAD, PAD - 4f, 26f);
            float plateBottom = PAD + 34f;
            _plate = new DanPlate(card, "Plate", new Vector2(LEFT_WIDTH - PAD * 2f, PLATE_HEIGHT), shadow: false);
            LibraryWidgets.Place(_plate.Root, Vector2.zero, Vector2.zero, new Vector2(PAD, plateBottom), _plate.Root.sizeDelta);
            Image plateHit = LibraryUi.CreateImage("Hit", _plate.Root, Color.clear);
            plateHit.raycastTarget = true;
            LibraryUi.Stretch(plateHit.rectTransform);
            Button plateButton = plateHit.gameObject.AddComponent<Button>();
            plateButton.targetGraphic = plateHit;
            plateButton.transition = Selectable.Transition.None;
            plateButton.onClick.AddListener(screen.OpenPlates);
            LibraryPressFeedback.Attach(plateHit.gameObject, _plate.Root, 0.985f);
            TextMeshProUGUI danCaption = LibraryWidgets.Label("DanCaption", card, "段位牌", 18f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.BottomStrip(danCaption.rectTransform, PAD, PAD, plateBottom + PLATE_HEIGHT + 10f, 26f);
            Divider(card, plateBottom + PLATE_HEIGHT + 52f, false);

            // ---------------------------------------------------------------- the lists
            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _rightGroup = right.gameObject.AddComponent<CanvasGroup>();
            _rightCard = right;
            RectTransform lists = Card(right, "ListCard");
            _tabs = new HomePageTabs(lists, new[] { "Best 30", "全部历史" }, index => ShowTab((ProfileTab)index, true), 196f, TAB_HEIGHT, 21f);
            LibraryWidgets.Place(_tabs.Root, topLeft, topLeft, new Vector2(28f, -26f), _tabs.Root.sizeDelta);
            _summary = LibraryWidgets.Label("Summary", lists, string.Empty, 19f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Right);
            LibraryWidgets.TopStrip(_summary.rectTransform, 460f, 34f, 26f, TAB_HEIGHT);
            _clearHistory = ServerWidgets.LabelledButton(lists, "ClearHistory", "清空历史", LibraryIcon.Trash,
                screen.RequestClearHistory, 178f, TAB_HEIGHT, LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger, UiCue.None);
            LibraryWidgets.Place((RectTransform)_clearHistory.transform, Vector2.one, Vector2.one,
                new Vector2(-28f, -26f), new Vector2(178f, TAB_HEIGHT));

            RectTransform listArea = LibraryUi.CreateRect("ListArea", lists);
            LibraryWidgets.Inset(listArea, LIST_PAD, LIST_PAD, LIST_PAD, 26f + TAB_HEIGHT + 18f);
            PackWidgets.Scroll(listArea, out ScrollRect scroll);
            _list = VirtualListView.Attach(scroll,
                width => VirtualListLayout.Rows(width, ProfileRow.HEIGHT, 8f, 0f, 12f),
                CreateRow, BindRow, ReleaseRow);
            _status = new ServerStatusView(listArea);
        }

        internal RectTransform Root => _root;

        internal ProfileTab Tab => _tab;

        /// <summary>Redraws everything from the screen's current snapshot.</summary>
        internal void Bind(bool keepScroll = false)
        {
            PlayerProfileSnapshot snapshot = _screen.Snapshot;
            Best30Snapshot best = snapshot?.Best30;
            int plays = _screen.HistoryCount;
            int charts = best?.Entries.Count ?? 0;
            long total = best?.Total ?? 0L;
            int tier = best?.TierId ?? 0;
            bool readable = snapshot != null;

            _avatar.SetAvatar(snapshot?.Appearance.AvatarId ?? ProfileAvatars.DEFAULT_ID);
            _nickname.text = snapshot?.Appearance.Nickname ?? "—";
            _meta.text = readable ? "本机档案  ·  " + ProfileCopy.Number(plays) + " 次游玩" : "档案暂时无法读取";
            _badge.SetTier(tier);
            _total.text = readable ? ProfileCopy.Number(total) : "—";
            Color32 tierColor = RankingTiers.Color(tier);
            _tier.text = RankingTiers.Name(tier) + "  " + RankingTiers.LatinName(tier);
            _tier.color = tierColor;
            _coverage.text = charts == 0
                ? "手动完成一首谱面后，成绩会计入 Best 30"
                : "Best 30 已收录 " + charts + " / 30 张谱面";

            OpenSekai.App.Playback.DanDefinition worn = snapshot?.EquippedDan;
            int earned = snapshot?.EarnedDanIds.Count ?? 0;
            if (worn != null)
            {
                _plate.SetStyle(worn.Label, worn.Emblem, worn.Color);
                _plate.SetLocked(false);
            }
            else
            {
                _plate.SetEmpty(earned > 0 ? "未佩戴段位牌" : "未取得段位");
            }

            _plateNote.text = earned > 0
                ? "已取得 " + earned + " 块段位牌，点按即可更换展示的一块"
                : "通过段位挑战后，可以在这里选择展示的段位牌";

            _tabs.SetLabel(0, "Best 30  " + Count(charts));
            _tabs.SetLabel(1, "全部历史  " + Count(plays));
            if (best != null && SumOfEntries(best) != total)
            {
                // The page shows both numbers; they must agree (one contribution per chart).
                Debug.LogWarning("[Profile] Best 30 entries add up to " + SumOfEntries(best) + " but the total is " + total + ".");
            }

            ShowTab(_tab, keepScroll);
        }

        /// <summary>Shows a list; <paramref name="keepScroll"/> false starts it from the top.</summary>
        internal void ShowTab(ProfileTab tab, bool keepScroll)
        {
            bool changed = tab != _tab;
            _tab = tab;
            _tabs.Apply((int)tab);
            if (changed)
            {
                UiSound.Play(UiCue.Select);
            }

            PlayerProfileSnapshot snapshot = _screen.Snapshot;
            bool historyTab = tab == ProfileTab.History;
            _subtitle.text = historyTab && _screen.HistoryCount > 0
                ? "长按一条记录即可删除。清空全部历史前，请先导出备份。"
                : "头像、昵称与游玩记录都保存在本机，可以导出备份";
            // Always tappable: without a backup the card it opens asks for one.
            _clearHistory.gameObject.SetActive(historyTab && snapshot != null && _screen.HistoryCount > 0);
            LibraryWidgets.TopStrip(_summary.rectTransform, 436f, historyTab ? 222f : 34f, 26f, TAB_HEIGHT);
            if (snapshot == null)
            {
                _summary.text = string.Empty;
                _list.SetItemCount(0);
                _status.ShowError(_screen.LoadError ?? "档案暂时无法读取", _screen.Reload);
                return;
            }

            int count;
            if (tab == ProfileTab.Best30)
            {
                count = snapshot.Best30.Entries.Count;
                _summary.text = count == 0
                    ? "每张谱面只取最高的一次，最多 30 张"
                    : count + " 张谱面合计 " + ProfileCopy.Number(SumOfEntries(snapshot.Best30)) + "  ·  每张取最高一次";
            }
            else
            {
                count = _screen.HistoryCount;
                _summary.text = count == 0 ? string.Empty : _screen.HistoryBackedUp ? "已备份" : "待备份";
            }

            _list.SetItemCount(count, keepScroll && !changed);
            if (count > 0)
            {
                _status.Hide();
            }
            else if (tab == ProfileTab.Best30)
            {
                _status.ShowMessage("还没有计入 Best 30 的成绩\n手动打完一首谱面，它就会出现在这里");
            }
            else
            {
                _status.ShowMessage("还没有游玩记录");
            }
        }

        /// <summary>The two cards rise in one after the other, as the page comes up.</summary>
        internal void PlayEnter()
        {
            _enter.TryCancel();
            _enter = LMotion.Create(0f, 1f, 0.62f)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => ApplyEnter(1f))
                .WithOnCancel(() => ApplyEnter(1f))
                .Bind(ApplyEnter);
        }

        public void Dispose()
        {
            _enter.TryCancel();
            _status.Dispose();
            _plate.Dispose();
        }

        private void ApplyEnter(float time)
        {
            Rise(_leftCard, _leftGroup, HomeMotion.Span(time, 0.02f, 0.45f, Ease.OutCubic));
            Rise(_rightCard, _rightGroup, HomeMotion.Span(time, 0.1f, 0.45f, Ease.OutCubic));
        }

        private static void Rise(RectTransform rect, CanvasGroup group, float progress)
        {
            group.alpha = progress;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -36f * (1f - progress));
        }

        // ---------------------------------------------------------------- rows

        private RectTransform CreateRow(RectTransform content)
        {
            var row = new ProfileRow(content);
            row.Hold.LongPressed = _screen.RequestDeleteHistory;
            _rows[row.Root] = row;
            return row.Root;
        }

        private void ReleaseRow(RectTransform cell)
        {
            if (_rows.TryGetValue(cell, out ProfileRow row))
            {
                row.Index = -1;
                row.Hold.Bind(null);
                row.Version++;
            }
        }

        private void BindRow(RectTransform cell, int index)
        {
            ProfileRow row = _rows[cell];
            row.Index = index;
            row.Version++;
            if (_tab == ProfileTab.Best30)
            {
                BindBest(row, index);
            }
            else
            {
                BindHistory(row, index);
            }
        }

        private void BindBest(ProfileRow row, int index)
        {
            IReadOnlyList<Best30Entry> entries = _screen.Snapshot?.Best30.Entries;
            if (entries == null || index >= entries.Count)
            {
                row.SetMissing();
                return;
            }

            Best30Entry entry = entries[index];
            CustomMusicScorePlayHistoryRecord play = entry.Play;
            Color32 lead = index < _podium.Length ? _podium[index] : LibraryTheme.Colors.TextMuted;
            row.SetLead((index + 1).ToString(CultureInfo.InvariantCulture), null, lead, index < 3 ? 44f : 36f);
            string when = DateTimeOffset.TryParse(play.PlayedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out DateTimeOffset played) ? "  ·  " + HomeScreen.DescribeWhen(played) : string.Empty;
            row.SetSong(ProfileCopy.Title(play), ProfileCopy.Difficulty(play), ProfileCopy.DifficultyColor(play),
                ProfileCopy.Number(play.TotalScore) + when);
            row.SetTag(ProfileCopy.AwardTag(entry.Award), ProfileCopy.AwardColor(entry.Award));
            row.SetValue(ProfileCopy.Number(entry.Points), LibraryTheme.Colors.TextPrimary, "成绩分", LibraryTheme.Colors.TextMuted);
            _screen.RequestJacket(play.ScoreKey, row);
        }

        private void BindHistory(ProfileRow row, int index)
        {
            CustomMusicScorePlayHistoryRecord play = _screen.HistoryAt(index);
            row.Hold.Bind(play);
            if (play == null)
            {
                row.SetMissing();
                return;
            }

            ProfileCopy.TryWhen(play, out string date, out string time);
            row.SetLead(date, time, LibraryTheme.Colors.TextSecondary, date.Length > 5 ? 22f : 30f);
            string mode = ProfileCopy.IsAuto(play) ? "自动演奏" : "手动演奏";
            string combo = play.TotalComboCount > 0
                ? "  ·  连击 " + ProfileCopy.Number(play.MaxCombo) + " / " + ProfileCopy.Number(play.TotalComboCount)
                : string.Empty;
            row.SetSong(ProfileCopy.Title(play), ProfileCopy.Difficulty(play), ProfileCopy.DifficultyColor(play), mode + combo);
            string tag = ProfileCopy.EndTag(play, out Color32 tagColor);
            row.SetTag(tag, tagColor);

            string caption;
            Color32 captionColor;
            if (_screen.TryStanding(play.RecordId, out int position, out long points))
            {
                caption = "Best 30 · 第 " + position + " 名 · " + ProfileCopy.Number(points) + " 分";
                captionColor = _standing;
            }
            else
            {
                caption = ProfileCopy.HistoryNote(play);
                captionColor = LibraryTheme.Colors.TextMuted;
            }

            bool finished = play.EndReason == PlayResultKind.Completed && !ProfileCopy.IsAuto(play);
            row.SetValue(ProfileCopy.Number(play.TotalScore), finished ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextSecondary,
                caption, captionColor);
            _screen.RequestJacket(play.ScoreKey, row);
        }

        // ---------------------------------------------------------------- building

        /// <summary>The page's own card: a raised surface with the home cards' light from above.</summary>
        private static RectTransform Card(RectTransform parent, string name)
        {
            RectTransform root = LibraryUi.CreateRect(name, parent);
            LibraryUi.Stretch(root);
            LibraryWidgets.Shadow(root, CARD_RADIUS, LibraryTheme.CoverShadow.BLUR, LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image surface = LibraryWidgets.Panel("Surface", root, CARD_RADIUS, HomeTheme.Colors.Card);
            LibraryUi.Stretch(surface.rectTransform);
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image highlight = LibraryWidgets.Shape("Highlight", surface.transform, HomeShapes.FadeUp(), HomeTheme.Colors.CardHighlight);
            highlight.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(highlight.rectTransform, 0f, 0f, 0f, 140f);
            RectTransform content = LibraryUi.CreateRect("Content", surface.transform);
            LibraryUi.Stretch(content);
            Image outline = LibraryWidgets.Shape("Outline", root, LibraryShapes.Ring(CARD_RADIUS, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
            return content;
        }

        private static void Divider(RectTransform card, float offset, bool fromTop)
        {
            Image line = LibraryUi.CreateImage("Divider", card, LibraryTheme.Colors.Hairline);
            line.raycastTarget = false;
            if (fromTop)
            {
                LibraryWidgets.TopStrip(line.rectTransform, PAD, PAD, offset, 1f);
            }
            else
            {
                LibraryWidgets.BottomStrip(line.rectTransform, PAD, PAD, offset, 1f);
            }
        }

        private static void Strip(RectTransform rect, float left, float right, float top, float height) =>
            LibraryWidgets.TopStrip(rect, left, right, top, height);

        private static string Count(int value) =>
            "<color=#9AA6B5>" + value.ToString(CultureInfo.InvariantCulture) + "</color>";

        private static long SumOfEntries(Best30Snapshot best)
        {
            long sum = 0;
            foreach (Best30Entry entry in best.Entries)
            {
                sum += entry.Points;
            }

            return sum;
        }
    }
}
