using System;
using NUnit.Framework;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class PracticePlaybackTests
    {
        private double _position, _boundary, _rate;
        private bool _audioPlaying;
        private int _jumps, _exits;
        private PracticePlayback _player;

        [SetUp]
        public void Setup()
        {
            _position = _boundary = _rate = 0;
            _jumps = _exits = 0;
            _audioPlaying = false;
            var template = new PracticeTemplate("two speeds", new[]
            {
                PracticeStep.Play(0.5f), PracticeStep.Rest(2), PracticeStep.Play(0.7f), PracticeStep.Rest(2)
            });
            _player = new PracticePlayback(new PracticePlan(10, 15, template, (_, __) => { }), -2, 40,
                () => _position, (position, playing) => { _position = position; _audioPlaying = playing; _jumps++; },
                rate => _rate = rate, end => _boundary = end, () => _audioPlaying = false,
                () => _audioPlaying = true, () => _exits++);
            _player.Restart();
        }

        [Test]
        public void BothPassesPlayExactlyTheSameSliceWithRealTimeRests()
        {
            Assert.That((_position, _boundary, _rate), Is.EqualTo((10d, 15d, 0.5d)));
            Assert.That(_player.Tick(30), Is.False, "chart time, not elapsed frames, closes the slice");
            _position = 15;
            Assert.That(_player.Tick(0.05), Is.True);
            Assert.That(_audioPlaying, Is.False);
            _player.Tick(1);
            _player.TogglePause();
            _player.Tick(50);
            Assert.That(_player.Resting, Is.True);
            _player.TogglePause();
            Assert.That(_audioPlaying, Is.False);
            _player.Tick(1);
            Assert.That(_audioPlaying, Is.True);
            Assert.That(_position, Is.EqualTo(10));
            Assert.That(_rate, Is.EqualTo(0.7).Within(0.00001));
            _position = 15;
            _player.Tick(0);
            _player.Tick(2);
            Assert.That(_player.Completed, Is.True);
            Assert.That(_audioPlaying, Is.False);
            Assert.That(_exits, Is.Zero, "the player can repeat the exercise");
        }

        [Test]
        public void DspStopAtFractionalChartAndBgmOffsetsAdvancesTheSegment()
        {
            var template = new PracticeTemplate("fraction", new[] { PracticeStep.Play(1) });
            var plan = new PracticePlan(1, 2.00031, template, (_, __) => { });
            var player = new PracticePlayback(plan, 0, 10, () => _position,
                (target, playing) => _position = target, _ => { }, _ => { }, () => { }, () => { }, () => { });
            player.Restart();
            var music = new Sekai.LiveMusicData { NativeBgmOffsetSeconds = 0.12013 };
            long stop = SoloLiveController.PracticeEndTimelineMs(music, plan.EndSeconds.Value);
            _position = music.AudioToChartTime(stop / 1000d);
            player.Tick(0.016);
            Assert.That(player.Completed, Is.True);
        }

        [Test]
        public void PausingAndChangingSpeedDoesNotRejudgeTheCurrentNote()
        {
            _position = 12.3;
            _player.SetRate(2.5);
            Assert.That(_audioPlaying, Is.True);
            _player.TogglePause();
            _player.SetRate(0.01);
            Assert.That(_audioPlaying, Is.False);
            _player.Tick(100);
            _player.TogglePause();
            Assert.That(_rate, Is.EqualTo(0.01).Within(0.00001));
            Assert.That(_jumps, Is.EqualTo(1));
            Assert.That(_position, Is.EqualTo(12.3));
        }

        [Test]
        public void ScrubbingBeyondSelectedSliceEntersFreePracticeAndRestartRecoversTheTemplate()
        {
            _player.TogglePause();
            _player.Seek(23);
            _player.Seek(-99);
            Assert.That(_position, Is.EqualTo(-2));
            Assert.That(_boundary, Is.EqualTo(40));
            Assert.That(_audioPlaying, Is.False);
            _player.TogglePause();
            _position = 40;
            _player.Tick(1);
            Assert.That(_player.Completed, Is.True);
            _player.Seek(7);
            Assert.That(_player.Paused, Is.True);
            _player.Restart();
            Assert.That((_position, _boundary, _rate), Is.EqualTo((10d,15d,0.5d)));
        }

        [Test]
        public void SeekingInsideTheStepKeepsItsNextRest()
        {
            _player.TogglePause();
            _player.Seek(13);
            _player.TogglePause();
            _position = 15;
            _player.Tick(0);
            Assert.That(_player.Resting, Is.True);
        }

        [Test]
        public void InvalidRateOrUnpausedSeekCannotMoveTheAudio()
        {
            _player.SetRate(double.NaN);
            _player.SetRate(0);
            _player.SetRate(double.PositiveInfinity);
            _player.Seek(12);
            Assert.That((_position, _rate, _jumps), Is.EqualTo((10d, 0.5d, 1)));
        }

        [Test]
        public void QuitIsIdempotentAndDisablesRetainedControls()
        {
            _player.Quit();
            _player.Restart();
            _player.SetRate(2);
            _player.TogglePause();
            _player.Seek(20);
            _player.Quit();
            Assert.That(_exits, Is.EqualTo(1));
            Assert.That(_audioPlaying, Is.False);
            Assert.That(_jumps, Is.EqualTo(1));
        }
    }
}
