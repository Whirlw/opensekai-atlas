using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Layout and binding of the 段位 page. On the left, every saved course as its plate with how
    /// far the player got; on the right, the chosen one: a large plate, three numbers (songs,
    /// starting life, attempts), the songs in order, what each judgment costs and the last few
    /// attempts, and the buttons along the bottom. With no course yet the right side explains what a
    /// 段位 is and offers 新建段位 and 粘贴段位. Sizes are canvas units at the 1920×1080 reference.
    /// </summary>
    internal sealed class DanView : IDisposable
    {
        private const float LEFT_WIDTH = 640f;
        private const float COLUMN_GAP = 28f;
        private const float CARD_RADIUS = 30f;
        private const float PAD = 32f;
        private const float ROW_HEIGHT = 112f;
        private const float ROW_GAP = 10f;
        private const float LINE_HEIGHT = 74f;
        private const float ATTEMPT_HEIGHT = 60f;
        private const int RECENT_ATTEMPTS = 5;
        private const float FOOTER = 120f;

        private readonly DanScreen _screen;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _subtitle;
        private readonly CanvasGroup _leftGroup;
        private readonly RectTransform _leftCard;
        private readonly CanvasGroup _rightGroup;
        private readonly RectTransform _rightCard;
        private readonly TextMeshProUGUI _listCount;
        private readonly RectTransform _listContent;
        private readonly List<ListRow> _rows = new List<ListRow>();
        private readonly TextMeshProUGUI _listEmpty;

        private readonly RectTransform _detail;
        private readonly MedalPlate _plate;
        private readonly RectTransform _statusTag;
        private readonly RectTransform _wornTag;
        private readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _statCaptions = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI _best;
        private readonly RectTransform _scrollContent;
        private readonly ScrollRect _scroll;
        private readonly TextMeshProUGUI _songsCaption;
        private readonly List<DanSongLine> _songLines = new List<DanSongLine>();
        private readonly TextMeshProUGUI _damageCaption;
        private readonly DanJudgeTable _damage;
        private readonly TextMeshProUGUI _attemptsCaption;
        private readonly List<DanSongLine> _attemptLines = new List<DanSongLine>();
        private readonly TextMeshProUGUI _attemptsEmpty;
        private readonly Button _start;
        private readonly Button _equip;

        private readonly RectTransform _welcome;
        private readonly MedalPlate _welcomePlate;
        private readonly TextMeshProUGUI _welcomeText;
        private MotionHandle _enter;

        internal DanView(RectTransform parent, DanScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("DanPage", parent);
            LibraryUi.Stretch(_root);

            RectTransform bar = ServerWidgets.TopBar(_root, "段位", screen.Back, screen.OpenSettings, out _, out _subtitle, out float rightInset);
            _subtitle.text = "多首谱面连续挑战，生命延续到最后一首。通过即可取得段位牌";
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            const float BUTTON_WIDTH = 212f;
            Button paste = ServerWidgets.LabelledButton(bar, "Paste", "粘贴段位", LibraryIcon.Download, () => screen.OpenPaste(), BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Open);
            LibraryWidgets.Outline((Image)paste.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)paste.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-rightInset, 0f),
                new Vector2(BUTTON_WIDTH, height));
            Button create = ServerWidgets.LabelledButton(bar, "Create", "新建段位", LibraryIcon.Add, screen.OpenCreate, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Open);
            LibraryWidgets.Place((RectTransform)create.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-(rightInset + BUTTON_WIDTH + 16f), 0f), new Vector2(BUTTON_WIDTH, height));

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", _root);
            LibraryWidgets.Inset(body, margin, 48f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            Vector2 topLeft = new Vector2(0f, 1f);

            // ---------------------------------------------------------------- the list
            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _leftGroup = left.gameObject.AddComponent<CanvasGroup>();
            _leftCard = left;
            RectTransform listCard = Card(left, "ListCard");
            TextMeshProUGUI listTitle = LibraryWidgets.Label("Title", listCard, "我的段位", 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(listTitle.rectTransform, PAD, PAD, 26f, 40f);
            _listCount = LibraryWidgets.Label("Count", listCard, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Right);
            LibraryWidgets.TopStrip(_listCount.rectTransform, 200f, PAD, 32f, 30f);
            RectTransform listArea = LibraryUi.CreateRect("ListArea", listCard);
            LibraryWidgets.Inset(listArea, 16f, 16f, 16f, 84f);
            _listContent = PackWidgets.Scroll(listArea, out _);
            _listEmpty = PackWidgets.Paragraph(listArea, "Empty", "还没有段位。\n新建一个，或粘贴好友分享的段位文本。", 20f, LibraryTheme.Colors.TextMuted);
            _listEmpty.alignment = TextAlignmentOptions.Center;
            LibraryWidgets.Inset(_listEmpty.rectTransform, 24f, 0f, 24f, 120f);

            // ---------------------------------------------------------------- the chosen course
            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _rightGroup = right.gameObject.AddComponent<CanvasGroup>();
            _rightCard = right;
            RectTransform detailCard = Card(right, "DetailCard");
            _detail = LibraryUi.CreateRect("Detail", detailCard);
            LibraryUi.Stretch(_detail);

            _plate = new MedalPlate(_detail, "Plate", new Vector2(520f, 112f), glow: true);
            LibraryWidgets.Place(_plate.Root, topLeft, topLeft, new Vector2(PAD, -PAD), _plate.Root.sizeDelta);
            float statsLeft = PAD + 520f + 40f;
            _statusTag = PackWidgets.Tag(_detail, "尚未挑战", LibraryTheme.Colors.TextMuted, out _, 34f);
            LibraryWidgets.Place(_statusTag, topLeft, topLeft, new Vector2(statsLeft, -PAD), _statusTag.sizeDelta);
            _wornTag = PackWidgets.Tag(_detail, "佩戴中", HomeTheme.Colors.Violet, out _, 34f, true);
            LibraryWidgets.Place(_wornTag, topLeft, topLeft, new Vector2(statsLeft, -PAD), _wornTag.sizeDelta);
            string[] captions = { "曲目数", "初始生命", "挑战次数" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform stat = LibraryUi.CreateRect("Stat" + i, _detail);
                LibraryWidgets.Place(stat, topLeft, topLeft, new Vector2(statsLeft + i * 170f, -(PAD + 44f)), new Vector2(160f, 84f));
                _statValues[i] = LibraryWidgets.Label("Value", stat, "0", 56f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_statValues[i]);
                _statValues[i].overflowMode = TextOverflowModes.Overflow;
                _statValues[i].alignment = TextAlignmentOptions.BottomLeft;
                LibraryWidgets.TopStrip(_statValues[i].rectTransform, 0f, 0f, -10f, 66f);
                _statCaptions[i] = LibraryWidgets.Label("Caption", stat, captions[i], 17f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.BottomStrip(_statCaptions[i].rectTransform, 2f, 0f, 0f, 24f);
            }

            _best = LibraryWidgets.Label("Best", _detail, string.Empty, 18f, LibraryTheme.Colors.TextSecondary);
            _best.richText = false;
            LibraryWidgets.TopStrip(_best.rectTransform, statsLeft, PAD, PAD + 134f, 28f);

            RectTransform scrollArea = LibraryUi.CreateRect("Scroll", _detail);
            LibraryWidgets.Inset(scrollArea, PAD - 8f, FOOTER, PAD - 8f, PAD + 178f);
            _scrollContent = PackWidgets.Scroll(scrollArea, out _scroll);
            _songsCaption = DanWidgets.Caption(_scrollContent, "曲目顺序", 8f, 0f);
            _damageCaption = DanWidgets.Caption(_scrollContent, "每个判定的扣血", 8f, 0f);
            _damage = new DanJudgeTable(_scrollContent, "Damage", 28f);
            _attemptsCaption = DanWidgets.Caption(_scrollContent, "最近挑战", 8f, 0f);
            _attemptsEmpty = LibraryWidgets.Label("AttemptsEmpty", _scrollContent, "还没有挑战记录", 18f, LibraryTheme.Colors.TextMuted);

            Image footerLine = LibraryUi.CreateImage("FooterLine", _detail, LibraryTheme.Colors.Hairline);
            footerLine.raycastTarget = false;
            LibraryWidgets.BottomStrip(footerLine.rectTransform, PAD, PAD, FOOTER - 8f, 1f);
            float buttonHeight = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT - 4f;
            _start = ServerWidgets.LabelledButton(_detail, "Start", "开始挑战", LibraryIcon.Play, screen.StartSelected, 300f, buttonHeight,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None, 24f);
            LibraryWidgets.Place((RectTransform)_start.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-PAD, 18f),
                new Vector2(300f, buttonHeight));
            float small = 64f;
            FooterButton("Copy", "复制分享", LibraryIcon.Export, screen.CopyShare, 0, small);
            FooterButton("Edit", "编辑", LibraryIcon.Settings, screen.OpenEdit, 1, small);
            _equip = FooterButton("Equip", "佩戴", LibraryIcon.Tag, screen.ToggleEquip, 2, small);
            Button delete = FooterButton("Delete", "删除", LibraryIcon.Trash, screen.RequestDelete, 3, small);
            ServerWidgets.SetLabelledButton(delete, "删除", LibraryIcon.Trash, LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger);

            // ---------------------------------------------------------------- no course yet
            _welcome = LibraryUi.CreateRect("Welcome", detailCard);
            LibraryUi.Stretch(_welcome);
            _welcomePlate = new MedalPlate(_welcome, "Plate", new Vector2(560f, 116f), glow: true);
            LibraryWidgets.Place(_welcomePlate.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), _welcomePlate.Root.sizeDelta);
            _welcomePlate.SetLook(new MedalLook("你的第一块段位牌", "star", null, DanStyles.ParseColor("#B692FF"), 0, null, true));
            _welcomeText = PackWidgets.Paragraph(_welcome, "Text",
                "段位把几首谱面连成一场挑战：生命只在开始时给一次，每个判定按扣血表扣除，打完最后一首仍有生命就算通过。\n"
                + "挑选谱面、设定生命与扣血、设计段位牌，再把分享文本发给好友，大家挑战的就是同一个段位。",
                21f, LibraryTheme.Colors.TextSecondary);
            _welcomeText.alignment = TextAlignmentOptions.Top;
            LibraryWidgets.Place(_welcomeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 50f), new Vector2(820f, 160f));
            Button welcomeCreate = ServerWidgets.LabelledButton(_welcome, "Create", "新建段位", LibraryIcon.Add, screen.OpenCreate, 260f, 84f,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Open, 22f);
            LibraryWidgets.Place((RectTransform)welcomeCreate.transform, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(-10f, -140f),
                new Vector2(260f, 84f));
            Button welcomePaste = ServerWidgets.LabelledButton(_welcome, "Paste", "粘贴段位", LibraryIcon.Download, () => screen.OpenPaste(), 260f, 84f,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Open, 22f);
            LibraryWidgets.Outline((Image)welcomePaste.targetGraphic, 42f);
            LibraryWidgets.Place((RectTransform)welcomePaste.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(10f, -140f),
                new Vector2(260f, 84f));
        }

        internal RectTransform Root => _root;

        /// <summary>Redraws the list and the chosen course from the screen's records.</summary>
        internal void Bind()
        {
            IReadOnlyList<DanRecord> records = _screen.Records;
            int earned = 0;
            foreach (DanRecord record in records)
            {
                if (record.Earned) earned++;
            }

            _listCount.text = records.Count == 0 ? string.Empty : records.Count + " 个  ·  已通过 " + earned + " 个";
            while (_rows.Count < records.Count)
            {
                var row = new ListRow(_listContent, _rows.Count, _screen);
                row.Root.anchoredPosition = new Vector2(0f, -_rows.Count * (ROW_HEIGHT + ROW_GAP));
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < records.Count;
                _rows[i].Root.gameObject.SetActive(used);
                if (used) _rows[i].Bind(records[i]);
            }

            _listContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, records.Count * (ROW_HEIGHT + ROW_GAP) - ROW_GAP));
            _listEmpty.gameObject.SetActive(records.Count == 0);
            if (_screen.LoadError != null)
            {
                _listEmpty.gameObject.SetActive(true);
                _listEmpty.text = _screen.LoadError;
            }
            else
            {
                _listEmpty.text = "还没有段位。\n新建一个，或粘贴好友分享的段位文本。";
            }

            BindSelection();
        }

        /// <summary>Redraws which row is chosen and the right side.</summary>
        internal void BindSelection()
        {
            DanRecord selected = _screen.Selected;
            foreach (ListRow row in _rows) row.SetSelected(row.Record != null && row.Record == selected);
            _detail.gameObject.SetActive(selected != null);
            _welcome.gameObject.SetActive(selected == null);
            if (selected == null) return;
            BindDetail(selected);
        }

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
            _plate.Dispose();
            _welcomePlate.Dispose();
            foreach (ListRow row in _rows) row.Dispose();
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

        private void BindDetail(DanRecord record)
        {
            DanDefinition definition = record.Definition;
            Color32 tint = DanStyles.ParseColor(definition.Color);
            _plate.SetLook(MedalLook.Of(definition));
            _plate.SetLocked(!record.Earned);
            float statsLeft = PAD + 520f + 40f;
            string status = record.Earned ? "已通过" : record.Attempts.Count > 0 ? "未通过" : "尚未挑战";
            PackWidgets.SetTag(_statusTag, status, record.Earned ? DanCopy.Pass : record.Attempts.Count > 0 ? LibraryTheme.Colors.TextSecondary
                : LibraryTheme.Colors.TextMuted);
            _wornTag.gameObject.SetActive(record.Equipped);
            _wornTag.anchoredPosition = new Vector2(statsLeft + _statusTag.sizeDelta.x + 10f, -PAD);
            _statValues[0].text = definition.Songs.Count.ToString(CultureInfo.InvariantCulture);
            _statValues[1].text = DanCopy.Number(definition.InitialLife);
            _statValues[2].text = record.Attempts.Count.ToString(CultureInfo.InvariantCulture);
            _best.text = record.Earned
                ? "通过 " + record.Passes + " 次  ·  最多剩余生命 " + DanCopy.Number(record.BestLife) + " / " + DanCopy.Number(definition.InitialLife)
                : record.Attempts.Count > 0 ? "最远完成 " + record.FurthestCleared + " / " + definition.Songs.Count + " 首" : "开打前检查所有曲目是否就绪";

            float top = 0f;
            _songsCaption.rectTransform.anchoredPosition = new Vector2(_songsCaption.rectTransform.anchoredPosition.x, -top);
            top += 34f;
            IReadOnlyList<DanSongInfo> songs = _screen.SongsOf(record);
            const float GAP = 8f;
            while (_songLines.Count < songs.Count) _songLines.Add(NewLine(_scrollContent, "Song" + _songLines.Count, LINE_HEIGHT, 180f,
                manualDownload: true));
            for (int i = 0; i < _songLines.Count; i++)
            {
                bool used = i < songs.Count;
                _songLines[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                DanSongInfo info = songs[i];
                string meta = (info.Difficulty != null ? info.Difficulty + "  ·  " : string.Empty) + info.Host;
                string best = BestScore(record, i);
                _songLines[i].Bind(i + 1, info.Title, meta, info.Availability,
                    info.Installed ? DanCopy.Life : LibraryTheme.Colors.TextMuted, HomeTheme.Colors.WithAlpha(tint, 46),
                    best != null ? "<size=60%><color=#9AA6B5>最高 </color></size>" + best : null, info.ManualDownloadPage);
                _songLines[i].Root.anchoredPosition = new Vector2(0f, -top);
                top += LINE_HEIGHT + GAP;
            }

            top += 18f;
            _damageCaption.rectTransform.anchoredPosition = new Vector2(_damageCaption.rectTransform.anchoredPosition.x, -top);
            top += 34f;
            for (int i = 0; i < 5; i++) _damage.Set(i, DanCopy.Cost(DanCopy.Damage(definition.Damage, i)));
            LibraryWidgets.TopStrip(_damage.Root, 0f, 0f, top, 76f);
            top += 76f + 26f;

            _attemptsCaption.rectTransform.anchoredPosition = new Vector2(_attemptsCaption.rectTransform.anchoredPosition.x, -top);
            top += 34f;
            int shown = Mathf.Min(RECENT_ATTEMPTS, record.Attempts.Count);
            while (_attemptLines.Count < shown) _attemptLines.Add(NewLine(_scrollContent, "Attempt" + _attemptLines.Count, ATTEMPT_HEIGHT, 160f));
            for (int i = 0; i < _attemptLines.Count; i++)
            {
                bool used = i < shown;
                _attemptLines[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                DanAttempt attempt = record.Attempts[i];
                Color32 color = DanCopy.OutcomeColor(attempt.Outcome);
                _attemptLines[i].Bind(record.Attempts.Count - i, DanCopy.Attempt(attempt, definition), DanCopy.When(attempt.StartedUtc),
                    DanCopy.Outcome(attempt.Outcome), color, HomeTheme.Colors.Chip,
                    DanCopy.Number(attempt.RemainingLife(definition)) + "<size=60%><color=#9AA6B5> 生命</color></size>");
                _attemptLines[i].Root.anchoredPosition = new Vector2(0f, -top);
                top += ATTEMPT_HEIGHT + GAP;
            }

            _attemptsEmpty.gameObject.SetActive(shown == 0);
            LibraryWidgets.TopStrip(_attemptsEmpty.rectTransform, 10f, 0f, top, 30f);
            if (shown == 0) top += 34f;
            _scrollContent.sizeDelta = new Vector2(0f, top + 12f);

            ServerWidgets.SetLabelledButton(_start, record.Attempts.Count > 0 ? "再次挑战" : "开始挑战", LibraryIcon.Play,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent);
            ServerWidgets.SetLabelledButton(_equip, record.Equipped ? "卸下" : "佩戴", record.Equipped ? LibraryIcon.Close : LibraryIcon.Tag,
                LibraryTheme.Colors.Control, record.Earned ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted);
        }

        /// <summary>The highest score a past attempt got on song <paramref name="index"/>, or null.</summary>
        private static string BestScore(DanRecord record, int index)
        {
            int best = -1;
            foreach (DanAttempt attempt in record.Attempts)
            {
                if (index < attempt.Songs.Count && attempt.Songs[index].Score.totalScore > best) best = attempt.Songs[index].Score.totalScore;
            }

            return best >= 0 ? DanCopy.Number(best) : null;
        }

        private static DanSongLine NewLine(RectTransform content, string name, float height, float valueWidth, bool manualDownload = false)
        {
            var line = new DanSongLine(content, name, height, valueWidth, manualDownload);
            line.Root.anchorMin = new Vector2(0f, 1f);
            line.Root.anchorMax = new Vector2(1f, 1f);
            line.Root.pivot = new Vector2(0.5f, 1f);
            line.Root.sizeDelta = new Vector2(0f, height);
            return line;
        }

        private Button FooterButton(string name, string label, LibraryIcon icon, UnityEngine.Events.UnityAction action, int index, float height)
        {
            const float WIDTH = 168f;
            Button button = ServerWidgets.LabelledButton(_detail, name, label, icon, action, WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 19f);
            LibraryWidgets.Outline((Image)button.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)button.transform, Vector2.zero, Vector2.zero,
                new Vector2(PAD + index * (WIDTH + 14f), 18f + (LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT - 4f - height) * 0.5f), new Vector2(WIDTH, height));
            return button;
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

        /// <summary>One saved course in the list: its plate, its shape and how far the player got.</summary>
        private sealed class ListRow : IDisposable
        {
            private readonly Image _background;
            private readonly Image _accent;
            private readonly MedalPlate _plate;
            private readonly TextMeshProUGUI _shape;
            private readonly TextMeshProUGUI _status;
            private readonly RectTransform _worn;

            internal ListRow(RectTransform content, int index, DanScreen screen)
            {
                Root = LibraryUi.CreateRect("Course" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, ROW_HEIGHT);
                _background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                PackWidgets.RowButton(Root, () => screen.Select(Record), UiCue.None, Root);
                _accent = LibraryWidgets.Panel("Accent", Root, 3f, Color.clear);
                LibraryWidgets.Place(_accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(6f, ROW_HEIGHT - 40f));
                _plate = new MedalPlate(Root, "Plate", new Vector2(258f, 60f), shadow: false);
                LibraryWidgets.Place(_plate.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), _plate.Root.sizeDelta);
                _shape = LibraryWidgets.Label("Shape", Root, string.Empty, 20f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.Inset(_shape.rectTransform, 312f, ROW_HEIGHT * 0.5f, 20f, 18f);
                _shape.alignment = TextAlignmentOptions.BottomLeft;
                _status = LibraryWidgets.Label("Status", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.Inset(_status.rectTransform, 312f, 16f, 20f, ROW_HEIGHT * 0.5f + 6f);
                _status.alignment = TextAlignmentOptions.TopLeft;
                _worn = PackWidgets.Tag(Root, "佩戴中", HomeTheme.Colors.Violet, out float width, 28f, true);
                LibraryWidgets.Place(_worn, Vector2.one, Vector2.one, new Vector2(-16f, -14f), new Vector2(width, 28f));
            }

            internal RectTransform Root { get; }
            internal DanRecord Record { get; private set; }

            internal void Bind(DanRecord record)
            {
                Record = record;
                DanDefinition definition = record.Definition;
                _plate.SetLook(MedalLook.Of(definition));
                _plate.SetLocked(!record.Earned);
                _shape.text = DanCopy.Shape(definition);
                _status.text = DanCopy.Status(record);
                _status.color = record.Earned ? DanCopy.Pass : LibraryTheme.Colors.TextMuted;
                _worn.gameObject.SetActive(record.Equipped);
            }

            internal void SetSelected(bool selected)
            {
                Color32 tint = Record != null ? DanStyles.ParseColor(Record.Definition.Color) : (Color32)Color.white;
                _background.color = selected ? HomeTheme.Colors.WithAlpha(tint, 36) : LibraryTheme.Colors.Control;
                _accent.color = selected ? (Color)DanStyles.Light(tint) : Color.clear;
            }

            public void Dispose() => _plate.Dispose();
        }
    }
}
