using System;
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
            const long end = 11000;
            Assert.That(SoloLiveController.LimitPracticeTimeMs(11031, end, 55.8, 55.85), Is.LessThan(end),
                "PracticePlayback sees this time and pauses the source as soon as it reaches the segment end");
            Assert.That(SoloLiveController.LimitPracticeTimeMs(10901, end, 55.85, 55.85), Is.EqualTo(end));
            Assert.That(SoloLiveController.LimitPracticeTimeMs(11031, end, 55.8, double.PositiveInfinity), Is.EqualTo(end));
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
