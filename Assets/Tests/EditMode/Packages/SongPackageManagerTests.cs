using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// Exporting a package and importing it back is the user-facing backup path, so it is exercised
    /// with a real zip in a temporary folder.
    /// </summary>
    public class SongPackageManagerTests
    {
        private string _root;
        private SongPackageManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _manager = new SongPackageManager(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static ChartDocument TwoNoteDocument()
        {
            var document = new ChartDocument { MusicScoreTicksMax = 1920 };
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            document.NoteList.Add(new ChartNote(1, 0, 0, 0, NoteCategory.Normal));
            document.NoteList.Add(new ChartNote(2, 960, 1, 1, NoteCategory.Flick, NoteType.Critical));
            return document;
        }

        [Test]
        public void CreateNewEntryWritesAFolderAndManifest()
        {
            SongPackage entry = _manager.CreateNewEntry();

            Assert.That(entry, Is.Not.Null);
            Assert.That(Directory.Exists(entry.RootDirectory), Is.True);
            Assert.That(File.Exists(entry.ManifestPath), Is.True);
            Assert.That(entry.Manifest.id, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void LoadItemsFindsTheCreatedEntry()
        {
            SongPackage entry = _manager.CreateNewEntry();

            var items = _manager.LoadItems();

            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Entry.Manifest.id, Is.EqualTo(entry.Manifest.id));
        }

        [Test]
        public void DeleteEntryRemovesTheFolder()
        {
            SongPackage entry = _manager.CreateNewEntry();

            _manager.DeleteEntry(entry);

            Assert.That(Directory.Exists(entry.RootDirectory), Is.False);
            Assert.That(_manager.LoadItems(), Is.Empty);
        }

        [Test]
        public void DuplicateEntryGetsANewIdAndKeepsTheChart()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, TwoNoteDocument());

            SongPackage copy = _manager.DuplicateEntry(entry);

            Assert.That(copy.Manifest.id, Is.Not.EqualTo(entry.Manifest.id));
            Assert.That(ChartFileReader.Read(copy).NoteList, Has.Count.EqualTo(2));
            Assert.That(_manager.LoadItems().Count, Is.EqualTo(2));
        }

        [Test]
        public void ExportWritesAZipFile()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, TwoNoteDocument());
            string destination = Path.Combine(_root, "export.oskp");

            string written = _manager.ExportZip(entry, destination);

            Assert.That(written, Is.EqualTo(destination));
            Assert.That(File.Exists(written), Is.True);
            Assert.That(new FileInfo(written).Length, Is.GreaterThan(0));
        }

        [Test]
        public void ExportThenImportKeepsTheNotesAndTitle()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, TwoNoteDocument());
            string title = entry.Manifest.title;
            string destination = Path.Combine(_root, "roundtrip.oskp");
            _manager.ExportZip(entry, destination);

            SongPackage imported = _manager.ImportZip(destination);

            Assert.That(imported, Is.Not.Null);
            Assert.That(imported.Manifest.title, Is.EqualTo(title));
            ChartDocument chart = ChartFileReader.Read(imported);
            Assert.That(chart, Is.Not.Null);
            Assert.That(chart.NoteList, Has.Count.EqualTo(2));
            Assert.That(chart.NoteList[1].type, Is.EqualTo(NoteType.Critical));
            Assert.That(_manager.LoadItems().Count, Is.EqualTo(2), "the import adds a second entry");
        }

        [Test]
        public void ExportedArchiveCarriesThePackageFiles()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, TwoNoteDocument());
            string destination = Path.Combine(_root, "archive.oskp");
            _manager.ExportZip(entry, destination);

            using (ZipArchive archive = ZipFile.OpenRead(destination))
            {
                string[] names = archive.Entries.Select(entry2 => entry2.FullName).ToArray();
                Assert.That(names, Has.Some.EndsWith("manifest.json"));
                Assert.That(names, Has.Some.EndsWith("score.json"));
            }
        }

        [Test]
        public void ImportingACopyGetsItsOwnIdAndFolder()
        {
            SongPackage entry = _manager.CreateNewEntry();
            ChartFileReader.Write(entry, TwoNoteDocument());
            string destination = Path.Combine(_root, "roundtrip.oskp");
            _manager.ExportZip(entry, destination);

            SongPackage imported = _manager.ImportZip(destination);

            Assert.That(imported, Is.Not.Null);
            Assert.That(imported.MusicId, Is.EqualTo(entry.MusicId), "the manifest id travels with the package");
            Assert.That(imported.RootDirectory, Is.Not.EqualTo(entry.RootDirectory));
            Assert.That(File.Exists(imported.ScorePath), Is.True);
        }
        [Test]
        public void ImportingSomethingThatIsNotAZipThrowsInvalidData()
        {
            string bogus = Path.Combine(_root, "not-a-package.oskp");
            File.WriteAllText(bogus, "this is not a zip");

            // The library UI turns this into "不是有效的曲包文件"; the manager itself stays honest.
            Assert.That(() => _manager.ImportZip(bogus), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void ImportingAMissingFileReturnsNull()
        {
            Assert.That(_manager.ImportZip(Path.Combine(_root, "missing.oskp")), Is.Null);
        }
    }
}
