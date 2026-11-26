using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// The library list is served from a cache saved in the library root: changes made through the
    /// manager go straight in, and changes made behind its back only show after a rescan.
    /// </summary>
    public class SongLibraryCacheTests
    {
        private string _root;
        private string _library;
        private SongPackageManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            _library = Path.Combine(_root, "library");
            Directory.CreateDirectory(_library);
            _manager = new SongPackageManager(_library);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static ChartDocument OneNoteDocument(int lane)
        {
            var document = new ChartDocument { MusicScoreTicksMax = 1920 };
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            document.NoteList.Add(new ChartNote(1, 0, lane, 0, NoteCategory.Normal));
            return document;
        }

        private static void RetitleOnDisk(SongPackage entry, string title)
        {
            SongPackageManifest manifest = JsonConvert.DeserializeObject<SongPackageManifest>(File.ReadAllText(entry.ManifestPath));
            manifest.title = title;
            File.WriteAllText(entry.ManifestPath, JsonConvert.SerializeObject(manifest));
        }

        [Test]
        public void ImportingTheSameChartAgainSharesItsScoreKey()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, OneNoteDocument(0));
            string archive = Path.Combine(_root, "package.oskp");
            _manager.ExportZip(entry, archive);
            _manager.RescanItems();

            _manager.ImportZip(archive);
            _manager.ImportZip(archive);

            var keys = _manager.LoadItems().Select(item => item.ScoreKey).ToArray();
            Assert.That(keys, Has.Length.EqualTo(3));
            Assert.That(keys, Has.All.Not.Empty);
            Assert.That(keys.Distinct().Count(), Is.EqualTo(1), "scores follow the chart, not the import");
        }

        [Test]
        public void TheCachedKeyIsTheContentHashAndPlaybackReadsIt()
        {
            SongPackage entry = _manager.CreateNewEntry();

            SongPackageItem item = _manager.LoadItems().Single();

            Assert.That(item.ScoreKey, Is.EqualTo(SongPackageHash.Compute(entry.ScorePath)));
            Assert.That(_manager.ScoreKeyOf(entry), Is.EqualTo(item.ScoreKey));
            Assert.That(_manager.FindCachedItem(entry.RootDirectory), Is.SameAs(item));
        }

        [Test]
        public void ChangesMadeOutsideTheManagerOnlyShowAfterARescan()
        {
            SongPackage entry = _manager.CreateNewEntry();
            string before = _manager.LoadItems().Single().ScoreKey;

            RetitleOnDisk(entry, "Edited Elsewhere");
            Directory.CreateDirectory(Path.Combine(_library, "dropped-in"));
            File.WriteAllText(Path.Combine(_library, "dropped-in", SongPackageStorage.MANIFEST_FILE_NAME), "{\"id\":\"dropped\"}");

            Assert.That(_manager.LoadItems().Single().Entry.Manifest.title, Is.Not.EqualTo("Edited Elsewhere"));

            var rescanned = _manager.RescanItems();
            Assert.That(rescanned.Count, Is.EqualTo(2));
            SongPackageItem edited = rescanned.Single(item => item.Entry.Manifest.id == entry.Manifest.id);
            Assert.That(edited.Entry.Manifest.title, Is.EqualTo("Edited Elsewhere"));
            Assert.That(edited.ScoreKey, Is.EqualTo(before), "editing a package's info keeps its scores");
            Assert.That(rescanned.Single(item => item.Entry.Manifest.id == "dropped").ScoreKey, Is.Empty,
                "a package without a chart has no key to share");
        }

        [Test]
        public void ANewLaunchReadsTheSnapshotInsteadOfTheFolders()
        {
            SongPackage entry = _manager.CreateNewEntry();
            string title = _manager.LoadItems().Single().Entry.Manifest.title;
            RetitleOnDisk(entry, "Edited Elsewhere");

            var relaunched = new SongPackageManager(_library);

            Assert.That(File.Exists(Path.Combine(_library, SongLibraryCache.FILE_NAME)), Is.True);
            Assert.That(relaunched.LoadItems().Single().Entry.Manifest.title, Is.EqualTo(title));
            Assert.That(relaunched.RescanItems().Single().Entry.Manifest.title, Is.EqualTo("Edited Elsewhere"));
        }

        [Test]
        public void VersionThreeBackfillsSourcesOnceAndKeepsScoreKeysAndLoudness()
        {
            SongPackage entry = EntryWithAudio();
            var original = _manager.LoadItems().Single();
            _manager.SetLoudness(original, -9f);
            _manager.SaveLoudness();
            string path = Path.Combine(_library, SongLibraryCache.FILE_NAME);
            var legacy = JObject.Parse(File.ReadAllText(path));
            legacy["version"] = 3;
            File.WriteAllText(path, legacy.ToString());
            SongPackageSource.FromWeb("https://opensekai.com/chart?id=751", "https://cdn.test/song.zip")
                .WriteWeb(entry.RootDirectory);
            File.WriteAllText(entry.ScorePath, "externally changed");

            var migrated = new SongPackageManager(_library).LoadItems().Single();
            Assert.That(migrated.RankKey, Is.EqualTo("os-751"));
            Assert.That(migrated.LoudnessLufs, Is.EqualTo(-9f));
            Assert.That(migrated.ScoreKey, Is.EqualTo(original.ScoreKey));
            File.Delete(Path.Combine(entry.RootDirectory, SongPackageSource.WEB_FILE_NAME));

            var relaunched = new SongPackageManager(_library);
            Assert.That(relaunched.LoadItems().Single().RankKey, Is.EqualTo("os-751"));
            Assert.That(relaunched.RescanItems().Single().RankKey, Is.Null);
        }

        [Test]
        public void ChangesThroughTheManagerGoStraightIntoTheCacheAndTheSnapshot()
        {
            SongPackage entry = _manager.CreateNewEntry();
            _manager.LoadItems();
            SongPackageManifest manifest = JsonConvert.DeserializeObject<SongPackageManifest>(
                JsonConvert.SerializeObject(entry.Manifest));
            manifest.title = "Renamed";

            SongPackage renamed = _manager.SaveManifest(entry, manifest);

            SongPackageItem cached = _manager.LoadItems().Single();
            Assert.That(cached.Entry.RootDirectory, Is.EqualTo(renamed.RootDirectory));
            Assert.That(cached.Entry.Manifest.title, Is.EqualTo("Renamed"));
            Assert.That(new SongPackageManager(_library).LoadItems().Single().Entry.Manifest.title, Is.EqualTo("Renamed"));

            _manager.DeleteEntry(renamed);

            Assert.That(_manager.LoadItems(), Is.Empty);
            Assert.That(new SongPackageManager(_library).LoadItems(), Is.Empty);
        }

        [Test]
        public void ReplacingTheChartGivesANewKeyAtOnce()
        {
            SongPackage entry = _manager.CreateNewEntry();
            string before = _manager.LoadItems().Single().ScoreKey;
            string source = Path.Combine(_root, "other");
            Directory.CreateDirectory(source);
            var other = new SongPackage(source, new SongPackageManifest { id = "other", scoreFileName = "score.json" });
            ChartFileReader.Write(other, OneNoteDocument(3));

            _manager.ReplaceScoreFile(entry, other.ScorePath);

            Assert.That(_manager.LoadItems().Single().ScoreKey, Is.Not.EqualTo(before));
        }

        [Test]
        public void SummariesAreGroupedByScoreKeyAndIgnoreRecordsWithoutOne()
        {
            string[] lines =
            {
                "{\"scoreKey\":\"a\",\"customMusicScoreId\":\"same\",\"totalScore\":100}",
                "{\"scoreKey\":\"b\",\"customMusicScoreId\":\"same\",\"totalScore\":200}",
                "{\"scoreKey\":\"a\",\"customMusicScoreId\":\"same\",\"totalScore\":300}",
                "{\"customMusicScoreId\":\"same\",\"totalScore\":999}"
            };

            var summaries = CustomMusicScorePlayHistoryStorage.Summarize(lines);

            Assert.That(summaries.Keys, Is.EquivalentTo(new[] { "a", "b" }));
            Assert.That(summaries["a"].Best.TotalScore, Is.EqualTo(300));
            Assert.That(summaries["a"].PlayCount, Is.EqualTo(2));
            Assert.That(summaries["b"].Best.TotalScore, Is.EqualTo(200));
        }

        private SongPackage EntryWithAudio(string bytes = "audio")
        {
            SongPackage entry = _manager.CreateNewEntry();
            File.WriteAllText(entry.AudioPath, bytes);
            _manager.RescanItems();
            return entry;
        }

        [Test]
        public void MeasuredLoudnessIsSavedAndReadBackOnTheNextLaunch()
        {
            EntryWithAudio();
            SongPackageItem item = _manager.ItemsWithoutLoudness().Single();

            Assert.That(_manager.SetLoudness(item, -9.5f), Is.True);
            _manager.SaveLoudness();

            Assert.That(_manager.ItemsWithoutLoudness(), Is.Empty);
            SongPackageItem relaunched = new SongPackageManager(_library).LoadItems().Single();
            Assert.That(relaunched.LoudnessMeasured, Is.True);
            Assert.That(relaunched.LoudnessLufs, Is.EqualTo(-9.5f));
        }

        [Test]
        public void ASilentSongCountsAsMeasured()
        {
            EntryWithAudio();
            _manager.SetLoudness(_manager.ItemsWithoutLoudness().Single(), null);
            _manager.SaveLoudness();

            Assert.That(new SongPackageManager(_library).ItemsWithoutLoudness(), Is.Empty);
        }

        [Test]
        public void AChangeThroughTheManagerKeepsLoudnessButARescanMeasuresEverySongAgain()
        {
            SongPackage entry = EntryWithAudio();
            _manager.SetLoudness(_manager.ItemsWithoutLoudness().Single(), -8f);
            SongPackageManifest manifest = JsonConvert.DeserializeObject<SongPackageManifest>(
                JsonConvert.SerializeObject(entry.Manifest));
            manifest.title = "Renamed";

            _manager.SaveManifest(entry, manifest);

            Assert.That(_manager.LoadItems().Single().LoudnessLufs, Is.EqualTo(-8f), "incremental: the audio is unchanged");

            _manager.RescanItems();

            Assert.That(_manager.ItemsWithoutLoudness().Count, Is.EqualTo(1), "a refresh measures everything again");
        }

        [Test]
        public void ReplacingTheAudioMeasuresItAgain()
        {
            SongPackage entry = EntryWithAudio();
            _manager.SetLoudness(_manager.ItemsWithoutLoudness().Single(), -8f);
            string replacement = Path.Combine(_root, "other.ogg");
            File.WriteAllText(replacement, "different audio");

            _manager.ReplaceAudioFile(entry, replacement);

            Assert.That(_manager.ItemsWithoutLoudness().Count, Is.EqualTo(1));
        }

        [Test]
        public void AnImportLeavesOnlyTheNewSongUnmeasured()
        {
            SongPackage entry = EntryWithAudio();
            _manager.SetLoudness(_manager.ItemsWithoutLoudness().Single(), -8f);
            string archive = Path.Combine(_root, "package.oskp");
            _manager.ExportZip(entry, archive);

            SongPackage imported = _manager.ImportZip(archive);

            SongPackageItem pending = _manager.ItemsWithoutLoudness().Single();
            Assert.That(pending.Entry.RootDirectory, Is.EqualTo(imported.RootDirectory));
        }

        [Test]
        public void AMeasurementForAPackageRescannedMeanwhileIsDropped()
        {
            EntryWithAudio();
            SongPackageItem stale = _manager.ItemsWithoutLoudness().Single();
            _manager.RescanItems();

            Assert.That(_manager.SetLoudness(stale, -8f), Is.False);
            Assert.That(_manager.ItemsWithoutLoudness().Count, Is.EqualTo(1));
        }
    }
}
