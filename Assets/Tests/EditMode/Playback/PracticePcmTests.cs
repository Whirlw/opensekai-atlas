using System;
using NUnit.Framework;
using OpenSekai.Shared.Audio;

namespace OpenSekai.Tests.EditMode.Playback
{
    public sealed class PracticePcmTests
    {
        [Test]
        public void ReductionFiltersEachStereoChannelSeparatelyAndKeepsTheTail()
        {
            float[] input = { 1, -1, 1, -1, 0, 1, 0, 1, 0.25f, 0.75f };
            var result = new float[4];
            PracticeAudioBank.Reduce(input, 5, 2, result);
            Assert.That(result, Is.EqualTo(new[] { 0.5f, 0f, 0.25f, 0.75f }));
        }

        [Test]
        public void HighFrequencyNyquistToneIsAttenuatedBeforeDecimation()
        {
            var input = new float[1024];
            for (int i = 0; i < input.Length; i++) input[i] = i % 2 == 0 ? 1f : -1f;
            var output = new float[input.Length / 4];
            PracticeAudioBank.Reduce(input, input.Length, 1, output);
            Assert.That(output, Is.All.Zero);
        }

        [Test]
        public void LongStereoSongDoesNotNeedASecondFullLengthPcmBuffer()
        {
            const int FRAMES = 48000 * 60 * 4;
            long bytes = PracticeAudioBank.EstimateBytes(FRAMES, 2);
            long originalBytes = (long)FRAMES * 2 * sizeof(float);
            Assert.That(bytes, Is.GreaterThan(originalBytes / 4));
            Assert.That(bytes, Is.LessThanOrEqualTo(originalBytes / 3 + 64));
            Assert.That(PracticeAudioBank.EstimateBytes(int.MaxValue, 8), Is.GreaterThan((long)int.MaxValue));
            Assert.That(PracticeAudioBank.EstimateBytes(1, 2), Is.Zero);
        }
    }
}
