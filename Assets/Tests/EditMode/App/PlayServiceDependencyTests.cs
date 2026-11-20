using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using Sekai;
using Sekai.Core;
using Sekai.Core.Live;
using Cysharp.Threading.Tasks;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>
    /// The start path with its dependencies replaced: the boot rollback, the lock and the guards are
    /// exercised without the live scene, a screen manager or a real audio clip.
    /// </summary>
    public sealed class PlayServiceDependencyTests
    {
        private static readonly string _fixtureDirectory = Path.GetFullPath("Tools/fixtures/baseline_tap");

        private FakeSurface _surface;
        private FakeBootPublisher _boot;
        private FakeAudioRegistrar _audio;
        private FakeDeckSelection _deck;
        private FakePlayHistoryRecorder _playHistory;
        private IPlayHistoryRecorder _previousRecorder;
        private PlayService _service;
        private LiveSettingsStore _settings;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _surface = new FakeSurface();
            _boot = new FakeBootPublisher();
            _audio = new FakeAudioRegistrar { Result = true };
            _deck = new FakeDeckSelection { SelectedDeckId = 7 };
            _playHistory = new FakePlayHistoryRecorder();
            _previousRecorder = PlayHistoryRecorder.Current;
            _root = Path.Combine(Path.GetTempPath(), "OpenSekai-play-" + Guid.NewGuid().ToString("N"));
            _settings = new LiveSettingsStore(Path.Combine(_root, "settings.json"));
            var packages = new OpenSekai.Infrastructure.Packages.SongPackageManager(_root);
            _service = new PlayService(packages, _settings, _boot, _surface, _audio, _deck, _playHistory);
        }

        [TearDown]
        public void TearDown()
        {
            PlayHistoryRecorder.Current = _previousRecorder;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public async System.Threading.Tasks.Task ANotReadyLibraryIsRefusedBeforeTheBootIsTouched()
        {
            _surface.LibraryReady = false;

            PlayStartResult result = await _service.StartAsync(Request(), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("曲库界面尚未就绪。"));
            Assert.That(_boot.CaptureCount, Is.Zero);
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
            // A refused start must not touch the scene-exit subscription: subscribing is the only
            // part that reaches into the scene manager.
            Assert.That(_surface.SubscribeCount, Is.Zero);
            Assert.That(_surface.UnsubscribeCount, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task ANotReadyLiveLoadingScreenIsRefusedBeforeTheBootIsTouched()
        {
            _surface.LiveLoadingReady = false;

            PlayStartResult result = await _service.StartAsync(Request(), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("打歌加载界面尚未就绪。"));
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task ARequestThatTargetsAnotherReturnSceneIsRefused()
        {
            PlayStartResult result = await _service.StartAsync(
                new PlayRequest(_fixtureDirectory, false, SceneManager.Scene.Core), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("无法从当前场景开始游玩。"));
            Assert.That(_boot.PublishCount, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task AStartOutsideTheLibrarySceneIsRefused()
        {
            _surface.Scene = SceneManager.Scene.Core;

            PlayStartResult result = await _service.StartAsync(Request(), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(result.Message, Is.EqualTo("曲库界面尚未就绪。"));
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task AStartThatCannotPushTheLoadingScreenRestoresThePreviousBoot()
        {
            _boot.State = new LiveBootState(null, PlayMode.None);
            _surface.FailOnPush = true;

            PlayStartResult result = await _service.StartAsync(Request(_fixtureDirectory), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(_boot.PublishCount, Is.EqualTo(1));
            Assert.That(_boot.RestoreCount, Is.EqualTo(1));
            Assert.That(_boot.LastRestored.Boot, Is.Null);
            Assert.That(_boot.LastRestored.Mode, Is.EqualTo(PlayMode.None));
            Assert.That(_service.IsStarting, Is.False);
            Assert.That(_surface.SceneExitHandlers, Is.Zero);
            Assert.That(_surface.SubscribeCount, Is.EqualTo(1));
            Assert.That(_surface.UnsubscribeCount, Is.EqualTo(1));
            Assert.That(_audio.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async System.Threading.Tasks.Task CancellationAfterAudioRegistrationReleasesTheClipWithoutPublishingBoot()
        {
            using var cancellation = new CancellationTokenSource();
            _audio.OnRegister = cancellation.Cancel;

            PlayStartResult result = await _service.StartAsync(Request(), cancellation.Token);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Cancelled));
            Assert.That(_audio.ReleaseCount, Is.EqualTo(1));
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task ReadingYieldsWithTheStartLockHeldAndCanBeCancelledBeforeAudio()
        {
            using var cancellation = new CancellationTokenSource();
            string progress = null;
            var start = _service.StartAsync(Request(), cancellation.Token, value => progress = value);
            Assert.That(progress, Is.EqualTo("正在读取谱面..."));
            Assert.That(start.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_service.IsStarting, Is.True);
            cancellation.Cancel();
            Assert.That((await start).Status, Is.EqualTo(PlayStartStatus.Cancelled));
            Assert.That(_audio.ReleaseCount, Is.Zero);
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task AStartedLiveKeepsTheLockUntilTheSceneExitsAndAllowsTheNextOne()
        {
            PlayStartResult first = await _service.StartAsync(Request(_fixtureDirectory), CancellationToken.None);

            Assert.That(first.Status, Is.EqualTo(PlayStartStatus.Started));
            Assert.That(_boot.PublishCount, Is.EqualTo(1));
            Assert.That(_boot.RestoreCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.True);
            Assert.That(_surface.SceneExitHandlers, Is.EqualTo(1));
            Assert.That(_surface.PushedLiveLoading, Is.EqualTo(1));
            Assert.That(_audio.ReleaseCount, Is.Zero);
            // The published boot must carry the deck the port reported, not one read from a singleton.
            var published = _boot.State.Boot as FreeLiveBootData;
            Assert.That(published, Is.Not.Null);
            Assert.That(published.DeckData.UserInfo.DeckId, Is.EqualTo(7));

            PlayStartResult whileRunning = await _service.StartAsync(Request(_fixtureDirectory), CancellationToken.None);
            Assert.That(whileRunning.Status, Is.EqualTo(PlayStartStatus.Busy));

            _surface.RaiseSceneExit();

            Assert.That(_service.IsStarting, Is.False);
            Assert.That(_surface.SceneExitHandlers, Is.Zero);
            Assert.That(_surface.UnsubscribeCount, Is.EqualTo(1));

            PlayStartResult second = await _service.StartAsync(Request(_fixtureDirectory), CancellationToken.None);
            Assert.That(second.Status, Is.EqualTo(PlayStartStatus.Started));
            Assert.That(_boot.PublishCount, Is.EqualTo(2));
            Assert.That(_surface.SubscribeCount, Is.EqualTo(2));
            Assert.That(_audio.ReleaseCount, Is.Zero);
        }

        [TestCase(false, 0f)]
        [TestCase(true, 11f)]
        public async System.Threading.Tasks.Task AStartedLiveUsesTheMirrorSettingWhenConvertingTheChart(bool mirrored, float lane)
        {
            LiveSettingData settings = _settings.Load();
            bool previous = settings.IsMirror;
            try
            {
                settings.IsMirror = mirrored;
                PlayStartResult result = await _service.StartAsync(Request(), CancellationToken.None);

                Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Started), result.Message);
                var boot = (FreeLiveBootData)_boot.State.Boot;
                Assert.That(boot.MusicData.MusicScore.NoteArray[0].LaneStartF, Is.EqualTo(lane));
            }
            finally
            {
                settings.IsMirror = previous;
            }
        }

        [Test]
        public async System.Threading.Tasks.Task AStartedLiveRegistersTheRecorderTheSceneWillRead()
        {
            PlayHistoryRecorder.Current = null;

            PlayStartResult result = await _service.StartAsync(Request(_fixtureDirectory), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Started));
            Assert.That(PlayHistoryRecorder.Current, Is.SameAs(_playHistory));
        }

        private static PlayRequest Request()
            => Request(_fixtureDirectory);

        [Test]
        public async System.Threading.Tasks.Task LeavingTheLibraryWhileReadingDoesNotRegisterAudio()
        {
            var start = _service.StartAsync(Request(), CancellationToken.None);
            _surface.Scene = SceneManager.Scene.Core;
            bool registered = false;
            _audio.OnRegister = () => registered = true;

            Assert.That((await start).Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(registered, Is.False);
            Assert.That(_boot.PublishCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task LinkedSusChartCanStartFromAnUncachedFolder()
        {
            string directory = Directory.CreateDirectory(Path.Combine(_root, "sus")).FullName;
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_fixtureDirectory, "manifest.json")));
            manifest["scoreFileName"] = "linked.sus";
            File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToString());
            File.Copy("Tools/fixtures/sus-p2-6/linked.sus", Path.Combine(directory, "linked.sus"));
            File.Copy(Path.Combine(_fixtureDirectory, "audio.wav"), Path.Combine(directory, "audio.wav"));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int audioThread = 0;
            _audio.OnRegister = () => audioThread = Thread.CurrentThread.ManagedThreadId;

            var previousFrame = Sekai.Live.MusicScore.CurrentFrameInfo;
            try
            {
                Sekai.Live.MusicScore.CurrentFrameInfo.speedRatio = 2f;
                var result = await _service.StartAsync(Request(directory), CancellationToken.None);

                Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Started), result.Message);
                Assert.That(audioThread, Is.EqualTo(mainThread));
                Assert.That(((FreeLiveBootData)_boot.State.Boot).MusicData.MusicScore.NoteArray, Is.Not.Empty);
                Assert.That(Sekai.Live.MusicScore.CurrentFrameInfo.speedRatio, Is.EqualTo(2f));
            }
            finally { Sekai.Live.MusicScore.CurrentFrameInfo = previousFrame; }
        }

        [TestCase(true, true, PlayStartStatus.Started)]
        [TestCase(false, true, PlayStartStatus.Failed)]
        [TestCase(true, false, PlayStartStatus.Failed)]
        public async System.Threading.Tasks.Task NativeDecorationPlaybackRequiresAutoAndVisualContent(
            bool auto, bool hasFakeKey, PlayStartStatus expected)
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
                "OpenSekai-visual-playback-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_fixtureDirectory, "manifest.json")));
                manifest["chartFormat"] = "next-rush";
                File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToString());
                File.Copy(Path.Combine(_fixtureDirectory, "audio.wav"), Path.Combine(directory, "audio.wav"));
                var entities = JArray.Parse(@"[
                    {'archetype':'Initialization','data':[{'name':'revision','value':2}]},
                    {'archetype':'#BPM_CHANGE','data':[{'name':'#BEAT','value':0},{'name':'#BPM','value':120}]}
                ]");
                if (hasFakeKey)
                    entities.Add(JObject.Parse(@"{'archetype':'FakeNormalTapNote','data':[
                        {'name':'#BEAT','value':2},{'name':'lane','value':0},{'name':'size','value':1}]}"));
                File.WriteAllText(Path.Combine(directory, "score.json"),
                    new JObject { ["bgmOffset"] = 0, ["entities"] = entities }.ToString());

                var result = await _service.StartAsync(new PlayRequest(directory, auto, SceneManager.Scene.Library),
                    CancellationToken.None);
                Assert.That(result.Status, Is.EqualTo(expected), result.Message);
                Assert.That(_surface.PushedLiveLoading, Is.EqualTo(expected == PlayStartStatus.Started ? 1 : 0));
                if (expected == PlayStartStatus.Started)
                {
                    var boot = (FreeLiveBootData)_boot.State.Boot;
                    Assert.That(boot.IsAuto, Is.True);
                    Assert.That(boot.MusicData.TotalNoteCount, Is.Zero);
                }
                else Assert.That(result.Message, Is.EqualTo(hasFakeKey
                    ? "此谱面仅含装饰，请选择自动游玩。" : "没有可游玩的音符。"));
            }
            finally { Directory.Delete(directory, true); }
        }

        private static PlayRequest Request(string directory)
        {
            return new PlayRequest(directory, false, SceneManager.Scene.Library);
        }

        private sealed class FakeBootPublisher : IPlayBootPublisher
        {
            public LiveBootState State { get; set; }
            public int CaptureCount { get; private set; }
            public int PublishCount { get; private set; }
            public int RestoreCount { get; private set; }
            public LiveBootState LastRestored { get; private set; }

            public LiveBootState Capture()
            {
                CaptureCount++;
                return State;
            }

            public void Publish(FreeLiveBootData boot)
            {
                PublishCount++;
                State = new LiveBootState(boot, PlayMode.SoloLive);
            }

            public void Restore(LiveBootState state)
            {
                RestoreCount++;
                LastRestored = state;
                State = state;
            }
        }

        private sealed class FakeAudioRegistrar : IPackageAudioRegistrar
        {
            public bool Result { get; set; }
            public Action OnRegister { get; set; }
            public int ReleaseCount { get; private set; }

            public UniTask<bool> RegisterAsync(SongPackage package, CancellationToken cancellationToken)
            {
                OnRegister?.Invoke();
                return UniTask.FromResult(Result);
            }

            public void Release() => ReleaseCount++;
        }

        private sealed class FakeDeckSelection : ILiveDeckSelection
        {
            public int SelectedDeckId { get; set; }
        }

        private sealed class FakePlayHistoryRecorder : IPlayHistoryRecorder
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

        private sealed class FakeSurface : IPlaySurface
        {
            private Action _sceneExit;

            public bool Exists { get; set; } = true;
            public SceneManager.Scene Scene { get; set; } = SceneManager.Scene.Library;
            public bool LibraryReady { get; set; } = true;
            public bool LiveLoadingReady { get; set; } = true;
            public bool FailOnPush { get; set; }
            public int PushedLiveLoading { get; private set; }
            public int SubscribeCount { get; private set; }
            public int UnsubscribeCount { get; private set; }
            public int SceneExitHandlers => _sceneExit?.GetInvocationList().Length ?? 0;

            public SceneManager.Scene CurrentScene => Scene;

            public bool IsLibraryReady => LibraryReady;

            public UniTask<bool> PrepareLiveLoadingAsync(CancellationToken token) => UniTask.FromResult(LiveLoadingReady);

            public void BeginSession(string chartHash)
            {
            }

            public void ForceFinishTransition()
            {
            }

            public void PushLiveLoading()
            {
                if (FailOnPush)
                {
                    throw new InvalidOperationException("Push failed.");
                }
                PushedLiveLoading++;
            }

            public void SubscribeSceneExit(Action handler)
            {
                SubscribeCount++;
                _sceneExit += handler;
            }

            public void UnsubscribeSceneExit(Action handler)
            {
                UnsubscribeCount++;
                _sceneExit -= handler;
            }

            public void RaiseSceneExit()
            {
                _sceneExit?.Invoke();
            }
        }
    }
}
