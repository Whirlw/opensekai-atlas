using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.UI
{
    internal sealed class LibraryPresenter : IDisposable
    {
        private const string PackageExtension = ".oskp";

        private readonly ILibraryView _view;
        private readonly SongPackageManager _packages;
        private readonly Func<PlayRequest, CancellationToken, Action<string>, UniTask<PlayStartResult>> _startPlay;
        private readonly Func<IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary>> _loadHistory;
        private readonly Action _reloadHistory;
        private readonly LibraryModel _model = new LibraryModel();
        private IReadOnlyList<SongPackageItem> _items = Array.Empty<SongPackageItem>();
        // The page's own copy, so a recorded run can update one package without touching the loader's.
        private Dictionary<string, CustomMusicScorePlayHistorySummary> _history =
            new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
        private LibraryListing _listing;
        private SongPackageItem _selected;
        private LibraryViewMode _viewMode = LibraryViewMode.List;
        private CancellationTokenSource _playbackCts;
        private bool _isStartingPlayback;

        /// <param name="view">The page to draw on.</param>
        /// <param name="packages">The package store the library lists.</param>
        /// <param name="startPlay">Starts a live session for a package.</param>
        /// <param name="loadHistory">Play history per score key, as cached; defaults to the history store.</param>
        /// <param name="reloadHistory">Rebuilds the cached history from the history file; defaults to the history store.</param>
        internal LibraryPresenter(
            ILibraryView view,
            SongPackageManager packages,
            Func<PlayRequest, CancellationToken, Action<string>, UniTask<PlayStartResult>> startPlay,
            Func<IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary>> loadHistory = null,
            Action reloadHistory = null)
        {
            _view = view;
            _packages = packages;
            _startPlay = startPlay;
            _loadHistory = loadHistory ?? CustomMusicScorePlayHistoryStorage.LoadSummaries;
            _reloadHistory = reloadHistory ?? (() => CustomMusicScorePlayHistoryStorage.ReloadSummaries());
            CustomMusicScorePlayHistoryStorage.Recorded += OnScoreRecorded;
        }

        /// <summary>
        /// A run was just written: take that package's updated summary (best only replaced when the
        /// run beat it, last-played and play count moved on) and redraw only where it shows. The
        /// list is not reloaded or re-sorted; the next refresh puts it in order.
        /// </summary>
        internal void OnScoreRecorded(string scoreKey, CustomMusicScorePlayHistorySummary summary)
        {
            if (string.IsNullOrEmpty(scoreKey) || summary == null)
            {
                return;
            }

            _history[scoreKey] = summary;
            _view.RefreshScore(scoreKey);
        }

        internal void SetSort(LibrarySortKey key)
        {
            _model.SortKey = key;
            Render();
        }

        internal void SetGroupMode(LibraryGroupKey key)
        {
            _model.SetGroupKey(key);
            Render();
        }

        /// <summary>Narrows the list to one group; null shows every group again.</summary>
        internal void SelectGroup(string group)
        {
            _model.SelectedGroup = group;
            Render();
        }

        internal void SetSearch(string text)
        {
            _model.SearchText = text ?? string.Empty;
            Render();
        }

        internal void SetViewMode(LibraryViewMode mode)
        {
            if (_viewMode == mode)
            {
                return;
            }

            _viewMode = mode;
            Render();
        }

        internal void Select(SongPackageItem item)
        {
            if (item == null || ReferenceEquals(item, _selected))
            {
                return;
            }

            _selected = item;
            _view.SetSelection(item);
        }

        internal void OpenActionsForSelected()
        {
            OpenActions(_selected);
        }

        public void Dispose()
        {
            CustomMusicScorePlayHistoryStorage.Recorded -= OnScoreRecorded;
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = null;
        }

        internal void ShowResult(PlayResult result)
        {
            if (result == null)
            {
                return;
            }

            _view.SetStatus(result.Title + "  ·  " + result.KindText);
            _view.ShowResult(result);
        }

        internal void OpenActions(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            _view.ShowActions(LibraryModel.BuildTitle(item), () => ExportPackage(item), () => DeletePackage(item));
        }

        /// <summary>
        /// The refresh button: walks every package folder and rebuilds the score summaries from the
        /// history file, the one way back in sync with files changed outside the app.
        /// </summary>
        internal void Resync()
        {
            if (!_view.HasList)
            {
                return;
            }

            try
            {
                _packages.RescanItems();
                _reloadHistory();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view.SetStatus("刷新失败：" + exception.Message);
                return;
            }

            Refresh();
            if (_items.Count > 0)
            {
                _view.SetStatus("已重新读取曲库：" + _items.Count + " 首");
            }
        }

        /// <summary>Redraws the page from the caches; nothing is read from disk once they are loaded.</summary>
        internal void Refresh()
        {
            if (!_view.HasList)
            {
                return;
            }

            try
            {
                _items = _packages.LoadItems();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _items = Array.Empty<SongPackageItem>();
                Render();
                _view.SetStatus("读取曲库失败：" + exception.Message);
                return;
            }

            IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> loaded = _loadHistory();
            if (loaded != null)
            {
                _history = new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, CustomMusicScorePlayHistorySummary> pair in loaded)
                {
                    _history[pair.Key] = pair.Value;
                }
            }
            Render();
            _view.SetStatus(_items.Count == 0
                ? "还没有曲包。点“导入”添加，或把曲包文件夹放到 CustomMusicScores 目录后点“刷新”。"
                : string.Empty);
        }

        private void Render()
        {
            _listing = _model.Build(_items, _history);
            _selected = KeepSelection(_listing.Items, _selected);
            _view.Render(new LibraryViewState(_listing, _model.GroupKey, _model.SortKey, _viewMode, _selected, _history));
        }

        /// <summary>
        /// Keeps the selected package across a reload (items are new objects each time, so it is
        /// matched by folder) and falls back to the first row when it left the list.
        /// </summary>
        private static SongPackageItem KeepSelection(IReadOnlyList<SongPackageItem> items, SongPackageItem selected)
        {
            string directory = selected?.Entry?.RootDirectory;
            if (directory != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Entry?.RootDirectory, directory, StringComparison.Ordinal))
                    {
                        return items[i];
                    }
                }
            }

            return items.Count > 0 ? items[0] : null;
        }

        internal void OnResultRetry(PlayResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.PackageDirectory))
            {
                _view.SetStatus("找不到该曲包的目录，无法重开。");
                return;
            }

            StartPlayback(result.PackageDirectory, result.Title, result.IsAuto);
        }

        private void ExportPackage(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            // Keep the extension out of the suggested name: the platform save dialog appends the
            // filter extension itself, and passing "Test.oskp" produced "Test.oskp.oskp".
            string baseName = SafeFileName(LibraryModel.BuildTitle(item));
            string destination = LibraryImport.PickExportDestination(baseName);
            if (string.IsNullOrEmpty(destination))
            {
                // Mobile has no save dialog: write next to the library so the file is still reachable.
                string exportDirectory = Path.Combine(_packages.RootDirectory, "Exports");
                destination = Path.Combine(exportDirectory, baseName + PackageExtension);
                _view.SetStatus("正在导出到 " + destination + " …");
            }
            else if (!destination.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                // Some dialogs do not append the filter extension when the user typed one.
                destination += PackageExtension;
            }

            try
            {
                string written = _packages.ExportZip(item.Entry, destination);
                _view.SetStatus("已导出：" + written);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view.SetStatus("导出失败：" + exception.Message);
            }
        }

        private void DeletePackage(SongPackageItem item)
        {
            if (item?.Entry == null)
            {
                return;
            }

            string title = LibraryModel.BuildTitle(item);
            try
            {
                _packages.DeleteEntry(item.Entry);
                _view.SetStatus("已删除曲包：" + title);
                Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view.SetStatus("删除失败：" + exception.Message);
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

        internal void OpenSettings()
        {
            _view.ShowSettings();
        }

        internal void OnSettingsChanged()
        {
            _view.SetStatus("设置已保存。");
        }

        internal void ImportZip()
        {
            _view.SetStatus("请选择曲包文件（.zip / .oskp）…");
            LibraryImport.PickZip(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    _view.SetStatus("已取消导入。");
                    return;
                }

                StartImport(path, false);
            });
        }

        internal void ImportFolder()
        {
            _view.SetStatus("请选择曲包文件夹…");
            LibraryImport.PickFolder(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    _view.SetStatus("已取消导入。");
                    return;
                }

                StartImport(path, true);
            });
        }

        internal void StartImport(string path, bool isFolder)
        {
            _view.SetStatus("正在导入…");
            try
            {
                SongPackage package = isFolder
                    ? _packages.ImportFolder(path)
                    : _packages.ImportZip(path);
                string name = package?.Manifest != null && !string.IsNullOrWhiteSpace(package.Manifest.title)
                    ? package.Manifest.title
                    : "曲包";
                _view.SetStatus("导入成功：" + name);
                Refresh();
            }
            catch (Exception exception)
            {
                _view.SetStatus(LibraryImport.DescribeFailure(exception));
            }
        }

        internal void StartPlayback(SongPackageItem item, bool isAuto)
        {
            if (_isStartingPlayback || item?.Entry == null)
            {
                return;
            }

            StartPlayback(item.Entry.RootDirectory, LibraryModel.BuildTitle(item), isAuto);
        }

        /// <summary>Starts a session for a package directory; used by rows and by 再玩一次.</summary>
        private void StartPlayback(string packageDirectory, string title, bool isAuto)
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = new CancellationTokenSource();
            PlayAsync(packageDirectory, title, isAuto, _playbackCts.Token).Forget();
        }

        internal void OnCancelPlayback()
        {
            _playbackCts?.Cancel();
        }

        private async UniTaskVoid PlayAsync(
            string packageDirectory, string title, bool isAuto, CancellationToken cancellationToken)
        {
            _isStartingPlayback = true;
            try
            {
                _view.SetStatus((isAuto ? "自动" : "手动") + "：" + title + " — 准备中…");
                _view.ShowLoading(title);
                PlayStartResult result = await _startPlay(
                    new PlayRequest(packageDirectory, isAuto, SceneManager.Scene.Library),
                    cancellationToken,
                    message =>
                    {
                        _view.SetStatus(message);
                        _view.SetLoadingMessage(message);
                    });

                if (!result.Started)
                {
                    _view.HideLoading();
                    _view.SetStatus(result.Message);
                    if (result.Status == PlayStartStatus.Failed)
                    {
                        _view.ShowResult(PlayResult.FromLoadError(title, isAuto, packageDirectory, result.Message));
                    }
                }
                else
                {
                    // The live scene unloads this page; keep the overlay until then so the
                    // transition does not flash an empty library behind the loading screen.
                    _view.SetLoadingMessage("正在进入…");
                }
            }
            catch (OperationCanceledException)
            {
                _view.HideLoading();
                _view.SetStatus("已取消开局。");
            }
            catch (Exception exception)
            {
                _view.HideLoading();
                Debug.LogException(exception);
                _view.SetStatus("开局失败，请检查运行日志。");
            }
            finally
            {
                _isStartingPlayback = false;
            }
        }
    }
}
