using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 个人信息 part: the player's local profile (avatar, nickname, Ranking Score and the two
    /// worn medals), the Best 30 and the whole play history, and the way to back the profile up to a
    /// ZIP and merge one back in. Like the server and resource pack parts it builds its own camera
    /// and canvas in the Library scene; the home page decides when it comes and goes. Every read and
    /// write goes through <see cref="PlayerProfileStore"/> on the main thread; the page only shows
    /// what the store returns.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        private const int HISTORY_PAGE = 50;
        private const string THUMBNAIL_DIRECTORY = "JacketThumbnails";

        private readonly Dictionary<int, IReadOnlyList<CustomMusicScorePlayHistoryRecord>> _historyPages =
            new Dictionary<int, IReadOnlyList<CustomMusicScorePlayHistoryRecord>>();
        private readonly Dictionary<string, (int Position, long Points)> _standings =
            new Dictionary<string, (int, long)>(StringComparer.Ordinal);
        private readonly Dictionary<string, SongPackageItem> _packages = new Dictionary<string, SongPackageItem>(StringComparer.Ordinal);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private Func<IReadOnlyList<SongPackageItem>> _loadPackages;
        private PlayerProfileStore _store;
        private RankingHistory _observedHistory;
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private Image _tierGlow;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private UiToast _toast;
        private AchievementUnlockPopup _unlock;
        private LibrarySettingsPanel _settings;
        private ProfileEditPanel _editor;
        private ProfileBusyPanel _busy;
        private ProfileImportPanel _import;
        private ProfileDeletePanel _deletion;
        private MedalPicker _medals;
        private RankingBadgeGallery _badges;
        private ProfileImportPreview _preview;
        private PlayerProfileStore _previewStore;
        private CancellationTokenSource _operation;
        private JacketThumbnails _thumbnails;
        private bool _presented;

        /// <summary>The back button (and back with nothing open): leave the part.</summary>
        internal event Action BackRequested;

        /// <summary>True when an import or deletion changed history and the library needs fresh counts.</summary>
        internal event Action<bool> DataChanged;

        internal bool IsPresented => _presented;

        /// <summary>True from starting an export or import until its picker and preview have returned.</summary>
        internal bool IsBusy => _operation != null;

        /// <summary>The store the page reads and edits; the local profile unless a tool swapped in another.</summary>
        internal PlayerProfileStore Store
        {
            get => _store ?? PlayerProfileStore.Local;
            set => _store = value;
        }

        internal ProfileView View { get; private set; }

        internal RectTransform ModalLayer { get; private set; }

        /// <summary>The profile as last read; null when reading failed (<see cref="LoadError"/> says why).</summary>
        internal PlayerProfileSnapshot Snapshot { get; private set; }

        internal string LoadError { get; private set; }

        /// <summary>Every medal the player owns, as last read.</summary>
        internal IReadOnlyList<Medal> Medals { get; private set; } = Array.Empty<Medal>();

        /// <summary>Runs in the whole history, as last read.</summary>
        internal int HistoryCount { get; private set; }
        internal bool HistoryBackedUp { get; private set; }

        internal bool IsEditorOpen => _editor.IsOpen;

        internal bool IsImportPreviewOpen => _import.IsOpen;

        /// <param name="loadPackages">The installed packages, for jackets; called each time the page comes up.</param>
        internal static ProfileScreen Create(Func<IReadOnlyList<SongPackageItem>> loadPackages)
        {
            var host = new GameObject("ProfileScreen");
            ProfileScreen screen = host.AddComponent<ProfileScreen>();
            screen._loadPackages = loadPackages;
            screen.Build();
            return screen;
        }

        // ---------------------------------------------------------------- presentation

        /// <summary>Brings the part on screen (reading the profile again) or takes it off.</summary>
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
                ObserveHistory(null);
                _operation?.Cancel();
                _settings?.Hide();
                _editor.Close();
                _deletion.Close();
                _medals.Close();
                _badges.Close();
                _busy.Hide();
                DiscardPreview();
                _toast.Hide();
                _unlock.Hide();
            }

            _canvasRoot.SetActive(presented);
            if (!presented)
            {
                return;
            }

            LoadPackages();
            Reload();
            _badges.Warm();
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
            View.PlayEnter();
        }

        /// <summary>Lowers the curtain over the page, then runs <paramref name="onCovered"/>.</summary>
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

        /// <summary>
        /// The Android back key: cancels a backup in progress, closes what is open (a merge preview
        /// is cancelled). False with nothing open, where back leaves the part.
        /// </summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive())
            {
                return true;
            }

            if (_operation != null)
            {
                _operation.Cancel();
                return true;
            }

            if (_import.IsOpen)
            {
                CancelPreview();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_deletion.IsOpen)
            {
                _deletion.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_editor.IsOpen)
            {
                _editor.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_medals.IsOpen)
            {
                _medals.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_badges.IsOpen)
            {
                _badges.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

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

        internal void ShowTab(ProfileTab tab) => View.ShowTab(tab, false);

        internal void ShowToast(string message) => _toast.Show(message, 2.8f);

        internal bool IsMedalPickerOpen => _medals.IsOpen;

        internal bool IsBadgeGalleryOpen => _badges.IsOpen;

        internal RankingBadgeGallery BadgeGallery => _badges;

        /// <summary>
        /// The badge on the player's card: every Ranking Score tier's badge, with the player's own
        /// marked. It only reads, so it opens even mid-backup or with the profile unreadable.
        /// </summary>
        internal void OpenBadges()
        {
            Best30Snapshot best = Snapshot?.Best30;
            _badges.Open(best != null ? best.Total : (long?)null);
        }

        /// <summary>A medal slot on the player's card: choose which medal to wear in it, or none.</summary>
        internal void OpenMedals(int slot)
        {
            if (IsBusy || Snapshot == null)
            {
                UiSound.Play(UiCue.Blocked);
                if (Snapshot == null) ShowToast(LoadError ?? "档案暂时无法读取");
                return;
            }

            if (Medals.Count == 0)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("通过段位挑战或解锁成就后，就能在这里佩戴奖牌");
                return;
            }

            _medals.Open(Medals, Snapshot.WornMedals, slot);
        }

        private bool WearMedal(int slot, string key)
        {
            if (string.Equals(key, Snapshot?.WornMedals[slot]?.Key, StringComparison.Ordinal)) return true;
            try
            {
                Store.WearMedal(slot, key);
            }
            catch (InvalidOperationException)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("备份正在进行，完成后再更换奖牌");
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Profile] Could not wear the medal: " + exception.Message);
                UiSound.Play(UiCue.Error);
                ShowToast("保存失败：文件无法写入");
                return false;
            }

            Reload(true);
            UiSound.Play(key != null ? UiCue.Success : UiCue.ToggleOff);
            Medal worn = Snapshot?.WornMedals[slot];
            ShowToast(worn != null ? "已佩戴「" + MedalCopy.Name(worn) + "」" : "已卸下奖牌");
            DataChanged?.Invoke(false);
            return true;
        }

        // ---------------------------------------------------------------- data

        /// <summary>Reads the profile, the history size and the Best 30 standings again and redraws the page.</summary>
        internal void Reload() => Reload(false);

        private void Reload(bool keepScroll)
        {
            _historyPages.Clear();
            _standings.Clear();
            try
            {
                Snapshot = Store.Read();
                Medals = Store.ReadMedals();
                ObserveHistory(_presented ? Store.History : null);
                HistoryCount = Store.ReadHistory(0, 1).TotalCount;
                HistoryBackedUp = Store.HasCurrentBackup;
                IReadOnlyList<Best30Entry> entries = Snapshot.Best30.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    string id = entries[i].Play?.RecordId;
                    if (!string.IsNullOrEmpty(id)) _standings[id] = (i + 1, entries[i].Points);
                }

                LoadError = null;
            }
            catch (Exception exception)
            {
                // A profile that cannot be read still opens the page, which says so and offers to retry.
                Debug.LogWarning("[Profile] Could not read the player profile: " + exception);
                Snapshot = null;
                Medals = Array.Empty<Medal>();
                HistoryCount = 0;
                HistoryBackedUp = false;
                LoadError = exception is InvalidDataException ? "档案数据无法识别，请检查运行日志" : "档案读取失败，请稍后重试";
            }

            UpdateGlow();
            View.Bind(keepScroll);
        }

        private void ObserveHistory(RankingHistory history)
        {
            if (_observedHistory == history) return;
            if (_observedHistory != null) _observedHistory.Appended -= OnHistoryAppended;
            _observedHistory = history;
            if (history != null) history.Appended += OnHistoryAppended;
        }

        private void OnHistoryAppended()
        {
            // A new run changes what the open card promises (the backup no longer covers everything).
            _deletion.Close();
            Reload(true);
        }

        /// <summary>清空历史: confirms with a backup of the current history, or asks for one first.</summary>
        internal void RequestClearHistory()
        {
            if (IsBusy || HistoryCount == 0)
            {
                UiSound.Play(UiCue.Blocked);
                return;
            }

            _deletion.ShowClear(HistoryCount, HistoryBackedUp, () => ApplyDeletion(null), ExportThenClear);
        }

        /// <summary>A long press on a history row: confirms before that one run is deleted.</summary>
        internal void RequestDeleteHistory(CustomMusicScorePlayHistoryRecord play)
        {
            if (play == null || IsBusy)
            {
                return;
            }

            TryStanding(play.RecordId, out int position, out _);
            _deletion.ShowRun(play, position, HistoryBackedUp, () => ApplyDeletion(play));
        }

        private void ApplyDeletion(CustomMusicScorePlayHistoryRecord play)
        {
            if (IsBusy) return;
            try
            {
                if (play == null) Store.DeleteAllHistory();
                else Store.DeleteHistory(play);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is InvalidDataException)
            {
                Debug.LogWarning("[Profile] Could not delete history: " + e.Message);
                Reload(true);
                UiSound.Play(UiCue.Error);
                ShowToast(play == null && !HistoryBackedUp ? "历史有了新记录，请重新导出备份后再清空" : "删除失败，请重试");
                return;
            }
            Reload(true);
            DataChanged?.Invoke(true);
            UiSound.Play(UiCue.Delete);
            ShowToast(play == null ? "已清空全部游玩记录" : "已删除这条游玩记录");
        }

        /// <summary>The run at <paramref name="index"/> of the history, newest first; null when it cannot be read.</summary>
        internal CustomMusicScorePlayHistoryRecord HistoryAt(int index)
        {
            if (index < 0 || index >= HistoryCount)
            {
                return null;
            }

            int page = index / HISTORY_PAGE;
            if (!_historyPages.TryGetValue(page, out IReadOnlyList<CustomMusicScorePlayHistoryRecord> records))
            {
                try
                {
                    records = Store.ReadHistory(page * HISTORY_PAGE, HISTORY_PAGE).Records;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                    || exception is InvalidDataException || exception is InvalidOperationException)
                {
                    Debug.LogWarning("[Profile] Could not read history page " + page + ": " + exception.Message);
                    return null;
                }

                _historyPages[page] = records;
            }

            int offset = index - page * HISTORY_PAGE;
            return offset < records.Count ? records[offset] : null;
        }

        /// <summary>Where a run stands in the Best 30, when it is the one its chart counts.</summary>
        internal bool TryStanding(string recordId, out int position, out long points)
        {
            if (!string.IsNullOrEmpty(recordId) && _standings.TryGetValue(recordId, out (int Position, long Points) standing))
            {
                position = standing.Position;
                points = standing.Points;
                return true;
            }

            position = 0;
            points = 0L;
            return false;
        }

        /// <summary>Puts the jacket of the installed package with this chart on <paramref name="row"/>, now or once it loads.</summary>
        internal void RequestJacket(string scoreKey, ProfileRow row)
        {
            row.SetJacket(null);
            if (string.IsNullOrEmpty(scoreKey) || !_packages.TryGetValue(scoreKey, out SongPackageItem item)
                || item?.Entry?.Manifest == null || !item.HasJacket)
            {
                return;
            }

            string path = item.Entry.JacketPath;
            long version = item.LastWriteTime.Ticks;
            if (_thumbnails.TryGetLoaded(path, version, out JacketThumbnail loaded))
            {
                row.SetJacket(loaded.Sprite);
                return;
            }

            LoadJacketAsync(row, row.Version, path, version).Forget();
        }

        private async UniTaskVoid LoadJacketAsync(ProfileRow row, int version, string path, long stamp)
        {
            JacketThumbnail thumbnail;
            try
            {
                thumbnail = await _thumbnails.LoadAsync(path, stamp, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (this != null && row.Version == version)
            {
                row.SetJacket(thumbnail.Sprite);
            }
        }

        private void LoadPackages()
        {
            _packages.Clear();
            try
            {
                IReadOnlyList<SongPackageItem> items = _loadPackages?.Invoke();
                if (items == null) return;
                foreach (SongPackageItem item in items)
                {
                    if (!string.IsNullOrEmpty(item?.ScoreKey) && !_packages.ContainsKey(item.ScoreKey))
                    {
                        _packages[item.ScoreKey] = item;
                    }
                }
            }
            catch (Exception exception)
            {
                // Jackets are a nicety: without the library the rows keep their placeholders.
                Debug.LogWarning("[Profile] Could not read the library for jackets: " + exception.Message);
            }
        }

        private void UpdateGlow()
        {
            Color32 tier = RankingTiers.Color(Snapshot?.Best30.TierId ?? 0);
            _tierGlow.color = new Color(tier.r / 255f, tier.g / 255f, tier.b / 255f, 0.16f);
        }

        // ---------------------------------------------------------------- editing

        internal void OpenEditor()
        {
            if (IsBusy || Snapshot == null)
            {
                UiSound.Play(UiCue.Blocked);
                if (Snapshot == null) ShowToast(LoadError ?? "档案暂时无法读取");
                return;
            }

            _editor.Open(Snapshot.Appearance, Store.ReadAchievements(), Store.OwnsAvatar);
        }

        private bool SaveAppearance(string nickname, string avatarId)
        {
            ProfileAppearance current = Snapshot?.Appearance;
            if (current != null && current.Nickname == nickname && current.AvatarId == avatarId)
            {
                return true;
            }

            try
            {
                Store.SetAppearance(nickname, avatarId);
            }
            catch (ArgumentException)
            {
                UiSound.Play(UiCue.Error);
                ShowToast("昵称不能为空，也不能包含控制字符");
                return false;
            }
            catch (InvalidOperationException)
            {
                UiSound.Play(UiCue.Blocked);
                ShowToast("备份正在进行，完成后再保存资料");
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Profile] Could not save the profile: " + exception.Message);
                UiSound.Play(UiCue.Error);
                ShowToast("保存失败：文件无法写入");
                return false;
            }

            UiSound.Play(UiCue.Success);
            Reload();
            ShowToast("资料已保存");
            DataChanged?.Invoke(false);
            return true;
        }

        // ---------------------------------------------------------------- backup

        /// <summary>Writes the profile to a ZIP and hands it to the system's save dialog.</summary>
        internal void Export()
        {
            if (IsBusy)
            {
                return;
            }

            UiSound.Play(UiCue.Open);
            RunExportAsync(false).Forget();
        }

        /// <summary>导出备份 on the card that asked for one: back to the clear confirmation once it is saved.</summary>
        private void ExportThenClear()
        {
            if (IsBusy)
            {
                return;
            }

            UiSound.Play(UiCue.Open);
            RunExportAsync(true).Forget();
        }

        /// <summary>Lets the player pick a backup ZIP, checks it and shows what merging it would do.</summary>
        internal void Import()
        {
            if (IsBusy)
            {
                return;
            }

            UiSound.Play(UiCue.Open);
            RunImportAsync().Forget();
        }

        /// <summary>Shows a backup's preview from <paramref name="store"/>; confirming commits exactly it.</summary>
        internal void ShowImportPreview(ProfileImportPreview preview, PlayerProfileStore store)
        {
            if (preview == null)
            {
                return;
            }

            DiscardPreview();
            _preview = preview;
            _previewStore = store;
            _import.Show(preview, Snapshot, ConfirmPreview, CancelPreview);
        }

        private async UniTaskVoid RunExportAsync(bool thenClear)
        {
            var cancellation = new CancellationTokenSource();
            _operation = cancellation;
            _busy.Show("正在导出…", "打包头像、昵称与全部游玩记录", "准备完成后会打开系统的保存位置", () => cancellation.Cancel());
            try
            {
                bool saved = await ProfileBackupPicker.ExportAsync(cancellation.Token, Store);
                if (this == null) return;
                _busy.Hide();
                if (saved)
                {
                    Reload(true);
                    UiSound.Play(UiCue.Success);
                    ShowToast("个人档案已导出");
                    if (thenClear && _presented && HistoryBackedUp && HistoryCount > 0)
                    {
                        _deletion.ShowClear(HistoryCount, true, () => ApplyDeletion(null), ExportThenClear);
                    }
                }
                else
                {
                    ShowToast("已取消导出");
                }
            }
            catch (OperationCanceledException)
            {
                if (this != null) ShowToast("已取消导出");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                UiSound.Play(UiCue.Error);
                ShowToast(ProfileCopy.DescribeBackupFailure(exception, false));
            }
            finally
            {
                if (_operation == cancellation) _operation = null;
                cancellation.Dispose();
                if (this != null) _busy.Hide();
            }
        }

        private async UniTaskVoid RunImportAsync()
        {
            var cancellation = new CancellationTokenSource();
            _operation = cancellation;
            DiscardPreview();
            _busy.Show("正在读取备份…", "确认合并之前不会改动本机档案", "校验格式、版本与每一条记录", () => cancellation.Cancel());
            PlayerProfileStore store = Store;
            try
            {
                ProfileImportPreview preview = await ProfileBackupPicker.PickImportAsync(cancellation.Token, store);
                if (this == null || !_presented)
                {
                    if (preview != null) store.CancelImport(preview.Id);
                    return;
                }

                _busy.Hide();
                if (preview == null)
                {
                    ShowToast("没有选择备份文件");
                    return;
                }

                UiSound.Play(UiCue.Open);
                ShowImportPreview(preview, store);
            }
            catch (OperationCanceledException)
            {
                if (this != null) ShowToast("已取消导入");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                UiSound.Play(UiCue.Error);
                ShowToast(ProfileCopy.DescribeBackupFailure(exception, true));
            }
            finally
            {
                if (_operation == cancellation) _operation = null;
                cancellation.Dispose();
                if (this != null) _busy.Hide();
            }
        }

        private void ConfirmPreview()
        {
            ProfileImportPreview preview = _preview;
            PlayerProfileStore store = _previewStore;
            if (preview == null || store == null)
            {
                return;
            }

            long before = Snapshot?.Best30.Total ?? 0L;
            _import.SetWorking(true);
            PlayerProfileSnapshot result;
            try
            {
                result = store.ConfirmImport(preview.Id);
            }
            catch (InvalidOperationException exception)
            {
                // Played, edited or opened another backup since the preview: it no longer applies.
                Debug.LogWarning("[Profile] Backup preview is stale: " + exception.Message);
                ForgetPreview();
                _import.Close();
                UiSound.Play(UiCue.Warning);
                ShowToast("本机档案在预览之后有变化，请重新选择备份");
                Reload();
                return;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // Nothing switched; the same preview can be confirmed again.
                Debug.LogWarning("[Profile] Could not commit the backup: " + exception.Message);
                _import.SetWorking(false);
                UiSound.Play(UiCue.Error);
                ShowToast("合并没有写入，本机档案保持原样，可以再试一次");
                return;
            }

            ForgetPreview();
            _import.Close();
            Reload();
            UiSound.Play(UiCue.Success);
            long after = result?.Best30.Total ?? before;
            string score = after != before
                ? " · Ranking Score " + ProfileCopy.Number(before) + " → " + ProfileCopy.Number(after)
                : " · Ranking Score 保持 " + ProfileCopy.Number(after);
            int dan = preview.DanDefinitionsAdded + preview.DanAttemptsAdded;
            ShowToast(preview.Added > 0
                ? "已合并 " + ProfileCopy.Number(preview.Added) + " 局" + (dan > 0 ? "与段位记录" : string.Empty) + score
                : dan > 0 ? "已合并备份中的段位与挑战记录"
                : preview.RestoresAppearance ? "已恢复备份中的头像与昵称" : "已合并备份中解锁的成就");
            // One card for every goal the merged history reaches; a backup merged twice reaches nothing new.
            if (_unlock.Bind(preview.ReachedAchievementIds, "合并备份后可解锁")) _unlock.Play();
            DataChanged?.Invoke(preview.Added > 0);
        }

        private void CancelPreview()
        {
            DiscardPreview();
            _import.Close();
        }

        /// <summary>Drops a preview that was not confirmed, so its staged copy is deleted.</summary>
        private void DiscardPreview()
        {
            ProfileImportPreview preview = _preview;
            PlayerProfileStore store = _previewStore;
            ForgetPreview();
            if (preview == null || store == null)
            {
                return;
            }

            try
            {
                store.CancelImport(preview.Id);
            }
            catch (InvalidOperationException)
            {
                // Already gone: another preview or an edit replaced it.
            }

            if (_import != null && _import.IsOpen) _import.Close();
        }

        private void ForgetPreview()
        {
            _preview = null;
            _previewStore = null;
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            _camera = CreateCamera();
            _camera.transform.SetParent(transform, false);
            _canvasRoot = new GameObject("ProfileCanvas", typeof(RectTransform));
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
            // The home page's aurora: the tier's color behind the player's card, violet on the far side.
            _tierGlow = AddGlow(root, "GlowTier", HomeTheme.Colors.Violet, 0.16f, new Vector2(0f, 1f), new Vector2(360f, -320f), 1700f);
            AddGlow(root, "GlowViolet", HomeTheme.Colors.Violet, 0.12f, new Vector2(1f, 0f), new Vector2(-320f, 160f), 1600f);
            AddGlow(root, "GlowRose", HomeTheme.Colors.Rose, 0.06f, new Vector2(0.65f, 1f), new Vector2(0f, 60f), 1100f);

            View = new ProfileView(root, this);
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            _editor = new ProfileEditPanel(ModalLayer, SaveAppearance);
            _busy = new ProfileBusyPanel(ModalLayer);
            _import = new ProfileImportPanel(ModalLayer);
            _deletion = new ProfileDeletePanel(ModalLayer);
            _medals = new MedalPicker(ModalLayer, WearMedal);
            _badges = new RankingBadgeGallery(ModalLayer);
            _toast = new UiToast(root, 48f, HomeTheme.Colors.Violet);
            _unlock = new AchievementUnlockPopup(root, 18f);
            _thumbnails = new JacketThumbnails(Path.Combine(Application.temporaryCachePath, THUMBNAIL_DIRECTORY));

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
            var cameraObject = new GameObject("ProfileScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 104f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            ObserveHistory(null);
            _curtainMotion.TryCancel();
            _operation?.Cancel();
            DiscardPreview();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _toast?.Dispose();
            _unlock?.Dispose();
            View?.Dispose();
            _editor?.Dispose();
            _busy?.Dispose();
            _import?.Dispose();
            _deletion?.Dispose();
            _medals?.Dispose();
            _badges?.Dispose();
            _thumbnails?.Dispose();
            if (_camera != null)
            {
                Destroy(_camera.gameObject);
            }
        }
    }
}
