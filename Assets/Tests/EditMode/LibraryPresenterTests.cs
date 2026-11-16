using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.App.Library;
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
        public void RefreshSortsByTitleAndCanSortByLevel()
        {
            AddPackage("alpha", "Alpha", 1);
            AddPackage("zeta", "Zeta", 9);
            _presenter.Refresh();
            Assert.That(_view.Items.Select(item => item.Entry.Manifest.id), Is.EqualTo(new[] { "alpha", "zeta" }));

            _presenter.SetSort(LibrarySortKey.Level);
            Assert.That(_view.State.SortKey, Is.EqualTo(LibrarySortKey.Level));
            Assert.That(_view.Items.Select(item => item.Entry.Manifest.id), Is.EqualTo(new[] { "zeta", "alpha" }));
        }

        [Test]
        public void ImportedSortUsesTheRecordedTimeEvenAfterAnEarlierPackageIsEdited()
        {
            AddPackage("alpha", "Alpha", 1, DateTimeOffset.UnixEpoch.AddDays(1));
            string newer = AddPackage("zeta", "Zeta", 9, DateTimeOffset.UnixEpoch.AddDays(2));
            File.SetLastWriteTimeUtc(Path.Combine(newer, "manifest.json"), DateTime.UnixEpoch);
            Directory.SetLastWriteTimeUtc(newer, DateTime.UnixEpoch);

            _presenter.Refresh();
            _presenter.SetSort(LibrarySortKey.Imported);

            Assert.That(_view.Items.Select(item => item.Entry.Manifest.id), Is.EqualTo(new[] { "zeta", "alpha" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RowPlaybackUsesThePackageAndRequestedMode(bool isAuto)
        {
            string folder = AddPackage("track", "Track", 1);
            _presenter.Refresh();
            _presenter.StartPlayback(_view.Selected, isAuto);

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
            _presenter.StartPlayback(_view.Selected, true);

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

        [Test]
        public void ARecordedRunUpdatesOnlyThatPackageWithoutReloadingHistory()
        {
            AddPackage("alpha", "Alpha", 1);
            AddPackage("zeta", "Zeta", 9);
            int loads = 0;
            var presenter = new LibraryPresenter(_view, _packages, StartPlayback, () =>
            {
                loads++;
                return new Dictionary<string, CustomMusicScorePlayHistorySummary>();
            });
            try
            {
                presenter.Refresh();
                SongPackageItem alpha = _view.Items.Single(item => item.Entry.Manifest.id == "alpha");
                SongPackageItem zeta = _view.Items.Single(item => item.Entry.Manifest.id == "zeta");
                var summaries = new Dictionary<string, CustomMusicScorePlayHistorySummary>(StringComparer.Ordinal);
                CustomMusicScorePlayHistorySummary summary = CustomMusicScorePlayHistoryStorage.Fold(summaries,
                    new CustomMusicScorePlayHistoryRecord { scoreKey = zeta.ScoreKey, totalScore = 500, playedAtUtc = "2026-11-16T00:00:00Z" });

                presenter.OnScoreRecorded(zeta.ScoreKey, summary);

                Assert.That(loads, Is.EqualTo(1), "the history is not loaded again");
                Assert.That(_view.RefreshedScores, Is.EqualTo(new[] { zeta.ScoreKey }));
                Assert.That(LibraryModel.SummaryOf(zeta, _view.State.History).Best.totalScore, Is.EqualTo(500));
                Assert.That(LibraryModel.SummaryOf(alpha, _view.State.History), Is.Null);
            }
            finally
            {
                presenter.Dispose();
            }
        }

        private string AddPackage(string id, string title, int level, DateTimeOffset? importedAt = null)
        {
            string folder = Path.Combine(_packages.RootDirectory, id);
            Directory.CreateDirectory(folder);
            var manifest = new SongPackageManifest { id = id, title = title, playLevel = level, importedAtUtc = importedAt };
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
            internal LibraryViewState State { get; private set; }
            internal IReadOnlyList<SongPackageItem> Items => State.Listing.Items;
            internal SongPackageItem Selected { get; private set; }
            internal string Status { get; private set; }
            internal PlayResult Result { get; private set; }
            internal string LoadingMessage { get; private set; }
            internal bool Loading { get; private set; }

            public void SetSelection(SongPackageItem selected) => Selected = selected;
            public void SetStatus(string message) => Status = message;
            public void ShowSettings() { }
            public void ShowResult(PlayResult result) => Result = result;
            public void ShowLoading(string title) => Loading = true;
            public void HideLoading() => Loading = false;
            public void SetLoadingMessage(string message) => LoadingMessage = message;
            public void ShowActions(string title, Action export, Action delete) { }
            internal List<string> RefreshedScores { get; } = new List<string>();
            public void RefreshScore(string scoreKey) => RefreshedScores.Add(scoreKey);

            public void Render(LibraryViewState state)
            {
                State = state;
                Selected = state.Selected;
            }
        }
    }
}
