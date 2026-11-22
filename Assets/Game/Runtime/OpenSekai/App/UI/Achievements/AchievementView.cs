using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Layout and binding of the 成就 page. On the left, a ring with how many goals are unlocked,
    /// the two running totals behind them (hits, full-combo charts) and the latest unlocks; on the
    /// right, the filter tabs and every goal as a row: its medal with a progress ring, what it asks
    /// for, how it is counted, and where it stands (a percentage and the count, or when it was
    /// unlocked). Sizes are canvas units at the 1920×1080 reference.
    /// </summary>
    internal sealed class AchievementView : IDisposable
    {
        private const float LEFT_WIDTH = 560f;
        private const float COLUMN_GAP = 28f;
        private const float CARD_RADIUS = 30f;
        private const float PAD = 32f;
        private const float RING = 228f;
        private const float RING_TOP = 84f;
        private const float RING_STROKE = 14f;
        private const float STAT_TOP = RING_TOP + RING + 30f;
        private const float STAT_HEIGHT = 96f;
        private const float RECENT_TOP = STAT_TOP + STAT_HEIGHT + 30f;
        private const float RECENT_HEIGHT = 84f;
        private const float RECENT_GAP = 8f;
        private const float ROW_HEIGHT = 152f;
        private const float ROW_GAP = 12f;
        private const float LIST_TOP = 108f;
        private const float TAB_WIDTH = 188f;
        private const float TAB_HEIGHT = 60f;
        private const float ENTER = 0.9f;
        private const float LIST_STAGGER = 0.05f;
        private const float LIST_FADE = 0.26f;

        private readonly AchievementScreen _screen;
        private readonly RectTransform _root;
        private readonly CanvasGroup _leftGroup;
        private readonly RectTransform _leftCard;
        private readonly CanvasGroup _rightGroup;
        private readonly RectTransform _rightCard;

        private readonly Image _ringGlow;
        private readonly Image _ringArc;
        private readonly TextMeshProUGUI _count;
        private readonly TextMeshProUGUI _countTotal;
        private readonly TextMeshProUGUI _countCaption;
        private readonly Stat[] _stats = new Stat[2];
        private readonly RectTransform _recentContent;
        private readonly List<RecentRow> _recentRows = new List<RecentRow>();
        private readonly TextMeshProUGUI _recentEmpty;

        private readonly HomePageTabs _tabs;
        private readonly TextMeshProUGUI _filterCount;
        private readonly RectTransform _listContent;
        private readonly ScrollRect _listScroll;
        private readonly List<Row> _rows = new List<Row>();
        private readonly TextMeshProUGUI _listEmpty;

        private int _unlocked;
        private int _total;
        private int _shownCount = -1;
        private int _rowsInUse;
        private MotionHandle _enter;
        private MotionHandle _listMotion;

        internal AchievementView(RectTransform parent, AchievementScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("AchievementPage", parent);
            LibraryUi.Stretch(_root);

            ServerWidgets.TopBar(_root, "成就", screen.Back, screen.OpenSettings, out _, out TextMeshProUGUI subtitle, out _);
            subtitle.text = "从游玩记录里累计的目标；解锁时间记在第一次达成的那一局";

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", _root);
            LibraryWidgets.Inset(body, margin, 48f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 top = new Vector2(0.5f, 1f);

            // ---------------------------------------------------------------- the summary
            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _leftGroup = left.gameObject.AddComponent<CanvasGroup>();
            _leftCard = left;
            RectTransform summary = Card(left, "SummaryCard");
            TextMeshProUGUI summaryTitle = LibraryWidgets.Label("Title", summary, "解锁进度", 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(summaryTitle.rectTransform, PAD, PAD, 26f, 40f);

            RectTransform ring = LibraryUi.CreateRect("Ring", summary);
            LibraryWidgets.Place(ring, top, top, new Vector2(0f, -RING_TOP), new Vector2(RING, RING));
            _ringGlow = LibraryUi.CreateImage("Glow", ring, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, 50));
            _ringGlow.sprite = LibraryShapes.Glow();
            _ringGlow.raycastTarget = false;
            LibraryWidgets.Place(_ringGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RING * 1.9f, RING * 1.9f));
            Image track = LibraryUi.CreateImage("Track", ring, HomeTheme.Colors.Chip);
            track.sprite = LibraryShapes.Ring(RING * 0.5f, RING_STROKE);
            track.raycastTarget = false;
            LibraryUi.Stretch(track.rectTransform);
            _ringArc = LibraryUi.CreateImage("Arc", ring, HomeTheme.Colors.Gold);
            _ringArc.sprite = LibraryShapes.Ring(RING * 0.5f, RING_STROKE);
            _ringArc.raycastTarget = false;
            _ringArc.type = Image.Type.Filled;
            _ringArc.fillMethod = Image.FillMethod.Radial360;
            _ringArc.fillOrigin = (int)Image.Origin360.Top;
            _ringArc.fillClockwise = true;
            LibraryUi.Stretch(_ringArc.rectTransform);
            _count = LibraryWidgets.Label("Count", ring, "0", 96f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.BottomRight);
            LibraryUi.UseCondensedFont(_count);
            _count.overflowMode = TextOverflowModes.Overflow;
            _countTotal = LibraryWidgets.Label("Total", ring, "/ 0", 38f, LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            LibraryUi.UseCondensedFont(_countTotal);
            _countTotal.overflowMode = TextOverflowModes.Overflow;
            _countCaption = LibraryWidgets.Label("Caption", ring, "已解锁", 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.Place(_countCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(RING, 26f));

            float statWidth = (LEFT_WIDTH - PAD * 2f - 16f) * 0.5f;
            for (int i = 0; i < _stats.Length; i++)
            {
                _stats[i] = new Stat(summary, "Stat" + i);
                LibraryWidgets.Place(_stats[i].Root, topLeft, topLeft, new Vector2(PAD + i * (statWidth + 16f), -STAT_TOP), new Vector2(statWidth, STAT_HEIGHT));
            }

            DanWidgets.Caption(summary, "最近解锁", PAD + 2f, RECENT_TOP);
            RectTransform recentArea = LibraryUi.CreateRect("RecentArea", summary);
            LibraryWidgets.Inset(recentArea, 16f, 16f, 16f, RECENT_TOP + 38f);
            _recentContent = PackWidgets.Scroll(recentArea, out _);
            _recentEmpty = PackWidgets.Paragraph(recentArea, "Empty", string.Empty, 19f, LibraryTheme.Colors.TextMuted);
            _recentEmpty.alignment = TextAlignmentOptions.Top;
            LibraryWidgets.Inset(_recentEmpty.rectTransform, 24f, 0f, 24f, 18f);

            // ---------------------------------------------------------------- every goal
            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _rightGroup = right.gameObject.AddComponent<CanvasGroup>();
            _rightCard = right;
            RectTransform list = Card(right, "ListCard");
            var names = new string[AchievementCopy.CategoryNames.Length];
            Array.Copy(AchievementCopy.CategoryNames, names, names.Length);
            _tabs = new HomePageTabs(list, names, index => screen.SetFilter((AchievementCategory)index), TAB_WIDTH, TAB_HEIGHT, 20f);
            LibraryWidgets.Place(_tabs.Root, topLeft, topLeft, new Vector2(28f, -26f), _tabs.Root.sizeDelta);
            _filterCount = LibraryWidgets.Label("FilterCount", list, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Right);
            LibraryWidgets.TopStrip(_filterCount.rectTransform, 28f + _tabs.Root.sizeDelta.x + 20f, PAD, 40f, 30f);
            RectTransform listArea = LibraryUi.CreateRect("ListArea", list);
            LibraryWidgets.Inset(listArea, 16f, 16f, 16f, LIST_TOP);
            _listContent = PackWidgets.Scroll(listArea, out _listScroll);
            _listEmpty = PackWidgets.Paragraph(listArea, "Empty", string.Empty, 20f, LibraryTheme.Colors.TextMuted);
            _listEmpty.alignment = TextAlignmentOptions.Center;
            LibraryWidgets.Inset(_listEmpty.rectTransform, 24f, 0f, 24f, 120f);
        }

        internal RectTransform Root => _root;

        /// <summary>Redraws the summary and the list from the screen's goals.</summary>
        internal void Bind()
        {
            IReadOnlyList<AchievementProgress> items = _screen.Items;
            _total = items.Count;
            _unlocked = 0;
            var counts = new int[AchievementCopy.CategoryNames.Length];
            foreach (AchievementProgress item in items)
            {
                if (AchievementCopy.IsUnlocked(item)) _unlocked++;
                counts[0]++;
                int category = (int)AchievementCopy.For(item).Category;
                if (category > 0 && category < counts.Length) counts[category]++;
            }

            for (int i = 0; i < counts.Length; i++)
            {
                _tabs.SetLabel(i, AchievementCopy.CategoryNames[i] + "  " + counts[i]);
            }

            _tabs.Apply((int)_screen.Filter);
            _countTotal.text = "/ " + _total.ToString(CultureInfo.InvariantCulture);
            _shownCount = -1;
            ApplySummary(1f);
            BindStats(items);
            BindRecent(items);
            BindList(false);
        }

        /// <summary>Redraws the list for the screen's filter; <paramref name="animate"/> fades the rows in one after another.</summary>
        internal void BindList(bool animate)
        {
            _tabs.Apply((int)_screen.Filter);
            List<AchievementProgress> items = _screen.Filtered();
            int unlocked = 0;
            foreach (AchievementProgress item in items)
            {
                if (AchievementCopy.IsUnlocked(item)) unlocked++;
            }

            _filterCount.text = _screen.LoadError != null || items.Count == 0 ? string.Empty
                : "已解锁 " + unlocked + " / " + items.Count;
            while (_rows.Count < items.Count)
            {
                var row = new Row(_listContent, _rows.Count);
                row.Root.anchoredPosition = new Vector2(0f, -_rows.Count * (ROW_HEIGHT + ROW_GAP));
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < items.Count;
                _rows[i].Root.gameObject.SetActive(used);
                if (used) _rows[i].Bind(items[i]);
            }

            _rowsInUse = items.Count;
            _listContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, items.Count * (ROW_HEIGHT + ROW_GAP) - ROW_GAP));
            _listContent.anchoredPosition = Vector2.zero;
            _listScroll.StopMovement();
            bool empty = _screen.LoadError != null || items.Count == 0;
            _listEmpty.gameObject.SetActive(empty);
            _listEmpty.text = _screen.LoadError ?? (_screen.Items.Count == 0 ? "暂时没有可以挑战的成就" : "这个分类里还没有成就");

            _listMotion.TryCancel();
            if (!animate)
            {
                ApplyList(LIST_FADE + _rowsInUse * LIST_STAGGER);
                return;
            }

            float length = LIST_FADE + _rowsInUse * LIST_STAGGER;
            ApplyList(0f);
            _listMotion = LMotion.Create(0f, length, length)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (time, view) => view.ApplyList(time));
        }

        internal void PlayEnter()
        {
            _enter.TryCancel();
            ApplyEnter(0f);
            _enter = LMotion.Create(0f, ENTER, ENTER)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnCancel(() => ApplyEnter(ENTER))
                .Bind(this, static (time, view) => view.ApplyEnter(time));
            BindList(true);
        }

        /// <summary>Shows the page where its opening ends.</summary>
        internal void SkipEnter()
        {
            _enter.TryCancel();
            _enter = default;
            ApplyEnter(ENTER);
        }

        public void Dispose()
        {
            _enter.TryCancel();
            _listMotion.TryCancel();
        }

        private void ApplyEnter(float time)
        {
            Rise(_leftCard, _leftGroup, HomeMotion.Span(time, 0.02f, 0.45f, Ease.OutCubic));
            Rise(_rightCard, _rightGroup, HomeMotion.Span(time, 0.1f, 0.45f, Ease.OutCubic));
            ApplySummary(HomeMotion.Span(time, 0.18f, 0.7f, Ease.OutCubic));
        }

        /// <summary>The ring and its count, filled to <paramref name="progress"/> of where they end.</summary>
        private void ApplySummary(float progress)
        {
            float fraction = _total > 0 ? (float)_unlocked / _total : 0f;
            _ringArc.fillAmount = fraction * progress;
            _ringArc.enabled = _unlocked > 0;
            Color glow = _ringGlow.color;
            glow.a = (_unlocked > 0 ? 0.2f + 0.25f * fraction : 0f) * progress;
            _ringGlow.color = glow;
            int shown = Mathf.RoundToInt(_unlocked * progress);
            if (shown == _shownCount) return;
            _shownCount = shown;
            _count.text = _screen.LoadError != null ? "—" : shown.ToString(CultureInfo.InvariantCulture);
            // "2" and "/ 5" sit side by side, centered together a little above the middle.
            float countWidth = Mathf.Ceil(_count.GetPreferredValues(_count.text).x);
            float totalWidth = Mathf.Ceil(_countTotal.GetPreferredValues(_countTotal.text).x);
            float leftEdge = -(countWidth + 10f + totalWidth) * 0.5f;
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_count.rectTransform, center, new Vector2(0f, 0f), new Vector2(leftEdge, -22f), new Vector2(countWidth + 4f, 110f));
            LibraryWidgets.Place(_countTotal.rectTransform, center, new Vector2(0f, 0f), new Vector2(leftEdge + countWidth + 10f, -12f),
                new Vector2(totalWidth + 4f, 60f));
        }

        private void ApplyList(float time)
        {
            for (int i = 0; i < _rowsInUse; i++)
            {
                _rows[i].Apply(HomeMotion.Span(time, i * LIST_STAGGER, LIST_FADE, Ease.OutCubic));
            }
        }

        private static void Rise(RectTransform rect, CanvasGroup group, float progress)
        {
            group.alpha = progress;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -36f * (1f - progress));
        }

        /// <summary>The running totals behind the goals: hits, and full-combo charts at the lowest level a goal asks for.</summary>
        private void BindStats(IReadOnlyList<AchievementProgress> items)
        {
            AchievementProgress hits = null;
            AchievementProgress charts = null;
            foreach (AchievementProgress item in items)
            {
                if (item.Metric == AchievementMetric.ManualHits && hits == null) hits = item;
                if (item.Metric == AchievementMetric.FullComboCharts && (charts == null || item.MinimumLevel < charts.MinimumLevel)) charts = item;
            }

            _stats[0].Bind(hits != null ? AchievementCopy.Number(hits.Current) : "—", "累计击打", AchievementCopy.HitsTint);
            string level = charts != null && charts.MinimumLevel > 0 ? "Lv." + charts.MinimumLevel + "+ " : string.Empty;
            _stats[1].Bind(charts != null ? AchievementCopy.Number(charts.Current) : "—", level + "全连谱面", AchievementCopy.FullComboTint);
        }

        /// <summary>The unlocked goals, latest first.</summary>
        private void BindRecent(IReadOnlyList<AchievementProgress> items)
        {
            var unlocked = new List<AchievementProgress>();
            foreach (AchievementProgress item in items)
            {
                if (AchievementCopy.IsUnlocked(item)) unlocked.Add(item);
            }

            // Latest first; the same moment keeps the backend's order.
            var order = new Dictionary<AchievementProgress, int>();
            for (int i = 0; i < unlocked.Count; i++) order[unlocked[i]] = i;
            unlocked.Sort((a, b) =>
            {
                int byTime = b.UnlockedAtUtc.Value.CompareTo(a.UnlockedAtUtc.Value);
                return byTime != 0 ? byTime : order[a].CompareTo(order[b]);
            });

            while (_recentRows.Count < unlocked.Count)
            {
                var row = new RecentRow(_recentContent, _recentRows.Count);
                row.Root.anchoredPosition = new Vector2(0f, -_recentRows.Count * (RECENT_HEIGHT + RECENT_GAP));
                _recentRows.Add(row);
            }

            for (int i = 0; i < _recentRows.Count; i++)
            {
                bool used = i < unlocked.Count;
                _recentRows[i].Root.gameObject.SetActive(used);
                if (used) _recentRows[i].Bind(unlocked[i]);
            }

            _recentContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, unlocked.Count * (RECENT_HEIGHT + RECENT_GAP) - RECENT_GAP));
            _recentEmpty.gameObject.SetActive(unlocked.Count == 0);
            _recentEmpty.text = _screen.LoadError ?? "还没有解锁的成就。\n打一局或挑战一个段位，进度就会累计起来";
        }

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

        /// <summary>A running total under the ring: a big number with what it counts.</summary>
        private sealed class Stat
        {
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _caption;
            private readonly Image _accent;

            internal Stat(RectTransform parent, string name)
            {
                Image tile = LibraryWidgets.Panel(name, parent, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                Root = tile.rectTransform;
                _accent = LibraryWidgets.Panel("Accent", Root, 3f, Color.white);
                LibraryWidgets.Place(_accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(5f, STAT_HEIGHT - 40f));
                _value = LibraryWidgets.Label("Value", Root, "0", 44f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_value);
                _value.overflowMode = TextOverflowModes.Overflow;
                _value.alignment = TextAlignmentOptions.BottomLeft;
                LibraryWidgets.TopStrip(_value.rectTransform, 34f, 12f, 4f, 54f);
                _caption = LibraryWidgets.Label("Caption", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.BottomStrip(_caption.rectTransform, 36f, 12f, 12f, 24f);
            }

            internal RectTransform Root { get; }

            internal void Bind(string value, string caption, Color32 tint)
            {
                _value.text = value;
                _caption.text = caption;
                _accent.color = tint;
            }
        }

        /// <summary>One unlocked goal in the summary: a small medal, its name and when.</summary>
        private sealed class RecentRow
        {
            private readonly AchievementMedal _medal;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _goal;
            private readonly TextMeshProUGUI _when;

            internal RecentRow(RectTransform content, int index)
            {
                Root = LibraryUi.CreateRect("Recent" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, RECENT_HEIGHT);
                Image background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(background.rectTransform);
                _medal = new AchievementMedal(Root, "Medal", 60f, withRing: false);
                LibraryWidgets.Place(_medal.Root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f + 30f, 0f), new Vector2(60f, 60f));
                _title = LibraryWidgets.Label("Title", Root, string.Empty, 21f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_title.rectTransform, 96f, 150f, 14f, 30f);
                _goal = LibraryWidgets.Label("Goal", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(_goal.rectTransform, 96f, 18f, 46f, 24f);
                _when = LibraryWidgets.Label("When", Root, string.Empty, 16f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                    TextAlignmentOptions.Right);
                LibraryWidgets.Place(_when.rectTransform, Vector2.one, Vector2.one, new Vector2(-18f, -16f), new Vector2(150f, 26f));
            }

            internal RectTransform Root { get; }

            internal void Bind(AchievementProgress progress)
            {
                AchievementLook look = AchievementCopy.For(progress);
                _medal.Bind(look, true, 1f);
                _title.text = look.Title;
                _goal.text = look.Goal;
                _when.text = HomeScreen.DescribeWhen(progress.UnlockedAtUtc.Value);
            }
        }

        /// <summary>One goal: its medal and ring, what it asks for and how it is counted, and where it stands.</summary>
        private sealed class Row
        {
            private const float MEDAL = 112f;
            private const float TEXT_LEFT = 22f + MEDAL + 26f;
            private const float STATUS_WIDTH = 280f;
            private const float TEXT_RIGHT = 22f + STATUS_WIDTH + 28f;
            private const float EDGE = 24f;

            private readonly CanvasGroup _group;
            private readonly RectTransform _inner;
            private readonly Image _background;
            private readonly Image _accent;
            private readonly AchievementMedal _medal;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _latin;
            private readonly TextMeshProUGUI _goal;
            private readonly TextMeshProUGUI _rule;
            private readonly TextMeshProUGUI _percent;
            private readonly TextMeshProUGUI _pending;
            private readonly RectTransform _unlockedTag;
            private readonly RectTransform _bar;
            private readonly Image _barFill;
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _extra;

            internal Row(RectTransform content, int index)
            {
                Root = LibraryUi.CreateRect("Achievement" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, ROW_HEIGHT);
                _inner = LibraryUi.CreateRect("Inner", Root);
                LibraryUi.Stretch(_inner);
                _group = _inner.gameObject.AddComponent<CanvasGroup>();
                _background = LibraryWidgets.Panel("Background", _inner, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                _accent = LibraryWidgets.Panel("Accent", _inner, 3f, Color.clear);
                LibraryWidgets.Place(_accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(6f, ROW_HEIGHT - 48f));

                _medal = new AchievementMedal(_inner, "Medal", MEDAL);
                LibraryWidgets.Place(_medal.Root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f + MEDAL * 0.5f, 0f), new Vector2(MEDAL, MEDAL));

                _title = LibraryWidgets.Label("Title", _inner, string.Empty, 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_title.rectTransform, TEXT_LEFT, TEXT_RIGHT, 20f, 36f);
                _latin = LibraryWidgets.Label("Latin", _inner, string.Empty, 15f, LibraryTheme.Colors.TextMuted);
                LibraryUi.UseSpacedCondensedFont(_latin, 3f);
                _goal = LibraryWidgets.Label("Goal", _inner, string.Empty, 19f, LibraryTheme.Colors.TextSecondary);
                LibraryWidgets.TopStrip(_goal.rectTransform, TEXT_LEFT, TEXT_RIGHT, 60f, 28f);
                _rule = PackWidgets.Paragraph(_inner, "Rule", string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                _rule.lineSpacing = 0f;
                LibraryWidgets.Inset(_rule.rectTransform, TEXT_LEFT, 12f, TEXT_RIGHT, 94f);

                Vector2 corner = Vector2.one;
                _percent = LibraryWidgets.Label("Percent", _inner, string.Empty, 44f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.TopRight);
                LibraryUi.UseCondensedFont(_percent);
                _percent.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Place(_percent.rectTransform, corner, corner, new Vector2(-EDGE, -14f), new Vector2(STATUS_WIDTH, 52f));
                _pending = LibraryWidgets.Label("Pending", _inner, "未达成", 21f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                    TextAlignmentOptions.TopRight);
                LibraryWidgets.Place(_pending.rectTransform, corner, corner, new Vector2(-EDGE, -26f), new Vector2(STATUS_WIDTH, 30f));
                _unlockedTag = PackWidgets.Tag(_inner, "已解锁", AchievementCopy.FullComboTint, out float tagWidth, 34f, true);
                LibraryWidgets.Place(_unlockedTag, corner, corner, new Vector2(-EDGE, -24f), new Vector2(tagWidth, 34f));

                Image bar = LibraryWidgets.Panel("Bar", _inner, 5f, HomeTheme.Colors.Chip);
                _bar = bar.rectTransform;
                LibraryWidgets.Place(_bar, corner, corner, new Vector2(-EDGE, -76f), new Vector2(STATUS_WIDTH, 10f));
                _barFill = LibraryWidgets.Panel("Fill", _bar, 5f, Color.white);
                _barFill.rectTransform.anchorMin = Vector2.zero;
                _barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                _barFill.rectTransform.offsetMin = Vector2.zero;
                _barFill.rectTransform.offsetMax = Vector2.zero;
                _value = LibraryWidgets.Label("Value", _inner, string.Empty, 19f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                    TextAlignmentOptions.TopRight);
                LibraryWidgets.Place(_value.rectTransform, corner, corner, new Vector2(-EDGE, -96f), new Vector2(STATUS_WIDTH, 28f));
                _extra = LibraryWidgets.Label("Extra", _inner, string.Empty, 16f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                    TextAlignmentOptions.TopRight);
                LibraryWidgets.Place(_extra.rectTransform, corner, corner, new Vector2(-EDGE, -122f), new Vector2(STATUS_WIDTH, 24f));
            }

            internal RectTransform Root { get; }

            internal void Bind(AchievementProgress progress)
            {
                AchievementLook look = AchievementCopy.For(progress);
                bool unlocked = AchievementCopy.IsUnlocked(progress);
                bool oneShot = AchievementCopy.IsOneShot(progress);
                float fraction = AchievementCopy.Fraction(progress);
                Color32 tint = look.Tint;
                _medal.Bind(look, unlocked, oneShot && !unlocked ? 0f : fraction);
                _background.color = unlocked ? HomeTheme.Colors.WithAlpha(tint, 22) : LibraryTheme.Colors.Control;
                _accent.color = unlocked ? tint : (Color32)Color.clear;

                _title.text = look.Title;
                _title.color = unlocked ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextSecondary;
                _latin.text = look.Latin;
                _latin.color = unlocked ? HomeTheme.Colors.WithAlpha(tint, 220) : LibraryTheme.Colors.TextMuted;
                float titleWidth = Mathf.Ceil(_title.GetPreferredValues(look.Title).x);
                LibraryWidgets.TopStrip(_latin.rectTransform, TEXT_LEFT + titleWidth + 14f, TEXT_RIGHT, 30f, 22f);
                _goal.text = look.Goal;
                _rule.text = look.Rule;

                _unlockedTag.gameObject.SetActive(unlocked);
                PackWidgets.SetTag(_unlockedTag, "已解锁", tint, true);
                _percent.gameObject.SetActive(!unlocked && !oneShot);
                _pending.gameObject.SetActive(!unlocked && oneShot);
                _bar.gameObject.SetActive(unlocked || !oneShot);
                _barFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
                _barFill.color = unlocked ? tint : HomeTheme.Colors.WithAlpha(tint, 200);
                _barFill.enabled = fraction > 0f;
                if (unlocked)
                {
                    _value.text = AchievementCopy.When(progress.UnlockedAtUtc.Value) + " 解锁";
                    _value.color = LibraryTheme.Colors.TextSecondary;
                    string total = AchievementCopy.Total(progress);
                    _extra.text = total ?? string.Empty;
                    _extra.gameObject.SetActive(total != null);
                    return;
                }

                _percent.text = AchievementCopy.Percent(progress).ToString(CultureInfo.InvariantCulture) + "<size=55%>%</size>";
                _percent.color = progress.Current > 0 ? (Color32)Color.Lerp(tint, Color.white, 0.25f) : LibraryTheme.Colors.TextMuted;
                _value.text = oneShot ? string.Empty : AchievementCopy.Count(progress);
                _value.color = LibraryTheme.Colors.TextSecondary;
                _extra.gameObject.SetActive(false);
            }

            /// <param name="shown">0 to 1: the row slides in from the right and fades in.</param>
            internal void Apply(float shown)
            {
                _group.alpha = shown;
                _inner.anchoredPosition = new Vector2(32f * (1f - shown), 0f);
            }
        }
    }
}
