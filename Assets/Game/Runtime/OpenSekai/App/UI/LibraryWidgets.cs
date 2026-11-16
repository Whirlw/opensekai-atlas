using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>Small building blocks shared by the library page's parts.</summary>
    internal static class LibraryWidgets
    {
        /// <summary>9-sliced image tinted with <paramref name="color"/>.</summary>
        internal static Image Shape(string name, Transform parent, Sprite sprite, Color32 color)
        {
            Image image = LibraryUi.CreateImage(name, parent, color);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Filled rounded rectangle.</summary>
        internal static Image Panel(string name, Transform parent, float radius, Color32 color)
        {
            return Shape(name, parent, LibraryShapes.RoundedRect(radius), color);
        }

        internal static Image Icon(Transform parent, LibraryIcon icon, Color32 color, float size)
        {
            Image image = LibraryUi.CreateImage(icon + "Icon", parent, color);
            image.sprite = LibraryShapes.Icon(icon);
            image.raycastTarget = false;
            Vector2 center = new Vector2(0.5f, 0.5f);
            Place(image.rectTransform, center, center, Vector2.zero, new Vector2(size, size));
            return image;
        }

        internal static void Outline(Image panel, float radius)
        {
            Image outline = Shape("Outline", panel.transform, LibraryShapes.Ring(radius, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
        }

        internal static Image Shadow(RectTransform parent, float radius, float blur, float drop, byte opacity)
        {
            Image shadow = Shape("Shadow", parent, LibraryShapes.Shadow(radius, blur), new Color32(0, 0, 0, opacity));
            Inset(shadow.rectTransform, -blur, -blur - drop, -blur, -blur + drop);
            shadow.transform.SetAsFirstSibling();
            return shadow;
        }

        /// <summary>Single-line label that ends in an ellipsis when it does not fit.</summary>
        internal static TextMeshProUGUI Label(
            string name, Transform parent, string text, float fontSize, Color32 color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI label = LibraryUi.CreateText(name, parent, text, fontSize, style, alignment);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>Pill-shaped button with a centered label and press feedback.</summary>
        internal static Button PillButton(
            string name, Transform parent, string label, UnityAction onClick, float width, float height,
            Color32 background, Color32 foreground, float fontSize = LibraryTheme.FontSize.BODY,
            FontStyles style = FontStyles.Normal)
        {
            Image image = Panel(name, parent, height * 0.5f, background);
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = new Vector2(width, height);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            TextMeshProUGUI text = Label("Label", image.transform, label, fontSize, foreground, style, TextAlignmentOptions.Center);
            LibraryUi.Stretch(text.rectTransform);
            LibraryPressFeedback.Attach(image.gameObject);
            return button;
        }

        /// <summary>Anchors a rect to one point of its parent.</summary>
        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Stretches a rect over its parent, inset by the given distances from each edge.</summary>
        internal static void Inset(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Stretches a rect across its parent's width at a fixed height, measured from the top.</summary>
        internal static void TopStrip(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Stretches a rect across its parent's width at a fixed height, measured from the bottom.</summary>
        internal static void BottomStrip(RectTransform rect, float left, float right, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }
    }

    /// <summary>
    /// Shrinks a control slightly while it is pressed and springs it back on release.
    /// </summary>
    internal sealed class LibraryPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Transform _target;
        private Selectable _selectable;
        private MotionHandle _pressMotion;

        /// <param name="host">Object that receives the pointer events.</param>
        /// <param name="target">Transform to scale; defaults to the host itself.</param>
        internal static LibraryPressFeedback Attach(GameObject host, Transform target = null)
        {
            LibraryPressFeedback feedback = host.AddComponent<LibraryPressFeedback>();
            feedback._target = target != null ? target : host.transform;
            feedback._selectable = host.GetComponent<Selectable>();
            return feedback;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            Play(LibraryTheme.Motion.PRESS_SCALE, LibraryTheme.Motion.FAST, LibraryTheme.Motion.ENTER_EASE);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Play(1f, LibraryTheme.Motion.STANDARD, LibraryTheme.Motion.SELECT_EASE);
        }

        private void OnDisable()
        {
            _pressMotion.TryCancel();
            _pressMotion = default;
            if (_target != null)
            {
                _target.localScale = Vector3.one;
            }
        }

        private void Play(float scale, float duration, Ease ease)
        {
            _pressMotion.TryCancel();
            _pressMotion = LMotion.Create(_target.localScale, Vector3.one * scale, duration)
                .WithEase(ease)
                .BindToLocalScale(_target);
        }
    }
}
