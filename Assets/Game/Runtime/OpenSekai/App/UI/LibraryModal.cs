using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A card over a dimmed page, in the library's look: rounded surface with a soft shadow and a
    /// hairline, a title area with an optional subtitle and a round close button, and a body the
    /// owner fills. Opening fades the scrim and springs the card up; closing is immediate, so a
    /// panel that hides and another that shows in the same frame never overlap.
    /// </summary>
    internal sealed class LibraryModal : IDisposable
    {
        private readonly CanvasGroup _group;
        private readonly RectTransform _card;
        private readonly FitCard _fit;
        private MotionHandle _openMotion;

        internal RectTransform Root { get; }
        internal RectTransform Card => _card;

        /// <summary>Area under the title, inset by the card's padding; the owner lays its content out here.</summary>
        internal RectTransform Body { get; }

        internal TextMeshProUGUI Title { get; }
        internal TextMeshProUGUI Subtitle { get; }
        internal Button CloseButton { get; }

        /// <param name="root">Full-screen rect the modal owns (scrim and card are built under it).</param>
        /// <param name="size">Card size; the height shrinks to fit a short screen.</param>
        /// <param name="onClose">Close button and scrim tap; null leaves both inert and hides the button.</param>
        internal LibraryModal(RectTransform root, Vector2 size, string title, Action onClose, bool withSubtitle = false)
        {
            Root = root;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            Image scrim = LibraryUi.CreateImage("Scrim", root, LibraryTheme.Colors.Scrim);
            LibraryUi.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            if (onClose != null)
            {
                Button scrimButton = scrim.gameObject.AddComponent<Button>();
                scrimButton.transition = Selectable.Transition.None;
                scrimButton.targetGraphic = scrim;
                scrimButton.onClick.AddListener(() => onClose());
                UiSound.Bind(scrimButton, UiCue.Close);
            }

            // The frame leaves a margin between the card and the edge of the page.
            RectTransform frame = LibraryUi.CreateRect("Frame", root);
            LibraryWidgets.Inset(frame, 48f, 40f, 48f, 40f);
            _card = LibraryUi.CreateRect("Card", frame);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_card, center, center, Vector2.zero, size);
            _fit = frame.gameObject.AddComponent<FitCard>();
            _fit.Card = _card;
            _fit.Preferred = size;

            LibraryWidgets.Shadow(_card, LibraryTheme.Radius.PANEL, 56f, 18f, 170);
            Image surface = LibraryWidgets.Panel("Surface", _card, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.Surface);
            surface.raycastTarget = true;
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, LibraryTheme.Radius.PANEL);

            float padding = LibraryTheme.Layout.MODAL_PADDING;
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            float titleRight = onClose != null ? padding + 80f : padding;
            Title = LibraryWidgets.Label(
                "Title", _card, title, 34f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            if (withSubtitle)
            {
                LibraryWidgets.TopStrip(Title.rectTransform, padding, titleRight, 26f, 46f);
                Subtitle = LibraryWidgets.Label(
                    "Subtitle", _card, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(Subtitle.rectTransform, padding, titleRight, 72f, 28f);
            }
            else
            {
                LibraryWidgets.TopStrip(Title.rectTransform, padding, titleRight, 0f, header);
            }

            if (onClose != null)
            {
                const float CLOSE_SIZE = 64f;
                CloseButton = LibraryWidgets.PillButton(
                    "Close", _card, string.Empty, () => onClose(), CLOSE_SIZE, CLOSE_SIZE,
                    LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: UiCue.Close);
                LibraryWidgets.Icon(CloseButton.transform, LibraryIcon.Close, LibraryTheme.Colors.TextSecondary, 26f);
                LibraryWidgets.Place(
                    (RectTransform)CloseButton.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                    new Vector2(-28f, -header * 0.5f), new Vector2(CLOSE_SIZE, CLOSE_SIZE));
            }

            Body = LibraryUi.CreateRect("Body", _card);
            LibraryWidgets.Inset(Body, padding, padding, padding, header);
        }

        internal bool IsOpen => Root.gameObject.activeSelf;

        /// <summary>Fits the card to its content. The callback receives the available body width and height.</summary>
        internal void FitToBody(Func<Vector2, float> measureHeight)
        {
            _fit.MeasureBodyHeight = measureHeight;
            _fit.Fit();
        }

        /// <param name="sound">Played as the card springs in.</param>
        internal void Open(UiCue sound = UiCue.Open)
        {
            UiSound.Play(sound);
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();

            _openMotion.TryCancel();
            _group.alpha = 0f;
            _card.localScale = new Vector3(0.96f, 0.96f, 1f);
            _openMotion = LSequence.Create()
                .Append(LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .BindToAlpha(_group))
                .Join(LMotion.Create(_card.localScale, Vector3.one, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.SELECT_EASE)
                    .BindToLocalScale(_card))
                .Run();
        }

        internal void Close()
        {
            _openMotion.TryCancel();
            _openMotion = default;
            _group.alpha = 1f;
            _card.localScale = Vector3.one;
            Root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            _openMotion.TryCancel();
        }

        /// <summary>Sizes the centered card within the frame.</summary>
        private sealed class FitCard : MonoBehaviour
        {
            internal RectTransform Card;
            internal Vector2 Preferred;
            internal Func<Vector2, float> MeasureBodyHeight;

            private void OnEnable() => Fit();

            private void OnRectTransformDimensionsChange() => Fit();

            internal void Fit()
            {
                if (Card == null)
                {
                    return;
                }

                Rect bounds = ((RectTransform)transform).rect;
                var size = new Vector2(Mathf.Min(Preferred.x, bounds.width), Mathf.Min(Preferred.y, bounds.height));
                if (MeasureBodyHeight != null)
                {
                    const float PADDING = LibraryTheme.Layout.MODAL_PADDING;
                    const float INSETS = LibraryTheme.Layout.MODAL_HEADER_HEIGHT + PADDING;
                    var availableBody = new Vector2(Mathf.Max(0f, size.x - 2f * PADDING),
                        Mathf.Max(0f, bounds.height - INSETS));
                    size.y = Mathf.Min(bounds.height, MeasureBodyHeight(availableBody) + INSETS);
                }

                if (Card.sizeDelta != size)
                {
                    Card.sizeDelta = size;
                }
            }
        }
    }
}
