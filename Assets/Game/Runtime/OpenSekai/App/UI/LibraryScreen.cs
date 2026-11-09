using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Lightweight song library: package list plus manual/auto playback entry.
    /// Builds its own UGUI hierarchy in code so the page needs no prefab, no ScreenLayerData
    /// registration and nothing from the chart editor's presenter stack.
    /// </summary>
    public sealed class LibraryScreen : MonoBehaviour, ILibraryPage
    {
        private const float ROW_HEIGHT = 92f;
        private const float BUTTON_WIDTH = 132f;
        private const float BUTTON_HEIGHT = 52f;
        private const string BUNDLED_FONT_RESOURCE_PATH = "Fonts/NotoSansSC-UI";
        /// <summary>Container extension used for exported packages; the picker accepts .zip too.</summary>
        private const string PackageExtension = ".oskp";
        private static readonly Color32 BackgroundColor = new Color32(18, 22, 28, 255);
        private static readonly Color32 RowColor = new Color32(32, 39, 48, 255);
        private static readonly Color32 ButtonColor = new Color32(54, 67, 80, 255);
        private static readonly Color32 AccentColor = new Color32(72, 148, 214, 255);
        private static readonly string[] CjkFontCandidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC",
            "Source Han Sans SC", "PingFang SC", "Hiragino Sans GB", "Arial Unicode MS"
        };

        private static TMP_FontAsset _cjkFontAsset;
        private static bool _cjkFontResolved;

        private RectTransform _contentRoot;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _statusText;
        private LibrarySettingsPanel _settingsPanel;
        private LibraryResultPanel _resultPanel;
        private LibraryLoadingOverlay _loadingOverlay;
        private LibraryActionsPanel _actionsPanel;
        private TextMeshProUGUI _sortLabelText;
        private int _sortMode;
        private CancellationTokenSource _playbackCts;
        private bool _isStartingPlayback;
        private static readonly Dictionary<string, Sprite> CoverCache = new Dictionary<string, Sprite>();

        public bool IsReady => isActiveAndEnabled && _contentRoot != null;

        public static LibraryScreen Create(Transform parent = null)
        {
            GameObject root = new GameObject("LibraryScreen", typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            LibraryScreen screen = root.AddComponent<LibraryScreen>();
            screen.Build();
            return screen;
        }

        private void Awake()
        {
            LibraryPageRegistry.Register(this);
        }

        private void OnDestroy()
        {
            LibraryPageRegistry.Unregister(this);
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = null;
        }

        private void Build()
        {
            // A dedicated camera keeps the page self-contained (no ScreenManager camera stack),
            // and makes the UI capturable through camera-based screenshots.
            Camera uiCamera = CreateUiCamera();
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            gameObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)transform;
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
            offset = AddHeaderButton(header, "Refresh", "刷新", Refresh, ref offset);
            offset = AddHeaderButton(header, "Settings", "设置", OpenSettings, ref offset);
            if (LibraryImport.SupportsFolderPick)
            {
                offset = AddHeaderButton(header, "ImportFolder", "导入文件夹", ImportFolder, ref offset);
            }
            AddHeaderButton(header, "ImportZip", "导入 ZIP", ImportZip, ref offset);
            Button sortButton = CreateHeaderButton(header, "Sort", SortModeLabel(), CycleSort, ref offset);
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

            // Build the list first: the page must stay usable even if the optional panel fails.
            Refresh();

            try
            {
                _settingsPanel = LibrarySettingsPanel.Create(root, OnSettingsChanged);
            }
            catch (Exception exception)
            {
                _settingsPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the settings panel: " + exception);
            }

            try
            {
                _resultPanel = LibraryResultPanel.Create(root, OnResultRetry);
            }
            catch (Exception exception)
            {
                _resultPanel = null;
                Debug.LogWarning("[LibraryScreen] Could not build the result panel: " + exception);
            }

            try
            {
                _loadingOverlay = LibraryLoadingOverlay.Create(root, OnCancelPlayback);
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

        private void Start()
        {
            // The live scene publishes its result just before it unloads; show it once here.
            PlayResult pending = PlayResultHandoff.Take();
            if (pending != null)
            {
                ShowResult(pending);
            }
        }

        /// <summary>Displays one finished (or failed) session. Public so the flow can be driven from
        /// tooling without clicking a row.</summary>
        public void ShowResult(PlayResult result)
        {
            if (result == null)
            {
                return;
            }

            SetStatus(result.Title + "  ·  " + result.KindText);
            _resultPanel?.Show(result);
        }

        private void OnResultRetry(PlayResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.PackageDirectory))
            {
                SetStatus("找不到该曲包的目录，无法重开。");
                return;
            }

            StartPlayback(result.PackageDirectory, result.Title, result.IsAuto);
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

        private string SortModeLabel()
        {
            switch (_sortMode)
            {
                case 1: return "排序：等级";
                case 2: return "排序：最近";
                default: return "排序：标题";
            }
        }

        private void CycleSort()
        {
            _sortMode = (_sortMode + 1) % 3;
            if (_sortLabelText != null)
            {
                _sortLabelText.text = SortModeLabel();
            }

            Refresh();
        }

        private void OpenActions(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            string title = BuildTitle(item);
            _actionsPanel?.Show(
                title,
                "导出为 .oskp 文件，或把曲包从曲库删除。",
                () => ExportPackage(item),
                () => DeletePackage(item));
        }

        private void ExportPackage(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            // Keep the extension out of the suggested name: the platform save dialog appends the
            // filter extension itself, and passing "Test.oskp" produced "Test.oskp.oskp".
            string baseName = SafeFileName(BuildTitle(item));
            string destination = LibraryImport.PickExportDestination(baseName);
            if (string.IsNullOrEmpty(destination))
            {
                // Mobile has no save dialog: write next to the library so the file is still reachable.
                string exportDirectory = Path.Combine(SongPackageStorage.RootDirectory, "Exports");
                destination = Path.Combine(exportDirectory, baseName + PackageExtension);
                SetStatus("正在导出到 " + destination + " …");
            }
            else if (!destination.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                // Some dialogs do not append the filter extension when the user typed one.
                destination += PackageExtension;
            }

            try
            {
                string written = SongPackageManager.Shared.ExportZip(item.Entry, destination);
                SetStatus("已导出：" + written);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("导出失败：" + exception.Message);
            }
        }

        private void DeletePackage(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            string title = BuildTitle(item);
            try
            {
                SongPackageManager.Shared.DeleteEntry(item.Entry);
                SetStatus("已删除曲包：" + title);
                Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("删除失败：" + exception.Message);
            }
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "package";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalid, buffer[i]) >= 0 || buffer[i] == '/' || buffer[i] == '\\')
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer).Trim();
        }

        private void OpenSettings()
        {
            _settingsPanel?.Show();
        }

        private void OnSettingsChanged()
        {
            SetStatus("设置已保存。");
        }

        private void ImportZip()
        {
            SetStatus("请选择曲包文件（.zip / .oskp）…");
            LibraryImport.PickZip(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("已取消导入。");
                    return;
                }

                StartImport(path, false);
            });
        }

        private void ImportFolder()
        {
            SetStatus("请选择曲包文件夹…");
            LibraryImport.PickFolder(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("已取消导入。");
                    return;
                }

                StartImport(path, true);
            });
        }

        private void StartImport(string path, bool isFolder)
        {
            SetStatus("正在导入…");
            try
            {
                SongPackage package = isFolder
                    ? SongPackageManager.Shared.ImportFolder(path)
                    : SongPackageManager.Shared.ImportZip(path);
                string name = package?.Manifest != null && !string.IsNullOrWhiteSpace(package.Manifest.title)
                    ? package.Manifest.title
                    : "曲包";
                SetStatus("导入成功：" + name);
                Refresh();
            }
            catch (Exception exception)
            {
                SetStatus(LibraryImport.DescribeFailure(exception));
            }
        }

        public void Refresh()
        {
            if (_contentRoot == null)
            {
                return;
            }

            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _contentRoot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            IReadOnlyList<SongPackageItem> items;
            try
            {
                items = SongPackageManager.Shared.LoadItems();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("读取曲库失败：" + exception.Message);
                return;
            }

            if (_titleText != null)
            {
                _titleText.text = "曲库（" + items.Count + "）";
            }

            if (items.Count == 0)
            {
                SetStatus("还没有曲包。把曲包文件夹放到 CustomMusicScores 目录后点“刷新”。");
                return;
            }

            List<SongPackageItem> ordered = new List<SongPackageItem>(items);
            ordered.Sort(CompareItems);
            for (int i = 0; i < ordered.Count; i++)
            {
                CreateRow(_contentRoot, ordered[i]);
            }

            SetStatus("就绪。");
        }

        private int CompareItems(SongPackageItem a, SongPackageItem b)
        {
            string left = BuildTitle(a);
            string right = BuildTitle(b);
            switch (_sortMode)
            {
                case 1:
                {
                    int levelCompare = PlayLevelOf(b).CompareTo(PlayLevelOf(a));
                    return levelCompare != 0
                        ? levelCompare
                        : string.Compare(left, right, StringComparison.CurrentCulture);
                }
                case 2:
                {
                    int timeCompare = b.LastWriteTime.CompareTo(a.LastWriteTime);
                    return timeCompare != 0
                        ? timeCompare
                        : string.Compare(left, right, StringComparison.CurrentCulture);
                }
                default:
                    return string.Compare(left, right, StringComparison.CurrentCulture);
            }
        }

        private static int PlayLevelOf(SongPackageItem item)
        {
            try
            {
                return item?.Entry?.Manifest != null ? item.Entry.Manifest.playLevel : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void CreateRow(Transform parent, SongPackageItem item)
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

            TextMeshProUGUI title = CreateText("Song", row, BuildTitle(item), 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(textLeft, 26f);
            title.rectTransform.offsetMax = new Vector2(-(BUTTON_WIDTH * 2f + 140f), -12f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI meta = CreateText("Meta", row, BuildMeta(item), 20f, FontStyles.Normal, TextAlignmentOptions.Left);
            meta.rectTransform.anchorMin = new Vector2(0f, 0f);
            meta.rectTransform.anchorMax = new Vector2(1f, 0f);
            meta.rectTransform.pivot = new Vector2(0.5f, 0f);
            meta.rectTransform.offsetMin = new Vector2(textLeft, 8f);
            meta.rectTransform.offsetMax = new Vector2(-(BUTTON_WIDTH * 2f + 140f), 34f);
            meta.color = new Color32(168, 182, 198, 255);

            SongPackageItem captured = item;
            Button manual = CreateButton("Manual", row, "手动", () => StartPlayback(captured, false), BUTTON_WIDTH, BUTTON_HEIGHT);
            Button auto = CreateButton("Auto", row, "自动", () => StartPlayback(captured, true), BUTTON_WIDTH, BUTTON_HEIGHT);
            PlaceRightAligned((RectTransform)manual.transform, BUTTON_WIDTH + 40f);
            PlaceRightAligned((RectTransform)auto.transform, 24f);

            Button more = CreateButton("More", row, "更多", () => OpenActions(captured), 88f, BUTTON_HEIGHT);
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

        private void StartPlayback(SongPackageItem item, bool isAuto)
        {
            if (_isStartingPlayback || item?.Entry == null)
            {
                return;
            }

            StartPlayback(item.Entry.RootDirectory, BuildTitle(item), isAuto);
        }

        /// <summary>Starts a session for a package directory; used by rows and by 再玩一次.</summary>
        private void StartPlayback(string packageDirectory, string title, bool isAuto)
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = new CancellationTokenSource();
            PlayAsync(packageDirectory, title, isAuto, _playbackCts.Token).Forget();
        }

        private void OnCancelPlayback()
        {
            _playbackCts?.Cancel();
        }

        /// <summary>Loads (and caches) a package jacket as a sprite; null when the package has none.</summary>
        private static Sprite LoadCover(SongPackage package)
        {
            if (package == null)
            {
                return null;
            }

            string path = package.JacketPath;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (CoverCache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;
            try
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(bytes))
                    {
                        texture.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        sprite.name = Path.GetFileNameWithoutExtension(path);
                    }
                    else
                    {
                        Destroy(texture);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not load jacket " + path + ": " + exception.Message);
            }

            CoverCache[path] = sprite;
            return sprite;
        }

        private async UniTaskVoid PlayAsync(
            string packageDirectory, string title, bool isAuto, CancellationToken cancellationToken)
        {
            _isStartingPlayback = true;
            try
            {
                SetStatus((isAuto ? "自动" : "手动") + "：" + title + " — 准备中…");
                _loadingOverlay?.Show(title, "正在准备…");
                PlayStartResult result = await PlayService.Instance.StartAsync(
                    new PlayRequest(packageDirectory, isAuto, SceneManager.Scene.Library),
                    cancellationToken,
                    message =>
                    {
                        SetStatus(message);
                        _loadingOverlay?.SetMessage(message);
                    });

                if (!result.Started)
                {
                    _loadingOverlay?.Hide();
                    SetStatus(result.Message);
                    if (result.Status == PlayStartStatus.Failed && _resultPanel != null)
                    {
                        _resultPanel.Show(PlayResult.FromLoadError(title, isAuto, packageDirectory, result.Message));
                    }
                }
                else
                {
                    // The live scene unloads this page; keep the overlay until then so the
                    // transition does not flash an empty library behind the loading screen.
                    _loadingOverlay?.SetMessage("正在进入…");
                }
            }
            catch (OperationCanceledException)
            {
                _loadingOverlay?.Hide();
                SetStatus("已取消开局。");
            }
            catch (Exception exception)
            {
                _loadingOverlay?.Hide();
                Debug.LogException(exception);
                SetStatus("开局失败，请检查运行日志。");
            }
            finally
            {
                _isStartingPlayback = false;
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }
        }

        private static string BuildTitle(SongPackageItem item)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            if (manifest == null)
            {
                return "未命名曲包";
            }
            return string.IsNullOrWhiteSpace(manifest.title) ? (manifest.scoreTitle ?? "未命名曲包") : manifest.title;
        }

        private static string BuildMeta(SongPackageItem item)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            string difficulty = manifest != null && !string.IsNullOrWhiteSpace(manifest.musicDifficultyType)
                ? manifest.musicDifficultyType.ToUpperInvariant()
                : "—";
            string level = manifest != null && manifest.playLevel > 0 ? " Lv." + manifest.playLevel : string.Empty;
            return difficulty + level + "  ·  " + item.StatusText;
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

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image CreateImage(string name, Transform parent, Color32 color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// The project font is a Japanese Rodin SDF asset; simplified Chinese labels would fall
        /// back to missing glyphs. Resolve one OS font that actually contains CJK and use it for
        /// this page. Returns null when the platform has no usable font, leaving TMP's default.
        /// </summary>
        private static TMP_FontAsset ResolveCjkFont()
        {
            if (_cjkFontResolved)
            {
                return _cjkFontAsset;
            }

            _cjkFontResolved = true;

            // Preferred: the bundled UI font (Noto Sans SC subset, OFL). It is the only source
            // that is guaranteed to exist on every target and cannot be affected by OS fonts.
            _cjkFontAsset = LoadBundledFont();
            if (_cjkFontAsset != null)
            {
                Debug.Log("[LibraryScreen] CJK font: " + _cjkFontAsset.name);
                return _cjkFontAsset;
            }

            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < CjkFontCandidates.Length && _cjkFontAsset == null; i++)
            {
                string candidate = FindInstalledFont(installed, CjkFontCandidates[i]);
                if (candidate == null)
                {
                    continue;
                }

                _cjkFontAsset = CreateFontAsset(candidate);
            }

            // GDI may report localized family names (for example "微软雅黑" on a zh-CN system),
            // so fall back to substring markers in both languages.
            string[] markers = { "YaHei", "雅黑", "SimHei", "黑体", "MSYH", "DengXian", "等线", "SimSun", "宋体", "Noto Sans SC", "Noto Sans CJK", "Source Han" };
            for (int m = 0; m < markers.Length && _cjkFontAsset == null; m++)
            {
                for (int i = 0; i < installed.Length && _cjkFontAsset == null; i++)
                {
                    if (installed[i].IndexOf(markers[m], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _cjkFontAsset = CreateFontAsset(installed[i]);
                    }
                }
            }

            if (_cjkFontAsset == null)
            {
                _cjkFontAsset = LoadProjectFontFallback();
            }

            Debug.Log("[LibraryScreen] CJK font: " + (_cjkFontAsset != null ? _cjkFontAsset.name : "<none>")
                + " / os fonts: " + installed.Length
                + " / first: " + string.Join(" | ", installed, 0, Math.Min(12, installed.Length)));
            return _cjkFontAsset;
        }

        private static TMP_FontAsset LoadBundledFont()
        {
            Font font;
            try
            {
                font = Resources.Load<Font>(BUNDLED_FONT_RESOURCE_PATH);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not load bundled font: " + exception.Message);
                return null;
            }

            if (font == null)
            {
                return null;
            }

            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset != null)
                {
                    asset.name = "NotoSansSC-UI";
                }
                return asset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not build TMP asset from bundled font: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Fallback when the OS exposes no CJK family (some Windows installs only register a
        /// subset through GDI). The project's dynamic Rodin asset covers Japanese kanji, so
        /// labels stay readable even though simplified-only glyphs may still be missing.
        /// </summary>
        private static TMP_FontAsset LoadProjectFontFallback()
        {
            TMP_FontAsset asset = Resources.Load<TMP_FontAsset>("font/SourceHanSansSC-Medium SDF_Dynamic");
            if (asset == null)
            {
                asset = Resources.Load<TMP_FontAsset>("font/SourceHanSansSC-Bold SDF_Dynamic");
            }
            return asset;
        }

        private static string FindInstalledFont(string[] installed, string preferred)
        {
            for (int i = 0; i < installed.Length; i++)
            {
                if (string.Equals(installed[i], preferred, StringComparison.OrdinalIgnoreCase))
                {
                    return installed[i];
                }
            }
            return null;
        }

        private static TMP_FontAsset CreateFontAsset(string osFontName)
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(osFontName, 36);
                if (osFont == null)
                {
                    return null;
                }

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(osFont);
                if (asset != null)
                {
                    asset.name = "LibraryScreen-" + osFontName;
                }
                return asset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not build font asset from " + osFontName + ": " + exception.Message);
                return null;
            }
        }

        internal static TextMeshProUGUI CreateText(
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

            TMP_FontAsset font = ResolveCjkFont();
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        internal static Button CreateButton(
            string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height)
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

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
