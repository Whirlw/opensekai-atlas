using System;
using System.IO;
using NUnit.Framework;
using OpenSekai.Shared.Settings;
using Sekai;

namespace OpenSekai.Tests.EditMode.Settings
{
    /// <summary>
    /// LiveSettingsStore takes its path in the constructor, so it can be exercised against a
    /// temporary file: defaults, persistence, migration of the old 120 FPS flag, and corrupt files.
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
        public void SavedValuesComeBack()
        {
            var store = new LiveSettingsStore(_path);
            LiveSettingData data = store.Load();
            data.NoteSkinIndex = 1;
            data.NoteSeIndex = 1;
            data.NoteEffect = 1;
            data.FrameRateLimit = 120;
            data.NoteEffect = 2;
            Assert.That(store.Save(data), Is.True);

            LiveSettingData reloaded = new LiveSettingsStore(_path).Load();

            Assert.That(reloaded.NoteSkinIndex, Is.EqualTo(1));
            Assert.That(reloaded.NoteSeIndex, Is.EqualTo(1));
            Assert.That(reloaded.NoteEffect, Is.EqualTo(2));
            Assert.That(reloaded.FrameRateLimit, Is.EqualTo(120));
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
        public void Legacy120FpsFlagUpgradesToALimit()
        {
            File.WriteAllText(_path, "{ \"Use120FPS\": true }");

            LiveSettingData loaded = new LiveSettingsStore(_path).Load();

            Assert.That(loaded.FrameRateLimit, Is.EqualTo(120), "the old boolean must become a limit");
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
