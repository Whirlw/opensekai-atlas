using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Core;
using Cysharp.Threading.Tasks;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>
    /// The start path with its dependencies replaced: the boot rollback, the lock and the guards are
    /// exercised without the live scene, a screen manager or a real audio clip.
    /// </summary>
    public sealed class PlayServiceDependencyTests
    {
        private static readonly string FixtureDirectory = Path.GetFullPath("Tools/fixtures/baseline_tap");

        private FakeSurface _surface;
        private FakeBootPublisher _boot;
        private FakeAudioRegistrar _audio;
        private PlayService _service;

        [SetUp]
        public void SetUp()
        {
            _surface = new FakeSurface();
            _boot = new FakeBootPublisher();
            _audio = new FakeAudioRegistrar { Result = true };
            _service = new PlayService(_boot, _surface, _audio);
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
                new PlayRequest(FixtureDirectory, false, SceneManager.Scene.Core), CancellationToken.None);

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

            PlayStartResult result = await _service.StartAsync(Request(FixtureDirectory), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(PlayStartStatus.Failed));
            Assert.That(_boot.PublishCount, Is.EqualTo(1));
            Assert.That(_boot.RestoreCount, Is.EqualTo(1));
            Assert.That(_boot.LastRestored.Boot, Is.Null);
            Assert.That(_boot.LastRestored.Mode, Is.EqualTo(PlayMode.None));
            Assert.That(_service.IsStarting, Is.False);
            Assert.That(_surface.SceneExitHandlers, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task AStartedLiveKeepsTheLockUntilTheSceneExitsAndAllowsTheNextOne()
        {
            PlayStartResult first = await _service.StartAsync(Request(FixtureDirectory), CancellationToken.None);

            Assert.That(first.Status, Is.EqualTo(PlayStartStatus.Started));
            Assert.That(_boot.PublishCount, Is.EqualTo(1));
            Assert.That(_boot.RestoreCount, Is.Zero);
            Assert.That(_service.IsStarting, Is.True);
            Assert.That(_surface.SceneExitHandlers, Is.EqualTo(1));
            Assert.That(_surface.PushedLiveLoading, Is.EqualTo(1));

            PlayStartResult whileRunning = await _service.StartAsync(Request(FixtureDirectory), CancellationToken.None);
            Assert.That(whileRunning.Status, Is.EqualTo(PlayStartStatus.Busy));

            _surface.RaiseSceneExit();

            Assert.That(_service.IsStarting, Is.False);
            Assert.That(_surface.SceneExitHandlers, Is.Zero);

            PlayStartResult second = await _service.StartAsync(Request(FixtureDirectory), CancellationToken.None);
            Assert.That(second.Status, Is.EqualTo(PlayStartStatus.Started));
            Assert.That(_boot.PublishCount, Is.EqualTo(2));
        }

        private static PlayRequest Request()
        {
            return Request(FixtureDirectory);
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

            public UniTask<bool> RegisterAsync(SongPackage package, CancellationToken cancellationToken)
            {
                return UniTask.FromResult(Result);
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
            public int SceneExitHandlers => _sceneExit?.GetInvocationList().Length ?? 0;

            public SceneManager.Scene CurrentScene => Scene;

            public bool IsLibraryReady => LibraryReady;

            public bool IsLiveLoadingReady => LiveLoadingReady;

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
                _sceneExit += handler;
            }

            public void UnsubscribeSceneExit(Action handler)
            {
                _sceneExit -= handler;
            }

            public void RaiseSceneExit()
            {
                _sceneExit?.Invoke();
            }
        }
    }
}
