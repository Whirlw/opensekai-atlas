using System.Collections.Generic;
using System.Text;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Layout and binding of the result page. Sizes are canvas units at the 1920×1080 reference;
    /// the stack on the right is laid out from the bottom so a short phone screen only narrows
    /// the gap under the score.
    /// </summary>
    internal sealed partial class ResultPage : IPointerClickHandler
    {
        private const float MARGIN = LibraryTheme.Spacing.PAGE_MARGIN;
        private const float BAR_HEIGHT = LibraryTheme.Layout.TOP_BAR_HEIGHT;
        private const float TOP = BAR_HEIGHT + 8f;
        private const float BOTTOM = 48f;
        private const float COVER_SIZE = 420f;
        private const float RIGHT_LEFT = MARGIN + COVER_SIZE + 72f;
        private const float CHIP_HEIGHT = 36f;
        private const float HERO_HEIGHT = 240f;
        private const float STRIP_HEIGHT = 120f;
        private const float CARD_HEIGHT = 280f;
        private const float GAP = 24f;
        private const float BUTTON_HEIGHT = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
        private const float CARD_PADDING = 36f;
        private const float BADGE_SIZE = 148f;
        private const float ROW_CAPTION_WIDTH = 120f;
        private const float TAG_HEIGHT = 30f;
        private const float BREAKDOWN_TOP = 108f;
        private const float STATUS_TOP = 196f;
        private const float STATUS_HEIGHT = 40f;

        private static readonly string[] _judgeNames = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };

        private readonly List<Block> _blocks = new List<Block>();

        private Image _glowMain;
        private Image _glowSecondary;
        private TextMeshProUGUI _skipHint;

        private Image _cover;
        private GameObject _coverPlaceholder;
        private TextMeshProUGUI _difficulty;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _title;
        private Chip _kindChip;
        private Chip _modeChip;

        private GameObject _scoreHero;
        private RectTransform _award;
        private CanvasGroup _awardGroup;
        private Image _awardGlow;
        private TextMeshProUGUI _awardText;
        private TextMeshProUGUI _rank;
        private TextMeshProUGUI _score;
        private GameObject _errorHero;
        private TextMeshProUGUI _errorBody;

        private GameObject _strip;
        private GameObject _judgeGroup;
        private readonly TextMeshProUGUI[] _judges = new TextMeshProUGUI[_judgeNames.Length];
        private GameObject _comboColumn;
        private TextMeshProUGUI _maxCombo;
        private TextMeshProUGUI _totalNotes;
        private TextMeshProUGUI _autoNote;

        private GameObject _cardBody;
        private GameObject _cardMissing;
        private TextMeshProUGUI _points;
        private TextMeshProUGUI _breakdown;
        private TextMeshProUGUI _bestValue;
        private Chip _recordTag;
        private TextMeshProUGUI _positionValue;
        private RankingBadge _badge;
        private TextMeshProUGUI _tierName;
        private TextMeshProUGUI _tierLatin;
        private Chip _tierUpTag;
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _status;
        private Chip _deltaTag;
        private Button _saveRetry;

        private Image _retryBackground;
        private TextMeshProUGUI _retryLabel;
        private Image _retryIcon;
        private AchievementUnlockPopup _unlock;

        /// <summary>A tap that lands on no control bubbles up here and skips the opening.</summary>
        public void OnPointerClick(PointerEventData eventData) => Skip();

        // ---------------------------------------------------------------- building

        private void Build(string backLabel)
        {
            var root = (RectTransform)transform;

            // Opaque, so the owner's page never shows through, and the target of a skipping tap.
            Image ground = LibraryUi.CreateImage("Ground", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(ground.rectTransform);
            ground.raycastTarget = true;
            _glowMain = Glow("GlowMain", root, new Vector2(0f, 1f),
                new Vector2(MARGIN + COVER_SIZE * 0.5f, -(TOP + COVER_SIZE * 0.45f)), 1600f);
            _glowSecondary = Glow("GlowSecondary", root, new Vector2(1f, 0f), new Vector2(-560f, 240f), 1400f);

            BuildTopBar(root, backLabel);
            BuildChartColumn(root);
            RectTransform results = LibraryUi.CreateRect("Results", root);
            LibraryWidgets.Inset(results, RIGHT_LEFT, BOTTOM, MARGIN, TOP);
            BuildHero(results);
            BuildJudgements(results);
            BuildContribution(results);
            BuildActions(results);
            // Last, so it draws over everything; at the top right, clear of the award mark and the score.
            _unlock = new AchievementUnlockPopup(root, 18f, MARGIN);
        }

        private void BuildTopBar(RectTransform root, string backLabel)
        {
            const float BACK_WIDTH = 248f;
            const float ICON = 26f;
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button back = LibraryWidgets.PillButton(
                "Back", root, backLabel, Close, BACK_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 24f, sound: UiCue.Back);
            LibraryWidgets.Outline((Image)back.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(MARGIN, -BAR_HEIGHT * 0.5f), new Vector2(BACK_WIDTH, height));
            TextMeshProUGUI label = back.GetComponentInChildren<TextMeshProUGUI>();
            LibraryWidgets.Inset(label.rectTransform, 30f + ICON, 0f, 10f, 0f);
            Image icon = LibraryWidgets.Icon(back.transform, LibraryIcon.Back, LibraryTheme.Colors.TextPrimary, ICON);
            LibraryWidgets.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(28f, 0f), new Vector2(ICON, ICON));

            _skipHint = LibraryWidgets.Label("SkipHint", root, "轻触屏幕跳过动画", LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(_skipHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-MARGIN, -BAR_HEIGHT * 0.5f), new Vector2(420f, 30f));
        }

        private void BuildChartColumn(RectTransform root)
        {
            float infoTop = COVER_SIZE + 28f;
            RectTransform column = LibraryUi.CreateRect("Chart", root);
            TopLeft(column, MARGIN, TOP, COVER_SIZE, infoTop + 156f + CHIP_HEIGHT);
            _blocks.Add(new Block(column, 0f));

            RectTransform frame = LibraryUi.CreateRect("Cover", column);
            TopLeft(frame, 0f, 0f, COVER_SIZE, COVER_SIZE);
            LibraryWidgets.Shadow(frame, LibraryTheme.Radius.PANEL, LibraryTheme.CoverShadow.BLUR,
                LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image face = LibraryWidgets.Panel("Frame", frame, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(face.rectTransform);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(face.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 96f).gameObject;
            _cover = LibraryUi.CreateImage("Jacket", face.transform, Color.white);
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);

            _difficulty = LibraryWidgets.Label("Difficulty", column, string.Empty, 24f, LibraryTheme.Colors.TextSecondary);
            LibraryUi.UseSpacedCondensedFont(_difficulty, 3f);
            TopLeft(_difficulty.rectTransform, 0f, infoTop, COVER_SIZE, CHIP_HEIGHT);
            _level = LibraryWidgets.Label("Level", column, string.Empty, 24f, LibraryTheme.Colors.TextSecondary);
            LibraryUi.UseCondensedFont(_level);
            TopLeft(_level.rectTransform, 0f, infoTop, 160f, CHIP_HEIGHT);

            _title = LibraryUi.CreateText("Title", column, string.Empty, 36f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            _title.color = LibraryTheme.Colors.TextPrimary;
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            TopLeft(_title.rectTransform, 0f, infoTop + 46f, COVER_SIZE, 100f);

            _kindChip = new Chip(column, "Kind", infoTop + 156f, CHIP_HEIGHT);
            _modeChip = new Chip(column, "Mode", infoTop + 156f, CHIP_HEIGHT);
        }

        private void BuildHero(RectTransform results)
        {
            RectTransform hero = LibraryUi.CreateRect("Hero", results);
            LibraryWidgets.TopStrip(hero, 0f, 0f, 0f, HERO_HEIGHT);
            _blocks.Add(new Block(hero, 0.06f));

            RectTransform score = LibraryUi.CreateRect("Score", hero);
            LibraryUi.Stretch(score);
            _scoreHero = score.gameObject;

            // The award mark grows from its left edge as it stamps in.
            _award = LibraryUi.CreateRect("Award", score);
            LibraryWidgets.Place(_award, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -36f), new Vector2(900f, 72f));
            _awardGroup = _award.gameObject.AddComponent<CanvasGroup>();
            _awardGlow = Glow("Glow", _award, new Vector2(0f, 0.5f), new Vector2(240f, 0f), 1f);
            _awardGlow.rectTransform.sizeDelta = new Vector2(820f, 260f);
            _awardText = LibraryWidgets.Label("Mark", _award, string.Empty, 60f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseSpacedCondensedFont(_awardText, 10f);
            _awardText.richText = true;
            _awardText.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_awardText.rectTransform);

            _rank = LibraryWidgets.Label("Rank", score, string.Empty, 176f, LibraryTheme.Colors.DefaultAccent,
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            LibraryUi.UseCondensedFont(_rank);
            _rank.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_rank.rectTransform, Vector2.zero, Vector2.zero, new Vector2(-6f, -18f), new Vector2(180f, 190f));
            TextMeshProUGUI caption = Caption(score, "ScoreCaption", "SCORE");
            LibraryWidgets.Place(caption.rectTransform, Vector2.zero, Vector2.zero, new Vector2(190f, 132f), new Vector2(300f, 26f));
            _score = LibraryWidgets.Label("Score", score, string.Empty, 124f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            LibraryUi.UseCondensedFont(_score);
            _score.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_score.rectTransform, Vector2.zero, Vector2.zero, new Vector2(184f, -10f), new Vector2(760f, 140f));

            // A load error: a plain sentence, and the reason where the score would be.
            RectTransform error = LibraryUi.CreateRect("Error", hero);
            LibraryUi.Stretch(error);
            _errorHero = error.gameObject;
            TextMeshProUGUI lead = LibraryWidgets.Label(
                "Lead", error, "这一局没有开始", 40f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(lead.rectTransform, 0f, 0f, 8f, 56f);
            Image reason = LibraryWidgets.Panel("Reason", error, LibraryTheme.Radius.ROW, LibraryTheme.Colors.DangerSurface);
            LibraryWidgets.TopStrip(reason.rectTransform, 0f, 0f, 84f, 150f);
            TextMeshProUGUI reasonCaption = LibraryWidgets.Label(
                "Caption", reason.transform, "原因", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.Danger, FontStyles.Bold);
            LibraryWidgets.TopStrip(reasonCaption.rectTransform, CARD_PADDING, CARD_PADDING, 22f, 26f);
            _errorBody = LibraryUi.CreateText("Body", reason.transform, string.Empty, 24f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _errorBody.color = LibraryTheme.Colors.TextPrimary;
            _errorBody.textWrappingMode = TextWrappingModes.Normal;
            _errorBody.overflowMode = TextOverflowModes.Ellipsis;
            LibraryWidgets.Inset(_errorBody.rectTransform, CARD_PADDING, 18f, CARD_PADDING, 58f);
        }

        private void BuildJudgements(RectTransform results)
        {
            const float JUDGE_WIDTH = 148f;
            const float COLUMN = 180f;
            Image strip = LibraryWidgets.Panel("Judgements", results, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryWidgets.BottomStrip(strip.rectTransform, 0f, 0f, BUTTON_HEIGHT + GAP + CARD_HEIGHT + GAP, STRIP_HEIGHT);
            LibraryWidgets.Outline(strip, LibraryTheme.Radius.ROW);
            _strip = strip.gameObject;
            _blocks.Add(new Block(strip.rectTransform, 0.12f));

            RectTransform grades = LibraryUi.CreateRect("Grades", strip.transform);
            LibraryWidgets.Inset(grades, CARD_PADDING, 22f, CARD_PADDING, 22f);
            _judgeGroup = grades.gameObject;
            Vector2 topLeft = new Vector2(0f, 1f);
            for (int i = 0; i < _judgeNames.Length; i++)
            {
                TextMeshProUGUI name = Caption(grades, "JudgeName" + i, _judgeNames[i]);
                LibraryWidgets.Place(name.rectTransform, topLeft, topLeft, new Vector2(i * JUDGE_WIDTH, 0f), new Vector2(JUDGE_WIDTH, 24f));
                _judges[i] = Number(grades, "Judge" + i, 40f, TextAlignmentOptions.Left);
                LibraryWidgets.Place(_judges[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * JUDGE_WIDTH, -4f), new Vector2(JUDGE_WIDTH, 52f));
            }

            // Right side: 总物量 against the edge, 最大连击 before it.
            _totalNotes = Column(strip.transform, "Notes", "总物量", CARD_PADDING, COLUMN);
            RectTransform combo = LibraryUi.CreateRect("Combo", strip.transform);
            LibraryUi.Stretch(combo);
            _comboColumn = combo.gameObject;
            _maxCombo = Column(combo, "Combo", "最大连击", CARD_PADDING + COLUMN + GAP, COLUMN);

            _autoNote = LibraryWidgets.Label("AutoNote", strip.transform, string.Empty, 22f, LibraryTheme.Colors.TextSecondary);
            _autoNote.richText = true;
            LibraryWidgets.Inset(_autoNote.rectTransform, CARD_PADDING, 0f, CARD_PADDING + COLUMN + GAP, 0f);
        }

        private void BuildContribution(RectTransform results)
        {
            Image card = LibraryWidgets.Panel("Contribution", results, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.Surface);
            LibraryWidgets.BottomStrip(card.rectTransform, 0f, 0f, BUTTON_HEIGHT + GAP, CARD_HEIGHT);
            LibraryWidgets.Outline(card, LibraryTheme.Radius.PANEL);
            _blocks.Add(new Block(card.rectTransform, 0.18f));

            RectTransform body = LibraryUi.CreateRect("Body", card.transform);
            LibraryUi.Stretch(body);
            _cardBody = body.gameObject;
            Image divider = LibraryUi.CreateImage("Divider", body, LibraryTheme.Colors.Hairline);
            divider.raycastTarget = false;
            divider.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            divider.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            divider.rectTransform.offsetMin = new Vector2(-0.5f, 28f);
            divider.rectTransform.offsetMax = new Vector2(0.5f, -28f);

            BuildPoints(Half(body, "Points", 0f));
            BuildRankingScore(Half(body, "RankingScore", 0.5f));

            _cardMissing = LibraryWidgets.Label("Missing", card.transform, "本局未记录成绩", 24f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center).gameObject;
            LibraryUi.Stretch((RectTransform)_cardMissing.transform);
        }

        private void BuildPoints(RectTransform section)
        {
            TextMeshProUGUI caption = Caption(section, "Caption", "本次成绩分");
            TopLeft(caption.rectTransform, 0f, 26f, 300f, 26f);
            _points = Number(section, "Points", 84f, TextAlignmentOptions.BottomLeft);
            TopLeft(_points.rectTransform, 0f, 52f, 300f, 96f);
            _breakdown = LibraryWidgets.Label("Breakdown", section, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary);
            TopLeft(_breakdown.rectTransform, 0f, BREAKDOWN_TOP, 420f, 32f);

            Row(section, "Best", "该谱最佳", 176f, out _bestValue);
            _recordTag = new Chip(section, "Record", 178f, TAG_HEIGHT);
            _recordTag.Set(ResultCopy.NEW_RECORD, LibraryTheme.Colors.DefaultAccent);
            Row(section, "Position", "Best 30", 222f, out _positionValue);
        }

        private void BuildRankingScore(RectTransform section)
        {
            const float TEXT_LEFT = BADGE_SIZE + 28f;
            TextMeshProUGUI caption = Caption(section, "Caption", "RANKING SCORE");
            TopLeft(caption.rectTransform, 0f, 26f, 300f, 26f);
            _badge = new RankingBadge(section, BADGE_SIZE, animate: true);
            TopLeft(_badge.Root, -10f, 62f, BADGE_SIZE, BADGE_SIZE);

            _tierName = LibraryWidgets.Label("TierName", section, string.Empty, 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            TopLeft(_tierName.rectTransform, TEXT_LEFT, 58f, 160f, 36f);
            _tierLatin = LibraryWidgets.Label("TierLatin", section, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseSpacedCondensedFont(_tierLatin, 4f);
            TopLeft(_tierLatin.rectTransform, TEXT_LEFT, 58f, 200f, 36f);
            _tierUpTag = new Chip(section, "TierUp", 61f, TAG_HEIGHT);

            _total = Number(section, "Total", 84f, TextAlignmentOptions.BottomLeft);
            TopLeft(_total.rectTransform, TEXT_LEFT, 90f, 420f, 96f);
            _status = LibraryWidgets.Label("Status", section, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(_status.rectTransform, TEXT_LEFT, 0f, STATUS_TOP, STATUS_HEIGHT);
            _deltaTag = new Chip(section, "Delta", STATUS_TOP + (STATUS_HEIGHT - TAG_HEIGHT) * 0.5f, TAG_HEIGHT, LibraryTheme.FontSize.BODY);

            const float SAVE_WIDTH = 148f;
            const float SAVE_HEIGHT = 44f;
            _saveRetry = LibraryWidgets.PillButton(
                "SaveRetry", section, "重新保存", RetrySave, SAVE_WIDTH, SAVE_HEIGHT,
                LibraryTheme.Colors.ControlActive, LibraryTheme.Colors.TextPrimary, LibraryTheme.FontSize.BODY,
                FontStyles.Bold, UiCue.None);
            LibraryWidgets.Place((RectTransform)_saveRetry.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(TEXT_LEFT, -STATUS_TOP - STATUS_HEIGHT * 0.5f), new Vector2(SAVE_WIDTH, SAVE_HEIGHT));
        }

        private void BuildActions(RectTransform results)
        {
            const float RETRY_WIDTH = 300f;
            RectTransform actions = LibraryUi.CreateRect("Actions", results);
            LibraryWidgets.BottomStrip(actions, 0f, 0f, 0f, BUTTON_HEIGHT);
            _blocks.Add(new Block(actions, 0.24f));

            Button retry = LibraryWidgets.PillButton(
                "Retry", actions, "再玩一次", Retry, RETRY_WIDTH, BUTTON_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, 26f, FontStyles.Bold, UiCue.Retry);
            LibraryWidgets.Place((RectTransform)retry.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(RETRY_WIDTH, BUTTON_HEIGHT));
            _retryBackground = (Image)retry.targetGraphic;
            _retryLabel = retry.GetComponentInChildren<TextMeshProUGUI>();
            _retryIcon = LibraryWidgets.Icon(retry.transform, LibraryIcon.Refresh, LibraryTheme.Colors.TextOnAccent, 26f);
        }

        // ---------------------------------------------------------------- binding

        private void SetCover(Sprite cover)
        {
            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.SetActive(cover == null);
        }

        /// <summary>Everything about the run itself; the counters start from nothing.</summary>
        private void Bind(PlayResult result)
        {
            _shownScore = -1;
            _title.text = result.Title;
            string difficulty = result.Difficulty.ToUpperInvariant();
            _difficulty.text = difficulty;
            _difficulty.color = LibraryTheme.Colors.Difficulty(result.Difficulty);
            float levelLeft = difficulty.Length > 0 ? Mathf.Ceil(_difficulty.GetPreferredValues(difficulty).x) + 14f : 0f;
            RectTransform level = _level.rectTransform;
            level.anchoredPosition = new Vector2(levelLeft, level.anchoredPosition.y);
            _level.text = result.PlayLevel > 0 ? "LV " + result.PlayLevel : string.Empty;
            _kindChip.Set(result.KindText, KindColor(result));
            _modeChip.Set(ResultCopy.Mode(result), LibraryTheme.Colors.TextSecondary);
            _modeChip.Layout(_kindChip.Layout(0f) + 10f);

            bool hasScore = result.HasScore;
            _scoreHero.SetActive(hasScore);
            _errorHero.SetActive(!hasScore);
            _strip.SetActive(hasScore);
            SetRetryLabel(hasScore ? "再玩一次" : "重试");
            if (!hasScore)
            {
                _errorBody.text = string.IsNullOrWhiteSpace(result.Message) ? "曲包无法载入。" : result.Message;
                return;
            }

            _rank.text = result.Rank.ToString();
            _score.text = ResultCopy.Number(0);
            _judgeGroup.SetActive(!result.IsAuto);
            _comboColumn.SetActive(!result.IsAuto);
            _autoNote.gameObject.SetActive(result.IsAuto);
            _totalNotes.text = ResultCopy.Number(result.TotalCombo);
            if (result.IsAuto)
            {
                _autoNote.text = "自动判定  <color=#F2F4F7>" + ResultCopy.Number(result.AutoCount)
                    + "</color>\n<size=80%><color=#9AA6B5>自动演奏不分判定等级，也不计入 Ranking Score</color></size>";
                return;
            }

            // Use the same five grades as the HUD.
            _judges[0].text = ResultCopy.Number(result.Perfect);
            _judges[1].text = ResultCopy.Number(result.Great);
            _judges[2].text = ResultCopy.Number(result.Good);
            _judges[3].text = ResultCopy.Number(result.Bad);
            _judges[4].text = ResultCopy.Number(result.Miss);
            _maxCombo.text = ResultCopy.Number(result.MaxCombo);
        }

        /// <summary>The contribution card. Called again when a failed save goes through on retry.</summary>
        private void BindRanking()
        {
            _shownPoints = -1;
            _shownTotal = -1;
            _shownTier = -1;
            bool known = _ranking != null;
            _cardBody.SetActive(known);
            _cardMissing.SetActive(!known);
            if (!known)
            {
                return;
            }

            bool counted = _ranking.Eligibility == RankingEligibility.Eligible;
            string finalPoints = counted ? ResultCopy.Number(_ranking.Points) : "—";
            _points.text = counted ? ResultCopy.Number(0) : finalPoints;
            _points.color = counted ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted;
            _breakdown.text = ResultCopy.Breakdown(_ranking);
            RectTransform breakdown = _breakdown.rectTransform;
            breakdown.anchoredPosition = new Vector2(Mathf.Ceil(_points.GetPreferredValues(finalPoints).x) + 20f, -BREAKDOWN_TOP);

            _bestValue.text = ResultCopy.ChartBest(_ranking);
            bool record = counted && _ranking.ChartImproved;
            _recordTag.SetShown(record);
            _recordTag.Layout(ROW_CAPTION_WIDTH + Mathf.Ceil(_bestValue.GetPreferredValues(_bestValue.text).x) + 14f);
            _positionValue.text = ResultCopy.Position(_ranking);

            _tierUpTag.Set(ResultCopy.TIER_UP, RankingTiers.Color(_ranking.After.TierId));
            _tierUpTag.SetShown(_ranking.TierUp);
            _tierUpTag.Pop(1f);
            // Both crests start drawing now, so the upgrade 2.6 s in has the new one ready.
            _badge.Warm(_ranking.Before.TierId);
            _badge.Warm(_ranking.After.TierId);
            ShowTier(_ranking.After.TierId);

            bool saveFailed = _ranking.SaveState == HistorySaveState.SaveFailed;
            string exclusion = ResultCopy.Exclusion(_current, _ranking);
            if (saveFailed)
            {
                _status.text = ResultCopy.NOT_SAVED;
                _status.color = LibraryTheme.Colors.Danger;
            }
            else
            {
                _status.text = exclusion ?? ResultCopy.Standing(_ranking);
                _status.color = exclusion != null ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.TextSecondary;
            }

            _saveRetry.gameObject.SetActive(saveFailed);
            _deltaTag.SetShown(!saveFailed && _ranking.Delta > 0);
            _deltaTag.Set(ResultCopy.Signed(_ranking.Delta), _accent);
            LayoutStatus();
        }

        /// <summary>Puts 重新保存 or the gain right after the status line, however long it is.</summary>
        private void LayoutStatus()
        {
            float statusEnd = _status.rectTransform.offsetMin.x + Mathf.Ceil(_status.GetPreferredValues(_status.text).x);
            RectTransform save = (RectTransform)_saveRetry.transform;
            save.anchoredPosition = new Vector2(statusEnd + 16f, save.anchoredPosition.y);
            _deltaTag.Layout(statusEnd + 14f);
        }

        /// <summary>Everything drawn in the jacket's accent.</summary>
        private void ApplyAccent()
        {
            _retryBackground.color = _accent;
            _rank.color = _accent;
            _glowMain.color = new Color32(_accent.r, _accent.g, _accent.b, 60);
            _recordTag.Set(ResultCopy.NEW_RECORD, _accent);
            if (_ranking != null)
            {
                _deltaTag.Set(ResultCopy.Signed(_ranking.Delta), _accent);
            }

            if (_current == null)
            {
                return;
            }

            _kindChip.Set(_current.KindText, KindColor(_current));
            if (_current.HasScore)
            {
                ApplyAward(_current);
            }
        }

        /// <summary>ALL PERFECT runs through its own colors, FULL COMBO is gold, the rest take one flat color.</summary>
        private void ApplyAward(PlayResult result)
        {
            string text = ResultCopy.Award(result);
            bool allPerfect = !result.IsAuto && result.Award == ClearAward.AllPerfect;
            bool fullCombo = !result.IsAuto && result.Award == ClearAward.FullCombo;
            _awardText.enableVertexGradient = allPerfect || fullCombo;
            _awardGlow.enabled = allPerfect || fullCombo;
            if (allPerfect)
            {
                _awardText.text = Prismatic(text);
                _awardText.color = Color.white;
                var shade = new Color(0.86f, 0.86f, 0.9f, 1f);
                _awardText.colorGradient = new VertexGradient(Color.white, Color.white, shade, shade);
                _awardGlow.color = LibraryTheme.Colors.AllPerfectGlow;
                return;
            }

            _awardText.text = text;
            if (fullCombo)
            {
                _awardText.color = Color.white;
                _awardText.colorGradient = new VertexGradient(
                    LibraryTheme.Colors.FullComboTop, LibraryTheme.Colors.FullComboTop,
                    LibraryTheme.Colors.FullComboBottom, LibraryTheme.Colors.FullComboBottom);
                _awardGlow.color = LibraryTheme.Colors.FullComboGlow;
                return;
            }

            _awardText.color = AwardColor(result);
        }

        private Color32 AwardColor(PlayResult result)
        {
            if (result.IsAuto) return LibraryTheme.Colors.TextSecondary;
            switch (result.Kind)
            {
                case PlayResultKind.Completed: return _accent;
                case PlayResultKind.Retired: return LibraryTheme.Colors.Warning;
                default: return LibraryTheme.Colors.Danger;
            }
        }

        private Color32 KindColor(PlayResult result)
        {
            switch (result.Kind)
            {
                case PlayResultKind.Completed: return _accent;
                case PlayResultKind.Retired: return LibraryTheme.Colors.Warning;
                default: return LibraryTheme.Colors.Danger;
            }
        }

        /// <summary>Icon and label sit together as one centered group, whatever the label's length.</summary>
        private void SetRetryLabel(string text)
        {
            const float ICON = 26f;
            const float ICON_GAP = 10f;
            _retryLabel.text = text;
            float group = ICON + ICON_GAP + Mathf.Ceil(_retryLabel.GetPreferredValues(text).x);
            _retryIcon.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + ICON * 0.5f, 0f);
            LibraryWidgets.Inset(_retryLabel.rectTransform, ICON + ICON_GAP, 0f, 0f, 0f);
        }

        /// <summary>One color per letter, running pink to violet to cyan.</summary>
        private static string Prismatic(string text)
        {
            var builder = new StringBuilder(text.Length * 26);
            float last = Mathf.Max(1, text.Length - 1);
            for (int i = 0; i < text.Length; i++)
            {
                Color color = LibraryTheme.Colors.AllPerfectAt(i / last);
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>')
                    .Append(text[i]).Append("</color>");
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------- small builders

        private static void TopLeft(RectTransform rect, float x, float top, float width, float height)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            LibraryWidgets.Place(rect, topLeft, topLeft, new Vector2(x, -top), new Vector2(width, height));
        }

        private static Image Glow(string name, Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            Image glow = LibraryUi.CreateImage(name, parent, Color.clear);
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            return glow;
        }

        private static TextMeshProUGUI Caption(Transform parent, string name, string text)
        {
            TextMeshProUGUI caption = LibraryWidgets.Label(name, parent, text, 17f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseSpacedCondensedFont(caption, 3f);
            return caption;
        }

        private static TextMeshProUGUI Number(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI number = LibraryWidgets.Label(name, parent, string.Empty, size, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, alignment);
            LibraryUi.UseCondensedFont(number);
            number.overflowMode = TextOverflowModes.Overflow;
            return number;
        }

        /// <summary>A caption and a number, right-aligned, at a distance from the strip's right edge.</summary>
        private static TextMeshProUGUI Column(Transform parent, string name, string caption, float right, float width)
        {
            TextMeshProUGUI label = LibraryWidgets.Label(name + "Caption", parent, caption, LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(label.rectTransform, Vector2.one, Vector2.one, new Vector2(-right, -22f), new Vector2(width, 26f));
            TextMeshProUGUI value = Number(parent, name, 40f, TextAlignmentOptions.Right);
            LibraryWidgets.Place(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-right, 18f), new Vector2(width, 52f));
            return value;
        }

        /// <summary>One half of the contribution card, inset by the card's padding.</summary>
        private static RectTransform Half(RectTransform body, string name, float left)
        {
            RectTransform half = LibraryUi.CreateRect(name, body);
            half.anchorMin = new Vector2(left, 0f);
            half.anchorMax = new Vector2(left + 0.5f, 1f);
            half.offsetMin = new Vector2(CARD_PADDING, 0f);
            half.offsetMax = new Vector2(-CARD_PADDING, 0f);
            return half;
        }

        private static void Row(RectTransform section, string name, string caption, float top, out TextMeshProUGUI value)
        {
            TextMeshProUGUI label = LibraryWidgets.Label(name + "Caption", section, caption, LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted);
            TopLeft(label.rectTransform, 0f, top, ROW_CAPTION_WIDTH, 34f);
            value = LibraryWidgets.Label(name, section, string.Empty, 26f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(value);
            TopLeft(value.rectTransform, ROW_CAPTION_WIDTH, top, 300f, 34f);
        }
    }
}
