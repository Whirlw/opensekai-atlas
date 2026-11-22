using System;
using System.Collections.Generic;
using System.IO;
using LitMotion;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 成就 part: how many goals are unlocked and the latest ones on the left, every goal with
    /// its medal and progress on the right, filtered by 累计击打, 谱面达成 or 段位通关. It only reads:
    /// progress comes from <see cref="PlayerProfileStore.ReadAchievements"/> on the main thread
    /// each time the part opens. Like the other parts it builds its own camera and canvas in the
    /// Library scene.
    /// </summary>
    public sealed class AchievementScreen : MonoBehaviour
    {
        private readonly List<AchievementProgress> _items = new List<AchievementProgress>();
        private PlayerProfileStore _store;
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private LibrarySettingsPanel _settings;
        private AchievementUnlockPopup _preview;
        private bool _presented;

        /// <summary>The back button (and back with nothing open): leave the part.</summary>
        internal event Action BackRequested;

        internal bool IsPresented => _presented;

        /// <summary>The store the page reads; the local profile unless a tool swapped in another.</summary>
        internal PlayerProfileStore Store
        {
            get => _store ?? PlayerProfileStore.Local;
            set => _store = value;
        }

        internal AchievementView View { get; private set; }

        internal RectTransform ModalLayer { get; private set; }

        /// <summary>Every goal, in the backend's order.</summary>
        internal IReadOnlyList<AchievementProgress> Items => _items;

        internal AchievementCategory Filter { get; private set; }

        /// <summary>Why the goals could not be read; null when they were.</summary>
        internal string LoadError { get; private set; }

        internal static AchievementScreen Create()
        {
            var host = new GameObject("AchievementScreen");
            AchievementScreen screen = host.AddComponent<AchievementScreen>();
            screen.Build();
            return screen;
        }

        // ---------------------------------------------------------------- presentation

        internal void SetPresented(bool presented, bool animate)
        {
            _presented = presented;
            _curtainMotion.TryCancel();
            _curtainMotion = default;
            _curtain.gameObject.SetActive(false);
            _camera.enabled = presented;
            _raycaster.enabled = presented;
            if (!presented)
            {
                _settings?.Hide();
                _preview.Hide();
            }

            _canvasRoot.SetActive(presented);
            if (!presented) return;
            Reload();
            if (!animate)
            {
                View.SkipEnter();
                return;
            }

            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(1f, 0f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _curtain.gameObject.SetActive(false))
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
            View.PlayEnter();
        }

        internal void PlayExit(Action onCovered)
        {
            if (!_presented)
            {
                onCovered?.Invoke();
                return;
            }

            _curtainMotion.TryCancel();
            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtain.raycastTarget = true;
            _curtainMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                .WithEase(LibraryTheme.Motion.EXIT_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() =>
                {
                    _curtainMotion = default;
                    onCovered?.Invoke();
                })
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
        }

        /// <summary>The Android back key: closes the settings when they are open. False with nothing open.</summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive()) return true;
            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return true;
            }

            return false;
        }

        internal void Back() => BackRequested?.Invoke();

        internal void OpenSettings()
        {
            if (_settings == null) _settings = LibrarySettingsPanel.Create(ModalLayer, null);
            _settings.transform.SetAsLastSibling();
            _settings.Show();
        }

        // ---------------------------------------------------------------- data

        internal void Reload()
        {
            _items.Clear();
            try
            {
                _items.AddRange(Store.ReadAchievements());
                LoadError = null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Achievements] Could not read the progress: " + exception);
                LoadError = exception is InvalidDataException ? "游玩记录无法识别，请检查运行日志" : "成就读取失败，请稍后重试";
            }

            View.Bind();
        }

        /// <summary>Shows only the goals of <paramref name="category"/>; <see cref="AchievementCategory.All"/> shows every one.</summary>
        internal void SetFilter(AchievementCategory category)
        {
            if (category == Filter) return;
            Filter = category;
            UiSound.Play(UiCue.Select);
            View.BindList(true);
        }

        /// <summary>The goals the current filter lets through, in the backend's order.</summary>
        internal List<AchievementProgress> Filtered()
        {
            var filtered = new List<AchievementProgress>();
            foreach (AchievementProgress item in _items)
            {
                if (Filter == AchievementCategory.All || AchievementCopy.For(item).Category == Filter) filtered.Add(item);
            }

            return filtered;
        }

        /// <summary>The unlock card for <paramref name="ids"/>, held at <paramref name="seconds"/> in, for screenshots.</summary>
        internal void PreviewUnlock(IReadOnlyList<string> ids, string context, float seconds)
        {
            if (_preview.Bind(ids, context)) _preview.Seek(seconds);
        }

        internal void EndPreview() => _preview.Hide();

        // ---------------------------------------------------------------- building

        private void Build()
        {
            _camera = CreateCamera();
            _camera.transform.SetParent(transform, false);
            _canvasRoot = new GameObject("AchievementCanvas", typeof(RectTransform));
            _canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _raycaster = _canvasRoot.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)_canvasRoot.transform;
            Image background = LibraryUi.CreateImage("Background", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(background.rectTransform);
            // The 成就 card's gold behind the summary, the home aurora's violet on the far side.
            AddGlow(root, "GlowGold", HomeTheme.Colors.Gold, 0.13f, new Vector2(0f, 1f), new Vector2(360f, -300f), 1700f);
            AddGlow(root, "GlowViolet", HomeTheme.Colors.Violet, 0.1f, new Vector2(1f, 0f), new Vector2(-320f, 160f), 1600f);
            AddGlow(root, "GlowRose", HomeTheme.Colors.Rose, 0.05f, new Vector2(0.65f, 1f), new Vector2(0f, 60f), 1100f);

            View = new AchievementView(root, this);
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            _preview = new AchievementUnlockPopup(root, 18f);

            _curtain = LibraryUi.CreateImage("Curtain", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(_curtain.rectTransform);
            _curtain.raycastTarget = true;
            _curtain.gameObject.SetActive(false);

            _camera.enabled = false;
            _raycaster.enabled = false;
            _canvasRoot.SetActive(false);
        }

        private static void AddGlow(RectTransform root, string name, Color32 tint, float alpha, Vector2 anchor, Vector2 position, float size)
        {
            Image glow = LibraryUi.CreateImage(name, root, new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, alpha));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("AchievementScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 106f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            _curtainMotion.TryCancel();
            _preview?.Dispose();
            View?.Dispose();
            // The camera is a child of this object and goes with it.
        }
    }
}
