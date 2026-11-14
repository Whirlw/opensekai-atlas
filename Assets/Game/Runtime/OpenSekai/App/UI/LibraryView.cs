using System;
using System.Collections.Generic;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static OpenSekai.App.UI.LibraryUi;

namespace OpenSekai.App.UI
{
    internal sealed class LibraryView : ILibraryView
    {
        private const float ROW_HEIGHT = 92f;
        private const float BUTTON_WIDTH = 132f;
        private const float BUTTON_HEIGHT = 52f;
        private static readonly Color32 BackgroundColor = new Color32(18, 22, 28, 255);
        private static readonly Color32 RowColor = new Color32(32, 39, 48, 255);

        private RectTransform _contentRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _statusText;
        private LibrarySettingsPanel _settingsPanel;
        private LibraryResultPanel _resultPanel;
        private LibraryLoadingOverlay _loadingOverlay;
        private LibraryActionsPanel _actionsPanel;
        private TextMeshProUGUI _sortLabelText;

        private readonly GameObject _page;

        internal LibraryView(GameObject page)
        {
            _page = page;
        }

        public bool HasList => _contentRoot != null;

        internal void BuildLayout(LibraryPresenter presenter)
        {
            // A dedicated camera keeps the page self-contained (no ScreenManager camera stack),
            // and makes the UI capturable through camera-based screenshots.
            Camera uiCamera = CreateUiCamera();
            Canvas canvas = _page.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            _page.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = _page.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)_page.transform;
            Stretch(root);

            Image background = CreateImage("Background", root, BackgroundColor);
            Stretch(background.rectTransform);

            RectTransform header = CreateRect("Header", root);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(64f, -140f);
            header.offsetMax = new Vector2(-64f, -40f);

            _titleText = CreateText("Title", header, "曲库", 44f, FontStyles.Bold, TextAlignmentOptions.Left);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _titleText.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            _titleText.rectTransform.offsetMin = Vector2.zero;
            _titleText.rectTransform.offsetMax = Vector2.zero;

            // Header actions, laid out right-to-left: 刷新 / 设置 / 导入文件夹 / 导入 ZIP.
            float offset = 0f;
            offset = AddHeaderButton(header, "Refresh", "刷新", presenter.Refresh, ref offset);
            offset = AddHeaderButton(header, "Settings", "设置", presenter.OpenSettings, ref offset);
            if (LibraryImport.SupportsFolderPick)
            {
                offset = AddHeaderButton(header, "ImportFolder", "导入文件夹", presenter.ImportFolder, ref offset);
            }
            AddHeaderButton(header, "ImportZip", "导入 ZIP", presenter.ImportZip, ref offset);
            Button sortButton = CreateHeaderButton(header, "Sort", presenter.SortLabel, presenter.CycleSort, ref offset);
            _sortLabelText = sortButton != null ? sortButton.GetComponentInChildren<TextMeshProUGUI>() : null;

            RectTransform listArea = CreateRect("List", root);
            listArea.anchorMin = new Vector2(0f, 0f);
            listArea.anchorMax = new Vector2(1f, 1f);
            listArea.offsetMin = new Vector2(64f, 132f);
            listArea.offsetMax = new Vector2(-64f, -156f);

            ScrollRect scroll = CreateScrollRect("Packages", listArea, out _contentRoot);
            Stretch((RectTransform)scroll.transform);

            RectTransform footer = CreateRect("Footer", root);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = new Vector2(64f, 32f);
            footer.offsetMax = new Vector2(-64f, 112f);

            _statusText = CreateText("Status", footer, string.Empty, 24f, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(_statusText.rectTransform);
        }

        internal void BuildPanels(LibraryPresenter presenter)
        {
            var root = _page.transform;

            try
            {
                _settingsPanel = LibrarySettingsPanel.Create(root, presenter.OnSettingsChanged);
            }
            catch (Exception exception)
            {
                _settingsPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the settings panel: " + exception);
            }

            try
            {
                _resultPanel = LibraryResultPanel.Create(root, presenter.OnResultRetry);
            }
            catch (Exception exception)
            {
                _resultPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the result panel: " + exception);
            }

            try
            {
                _loadingOverlay = LibraryLoadingOverlay.Create(root, presenter.OnCancelPlayback);
            }
            catch (Exception exception)
            {
                _loadingOverlay = null;
                Debug.LogWarning("[LibraryScreen] Could not build the loading overlay: " + exception);
            }

            try
            {
                _actionsPanel = LibraryActionsPanel.Create(root);
            }
            catch (Exception exception)
            {
                _actionsPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the actions panel: " + exception);
            }
        }

        public void ClearRows()
        {
            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _contentRoot.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        public void SetCount(int count)
        {
            if (_titleText != null)
            {
                _titleText.text = "曲库（" + count + "）";
            }
        }

        public void SetSortLabel(string label)
        {
            if (_sortLabelText != null)
            {
                _sortLabelText.text = label;
            }
        }

        public void RenderRows(IReadOnlyList<SongPackageItem> items, Action<SongPackageItem, bool> play, Action<SongPackageItem> openActions)
        {
            foreach (var item in items)
            {
                CreateRow(_contentRoot, item, play, openActions);
            }
        }

        public void ShowSettings() => _settingsPanel?.Show();
        public void ShowResult(PlayResult result) => _resultPanel?.Show(result);
        public void ShowLoading(string title) => _loadingOverlay?.Show(title, "正在准备…");
        public void HideLoading() => _loadingOverlay?.Hide();
        public void SetLoadingMessage(string message) => _loadingOverlay?.SetMessage(message);

        public void ShowActions(string title, Action export, Action delete)
        {
            _actionsPanel?.Show(title, "导出为 .oskp 文件，或把曲包从曲库删除。", export, delete);
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }
        }

        private float AddHeaderButton(
            Transform header, string name, string label, UnityEngine.Events.UnityAction onClick, ref float rightOffset)
        {
            CreateHeaderButton(header, name, label, onClick, ref rightOffset);
            return rightOffset;
        }

        private Button CreateHeaderButton(
            Transform header, string name, string label, UnityEngine.Events.UnityAction onClick, ref float rightOffset)
        {
            Button button = CreateButton(name, header, label, onClick, 180f, 56f);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-rightOffset, 0f);
            rightOffset += 192f;
            return button;
        }

        private void CreateRow(Transform parent, SongPackageItem item, Action<SongPackageItem, bool> play, Action<SongPackageItem> openActions)
        {
            RectTransform row = CreateRect("Row", parent);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = ROW_HEIGHT;
            layout.flexibleWidth = 1f;

            Image rowBackground = row.gameObject.AddComponent<Image>();
            rowBackground.color = RowColor;

            // Jacket thumbnail, when the package ships one; the text column shifts right to fit.
            float textLeft = 24f;
            Sprite cover = LoadCover(item.Entry);
            if (cover != null)
            {
                Image coverImage = CreateImage("Cover", row, Color.white);
                coverImage.sprite = cover;
                coverImage.preserveAspect = true;
                RectTransform coverRect = coverImage.rectTransform;
                coverRect.anchorMin = new Vector2(0f, 0.5f);
                coverRect.anchorMax = new Vector2(0f, 0.5f);
                coverRect.pivot = new Vector2(0f, 0.5f);
                coverRect.sizeDelta = new Vector2(68f, 68f);
                coverRect.anchoredPosition = new Vector2(12f, 0f);
                textLeft = 96f;
            }

            TextMeshProUGUI title = CreateText("Song", row, LibraryModel.BuildTitle(item), 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(textLeft, 26f);
            title.rectTransform.offsetMax = new Vector2(-(BUTTON_WIDTH * 2f + 140f), -12f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI meta = CreateText("Meta", row, LibraryModel.BuildMeta(item), 20f, FontStyles.Normal, TextAlignmentOptions.Left);
            meta.rectTransform.anchorMin = new Vector2(0f, 0f);
            meta.rectTransform.anchorMax = new Vector2(1f, 0f);
            meta.rectTransform.pivot = new Vector2(0.5f, 0f);
            meta.rectTransform.offsetMin = new Vector2(textLeft, 8f);
            meta.rectTransform.offsetMax = new Vector2(-(BUTTON_WIDTH * 2f + 140f), 34f);
            meta.color = new Color32(168, 182, 198, 255);

            SongPackageItem captured = item;
            Button manual = CreateButton("Manual", row, "手动", () => play(captured, false), BUTTON_WIDTH, BUTTON_HEIGHT);
            Button auto = CreateButton("Auto", row, "自动", () => play(captured, true), BUTTON_WIDTH, BUTTON_HEIGHT);
            PlaceRightAligned((RectTransform)manual.transform, BUTTON_WIDTH + 40f);
            PlaceRightAligned((RectTransform)auto.transform, 24f);

            Button more = CreateButton("More", row, "更多", () => openActions(captured), 88f, BUTTON_HEIGHT);
            PlaceRightAligned((RectTransform)more.transform, BUTTON_WIDTH * 2f + 56f);

            bool ready = item.HasManifest && item.HasScore && item.HasAudio;
            manual.interactable = ready;
            auto.interactable = ready && item.HasManifest;
        }

        private static void PlaceRightAligned(RectTransform target, float rightOffset)
        {
            target.anchorMin = new Vector2(1f, 0.5f);
            target.anchorMax = new Vector2(1f, 0.5f);
            target.pivot = new Vector2(1f, 0.5f);
            target.anchoredPosition = new Vector2(-rightOffset, 0f);
        }

        private static Camera CreateUiCamera()
        {
            GameObject cameraObject = new GameObject("LibraryScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.cullingMask = ~0;
            camera.depth = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static ScrollRect CreateScrollRect(string name, Transform parent, out RectTransform content)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(parent, false);

            RectTransform viewport = CreateRect("Viewport", go.transform);
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport);

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }
    }
}
