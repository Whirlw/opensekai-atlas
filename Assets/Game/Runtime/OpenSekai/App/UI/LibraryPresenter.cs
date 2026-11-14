using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        private readonly LibraryModel _model = new LibraryModel();
        private CancellationTokenSource _playbackCts;
        private bool _isStartingPlayback;

        internal LibraryPresenter(
            ILibraryView view,
            SongPackageManager packages,
            Func<PlayRequest, CancellationToken, Action<string>, UniTask<PlayStartResult>> startPlay)
        {
            _view = view;
            _packages = packages;
            _startPlay = startPlay;
        }

        internal string SortLabel => _model.SortLabel;

        internal void CycleSort()
        {
            _model.CycleSort();
            _view.SetSortLabel(SortLabel);
            Refresh();
        }

        public void Dispose()
        {
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

        internal void Refresh()
        {
            if (!_view.HasList)
            {
                return;
            }

            _view.ClearRows();
            IReadOnlyList<SongPackageItem> items;
            try
            {
                items = _packages.LoadItems();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view.SetStatus("读取曲库失败：" + exception.Message);
                return;
            }

            _view.SetCount(items.Count);
            if (items.Count == 0)
            {
                _view.SetStatus("还没有曲包。把曲包文件夹放到 CustomMusicScores 目录后点“刷新”。");
                return;
            }

            _view.RenderRows(_model.Order(items), StartPlayback, OpenActions);
            _view.SetStatus("就绪。");
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
