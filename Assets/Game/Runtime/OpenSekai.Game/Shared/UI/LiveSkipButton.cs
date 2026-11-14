using System;
using Sekai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.Shared.UI
{
    /// <summary>
    /// The skip button a live shows over a long intro or outro. Built in code like
    /// <see cref="LivePauseOverlay"/>, and drawn under it, so a pause dialog covers the button.
    /// The live decides when it shows and what a press does; the button only reports the press.
    /// </summary>
    public sealed class LiveSkipButton : MonoBehaviour
    {
        private const float WIDTH = 300f;
        private const float HEIGHT = 84f;
        private const float MARGIN = 48f;

        private static readonly Color32 ButtonColor = new Color32(26, 32, 40, 210);
        private static readonly Color32 LabelColor = new Color32(220, 236, 250, 255);

        private Action _onPress;
        private TextMeshProUGUI _label;

        public bool IsVisible => gameObject.activeSelf;

        public static LiveSkipButton Create(Transform parent)
        {
            GameObject root = new GameObject("LiveSkipButton", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            LiveSkipButton button = root.AddComponent<LiveSkipButton>();
            button.Build();
            button.gameObject.SetActive(false);
            return button;
        }

        public void Show(string label, Action onPress)
        {
            _onPress = onPress;
            if (_label != null)
            {
                _label.text = label ?? string.Empty;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _onPress = null;
            gameObject.SetActive(false);
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            gameObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(WIDTH, HEIGHT);
            rect.anchoredPosition = new Vector2(-MARGIN - SafeAreaRightInset(), MARGIN);

            Image image = go.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(Press);

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(rect, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _label = textObject.GetComponent<TextMeshProUGUI>();
            _label.fontSize = 30f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            _label.raycastTarget = false;
            TMP_FontAsset font = ResourceCatalog.MediumDynamicFont.Load() ?? ResourceCatalog.BoldDynamicFont.Load();
            if (font != null)
            {
                _label.font = font;
            }
        }

        /// <summary>The right-hand cut-out of a notched phone, in canvas units at the reference height.</summary>
        private static float SafeAreaRightInset()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return 0f;
            }

            float inset = Screen.width - Screen.safeArea.xMax;
            return Mathf.Max(0f, inset) * 1080f / Screen.height;
        }

        private void Press()
        {
            Action callback = _onPress;
            Hide();
            callback?.Invoke();
        }
    }
}
