using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The pill-shaped note that rises from the bottom of a page for a moment and fades away
    /// (download progress, "再按一次返回键退出", errors). Showing a new message replaces the old one.
    /// </summary>
    internal sealed class UiToast
    {
        private const float HEIGHT = 64f;

        private readonly RectTransform _toast;
        private readonly CanvasGroup _group;
        private readonly TextMeshProUGUI _text;
        private readonly Image _icon;
        private readonly float _bottom;
        private MotionHandle _motion;

        /// <param name="root">Full-screen rect the toast sits in.</param>
        /// <param name="bottom">Distance from the bottom edge to the toast at rest.</param>
        internal UiToast(RectTransform root, float bottom, Color32 iconColor)
        {
            _bottom = bottom;
            _toast = LibraryUi.CreateRect("Toast", root);
            LibraryWidgets.Place(_toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, bottom), new Vector2(480f, HEIGHT));
            _group = _toast.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            LibraryWidgets.Shadow(_toast, HEIGHT * 0.5f, 30f, 10f, 140);
            Image surface = LibraryWidgets.Panel("Surface", _toast, HEIGHT * 0.5f, LibraryTheme.Colors.SurfaceRaised);
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, HEIGHT * 0.5f);
            _icon = LibraryWidgets.Icon(_toast, LibraryIcon.Info, iconColor, 26f);
            LibraryWidgets.Place(_icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(26f, 26f));
            _text = LibraryWidgets.Label(
                "Text", _toast, string.Empty, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Left);
            LibraryWidgets.Inset(_text.rectTransform, 70f, 0f, 30f, 0f);
            _toast.gameObject.SetActive(false);
        }

        internal void Show(string message, float hold = HomeTheme.Motion.TOAST)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _text.text = message;
            RectTransform parent = (RectTransform)_toast.parent;
            float maxWidth = parent.rect.width > 0f ? parent.rect.width - 96f : 1600f;
            float width = Mathf.Min(maxWidth, Mathf.Ceil(_text.GetPreferredValues(message).x) + 112f);
            _toast.sizeDelta = new Vector2(width, HEIGHT);
            _motion.TryCancel();
            _motion = LMotion.Create(0f, hold + 0.6f, hold + 0.6f)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _toast.gameObject.SetActive(false))
                .Bind(time =>
                {
                    float shown = HomeMotion.Span(time, 0f, 0.28f, Ease.OutCubic) * (1f - HomeMotion.Span(time, hold + 0.3f, 0.3f, Ease.InQuad));
                    _group.alpha = shown;
                    _toast.anchoredPosition = new Vector2(0f, _bottom + 4f + 14f * shown);
                });
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
        }

        internal void Hide()
        {
            _motion.TryCancel();
            _motion = default;
            _toast.gameObject.SetActive(false);
        }

        internal void Dispose() => _motion.TryCancel();
    }
}
