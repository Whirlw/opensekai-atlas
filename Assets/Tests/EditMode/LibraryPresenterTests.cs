using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;

namespace OpenSekai.Tests.EditMode
{
    public sealed class LibraryPresenterTests
    {
        private string _root;
        private SongPackageManager _packages;
        private View _view;
        private LibraryPresenter _presenter;
        private PlayRequest _request;
        private PlayStartStatus _status;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "OpenSekai-LibraryPresenter-" + Guid.NewGuid().ToString("N"));
            _packages = new SongPackageManager(Path.Combine(_root, "library"), Path.Combine(_root, "imports"));
            _view = new View();
            _status = PlayStartStatus.Started;
            _presenter = new LibraryPresenter(_view, _packages, StartPlayback);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void RefreshSortsByTitleThenCyclesToLevel()
        {
            AddPackage("alpha", "Alpha", 1);
            AddPackage("zeta", "Zeta", 9);
            _presenter.Refresh();
            Assert.That(_view.Items.Select(item => item.Entry.Manifest.id), Is.EqualTo(new[] { "alpha", "zeta" }));

            _presenter.CycleSort();
            Assert.That(_view.SortLabel, Is.EqualTo("排序：等级"));
            Assert.That(_view.Items.Select(item => item.Entry.Manifest.id), Is.EqualTo(new[] { "zeta", "alpha" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RowPlaybackUsesThePackageAndRequestedMode(bool isAuto)
        {
            string folder = AddPackage("track", "Track", 1);
            _presenter.Refresh();
            _view.Play(_view.Items.Single(), isAuto);

            Assert.That(_request.PackageDirectory, Is.EqualTo(folder));
            Assert.That(_request.IsAuto, Is.EqualTo(isAuto));
            Assert.That(_request.ReturnScene, Is.EqualTo(Sekai.SceneManager.Scene.Library));
            Assert.That(_view.LoadingMessage, Is.EqualTo("正在进入…"));
        }

        [Test]
        public void FailedStartShowsAResultThatCanRetryTheSameRequest()
        {
            string folder = AddPackage("track", "Track", 1);
            _presenter.Refresh();
            _status = PlayStartStatus.Failed;
            _view.Play(_view.Items.Single(), true);

            Assert.That(_view.Result.Kind, Is.EqualTo(PlayResultKind.LoadError));
            Assert.That(_view.Loading, Is.False);
            Assert.That(_view.Status, Is.EqualTo("test result"));
            _status = PlayStartStatus.Started;
            _presenter.OnResultRetry(_view.Result);

            Assert.That(_request.PackageDirectory, Is.EqualTo(folder));
            Assert.That(_request.IsAuto, Is.True);
            Assert.That(_view.Loading, Is.True);
        }

        [Test]
        public void FolderImportRefreshesTheLibraryFromTheExistingPackageStore()
        {
            _presenter.StartImport(Path.GetFullPath("Tools/fixtures/baseline_tap"), true);

            Assert.That(_view.Items.Single().Entry.Manifest.id, Is.EqualTo("codexbaseline20261101tap"));
            Assert.That(_view.Items.Single().HasScore, Is.True);
            Assert.That(_view.Items.Single().HasAudio, Is.True);
        }

        private string AddPackage(string id, string title, int level)
        {
            string folder = Path.Combine(_packages.RootDirectory, id);
            Directory.CreateDirectory(folder);
            var manifest = new SongPackageManifest { id = id, title = title, playLevel = level };
            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonConvert.SerializeObject(manifest));
            return folder;
        }

        private UniTask<PlayStartResult> StartPlayback(PlayRequest request, CancellationToken token, Action<string> progress)
        {
            _request = request;
            progress("test progress");
            return UniTask.FromResult(new PlayStartResult(_status, "test result"));
        }

        private sealed class View : ILibraryView
        {
            public bool HasList => true;
            internal IReadOnlyList<SongPackageItem> Items { get; private set; } = Array.Empty<SongPackageItem>();
            internal Action<SongPackageItem, bool> Play { get; private set; }
            internal string SortLabel { get; private set; }
            internal string Status { get; private set; }
            internal PlayResult Result { get; private set; }
            internal string LoadingMessage { get; private set; }
            internal bool Loading { get; private set; }

            public void ClearRows() => Items = Array.Empty<SongPackageItem>();
            public void SetCount(int count) { }
            public void SetSortLabel(string label) => SortLabel = label;
            public void SetStatus(string message) => Status = message;
            public void ShowSettings() { }
            public void ShowResult(PlayResult result) => Result = result;
            public void ShowLoading(string title) => Loading = true;
            public void HideLoading() => Loading = false;
            public void SetLoadingMessage(string message) => LoadingMessage = message;
            public void ShowActions(string title, Action export, Action delete) { }

            public void RenderRows(IReadOnlyList<SongPackageItem> items, Action<SongPackageItem, bool> play, Action<SongPackageItem> openActions)
            {
                Items = items;
                Play = play;
            }
        }
    }
}
