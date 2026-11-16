using System;
using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Pill button that shows a caption and the current choice, and opens a list of options under
    /// itself. The list lives on a page-wide overlay so it draws above the content, with a
    /// backdrop that closes it on a tap anywhere else.
    /// </summary>
    internal sealed class LibraryDropdown : MonoBehaviour
    {
        private const float POPUP_WIDTH = 260f;
        private const float POPUP_GAP = 10f;
        private const float POPUP_PADDING = 8f;
        private const float OPTION_HEIGHT = 52f;
        private const float OPTION_SPACING = 2f;
        private const float MARK_SIZE = 22f;

        private readonly List<Option> _options = new List<Option>();
        private RectTransform _overlay;
        private Image _background;
        private TextMeshProUGUI _value;
        private RectTransform _chevron;
        private GameObject _popup;
        private RectTransform _panel;
        private CanvasGroup _panelGroup;
        private MotionHandle _openMotion;
        private IReadOnlyList<string> _labels = Array.Empty<string>();
        private int _selected;
        private Action<int> _onPick;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;

        internal bool IsOpen => _popup != null && _popup.activeSelf;

        internal static void AttachActions(Button button, RectTransform overlay,
            IReadOnlyList<string> labels, Action<int> onPick)
        {
            LibraryDropdown dropdown = button.gameObject.AddComponent<LibraryDropdown>();
            dropdown._overlay = overlay;
            dropdown._background = (Image)button.targetGraphic;
            dropdown._labels = labels;
            dropdown._selected = -1;
            dropdown._onPick = onPick;
            button.onClick.AddListener(dropdown.Toggle);
        }

        /// <param name="parent">Where the button goes.</param>
        /// <param name="overlay">Page-wide rect, above the content, that hosts the option list.</param>
        /// <param name="caption">Muted word in front of the current choice, e.g. 排序.</param>
        /// <param name="width">Width of the button.</param>
        /// <param name="height">Height of the button.</param>
        internal static LibraryDropdown Create(Transform parent, RectTransform overlay, string caption, float width, float height)
        {
            Image background = LibraryWidgets.Panel("Dropdown-" + caption, parent, height * 0.5f, LibraryTheme.Colors.Control);
            background.raycastTarget = true;
            background.rectTransform.sizeDelta = new Vector2(width, height);
            LibraryWidgets.Outline(background, height * 0.5f);

            LibraryDropdown dropdown = background.gameObject.AddComponent<LibraryDropdown>();
            dropdown._overlay = overlay;
            dropdown._background = background;

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(dropdown.Toggle);
            LibraryPressFeedback.Attach(background.gameObject);

            const float padding = 22f;
            TextMeshProUGUI captionText = LibraryWidgets.Label(
                "Caption", background.transform, caption, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            float captionWidth = captionText.GetPreferredValues(caption).x;
            LibraryWidgets.Place(
                captionText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(padding, 0f), new Vector2(captionWidth + 2f, height));

            dropdown._value = LibraryWidgets.Label(
                "Value", background.transform, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold);
            LibraryWidgets.Inset(dropdown._value.rectTransform, padding + captionWidth + 10f, 0f, 46f, 0f);

            Image chevron = LibraryUi.CreateImage("Chevron", background.transform, LibraryTheme.Colors.TextSecondary);
            chevron.sprite = LibraryShapes.Chevron();
            chevron.raycastTarget = false;
            dropdown._chevron = chevron.rectTransform;
            LibraryWidgets.Place(
                dropdown._chevron, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28f, 0f), new Vector2(20f, 20f));
            return dropdown;
        }

        /// <summary>Sets the choices, the one shown on the button, and what a pick does.</summary>
        internal void SetOptions(IReadOnlyList<string> labels, int selected, Action<int> onPick)
        {
            _labels = labels ?? Array.Empty<string>();
            _selected = selected;
            _onPick = onPick;
            _value.text = selected >= 0 && selected < _labels.Count ? _labels[selected] : string.Empty;
            if (IsOpen)
            {
                RefreshOptions();
            }
        }

        /// <summary>Color of the current choice in the open list.</summary>
        internal void SetAccent(Color32 accent)
        {
            _accent = accent;
            if (IsOpen)
            {
                RefreshOptions();
            }
        }

        internal void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            KillTween();
            _popup.SetActive(false);
            _background.color = LibraryTheme.Colors.Control;
            if (_chevron != null)
            {
                _chevron.localEulerAngles = Vector3.zero;
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            KillTween();
            if (_popup != null)
            {
                Destroy(_popup);
            }
        }

        private void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            if (_popup == null)
            {
                BuildPopup();
            }

            RefreshOptions();

            // The list hangs from the button's bottom-left corner, in the overlay's space.
            var buttonRect = (RectTransform)transform;
            Vector3 corner = buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMin, buttonRect.rect.yMin, 0f));
            Vector2 local = _overlay.InverseTransformPoint(corner);
            // Hangs upward from the top corner instead when the list would run off the bottom.
            Rect bounds = _overlay.rect;
            float listBottom = local.y - POPUP_GAP - _panel.sizeDelta.y;
            float below = bounds.center.y - bounds.height * 0.5f + POPUP_GAP;
            if (listBottom < below)
            {
                Vector3 top = buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMin, buttonRect.rect.yMax, 0f));
                Vector2 localTop = _overlay.InverseTransformPoint(top);
                _panel.pivot = new Vector2(0f, 0f);
                _panel.anchoredPosition = new Vector2(localTop.x, localTop.y + POPUP_GAP);
            }
            else
            {
                _panel.pivot = new Vector2(0f, 1f);
                _panel.anchoredPosition = new Vector2(local.x, local.y - POPUP_GAP);
            }

            _popup.transform.SetAsLastSibling();
            _popup.SetActive(true);
            _background.color = LibraryTheme.Colors.ControlActive;
            if (_chevron != null)
            {
                _chevron.localEulerAngles = new Vector3(0f, 0f, 180f);
            }

            KillTween();
            _panelGroup.alpha = 0f;
            _panel.localScale = new Vector3(1f, 0.92f, 1f);
            _openMotion = LSequence.Create()
                .Append(LMotion.Create(_panelGroup.alpha, 1f, LibraryTheme.Motion.FAST)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .BindToAlpha(_panelGroup))
                .Join(LMotion.Create(_panel.localScale.y, 1f, LibraryTheme.Motion.FAST)
                    .WithEase(LibraryTheme.Motion.ENTER_EASE)
                    .BindToLocalScaleY(_panel))
                .Run();
        }

        private void BuildPopup()
        {
            RectTransform root = LibraryUi.CreateRect("DropdownPopup", _overlay);
            LibraryUi.Stretch(root);
            _popup = root.gameObject;

            Image backdrop = LibraryUi.CreateImage("Backdrop", root, new Color32(8, 10, 14, 90));
            LibraryUi.Stretch(backdrop.rectTransform);
            Button backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Close);

            Image panel = LibraryWidgets.Panel("Panel", root, 20f, LibraryTheme.Colors.SurfaceRaised);
            panel.raycastTarget = true;
            _panel = panel.rectTransform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0f, 1f);
            _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            LibraryWidgets.Shadow(_panel, 20f, 40f, 16f, 150);
            Image outline = LibraryWidgets.Shape("Outline", _panel, LibraryShapes.Ring(20f, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
        }

        private void RefreshOptions()
        {
            while (_options.Count < _labels.Count)
            {
                _options.Add(CreateOption(_options.Count));
            }

            for (int i = 0; i < _options.Count; i++)
            {
                Option option = _options[i];
                bool used = i < _labels.Count;
                option.Root.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                bool current = i == _selected;
                option.Label.text = _labels[i];
                option.Label.color = current ? _accent : LibraryTheme.Colors.TextPrimary;
                LibraryUi.ApplyCjkTextStyle(option.Label, current ? FontStyles.Bold : FontStyles.Normal);
                option.Background.color = current ? LibraryTheme.Colors.Control : new Color32(255, 255, 255, 0);
                option.Mark.color = _accent;
                option.Mark.enabled = current;
            }

            float height = POPUP_PADDING * 2f + _labels.Count * OPTION_HEIGHT + Mathf.Max(0, _labels.Count - 1) * OPTION_SPACING;
            _panel.sizeDelta = new Vector2(POPUP_WIDTH, height);
        }

        private Option CreateOption(int index)
        {
            Image background = LibraryWidgets.Panel("Option" + index, _panel, 12f, new Color32(255, 255, 255, 0));
            background.raycastTarget = true;
            LibraryWidgets.TopStrip(
                background.rectTransform, POPUP_PADDING, POPUP_PADDING,
                POPUP_PADDING + index * (OPTION_HEIGHT + OPTION_SPACING), OPTION_HEIGHT);

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Pick(index));

            TextMeshProUGUI label = LibraryWidgets.Label(
                "Label", background.transform, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.Inset(label.rectTransform, 16f, 0f, 40f, 0f);

            Image mark = LibraryWidgets.Icon(background.transform, LibraryIcon.Check, _accent, MARK_SIZE);
            LibraryWidgets.Place(
                mark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(MARK_SIZE, MARK_SIZE));

            return new Option { Root = background.rectTransform, Background = background, Label = label, Mark = mark };
        }

        private void Pick(int index)
        {
            Action<int> onPick = _onPick;
            Close();
            onPick?.Invoke(index);
        }

        private void KillTween()
        {
            _openMotion.TryCancel();
            _openMotion = default;
        }

        private sealed class Option
        {
            internal RectTransform Root;
            internal Image Background;
            internal TextMeshProUGUI Label;
            internal Image Mark;
        }
    }
}
