using OpenSekai.MaterialPacks;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Infrastructure.Packages;

namespace OpenSekai.Tests.EditMode
{
    public sealed class MaterialPackStoreTests
    {
        private const string SAMPLE = "Tools/fixtures/material-audio.zip";
        private string _root;
        private MaterialPackStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "material-test-" + Guid.NewGuid().ToString("N"));
            _store = new MaterialPackStore(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void StereoLoopUsesFramesPerChannel()
        {
            Directory.CreateDirectory(_root);
            string path = Path.Combine(_root, "stereo.wav");
            AcbDecoder.WavWriter.WritePcm16(path, new AcbDecoder.AudioData(new[] { 0f, 0f, -1f, .5f, 1f, -.5f }, 44100, 2));
            var bytes = File.ReadAllBytes(path);
            var wave = MaterialPackWave.Read(bytes);
            Assert.That(wave.Frames, Is.EqualTo(3));
            var samples = wave.Decode(bytes, 1, 2);
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(-1f));
            Assert.That(samples[1], Is.EqualTo(.5f).Within(.0001f));
            bytes[34] = 8;
            Assert.Throws<InvalidDataException>(() => MaterialPackWave.Read(bytes));
        }

        [Test]
        public async Task RepackedArchiveKeepsDigestAndDoesNotChangeSelection()
        {
            var preview = await _store.ValidateAsync(SAMPLE);
            Assert.That(_store.List().Installed, Is.Empty);
            var pack = await _store.ImportAsync(SAMPLE);
            var duplicate = await _store.ImportAsync(Repack());
            Assert.That(duplicate.Digest, Is.EqualTo(preview.Digest));
            Assert.That(_store.List().Revision, Is.EqualTo(1));
            Assert.That(_store.List().Audio.Groups, Is.Empty);
            Assert.That(pack.Groups.Select(g => g.Id), Is.EquivalentTo(new[] { "clicks", "holds" }));
        }

        [Test]
        public async Task PriorityOverridesRestartAndUninstallUseTheSameResolution()
        {
            var a = await _store.ImportAsync(SAMPLE);
            var b = await _store.ImportAsync(Repack(s => s.Replace("opensekai.sound-check", "another")));
            string first = a.GroupReference("clicks"), second = b.GroupReference("clicks");
            var selection = new MaterialAudioSelection { Groups = { first, second, b.GroupReference("holds") } };
            var hits = _store.Resolve(selection).Single(x => x.Slot == "audio.tap");
            Assert.That(hits.Source, Is.EqualTo(first));
            Assert.That(hits.Candidates, Is.EqualTo(new[] { first, second }));
            selection.Overrides.Add("audio.tap", null);
            selection.Overrides.Add("audio.perfect", second);
            long revision = _store.SaveSelection(selection, _store.List().Revision);
            selection.Groups.Clear();
            _store = new MaterialPackStore(_root);
            var bindings = _store.Resolve(_store.List().Audio);
            Assert.That(bindings.Single(x => x.Slot == "audio.tap").Source, Is.Null);
            Assert.That(bindings.Single(x => x.Slot == "audio.perfect").Source, Is.EqualTo(second));
            Assert.That(bindings.Single(x => x.Slot == "audio.long").Source, Is.EqualTo(b.GroupReference("holds")));
            Assert.That(bindings.Single(x => x.Slot == "audio.flick").Source, Is.Null);
            Assert.Throws<InvalidOperationException>(() => _store.SaveSelection(selection, revision - 1));
            _store.Uninstall(b.PackId, b.Digest, revision);
            bindings = _store.Resolve(_store.List().Audio);
            Assert.That(bindings.Single(x => x.Slot == "audio.perfect").Source, Is.EqualTo(first));
            Assert.That(bindings.Single(x => x.Slot == "audio.long").Source, Is.Null);
        }

        [TestCase("Case")]
        [TestCase("Duplicate")]
        [TestCase("Range")]
        [TestCase("Unknown")]
        [TestCase("Missing")]
        [TestCase("InvalidWave")]
        [TestCase("Field")]
        [TestCase("Schema")]
        [TestCase("Traversal")]
        public async Task BadPackageCannotPublish(string fault)
        {
            var a = await _store.ImportAsync(SAMPLE);
            string path = Repack(s => fault switch
            {
                "Duplicate" => s.Replace("\"schemaVersion\":", "\"schemaVersion\":1,\"schemaVersion\":"),
                "Range" => s.Replace("5280", "999999"),
                "Unknown" => s.Replace("audio.tap", "notes.tap"),
                "Missing" => s.Replace("sound/click.wav", "sound/Click.wav"),
                "Field" => s.Replace("\"loopFrames\"", "\"loops\""),
                "Schema" => s.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"),
                _ => s
            }, fault);
            try
            {
                await _store.ImportAsync(path);
                Assert.Fail("应拒绝无效资源包：" + fault);
            }
            catch (InvalidDataException) { }
            Assert.That(_store.List().Revision, Is.EqualTo(1));
            Assert.That(_store.List().Installed.Single().Digest, Is.EqualTo(a.Digest));
        }

        [Test]
        public async Task NewRevisionStaysOptionalAndCancelledImportLeavesTheIndexAlone()
        {
            var original = await _store.ImportAsync(SAMPLE);
            _store.SaveSelection(new MaterialAudioSelection { Groups = { original.GroupReference("clicks") } }, 1);
            string updated = Repack(s => s.Replace("0.1.0", "0.2.0"));
            using var cancel = new System.Threading.CancellationTokenSource();
            cancel.Cancel();
            try { await _store.ImportAsync(updated, cancel.Token); Assert.Fail("已取消的导入应终止"); }
            catch (OperationCanceledException) { }
            Assert.That(_store.List().Revision, Is.EqualTo(2));
            var revision = await _store.ImportAsync(updated);
            Assert.That(revision.PackId, Is.EqualTo(original.PackId));
            Assert.That(revision.Digest, Is.Not.EqualTo(original.Digest));
            Assert.That(_store.List().Installed.Count, Is.EqualTo(2));
            Assert.That(_store.Resolve(_store.List().Audio).Single(x => x.Slot == "audio.tap").Source,
                Is.EqualTo(original.GroupReference("clicks")));
        }

        [Test]
        public async Task DecodedBudgetCountsSharedClipsOnceAndDistinctClipsSeparately()
        {
            var pack = await _store.ValidateAsync(SAMPLE);
            var slots = pack.Groups[0].Slots;
            foreach (var slot in slots.Values)
                slot.Frames = slot.End = MaterialPackResolver.MAX_DECODED_BYTES / sizeof(float);
            var selection = new MaterialAudioSelection { Groups = { pack.GroupReference("clicks") } };
            Assert.DoesNotThrow(() => MaterialPackResolver.Resolve(new[] { pack }, selection, _root));
            slots["audio.perfect"].Hash = new string('0', 64);
            Assert.Throws<InvalidDataException>(() => MaterialPackResolver.Resolve(new[] { pack }, selection, _root));
        }

        [Test]
        public async Task CaptureBlocksWritesUntilReleaseAndBackupRestoresCompleteState()
        {
            var pack = await _store.ImportAsync(SAMPLE);
            var selection = new MaterialAudioSelection { Groups = { pack.GroupReference("holds") } };
            _store.SaveSelection(selection, 1);
            var captured = _store.Capture();
            try
            {
                Assert.Throws<InvalidOperationException>(() => _store.SaveSelection(new MaterialAudioSelection(), 2));
                Assert.Throws<InvalidOperationException>(() => _store.Uninstall(pack.PackId, pack.Digest, 2));
                Assert.That(captured.Single(x => x.Slot == "audio.long").Source, Is.EqualTo(selection.Groups[0]));
            }
            finally { _store.Release(); }
            _store.SaveSelection(new MaterialAudioSelection(), 2);
            File.WriteAllText(Path.Combine(_root, "state.json"), "{");
            var recovered = new MaterialPackStore(_root).List();
            Assert.That(recovered.Revision, Is.EqualTo(2));
            Assert.That(recovered.Audio.Groups, Is.EqualTo(selection.Groups));
        }

        private string Repack(Func<string, string> edit = null, string fault = null)
        {
            string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
            using var input = ZipFile.OpenRead(SAMPLE);
            using var output = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var item in input.Entries.Reverse())
            {
                using var src = item.Open();
                using var dst = output.CreateEntry(item.FullName, CompressionLevel.NoCompression).Open();
                if (item.FullName == "pack.json" && edit != null)
                {
                    using var reader = new StreamReader(src);
                    byte[] bytes = Encoding.UTF8.GetBytes(edit(reader.ReadToEnd()));
                    dst.Write(bytes, 0, bytes.Length);
                }
                else if (fault == "InvalidWave" && item.FullName == "sound/click.wav") dst.WriteByte(1);
                else src.CopyTo(dst);
            }
            if (fault == "Case") output.CreateEntry("SOUND/click.wav");
            if (fault == "Traversal") output.CreateEntry("../escape.wav");
            return path;
        }
    }
}
