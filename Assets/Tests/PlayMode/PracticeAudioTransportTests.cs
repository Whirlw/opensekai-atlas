using System.Collections;
using System.Linq;
using CriWare;
using NUnit.Framework;
using Sekai;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class PracticeAudioTransportTests
    {
        [UnityTest]
        public IEnumerator ChangingRatePreservesPositionAndPauseSeekKeepsAudioAligned()
        {
            var sound = SoundManager.Instance;
            const string cue = "practice_audio_transport";
            var clip = AudioClip.Create(cue, 44100 * 10, 1, 44100, false);
            try
            {
                sound.RegisterPackageAudioClip(cue, clip);
                sound.PrepareIngameBGM(cue, 1f);
                sound.ResumePreparedPlaybackIngame();
                yield return new WaitForSecondsRealtime(.3f);
                long before = sound.GetAudioSyncedUnityTimer();
                sound.SetIngameBgmPitch(.5f);
                Assert.That(sound.GetAudioSyncedUnityTimer(), Is.EqualTo(before).Within(30),
                    "pitch changes only the future slope of the clock");
                var source = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(s => s.clip == clip);
                Assert.That(source.pitch, Is.EqualTo(.5f));
                sound.Pause();
                sound.SeekIngame(3250);
                sound.SetIngameBgmPitch(.7f);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(sound.GetAudioSyncedUnityTimer(), Is.EqualTo(3250).Within(2));
                Assert.That((double)source.timeSamples / clip.frequency, Is.EqualTo(3.25d).Within(.002d));
                sound.ResumePreparedPlaybackIngame();
                yield return new WaitForSecondsRealtime(.2f);
                double audioSeconds = (double)source.timeSamples / clip.frequency;
                Assert.That(audioSeconds, Is.GreaterThan(3.25));
                Assert.That(sound.GetAudioSyncedUnityTimer() / 1000d, Is.EqualTo(audioSeconds).Within(.14));
                sound.StopIngame();
                sound.PrepareIngameBGM(cue, 0f);
                Assert.That(source.pitch, Is.EqualTo(1f), "pooled sources used by subsequent ordinary songs start at 1x");
            }
            finally
            {
                sound.ReleasePackageAudio();
                if (clip != null) Object.Destroy(clip);
            }
        }

        [UnityTest]
        public IEnumerator SegmentStopsOnMixerClockAndCanReplayEarlierAtAnotherRate()
        {
            const string cue = "practice_segment_end";
            var sound = SoundManager.Instance;
            var clip = AudioClip.Create(cue, 44100 * 5, 1, 44100, false);
            try
            {
                sound.RegisterPackageAudioClip(cue, clip);
                foreach (float rate in new[] { .5f, 1.5f })
                {
                    sound.PrepareIngameBGM(cue, 1f);
                    sound.SetIngameBgmPitch(rate);
                    sound.ResumePreparedPlaybackIngame();
                    double deadline = sound.ScheduleIngameEnd(1.2);
                    Assert.That(deadline, Is.GreaterThan(AudioSettings.dspTime));
                    yield return new WaitForSecondsRealtime(.2f / rate + .25f);
                    Assert.That(sound.GetIngameStatus(), Is.Not.EqualTo(CriAtomExPlayback.Status.Playing),
                        "the mixer must clip the song even without controller Update polling");
                }
            }
            finally
            {
                sound.ReleasePackageAudio();
                if (clip != null) Object.Destroy(clip);
            }
        }
    }
}
