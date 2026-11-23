using System;
using System.Globalization;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Right side of the library page: the selected package's jacket, credits and best record, and
    /// the controls that start it. The accent color follows the jacket.
    /// </summary>
    internal sealed class LibraryDetailPanel : IDisposable
    {
        private const float GAP = 32f;
        private const float INFO_LEFT = 40f;
        private const float ACTION_HEIGHT = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
        private const float AUTO_WIDTH = 212f;
        private const float CARD_PADDING_X = 36f;
        private const float CARD_PADDING_Y = 28f;
        private const float JUDGE_WIDTH = 108f;
        private const float TITLE_LINE_HEIGHT = 58f;
        private const float DIFFICULTY_HEIGHT = 36f;
        private const float DIFFICULTY_PADDING = 14f;
        private const float DIFFICULTY_MAX_WIDTH = 120f;
        private const float METADATA_GAP = 12f;

        private static readonly string[] _judgeNames = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };
        private static readonly Color32 _cardColor = new Color32(16, 20, 27, 184);
        private static readonly Color32 _switchOff = new Color32(255, 255, 255, 56);

        private readonly Action<bool> _play;
        private readonly RectTransform _content;
        private readonly CanvasGroup _contentGroup;
        private readonly RectTransform _coverFrame;
        private readonly Image _cover;
        private readonly GameObject _coverPlaceholder;
        private readonly UnityEngine.UI.Button _source;
        private readonly RectTransform _difficultyChip;
        private readonly TextMeshProUGUI _difficulty;
        private readonly TextMeshProUGUI _level;
        private readonly TextMeshProUGUI _length;
        private readonly TextMeshProUGUI _title;
        private readonly RectTransform _info;
        private readonly RectTransform[] _creditNames = new RectTransform[3];
        private readonly TextMeshProUGUI[] _credits = new TextMeshProUGUI[3];
        private readonly RectTransform _previewRow;
        private readonly Image _previewIcon;
        private readonly Image _previewFill;

        private readonly GameObject _record;
        private readonly TextMeshProUGUI _rank;
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _clear;
        private readonly TextMeshProUGUI _played;
        private readonly TextMeshProUGUI[] _judges = new TextMeshProUGUI[_judgeNames.Length];
        private readonly TextMeshProUGUI _combo;
        private readonly GameObject _noRecord;
        private readonly TextMeshProUGUI _noRecordTitle;
        private readonly TextMeshProUGUI _noRecordBody;

        private readonly Image _autoTrack;
        private readonly RectTransform _autoKnob;
        private readonly Button _startButton;
        private readonly Image _startBackground;
        private readonly TextMeshProUGUI _startLabel;
        private readonly Image _startIcon;

        private MotionHandle _showMotion;
        private MotionHandle _accentMotion;
        private MotionHandle _autoMotion;
        private Color _accent = LibraryTheme.Colors.DefaultAccent;
        private bool _isAuto;
        private bool _playable;

        /// <param name="root">Rect the panel fills.</param>
        /// <param name="coverTop">Top inset of the first song when grouping is off.</param>
        /// <param name="play">Starts the selected package; the argument is the 自动演奏 switch.</param>
        /// <param name="more">Opens export and delete for the selected package.</param>
        internal LibraryDetailPanel(RectTransform root, float coverTop, Action<bool> play, Action more, Action source)
        {
            _play = play;
            _content = LibraryUi.CreateRect("Detail", root);
            LibraryUi.Stretch(_content);
            _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();
            float coverBottom = LibraryTheme.Layout.DETAIL_COVER_SIZE;
            float coverSize = coverBottom - coverTop;

            // Jacket.
            _coverFrame = LibraryUi.CreateRect("Cover", _content);
            LibraryWidgets.Place(
                _coverFrame, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(coverSize * 0.5f, -coverTop - coverSize * 0.5f), new Vector2(coverSize, coverSize));
            LibraryWidgets.Shadow(
                _coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.CoverShadow.BLUR,
                LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image frame = LibraryWidgets.Panel("Frame", _coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image placeholderShape = LibraryWidgets.Panel("Shape", frame.transform, 80f, LibraryTheme.Colors.SurfaceSelected);
            LibraryWidgets.Place(placeholderShape.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(coverSize * 0.52f, -coverSize * 0.10f), Vector2.one * (coverSize * 0.58f));
            Image placeholderLabel = LibraryWidgets.Panel("Placeholder", frame.transform, 6f, new Color32(0, 0, 0, 140));
            LibraryWidgets.Place(placeholderLabel.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(20f, 18f), new Vector2(82f, 26f));
            TextMeshProUGUI placeholder = LibraryWidgets.Label(
                "Label", placeholderLabel.transform, "封面占位", 14f, LibraryTheme.Colors.TextPrimary,
                alignment: TextAlignmentOptions.Center);
            LibraryUi.Stretch(placeholder.rectTransform);
            _coverPlaceholder = placeholderLabel.gameObject;
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            _cover.enabled = false;
            LibraryUi.Stretch(_cover.rectTransform);
            _source = LibraryWidgets.PillButton("Source", _coverFrame, "来源", () => source?.Invoke(), 106f, 48f,
                new Color32(16, 20, 27, 232), LibraryTheme.Colors.TextPrimary, 21f);
            LibraryWidgets.Place((RectTransform)_source.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f),
                new Vector2(106f, 48f));
            _source.gameObject.SetActive(false);

            // Title and credits, to the right of the jacket.
            RectTransform info = LibraryUi.CreateRect("Info", _content);
            _info = info;
            LibraryWidgets.TopStrip(info, coverSize + INFO_LEFT, 0f, coverTop + 8f, coverSize - 8f);

            Image chip = LibraryWidgets.Panel("Difficulty", info, LibraryTheme.Radius.CHIP, LibraryTheme.Colors.Hairline);
            _difficultyChip = chip.rectTransform;
            Vector2 topLeft = new Vector2(0f, 1f);
            LibraryWidgets.Place(_difficultyChip, topLeft, topLeft, Vector2.zero,
                new Vector2(DIFFICULTY_MAX_WIDTH, DIFFICULTY_HEIGHT));
            _difficulty = LibraryWidgets.Label(
                "Label", chip.transform, string.Empty, 22f, LibraryTheme.Colors.TextSecondary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_difficulty, 2f * 100f / _difficulty.fontSize);
            LibraryWidgets.Inset(_difficulty.rectTransform, DIFFICULTY_PADDING, 0f, DIFFICULTY_PADDING, 0f);
            _level = LibraryWidgets.Label("Level", info, string.Empty, 22f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
            LibraryUi.UseCondensedFont(_level);
            _length = LibraryWidgets.Label("Length", info, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);

            _title = LibraryUi.CreateText(
                "Title", info, string.Empty, LibraryTheme.FontSize.DETAIL_TITLE, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            _title.color = LibraryTheme.Colors.TextPrimary;
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.overflowMode = TextOverflowModes.Overflow;
            _title.maxVisibleLines = 2;
            var face = _title.font != null ? _title.font.faceInfo : default;
            if (face.pointSize > 0f)
            {
                float defaultLineHeight = face.lineHeight * face.scale * _title.fontSize / face.pointSize;
                _title.lineSpacing = (TITLE_LINE_HEIGHT - defaultLineHeight) * 100f / _title.fontSize;
            }

            string[] creditNames = { "演唱", "作曲", "谱面" };
            for (int i = 0; i < _credits.Length; i++)
            {
                TextMeshProUGUI name = LibraryWidgets.Label(
                    "CreditName" + i, info, creditNames[i], LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
                _creditNames[i] = name.rectTransform;
                _credits[i] = LibraryWidgets.Label(
                    "Credit" + i, info, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary);
            }

            _previewRow = LibraryUi.CreateRect("Preview", info);
            LibraryWidgets.BottomStrip(_previewRow, 0f, 0f, 0f, 26f);
            _previewIcon = LibraryWidgets.Icon(_previewRow, LibraryIcon.Wave, LibraryTheme.Colors.DefaultAccent, 26f);
            LibraryWidgets.Place(_previewIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(26f, 26f));
            TextMeshProUGUI previewLabel = LibraryWidgets.Label(
                "Label", _previewRow, "试听中", 18f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.Place(previewLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(40f, 0f), new Vector2(58f, 26f));
            Image track = LibraryWidgets.Panel("Track", _previewRow, 2f, new Color32(255, 255, 255, 31));
            LibraryWidgets.BottomStrip(track.rectTransform, 112f, 0f, 11f, 4f);
            _previewFill = LibraryWidgets.Panel("Progress", track.transform, 2f, LibraryTheme.Colors.DefaultAccent);
            _previewFill.rectTransform.anchorMin = Vector2.zero;
            _previewFill.rectTransform.anchorMax = Vector2.one;
            _previewFill.rectTransform.offsetMin = Vector2.zero;
            _previewFill.rectTransform.offsetMax = Vector2.zero;
            SetPreviewProgress(null);

            // Best record, filling the space between the jacket and the action row.
            Image card = LibraryWidgets.Panel("Record", _content, LibraryTheme.Radius.PANEL, _cardColor);
            LibraryWidgets.Inset(card.rectTransform, 0f, ACTION_HEIGHT + GAP, 0f, coverBottom + GAP);
            Image outline = LibraryWidgets.Shape(
                "Outline", card.transform, LibraryShapes.Ring(LibraryTheme.Radius.PANEL, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);

            RectTransform record = LibraryUi.CreateRect("Best", card.transform);
            LibraryWidgets.Inset(record, CARD_PADDING_X, CARD_PADDING_Y, CARD_PADDING_X, CARD_PADDING_Y);
            _record = record.gameObject;

            _rank = LibraryWidgets.Label(
                "Rank", record, string.Empty, 112f, LibraryTheme.Colors.DefaultAccent, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_rank);
            _rank.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(96f, 96f));
            TextMeshProUGUI bestCaption = LibraryWidgets.Label(
                "BestCaption", record, "最佳成绩", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(bestCaption.rectTransform, 128f, 240f, 4f, 26f);
            _score = LibraryWidgets.Label(
                "Score", record, string.Empty, LibraryTheme.FontSize.SCORE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryUi.UseCondensedFont(_score);
            _score.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.TopStrip(_score.rectTransform, 128f, 240f, 30f, 66f);

            Image clearRing = LibraryWidgets.Shape(
                "Clear", record, LibraryShapes.Ring(18f, 2f), new Color32(255, 255, 255, 72));
            LibraryWidgets.Place(
                clearRing.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(170f, 36f));
            _clear = LibraryWidgets.Label(
                "Label", clearRing.transform, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_clear);
            LibraryUi.Stretch(_clear.rectTransform);
            _played = LibraryWidgets.Label(
                "Played", record, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(
                _played.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(240f, 28f));

            Image divider = LibraryUi.CreateImage("Divider", record, LibraryTheme.Colors.Hairline);
            divider.raycastTarget = false;
            LibraryWidgets.TopStrip(divider.rectTransform, 0f, 0f, 116f, 1f);

            for (int i = 0; i < _judgeNames.Length; i++)
            {
                TextMeshProUGUI caption = LibraryWidgets.Label(
                    "JudgeName" + i, record, _judgeNames[i], 16f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
                LibraryUi.UseCondensedFont(caption);
                LibraryWidgets.Place(
                    caption.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(i * JUDGE_WIDTH, 40f), new Vector2(JUDGE_WIDTH, 22f));
                _judges[i] = LibraryWidgets.Label("Judge" + i, record, string.Empty, 32f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_judges[i]);
                LibraryWidgets.Place(
                    _judges[i].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(i * JUDGE_WIDTH, 0f), new Vector2(JUDGE_WIDTH, 40f));
            }

            TextMeshProUGUI comboCaption = LibraryWidgets.Label(
                "ComboCaption", record, "最大连击", 16f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(
                comboCaption.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(220f, 22f));
            _combo = LibraryWidgets.Label(
                "Combo", record, string.Empty, 32f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Right);
            _combo.richText = true;
            LibraryUi.UseCondensedFont(_combo);
            LibraryWidgets.Place(
                _combo.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(240f, 40f));

            RectTransform noRecord = LibraryUi.CreateRect("NoRecord", card.transform);
            LibraryWidgets.Inset(noRecord, CARD_PADDING_X, CARD_PADDING_Y, CARD_PADDING_X, CARD_PADDING_Y);
            _noRecord = noRecord.gameObject;
            _noRecordTitle = LibraryWidgets.Label(
                "Title", noRecord, string.Empty, LibraryTheme.FontSize.ROW_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(
                _noRecordTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 6f), new Vector2(700f, 36f));
            _noRecordBody = LibraryUi.CreateText(
                "Body", noRecord, string.Empty, LibraryTheme.FontSize.BODY, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _noRecordBody.color = LibraryTheme.Colors.TextMuted;
            _noRecordBody.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _noRecordBody.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _noRecordBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            _noRecordBody.rectTransform.offsetMin = new Vector2(0f, -64f);
            _noRecordBody.rectTransform.offsetMax = new Vector2(0f, -4f);

            // Action row.
            RectTransform actions = LibraryUi.CreateRect("Actions", _content);
            LibraryWidgets.BottomStrip(actions, 0f, 0f, 0f, ACTION_HEIGHT);

            Button auto = LibraryWidgets.PillButton(
                "Auto", actions, string.Empty, ToggleAuto, AUTO_WIDTH, ACTION_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.None);
            LibraryWidgets.Place(
                (RectTransform)auto.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(AUTO_WIDTH, ACTION_HEIGHT));
            _autoTrack = LibraryWidgets.Panel("Track", auto.transform, 16f, _switchOff);
            LibraryWidgets.Place(
                _autoTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(56f, 32f));
            Image knob = LibraryWidgets.Panel("Knob", _autoTrack.transform, 12f, LibraryTheme.Colors.TextPrimary);
            _autoKnob = knob.rectTransform;
            LibraryWidgets.Place(_autoKnob, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(24f, 24f));
            TextMeshProUGUI autoLabel = LibraryWidgets.Label(
                "Caption", auto.transform, "自动演奏", 22f, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.Inset(autoLabel.rectTransform, 96f, 0f, 16f, 0f);

            Button moreButton = LibraryWidgets.PillButton(
                "More", actions, string.Empty, () => more?.Invoke(), ACTION_HEIGHT, ACTION_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.None);
            LibraryWidgets.Icon(moreButton.transform, LibraryIcon.More, LibraryTheme.Colors.TextPrimary, 28f);
            LibraryWidgets.Place(
                (RectTransform)moreButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(ACTION_HEIGHT, ACTION_HEIGHT));

            _startButton = LibraryWidgets.PillButton(
                "Start", actions, "开始演奏", Start, 0f, ACTION_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, 28f, FontStyles.Bold, UiCue.None);
            LibraryWidgets.Inset((RectTransform)_startButton.transform, AUTO_WIDTH + 20f, 0f, ACTION_HEIGHT + 20f, 0f);
            _startBackground = (Image)_startButton.targetGraphic;
            _startLabel = _startButton.GetComponentInChildren<TextMeshProUGUI>();
            _startIcon = LibraryWidgets.Icon(_startButton.transform, LibraryIcon.Play, LibraryTheme.Colors.TextOnAccent, 26f);
            _startIcon.rectTransform.anchoredPosition = new Vector2(-63f, 0f);

            Show(null, null, false);
        }

        internal void SetSourceAvailable(bool available) => _source.gameObject.SetActive(available);

        internal void SetRankStatus(int level, string status)
        {
            _level.text = (level > 0 ? "LV " + level + "  ·  " : string.Empty) + status;
            LayoutDifficulty();
        }

        /// <summary>Fills the panel for a package; null clears it for an empty list.</summary>
        internal void Show(SongPackageItem item, CustomMusicScorePlayHistorySummary summary, bool animate)
        {
            _contentGroup.gameObject.SetActive(item != null);
            _cover.sprite = null;
            _cover.enabled = false;
            _coverPlaceholder.SetActive(true);
            if (item == null)
            {
                return;
            }

            SongPackageManifest manifest = item.Entry?.Manifest;
            _playable = LibraryModel.IsPlayable(item);
            _difficulty.text = LibraryModel.DifficultyLabel(item);
            _difficulty.color = LibraryTheme.Colors.Difficulty(manifest?.musicDifficultyType);
            int level = LibraryModel.PlayLevelOf(item);
            _level.text = level > 0 ? "LV " + level : string.Empty;
            LayoutDifficulty();
            _title.text = LibraryModel.BuildTitle(item);
            LayoutTitleAndCredits();
            _credits[0].text = OrDash(manifest?.singer);
            _credits[1].text = OrDash(manifest?.composer);
            _credits[2].text = OrDash(manifest?.userName);

            ShowScore(item, summary);

            _startButton.interactable = _playable;
            string unavailableReason = item.StatusText == "缺少音频" ? "缺少音频文件" : item.StatusText;
            _startLabel.text = _playable ? "开始演奏" : "无法演奏 · " + unavailableReason;
            _startLabel.fontSize = _playable ? 28f : 24f;
            LibraryWidgets.Inset(_startLabel.rectTransform, _playable ? 40f : 0f, 0f, 0f, 0f);
            _startIcon.enabled = _playable;
            ApplyAccent();

            _showMotion.TryCancel();
            _showMotion = default;
            _contentGroup.alpha = 1f;
            _coverFrame.localScale = Vector3.one;
            if (animate)
            {
                _contentGroup.alpha = 0.3f;
                _coverFrame.localScale = new Vector3(0.96f, 0.96f, 1f);
                _showMotion = LSequence.Create()
                    .Append(LMotion.Create(_contentGroup.alpha, 1f, LibraryTheme.Motion.STANDARD)
                        .WithEase(LibraryTheme.Motion.FADE_EASE)
                        .BindToAlpha(_contentGroup))
                    .Join(LMotion.Create(_coverFrame.localScale, Vector3.one, LibraryTheme.Motion.STANDARD)
                        .WithEase(LibraryTheme.Motion.SELECT_EASE)
                        .BindToLocalScale(_coverFrame))
                    .Run();
            }
        }

        /// <summary>
        /// Redraws only the play-history part (length, best run, clear state, last played) of the
        /// package already shown: the cover, labels and motion stay as they are.
        /// </summary>
        internal void ShowScore(SongPackageItem item, CustomMusicScorePlayHistorySummary summary)
        {
            if (item == null)
            {
                return;
            }

            SongPackageManifest manifest = item.Entry?.Manifest;
            CustomMusicScorePlayHistoryRecord best = summary?.Best;
            long knownLength = best?.MusicLengthMs ?? 0L;
            if (knownLength <= 0 && item.HasAudio && manifest != null)
            {
                knownLength = manifest.secForMusicScoreMaker * 1000L;
            }
            _length.text = knownLength > 0 ? FormatLength(knownLength) : string.Empty;
            _record.SetActive(best != null);
            _noRecord.SetActive(best == null);
            if (best != null)
            {
                _rank.text = best.Rank ?? string.Empty;
                _score.text = best.TotalScore.ToString("N0", CultureInfo.InvariantCulture);
                _clear.text = LibraryModel.ClearLabel(summary);
                _played.text = "上次游玩 " + FormatDate(summary.LastPlayedAtUtc);
                _judges[0].text = best.PerfectCount.ToString("N0", CultureInfo.InvariantCulture);
                _judges[1].text = best.GreatCount.ToString("N0", CultureInfo.InvariantCulture);
                _judges[2].text = best.GoodCount.ToString("N0", CultureInfo.InvariantCulture);
                _judges[3].text = best.BadCount.ToString("N0", CultureInfo.InvariantCulture);
                _judges[4].text = best.MissCount.ToString("N0", CultureInfo.InvariantCulture);
                _combo.text = best.MaxCombo.ToString("N0", CultureInfo.InvariantCulture)
                    + "<size=70%><color=#9AA6B5> / " + best.TotalComboCount.ToString("N0", CultureInfo.InvariantCulture)
                    + "</color></size>";
            }
            else
            {
                bool playable = LibraryModel.IsPlayable(item);
                _noRecordTitle.text = playable ? "还没有游玩记录" : "曲包不完整";
                _noRecordBody.text = playable
                    ? "完成一次演奏后，这里会显示最佳成绩、判定分布和最大连击。"
                    : item.StatusText + "。重新导入完整的曲包后即可演奏。";
            }
        }

        private void LayoutDifficulty()
        {
            float chipWidth = Mathf.Min(DIFFICULTY_MAX_WIDTH,
                Mathf.Ceil(_difficulty.GetPreferredValues(_difficulty.text).x) + DIFFICULTY_PADDING * 2f);
            _difficultyChip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, chipWidth);

            Vector2 topLeft = new Vector2(0f, 1f);
            float cursor = chipWidth + METADATA_GAP;
            if (_level.text.Length > 0)
            {
                float levelWidth = Mathf.Ceil(_level.GetPreferredValues(_level.text).x);
                LibraryWidgets.Place(_level.rectTransform, topLeft, topLeft,
                    new Vector2(cursor, 0f), new Vector2(levelWidth, DIFFICULTY_HEIGHT));
                cursor += levelWidth + METADATA_GAP;
            }

            LibraryWidgets.TopStrip(_length.rectTransform, cursor, 0f, 0f, DIFFICULTY_HEIGHT);
        }

        private void LayoutTitleAndCredits()
        {
            const float TITLE_TOP = 54f;
            const float CREDIT_GAP = 18f;
            float titleHeight = Mathf.Clamp(Mathf.Ceil(_title.GetPreferredValues(_title.text, _info.rect.width, 0f).y),
                TITLE_LINE_HEIGHT, TITLE_LINE_HEIGHT * 2f);
            LibraryWidgets.TopStrip(_title.rectTransform, 0f, 0f, TITLE_TOP, titleHeight);
            for (int i = 0; i < _credits.Length; i++)
            {
                float top = TITLE_TOP + titleHeight + CREDIT_GAP + i * 36f;
                Vector2 anchor = new Vector2(0f, 1f);
                LibraryWidgets.Place(_creditNames[i], anchor, anchor, new Vector2(0f, -top), new Vector2(64f, 30f));
                LibraryWidgets.TopStrip(_credits[i].rectTransform, 68f, 0f, top, 30f);
            }
        }

        /// <summary>The jacket on screen, or null while it is still the placeholder.</summary>
        internal Sprite Cover => _cover.enabled ? _cover.sprite : null;

        internal void SetAudioLength(long milliseconds)
        {
            if (milliseconds > 0 && _length != null)
            {
                _length.text = FormatLength(milliseconds);
            }
        }

        internal void SetPreviewProgress(float? progress)
        {
            if (_previewRow == null)
            {
                return;
            }

            _previewRow.gameObject.SetActive(progress.HasValue);
            _previewFill.rectTransform.anchorMax = new Vector2(progress.GetValueOrDefault(), 1f);
        }

        /// <summary>Shows the jacket once it is decoded and moves the accent to its palette.</summary>
        internal void SetThumbnail(JacketThumbnail thumbnail)
        {
            _cover.sprite = thumbnail.Sprite;
            _cover.enabled = thumbnail.Sprite != null;
            _coverPlaceholder.SetActive(thumbnail.Sprite == null);
            SetAccent(thumbnail.Palette.Accent, true);
        }

        internal void SetAccent(Color32 accent, bool animate)
        {
            _accentMotion.TryCancel();
            _accentMotion = default;
            if (!animate)
            {
                _accent = accent;
                ApplyAccent();
                return;
            }

            _accentMotion = LMotion.Create(_accent, (Color)accent, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithImmediateBind(false)
                .Bind(this, static (value, panel) =>
                {
                    panel._accent = value;
                    panel.ApplyAccent();
                });
        }

        public void Dispose()
        {
            _showMotion.TryCancel();
            _accentMotion.TryCancel();
            _autoMotion.TryCancel();
        }

        private void ApplyAccent()
        {
            _rank.color = _accent;
            _startBackground.color = _playable ? _accent : (Color)LibraryTheme.Colors.Control;
            _startLabel.color = _playable ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
            _autoTrack.color = _isAuto ? _accent : (Color)_switchOff;
            _previewIcon.color = _accent;
            _previewFill.color = _accent;
        }

        private void ToggleAuto()
        {
            _isAuto = !_isAuto;
            UiSound.Play(_isAuto ? UiCue.ToggleOn : UiCue.ToggleOff);
            ApplyAccent();
            _autoMotion.TryCancel();
            _autoMotion = LMotion.Create(_autoKnob.anchoredPosition.x, _isAuto ? 28f : 4f, LibraryTheme.Motion.STANDARD)
                .WithEase(LibraryTheme.Motion.SELECT_EASE)
                .BindToAnchoredPositionX(_autoKnob);
        }

        private void Start()
        {
            if (_playable)
            {
                UiSound.Play(UiCue.Start);
                _play?.Invoke(_isAuto);
            }
            else
            {
                UiSound.Play(UiCue.Blocked);
            }
        }

        private static string OrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static string FormatLength(long milliseconds)
        {
            long seconds = milliseconds / 1000L;
            return seconds / 60L + ":" + (seconds % 60L).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(string isoUtc)
        {
            return DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)
                ? time.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "—";
        }
    }
}
