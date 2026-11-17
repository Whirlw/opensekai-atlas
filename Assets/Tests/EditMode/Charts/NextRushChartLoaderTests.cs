using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using OpenSekai.Tests.EditMode.Live;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Charts
{
    public sealed class NextRushChartLoaderTests
    {
        private LiveBundleBuildData _config;
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            Assert.That(_config, Is.Not.Null);
            _directory = Path.Combine(Path.GetTempPath(), "OpenSekai-NextRush-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }
        [TearDown]
        public void TearDown() { Directory.Delete(_directory, true); }

        private static JObject V(string name, double value) => new JObject { ["name"] = name, ["value"] = value };
        private static JObject R(string name, string value) => new JObject { ["name"] = name, ["ref"] = value };
        private static JObject E(string archetype, string name, params JObject[] data)
        {
            var e = new JObject { ["archetype"] = archetype, ["data"] = new JArray(data) };
            if (name != null) e["name"] = name;
            return e;
        }
        private static JObject N(string type, string name, double beat, double lane = 0, double size = 1)
            => E(type, name, V("#BEAT", beat), V("lane", lane), V("size", size));
        private static void Add(JObject entity, params JObject[] fields) { foreach (var field in fields) ((JArray)entity["data"]).Add(field); }
        private static JObject Chart(params JObject[] entities)
        {
            var data = new JArray { E("Initialization", null, V("revision", 2)), E("#BPM_CHANGE", null, V("#BEAT", 0), V("#BPM", 120)) };
            foreach (var entity in entities) data.Add(entity);
            return new JObject { ["bgmOffset"] = 0, ["entities"] = data };
        }
        private RuntimeChartDefinition Load(JObject json, bool pairs = true) => NextRushChartLoader.Load(NextRushChart.Parse(json.ToString()), _config, pairs);

        private static JObject Hold()
        {
            var head = N("NormalHeadTapNote", "head", 1, -2, 1);
            Add(head, R("next", "middle"));
            var middle = N("NormalTickNote", "middle", 2, 0, 1);
            Add(middle, R("next", "tail"), R("activeHead", "head"));
            var tail = N("NormalTailReleaseNote", "tail", 3, 2, 1);
            Add(tail, R("activeHead", "head"));
            var connector1 = E("Connector", null, R("head", "head"), R("tail", "tail"), R("segmentHead", "head"), R("segmentTail", "middle"), R("activeHead", "head"), R("activeTail", "tail"));
            var connector2 = E("Connector", null, R("head", "head"), R("tail", "tail"), R("segmentHead", "middle"), R("segmentTail", "tail"), R("activeHead", "head"), R("activeTail", "tail"));
            var hidden = E("TransientHiddenTickNote", null, V("#BEAT", 1.5), V("isAttached", 1), R("attachHead", "head"), R("attachTail", "middle"));
            // Deliberately shuffled: names are references, never array indices or hexadecimal IDs.
            return Chart(tail, hidden, connector2, head, connector1, middle);
        }

        [Test]
        public void NativeRuntimeHasNoLegacyDocumentAndKeepsTheSource()
        {
            var json = Chart(N("NormalTapNote", "tap", 4));
            var source = NextRushChart.Parse(json.ToString());
            var result = NextRushChartLoader.Load(source, _config);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.NextRush, Is.SameAs(source));
            Assert.That(result.HasPlayableNotes, Is.True);
            Assert.That(result.TotalNoteCount, Is.EqualTo(1));
            Assert.That(result.TicksMax, Is.EqualTo(1920));
            Assert.That(result.Score.NoteArray[0].MusicScoreInfo.time, Is.EqualTo(2).Within(0.0001));
        }

        [TestCase(-5.5, 0.5, 0, 0)]
        [TestCase(5.5, 0.5, 11, 11)]
        [TestCase(0, 1, 5, 6)]
        [TestCase(0.25, 0.75, 5.5, 6)]
        public void GeometryPreservesNativeFractionalBoundaries(double lane, double size, double left, double right)
        {
            var note = Load(Chart(N("NormalTapNote", null, 1, lane, size))).Score.NoteArray.Single();
            Assert.That(note.LaneStartF, Is.EqualTo(left));
            Assert.That(note.LaneEndF, Is.EqualTo(right));
        }

        [Test]
        public void BpmChangesDriveNativeBeatTiming()
        {
            var result = Load(Chart(E("#BPM_CHANGE", null, V("#BEAT", 4), V("#BPM", 240)), N("CriticalFlickNote", null, 8)));
            Assert.That(result.Score.NoteArray.Single().MusicScoreInfo.time, Is.EqualTo(3).Within(0.0001));
            Assert.That(result.Score.NoteArray.Single(), Is.TypeOf<FlickNote>());
            Assert.That(result.Score.NoteArray.Single().Type, Is.EqualTo(NoteType.Critical));
        }

        [Test]
        public void NamedHoldConnectionsAndExplicitTicksAreLoadedExactlyOnce()
        {
            var result = Load(Hold());
            var head = (LongNote)result.Score.NoteArray.Single();
            Assert.That(head.ChildNote, Is.TypeOf<NormalNote>());
            Assert.That(head.NoteList.Count, Is.EqualTo(4));
            Assert.That(result.TotalNoteCount, Is.EqualTo(4));
            var hidden = head.NoteList.OfType<LongHoldCombo>().Single();
            Assert.That(hidden.LaneStartF, Is.EqualTo(4));
            Assert.That(hidden.LaneEndF, Is.EqualTo(5));
        }

        [Test]
        public void NativeTailKeepsItsOwnCriticalTypeAfterParenting()
        {
            var json = Hold();
            var head = ((JArray)json["entities"]).OfType<JObject>().Single(e => (string)e["name"] == "head");
            head["archetype"] = "CriticalHeadTapNote";
            var result = Load(json);
            var root = (LongNote)result.Score.NoteArray.Single();
            Assert.That(root.Type, Is.EqualTo(NoteType.Critical));
            Assert.That(root.ChildNote.Type, Is.EqualTo(NoteType.Default));
        }

        [Test]
        public void NativeHoldAutoplaySettlesEveryJudgedPoint()
        {
            var result = Load(Hold());
            using var harness = new LiveHarness();
            harness.Arrange(result.Score);
            for (float time = 0; time < 5; time += 1f / 120)
            {
                harness.OnUpdate(time, 10d + time);
                harness.Logic.OnAutoInput();
                if (harness.Logic.IsNotesAllFinished) break;
            }
            Assert.That(harness.Logic.IsNotesAllFinished, Is.True);
            Assert.That(result.Score.NoteArray.SelectMany(n => n.NoteList).All(n => n.State == NoteState.Done), Is.True);
        }

        [Test]
        public void PairSettingAppliesToNativeNotes()
        {
            var json = Chart(N("NormalTapNote", "left", 1, -3), N("NormalTapNote", "right", 1, 3));
            Assert.That(Load(json).Score.NoteArray[0].PairNote, Is.Not.Null);
            Assert.That(Load(json, false).Score.NoteArray[0].PairNote, Is.Null);
        }

        [TestCase("#TIMESCALE_CHANGE")]
        [TestCase("AnchorNote")]
        [TestCase("NormalHeadTraceNote")]
        [TestCase("NormalTailTapNote")]
        [TestCase("DamageNote")]
        public void UnsupportedArchetypesAreRefused(string archetype)
        {
            Assert.Throws<InvalidDataException>(() => Load(Chart(N(archetype, null, 1))));
        }

        [TestCase("style", 1)]
        [TestCase("direction", 3)]
        [TestCase("segmentAlpha", 0.5)]
        [TestCase("connectorEase", 2)]
        [TestCase("futureField", 1)]
        public void UnsupportedFieldsAreNeverSilentlyDropped(string field, double value)
        {
            var note = N("NormalTapNote", null, 1);
            Add(note, V(field, value));
            Assert.Throws<InvalidDataException>(() => Load(Chart(note)));
        }

        [TestCase(1.271)]
        [TestCase(-1.271)]
        public void UnimplementedOffsetsAreExplicitlyRefused(double offset)
        {
            var json = Chart(N("NormalTapNote", null, 1));
            json["bgmOffset"] = offset;
            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("bgmOffset"));
        }

        [Test]
        public void BrokenAndCyclicHoldReferencesAreRefused()
        {
            var head = N("NormalHeadTapNote", "h", 1);
            Add(head, R("next", "missing"));
            Assert.Throws<InvalidDataException>(() => Load(Chart(head)));
            ((JArray)head["data"]).Last["ref"] = "h";
            Assert.Throws<InvalidDataException>(() => Load(Chart(head)));
        }

        [Test]
        public void MissingAndDuplicateConnectorSegmentsAreRefused()
        {
            var json = Hold();
            var entities = (JArray)json["entities"];
            var connector = entities.OfType<JObject>().First(e => (string)e["archetype"] == "Connector");
            connector.Remove();
            Assert.Throws<InvalidDataException>(() => Load(json));
            json = Hold();
            entities = (JArray)json["entities"];
            entities.Add(entities.OfType<JObject>().First(e => (string)e["archetype"] == "Connector").DeepClone());
            Assert.Throws<InvalidDataException>(() => Load(json));
        }

        [Test]
        public void FailedNativeReplacementKeepsTheInstalledChart()
        {
            var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
            var package = manager.CreateNewEntry();
            byte[] previous = File.ReadAllBytes(package.ScorePath);
            string invalid = Path.Combine(_directory, "broken.next-rush.json");
            File.WriteAllText(invalid, "{}");
            Assert.Throws<InvalidDataException>(() => manager.ReplaceScoreFile(package, invalid));
            Assert.That(File.ReadAllBytes(package.ScorePath), Is.EqualTo(previous));
            Assert.That(SongPackageStorage.LoadEntry(package.RootDirectory).Manifest.chartFormat, Is.Null);
        }

        [Test]
        public void ParserRejectsDuplicateNamesFieldsAndNonfiniteNumbers()
        {
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse(Chart(N("NormalTapNote", "x", 1), N("NormalTapNote", "x", 2)).ToString()));
            var note = N("NormalTapNote", null, 1);
            Add(note, V("#BEAT", 2));
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse(Chart(note).ToString()));
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse("{\"bgmOffset\":NaN,\"entities\":[]}"));
        }

        [Test]
        public void GzipPackageSurvivesImportExportAndLoadsThroughTheSharedBoundary()
        {
            string path = Path.Combine(_directory, "original.next-rush.json.gz");
            byte[] json = Encoding.UTF8.GetBytes(Hold().ToString());
            using (var output = File.Create(path))
            using (var gzip = new GZipStream(output, CompressionMode.Compress)) gzip.Write(json, 0, json.Length);
            byte[] original = File.ReadAllBytes(path);
            var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
            var package = manager.ReplaceScoreFile(manager.CreateNewEntry(), path);
            Assert.That(package.Manifest.chartFormat, Is.EqualTo("next-rush"));
            Assert.That(File.ReadAllBytes(package.ScorePath), Is.EqualTo(original));
            string archive = manager.ExportZip(package, Path.Combine(_directory, "export.oskp"));
            var imported = manager.ImportZip(archive);
            Assert.That(File.ReadAllBytes(imported.ScorePath), Is.EqualTo(original));
            var runtime = RuntimeChartLoader.Load(imported, _config);
            var session = PlaySession.FromChart(new PlayRequest(imported.RootDirectory, true, SceneManager.Scene.Library), imported, runtime,
                _config, new PlaybackSettingsSnapshot(new LiveSettingData()));
            Assert.That(session.Chart.NextRush, Is.Not.Null);
            Assert.That(session.Chart.TotalNoteCount, Is.EqualTo(4));
            // Replacing a native chart by a legacy one must clear the format discriminator.
            string legacy = Path.Combine(_directory, "legacy.json");
            File.WriteAllText(legacy, OpenSekai.Charts.Serialization.ChartJson.Write(new ChartDocument()));
            Assert.That(manager.ReplaceScoreFile(imported, legacy).Manifest.chartFormat, Is.Null);
        }

        [Test]
        public void CorruptGzipAndUnknownPackageFormatsAreRefused()
        {
            string path = Path.Combine(_directory, "bad.next-rush.json.gz");
            File.WriteAllBytes(path, new byte[] { 0x1f, 0x8b, 0, 0, 0, 0, 0, 0, 0, 0, 0xff });
            Assert.That(() => NextRushChartLoader.ReadFile(path), Throws.Exception);
            var package = new SongPackage(_directory, new SongPackageManifest { chartFormat = "unknown", scoreFileName = "anything.json" });
            Assert.Throws<InvalidDataException>(() => RuntimeChartLoader.Load(package, _config));
        }

        [Test]
        public void DownloadedServerSampleLoadsAndAutoplaysWhenProvided()
        {
            string directory = Environment.GetEnvironmentVariable("OPENSEKAI_SONOLUS_SAMPLE_DIR");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Optional integration test: provide OPENSEKAI_SONOLUS_SAMPLE_DIR from probe-sonolus.ps1.");
            var source = NextRushChartLoader.ReadFile(Path.Combine(directory, "level-data.json.gz"));
            var result = NextRushChartLoader.Load(source, _config);
            string archive = Environment.GetEnvironmentVariable("OPENSEKAI_SONOLUS_PACKAGE");
            if (!string.IsNullOrEmpty(archive))
            {
                var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
                var imported = manager.ImportZip(archive);
                Assert.That(imported.Manifest.chartFormat, Is.EqualTo("next-rush"));
                Assert.That(File.Exists(imported.AudioPath), Is.True);
                Assert.That(File.Exists(imported.JacketPath), Is.True);
                Assert.That(File.Exists(Path.Combine(imported.RootDirectory, "sonolus-source.json")), Is.True);
                Assert.That(File.ReadAllBytes(imported.ScorePath), Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, "level-data.json.gz"))));
                result = RuntimeChartLoader.Load(imported, _config);
            }
            int authored = source.Entities.Count(e => e.Archetype.EndsWith("Note", StringComparison.Ordinal));
            Assert.That(result.TotalNoteCount, Is.EqualTo(authored), "native scored entities must appear exactly once");
            using var harness = new LiveHarness();
            harness.Arrange(result.Score);
            float end = result.Score.NoteArray.SelectMany(n => n.NoteList).Max(n => n.MusicScoreInfo.time) + 3;
            for (float time = -2; time < end; time += 1f / 120)
            {
                harness.OnUpdate(time, 10d + time);
                harness.Logic.OnAutoInput();
                if (harness.Logic.IsNotesAllFinished) break;
            }
            Assert.That(harness.Logic.IsNotesAllFinished, Is.True);
            TestContext.WriteLine($"Native server sample: {source.Entities.Count} entities, {result.TotalNoteCount} judged points, autoplay completed.");
        }
    }
}
