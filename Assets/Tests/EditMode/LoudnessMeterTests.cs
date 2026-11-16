using System;
using NUnit.Framework;
using OpenSekai.Shared.Audio;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>The loudness meter against the EBU Tech 3341 reference signals.</summary>
    public class LoudnessMeterTests
    {
        private const int RATE = 48000;

        private static float? Measure(int sampleRate, int channels, params (double dbfs, int seconds)[] parts)
        {
            var meter = new LoudnessMeter(sampleRate, channels);
            var frame = new float[channels];
            long t = 0;
            foreach ((double dbfs, int seconds) in parts)
            {
                double amplitude = dbfs <= -200 ? 0 : Math.Pow(10, dbfs / 20);
                for (int i = 0; i < sampleRate * seconds; i++, t++)
                {
                    float value = (float)(amplitude * Math.Sin(2 * Math.PI * 1000 * t / sampleRate));
                    for (int c = 0; c < channels; c++)
                    {
                        frame[c] = value;
                    }
                    meter.Feed(frame, 1);
                }
            }
            return meter.Result();
        }

        [TestCase(48000)]
        [TestCase(44100)]
        public void AStereoSineAtMinus23DbfsReadsMinus23Lufs(int sampleRate)
        {
            Assert.That(Measure(sampleRate, 2, (-23, 20)), Is.EqualTo(-23f).Within(0.1f));
        }

        [Test]
        public void QuietPassagesBelowTheRelativeGateAreLeftOut()
        {
            float? lufs = Measure(RATE, 2, (-72, 10), (-36, 10), (-23, 60), (-36, 10), (-72, 10));
            Assert.That(lufs, Is.EqualTo(-23f).Within(0.1f));
        }

        [Test]
        public void SilenceHasNoLoudness()
        {
            Assert.That(Measure(RATE, 2, (-300, 5)), Is.Null);
        }

        [TestCase(-6f, 0.5012f)]
        [TestCase(-12f, 1f)]
        [TestCase(-20f, 1f)]
        public void GainTurnsLouderSongsDownToTheTargetAndLeavesQuieterOnes(float lufs, float expected)
        {
            Assert.That(LoudnessNormalization.GainFor(lufs, -12f), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void AnUnmeasuredSongKeepsItsVolume()
        {
            Assert.That(LoudnessNormalization.GainFor(null, -12f), Is.EqualTo(1f));
        }
    }
}
