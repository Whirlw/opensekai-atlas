using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using OpenSekai.Infrastructure.Packages;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class ServerChartImportTests
    {
        private string _root;
        [SetUp] public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "server-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }
        [TearDown] public void TearDown() { Directory.Delete(_root, true); }

        private string Archive(string name, string content = "test")
        {
            string zip = Path.Combine(_root, "download.zip");
            using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
            return zip;
        }

        [TestCase("../escape.txt")]
        [TestCase("folder/../../escape.txt")]
        [TestCase("..\\escape.txt")]
        [TestCase("/absolute.txt")]
        [TestCase("C:/escape.txt")]
        public void RejectsArchivePathEscape(string name)
        {
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(Archive(name), Path.Combine(_root, "stage")));
            Assert.That(File.Exists(Path.Combine(_root, "escape.txt")), Is.False);
        }

        [Test] public void ExtractsUtf8AndNestedFiles()
        {
            string stage = Path.Combine(_root, "stage");
            float progress = 0;
            SafeZipExtractor.Extract(Archive("谱面/音乐.txt", "音符"), stage, p => progress = p);
            Assert.That(File.ReadAllText(Path.Combine(stage, "谱面", "音乐.txt")), Is.EqualTo("音符"));
            Assert.That(progress, Is.EqualTo(1f));
        }

        [Test] public void InsufficientSpaceRejectsBeforeWritingArchiveContents()
        {
            string zip = Archive("score.json");
            string stage = Path.Combine(_root, "stage");
            var error = Assert.Throws<IOException>(() => SafeZipExtractor.Extract(zip, stage,
                availableBytes: SafeZipExtractor.FreeSpaceReserveBytes));
            Assert.That(error.Message, Does.Contain("存储空间不足"));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [Test] public void ExtractsWithCapturedFreeSpaceWithoutQueryingTheWorkerFilesystem()
        {
            string zip = Archive("score.json");
            string stage = Path.Combine(_root, "stage");
            SafeZipExtractor.Extract(zip, stage, availableBytes: SafeZipExtractor.FreeSpaceReserveBytes + 4);
            Assert.That(File.ReadAllText(Path.Combine(stage, "score.json")), Is.EqualTo("test"));
        }

        [Test] public void RejectsSymbolicLinks()
        {
            string zip = Path.Combine(_root, "link.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                archive.CreateEntry("link").ExternalAttributes = unchecked((int)0xA1FF0000);
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, Path.Combine(_root, "stage")));
        }

        [Test] public void RejectsTooManyFilesBeforeExtracting()
        {
            string zip = Path.Combine(_root, "many.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                for (int i = 0; i <= SafeZipExtractor.MaxFiles; i++) archive.CreateEntry(i + ".txt");
            string stage = Path.Combine(_root, "stage");
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, stage));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [Test] public void RejectsOversizedDeclaredFileBeforeExtracting()
        {
            string zip = Archive("large.bin");
            byte[] bytes = File.ReadAllBytes(zip);
            for (int i = 0; i < bytes.Length - 28; i++)
            {
                if (BitConverter.ToUInt32(bytes, i) != 0x02014b50) continue;
                BitConverter.GetBytes((uint)(SafeZipExtractor.MaxFileBytes + 1)).CopyTo(bytes, i + 24);
                break;
            }
            File.WriteAllBytes(zip, bytes);
            string stage = Path.Combine(_root, "stage");
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, stage));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [UnityTest] public IEnumerator RejectsManifestReferencesOutsideThePackage()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            string zip = Archive("manifest.json", "{\"id\":\"bad\",\"scoreFileName\":\"../outside.json\"}");
            var task = manager.ImportDownloadedZipAsync(zip, "44");
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
            Assert.That(Directory.Exists(manager.RootDirectory), Is.False);
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }

        [UnityTest] public IEnumerator CompletedDownloadCanBeRecoveredWithoutDuplicateImport()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"), Path.Combine(_root, "temp"));
            var original = manager.CreateNewEntry();
            string zip = manager.ExportZip(original, Path.Combine(_root, "package.zip"));
            var firstTask = manager.ImportDownloadedZipAsync(zip, "42");
            while (!firstTask.IsCompleted) yield return null;
            var first = firstTask.GetAwaiter().GetResult();
            // Simulate process death after publishing but before acknowledging the download.
            var restarted = new SongPackageManager(manager.RootDirectory);
            var recoveredTask = restarted.ImportDownloadedZipAsync(zip, "42");
            while (!recoveredTask.IsCompleted) yield return null;
            var recovered = recoveredTask.GetAwaiter().GetResult();
            Assert.That(recovered.RootDirectory, Is.EqualTo(first.RootDirectory));
            Assert.That(restarted.LoadItems().Count, Is.EqualTo(2));
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }

        [UnityTest] public IEnumerator DownloadedNativeChartUsesTheFormatDeclaredInItsManifest()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            string zip = Path.Combine(_root, "native.zip");
            const string scoreName = "level-data.json.gz";
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using (var manifest = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                    manifest.Write(@"{""id"":""next-rush-demo"",""chartFormat"":""next-rush"",""scoreFileName"":""level-data.json.gz""}");
                using (var compressed = new GZipStream(archive.CreateEntry(scoreName).Open(), CompressionMode.Compress))
                using (var score = new StreamWriter(compressed))
                    score.Write(@"{""bgmOffset"":0,""entities"":[]}");
            }

            var task = manager.ImportDownloadedZipAsync(zip, "45");
            while (!task.IsCompleted) yield return null;
            var installed = task.GetAwaiter().GetResult();

            Assert.That(installed.Manifest.chartFormat, Is.EqualTo("next-rush"));
            Assert.That(Path.GetFileName(installed.ScorePath), Is.EqualTo(scoreName));
            Assert.That(File.Exists(installed.ScorePath), Is.True);
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }

        [UnityTest] public IEnumerator InvalidDownloadLeavesExistingLibraryUntouched()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            var existing = manager.CreateNewEntry();
            string zip = Archive("readme.txt");
            var task = manager.ImportDownloadedZipAsync(zip, "43");
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
            Assert.That(File.Exists(existing.ManifestPath), Is.True);
            Assert.That(manager.LoadItems().Count, Is.EqualTo(1));
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }
    }
}
