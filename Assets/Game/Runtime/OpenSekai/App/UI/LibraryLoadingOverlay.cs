using System;
using System.Collections;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Full-page "preparing" screen shown while a package is read and registered: the jacket over
    /// a glow in its accent, the title, the current step and an indeterminate bar, with 取消 while
    /// loading still runs. It is also the curtain into the live (<see cref="LiveEntryCurtain"/>):
    /// it fades in over the library, holds while anything is still loading, is carried across the
    /// scene load, and fades out as the song info page fades in. Fade in plus fade out stays well
    /// under a second; only real loading time adds to it.
    /// </summary>
    public sealed class LibraryLoadingOverlay : MonoBehaviour, ILiveEntryCover
    {
        private const float COVER_SIZE = 300f;
        private const float BAR_WIDTH = 520f;
        private const float BAR_HEIGHT = 6f;
        private const float SWEEP_WIDTH = 0.28f;
        private const float SWEEP_SECONDS = 1.3f;

        private const float FADE_IN_SECONDS = 0.3f;
        private const float FADE_OUT_SECONDS = 0.35f;
        private const float CONTENT_DRIFT = 16f;

        /// <summary>Above every menu, loading and transition canvas, so nothing flashes over it.</summary>
        private const int SORTING_ORDER = 32000;

        /// <summary>A live that never reaches its info page does not leave the screen covered.</summary>
        private const float CARRIED_TIMEOUT_SECONDS = 20f;

        private const string LIVE_SCENE_NAME = "Core";

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
        private RawImage _carriedCover;
        private RenderTexture _carriedTexture;
        private Action _onCovered;
        private bool _covered;
        private bool _carried;
        private bool _releasing;
        private bool _busy;
        private float _sweep;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;

        /// <param name="parent">The library page; the overlay lives in its own top-level canvas beside it.</param>
        public static LibraryLoadingOverlay Create(Transform parent, Action onCancel)
        {
            // A root overlay canvas of its own: the library page renders through a camera, and the
            // live loading screen pushed on start would otherwise draw over it. Being a root also
            // lets the overlay be kept across the scene load.
            var holder = new GameObject("LiveEntryCurtain", typeof(RectTransform));
            if (parent != null)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, parent.gameObject.scene);
            }
            Canvas canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            CanvasScaler scaler = holder.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            holder.AddComponent<GraphicRaycaster>();

            RectTransform root = LibraryUi.CreateRect("LoadingOverlay", holder.transform);
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
            // The library's jacket sprites are destroyed with the page; across the scene load the
            // overlay shows its own copy here instead.
            GameObject carried = new GameObject("CarriedJacket", typeof(RectTransform), typeof(RawImage));
            carried.transform.SetParent(frame.transform, false);
            _carriedCover = carried.GetComponent<RawImage>();
            _carriedCover.raycastTarget = false;
            _carriedCover.enabled = false;
            LibraryUi.Stretch(_carriedCover.rectTransform);
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
            LiveEntryCurtain.Hold(this);
            if (!wasVisible)
            {
                _sweep = 0f;
                PlayEnter();
            }
        }

        /// <summary>Fills in the jacket and its accent once they are decoded, while the overlay is up.</summary>
        internal void SetArtwork(Sprite cover, Color32 accent)
        {
            if (!gameObject.activeSelf || _carried)
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
            if (_carried)
            {
                // Past the scene hand-over the overlay belongs to the live; it leaves by Release.
                return;
            }

            LiveEntryCurtain.Drop(this);
            _busy = false;
            _covered = false;
            _onCovered = null;
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
            LiveEntryCurtain.Drop(this);
            _showMotion.TryCancel();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_carriedTexture != null)
            {
                _carriedTexture.Release();
                Destroy(_carriedTexture);
                _carriedTexture = null;
            }
        }

        private void PlayEnter()
        {
            _showMotion.TryCancel();
            _covered = false;
            _group.alpha = 0f;
            Vector2 from = new Vector2(0f, 10f - CONTENT_DRIFT);
            _content.anchoredPosition = from;
            _showMotion = LSequence.Create()
                .Append(LMotion.Create(0f, 1f, FADE_IN_SECONDS)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .WithOnComplete(OnFadeInComplete)
                    .BindToAlpha(_group))
                .Join(LMotion.Create(from, new Vector2(0f, 10f), FADE_IN_SECONDS)
                    .WithEase(LibraryTheme.Motion.ENTER_EASE)
                    .BindToAnchoredPosition(_content))
                .Run();
        }

        private void OnFadeInComplete()
        {
            _covered = true;
            if (_onCovered != null && this != null)
            {
                StartCoroutine(InvokeCovered());
            }
        }

        /// <summary>
        /// Takes the overlay out of the library page so it survives the scene load, then runs
        /// <paramref name="onCovered"/> once the fade-in is done — a frame later at the earliest, so
        /// the scene swap never starts inside the screen push that asked for it.
        /// </summary>
        public void WhenCovered(Action onCovered)
        {
            if (_releasing)
            {
                onCovered?.Invoke();
                return;
            }

            Carry();
            _onCovered += onCovered;
            if (_covered)
            {
                StartCoroutine(InvokeCovered());
            }
        }

        private IEnumerator InvokeCovered()
        {
            yield return null;
            Action callback = _onCovered;
            _onCovered = null;
            callback?.Invoke();
        }

        /// <summary>Fades out over whatever the live has put up by now, then removes itself.</summary>
        public void Release()
        {
            LiveEntryCurtain.Drop(this);
            if (_releasing || this == null)
            {
                return;
            }

            _releasing = true;
            _busy = false;
            _onCovered = null;
            _group.blocksRaycasts = false;
            _showMotion.TryCancel();
            float from = _group.alpha;
            Vector2 position = _content.anchoredPosition;
            GameObject owner = transform.parent.gameObject;
            _showMotion = LSequence.Create()
                .Append(LMotion.Create(from, 0f, FADE_OUT_SECONDS)
                    .WithEase(Ease.InOutQuad)
                    .WithOnComplete(() =>
                    {
                        if (owner != null)
                        {
                            Destroy(owner);
                        }
                    })
                    .BindToAlpha(_group))
                .Join(LMotion.Create(position, position + new Vector2(0f, CONTENT_DRIFT), FADE_OUT_SECONDS)
                    .WithEase(LibraryTheme.Motion.EXIT_EASE)
                    .BindToAnchoredPosition(_content))
                .Run();
        }

        private void Carry()
        {
            if (_carried)
            {
                return;
            }

            _carried = true;
            _cancelButton.interactable = false;
            _cancelButton.gameObject.SetActive(false);
            CopyJacket();

            DontDestroyOnLoad(transform.parent.gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ReleaseAfterTimeout());
        }

        /// <summary>The library's jacket sprite dies with the page, so a GPU copy goes with the overlay.</summary>
        private void CopyJacket()
        {
            Sprite sprite = _cover.enabled ? _cover.sprite : null;
            Texture source = sprite != null ? sprite.texture : null;
            if (source == null)
            {
                return;
            }

            try
            {
                _carriedTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                _carriedTexture.Create();
                Graphics.Blit(source, _carriedTexture);
                Rect rect = sprite.textureRect;
                _carriedCover.texture = _carriedTexture;
                _carriedCover.uvRect = new Rect(
                    rect.x / source.width, rect.y / source.height, rect.width / source.width, rect.height / source.height);
                _carriedCover.enabled = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryLoadingOverlay] Could not keep the jacket for the live: " + exception.Message);
                _coverPlaceholder.SetActive(true);
            }

            _cover.enabled = false;
            _cover.sprite = null;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // A start that ends anywhere but the live (an error sends it back) must not stay covered.
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single && scene.name != LIVE_SCENE_NAME)
            {
                Release();
            }
        }

        private IEnumerator ReleaseAfterTimeout()
        {
            yield return new WaitForSecondsRealtime(CARRIED_TIMEOUT_SECONDS);
            if (!_releasing)
            {
                Debug.LogWarning("[LibraryLoadingOverlay] The live never released the loading curtain; removing it.");
                Release();
            }
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
