using System;
using System.IO;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Charts;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Playback
{
    /// <summary>
    /// PlaySession is the single place that resolves a request into "everything one attempt needs".
    /// The tests pin the guard clauses and the fact that the conversion happens exactly once.
    /// </summary>
    public class PlaySessionTests
    {
        private string _root;
        private SongPackage _package;
        private LiveBundleBuildData _config;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            _package = new SongPackage(_root, new SongPackageManifest
            {
                title = "Session",
                id = "session",
                scoreFileName = "score.json"
            });
            _config = ScriptableObject.CreateInstance<LiveBundleBuildData>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
            }

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static PlayRequest Request() =>
            new PlayRequest("/tmp/package", isAuto: true, SceneManager.Scene.Library);

        private static ChartDocument Document(int noteCount)
        {
            var document = new ChartDocument();
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            for (int i = 0; i < noteCount; i++)
            {
                document.NoteList.Add(new ChartNote(i + 1, i * 480L, i % 4, i % 4, NoteCategory.Normal));
            }

            return document;
        }

        private PlaybackSettingsSnapshot Snapshot() => new PlaybackSettingsSnapshot(new LiveSettingData());

        [Test]
        public void NullRequestIsRejected()
        {
            Assert.That(() => PlaySession.Create(null, _package, Document(1), _config, Snapshot()),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("request"));
        }

        [Test]
        public void NullPackageIsRejected()
        {
            Assert.That(() => PlaySession.Create(Request(), null, Document(1), _config, Snapshot()),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("package"));
        }

        [Test]
        public void NullDocumentIsRejected()
        {
            Assert.That(() => PlaySession.Create(Request(), _package, null, _config, Snapshot()),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("document"));
        }

        [Test]
        public void NullBundleConfigIsRejected()
        {
            Assert.That(() => PlaySession.Create(Request(), _package, Document(1), null, Snapshot()),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("bundleConfig"));
        }

        [Test]
        public void ValidArgumentsProduceAReadySession()
        {
            PlayRequest request = Request();
            ChartDocument document = Document(5);

            PlaySession session = PlaySession.Create(request, _package, document, _config, Snapshot());

            Assert.That(session.Request, Is.SameAs(request));
            Assert.That(session.Package, Is.SameAs(_package));
            Assert.That(session.BundleConfig, Is.SameAs(_config));
            Assert.That(session.Settings, Is.Not.Null);
            Assert.That(session.Chart, Is.Not.Null);
            Assert.That(session.Chart.Document, Is.SameAs(document));
            Assert.That(session.Chart.TotalNoteCount, Is.EqualTo(5));
            Assert.That(session.Chart.HasPlayableNotes, Is.True);
        }

        [Test]
        public void SessionKeepsTheRequestFlags()
        {
            PlaySession session = PlaySession.Create(
                new PlayRequest("/tmp/x", isAuto: false, SceneManager.Scene.Library),
                _package, Document(1), _config, Snapshot());

            Assert.That(session.Request.IsAuto, Is.False);
            Assert.That(session.Request.PackageDirectory, Is.EqualTo("/tmp/x"));
            Assert.That(session.Request.ReturnScene, Is.EqualTo(SceneManager.Scene.Library));
        }

        [Test]
        public void SettingsSnapshotIsIndependentOfLaterEdits()
        {
            var settings = new LiveSettingData { NoteSkinIndex = 0, TimingAdjustData = 0f };
            var snapshot = new PlaybackSettingsSnapshot(settings);

            settings.NoteSkinIndex = 1;
            settings.TimingAdjustData = 0.25f;

            LiveSettingData copy = snapshot.ToLegacyData();
            Assert.That(copy.NoteSkinIndex, Is.EqualTo(0), "the snapshot must not follow later edits");
            Assert.That(copy.TimingAdjustData, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
