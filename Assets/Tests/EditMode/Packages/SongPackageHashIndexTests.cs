using System;
using System.IO;
using System.Linq;
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
    /// Play history is keyed by the content hash of manifest.json + the score file, so packages
    /// that share a manifest id must still get scores of their own.
    /// </summary>
    public class SongPackageHashIndexTests
    {
        private string _root;
        private SongPackageManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _manager = new SongPackageManager(Path.Combine(_root, "library"));
            Directory.CreateDirectory(_manager.RootDirectory);
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

        [Test]
        public void ImportingTheSameArchiveTwiceGivesTwoScoreKeys()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, OneNoteDocument(0));
            string archive = Path.Combine(_root, "package.oskp");
            _manager.ExportZip(entry, archive);

            SongPackage first = _manager.ImportZip(archive);
            SongPackage second = _manager.ImportZip(archive);

            Assert.That(first.Manifest.id, Is.EqualTo(second.Manifest.id), "the manifest id is kept on import");
            var keys = _manager.LoadItems().Select(item => item.ScoreKey).ToArray();
            Assert.That(keys, Has.Length.EqualTo(3));
            Assert.That(keys, Has.All.Not.Empty);
            Assert.That(keys.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TheKeyIsStoredAndReusedWhileTheFilesStayTheSame()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, OneNoteDocument(0));

            string key = _manager.LoadItems().Single().ScoreKey;

            Assert.That(File.Exists(Path.Combine(_manager.RootDirectory, SongPackageHashIndex.FILE_NAME)), Is.True);
            Assert.That(_manager.LoadItems().Single().ScoreKey, Is.EqualTo(key));
            Assert.That(SongPackageHashIndex.HashOf(entry), Is.EqualTo(key), "playback reads the same key");
            Assert.That(key, Is.EqualTo(SongPackageHashIndex.ComputeHash(entry.ManifestPath, entry.ScorePath)));
        }

        [Test]
        public void ChangingTheChartGivesANewKey()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, OneNoteDocument(0));
            string before = _manager.LoadItems().Single().ScoreKey;

            ChartFileReader.Write(entry, OneNoteDocument(3));
            File.SetLastWriteTimeUtc(entry.ScorePath, DateTime.UtcNow.AddMinutes(1));

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
            Assert.That(summaries["a"].Best.totalScore, Is.EqualTo(300));
            Assert.That(summaries["a"].PlayCount, Is.EqualTo(2));
            Assert.That(summaries["b"].Best.totalScore, Is.EqualTo(200));
        }
    }
}
