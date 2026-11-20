using System;
using System.Globalization;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// End-of-session card in the library's look: the jacket beside the outcome (status chips,
    /// title, rank, score and clear state), the judgement strip underneath, and 返回曲库 /
    /// 再玩一次. A load error keeps the same card and shows the reason in place of the score.
    /// </summary>
    public sealed class LibraryResultPanel : MonoBehaviour
    {
        private const float CARD_WIDTH = 1240f;
        private const float CARD_HEIGHT = 640f;
        private const float PADDING = LibraryTheme.Layout.MODAL_PADDING;
        private const float COVER_SIZE = 300f;
        private const float INFO_LEFT = PADDING + COVER_SIZE + 40f;
        private const float CHIP_HEIGHT = 36f;
        private const float STRIP_HEIGHT = 124f;
        private const float BUTTON_HEIGHT = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
        private const float SCORE_COUNT_SECONDS = 0.7f;

        private static readonly string[] _judgeNames = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };

        private Action<PlayResult> _onRetry;
        internal Action OnClose { private get; set; }
        private PlayResult _current;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;

        private LibraryModal _modal;
        private Image _cover;
        private GameObject _coverPlaceholder;
        private Chip _kindChip;
        private Chip _modeChip;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _titleText;
        private GameObject _scoreBlock;
        private TextMeshProUGUI _rankText;
        private TextMeshProUGUI _scoreCaption;
        private TextMeshProUGUI _scoreText;
        private Image _clearRing;
        private TextMeshProUGUI _clearText;
        private GameObject _errorBlock;
        private TextMeshProUGUI _errorBody;
        private GameObject _strip;
        private GameObject _judgeGroup;
        private readonly TextMeshProUGUI[] _judges = new TextMeshProUGUI[_judgeNames.Length];
        private TextMeshProUGUI _comboCaption;
        private TextMeshProUGUI _combo;
        private TextMeshProUGUI _autoNote;
        private Image _retryBackground;
        private TextMeshProUGUI _retryLabel;
        private Image _retryIcon;
        private MotionHandle _scoreMotion;

        public static LibraryResultPanel Create(Transform parent, Action<PlayResult> onRetry, string backLabel = "返回曲库")
        {
            RectTransform root = LibraryUi.CreateRect("ResultPanel", parent);
            LibraryUi.Stretch(root);
            LibraryResultPanel panel = root.gameObject.AddComponent<LibraryResultPanel>();
            panel._onRetry = onRetry;
            panel.Build(backLabel);
            panel.Hide();
            return panel;
        }

        public void Show(PlayResult result) => Show(result, null, _accent);

        /// <param name="cover">The package's jacket, when one is already decoded.</param>
        /// <param name="accent">Rank and 再玩一次 color; follows the jacket.</param>
        internal void Show(PlayResult result, Sprite cover, Color32 accent)
        {
            if (result == null)
            {
                Hide();
                return;
            }

            _current = result;
            _accent = accent;
            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.SetActive(cover == null);
            Refresh(result);
            _modal.Open(OpenSound(result));
            PlayScore(result);
        }

        /// <summary>
        /// Fills in the jacket and its accent once they are decoded. The card usually opens right as
        /// the page comes back from a live, before the selected package's jacket has loaded.
        /// </summary>
        internal void SetArtwork(Sprite cover, Color32 accent)
        {
            if (!_modal.IsOpen || _current == null)
            {
                return;
            }

            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.SetActive(cover == null);
            _accent = accent;
            ApplyKindChip(_current);
            ApplyAccent(_current);
        }

        private void ApplyKindChip(PlayResult result)
        {
            _kindChip.Set(result.KindText, KindColor(result));
        }

        /// <summary>Everything drawn in the jacket's accent: rank, 再玩一次 and a finished run's clear badge.</summary>
        private void ApplyAccent(PlayResult result)
        {
            _retryBackground.color = _accent;
            _rankText.color = _accent;
            Color32 clearColor = result.Kind == PlayResultKind.Completed ? _accent : LibraryTheme.Colors.TextSecondary;
            _clearText.color = clearColor;
            _clearRing.color = new Color32(clearColor.r, clearColor.g, clearColor.b, 110);
        }

        internal bool IsOpen => _modal != null && _modal.IsOpen;

        /// <summary>The Android back key: closes the card the way 返回曲库 does.</summary>
        internal void Dismiss()
        {
            UiSound.Play(UiCue.Back);
            Close();
        }

        public void Hide()
        {
            _scoreMotion.TryCancel();
            _scoreMotion = default;
            _modal.Close();
        }

        private void OnDestroy()
        {
            _scoreMotion.TryCancel();
            _modal?.Dispose();
        }

        private void Build(string backLabel)
        {
            var root = (RectTransform)transform;
            // No close button and an inert scrim: the session ends on one of the two actions.
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), string.Empty, null);
            _modal.Title.gameObject.SetActive(false);
            RectTransform card = _modal.Card;
            Vector2 topLeft = new Vector2(0f, 1f);

            // Jacket.
            RectTransform coverFrame = LibraryUi.CreateRect("Cover", card);
            LibraryWidgets.Place(coverFrame, topLeft, topLeft, new Vector2(PADDING, -PADDING), new Vector2(COVER_SIZE, COVER_SIZE));
            LibraryWidgets.Shadow(coverFrame, LibraryTheme.Radius.PANEL, 28f, 6f, 90);
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 72f).gameObject;
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);

            // Info column: chips, title, then the score or the load error.
            RectTransform info = LibraryUi.CreateRect("Info", card);
            LibraryWidgets.TopStrip(info, INFO_LEFT, PADDING, PADDING, COVER_SIZE);

            _kindChip = new Chip(info, "Kind");
            _modeChip = new Chip(info, "Mode");
            _level = LibraryWidgets.Label("Level", info, string.Empty, 22f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
            LibraryUi.UseCondensedFont(_level);

            _titleText = LibraryWidgets.Label(
                "Title", info, string.Empty, LibraryTheme.FontSize.DETAIL_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_titleText.rectTransform, 0f, 0f, CHIP_HEIGHT + 16f, 60f);

            RectTransform scoreBlock = LibraryUi.CreateRect("Score", info);
            LibraryWidgets.BottomStrip(scoreBlock, 0f, 0f, 0f, 150f);
            _scoreBlock = scoreBlock.gameObject;
            _rankText = LibraryWidgets.Label(
                "Rank", scoreBlock, string.Empty, 150f, LibraryTheme.Colors.DefaultAccent, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            LibraryUi.UseCondensedFont(_rankText);
            _rankText.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_rankText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(-4f, -14f), new Vector2(150f, 160f));
            _scoreCaption = LibraryWidgets.Label(
                "ScoreCaption", scoreBlock, "分数", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_scoreCaption.rectTransform, Vector2.zero, Vector2.zero, new Vector2(164f, 96f), new Vector2(300f, 28f));
            _scoreText = LibraryWidgets.Label(
                "Score", scoreBlock, string.Empty, 92f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            LibraryUi.UseCondensedFont(_scoreText);
            _scoreText.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_scoreText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(160f, -6f), new Vector2(420f, 104f));

            _clearRing = LibraryWidgets.Shape("Clear", scoreBlock, LibraryShapes.Ring(20f, 2f), new Color32(255, 255, 255, 72));
            LibraryWidgets.Place(_clearRing.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(200f, 44f));
            _clearText = LibraryWidgets.Label(
                "Label", _clearRing.transform, string.Empty, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_clearText);
            _clearText.characterSpacing = 4f;
            LibraryUi.Stretch(_clearText.rectTransform);

            // A load error: a plain sentence under the title, and the reason where the judgements go.
            RectTransform errorBlock = LibraryUi.CreateRect("Error", card);
            LibraryUi.Stretch(errorBlock);
            _errorBlock = errorBlock.gameObject;
            TextMeshProUGUI errorLead = LibraryWidgets.Label(
                "Lead", errorBlock, "曲包没能载入，这一局没有开始。", 24f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(errorLead.rectTransform, INFO_LEFT, PADDING, PADDING + CHIP_HEIGHT + 96f, 36f);
            Image reason = LibraryWidgets.Panel("Reason", errorBlock, LibraryTheme.Radius.ROW, LibraryTheme.Colors.DangerSurface);
            LibraryWidgets.TopStrip(reason.rectTransform, PADDING, PADDING, PADDING + COVER_SIZE + 28f, STRIP_HEIGHT);
            TextMeshProUGUI reasonCaption = LibraryWidgets.Label(
                "Caption", reason.transform, "原因", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.Danger, FontStyles.Bold);
            LibraryWidgets.TopStrip(reasonCaption.rectTransform, 36f, 36f, 22f, 26f);
            _errorBody = LibraryUi.CreateText(
                "Body", reason.transform, string.Empty, 24f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _errorBody.color = LibraryTheme.Colors.TextPrimary;
            _errorBody.textWrappingMode = TextWrappingModes.Normal;
            _errorBody.overflowMode = TextOverflowModes.Ellipsis;
            LibraryWidgets.Inset(_errorBody.rectTransform, 36f, 16f, 36f, 56f);

            // Judgement strip, the full width under the jacket.
            Image strip = LibraryWidgets.Panel("Judgements", card, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryWidgets.TopStrip(strip.rectTransform, PADDING, PADDING, PADDING + COVER_SIZE + 28f, STRIP_HEIGHT);
            LibraryWidgets.Outline(strip, LibraryTheme.Radius.ROW);
            _strip = strip.gameObject;

            RectTransform judges = LibraryUi.CreateRect("Grades", strip.transform);
            LibraryWidgets.Inset(judges, 36f, 24f, 36f, 24f);
            _judgeGroup = judges.gameObject;
            const float JUDGE_WIDTH = 150f;
            for (int i = 0; i < _judgeNames.Length; i++)
            {
                TextMeshProUGUI caption = LibraryWidgets.Label(
                    "JudgeName" + i, judges, _judgeNames[i], 17f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
                LibraryUi.UseCondensedFont(caption);
                caption.characterSpacing = 3f;
                LibraryWidgets.Place(caption.rectTransform, topLeft, topLeft, new Vector2(i * JUDGE_WIDTH, 0f), new Vector2(JUDGE_WIDTH, 24f));
                _judges[i] = LibraryWidgets.Label("Judge" + i, judges, string.Empty, 40f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_judges[i]);
                LibraryWidgets.Place(_judges[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * JUDGE_WIDTH, -4f), new Vector2(JUDGE_WIDTH, 52f));
            }

            _comboCaption = LibraryWidgets.Label(
                "ComboCaption", strip.transform, "最大连击", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(_comboCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -22f), new Vector2(260f, 26f));
            _combo = LibraryWidgets.Label(
                "Combo", strip.transform, string.Empty, 40f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Right);
            _combo.richText = true;
            LibraryUi.UseCondensedFont(_combo);
            LibraryWidgets.Place(_combo.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 20f), new Vector2(300f, 52f));

            _autoNote = LibraryWidgets.Label(
                "AutoNote", strip.transform, string.Empty, 22f, LibraryTheme.Colors.TextSecondary);
            _autoNote.richText = true;
            LibraryWidgets.Inset(_autoNote.rectTransform, 36f, 0f, 360f, 0f);

            // Actions.
            RectTransform actions = LibraryUi.CreateRect("Actions", card);
            LibraryWidgets.BottomStrip(actions, PADDING, PADDING, PADDING, BUTTON_HEIGHT);
            const float BACK_WIDTH = 220f;
            const float RETRY_WIDTH = 280f;
            Button back = LibraryWidgets.PillButton(
                "Back", actions, backLabel, Close, BACK_WIDTH, BUTTON_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 24f, sound: UiCue.Back);
            LibraryWidgets.Outline((Image)back.targetGraphic, BUTTON_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)back.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-(RETRY_WIDTH + 16f), 0f), new Vector2(BACK_WIDTH, BUTTON_HEIGHT));

            Button retry = LibraryWidgets.PillButton(
                "Retry", actions, "再玩一次", Retry, RETRY_WIDTH, BUTTON_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, 26f, FontStyles.Bold, UiCue.Retry);
            LibraryWidgets.Place((RectTransform)retry.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(RETRY_WIDTH, BUTTON_HEIGHT));
            _retryBackground = (Image)retry.targetGraphic;
            _retryLabel = retry.GetComponentInChildren<TextMeshProUGUI>();
            LibraryWidgets.Inset(_retryLabel.rectTransform, 36f, 0f, 0f, 0f);
            _retryIcon = LibraryWidgets.Icon(retry.transform, LibraryIcon.Refresh, LibraryTheme.Colors.TextOnAccent, 26f);
            _retryIcon.rectTransform.anchoredPosition = new Vector2(-58f, 0f);
        }

        private void Retry()
        {
            PlayResult result = _current;
            Hide();
            _onRetry?.Invoke(result);
        }

        private void Close()
        {
            Hide();
            OnClose?.Invoke();
        }

        /// <summary>A full combo or all perfect gets the achievement chime, a clear the success one.</summary>
        private static UiCue OpenSound(PlayResult result)
        {
            switch (result.Kind)
            {
                case PlayResultKind.Completed:
                    bool fullCombo = !result.IsAuto && result.TotalCombo > 0
                        && result.Miss == 0 && result.Bad == 0 && result.Good == 0;
                    return fullCombo ? UiCue.Achievement : UiCue.Success;
                case PlayResultKind.LoadError:
                    return UiCue.Error;
                default:
                    return UiCue.Open;
            }
        }

        private void Refresh(PlayResult result)
        {
            _titleText.text = result.Title;
            ApplyKindChip(result);
            _modeChip.Set(result.IsAuto ? "自动演奏" : "手动演奏", LibraryTheme.Colors.TextSecondary);
            _level.text = result.PlayLevel > 0 ? "LV " + result.PlayLevel : string.Empty;
            LayoutChips();

            ApplyAccent(result);

            bool hasScore = result.HasScore;
            _scoreBlock.SetActive(hasScore);
            _strip.SetActive(hasScore);
            _errorBlock.SetActive(!hasScore);
            _retryLabel.text = hasScore ? "再玩一次" : "重试";
            // Icon and label sit together as one centered group, whatever the label's length.
            const float ICON_GAP = 10f;
            float labelWidth = Mathf.Ceil(_retryLabel.GetPreferredValues(_retryLabel.text).x);
            float group = 26f + ICON_GAP + labelWidth;
            _retryIcon.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + 13f, 0f);
            LibraryWidgets.Inset(_retryLabel.rectTransform, 26f + ICON_GAP, 0f, 0f, 0f);
            if (!hasScore)
            {
                _errorBody.text = string.IsNullOrWhiteSpace(result.Message) ? "曲包无法载入。" : result.Message;
                return;
            }

            _rankText.text = result.IsAllPerfectClear ? "AP" : result.Rank.ToString();
            _scoreText.text = Format(result.Score);
            string clear = ClearLabel(result);
            _clearRing.gameObject.SetActive(clear.Length > 0);
            _clearText.text = clear;

            _judgeGroup.SetActive(!result.IsAuto);
            _comboCaption.text = result.IsAuto ? "总音符" : "最大连击";
            if (result.IsAuto)
            {
                _combo.text = Format(result.TotalCombo);
                _autoNote.gameObject.SetActive(true);
                _autoNote.text = "自动判定  <color=#F2F4F7>" + Format(result.AutoCount)
                    + "</color>\n<size=80%><color=#9AA6B5>自动模式不计判定等级，也不计入最佳成绩</color></size>";
            }
            else
            {
                // The game only knows five judgement grades on screen: a just-perfect hit is still a
                // PERFECT. Showing a separate "JP" column would invent a grade the HUD never had.
                _autoNote.gameObject.SetActive(false);
                _judges[0].text = Format(result.Perfect);
                _judges[1].text = Format(result.Great);
                _judges[2].text = Format(result.Good);
                _judges[3].text = Format(result.Bad);
                _judges[4].text = Format(result.Miss);
                _combo.text = Format(result.MaxCombo)
                    + "<size=70%><color=#9AA6B5> / " + Format(result.TotalCombo) + "</color></size>";
            }
        }

        private void LayoutChips()
        {
            float cursor = _kindChip.Layout(0f);
            cursor = _modeChip.Layout(cursor + 10f);
            Vector2 topLeft = new Vector2(0f, 1f);
            LibraryWidgets.Place(_level.rectTransform, topLeft, topLeft, new Vector2(cursor + 14f, 0f), new Vector2(120f, CHIP_HEIGHT));
        }

        /// <summary>Counts the score up as the card opens.</summary>
        private void PlayScore(PlayResult result)
        {
            _scoreMotion.TryCancel();
            _scoreMotion = default;
            if (!result.HasScore || result.Score <= 0)
            {
                return;
            }

            _scoreMotion = LMotion.Create(0f, result.Score, SCORE_COUNT_SECONDS)
                .WithEase(Ease.OutCubic)
                .WithDelay(LibraryTheme.Motion.FAST)
                .Bind(_scoreText, static (value, text) =>
                    text.text = Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture));
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

        private static string ClearLabel(PlayResult result)
        {
            if (result.IsAuto)
            {
                return "AUTO";
            }

            switch (result.Kind)
            {
                case PlayResultKind.Completed:
                    if (result.IsAllPerfectClear) return "ALL PERFECT";
                    return result.TotalCombo > 0 && result.MaxCombo >= result.TotalCombo ? "FULL COMBO" : "CLEAR";
                case PlayResultKind.Failed: return "FAILED";
                case PlayResultKind.Retired: return "RETIRED";
                default: return string.Empty;
            }
        }

        private static string Format(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Small rounded status label whose width follows its text.</summary>
        private sealed class Chip
        {
            private const float PADDING_X = 14f;
            private readonly Image _background;
            private readonly TextMeshProUGUI _label;

            internal Chip(RectTransform parent, string name)
            {
                _background = LibraryWidgets.Panel(name, parent, LibraryTheme.Radius.CHIP, LibraryTheme.Colors.Hairline);
                _label = LibraryWidgets.Label(
                    "Label", _background.transform, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextSecondary,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                LibraryWidgets.Inset(_label.rectTransform, PADDING_X, 0f, PADDING_X, 0f);
            }

            internal void Set(string text, Color32 color)
            {
                _label.text = text;
                _label.color = color;
                _background.color = new Color32(color.r, color.g, color.b, 30);
            }

            /// <returns>The x where the chip ends.</returns>
            internal float Layout(float x)
            {
                float width = Mathf.Ceil(_label.GetPreferredValues(_label.text).x) + PADDING_X * 2f;
                Vector2 topLeft = new Vector2(0f, 1f);
                LibraryWidgets.Place(_background.rectTransform, topLeft, topLeft, new Vector2(x, 0f), new Vector2(width, CHIP_HEIGHT));
                return x + width;
            }
        }
    }
}
