using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;

namespace OpenSekai.Tests.Core
{
    public sealed class AudioClockDriftTests
    {
        private static AudioClockCorrection Feed(AudioClockDrift drift, params float[] readings)
        {
            AudioClockCorrection correction = default;
            bool decided = false;
            foreach (float reading in readings)
            {
                decided = drift.Add(reading, out correction);
            }

            Assert.That(decided, Is.True, "the readings fill exactly one window");
            return correction;
        }

        private static float[] Same(float value)
        {
            var readings = new float[AudioClockDrift.WINDOW_SIZE];
            for (int i = 0; i < readings.Length; i++)
            {
                readings[i] = value;
            }

            return readings;
        }

        [Test]
        public void NoDecisionUntilTheWindowIsFull()
        {
            var drift = new AudioClockDrift();
            for (int i = 0; i < AudioClockDrift.WINDOW_SIZE - 1; i++)
            {
                Assert.That(drift.Add(0.5f, out _), Is.False);
            }

            Assert.That(drift.Add(0.5f, out _), Is.True);
            Assert.That(drift.Count, Is.Zero, "the window starts over after a decision");
        }

        [Test]
        public void SmallErrorsAreLeftAlone()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(), Same(0.010f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.None));
            Assert.That(correction.RateScale, Is.EqualTo(1f));
        }

        [Test]
        public void ClockAheadIsSlewedSlow()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(), Same(0.030f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.Slew));
            Assert.That(correction.RateScale, Is.LessThan(1f));
            Assert.That(correction.RateScale, Is.GreaterThanOrEqualTo(1f - AudioClockDrift.MAX_SLEW_RATE));
            Assert.That(correction.JumpSeconds, Is.Zero, "a slew never steps the clock");
        }

        [Test]
        public void ClockBehindIsSlewedFast()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(), Same(-0.030f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.Slew));
            Assert.That(correction.RateScale, Is.GreaterThan(1f));
            Assert.That(correction.RateScale, Is.LessThanOrEqualTo(1f + AudioClockDrift.MAX_SLEW_RATE));
        }

        [Test]
        public void SlewRateIsProportionalUntilCapped()
        {
            AudioClockCorrection small = AudioClockDrift.Decide(0.016f, 0f);
            Assert.That(small.RateScale, Is.EqualTo(1f - 0.016f / AudioClockDrift.SLEW_TIME_CONSTANT_SECONDS).Within(1e-6f));

            AudioClockCorrection large = AudioClockDrift.Decide(0.059f, 0f);
            Assert.That(large.RateScale, Is.EqualTo(1f - AudioClockDrift.MAX_SLEW_RATE).Within(1e-6f));
        }

        [Test]
        public void ClockFarBehindJumpsForwardOntoTheAudio()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(), Same(-0.080f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.JumpForward));
            Assert.That(correction.JumpSeconds, Is.EqualTo(0.080f).Within(1e-6f));
            Assert.That(correction.RateScale, Is.EqualTo(1f));
        }

        [Test]
        public void ClockFarAheadWaitsInsteadOfSteppingBack()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(), Same(0.080f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.Wait));
            Assert.That(correction.WaitSeconds, Is.EqualTo(0.080f).Within(1e-6f));
            Assert.That(correction.JumpSeconds, Is.Zero, "the clock is never moved back");
        }

        [Test]
        public void TheFreshLowReadingsDecideNotTheStaleHighOnes()
        {
            // Chunky position reports: the audio lags by up to a 90 ms chunk, so readings spread over
            // 0..90 ms ahead. The real error is near the low end; a median (~45 ms) would slew for nothing.
            AudioClockCorrection correction = Feed(new AudioClockDrift(),
                0.090f, 0.000f, 0.070f, 0.010f, 0.050f, 0.020f, 0.080f, 0.040f, 0.060f);
            Assert.That(correction.ErrorSeconds, Is.EqualTo(0.020f).Within(1e-6f), "the lower quartile");
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.None),
                "a scattered window widens the dead band past its own estimate");
        }

        [Test]
        public void OneStrayLowReadingDoesNotTriggerAJump()
        {
            AudioClockCorrection correction = Feed(new AudioClockDrift(),
                0.005f, 0.005f, -0.500f, 0.005f, 0.005f, 0.005f, 0.005f, 0.005f, 0.005f);
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.None));
        }

        [Test]
        public void DeadBandFollowsTheScatterWithinItsLimits()
        {
            Assert.That(AudioClockDrift.DeadBand(0f), Is.EqualTo(AudioClockDrift.MIN_DEAD_BAND_SECONDS));
            Assert.That(AudioClockDrift.DeadBand(0.020f), Is.EqualTo(0.020f).Within(1e-6f));
            Assert.That(AudioClockDrift.DeadBand(1f), Is.EqualTo(AudioClockDrift.HARD_RESYNC_SECONDS * 0.5f),
                "there is always room left to slew before a hard resync");
        }

        [Test]
        public void ResetDropsAPartialWindow()
        {
            var drift = new AudioClockDrift();
            for (int i = 0; i < 5; i++)
            {
                drift.Add(-1f, out _);
            }

            drift.Reset();
            AudioClockCorrection correction = Feed(drift, Same(0f));
            Assert.That(correction.Kind, Is.EqualTo(AudioClockCorrectionKind.None), "the old readings are gone");
        }

        [Test]
        public void NonFiniteReadingsAreIgnored()
        {
            var drift = new AudioClockDrift();
            Assert.That(drift.Add(float.NaN, out _), Is.False);
            Assert.That(drift.Add(float.PositiveInfinity, out _), Is.False);
            Assert.That(drift.Count, Is.Zero);
        }
    }
}
