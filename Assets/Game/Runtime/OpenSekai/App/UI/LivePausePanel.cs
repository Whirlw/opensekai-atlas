using System;
using LitMotion;
using OpenSekai.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The pause card of a live started from the library, in the library's look: the jacket and
    /// title in the header with the difficulty, level and charter under it, two action rows for
    /// 重新开始 and 返回曲库, and a wide accent 继续 button. Both rows ask for a second tap — the
    /// row turns to its warning color and says so — so a stray tap cannot throw the run away.
    /// The scrim is inert: the live only goes on through 继续.
    /// </summary>
    internal sealed class LivePausePanel : MonoBehaviour, ILivePauseMenu
    {
        private const float CARD_WIDTH = 780f;
        private const float CARD_HEIGHT = 492f;
        private const float COVER_SIZE = 64f;
        private const float ACTION_HEIGHT = 104f;
        private const float ACTION_GAP = 12f;
        private const float ICON_SIZE = 60f;
        private const float RESUME_HEIGHT = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
        private const float STATUS_WIDTH = 140f;

        private LibraryModal _modal;
        private RawImage _cover;
        private Image _coverPlaceholder;
        private Image _resumeBackground;
        private ArmableRow _retry;
        private ArmableRow _quit;

        private Action _onResume;
        private Action _onRetry;
        private Action _onQuit;

        public bool IsVisible => _modal != null && _modal.IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            LivePauseMenus.Factory = Create;
        }

        internal static ILivePauseMenu Create(Transform parent)
        {
            GameObject go = new GameObject("LivePausePanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LivePausePanel panel = go.AddComponent<LivePausePanel>();
            panel.Build();
            return panel;
        }

        public void Show(LivePauseInfo info, Action resume, Action retry, Action quit)
        {
            info ??= new LivePauseInfo();
            _onResume = resume;
            _onRetry = retry;
            _onQuit = quit;

            _modal.Title.text = string.IsNullOrWhiteSpace(info.Title) ? "未命名曲目" : info.Title;
            _modal.Subtitle.text = Subtitle(info);
            _cover.texture = info.Jacket;
            _cover.enabled = info.Jacket != null;
            _coverPlaceholder.enabled = info.Jacket == null;

            Color32 accent = info.Accent;
            accent.a = 255;
            _resumeBackground.color = accent;

            _retry.SetArmed(false, false);
            _quit.SetArmed(false, false);
            _modal.Open();
        }

        public void Hide()
        {
            _onResume = null;
            _onRetry = null;
            _onQuit = null;
            _retry?.SetArmed(false, false);
            _quit?.SetArmed(false, false);
            _modal?.Close();
        }

        private void OnDestroy()
        {
            _retry?.Dispose();
            _quit?.Dispose();
            _modal?.Dispose();
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            gameObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)transform;
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), string.Empty, null, withSubtitle: true);

            // Header: the jacket in front of the title, as on the package action card.
            float padding = LibraryTheme.Layout.MODAL_PADDING;
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            RectTransform coverFrame = LibraryUi.CreateRect("Cover", _modal.Card);
            LibraryWidgets.Place(coverFrame, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(padding, -header * 0.5f - 2f), new Vector2(COVER_SIZE, COVER_SIZE));
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.COVER_SMALL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 28f);
            GameObject jacket = new GameObject("Jacket", typeof(RectTransform), typeof(RawImage));
            jacket.transform.SetParent(frame.transform, false);
            _cover = jacket.GetComponent<RawImage>();
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);

            float textLeft = padding + COVER_SIZE + 20f;
            float textRight = padding + STATUS_WIDTH + 12f;
            SetHorizontalInsets(_modal.Title.rectTransform, textLeft, textRight);
            SetHorizontalInsets(_modal.Subtitle.rectTransform, textLeft, textRight);
            _modal.Title.fontSize = 30f;
            _modal.Subtitle.richText = true;

            TextMeshProUGUI status = LibraryWidgets.Label(
                "Status", _modal.Card, "PAUSED", 22f, LibraryTheme.Colors.TextMuted, FontStyles.Bold, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(status);
            status.characterSpacing = 6f;
            LibraryWidgets.Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-padding, -header * 0.5f), new Vector2(STATUS_WIDTH, 32f));

            RectTransform body = _modal.Body;
            body.offsetMax = new Vector2(body.offsetMax.x, -header - 8f);

            _retry = new ArmableRow(body, 0, "Retry", LibraryIcon.Refresh,
                "重新开始", "从头再打一次",
                "再点一次重新开始", "这一局的进度会清空",
                LibraryTheme.Colors.Warning, LibraryTheme.Colors.TextPrimary, OnRetryClicked);
            _quit = new ArmableRow(body, 1, "Quit", LibraryIcon.Back,
                "返回曲库", "结束这一局，回到曲库",
                "再点一次返回曲库", "这一局会按中途退出结算",
                LibraryTheme.Colors.Danger, LibraryTheme.Colors.TextPrimary, OnQuitClicked);

            Button resume = LibraryWidgets.PillButton(
                "Resume", body, "继续", OnResumeClicked, 0f, RESUME_HEIGHT,
                LibraryTheme.Colors.DefaultAccent, LibraryTheme.Colors.TextOnAccent, 28f, FontStyles.Bold, UiCue.Forward);
            LibraryWidgets.BottomStrip((RectTransform)resume.transform, 0f, 0f, 0f, RESUME_HEIGHT);
            _resumeBackground = (Image)resume.targetGraphic;
            TextMeshProUGUI resumeLabel = resume.GetComponentInChildren<TextMeshProUGUI>();
            LibraryWidgets.Inset(resumeLabel.rectTransform, 40f, 0f, 0f, 0f);
            Image resumeIcon = LibraryWidgets.Icon(resume.transform, LibraryIcon.Play, LibraryTheme.Colors.TextOnAccent, 26f);
            resumeIcon.rectTransform.anchoredPosition = new Vector2(-40f, 0f);

            _modal.Close();
        }

        private void OnResumeClicked()
        {
            Action action = _onResume;
            Hide();
            action?.Invoke();
        }

        private void OnRetryClicked()
        {
            if (!_retry.Armed)
            {
                UiSound.Play(UiCue.Warning);
                _quit.SetArmed(false, false);
                _retry.SetArmed(true, true);
                return;
            }

            UiSound.Play(UiCue.Retry);
            Action action = _onRetry;
            Hide();
            action?.Invoke();
        }

        private void OnQuitClicked()
        {
            if (!_quit.Armed)
            {
                UiSound.Play(UiCue.Warning);
                _retry.SetArmed(false, false);
                _quit.SetArmed(true, true);
                return;
            }

            UiSound.Play(UiCue.Back);
            Action action = _onQuit;
            Hide();
            action?.Invoke();
        }

        /// <summary>"MASTER  LV 32  ·  谱面 X  ·  自动", the difficulty in its own color; parts the song lacks are left out.</summary>
        private static string Subtitle(LivePauseInfo info)
        {
            string text = string.Empty;
            if (!string.IsNullOrWhiteSpace(info.Difficulty))
            {
                string color = ColorUtility.ToHtmlStringRGB(LibraryTheme.Colors.Difficulty(info.Difficulty));
                text = "<color=#" + color + ">" + info.Difficulty.ToUpperInvariant() + "</color>";
            }
            if (info.Level > 0)
            {
                text += (text.Length > 0 ? "  " : string.Empty) + "LV " + info.Level;
            }
            if (!string.IsNullOrWhiteSpace(info.Charter))
            {
                text += (text.Length > 0 ? "  ·  " : string.Empty) + "谱面 " + info.Charter.Trim();
            }
            if (info.IsAuto)
            {
                text += (text.Length > 0 ? "  ·  " : string.Empty) + "自动";
            }
            return text;
        }

        private static void SetHorizontalInsets(RectTransform rect, float left, float right)
        {
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        }

        private static Color32 WithAlpha(Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// An action row in the package action card's shape (icon disc, title, one line of detail,
        /// chevron) that needs a second tap: the first one turns it to <c>armedColor</c> and shakes it.
        /// </summary>
        private sealed class ArmableRow : IDisposable
        {
            private readonly RectTransform _row;
            private readonly Image _background;
            private readonly Image _outline;
            private readonly Image _iconBackground;
            private readonly Image _icon;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _body;
            private readonly string _idleTitle;
            private readonly string _idleBody;
            private readonly string _armedTitle;
            private readonly string _armedBody;
            private readonly Color32 _armedColor;
            private readonly Color32 _idleColor;
            private MotionHandle _armMotion;

            internal bool Armed { get; private set; }

            internal ArmableRow(
                RectTransform parent, int index, string name, LibraryIcon icon,
                string idleTitle, string idleBody, string armedTitle, string armedBody,
                Color32 armedColor, Color32 idleColor, UnityEngine.Events.UnityAction onClick)
            {
                _idleTitle = idleTitle;
                _idleBody = idleBody;
                _armedTitle = armedTitle;
                _armedBody = armedBody;
                _armedColor = armedColor;
                _idleColor = idleColor;

                // Each click picks its own sound: the first arms with a warning, the second confirms.
                Button button = LibraryWidgets.PillButton(
                    name, parent, string.Empty, onClick, 0f, ACTION_HEIGHT,
                    LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.None);
                _background = (Image)button.targetGraphic;
                _background.sprite = LibraryShapes.RoundedRect(LibraryTheme.Radius.ROW);
                _row = _background.rectTransform;
                LibraryWidgets.TopStrip(_row, 0f, 0f, index * (ACTION_HEIGHT + ACTION_GAP), ACTION_HEIGHT);

                _outline = LibraryWidgets.Shape("Outline", _background.transform,
                    LibraryShapes.Ring(LibraryTheme.Radius.ROW, 1f), LibraryTheme.Colors.Hairline);
                LibraryUi.Stretch(_outline.rectTransform);

                _iconBackground = LibraryWidgets.Panel("IconBackground", _background.transform, ICON_SIZE * 0.5f, LibraryTheme.Colors.Control);
                LibraryWidgets.Place(_iconBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(24f, 0f), new Vector2(ICON_SIZE, ICON_SIZE));
                _icon = LibraryWidgets.Icon(_iconBackground.transform, icon, LibraryTheme.Colors.TextPrimary, 28f);

                float textLeft = 24f + ICON_SIZE + 22f;
                _title = LibraryWidgets.Label("Title", _background.transform, idleTitle, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.Place(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(textLeft, 2f), new Vector2(520f, 34f));
                _body = LibraryWidgets.Label("Body", _background.transform, idleBody, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.Place(_body.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f),
                    new Vector2(textLeft, -4f), new Vector2(520f, 28f));

                Image chevron = LibraryWidgets.Icon(_background.transform, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 26f);
                LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-26f, 0f), new Vector2(26f, 26f));
            }

            internal void SetArmed(bool armed, bool animate)
            {
                Armed = armed;
                _title.text = armed ? _armedTitle : _idleTitle;
                _body.text = armed ? _armedBody : _idleBody;
                _title.color = armed ? _armedColor : _idleColor;
                _icon.color = armed ? _armedColor : _idleColor;
                _body.color = armed ? _armedColor : LibraryTheme.Colors.TextMuted;
                _background.color = armed ? WithAlpha(_armedColor, 36) : LibraryTheme.Colors.Control;
                _outline.color = armed ? WithAlpha(_armedColor, 110) : LibraryTheme.Colors.Hairline;
                _iconBackground.color = armed ? WithAlpha(_armedColor, 60) : LibraryTheme.Colors.Control;

                _armMotion.TryCancel();
                _armMotion = default;
                _row.localRotation = Quaternion.identity;
                if (animate)
                {
                    _armMotion = LMotion.Punch.Create(0f, 1.6f, 0.36f)
                        .WithFrequency(8)
                        .WithDampingRatio(1f)
                        .Bind(_row, static (z, row) => row.localRotation = Quaternion.Euler(0f, 0f, z));
                }
            }

            public void Dispose()
            {
                _armMotion.TryCancel();
            }
        }
    }
}
