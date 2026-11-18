using System;
using LitMotion;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Pieces the server pages share, all in the library's look: the top bar with its back
    /// button, round icon buttons, chips, scroll areas and the status line that says a page is
    /// loading, empty or failed.
    /// </summary>
    internal static class ServerWidgets
    {
        internal const float CHIP_HEIGHT = 40f;

        /// <summary>
        /// The page's top bar: back button, title and a muted subtitle on the left. Returns the
        /// bar; the caller lays its own controls out from the right edge.
        /// </summary>
        internal static RectTransform TopBar(
            RectTransform root, string title, Action back, out TextMeshProUGUI titleText, out TextMeshProUGUI subtitleText)
        {
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;
            RectTransform bar = LibraryUi.CreateRect("TopBar", root);
            LibraryWidgets.TopStrip(bar, margin, margin, 0f, LibraryTheme.Layout.TOP_BAR_HEIGHT);
            Vector2 leftCenter = new Vector2(0f, 0.5f);

            Button backButton = IconButton(bar, "Back", LibraryIcon.Back, () => back?.Invoke(), UiCue.Back);
            LibraryWidgets.Place((RectTransform)backButton.transform, leftCenter, leftCenter, Vector2.zero, new Vector2(height, height));

            titleText = LibraryWidgets.Label("Title", bar, title, LibraryTheme.FontSize.PAGE_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            titleText.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleText.rectTransform.offsetMin = new Vector2(height + 20f, -6f);
            titleText.rectTransform.offsetMax = new Vector2(0f, 46f);
            subtitleText = LibraryWidgets.Label("Subtitle", bar, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subtitleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleText.rectTransform.pivot = new Vector2(0f, 0.5f);
            subtitleText.rectTransform.offsetMin = new Vector2(height + 22f, -36f);
            subtitleText.rectTransform.offsetMax = new Vector2(0f, -8f);
            return bar;
        }

        /// <summary>Round button with one icon, the top bar's size.</summary>
        internal static Button IconButton(
            Transform parent, string name, LibraryIcon icon, UnityAction onClick, UiCue sound = UiCue.Press, float size = LibraryTheme.Layout.CONTROL_HEIGHT)
        {
            Button button = LibraryWidgets.PillButton(
                name, parent, string.Empty, onClick, size, size, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: sound);
            LibraryWidgets.Outline((Image)button.targetGraphic, size * 0.5f);
            LibraryWidgets.Icon(button.transform, icon, LibraryTheme.Colors.TextSecondary, size * 0.39f);
            return button;
        }

        /// <summary>Pill button with an icon in front of its label.</summary>
        internal static Button LabelledButton(
            Transform parent, string name, string label, LibraryIcon icon, UnityAction onClick, float width, float height,
            Color32 background, Color32 foreground, UiCue sound = UiCue.Press, float fontSize = LibraryTheme.FontSize.BODY)
        {
            Button button = LibraryWidgets.PillButton(name, parent, label, onClick, width, height, background, foreground,
                fontSize, FontStyles.Bold, sound);
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            float textWidth = Mathf.Ceil(text.GetPreferredValues(label).x);
            float iconSize = height * 0.36f;
            float total = iconSize + 10f + textWidth;
            Image glyph = LibraryWidgets.Icon(button.transform, icon, foreground, iconSize);
            glyph.name = "Glyph";
            glyph.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f, 0f);
            text.alignment = TextAlignmentOptions.Center;
            LibraryWidgets.Inset(text.rectTransform, iconSize + 10f, 0f, 0f, 0f);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(textWidth + 4f, 0f);
            text.rectTransform.anchoredPosition = new Vector2(total * 0.5f - textWidth * 0.5f, 0f);
            return button;
        }

        /// <summary>Recolors and relabels a <see cref="LabelledButton"/>.</summary>
        internal static void SetLabelledButton(Button button, string label, LibraryIcon? icon, Color32 background, Color32 foreground)
        {
            ((Image)button.targetGraphic).color = background;
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            text.color = foreground;
            Transform glyphTransform = button.transform.Find("Glyph");
            Image glyph = glyphTransform != null ? glyphTransform.GetComponent<Image>() : null;
            float height = ((RectTransform)button.transform).rect.height;
            if (height <= 0f)
            {
                height = ((RectTransform)button.transform).sizeDelta.y;
            }

            float textWidth = Mathf.Ceil(text.GetPreferredValues(label).x);
            float iconSize = height * 0.36f;
            bool hasIcon = glyph != null && icon.HasValue;
            if (glyph != null)
            {
                glyph.enabled = hasIcon;
                if (hasIcon)
                {
                    glyph.sprite = LibraryShapes.Icon(icon.Value);
                    glyph.color = foreground;
                }
            }

            float total = hasIcon ? iconSize + 10f + textWidth : textWidth;
            if (glyph != null)
            {
                glyph.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f, 0f);
            }

            text.rectTransform.sizeDelta = new Vector2(textWidth + 4f, 0f);
            text.rectTransform.anchoredPosition = new Vector2(total * 0.5f - textWidth * 0.5f, 0f);
        }

        /// <summary>
        /// A small rounded chip with an optional icon; sized to its text. Returns its rect so the
        /// caller can place it, and its width in <paramref name="width"/>.
        /// </summary>
        internal static RectTransform Chip(
            Transform parent, string text, LibraryIcon? icon, Color32 background, Color32 foreground, out float width,
            float height = CHIP_HEIGHT, float fontSize = LibraryTheme.FontSize.CAPTION, bool condensed = false)
        {
            Image chip = LibraryWidgets.Panel("Chip", parent, height * 0.5f, background);
            TextMeshProUGUI label = LibraryWidgets.Label("Label", chip.transform, text, fontSize, foreground, FontStyles.Bold);
            if (condensed)
            {
                LibraryUi.UseCondensedFont(label);
                label.characterSpacing = 4f;
            }

            label.overflowMode = TextOverflowModes.Overflow;
            float textWidth = Mathf.Ceil(label.GetPreferredValues(text).x);
            float padding = height * 0.42f;
            float left = padding;
            if (icon.HasValue)
            {
                float iconSize = height * 0.5f;
                Image glyph = LibraryWidgets.Icon(chip.transform, icon.Value, foreground, iconSize);
                LibraryWidgets.Place(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(padding - 2f, 0f), new Vector2(iconSize, iconSize));
                left = padding + iconSize + 6f;
            }

            width = left + textWidth + padding;
            LibraryWidgets.Inset(label.rectTransform, left, 0f, padding - 2f, 0f);
            chip.rectTransform.sizeDelta = new Vector2(width, height);
            return chip.rectTransform;
        }

        /// <summary>The library icon for a Sonolus icon name, or null when there is none close enough.</summary>
        internal static LibraryIcon? IconFor(string sonolusIcon)
        {
            switch (sonolusIcon)
            {
                case "heart":
                case "heartHollow":
                case "like":
                    return LibraryIcon.Heart;
                case "comment":
                case "comments":
                case "message":
                    return LibraryIcon.Comment;
                case "clock":
                case "time":
                case "calendar":
                    return LibraryIcon.Clock;
                case "trophy":
                case "award":
                case "star":
                case "starHollow":
                case "crown":
                    return LibraryIcon.Trophy;
                case "tag":
                case "tags":
                    return LibraryIcon.Tag;
                case "globe":
                case "public":
                    return LibraryIcon.Globe;
                case "user":
                case "users":
                case "person":
                    return LibraryIcon.User;
                case "level":
                case "music":
                    return LibraryIcon.Wave;
                case "search":
                    return LibraryIcon.Search;
                case "download":
                    return LibraryIcon.Download;
                case "play":
                    return LibraryIcon.Play;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A vertical scroll area over <paramref name="parent"/> whose content stacks its children
        /// top to bottom and grows to fit them.
        /// </summary>
        internal static RectTransform VerticalList(RectTransform parent, out ScrollRect scroll, float spacing, RectOffset padding = null, bool stack = true)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            LibraryUi.Stretch((RectTransform)go.transform);
            Image drag = go.AddComponent<Image>();
            drag.color = new Color32(0, 0, 0, 0);

            RectTransform viewport = LibraryUi.CreateRect("Viewport", go.transform);
            LibraryUi.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = LibraryUi.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            // A new rect is 100 units wide by default; stretched across the viewport that would make
            // the content 100 wider than it and, centered, push everything 50 to the left.
            content.sizeDelta = Vector2.zero;
            if (stack)
            {
                AddStack(content, spacing, padding);
            }

            scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        private static void AddStack(RectTransform content, float spacing, RectOffset padding)
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>A fixed-height child of a <see cref="VerticalList"/>.</summary>
        internal static RectTransform ListItem(Transform content, string name, float height)
        {
            RectTransform item = LibraryUi.CreateRect(name, content);
            LayoutElement element = item.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return item;
        }

        /// <summary>Wrapping text that sizes its list item to fit.</summary>
        internal static TextMeshProUGUI Paragraph(Transform content, string name, string text, float fontSize, Color32 color)
        {
            TextMeshProUGUI paragraph = LibraryUi.CreateText(name, content, text, fontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            paragraph.color = color;
            paragraph.textWrappingMode = TextWrappingModes.Normal;
            paragraph.overflowMode = TextOverflowModes.Overflow;
            paragraph.richText = false;
            paragraph.lineSpacing = 12f;
            return paragraph;
        }

        /// <summary>"3 分钟前" for a recent time, the date for anything older than a week.</summary>
        internal static string DescribeTime(DateTimeOffset? time) => time.HasValue ? HomeScreen.DescribeWhen(time.Value) : string.Empty;

        internal static string Localize(string text) => SonolusText.Localize(text);
    }

    /// <summary>
    /// A line in the middle of an area that says what is going on there: loading (breathing),
    /// empty, or failed with a retry button.
    /// </summary>
    internal sealed class ServerStatusView : IDisposable
    {
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _text;
        private readonly Button _retry;
        private readonly CanvasGroup _group;
        private MotionHandle _breathe;
        private Action _onRetry;

        internal ServerStatusView(RectTransform parent)
        {
            _root = LibraryUi.CreateRect("Status", parent);
            LibraryUi.Stretch(_root);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _text = LibraryWidgets.Label("Text", _root, string.Empty, LibraryTheme.FontSize.ROW_TITLE, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            _text.textWrappingMode = TextWrappingModes.Normal;
            LibraryWidgets.Place(_text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1100f, 120f));
            _retry = LibraryWidgets.PillButton("Retry", _root, "重试", () => _onRetry?.Invoke(), 180f, 64f,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Retry);
            LibraryWidgets.Outline((Image)_retry.targetGraphic, 32f);
            LibraryWidgets.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(180f, 64f));
            _root.gameObject.SetActive(false);
        }

        internal bool IsVisible => _root.gameObject.activeSelf;

        internal void ShowLoading(string message = "正在加载…")
        {
            Show(message, null);
            _breathe = LMotion.Create(0f, Mathf.PI * 2f, 1.6f)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(phase => _group.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(phase)));
        }

        internal void ShowMessage(string message) => Show(message, null);

        internal void ShowError(string message, Action retry) => Show(message, retry);

        internal void Hide()
        {
            _breathe.TryCancel();
            _breathe = default;
            _root.gameObject.SetActive(false);
        }

        public void Dispose() => _breathe.TryCancel();

        private void Show(string message, Action retry)
        {
            _breathe.TryCancel();
            _breathe = default;
            _group.alpha = 1f;
            _onRetry = retry;
            _text.text = message ?? string.Empty;
            _retry.gameObject.SetActive(retry != null);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }
    }
}
