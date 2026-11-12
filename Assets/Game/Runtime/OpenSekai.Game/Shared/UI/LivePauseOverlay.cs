using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OpenSekai.Shared.UI
{
    /// <summary>
    /// Lightweight pause layer for the player-library flow: resume, retry, quit back to the library.
    /// Built in code so it carries no editor dialogs and no LiveOutUI prefab dependency.
    /// The live scene owns the lifetime; the overlay only reports the chosen action.
    /// </summary>
    public sealed class LivePauseOverlay : MonoBehaviour
    {
        private const float PANEL_WIDTH = 720f;
        private const float PANEL_HEIGHT = 460f;

        private static readonly Color32 DimColor = new Color32(0, 0, 0, 150);
        private static readonly Color32 PanelColor = new Color32(26, 32, 40, 255);
        private static readonly Color32 ButtonColor = new Color32(58, 74, 94, 255);
        private static readonly Color32 AccentColor = new Color32(168, 208, 240, 255);
        private static readonly Color32 SongColor = new Color32(168, 182, 198, 255);

        private static TMP_FontAsset _font;
        private static bool _fontResolved;

        private Action _onResume;
        private Action _onRetry;
        private Action _onQuit;
        private TextMeshProUGUI _songText;

        public bool IsVisible => gameObject.activeSelf;

        public static LivePauseOverlay Create(Transform parent)
        {
            GameObject root = new GameObject("LivePauseOverlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            LivePauseOverlay overlay = root.AddComponent<LivePauseOverlay>();
            overlay.Build();
            overlay.Hide();
            return overlay;
        }

        public void Show(string songTitle, Action resume, Action retry, Action quit)
        {
            _onResume = resume;
            _onRetry = retry;
            _onQuit = quit;
            if (_songText != null)
            {
                _songText.text = string.IsNullOrWhiteSpace(songTitle) ? string.Empty : songTitle;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _onResume = null;
            _onRetry = null;
            _onQuit = null;
            gameObject.SetActive(false);
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

            RectTransform root = (RectTransform)transform;
            Stretch(root);

            Image dim = CreateImage("Dim", root, DimColor);
            Stretch(dim.rectTransform);

            RectTransform panel = CreateRect("Panel", root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            TextMeshProUGUI title = CreateText(
                "Title", panel, "暂停", 44f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(32f, -110f);
            title.rectTransform.offsetMax = new Vector2(-32f, -40f);

            _songText = CreateText("Song", panel, string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            _songText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _songText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _songText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _songText.rectTransform.offsetMin = new Vector2(32f, -148f);
            _songText.rectTransform.offsetMax = new Vector2(-32f, -110f);
            _songText.color = SongColor;

            Button resume = CreateButton("Resume", panel, "继续", Resume, 420f, 68f);
            Place((RectTransform)resume.transform, 250f);

            Button retry = CreateButton("Retry", panel, "重开", Retry, 420f, 68f);
            Place((RectTransform)retry.transform, 160f);

            Button quit = CreateButton("Quit", panel, "退出到曲库", Quit, 420f, 68f);
            Place((RectTransform)quit.transform, 70f);
        }

        private void Resume()
        {
            Action callback = _onResume;
            Hide();
            callback?.Invoke();
        }

        private void Retry()
        {
            Action callback = _onRetry;
            Hide();
            callback?.Invoke();
        }

        private void Quit()
        {
            Action callback = _onQuit;
            Hide();
            callback?.Invoke();
        }

        private static void Place(RectTransform target, float bottomOffset)
        {
            target.anchorMin = new Vector2(0.5f, 0f);
            target.anchorMax = new Vector2(0.5f, 0f);
            target.pivot = new Vector2(0.5f, 0f);
            target.anchoredPosition = new Vector2(0f, bottomOffset);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Color32 color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            TMP_FontAsset font = ResolveFont();
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static Button CreateButton(
            string name, Transform parent, string label, UnityAction onClick, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            Image image = go.GetComponent<Image>();
            image.color = ButtonColor;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(220, 238, 255, 255);
            colors.pressedColor = new Color32(180, 213, 242, 255);
            colors.disabledColor = new Color32(120, 126, 132, 120);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = CreateText("Label", rect, label, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = AccentColor;
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The overlay is built in the live scene, whose UI font stack is the Japanese Rodin
        /// replacement; simplified-Chinese labels need the bundled CJK font instead.
        /// </summary>
        private static TMP_FontAsset ResolveFont()
        {
            if (_fontResolved)
            {
                return _font;
            }

            _fontResolved = true;
            _font = Resources.Load<TMP_FontAsset>("font/SourceHanSansSC-Medium SDF_Dynamic");
            if (_font == null)
            {
                _font = Resources.Load<TMP_FontAsset>("font/SourceHanSansSC-Bold SDF_Dynamic");
            }

            return _font;
        }
    }
}
