using NUnit.Framework;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>
    /// The live context is the one object a live run receives; these checks pin what it carries and
    /// that reading the retained boundary copies exactly the five facts the controller used to read
    /// from three statics during Awake.
    /// </summary>
    public sealed class LiveContextTests
    {
        private FreeLiveBootData _previousBoot;
        private PlayMode _previousMode;
        private IPlayHistoryRecorder _previousRecorder;

        [SetUp]
        public void SetUp()
        {
            _previousBoot = UserDataManager.Instance.FreeLiveBootData;
            _previousMode = EntryPoint.PlayMode;
            _previousRecorder = PlayHistoryRecorder.Current;
            PlaySessionContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            UserDataManager.Instance.FreeLiveBootData = _previousBoot;
            EntryPoint.PlayMode = _previousMode;
            PlayHistoryRecorder.Current = _previousRecorder;
            PlaySessionContext.Clear();
        }

        [Test]
        public void ReadingTheRetainedBootCopiesEveryFact()
        {
            var boot = new FreeLiveBootData(7, "master", 0);
            UserDataManager.Instance.FreeLiveBootData = boot;
            EntryPoint.PlayMode = PlayMode.SoloLive;
            PlaySessionContext.Begin("abc123");

            LiveContext context = LiveContext.FromRetainedBoot();

            Assert.That(context.BootData, Is.SameAs(boot));
            Assert.That(context.PlayMode, Is.EqualTo(PlayMode.SoloLive));
            Assert.That(context.SessionId, Is.EqualTo(PlaySessionContext.SessionId));
            Assert.That(context.ChartHash, Is.EqualTo("abc123"));
            Assert.That(context.RulesVersion, Is.EqualTo(PlaySessionContext.RULES_VERSION));
        }

        [Test]
        public void ReadingWithoutABootKeepsEveryOtherFact()
        {
            UserDataManager.Instance.FreeLiveBootData = null;
            EntryPoint.PlayMode = PlayMode.None;

            LiveContext context = LiveContext.FromRetainedBoot();

            Assert.That(context.BootData, Is.Null);
            Assert.That(context.PlayMode, Is.EqualTo(PlayMode.None));
            Assert.That(context.ChartHash, Is.Empty);
        }

        [Test]
        public void MissingSessionValuesBecomeEmptyStrings()
        {
            var context = new LiveContext(null, PlayMode.MultiLive, null, null, 9);

            Assert.That(context.SessionId, Is.Empty);
            Assert.That(context.ChartHash, Is.Empty);
            Assert.That(context.RulesVersion, Is.EqualTo(9));
        }

        [Test]
        public void ReadingTheRetainedBootCarriesTheRegisteredRecorder()
        {
            var recorder = new FakeRecorder();
            PlayHistoryRecorder.Current = recorder;

            LiveContext context = LiveContext.FromRetainedBoot();

            Assert.That(context.PlayHistory, Is.SameAs(recorder));
        }

        [Test]
        public void AContextWithoutARecorderLeavesHistoryOff()
        {
            var context = new LiveContext(null, PlayMode.None, null, null, 0);

            Assert.That(context.PlayHistory, Is.Null);
        }

        [Test]
        public void ReadingTwiceReturnsIndependentCopies()
        {
            UserDataManager.Instance.FreeLiveBootData = new FreeLiveBootData(1, "master", 0)
            {
                CustomMusicScoreTitle = "first"
            };
            LiveContext first = LiveContext.FromRetainedBoot();
            UserDataManager.Instance.FreeLiveBootData = new FreeLiveBootData(2, "expert", 0)
            {
                CustomMusicScoreTitle = "second"
            };
            LiveContext second = LiveContext.FromRetainedBoot();

            Assert.That(first.BootData, Is.Not.SameAs(second.BootData));
            Assert.That(((FreeLiveBootData)first.BootData).CustomMusicScoreTitle, Is.EqualTo("first"));
            Assert.That(((FreeLiveBootData)second.BootData).CustomMusicScoreTitle, Is.EqualTo("second"));
        }

        private sealed class FakeRecorder : IPlayHistoryRecorder
        {
            public void AppendLiveResult(
                FreeLiveBootData bootData,
                LiveScore score,
                long musicTimeMs,
                long musicLengthMs,
                string sessionId,
                string chartHash,
                int rulesVersion)
            {
            }
        }
    }
}
