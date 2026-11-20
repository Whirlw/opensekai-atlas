using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.EditMode.Sonolus
{
    public sealed class SonolusInstallIndexTests
    {
        private string _root;
        private string _indexDirectory;
        private SongPackageManager _packages;
        private SonolusServerAddress _server;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "sonolus-install-" + Guid.NewGuid().ToString("N"));
            _indexDirectory = Path.Combine(_root, "index");
            _packages = new SongPackageManager(Path.Combine(_root, "library"), Path.Combine(_root, "temp"));
            _server = SonolusServerAddress.Parse("example.com");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, recursive: true);

        [Test]
        public void MusicAndCoverChangesAreDetectedAfterRestart()
        {
            string staged = StagePackage(withCover: true);
            CreateInstaller().PublishPackage(staged, _server, Level());

            var reloaded = CreateInstaller();
            Assert.That(reloaded.StateOf(_server, Level()), Is.EqualTo(SonolusInstallState.Installed));
            Assert.That(reloaded.StateOf(_server, Level(bgmHash: "new-music")), Is.EqualTo(SonolusInstallState.Outdated));
            Assert.That(reloaded.StateOf(_server, Level(coverHash: "new-cover")), Is.EqualTo(SonolusInstallState.Outdated));
            Assert.That(reloaded.StateOf(_server, Level(coverHash: null)), Is.EqualTo(SonolusInstallState.Outdated));
        }

        [Test]
        public void PublishingWithADamagedIndexRowPreservesGoodRowsAndTheOriginalIndex()
        {
            var installer = CreateInstaller();
            var first = Level(name: "previous");
            var previous = installer.PublishPackage(StagePackage(withCover: true), _server, first);
            string index = Path.Combine(_indexDirectory, "sonolus-downloads.json");
            var rows = JObject.Parse(File.ReadAllText(index));
            rows["broken"] = new JObject();
            File.WriteAllText(index, rows.ToString());
            byte[] before = File.ReadAllBytes(index);
            var next = installer.PublishPackage(StagePackage(withCover: true), _server, Level());
            var fresh = CreateInstaller();
            Assert.That(fresh.FindInstalled(_server, first.Name), Is.EqualTo(previous.RootDirectory));
            Assert.That(fresh.FindInstalled(_server, Level().Name), Is.EqualTo(next.RootDirectory));
            Assert.That(File.ReadAllBytes(Directory.GetFiles(_indexDirectory, "*.bak").Single()), Is.EqualTo(before));
        }

        [Test]
        public void MissingOptionalCoverLeavesThePackageAvailableAndCanBeRetried()
        {
            SongPackage package = CreateInstaller().PublishPackage(StagePackage(withCover: false), _server, Level());

            var reloaded = CreateInstaller();
            Assert.That(reloaded.FindInstalled(_server, Level().Name), Is.EqualTo(package.RootDirectory));
            Assert.That(File.Exists(package.AudioPath), Is.True);
            Assert.That(reloaded.StateOf(_server, Level()), Is.EqualTo(SonolusInstallState.Outdated));
            Assert.That(reloaded.StateOf(_server, Level(coverHash: null)), Is.EqualTo(SonolusInstallState.Installed));
        }

        [Test]
        public void FailedIndexCommitKeepsThePreviousPackageAndAllowsARetry()
        {
            var installer = CreateInstaller();
            var oldLevel = Level();
            var newLevel = Level(bgmHash: "new-music");
            SongPackage old = installer.PublishPackage(StagePackage(withCover: true), _server, oldLevel);
            string update = StagePackage(withCover: true);
            string heldIndex = Path.Combine(_root, "held-index");
            Directory.Move(_indexDirectory, heldIndex);
            try
            {
                File.WriteAllText(_indexDirectory, "Index storage is unavailable for this update.");
                Assert.Throws<SonolusException>(() => installer.PublishPackage(update, _server, newLevel));
                Assert.That(installer.FindInstalled(_server, oldLevel.Name), Is.EqualTo(old.RootDirectory));
                Assert.That(_packages.LoadItems().Count, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(_indexDirectory);
                Directory.Move(heldIndex, _indexDirectory);
            }

            Assert.That(CreateInstaller().StateOf(_server, oldLevel), Is.EqualTo(SonolusInstallState.Installed));
            SongPackage updated = installer.PublishPackage(update, _server, newLevel);
            Assert.That(CreateInstaller().FindInstalled(_server, newLevel.Name), Is.EqualTo(updated.RootDirectory));
            Assert.That(CreateInstaller().StateOf(_server, newLevel), Is.EqualTo(SonolusInstallState.Installed));
            Assert.That(Directory.Exists(old.RootDirectory), Is.False);
            Assert.That(_packages.LoadItems().Count, Is.EqualTo(1));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RestartRecoversTheIndexedPackageAndLeavesManualCopiesAlone(bool indexCommitted, bool directoryAlreadyRemoved)
        {
            var installer = CreateInstaller();
            var oldLevel = Level();
            var newLevel = Level(bgmHash: "new-music");
            var unrelatedLevel = Level(name: "unrelated");
            SongPackage unrelated = installer.PublishPackage(StagePackage(withCover: true), _server, unrelatedLevel);
            SongPackage old = installer.PublishPackage(StagePackage(withCover: true), _server, oldLevel);
            string downloads = Directory.CreateDirectory(Path.Combine(_root, "staging")).FullName;
            string download = Path.Combine(downloads, Guid.NewGuid().ToString("N"));
            Directory.Move(StagePackage(withCover: true), download);
            SongPackage pending = installer.PreparePublish(download, _server, newLevel, ownsDownloadStage: true);
            Assert.That(Directory.Exists(pending.RootDirectory), Is.True);
            Assert.That(installer.FindInstalled(_server, oldLevel.Name), Is.EqualTo(old.RootDirectory));

            SongPackage kept = indexCommitted ? pending : old;
            SongPackage discarded = indexCommitted ? old : pending;
            SongPackage manualCopy = _packages.ImportFolder(discarded.RootDirectory);
            File.Copy(discarded.ManifestPath, manualCopy.ManifestPath, overwrite: true);
            Assert.That(File.ReadAllText(manualCopy.ManifestPath), Is.EqualTo(File.ReadAllText(discarded.ManifestPath)));

            // Keep a real cache on disk that still lists both installs when recovery starts.
            Assert.That(_packages.RescanItems().Select(item => item.Entry.RootDirectory), Does.Contain(discarded.RootDirectory));
            string journal = Path.Combine(_indexDirectory, "sonolus-downloads.json.pending");
            var operation = Guid.Parse((string)JObject.Parse(File.ReadAllText(journal))["Id"]);
            string retired = _packages.ImportStagingPath(operation);
            if (indexCommitted)
                installer.CommitPublication(_server, newLevel, pending);
            if (directoryAlreadyRemoved)
                Directory.Move(discarded.RootDirectory, retired);

            for (int restart = 0; restart < 2; restart++)
            {
                var reloadedPackages = new SongPackageManager(_packages.RootDirectory, Path.Combine(_root, "temp"));
                var reloadedInstaller = CreateInstaller(reloadedPackages);
                Assert.That(reloadedInstaller.FindInstalled(_server, oldLevel.Name), Is.EqualTo(kept.RootDirectory));
                Assert.That(reloadedInstaller.StateOf(_server, indexCommitted ? newLevel : oldLevel), Is.EqualTo(SonolusInstallState.Installed));
                Assert.That(reloadedInstaller.FindInstalled(_server, unrelatedLevel.Name), Is.EqualTo(unrelated.RootDirectory));
                Assert.That(Directory.Exists(discarded.RootDirectory), Is.False);
                Assert.That(Directory.Exists(retired), Is.False);
                Assert.That(Directory.Exists(download), Is.False);
                Assert.That(File.Exists(journal), Is.False);
                Assert.That(File.Exists(manualCopy.ManifestPath), Is.True);
                Assert.That(reloadedPackages.LoadItems().Select(item => item.Entry.RootDirectory), Is.EquivalentTo(new[]
                {
                    kept.RootDirectory, manualCopy.RootDirectory, unrelated.RootDirectory
                }));
            }
        }

        [Test]
        public void UnreadableIndexDefersCleanupUntilTheSelectedEditionCanBeVerified()
        {
            var installer = CreateInstaller();
            var old = installer.PublishPackage(StagePackage(true), _server, Level());
            var incoming = installer.PreparePublish(StagePackage(true), _server, Level(bgmHash: "new"));
            string indexPath = Path.Combine(_indexDirectory, "sonolus-downloads.json");
            string validIndex = File.ReadAllText(indexPath);
            File.WriteAllText(indexPath, "{");
            CreateInstaller();
            Assert.That(File.Exists(old.ManifestPath), Is.True);
            Assert.That(File.Exists(incoming.ManifestPath), Is.True);

            File.WriteAllText(indexPath, validIndex);
            Assert.That(CreateInstaller().FindInstalled(_server, Level().Name), Is.EqualTo(old.RootDirectory));
            Assert.That(Directory.Exists(incoming.RootDirectory), Is.False);
        }

        [Test]
        public void AnUnmarkedReplacementAtTheIncomingPathSurvivesCleanup()
        {
            var installer = CreateInstaller();
            var old = installer.PublishPackage(StagePackage(true), _server, Level());
            var incoming = installer.PreparePublish(StagePackage(true), _server, Level(bgmHash: "new"));
            Directory.Move(incoming.RootDirectory, Path.Combine(_root, "moved-by-owner"));
            Directory.Move(StagePackage(false), incoming.RootDirectory);

            var restarted = CreateInstaller();
            Assert.That(restarted.FindInstalled(_server, Level().Name), Is.EqualTo(old.RootDirectory));
            Assert.That(File.Exists(incoming.ManifestPath), Is.True);
            Assert.That(_packages.LoadItems().Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CancellationAfterTheWorkerWritesLeavesNoPackageOrStagingFiles() => UniTask.ToCoroutine(async () =>
        {
            var installer = CreateInstaller();
            var level = Level();
            using var cancellation = new CancellationTokenSource();
            string writtenFile = null;
            try
            {
                await installer.StageAndPublishAsync(_server, level, directory =>
                {
                    string file = Path.Combine(directory, "score.json");
                    File.WriteAllText(file, "{}");
                    writtenFile = file;
                    cancellation.Cancel();
                }, null, cancellation.Token);
                Assert.Fail("The publish step should observe the cancellation.");
            }
            catch (OperationCanceledException)
            {
                Assert.That(cancellation.IsCancellationRequested, Is.True);
            }

            Assert.That(writtenFile, Is.Not.Null);
            Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(_root, "staging")), Is.Empty);
            Assert.That(installer.FindInstalled(_server, level.Name), Is.Null);
            Assert.That(_packages.LoadItems(), Is.Empty);
        });

        private SonolusLevelInstaller CreateInstaller(SongPackageManager packages = null) => new SonolusLevelInstaller(
            packages ?? _packages, _indexDirectory, Path.Combine(_root, "staging"));

        private string StagePackage(bool withCover)
        {
            string path = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, SongPackageStorage.MANIFEST_FILE_NAME),
                @"{""id"":""fixture"",""audioFileName"":""audio.ogg"",""jacketFileName"":""jacket.png""}");
            File.WriteAllText(Path.Combine(path, "score.json"), "{}");
            File.WriteAllText(Path.Combine(path, "audio.ogg"), "Audio fixture");
            if (withCover) File.WriteAllText(Path.Combine(path, "jacket.png"), "Cover fixture");
            return path;
        }

        private static SonolusLevel Level(string bgmHash = "music", string coverHash = "cover", string name = "level") => SonolusLevel.From(new JObject
        {
            ["name"] = name,
            ["version"] = 1,
            ["data"] = new JObject { ["hash"] = "chart", ["url"] = "/chart" },
            ["bgm"] = new JObject { ["hash"] = bgmHash, ["url"] = "/music" },
            ["cover"] = coverHash == null ? null : new JObject { ["hash"] = coverHash, ["url"] = "/cover" }
        });
    }
}
