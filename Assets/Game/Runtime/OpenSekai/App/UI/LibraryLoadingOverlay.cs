using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Full-page "preparing" screen shown while a package is read and registered: the jacket over
    /// a glow in its accent, the title, the current step and an indeterminate bar, with 取消 while
    /// loading still runs. The live scene unloads the library right after the start succeeds, so
    /// the overlay never needs a finish animation.
    /// </summary>
    public sealed class LibraryLoadingOverlay : MonoBehaviour
    {
        private const float COVER_SIZE = 300f;
        private const float BAR_WIDTH = 520f;
        private const float BAR_HEIGHT = 6f;
        private const float SWEEP_WIDTH = 0.28f;
        private const float SWEEP_SECONDS = 1.3f;

        private static readonly Color32 BackgroundColor = new Color32(12, 15, 20, 255);

        private CanvasGroup _group;
        private RectTransform _content;
        private Image _glow;
        private Image _cover;
        private GameObject _coverPlaceholder;
        private RectTransform _barFill;
        private Image _barFillImage;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _messageText;
        private Button _cancelButton;
        private Action _cancelRequested;
        private MotionHandle _showMotion;
        private bool _busy;
        private float _sweep;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;

        public static LibraryLoadingOverlay Create(Transform parent, Action onCancel)
        {
            RectTransform root = LibraryUi.CreateRect("LoadingOverlay", parent);
            LibraryUi.Stretch(root);

            LibraryLoadingOverlay overlay = root.gameObject.AddComponent<LibraryLoadingOverlay>();
            overlay.Build(root, onCancel);
            overlay.Hide();
            return overlay;
        }

        private void Build(RectTransform root, Action onCancel)
        {
            _cancelRequested = onCancel;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            Image background = LibraryUi.CreateImage("Background", root, BackgroundColor);
            LibraryUi.Stretch(background.rectTransform);
            background.raycastTarget = true;

            Vector2 center = new Vector2(0.5f, 0.5f);
            _glow = LibraryUi.CreateImage("Glow", root, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, center, center, new Vector2(0f, 120f), new Vector2(1500f, 1500f));

            _content = LibraryUi.CreateRect("Content", root);
            LibraryWidgets.Place(_content, center, center, new Vector2(0f, 10f), new Vector2(BAR_WIDTH + 400f, 620f));

            // Jacket.
            RectTransform coverFrame = LibraryUi.CreateRect("Cover", _content);
            LibraryWidgets.Place(coverFrame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(COVER_SIZE, COVER_SIZE));
            LibraryWidgets.Shadow(coverFrame, LibraryTheme.Radius.PANEL, 40f, 10f, 120);
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 72f).gameObject;
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);
            LibraryWidgets.Outline(frame, LibraryTheme.Radius.PANEL);

            float top = COVER_SIZE + 40f;
            _titleText = LibraryWidgets.Label(
                "Title", _content, string.Empty, LibraryTheme.FontSize.PAGE_TITLE, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryWidgets.TopStrip(_titleText.rectTransform, 0f, 0f, top, 56f);

            _messageText = LibraryWidgets.Label(
                "Message", _content, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryWidgets.TopStrip(_messageText.rectTransform, 0f, 0f, top + 62f, 30f);

            Image track = LibraryWidgets.Panel("Track", _content, BAR_HEIGHT * 0.5f, LibraryTheme.Colors.ControlActive);
            LibraryWidgets.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(top + 124f)), new Vector2(BAR_WIDTH, BAR_HEIGHT));
            track.gameObject.AddComponent<RectMask2D>();
            _barFillImage = LibraryWidgets.Panel("Fill", track.transform, BAR_HEIGHT * 0.5f, LibraryTheme.Colors.DefaultAccent);
            _barFill = _barFillImage.rectTransform;
            _barFill.anchorMin = new Vector2(0f, 0f);
            _barFill.anchorMax = new Vector2(SWEEP_WIDTH, 1f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;

            const float cancelWidth = 200f;
            const float cancelHeight = 68f;
            _cancelButton = LibraryWidgets.PillButton(
                "Cancel", _content, "取消", OnCancelClicked, cancelWidth, cancelHeight,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Cancel);
            LibraryWidgets.Place((RectTransform)_cancelButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(top + 170f)), new Vector2(cancelWidth, cancelHeight));
            LibraryWidgets.Outline((Image)_cancelButton.targetGraphic, cancelHeight * 0.5f);
        }

        /// <summary>Shows the overlay for one package; safe to call repeatedly.</summary>
        public void Show(string title, string message)
        {
            Show(title, message, null, _accent);
        }

        /// <param name="cover">The package's jacket, when one is already decoded.</param>
        /// <param name="accent">Color of the glow and the bar; follows the jacket.</param>
        internal void Show(string title, string message, Sprite cover, Color32 accent)
        {
            if (_titleText != null)
            {
                _titleText.text = string.IsNullOrEmpty(title) ? "准备中" : title;
            }

            _accent = accent;
            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.SetActive(cover == null);
            Color glow = (Color)accent;
            glow.a = 0.16f;
            _glow.color = glow;
            _barFillImage.color = accent;
            _cancelButton.interactable = true;

            SetMessage(message);
            bool wasVisible = gameObject.activeSelf;
            _busy = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!wasVisible)
            {
                _sweep = 0f;
                PlayEnter();
            }
        }

        /// <summary>Fills in the jacket and its accent once they are decoded, while the overlay is up.</summary>
        internal void SetArtwork(Sprite cover, Color32 accent)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            _accent = accent;
            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.SetActive(cover == null);
            Color glow = (Color)accent;
            glow.a = 0.16f;
            _glow.color = glow;
            _barFillImage.color = accent;
        }

        public void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = string.IsNullOrEmpty(message) ? "准备中…" : message;
            }
        }

        public void Hide()
        {
            _busy = false;
            _showMotion.TryCancel();
            _showMotion = default;
            if (_group != null)
            {
                _group.alpha = 1f;
            }
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _showMotion.TryCancel();
        }

        private void PlayEnter()
        {
            _showMotion.TryCancel();
            _group.alpha = 0f;
            _content.anchoredPosition = new Vector2(0f, -10f);
            _showMotion = LSequence.Create()
                .Append(LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .BindToAlpha(_group))
                .Join(LMotion.Create(new Vector2(0f, -10f), new Vector2(0f, 10f), LibraryTheme.Motion.SLOW)
                    .WithEase(LibraryTheme.Motion.ENTER_EASE)
                    .BindToAnchoredPosition(_content))
                .Run();
        }

        private void OnCancelClicked()
        {
            if (_busy)
            {
                SetMessage("正在取消…");
                _cancelButton.interactable = false;
            }
            _cancelRequested?.Invoke();
        }

        private void Update()
        {
            // Indeterminate sweep: the loading steps are not measurable, so the bar must not
            // pretend to know a percentage. The segment runs off one end and back in at the other.
            if (!_busy || _barFill == null)
            {
                return;
            }

            _sweep = (_sweep + Time.unscaledDeltaTime / SWEEP_SECONDS) % 1f;
            float eased = Mathf.SmoothStep(0f, 1f, _sweep);
            float start = Mathf.Lerp(-SWEEP_WIDTH, 1f, eased);
            _barFill.anchorMin = new Vector2(start, 0f);
            _barFill.anchorMax = new Vector2(start + SWEEP_WIDTH, 1f);
        }
    }
}
