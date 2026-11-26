using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Newtonsoft.Json;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 资源包 part of the app: the installed resource packs and the two ways to use them. 预设
    /// takes a whole preset (a pack's, or a built-in set); 混搭 picks groups per category, with
    /// fallbacks in priority order. A demo (the live itself on a short autoplay chart) shows the
    /// choice in play. Like the server part it builds its own camera and canvas in the Library
    /// scene, the home page decides when it comes and goes, and inside it pages stack and slide.
    /// Every confirmed choice is saved at once and applies from the next new live.
    /// </summary>
    public sealed class PackScreen : MonoBehaviour
    {
        private const float PUSH_OFFSET = 64f;

        private readonly List<PackPage> _pages = new List<PackPage>();
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private CanvasGroup _pagesGroup;
        private LibrarySettingsPanel _settings;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private MotionHandle _pushMotion;
        private UiToast _toast;
        private PackImportPanel _importPanel;
        private CancellationTokenSource _importCancel;
        private CancellationTokenSource _backfill;
        private bool _picking;
        private bool _presented;

        /// <summary>The overview's back button (and back from it): leave the part.</summary>
        internal event Action BackRequested;

        internal bool IsPresented => _presented;

        internal RectTransform PageHost { get; private set; }

        /// <summary>Above the pages: modal cards.</summary>
        internal RectTransform ModalLayer { get; private set; }

        /// <summary>Null when the index could not be opened; <see cref="LoadError"/> says why.</summary>
        internal MaterialPackStore Store { get; private set; }

        /// <summary>The configuration and installed packs as last read; null until read or when reading failed.</summary>
        internal MaterialPackState State { get; private set; }

        internal string LoadError { get; private set; }

        internal PackMedia Media { get; private set; }

        internal PackPreviewPanel Preview { get; private set; }

        /// <summary>The demo the top page shows, if it shows one.</summary>
        internal PackDemo Demo { get; private set; }

        /// <summary>True from picking a file until its import ends.</summary>
        internal bool IsImporting => _picking || _importCancel != null;

        internal PackPage Top => _pages.Count > 0 ? _pages[_pages.Count - 1] : null;

        internal static PackScreen Create()
        {
            var host = new GameObject("PackScreen");
            PackScreen screen = host.AddComponent<PackScreen>();
            screen.Build();
            return screen;
        }

        // ---------------------------------------------------------------- presentation

        /// <summary>Brings the part on screen (reading the current configuration) or takes it off.</summary>
        /// <param name="animate">Coming on: rise out of the ground-colored curtain.</param>
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
                Preview.Close();
                Media.Stop();
                _toast.Hide();
                _backfill?.Cancel();
                Top?.OnHidden();
                Demo.Stop();
            }

            _canvasRoot.SetActive(presented);
            if (!presented)
            {
                return;
            }

            Reload(notify: false);
            if (_pages.Count == 0)
            {
                Push(new PackOverviewPage(this), false);
            }
            else
            {
                foreach (PackPage page in _pages) page.OnStateChanged();
                Top.OnShown();
            }

            RefreshDemo();
            BackfillAsync().Forget();
            if (!animate)
            {
                return;
            }

            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(1f, 0f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _curtain.gameObject.SetActive(false))
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
            Top?.PlayEnter();
        }

        /// <summary>Lowers the curtain over the page, then runs <paramref name="onCovered"/>.</summary>
        internal void PlayExit(Action onCovered)
        {
            if (!_presented)
            {
                onCovered?.Invoke();
                return;
            }

            Media.Stop();
            Demo.Stop();
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

        /// <summary>
        /// The Android back key: cancels an import, closes what is open, else goes back a page.
        /// False on the overview with nothing open, where back leaves the part.
        /// </summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive() || _pushMotion.IsActive())
            {
                return true;
            }

            if (_importCancel != null)
            {
                _importCancel.Cancel();
                return true;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return true;
            }

            if (Preview.IsOpen)
            {
                Preview.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            PackPage top = Top;
            if (top != null && top.TryHandleBack())
            {
                return true;
            }

            if (_pages.Count > 1)
            {
                UiSound.Play(UiCue.Back);
                Pop();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- navigation

        internal void Push(PackPage page, bool animate = true)
        {
            PackPage previous = Top;
            Media.Stop();
            _pages.Add(page);
            previous?.OnHidden();
            page.Root.gameObject.SetActive(true);
            page.OnShown();
            RefreshDemo();
            if (previous == null || !animate)
            {
                if (previous != null) previous.Root.gameObject.SetActive(false);
                page.Group.alpha = 1f;
                page.Root.anchoredPosition = Vector2.zero;
                return;
            }

            Slide(previous, page, true);
        }

        /// <summary>Goes back one page; from the overview, leaves the part.</summary>
        internal void Back()
        {
            if (_pages.Count <= 1)
            {
                BackRequested?.Invoke();
                return;
            }

            Pop();
        }

        internal void Pop()
        {
            if (_pages.Count <= 1)
            {
                return;
            }

            Media.Stop();
            PackPage leaving = Top;
            _pages.RemoveAt(_pages.Count - 1);
            leaving.OnHidden();
            PackPage showing = Top;
            showing.Root.gameObject.SetActive(true);
            showing.OnShown();
            RefreshDemo();
            Slide(leaving, showing, false, () => leaving.Dispose());
        }

        /// <summary>Drops pages above the overview at once, for a pack that is gone.</summary>
        internal void PopToRoot()
        {
            while (_pages.Count > 1)
            {
                PackPage page = Top;
                _pages.RemoveAt(_pages.Count - 1);
                page.OnHidden();
                page.Dispose();
            }

            Top?.Root.gameObject.SetActive(true);
            if (Top != null)
            {
                Top.Group.alpha = 1f;
                Top.Root.anchoredPosition = Vector2.zero;
                Top.OnShown();
            }

            RefreshDemo();
        }

        /// <summary>Open the resource group list. A null revision identifies the built-in bundle.</summary>
        internal void OpenPack(InstalledMaterialPack pack)
        {
            UiSound.Play(UiCue.Forward);
            Push(new PackDetailPage(this, pack));
        }

        internal void OpenCategory(string category)
        {
            if (State == null) return;
            UiSound.Play(UiCue.Forward);
            Push(new PackCategoryPage(this, category));
        }

        internal void OpenSettings()
        {
            if (_settings == null) _settings = LibrarySettingsPanel.Create(ModalLayer, null);
            _settings.transform.SetAsLastSibling();
            _settings.Show();
        }

        internal void ShowToast(string message) => _toast.Show(message, 2.8f);

        // ---------------------------------------------------------------- state

        /// <summary>Reads the configuration and the installed packs again and redraws every page.</summary>
        internal void Reload(bool notify = true)
        {
            try
            {
                Store ??= MaterialPackStore.Shared;
                State = Store.List();
                LoadError = null;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                Debug.LogWarning("[PackScreen] " + e);
                State = null;
                LoadError = PackImport.DescribeOpen(e);
            }

            if (!notify) return;
            foreach (PackPage page in _pages.ToArray()) page.OnStateChanged();
            RefreshDemo();
        }

        /// <summary>Runs the demo for the top page's choice, or stops it when the page has none.</summary>
        internal void RefreshDemo()
        {
            if (Demo == null) return;
            MaterialPackConfiguration configuration = _presented && State != null ? Top?.DemoConfiguration : null;
            if (configuration == null) Demo.Stop();
            else Demo.Show(configuration);
        }

        /// <summary>The 混搭 configuration, for an edit to start from.</summary>
        internal MaterialPackConfiguration BeginEdit() => State?.MixConfiguration() ?? new MaterialPackConfiguration();

        /// <summary>
        /// Saves a 混搭 change in one commit. Choosing what is already chosen saves nothing. On
        /// failure the last valid configuration stays and the page shows it.
        /// </summary>
        internal bool Save(MaterialPackConfiguration next, string success = null)
        {
            if (!CanWrite()) return false;
            if (!State.IsPresetMode && next.SameChoicesAs(State.MixConfiguration()))
            {
                return false;
            }

            try
            {
                Store.SaveConfiguration(next, State.Revision);
                Reload();
                ShowToast(success ?? "已保存，下一次开局生效");
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        /// <summary>Saves the built-in bundle's sets, which every bundle and 混搭 fall back to.</summary>
        internal bool SaveBuiltinBundle(MaterialBuiltinSelection sets, long revision)
        {
            if (!CanWrite()) return false;
            try
            {
                Store.SaveBuiltinBundle(sets, revision);
                Reload();
                UiSound.Play(UiCue.Select);
                ShowToast("已保存内置整合包，下一次开局生效");
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        /// <summary>Chooses the main group a bundle's preset uses for one category; the mode stays.</summary>
        internal bool SaveBundleGroup(InstalledMaterialPack pack, string category, string groupId)
        {
            if (pack == null || !CanWrite()) return false;
            try
            {
                long before = State.Revision;
                if (Store.SaveBundleGroup(pack.PackId, pack.Digest, category, groupId, before) == before) return false;
                Reload();
                UiSound.Play(UiCue.Select);
                bool used = State != null && State.IsPresetMode && State.Preset.IsFrom(pack.PackId, pack.Digest);
                ShowToast(used ? "已保存整合包设置，下一次开局生效"
                    : State?.IsPresetMode == false ? "已保存整合包设置，点整合包可载入混搭"
                    : "已保存整合包设置，使用此整合包时生效");
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        /// <summary>Load a whole bundle into the current mode. In mix mode it becomes an editable copy.</summary>
        internal bool UseBundle(MaterialPresetChoice choice)
        {
            if (choice == null || !CanWrite()) return false;
            try
            {
                if (!State.IsPresetMode)
                {
                    MaterialPackConfiguration candidate = State.BundleConfiguration(choice);
                    return candidate.SameChoicesAs(State.MixConfiguration()) || Save(candidate, "已套用整合包的配置，可继续调整各类别");
                }
                if (State.Preset.SameAs(choice)) return true;
                Store.ChoosePreset(choice, State.Revision);
                Reload();
                UiSound.Play(UiCue.Success);
                ShowToast("已使用预设「" + PresetName(choice) + "」，下一次开局生效");
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        /// <summary>Switches between 预设 and 混搭; each keeps its own choices.</summary>
        internal bool SetMode(string mode)
        {
            if (!CanWrite()) return false;
            if (State.Mode == mode) return false;
            try
            {
                Store.SetMode(mode, State.Revision);
                Reload();
                UiSound.Play(UiCue.Select);
                ShowToast(mode == MaterialPackModes.MIX
                    ? "已切换到混搭：按类别选择资源组，下一次开局生效"
                    : "已切换到预设「" + PresetName(State.Preset) + "」，下一次开局生效");
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        /// <summary>Removes one installed revision; the pages fall back to the overview if they showed it.</summary>
        internal bool Uninstall(InstalledMaterialPack pack)
        {
            if (pack == null || !CanWrite()) return false;
            try
            {
                Store.Uninstall(pack.PackId, pack.Digest, State.Revision, out MaterialUninstallImpact impact);
                Reload();
                UiSound.Play(UiCue.Delete);
                ShowToast("已卸载 " + PackWidgets.PackName(pack) + (impact.ClearedPreset != null ? "，预设改为" + PresetName(State?.Preset) : string.Empty));
                return true;
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                UiSound.Play(UiCue.Error);
                Reload();
                ShowToast(PackImport.DescribeWrite(e));
                return false;
            }
        }

        private bool CanWrite()
        {
            if (Store == null || State == null)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast(LoadError ?? "资源包尚未就绪。");
                return false;
            }

            if (IsImporting)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("正在导入资源包，请稍候。");
                return false;
            }

            return true;
        }

        private static bool IsWriteFailure(Exception e) =>
            e is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException;

        // ---------------------------------------------------------------- import

        /// <summary>Picks a ZIP and installs it; a second tap while picking or importing does nothing.</summary>
        internal void Import()
        {
            if (IsImporting) return;
            if (Store == null)
            {
                Reload();
                if (Store == null)
                {
                    ShowToast(LoadError ?? "资源包尚未就绪。");
                    return;
                }
            }

            if (Store.IsInSession)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("本局进行中，结束后再导入资源包。");
                return;
            }

            _picking = true;
            UiSound.Play(UiCue.Open);
            PackImport.PickZip(path =>
            {
                if (this == null) return;
                _picking = false;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                RunImport(path).Forget();
            });
        }

        private async UniTaskVoid RunImport(string path)
        {
            var cancellation = new CancellationTokenSource();
            _importCancel = cancellation;
            var before = new HashSet<string>(State?.Installed.Select(p => p.PackId + "/" + p.Digest) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            _importPanel.Show(Path.GetFileName(path), () => cancellation.Cancel());
            try
            {
                InstalledMaterialPack pack = await Store.ImportAsync(path, cancellation.Token);
                if (this == null) return;
                bool duplicate = before.Contains(pack.PackId + "/" + pack.Digest);
                _importPanel.Hide();
                _importCancel = null;
                Reload();
                UiSound.Play(UiCue.Success);
                InstalledMaterialPack installed = State?.FindPack(pack.PackId, pack.Digest);
                string name = PackWidgets.PackName(installed ?? pack);
                bool sibling = !duplicate && State != null && State.Installed.Count(p => p.PackId == pack.PackId) > 1;
                ShowToast(duplicate ? "已安装过相同内容：" + name
                    : sibling ? "已导入 " + name + " 的新修订，当前选择仍使用原修订"
                    : "已导入 " + name + "，可在下方选用");
                if (installed != null && Top is PackOverviewPage) OpenPack(installed);
            }
            catch (Exception e) when (e is OperationCanceledException || IsWriteFailure(e) || e is ArgumentException)
            {
                if (this == null) return;
                UiSound.Play(e is OperationCanceledException ? UiCue.Cancel : UiCue.Error);
                ShowToast(PackImport.DescribeImport(e));
            }
            finally
            {
                if (_importCancel == cancellation) _importCancel = null;
                cancellation.Dispose();
                if (this != null) _importPanel.Hide();
            }
        }

        private async UniTaskVoid BackfillAsync()
        {
            if (Store == null || State == null || State.Installed.All(p => p.MetadataVersion >= InstalledMaterialPack.CURRENT_METADATA_VERSION)) return;
            _backfill?.Cancel();
            var cancellation = new CancellationTokenSource();
            _backfill = cancellation;
            try
            {
                int filled = await Store.BackfillMetadataAsync(cancellation.Token);
                if (this != null && filled > 0 && _presented) Reload();
            }
            catch (Exception e) when (e is OperationCanceledException || IsWriteFailure(e))
            {
                // Packs keep showing their IDs; the detail page suggests importing them again.
            }
            finally
            {
                if (_backfill == cancellation) _backfill = null;
                cancellation.Dispose();
            }
        }

        // ---------------------------------------------------------------- descriptions

        /// <summary>"组名 · 包名", or the raw reference when it no longer resolves.</summary>
        internal string DescribeGroup(string reference)
        {
            if (reference == null) return "内置";
            if (State != null && State.TryFindGroup(reference, out InstalledMaterialPack pack, out MaterialPackGroup group))
                return PackWidgets.GroupName(group) + " · " + PackWidgets.PackName(pack);
            return reference;
        }

        /// <summary>What the next live uses: the chosen preset's name, or 混搭.</summary>
        internal string ActiveName => State == null ? string.Empty : State.IsPresetMode ? PresetName(State.Preset) : "混搭";

        /// <summary>A preset's name: the pack's preset name, or 游戏内置 with the set.</summary>
        internal string PresetName(MaterialPresetChoice choice)
        {
            if (choice == null || choice.IsBuiltin) return BUILTIN_BUNDLE_NAME;
            InstalledMaterialPack pack = State?.FindPack(choice.PackId, choice.Digest);
            return pack == null ? choice.PackId : pack.Preset?.Name ?? PackWidgets.PackName(pack);
        }

        internal const string BUILTIN_BUNDLE_NAME = "游戏内置整合包";

        // ---------------------------------------------------------------- building

        private void Build()
        {
            Media = new PackMedia(gameObject);
            _camera = CreateCamera();
            _camera.transform.SetParent(transform, false);
            _canvasRoot = new GameObject("PackCanvas", typeof(RectTransform));
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
            // The same aurora as the home page, in the 资源包 card's rose and violet.
            AddGlow(root, "GlowRose", HomeTheme.Colors.Rose, 0.20f, new Vector2(1f, 1f), new Vector2(-360f, -300f), 1800f);
            AddGlow(root, "GlowViolet", HomeTheme.Colors.Violet, 0.16f, new Vector2(0f, 0f), new Vector2(240f, 120f), 1500f);
            AddGlow(root, "GlowPeriwinkle", HomeTheme.Colors.Periwinkle, 0.08f, new Vector2(0.35f, 1f), new Vector2(0f, 80f), 1100f);

            PageHost = LibraryUi.CreateRect("Pages", root);
            LibraryUi.Stretch(PageHost);
            _pagesGroup = PageHost.gameObject.AddComponent<CanvasGroup>();
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            Preview = new PackPreviewPanel(this);
            Demo = new PackDemo(transform, () => Store);
            _importPanel = new PackImportPanel(ModalLayer);
            _toast = new UiToast(root, 48f, HomeTheme.Colors.Rose);

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

        private void Update()
        {
            if (_presented) Media.Tick();
        }

        private void Slide(PackPage from, PackPage to, bool forward, Action onDone = null)
        {
            _pushMotion.TryCancel();
            _pagesGroup.blocksRaycasts = false;
            float direction = forward ? 1f : -1f;
            _pushMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD + 0.06f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(Finish)
                .WithOnCancel(Finish)
                .Bind(t =>
                {
                    float fade = Mathf.Clamp01(t * 1.6f);
                    from.Group.alpha = 1f - fade;
                    from.Root.anchoredPosition = new Vector2(-direction * PUSH_OFFSET * 0.5f * t, 0f);
                    to.Group.alpha = t;
                    to.Root.anchoredPosition = new Vector2(direction * PUSH_OFFSET * (1f - t), 0f);
                });

            void Finish()
            {
                _pagesGroup.blocksRaycasts = true;
                if (from.Root != null)
                {
                    from.Root.anchoredPosition = Vector2.zero;
                    from.Group.alpha = 1f;
                    from.Root.gameObject.SetActive(false);
                }

                to.Group.alpha = 1f;
                to.Root.anchoredPosition = Vector2.zero;
                onDone?.Invoke();
            }
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("PackScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 103f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            _curtainMotion.TryCancel();
            _pushMotion.TryCancel();
            _importCancel?.Cancel();
            _backfill?.Cancel();
            _toast?.Dispose();
            for (int i = _pages.Count - 1; i >= 0; i--)
            {
                _pages[i].Dispose();
            }

            _pages.Clear();
            Preview?.Dispose();
            Demo?.Dispose();
            _importPanel?.Dispose();
            Media?.Dispose();
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
            }
        }
    }

    /// <summary>One page of the 资源包 part: a full-screen rect the screen slides in and out.</summary>
    internal abstract class PackPage : IDisposable
    {
        private bool _disposed;

        protected PackPage(PackScreen screen, string name)
        {
            Screen = screen;
            Root = LibraryUi.CreateRect(name, screen.PageHost);
            LibraryUi.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
        }

        protected PackScreen Screen { get; }
        internal RectTransform Root { get; }
        internal CanvasGroup Group { get; }

        /// <summary>The page became the top page.</summary>
        internal virtual void OnShown()
        {
        }

        /// <summary>Another page covered it, or it is leaving.</summary>
        internal virtual void OnHidden()
        {
        }

        /// <summary>The configuration or the installed packs changed; redraw from <see cref="PackScreen.State"/>.</summary>
        internal virtual void OnStateChanged()
        {
        }

        /// <summary>What the demo plays while this page is on top; null for no demo.</summary>
        internal virtual MaterialPackConfiguration DemoConfiguration => null;

        /// <summary>The page's content cascades in, as the part rises.</summary>
        internal virtual void PlayEnter()
        {
        }

        /// <summary>Closes something open on the page for the back key; false when nothing was.</summary>
        internal virtual bool TryHandleBack() => false;

        protected bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OnDispose();
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
            }
        }

        protected virtual void OnDispose()
        {
        }
    }
}
