using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Modal "preparing" page shown while a package is being read and registered. The live scene
    /// unloads the library right after the start succeeds, so this overlay never needs a finish
    /// animation: it reports progress text and offers cancel while loading is still running.
    /// </summary>
    public sealed class LibraryLoadingOverlay : MonoBehaviour
    {
        private static readonly Color32 ScrimColor = new Color32(8, 11, 15, 232);
        private static readonly Color32 CardColor = new Color32(28, 34, 42, 255);
        private static readonly Color32 BarTrackColor = new Color32(46, 55, 66, 255);
        private static readonly Color32 AccentColor = new Color32(72, 148, 214, 255);

        private RectTransform _barFill;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _messageText;
        private Action _cancelRequested;
        private bool _busy;
        private float _sweep;

        public static LibraryLoadingOverlay Create(Transform parent, Action onCancel)
        {
            RectTransform root = LibraryScreen.CreateRect("LoadingOverlay", parent);
            LibraryScreen.Stretch(root);

            LibraryLoadingOverlay overlay = root.gameObject.AddComponent<LibraryLoadingOverlay>();
            overlay.Build(root, onCancel);
            overlay.Hide();
            return overlay;
        }

        private void Build(RectTransform root, Action onCancel)
        {
            _cancelRequested = onCancel;

            Image scrim = LibraryScreen.CreateImage("Scrim", root, ScrimColor);
            LibraryScreen.Stretch(scrim.rectTransform);

            RectTransform card = LibraryScreen.CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(860f, 300f);
            Image cardBackground = card.gameObject.AddComponent<Image>();
            cardBackground.color = CardColor;

            _titleText = LibraryScreen.CreateText(
                "Title", card, string.Empty, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(32f, -84f);
            _titleText.rectTransform.offsetMax = new Vector2(-32f, -24f);
            _titleText.textWrappingMode = TextWrappingModes.NoWrap;
            _titleText.overflowMode = TextOverflowModes.Ellipsis;

            _messageText = LibraryScreen.CreateText(
                "Message", card, string.Empty, 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            _messageText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _messageText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _messageText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _messageText.rectTransform.offsetMin = new Vector2(32f, -130f);
            _messageText.rectTransform.offsetMax = new Vector2(-32f, -92f);
            _messageText.color = new Color32(186, 198, 212, 255);

            RectTransform track = LibraryScreen.CreateRect("Track", card);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(64f, 104f);
            track.offsetMax = new Vector2(-64f, 116f);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = BarTrackColor;

            RectTransform fill = LibraryScreen.CreateRect("Fill", track);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0.2f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = AccentColor;
            _barFill = fill;

            Button cancel = LibraryScreen.CreateButton("Cancel", card, "取消", OnCancelClicked, 200f, 56f);
            RectTransform cancelRect = (RectTransform)cancel.transform;
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(0f, 28f);
        }

        /// <summary>Shows the overlay for one package; safe to call repeatedly.</summary>
        public void Show(string title, string message)
        {
            if (_titleText != null)
            {
                _titleText.text = string.IsNullOrEmpty(title) ? "准备中" : title;
            }

            SetMessage(message);
            _busy = true;
            _sweep = 0f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
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
            gameObject.SetActive(false);
        }

        private void OnCancelClicked()
        {
            Action callback = _cancelRequested;
            _cancelRequested?.Invoke();
        }

        private void Update()
        {
            // Indeterminate sweep: the loading steps are not measurable, so the bar must not
            // pretend to know a percentage.
            if (!_busy || _barFill == null)
            {
                return;
            }

            _sweep += Time.unscaledDeltaTime * 0.9f;
            float phase = Mathf.PingPong(_sweep, 1f);
            _barFill.anchorMin = new Vector2(Mathf.Lerp(0f, 0.8f, phase), 0f);
            _barFill.anchorMax = new Vector2(Mathf.Lerp(0.2f, 1f, phase), 1f);
        }
    }
}
