using System;
using System.Reflection;
using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class PracticeTransportRegressionTests
    {
        private sealed class Probe : SoloLiveController
        {
            protected override void OnAwake() { }
            protected override void OnExit() { }
            internal (float logic, float view) Sample(double speed)
            {
                var boot = new FreeLiveBootData(0, "master", 0);
                boot.Practice = new PracticePlan(10, 11, new PracticeTemplate("test", new[] { PracticeStep.Play(1) }), (_, _) => { });
                BootData = boot;
                TimelinePlaybackRate = speed;
                currentMusicTimeMs = 10000;
                timingCompensation = new TimingCompensation(200, 50, 30);
                return (CurrentLogicChartTime, CurrentViewTimeSeconds);
            }
        }

        // Attach the same logic as the live scene; only replace the physical audio clock and
        // the visual prefab array. Every input below goes through SoloLiveController.OnUpdate.
        private sealed class FrameProbe : SoloLiveController
        {
            private long _nextSourceMs;
            private double _wall;
            protected override void OnAwake() { }
            protected override void OnExit() { }
            protected override void UpdateMusicTime()
            {
                currentMusicTimeMs = _nextSourceMs;
                currentGameTime = _wall;
            }
            internal void Begin(LiveHarness harness, MusicScore score)
            {
                var boot = new FreeLiveBootData(0, "master", 0);
                boot.MusicData.MusicScore = score;
                boot.Practice = new PracticePlan(0, 1, new PracticeTemplate("take", new[] { PracticeStep.Play(1) }), (_, _) => { });
                boot.ResolvePracticeRange(2);
                BootData = boot;
                if (score.EventArray != null) harness.Logic.Session.Bind(boot, Array.Empty<LiveViewBase>());
                musicLength = 2000;
                const BindingFlags FIELDS = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(SoloLiveController).GetField("_liveLogic", FIELDS).SetValue(this, harness.Logic);
                typeof(SoloLiveController).GetField("_liveViews", FIELDS).SetValue(this, Array.Empty<LiveViewBase>());
                OnRhythmGameStart();
            }
            internal void Frame(long sourceMs, double wall)
            {
                _nextSourceMs = sourceMs;
                _wall = wall;
                OnUpdate();
            }
        }

        [TestCase(.995f, .995d, 1000, true)]
        [TestCase(1f, .985d, 990, false)]
        [TestCase(1.030f, .985d, 990, false)]
        [TestCase(.995f, 1.005d, 1010, false)]
        public void SegmentJudgesOnlyRealTouchesAndNotesInsideItsHalfOpenInterval(float noteTime,
            double sampleTime, long deliveryMs, bool shouldJudge)
        {
            using var harness = new LiveHarness();
            var info = new MusicScoreInfo(0, 0, noteTime, 120, 4, 1, 1);
            var note = new NormalNote(info, 1, 4, 4, NoteCategory.Normal, 1, NoteType.Default, 0);
            var score = new MusicScore(null, null, new NoteBase[] { note });
            harness.Arrange(score);
            harness.BindViews();
            var obj = new GameObject();
            try
            {
                var probe = obj.AddComponent<FrameProbe>();
                probe.Begin(harness, score);
                probe.Frame(950, 100.95);
                harness.AddTouch(1, LiveConfig.JudgmentPositions[4], 100 + sampleTime);
                probe.Frame(deliveryMs, 100 + deliveryMs / 1000d);
                Assert.That(note.Result != NoteResult.None, Is.EqualTo(shouldJudge));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [Test]
        public void LastFrameExecutesTheEventBeforeTheCutAndLeavesTheEndEventPending()
        {
            using var harness = new LiveHarness();
            var before = new EventBase(new MusicScoreInfo(0, 0, .996f, 120, 4, 1, 1));
            var end = new EventBase(new MusicScoreInfo(0, 0, 1, 120, 4, 1, 1));
            var score = new MusicScore(null, new[] { before, end }, Array.Empty<NoteBase>());
            var obj = new GameObject();
            try
            {
                var probe = obj.AddComponent<FrameProbe>();
                probe.Begin(harness, score);
                probe.Frame(950, 100.95);
                probe.Frame(1000, 101);
                Assert.That(before.State, Is.EqualTo(EventState.Done));
                Assert.That(end.State, Is.EqualTo(EventState.Wait));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [TestCase(.5d)]
        [TestCase(2d)]
        public void DeviceOffsetsRemainWallTimeWhenMusicSpeedChanges(double speed)
        {
            var obj = new GameObject();
            try
            {
                var clocks = obj.AddComponent<Probe>().Sample(speed);
                // Practice clock 10 is source second 10. Subtract 200 ms of physical output
                // latency before computing the visual (+50) and input (-30) corrections.
                Assert.That(clocks.logic, Is.EqualTo(10 - .230 * speed).Within(.00001));
                Assert.That(clocks.view, Is.EqualTo(10 - .150 * speed).Within(.00001));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [Test]
        public void EarlyPresentationClockCannotCutAudioBeforeTheScheduledSample()
        {
            const long END = 11000;
            Assert.That(SoloLiveController.LimitPracticeTimeMs(11031, END, 55.8, 55.85), Is.LessThan(END),
                "PracticePlayback sees this time and pauses the source as soon as it reaches the segment end");
            Assert.That(SoloLiveController.LimitPracticeTimeMs(10901, END, 55.85, 55.85), Is.EqualTo(END));
            Assert.That(SoloLiveController.LimitPracticeTimeMs(11031, END, 55.8, double.PositiveInfinity), Is.EqualTo(END));
        }

        [Test]
        public void RateChangesInsideDeviceLatencyWindowsUseOnlyTimeActuallyPlayedAtEachSpeed()
        {
            var history = new PracticeRateHistory(1);
            history.Change(10, 2);
            history.Change(10.05, .5);
            Assert.That(history.Distance(9.9, 10.15), Is.EqualTo(.1 + .1 + .05).Within(1e-9));
            Assert.That(history.Distance(10.15, 9.9), Is.EqualTo(-.25).Within(1e-9));
            // A late sample acquired before the rate switch belongs to the old portion of the
            // timeline, even when the current device delay puts the whole interval in the past.
            using var h = new LiveHarness();
            h.Arrange(new MusicScore(null, null, Array.Empty<NoteBase>()));
            h.Logic.Clock.Rates = history;
            h.Logic.Clock.InputWallOffset = -.1;
            h.OnUpdate(100, 10.15);
            Assert.That(h.Logic.ResolveInputMusicTime(10.05), Is.EqualTo(99.85).Within(.00001));
            history.Reset(3);
            Assert.That(history.Distance(9.9, 10.15), Is.EqualTo(.75).Within(1e-9));
        }

        [Test]
        public void ScrubbingKeepsTheMouseButtonHeldAcrossMultipleSeeks()
        {
            using var h = new LiveHarness();
            h.Arrange(new MusicScore(null, null, Array.Empty<NoteBase>()));
            var mouse = Mouse.current;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(mouse.leftButton.isPressed, Is.True);
            h.Logic.SeekPractice(1);
            h.Logic.SeekPractice(2);
            Assert.That(mouse.leftButton.isPressed, Is.True);
        }
    }
}
