using System;
using System.IO;
using NUnit.Framework;
using OpenSekai.Shared.Settings;
using Sekai;

namespace OpenSekai.Tests.EditMode.Settings
{
    /// <summary>
    /// LiveSettingsStore takes its path in the constructor, so it can be exercised against a
    /// temporary file: defaults, persistence, and corrupt files.
    /// </summary>
    public class LiveSettingsStoreTests
    {
        private string _root;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _path = Path.Combine(_root, "LiveSettingData.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void MissingFileYieldsDefaults()
        {
            LiveSettingData loaded = new LiveSettingsStore(_path).Load();

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.NoteSkinIndex, Is.EqualTo(0));
        }

        [Test]
        public void LegacyNoteShowRateKeepsItsInvertedJsonValue()
        {
            File.WriteAllText(_path, "{\"_noteShowRate\":0.25}");
            var store = new LiveSettingsStore(_path);
            var settings = store.Load();
            Assert.That(settings.NoteShowRate, Is.EqualTo(0.75f));
            Assert.That(settings.UseSubtractiveScore, Is.False);
            Assert.That(settings.ShowFastLate, Is.False);
            Assert.That(settings.ShowFlickMiss, Is.False);
            settings.NoteShowRate = 0.5f;
            Assert.That(store.Save(settings), Is.True);
            var saved = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(_path));
            Assert.That((float)saved["_noteShowRate"], Is.EqualTo(0.5f));
            Assert.That(saved.Property("NoteShowRate"), Is.Null);
        }

        [TestCase("{}", 1f)]
        [TestCase("{\"NoteAlpha\":0}", 1f)]
        [TestCase("{\"NoteAlpha\":0.4}", 0.4f)]
        public void LongNoteBodyOpacityPreservesLegacyAppearanceAndCanBeHidden(string json, float expected)
        {
            File.WriteAllText(_path, json);
            var store = new LiveSettingsStore(_path);
            var settings = store.Load();
            Assert.That(settings.LongNoteBodyOpacity, Is.EqualTo(expected));
            settings.LongNoteBodyOpacity = 0f;
            Assert.That(store.Save(settings), Is.True);
            var nextPlay = new LiveSettingsStore(_path).CaptureSnapshot().ToLegacyData();
            Assert.That(nextPlay.LongNoteBodyOpacity, Is.Zero);
        }

        [Test]
        public void SavedValuesComeBack()
        {
            var store = new LiveSettingsStore(_path);
            LiveSettingData data = store.Load();
            data.NoteSkinIndex = 1;
            data.NoteSeIndex = 1;
            data.NoteEffect = 1;
            data.FrameRateLimit = 120;
            data.UseSubtractiveScore = true;
            data.ShowFastLate = true;
            data.NoteEffect = 2;
            Assert.That(store.Save(data), Is.True);

            LiveSettingData reloaded = new LiveSettingsStore(_path).Load();

            Assert.That(reloaded.NoteSkinIndex, Is.EqualTo(1));
            Assert.That(reloaded.NoteSeIndex, Is.EqualTo(1));
            Assert.That(reloaded.NoteEffect, Is.EqualTo(2));
            Assert.That(reloaded.FrameRateLimit, Is.EqualTo(120));
            Assert.That(reloaded.UseSubtractiveScore, Is.True);
            Assert.That(reloaded.ShowFastLate, Is.True);
            Assert.That(reloaded.ShowFlickMiss, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LegacyCaptionPreferenceSplitsIntoIndependentSwitches(bool enabled)
        {
            File.WriteAllText(_path, enabled ? "{\"IsFastLateFlick\":true}" : "{\"IsFastLateFlick\":false}");
            var store = new LiveSettingsStore(_path);
            var settings = store.Load();
            Assert.That(settings.ShowFastLate, Is.EqualTo(enabled));
            Assert.That(settings.ShowFlickMiss, Is.EqualTo(enabled));

            settings.ShowFlickMiss = !enabled;
            Assert.That(store.Save(settings), Is.True);
            Assert.That(File.ReadAllText(_path), Does.Not.Contain("IsFastLateFlick"));
            var nextPlay = new LiveSettingsStore(_path).CaptureSnapshot().ToLegacyData();
            Assert.That(nextPlay.ShowFastLate, Is.EqualTo(enabled));
            Assert.That(nextPlay.ShowFlickMiss, Is.EqualTo(!enabled));
        }

        [Test]
        public void SaveCreatesTheDirectory()
        {
            string nested = Path.Combine(_root, "a", "b", "settings.json");
            var store = new LiveSettingsStore(nested);

            Assert.That(store.Save(new LiveSettingData { NoteSkinIndex = 1 }), Is.True);
            Assert.That(File.Exists(nested), Is.True);
        }

        [Test]
        public void FollowDisplayPersistsAfterChangingFrom120Hz()
        {
            File.WriteAllText(_path, "{ \"FrameRateLimit\": 120, \"Use120FPS\": true }");
            var store = new LiveSettingsStore(_path);
            var settings = store.Load();
            settings.FrameRateLimit = 0;
            Assert.That(store.Save(settings), Is.True);
            Assert.That(new LiveSettingsStore(_path).Load().FrameRateLimit, Is.Zero);
        }

        [Test]
        public void TheOldFrameShiftMigratesToAnAudioLatencyOnce()
        {
            // The old "判定延迟" of -1.5 frames moved the clock back 25 ms: an audio latency of 25 ms.
            File.WriteAllText(_path, "{ \"TimingAdjustData\": -1.5 }");

            var store = new LiveSettingsStore(_path);
            LiveSettingData loaded = store.Load();
            Assert.That(loaded.AudioLatencyMs, Is.EqualTo(25f).Within(1e-3f));
            Assert.That(loaded.TimingAdjustData, Is.Zero, "the old value is consumed, not applied twice");

            store.Save(loaded);
            LiveSettingData reloaded = new LiveSettingsStore(_path).Load();
            Assert.That(reloaded.AudioLatencyMs, Is.EqualTo(25f).Within(1e-3f));
        }

        [Test]
        public void AnAudioLatencyAlreadySetWinsOverTheOldFrameShift()
        {
            File.WriteAllText(_path, "{ \"TimingAdjustData\": 2, \"AudioLatencyMs\": 40 }");

            LiveSettingData loaded = new LiveSettingsStore(_path).Load();
            Assert.That(loaded.AudioLatencyMs, Is.EqualTo(40f));
            Assert.That(loaded.TimingAdjustData, Is.Zero);
        }

        [Test]
        public void TheThreeCompensationsComeBack()
        {
            var store = new LiveSettingsStore(_path);
            LiveSettingData data = store.Load();
            data.AudioLatencyMs = 30f;
            data.VisualLatencyMs = 20f;
            data.JudgeOffsetMs = -10f;
            store.Save(data);

            LiveSettingData reloaded = new LiveSettingsStore(_path).Load();
            Assert.That(reloaded.AudioLatencyMs, Is.EqualTo(30f));
            Assert.That(reloaded.VisualLatencyMs, Is.EqualTo(20f));
            Assert.That(reloaded.JudgeOffsetMs, Is.EqualTo(-10f));
        }

        [Test]
        public void CorruptFileFallsBackToDefaults()
        {
            File.WriteAllText(_path, "{ this is not json");

            LiveSettingData loaded = new LiveSettingsStore(_path).Load();

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.NoteSkinIndex, Is.EqualTo(0));
        }

        [Test]
        public void NullPathIsRejected()
        {
            Assert.That(() => new LiveSettingsStore(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void SaveNullStoresDefaults()
        {
            var store = new LiveSettingsStore(_path);

            Assert.That(store.Save(null), Is.True);
            Assert.That(store.Load().NoteSkinIndex, Is.EqualTo(0));
        }
    }
}
