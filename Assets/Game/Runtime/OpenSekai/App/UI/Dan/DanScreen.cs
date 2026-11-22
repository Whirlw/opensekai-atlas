using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Remembers which course the 段位 page started, so the library scene that loads when the
    /// course ends opens the page on its result, once.
    /// </summary>
    internal static class DanResultHandoff
    {
        private static string _definitionId;

        internal static void Await(string definitionId) => _definitionId = definitionId;

        internal static void Forget() => _definitionId = null;

        /// <summary>True (and forgotten) when <paramref name="service"/> just ended the course the page started.</summary>
        internal static bool TakeFinished(DanChallengeService service)
        {
            if (_definitionId == null || service?.Definition == null || service.Attempt == null) return false;
            if (service.Phase != DanPhase.Finished && service.Phase != DanPhase.Error) return false;
            if (!string.Equals(service.Definition.Id, _definitionId, StringComparison.Ordinal)) return false;
            _definitionId = null;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _definitionId = null;
    }

    /// <summary>
    /// The 段位 part: the player's saved courses on the left, the chosen one on the right (its plate,
    /// songs, damage table and recent attempts), with 开始挑战, 复制分享, 编辑 and 佩戴. 新建段位 opens
    /// the maker, 粘贴段位 reads a shared text. Starting checks every song first, then hands the first
    /// one to the live scene; the course overlay carries the rest, and the library scene that loads
    /// afterwards opens this page on the result. Like the other parts it builds its own camera and
    /// canvas in the Library scene. Every read and write goes through <see cref="PlayerProfileStore"/>
    /// and <see cref="DanChallengeService"/> on the main thread.
    /// </summary>
    public sealed class DanScreen : MonoBehaviour
    {
        private readonly List<DanRecord> _records = new List<DanRecord>();
        private readonly Dictionary<string, DanSongInfo> _songs = new Dictionary<string, DanSongInfo>(StringComparer.Ordinal);
        private PlayerProfileStore _store;
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private Image _glow;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private UiToast _toast;
        private LibrarySettingsPanel _settings;
        private DanEditorPanel _editor;
        private DanSharePanel _share;
        private DanPreparePanel _prepare;
        private DanDeletePanel _delete;
        private DanResultPage _result;
        private CancellationTokenSource _starting;
        private DanDefinition _startingDefinition;
        private bool _presented;

        /// <summary>The back button (and back with nothing open): leave the part.</summary>
        internal event Action BackRequested;

        internal bool IsPresented => _presented;

        /// <summary>True from 开始挑战 until the first song is handed over, or the start fails or is cancelled.</summary>
        internal bool IsStarting => _starting != null;

        /// <summary>The store the page reads and edits; the local profile unless a tool swapped in another.</summary>
        internal PlayerProfileStore Store
        {
            get => _store ?? PlayerProfileStore.Local;
            set => _store = value;
        }

        internal DanView View { get; private set; }

        internal RectTransform ModalLayer { get; private set; }

        internal IReadOnlyList<DanRecord> Records => _records;

        /// <summary>The course on the right; null when there are none.</summary>
        internal DanRecord Selected { get; private set; }

        internal string LoadError { get; private set; }

        internal bool IsResultOpen => _result.IsOpen;

        internal DanResultPage Result => _result;

        internal DanEditorPanel Editor => _editor;

        internal DanSharePanel Share => _share;

        internal DanPreparePanel Prepare => _prepare;

        internal static DanScreen Create()
        {
            var host = new GameObject("DanScreen");
            DanScreen screen = host.AddComponent<DanScreen>();
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
                _starting?.Cancel();
                _settings?.Hide();
                _editor.Close();
                _share.Close();
                _delete.Close();
                if (!IsStarting) _prepare.Hide();
                _result.Hide();
                _toast.Hide();
            }

            _canvasRoot.SetActive(presented);
            if (!presented) return;
            Reload();
            if (!animate) return;
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

        /// <summary>The Android back key: cancels a start, closes what is open. False with nothing open.</summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive()) return true;
            if (_starting != null)
            {
                _starting.Cancel();
                return true;
            }

            if (_prepare.IsOpen)
            {
                _prepare.Hide();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_editor.IsOpen)
            {
                _editor.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_share.IsOpen)
            {
                _share.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_delete.IsOpen)
            {
                _delete.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return true;
            }

            if (_result.IsOpen)
            {
                if (!_result.TryHandleBack()) CloseResult();
                return true;
            }

            return false;
        }

        internal void Back()
        {
            if (_result.IsOpen)
            {
                CloseResult();
                return;
            }

            BackRequested?.Invoke();
        }

        internal void OpenSettings()
        {
            if (_settings == null) _settings = LibrarySettingsPanel.Create(ModalLayer, null);
            _settings.transform.SetAsLastSibling();
            _settings.Show();
        }

        internal void ShowToast(string message) => _toast.Show(message, 2.8f);

        // ---------------------------------------------------------------- data

        internal void Reload()
        {
            string selected = Selected?.Definition.Id;
            _records.Clear();
            _songs.Clear();
            try
            {
                IReadOnlyList<DanDefinition> definitions = Store.ReadDanDefinitions();
                IReadOnlyList<DanAttempt> history = Store.ReadDanHistory();
                PlayerProfileSnapshot snapshot = Store.Read();
                var earned = new HashSet<string>(snapshot.EarnedDanIds, StringComparer.Ordinal);
                var attempts = new Dictionary<string, List<DanAttempt>>(StringComparer.Ordinal);
                foreach (DanAttempt attempt in history)
                {
                    if (!attempts.TryGetValue(attempt.DefinitionId, out List<DanAttempt> list))
                    {
                        attempts[attempt.DefinitionId] = list = new List<DanAttempt>();
                    }

                    list.Add(attempt);
                }

                // Newest saved first: the store keeps the order courses were saved in.
                for (int i = definitions.Count - 1; i >= 0; i--)
                {
                    DanDefinition definition = definitions[i];
                    attempts.TryGetValue(definition.Id, out List<DanAttempt> list);
                    _records.Add(new DanRecord(definition, (IReadOnlyList<DanAttempt>)list ?? Array.Empty<DanAttempt>(),
                        earned.Contains(definition.Id), string.Equals(snapshot.EquippedDanId, definition.Id, StringComparison.Ordinal)));
                }

                LoadError = null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Dan] Could not read the courses: " + exception);
                LoadError = exception is InvalidDataException ? "段位数据无法识别，请检查运行日志" : "段位读取失败，请稍后重试";
            }

            Selected = Find(selected) ?? (_records.Count > 0 ? _records[0] : null);
            UpdateGlow();
            View.Bind();
        }

        internal void Select(DanRecord record)
        {
            if (record == null || record == Selected) return;
            Selected = record;
            UiSound.Play(UiCue.Select);
            UpdateGlow();
            View.BindSelection();
        }

        internal void Select(string definitionId)
        {
            DanRecord record = Find(definitionId);
            if (record == null) return;
            Selected = record;
            UpdateGlow();
            View.Bind();
        }

        /// <summary>The songs of <paramref name="record"/>'s course, each with what the device and its attempts know.</summary>
        internal IReadOnlyList<DanSongInfo> SongsOf(DanRecord record)
        {
            var songs = new List<DanSongInfo>();
            if (record == null) return songs;
            IReadOnlyList<string> links = record.Definition.Songs;
            for (int i = 0; i < links.Count; i++)
            {
                if (!_songs.TryGetValue(links[i], out DanSongInfo info))
                {
                    info = DanSongInfo.Resolve(links[i], SavedTitle(record, i));
                    _songs[links[i]] = info;
                }

                songs.Add(info);
            }

            return songs;
        }

        private static string SavedTitle(DanRecord record, int index)
        {
            foreach (DanAttempt attempt in record.Attempts)
            {
                if (index < attempt.Songs.Count) return attempt.Songs[index].Title;
            }

            return null;
        }

        private DanRecord Find(string id)
        {
            if (id == null) return null;
            foreach (DanRecord record in _records)
            {
                if (string.Equals(record.Definition.Id, id, StringComparison.Ordinal)) return record;
            }

            return null;
        }

        private void UpdateGlow()
        {
            Color32 tint = Selected != null ? DanStyles.ParseColor(Selected.Definition.Color) : HomeTheme.Colors.Violet;
            _glow.color = HomeTheme.Colors.WithAlpha(tint, 40);
        }

        // ---------------------------------------------------------------- making and sharing

        internal void OpenCreate()
        {
            if (IsStarting) return;
            _editor.Open(null);
        }

        internal void OpenEdit()
        {
            if (IsStarting || Selected == null) return;
            _editor.Open(Selected.Definition);
        }

        internal void OpenPaste(string text = null)
        {
            if (IsStarting) return;
            _share.Open(text);
        }

        internal void CopyShare()
        {
            if (Selected == null) return;
            GUIUtility.systemCopyBuffer = Selected.Definition.ShareText;
            UiSound.Play(UiCue.Success);
            ShowToast("已复制分享文本。好友在「段位 → 粘贴段位」里打开，就能挑战同一个段位");
        }

        private bool SaveEdited(DanDefinition original, DanDefinition definition)
        {
            if (!Save(definition)) return false;
            UiSound.Play(UiCue.Success);
            ShowToast(original == null ? "已保存「" + definition.Label + "」"
                : "已另存为新的段位，原段位与它的记录都保留");
            return true;
        }

        private void SaveShared(DanDefinition definition)
        {
            if (!Save(definition)) return;
            UiSound.Play(UiCue.Success);
            ShowToast("已保存「" + definition.Label + "」，可以开始挑战了");
        }

        private void ShowShared(DanDefinition definition)
        {
            Select(definition.Id);
            ShowToast("这个段位已在本机");
        }

        private bool Save(DanDefinition definition)
        {
            try
            {
                Store.SaveDanDefinition(definition);
            }
            catch (InvalidOperationException)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("备份正在进行，完成后再保存段位");
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Dan] Could not save a course: " + exception.Message);
                UiSound.Play(UiCue.Error);
                ShowToast("保存失败：文件无法写入");
                return false;
            }

            Reload();
            Select(definition.Id);
            return true;
        }

        private bool IsSaved(DanDefinition definition) => Find(definition.Id) != null;

        // ---------------------------------------------------------------- deleting

        /// <summary>删除 on the chosen course: asks first, saying what goes with it.</summary>
        internal void RequestDelete()
        {
            DanRecord record = Selected;
            if (record == null) return;
            DanChallengeService service = DanChallengeService.Local;
            bool running = service.Phase == DanPhase.Preparing || service.Phase == DanPhase.StartingSong
                || service.Phase == DanPhase.Playing || service.Phase == DanPhase.BetweenSongs;
            if (IsStarting || running && string.Equals(service.Definition?.Id, record.Definition.Id, StringComparison.Ordinal))
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("这个段位正在挑战，结束后再删除");
                return;
            }

            _delete.Show(record, () => DeleteCourse(record.Definition));
        }

        private void DeleteCourse(DanDefinition definition)
        {
            int index = _records.FindIndex(r => string.Equals(r.Definition.Id, definition.Id, StringComparison.Ordinal));
            try
            {
                Store.DeleteDanDefinition(definition.Id);
            }
            catch (InvalidOperationException)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("备份正在进行，完成后再删除段位");
                return;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Dan] Could not delete a course: " + exception.Message);
                UiSound.Play(UiCue.Error);
                ShowToast("删除失败：文件无法写入");
                return;
            }

            Reload();
            // The course that took its place in the list, or the one above when it was the last.
            if (_records.Count > 0) Select(_records[Mathf.Clamp(index, 0, _records.Count - 1)].Definition.Id);
            UiSound.Play(UiCue.Delete);
            ShowToast("已删除「" + definition.Label + "」");
        }

        // ---------------------------------------------------------------- wearing

        /// <summary>佩戴 / 卸下 on the chosen course: wears its plate, or takes it off when it is worn.</summary>
        internal void ToggleEquip()
        {
            if (Selected == null) return;
            if (!Selected.Earned)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("通过这个段位后才能佩戴它的段位牌");
                return;
            }

            bool wearing = !Selected.Equipped;
            if (Equip(wearing ? Selected.Definition.Id : null))
            {
                UiSound.Play(wearing ? UiCue.Success : UiCue.ToggleOff);
                ShowToast(wearing ? "已佩戴「" + Selected.Definition.Label + "」" : "已卸下段位牌");
            }
        }

        private bool EquipFromResult(DanDefinition definition)
        {
            if (!Equip(definition.Id)) return false;
            ShowToast("已佩戴「" + definition.Label + "」，个人信息里会展示这块段位牌");
            return true;
        }

        private bool Equip(string id)
        {
            try
            {
                Store.EquipDan(id);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning("[Dan] Could not wear the plate: " + exception.Message);
                UiSound.Play(UiCue.Blocked);
                ShowToast("暂时无法佩戴，请稍后再试");
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Dan] Could not wear the plate: " + exception.Message);
                UiSound.Play(UiCue.Error);
                ShowToast("保存失败：文件无法写入");
                return false;
            }

            Reload();
            return true;
        }

        // ---------------------------------------------------------------- playing

        internal void StartSelected()
        {
            if (Selected != null) StartChallenge(Selected.Definition);
        }

        internal void StartChallenge(DanDefinition definition)
        {
            if (definition == null || IsStarting) return;
            DanChallengeService service = DanChallengeService.Local;
            if (service.Phase == DanPhase.Preparing || service.Phase == DanPhase.StartingSong
                || service.Phase == DanPhase.Playing || service.Phase == DanPhase.BetweenSongs)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("已有一场挑战正在进行");
                return;
            }

            UiSound.Play(UiCue.Start);
            _startingDefinition = definition;
            DanRecord record = Find(definition.Id);
            _prepare.Show(definition, record != null ? SongsOf(record) : SongsOf(new DanRecord(definition, Array.Empty<DanAttempt>(), false, false)));
            RunStartAsync(definition).Forget();
        }

        private void RetryStart()
        {
            if (_startingDefinition != null) StartChallenge(_startingDefinition);
        }

        private void CancelStart() => _starting?.Cancel();

        private async UniTaskVoid RunStartAsync(DanDefinition definition)
        {
            var cancellation = new CancellationTokenSource();
            _starting = cancellation;
            DanChallengeService service = DanChallengeService.Local;
            service.Changed += OnServiceChanged;
            // Before the start: the library scene can begin unloading as soon as the first song is handed over.
            DanCourseOverlay.Ensure();
            DanResultHandoff.Await(definition.Id);
            bool started = false;
            try
            {
                PlayStartResult result = await service.StartAsync(definition, cancellation.Token);
                started = result.Started;
                if (this == null || started) return;
                if (result.Status == PlayStartStatus.Cancelled)
                {
                    _prepare.Hide();
                    ShowToast("已取消挑战");
                    Reload();
                    return;
                }

                UiSound.Play(UiCue.Error);
                _prepare.Apply(DanPhase.Error, Mathf.Max(1, service.SongNumber), result.Message ?? service.Error);
                Reload();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                UiSound.Play(UiCue.Error);
                _prepare.Apply(DanPhase.Error, Mathf.Max(1, service.SongNumber), "开局失败，请检查运行日志");
            }
            finally
            {
                service.Changed -= OnServiceChanged;
                if (!started) DanResultHandoff.Forget();
                if (_starting == cancellation) _starting = null;
                cancellation.Dispose();
            }
        }

        private void OnServiceChanged()
        {
            DanChallengeService service = DanChallengeService.Local;
            if (this == null || !_prepare.IsOpen) return;
            if (service.Phase == DanPhase.Preparing || service.Phase == DanPhase.StartingSong || service.Phase == DanPhase.Playing)
            {
                _prepare.Apply(service.Phase, service.SongNumber, null);
            }
        }

        // ---------------------------------------------------------------- result

        /// <summary>Opens the result of the course <see cref="DanChallengeService.Local"/> just ended.</summary>
        internal void ShowCourseResult()
        {
            DanChallengeService service = DanChallengeService.Local;
            if (service.Definition == null || service.Attempt == null) return;
            Reload();
            Select(service.Definition.Id);
            DanRecord record = Find(service.Definition.Id);
            ShowResult(new DanResultModel(service.Definition, service.Attempt, service.NewlyAwarded, record?.Equipped == true,
                service.Phase == DanPhase.Error ? service.Error : null,
                SongsOf(record ?? new DanRecord(service.Definition, Array.Empty<DanAttempt>(), false, false))), true);
        }

        /// <param name="animate">False shows where the opening settles, for screenshots.</param>
        internal void ShowResult(DanResultModel model, bool animate)
        {
            _prepare.Hide();
            _result.Show(model, animate);
        }

        private void CloseResult()
        {
            _result.Hide();
            Reload();
            View.PlayEnter();
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            _camera = CreateCamera();
            _camera.transform.SetParent(transform, false);
            _canvasRoot = new GameObject("DanCanvas", typeof(RectTransform));
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
            _glow = AddGlow(root, "GlowPlate", HomeTheme.Colors.Violet, 0.16f, new Vector2(0f, 1f), new Vector2(380f, -300f), 1700f);
            AddGlow(root, "GlowViolet", HomeTheme.Colors.Violet, 0.1f, new Vector2(1f, 0f), new Vector2(-320f, 160f), 1600f);
            AddGlow(root, "GlowRose", HomeTheme.Colors.Rose, 0.05f, new Vector2(0.65f, 1f), new Vector2(0f, 60f), 1100f);

            View = new DanView(root, this);
            _result = new DanResultPage(root, CloseResult, StartChallenge, EquipFromResult);
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            _editor = new DanEditorPanel(ModalLayer, SaveEdited, ShowToast);
            _share = new DanSharePanel(ModalLayer, IsSaved, SaveShared, ShowShared);
            _delete = new DanDeletePanel(ModalLayer);
            _prepare = new DanPreparePanel(ModalLayer, CancelStart, RetryStart, () =>
            {
                _prepare.Hide();
                UiSound.Play(UiCue.Close);
            });
            _toast = new UiToast(root, 48f, HomeTheme.Colors.Violet);

            _curtain = LibraryUi.CreateImage("Curtain", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(_curtain.rectTransform);
            _curtain.raycastTarget = true;
            _curtain.gameObject.SetActive(false);

            _camera.enabled = false;
            _raycaster.enabled = false;
            _canvasRoot.SetActive(false);
        }

        private static Image AddGlow(RectTransform root, string name, Color32 tint, float alpha, Vector2 anchor, Vector2 position, float size)
        {
            Image glow = LibraryUi.CreateImage(name, root, new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, alpha));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            return glow;
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("DanScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 105f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            _curtainMotion.TryCancel();
            // A start that already handed the first song over keeps running; only a pending one is cancelled.
            _starting?.Cancel();
            _toast?.Dispose();
            View?.Dispose();
            _editor?.Dispose();
            _share?.Dispose();
            _prepare?.Dispose();
            _delete?.Dispose();
            _result?.Dispose();
            if (_camera != null) Destroy(_camera.gameObject);
        }
    }
}
