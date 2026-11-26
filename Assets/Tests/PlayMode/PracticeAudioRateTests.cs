using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Shared.Audio;
using Sekai;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class PracticeAudioRateTests
    {
        [UnityTest]
        public IEnumerator HighSpeedClipContainsTheExpectedMusicAcrossBlockAndChannelBoundaries() =>
            UniTask.ToCoroutine(async () =>
            {
                const int FRAMES = 8192 + 5;
                const int RATE = 44100;
                var original = AudioClip.Create("practice-rate-pcm", FRAMES, 2, RATE, false);
                var data = new float[FRAMES * 2];
                for (int i = 0; i < FRAMES; i++)
                {
                    data[2 * i] = (float)i / FRAMES;
                    data[2 * i + 1] = -data[2 * i];
                }
                original.SetData(data, 0);
                try
                {
                    using var bank = await PracticeAudioBank.PrepareAsync(original, CancellationToken.None);
                    Assert.That(bank.Select(0.005f, out int slowStride), Is.SameAs(original));
                    Assert.That(slowStride, Is.EqualTo(1));
                    var proxy = bank.Select(8, out int stride);
                    Assert.That(stride, Is.EqualTo(4));
                    Assert.That(proxy.frequency, Is.EqualTo(RATE));
                    Assert.That(proxy.samples, Is.EqualTo((FRAMES + 3) / 4));
                    var pair = new float[2];
                    foreach (int frame in new[] { 0, 2047, 2048, 2049 })
                    {
                        Assert.That(proxy.GetData(pair, frame), Is.True);
                        int start = frame * 4;
                        int count = Math.Min(4, FRAMES - start);
                        float expected = data.Where((_, i) => (i & 1) == 0).Skip(start).Take(count).Average();
                        Assert.That(pair[0], Is.EqualTo(expected).Within(.0001));
                        Assert.That(pair[1], Is.EqualTo(-expected).Within(.0001));
                    }
                    Assert.That(bank.Select(16, out int fasterStride).samples, Is.EqualTo((FRAMES + 15) / 16));
                    Assert.That(fasterStride, Is.EqualTo(16));
                    bank.Select(3072, out int fastestStride);
                    Assert.That(fastestStride, Is.EqualTo(1024));
                }
                finally { Object.Destroy(original); }
            });

        [UnityTest]
        public IEnumerator ActualSourceAndDeadlinesUseSongSecondsOnBothSidesOfNativePitchLimit() =>
            UniTask.ToCoroutine(async () =>
            {
                const int FREQUENCY = 24000;
                const string CUE = "practice-rate-clock";
                var original = AudioClip.Create(CUE, FREQUENCY * 45, 1, FREQUENCY, false);
                var sound = SoundManager.Instance;
                try
                {
                    sound.RegisterPackageAudioClip(CUE, original, ownsClip: false);
                    await sound.PreparePracticeAudioAsync(CancellationToken.None);
                    sound.PrepareIngameBGM(CUE, 2f);
                    var source = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(s => s.clip == original);
                    foreach (float rate in new[] { .005f, 4f, 8f, 16f, 2f })
                    {
                        sound.SetIngameBgmPitch(rate);
                        sound.SeekIngame(4000);
                        int stride = rate <= 3 ? 1 : rate <= 12 ? 4 : 16;
                        Assert.That(source.pitch, Is.EqualTo(rate / stride).Within(.0001), "native source pitch");
                        Assert.That(source.timeSamples, Is.EqualTo(4 * FREQUENCY / stride).Within(2));
                        Assert.That(sound.GetAudioSyncedUnityTimer(), Is.EqualTo(4000).Within(3));
                        sound.ResumePreparedPlaybackIngame();
                        double deadline = sound.ScheduleIngameEnd(6d);
                        Assert.That(deadline - AudioSettings.dspTime, Is.EqualTo(2d / rate).Within(.12));
                        sound.Pause();
                    }
                    // Cross the band during actual playback. A new clip must not restart from zero.
                    sound.SeekIngame(10000);
                    sound.ResumePreparedPlaybackIngame();
                    sound.SetIngameBgmPitch(8);
                    Assert.That((double)source.timeSamples * 4 / FREQUENCY, Is.EqualTo(10d).Within(.2));
                    await UniTask.Delay(120, ignoreTimeScale: true);
                    double songSeconds = (double)source.timeSamples * 4 / FREQUENCY;
                    Assert.That(songSeconds, Is.GreaterThan(10));
                    Assert.That(sound.GetAudioSyncedUnityTimer() / 1000d, Is.EqualTo(songSeconds).Within(.8));
                }
                finally
                {
                    sound.ReleasePackageAudio();
                    Object.Destroy(original);
                }
            });

        [Test]
        public void MissingAudioHasAPlayerReadablePreparationFailure()
        {
            var failure = Assert.Throws<InvalidDataException>(() =>
                PracticeAudioBank.PrepareAsync(null, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(failure.Message, Does.Contain("音频"));
        }

        [Test]
        public void CancelledPreparationCreatesNoProxy()
        {
            Assert.Catch<OperationCanceledException>(() =>
                PracticeAudioBank.PrepareAsync(null, new CancellationToken(true)).GetAwaiter().GetResult());
        }
    }
}
