using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class ServerChartImportTests
    {
        private string _root;

        [TestCase("https://opensekai.com/download?id=761", "os-761")]
        [TestCase("https://opensekai.com/chart?other=1&id=761", "os-761")]
        [TestCase("https://opensekai.com/chart?id=761&id=12", null)]
        [TestCase("https://opensekai.com.fake.org/chart?id=761", null)]
        [TestCase("https://opensekai.com:444/chart?id=761", null)]
        [TestCase("https://opensekai.com/?zone=quality&id=761", null)]
        public void RankLookupTargetsOnlyASinglePublisherChart(string page, string key)
        {
            Assert.That(ChartRankCache.Key(SongPackageSource.FromWeb(page, "https://cdn.test/song.zip")), Is.EqualTo(key));
        }

        [Test]
        public void OnlyMissingLinksBlockDetailsAndLocalReadErrorsDoNotBlockChecking()
        {
            string id = (1L + (uint)Guid.NewGuid().GetHashCode()).ToString();
            var ranked = SongPackageSource.FromWeb(null, "https://opensekai.com/download?id=" + id);
            var entry = new SongPackage(_root, new SongPackageManifest { playLevel = 30 });
            var pending = new SongPackageItem(entry, default, true, false, false, false, default, source: ranked);
            Assert.That(pending.RankKey, Is.EqualTo("os-" + id));
            Assert.That(LibraryView.ChartInfoAccessFor(pending, false), Is.EqualTo(ChartInfoAccess.Check));
            Assert.That(LibraryView.ChartInfoAccessFor(pending, true), Is.EqualTo(ChartInfoAccess.Checking));

            RankCheck missing = ChartRankLookup.CheckAsync(ranked, entry, string.Empty).GetAwaiter().GetResult();
            Assert.That(missing.Outcome, Is.EqualTo(RankCheckOutcome.Unreadable));
            Assert.That(missing.Reason, Is.Not.Empty);
            Assert.That(LibraryView.ChartInfoAccessFor(pending, false), Is.EqualTo(ChartInfoAccess.Check));

            var elsewhere = SongPackageSource.FromWeb("https://charts.test/song/1", null);
            var unranked = new SongPackageItem(entry, default, true, true, true, false, default, "hash", elsewhere);
            var local = new SongPackageItem(entry, default, true, true, true, false, default, "hash");
            Assert.That(LibraryView.ChartInfoAccessFor(unranked, false), Is.EqualTo(ChartInfoAccess.Open));
            Assert.That(LibraryView.ChartInfoAccessFor(local, false), Is.EqualTo(ChartInfoAccess.Blocked));
            RankCheck none = ChartRankLookup.CheckAsync(elsewhere, null, "hash").GetAwaiter().GetResult();
            Assert.That(none.Outcome, Is.EqualTo(RankCheckOutcome.Known));
            Assert.That(none.Reason, Is.Null);
        }

        [Test]
        public void NetworkFailureAllowsAnImmediateRankCheckRetry()
        {
            string key = "rank-check-test-" + Guid.NewGuid().ToString("N");
            int requests = 0;
            UniTask<ChartRankCache.Verdict> Disconnect(ChartRankCache.Verdict _)
            {
                requests++;
                return UniTask.FromException<ChartRankCache.Verdict>(SonolusException.ConnectionFailure("timeout"));
            }

            for (int i = 0; i < 2; i++)
            {
                RankCheck result = ChartRankLookup.CheckRemoteAsync(key, "original", 30, Disconnect).GetAwaiter().GetResult();
                Assert.That(result.Outcome, Is.EqualTo(RankCheckOutcome.RetryLater));
                Assert.That(result.Reason, Does.Contain("timeout"));
            }
            Assert.That(requests, Is.EqualTo(2));
        }

        [TestCase("valid")]
        [TestCase("unparseable")]
        [TestCase("invalid-sonolus")]
        [TestCase("http-error")]
        public void AServerResponseStartsCooldownAndInvalidDataRecordsUnranked(string response)
        {
            string id = (1L + (uint)Guid.NewGuid().GetHashCode()).ToString();
            var source = SongPackageSource.FromWeb("https://opensekai.com/chart?id=" + id, null);
            var entry = new SongPackage(_root, new SongPackageManifest { playLevel = 30 });
            var item = new SongPackageItem(entry, default, true, true, true, false, default, "original", source);
            string key = item.RankKey;
            string rankFile = Path.Combine(UnityEngine.Application.persistentDataPath, "ChartRanks", key + ".json");
            var original = ChartRankCache.FromWeb("quality", 30, "original");
            if (response is "invalid-sonolus" or "http-error") ChartRankCache.Save(key, original);
            try
            {
                int requests = 0;
                UniTask<ChartRankCache.Verdict> Fetch(ChartRankCache.Verdict _)
                {
                    requests++;
                    return response switch
                    {
                        "unparseable" => UniTask.FromException<ChartRankCache.Verdict>(new InvalidDataException("bad data")),
                        "invalid-sonolus" => UniTask.FromException<ChartRankCache.Verdict>(SonolusException.InvalidResponse("bad JSON")),
                        "http-error" => UniTask.FromException<ChartRankCache.Verdict>(new SonolusException("server error", 503)),
                        _ => UniTask.FromResult(ChartRankCache.FromWeb("quality", 31, "updated"))
                    };
                }

                RankCheck first = ChartRankLookup.CheckRemoteAsync(key, item.ScoreKey, 30, Fetch).GetAwaiter().GetResult();
                Assert.That(first.Outcome, Is.EqualTo(response == "http-error" ? RankCheckOutcome.RetryLater : RankCheckOutcome.Known));
                if (response == "valid")
                    Assert.That(ChartRankCache.Read(key).Matches("updated", 31), Is.True);
                else if (response == "http-error")
                    Assert.That(ChartRankCache.Read(key), Is.SameAs(original));
                else
                {
                    var verdict = ChartRankCache.Read(key);
                    Assert.That(verdict.UnreadableResponse, Is.True);
                    Assert.That(verdict.ScoreKey, Is.EqualTo(item.ScoreKey));
                    Assert.That(verdict.Level, Is.EqualTo(30));
                    Assert.That(ChartRankLookup.CachedLabel(item), Is.EqualTo(ChartRankLookup.UNRANKED));
                    Assert.That(LibraryView.ChartInfoAccessFor(item, false), Is.EqualTo(ChartInfoAccess.Open));
                    Assert.That(LibraryChartInfoPanel.RankNote(item), Does.Contain("无法解析"));
                }

                RankCheck retry = ChartRankLookup.CheckRemoteAsync(key, item.ScoreKey, 30, Fetch).GetAwaiter().GetResult();
                Assert.That(retry.Outcome, Is.EqualTo(RankCheckOutcome.RetryLater));
                Assert.That(retry.Reason, Does.Contain("10 分钟后再试"));
                Assert.That(requests, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(rankFile);
            }
        }

        [TestCase(null, 0)]
        [TestCase("unknown", 30)]
        [TestCase("quality", 0)]
        public void DownloadWithUnreadableRankMetadataIsUnrankedWithDetails(string zone, int publishedLevel)
        {
            string id = (1L + (uint)Guid.NewGuid().GetHashCode()).ToString();
            var source = SongPackageSource.FromWeb("https://opensekai.com/chart?id=" + id, null);
            var entry = new SongPackage(_root, new SongPackageManifest { playLevel = 30 });
            var item = new SongPackageItem(entry, default, true, true, true, false, default, "downloaded", source);
            string rankFile = Path.Combine(UnityEngine.Application.persistentDataPath, "ChartRanks", item.RankKey + ".json");
            try
            {
                Assert.That(LibraryView.ChartInfoAccessFor(item, false), Is.EqualTo(ChartInfoAccess.Check));
                ChartRankCache.RecordWebDownload(item.RankKey, zone, publishedLevel, item.ScoreKey, entry.Manifest.playLevel);
                Assert.That(File.Exists(rankFile), Is.True);
                Assert.That(ChartRankLookup.CachedLabel(item), Is.EqualTo(ChartRankLookup.UNRANKED));
                Assert.That(LibraryView.ChartInfoAccessFor(item, false), Is.EqualTo(ChartInfoAccess.Open));
                var verdict = ChartRankCache.Read(item.RankKey);
                Assert.That(verdict.UnreadableResponse, Is.True);
                Assert.That(verdict.ScoreKey, Is.EqualTo(item.ScoreKey));
                Assert.That(verdict.Level, Is.EqualTo(30));
            }
            finally
            {
                File.Delete(rankFile);
            }
        }

        [TestCase("quality", true)]
        [TestCase("beginner", false)]
        [TestCase("abstract", false)]
        public void DetailAndDownloadMetadataAgreeOnRankedContentAndLevel(string zone, bool ranked)
        {
            string html = "<div class=\"detail-labels\"><span class=\"badge master\" style=\"color:red\">master · 34</span>"
                + "<span class=\"zone-badge " + zone + "\">标签</span></div><a href=\"/download?id=761\">下载</a>";
            var verdict = ChartRankCache.ParseDetail(html, "761");
            Assert.That(verdict.Ranked, Is.EqualTo(ranked));
            Assert.That(verdict.Level, Is.EqualTo(34));
            Assert.That(ChartRankCache.ParseDetail(html, "99"), Is.Null);
            Assert.That(ChartRankCache.FromWeb("unknown", 34, "file"), Is.Null);
            var snapshot = ChartRankCache.FromWeb(zone, 34, "file");
            Assert.That(snapshot.Matches("file", 34), Is.EqualTo(ranked));
            Assert.That(snapshot.Matches("edited", 34), Is.False);
            Assert.That(snapshot.Matches("file", 38), Is.False);
        }

        [TestCase("https://sonolus.sbuga.com", true)]
        [TestCase("https://sonolus.sbuga.com:443", true)]
        [TestCase("https://sonolus.sbuga.com/en", true)]
        [TestCase("https://sonolus.sbuga.com:444", false)]
        [TestCase("https://sonolus.sbuga.com.other.test", false)]
        [TestCase("https://untitledcharts.com", false)]
        public void SbugaRanksUntaggedChartsFromThePublisher(string url, bool expected)
        {
            var server = SonolusServerAddress.Parse(url);
            var level = SonolusLevel.From(Newtonsoft.Json.Linq.JObject.FromObject(new { name = "song/expert", rating = 30 }));
            var verdict = ChartRankCache.FromLevel(server, level, "original");
            Assert.That(verdict.Matches("original", 30), Is.EqualTo(expected));
            Assert.That(verdict.Matches("edited", 30), Is.False);
            Assert.That(verdict.Matches("original", 31), Is.False);
            string key = ChartRankCache.SonolusKey(server, level.Name);
            if (expected) Assert.That(key, Does.Match("^sbuga-[0-9a-f]{64}$"));
            else Assert.That(key, Is.Null);
        }

        [TestCase("Staff Pick", "trophy", true)]
        [TestCase("", "trophy", true)]
        [TestCase("Staff Pick", "tag", false)]
        public void StaffPickUsesThePublisherTag(string title, string icon, bool picked)
        {
            const string NAME = "UnCh-1acf28f11b564a13a3d13bae634e92dc";
            var level = SonolusLevel.From(Newtonsoft.Json.Linq.JObject.FromObject(new { name = NAME, tags = new[] { new { title, icon } } }));
            Assert.That(ChartRankCache.IsStaffPick(level), Is.EqualTo(picked));
            SonolusServerAddress.TryParse("https://untitledcharts.com", out var server, out _, out _);
            Assert.That(ChartRankCache.UntitledKey(server, NAME), Is.EqualTo(NAME));
            SonolusServerAddress.TryParse("https://untitledcharts.com/community", out server, out _, out _);
            Assert.That(ChartRankCache.UntitledKey(server, NAME), Is.Null);
        }
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "server-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }
        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

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

        [Test]
        public void ExtractsUtf8AndNestedFiles()
        {
            string stage = Path.Combine(_root, "stage");
            float progress = 0;
            SafeZipExtractor.Extract(Archive("谱面/音乐.txt", "音符"), stage, p => progress = p);
            Assert.That(File.ReadAllText(Path.Combine(stage, "谱面", "音乐.txt")), Is.EqualTo("音符"));
            Assert.That(progress, Is.EqualTo(1f));
        }

        [Test]
        public void InsufficientSpaceRejectsBeforeWritingArchiveContents()
        {
            string zip = Archive("score.json");
            string stage = Path.Combine(_root, "stage");
            var error = Assert.Throws<IOException>(() => SafeZipExtractor.Extract(zip, stage,
                availableBytes: SafeZipExtractor.FREE_SPACE_RESERVE_BYTES));
            Assert.That(error.Message, Does.Contain("存储空间不足"));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [Test]
        public void ExtractsWithCapturedFreeSpaceWithoutQueryingTheWorkerFilesystem()
        {
            string zip = Archive("score.json");
            string stage = Path.Combine(_root, "stage");
            SafeZipExtractor.Extract(zip, stage, availableBytes: SafeZipExtractor.FREE_SPACE_RESERVE_BYTES + 4);
            Assert.That(File.ReadAllText(Path.Combine(stage, "score.json")), Is.EqualTo("test"));
        }

        [Test]
        public void RejectsSymbolicLinks()
        {
            string zip = Path.Combine(_root, "link.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                archive.CreateEntry("link").ExternalAttributes = unchecked((int)0xA1FF0000);
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, Path.Combine(_root, "stage")));
        }

        [Test]
        public void RejectsTooManyFilesBeforeExtracting()
        {
            string zip = Path.Combine(_root, "many.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                for (int i = 0; i <= SafeZipExtractor.MAX_FILES; i++) archive.CreateEntry(i + ".txt");
            string stage = Path.Combine(_root, "stage");
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, stage));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [Test]
        public void RejectsOversizedDeclaredFileBeforeExtracting()
        {
            string zip = Archive("large.bin");
            byte[] bytes = File.ReadAllBytes(zip);
            for (int i = 0; i < bytes.Length - 28; i++)
            {
                if (BitConverter.ToUInt32(bytes, i) != 0x02014b50) continue;
                BitConverter.GetBytes((uint)(SafeZipExtractor.MAX_FILE_BYTES + 1)).CopyTo(bytes, i + 24);
                break;
            }
            File.WriteAllBytes(zip, bytes);
            string stage = Path.Combine(_root, "stage");
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Extract(zip, stage));
            Assert.That(Directory.GetFiles(stage), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RejectsManifestReferencesOutsideThePackage()
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

        [UnityTest]
        public IEnumerator CompletedDownloadCanBeRecoveredWithoutDuplicateImport()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"), Path.Combine(_root, "temp"));
            var original = manager.CreateNewEntry();
            string zip = manager.ExportZip(original, Path.Combine(_root, "package.zip"));
            var firstTask = manager.ImportDownloadedZipAsync(zip, "42");
            while (!firstTask.IsCompleted) yield return null;
            var first = firstTask.GetAwaiter().GetResult();
            Assert.That(SongPackageSource.Read(first), Is.Null);
            // Simulate process death after publishing but before acknowledging the download.
            var restarted = new SongPackageManager(manager.RootDirectory);
            const string PAGE = "https://charts.test/song?id=歌#downloads";
            const string DOWNLOAD = "https://cdn.test/package.zip?signature=Ab%2FC";
            var recoveredTask = restarted.ImportDownloadedZipAsync(zip, "42", source: SongPackageSource.FromWeb(PAGE, DOWNLOAD));
            while (!recoveredTask.IsCompleted) yield return null;
            var recovered = recoveredTask.GetAwaiter().GetResult();
            Assert.That(recovered.RootDirectory, Is.EqualTo(first.RootDirectory));
            var source = SongPackageSource.Read(recovered);
            Assert.That(source.PageUrl, Is.EqualTo(PAGE));
            Assert.That(source.DownloadUrl, Is.EqualTo(DOWNLOAD));
            Assert.That(restarted.LoadItems().Count, Is.EqualTo(2));
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DownloadedNativeChartUsesTheFormatDeclaredInItsManifest()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            string zip = Path.Combine(_root, "native.zip");
            const string SCORE_NAME = "level-data.json.gz";
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using (var manifest = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                    manifest.Write(@"{""id"":""next-rush-demo"",""chartFormat"":""next-rush"",""scoreFileName"":""level-data.json.gz""}");
                using (var compressed = new GZipStream(archive.CreateEntry(SCORE_NAME).Open(), CompressionMode.Compress))
                using (var score = new StreamWriter(compressed))
                    score.Write(@"{""bgmOffset"":0,""entities"":[]}");
            }

            var task = manager.ImportDownloadedZipAsync(zip, "45", source: SongPackageSource.FromWeb(
                "https://charts.test/chart/1", "https://cdn.test/1.zip?token=abc"));
            while (!task.IsCompleted) yield return null;
            var installed = task.GetAwaiter().GetResult();

            Assert.That(installed.Manifest.chartFormat, Is.EqualTo("next-rush"));
            Assert.That(Path.GetFileName(installed.ScorePath), Is.EqualTo(SCORE_NAME));
            Assert.That(File.Exists(installed.ScorePath), Is.True);
            Assert.That(SongPackageSource.Read(installed).DownloadUrl, Is.EqualTo("https://cdn.test/1.zip?token=abc"));
            string exported = manager.ExportZip(installed, Path.Combine(_root, "exported.zip"));
            using (var archive = ZipFile.OpenRead(exported))
            {
                Assert.That(archive.GetEntry("web-source.json"), Is.Null);
                Assert.That(archive.GetEntry(SCORE_NAME), Is.Not.Null);
            }
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }

        [Test]
        public void ExistingSonolusRecordKeepsItsServerAndLevelLinks()
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            var package = manager.CreateNewEntry();
            string file = Path.Combine(package.RootDirectory, "sonolus-source.json");
            const string JSON = "{\"server\":\"https://charts.test/pack\",\"name\":\"song / 1\"}";
            File.WriteAllText(file, JSON);
            var source = SongPackageSource.Read(package);
            Assert.That(source.SonolusServerUrl, Is.EqualTo("https://charts.test/pack"));
            Assert.That(source.SonolusLevelUrl, Is.EqualTo("https://charts.test/pack/levels/song%20%2F%201"));
            Assert.That(File.ReadAllText(file), Is.EqualTo(JSON));
        }

        [UnityTest]
        public IEnumerator InvalidDownloadLeavesExistingLibraryUntouched()
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

        [TestCase("sus")]
        [TestCase("txt")]
        public async System.Threading.Tasks.Task DownloadWithNoPlayableSusNotesIsNotPublished(string extension)
        {
            var manager = new SongPackageManager(Path.Combine(_root, "library"));
            var existing = manager.CreateNewEntry();
            byte[] originalManifest = File.ReadAllBytes(existing.ManifestPath);
            byte[] originalScore = File.ReadAllBytes(existing.ScorePath);
            byte[] originalAudio = { 0x00, 0x7F, 0x80, 0xFF };
            File.WriteAllBytes(existing.AudioPath, originalAudio);
            string filename = "empty." + extension;
            string zip = Archive("manifest.json", "{\"id\":\"empty\",\"scoreFileName\":\"" + filename + "\"}");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
            using (var score = new StreamWriter(archive.CreateEntry(filename).Open()))
                score.Write("#TITLE \"Empty\"\n#BPM01:120\n#00008:01");

            try
            {
                await manager.ImportDownloadedZipAsync(zip, "46");
                Assert.Fail("Expected the downloaded score to be rejected.");
            }
            catch (InvalidDataException error) { Assert.That(error.Message, Does.Contain("没有可游玩的音符")); }
            var items = manager.RescanItems();
            Assert.That(items.Count, Is.EqualTo(1));
            var retained = items[0].Entry;
            Assert.That(retained.RootDirectory, Is.EqualTo(existing.RootDirectory));
            Assert.That(File.ReadAllBytes(retained.ManifestPath), Is.EqualTo(originalManifest));
            Assert.That(File.ReadAllBytes(retained.ScorePath), Is.EqualTo(originalScore));
            Assert.That(File.ReadAllBytes(retained.AudioPath), Is.EqualTo(originalAudio));
            Assert.That(Directory.Exists(Path.Combine(manager.RootDirectory, "server-46")), Is.False);
            Assert.That(Directory.GetDirectories(_root, ".chart-import-*"), Is.Empty);
        }
    }
}
