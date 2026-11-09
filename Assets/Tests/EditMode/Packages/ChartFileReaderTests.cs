using System;
using System.IO;
using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// Writing and reading a chart is the round trip every import goes through, so it is exercised
    /// against a real temporary folder rather than a mock.
    /// </summary>
    public class ChartFileReaderTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private SongPackage Package() => new SongPackage(_root, new SongPackageManifest
        {
            title = "Round Trip",
            id = "roundtrip",
            scoreFileName = "score.json"
        });

        private static ChartDocument DocumentWithTwoNotes()
        {
            var document = new ChartDocument { MusicScoreTicksMax = 1920 };
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            document.NoteList.Add(new ChartNote(id: 1, ticks: 0, laneStart: 0, laneEnd: 0, NoteCategory.Normal));
            document.NoteList.Add(new ChartNote(id: 2, ticks: 960, laneStart: 2, laneEnd: 2,
                NoteCategory.Flick, NoteType.Critical));
            return document;
        }

        [Test]
        public void WriteCreatesTheScoreFile()
        {
            SongPackage package = Package();

            ChartFileReader.Write(package, DocumentWithTwoNotes());

            Assert.That(File.Exists(package.ScorePath), Is.True, package.ScorePath);
            Assert.That(new FileInfo(package.ScorePath).Length, Is.GreaterThan(0));
        }

        [Test]
        public void RoundTripKeepsNotesAndEvents()
        {
            SongPackage package = Package();
            ChartFileReader.Write(package, DocumentWithTwoNotes());

            ChartDocument loaded = ChartFileReader.Read(package);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.NoteList, Has.Count.EqualTo(2));
            Assert.That(loaded.MusicScoreEventDataList, Has.Count.EqualTo(1));
            Assert.That(loaded.NoteList[0].id, Is.EqualTo(1));
            Assert.That(loaded.NoteList[1].ticks, Is.EqualTo(960L));
            Assert.That(loaded.NoteList[1].category, Is.EqualTo(NoteCategory.Flick));
            Assert.That(loaded.NoteList[1].type, Is.EqualTo(NoteType.Critical));
            Assert.That(loaded.MusicScoreTicksMax, Is.EqualTo(1920L));
        }

        [Test]
        public void RoundTripStampesThePackageMusicId()
        {
            SongPackage package = Package();
            ChartFileReader.Write(package, DocumentWithTwoNotes());

            ChartDocument loaded = ChartFileReader.Read(package);

            Assert.That(loaded.MusicId, Is.EqualTo(package.MusicId));
        }

        [Test]
        public void WrittenDocumentIsMigrated()
        {
            SongPackage package = Package();
            ChartFileReader.Write(package, DocumentWithTwoNotes());

            ChartDocument loaded = ChartFileReader.Read(package);

            Assert.That(loaded.VersionCode, Is.EqualTo(ChartDocument.CURRENT_VERSION));
        }

        [Test]
        public void WriteDoesNotMutateTheSourceDocument()
        {
            ChartDocument document = DocumentWithTwoNotes();
            int versionBefore = document.VersionCode;
            SongPackage package = Package();

            ChartFileReader.Write(package, document);

            Assert.That(document.MusicId, Is.EqualTo(0), "the copy gets the music id, not the caller's document");
            Assert.That(document.VersionCode, Is.EqualTo(versionBefore));
            Assert.That(document.NoteList, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReadingAMissingFileReturnsNull()
        {
            Assert.That(ChartFileReader.Read(Package()), Is.Null);
        }

        [Test]
        public void ReadingNullPackageReturnsNull()
        {
            Assert.That(ChartFileReader.Read(null), Is.Null);
        }

        [Test]
        public void WritingNullArgumentsIsANoOp()
        {
            Assert.That(() => ChartFileReader.Write(null, DocumentWithTwoNotes()), Throws.Nothing);
            Assert.That(() => ChartFileReader.Write(Package(), null), Throws.Nothing);
        }

        [Test]
        public void UnsupportedExtensionIsRejected()
        {
            string path = Path.Combine(_root, "score.xyz");
            File.WriteAllText(path, "{}");

            Assert.That(() => ChartFileReader.ReadFile(path, 0), Throws.TypeOf<InvalidDataException>());
        }
    }
}
