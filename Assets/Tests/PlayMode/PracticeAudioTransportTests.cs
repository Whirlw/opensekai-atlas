using System.Collections;
using System.Linq;
using CriWare;
using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Shared.Playback;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class PracticeAudioTransportTests
    {
        private sealed class CalibratedPlayer : SoloLiveController
        {
            protected override void OnAwake() { }
            protected override void OnUpdate() { }
            protected override void OnExit() { }
            public void Begin()
            {
                var boot = new FreeLiveBootData(0, "master", 0) { CustomMusicScoreId = "practice_calibration" };
                boot.Practice = new PracticePlan(10, 11, new PracticeTemplate("test", new[] { PracticeStep.Play(1) }), (_, _) => { });
                BootData = boot;
                musicLength = 12000;
                currentMusicTimeMs = 10000;
                timingCompensation = new TimingCompensation(200, 50, 30);
                PracticeContinue();
            }
        }

        [UnityTest]
        public IEnumerator ClipStartsAtTheRequestedSourceSampleEvenWithOutputCalibration()
        {
            const string CUE = "custom_practice_calibration";
            var sound = SoundManager.Instance;
            var clip = AudioClip.Create(CUE, 44100 * 12, 1, 44100, false);
            var obj = new GameObject();
            try
            {
                sound.RegisterPackageAudioClip(CUE, clip);
                obj.AddComponent<CalibratedPlayer>().Begin();
                var source = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(s => s.clip == clip);
                double sampleTime = (double)source.timeSamples / clip.frequency;
                Assert.That(sampleTime, Is.EqualTo(10d).Within(.05d), "audio calibration must not crop away the first 200ms of the selected clip");
                yield return null;
            }
            finally
            {
                sound.ReleasePackageAudio();
                Object.Destroy(obj);
                if (clip != null) Object.Destroy(clip);
            }
        }

        [UnityTest]
        public IEnumerator ChangingRatePreservesPositionAndPauseSeekKeepsAudioAligned()
        {
            var sound = SoundManager.Instance;
            const string CUE = "practice_audio_transport";
            var clip = AudioClip.Create(CUE, 44100 * 10, 1, 44100, false);
            try
            {
                sound.RegisterPackageAudioClip(CUE, clip);
                sound.PrepareIngameBGM(CUE, 1f);
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
                sound.PrepareIngameBGM(CUE, 0f);
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
            const string CUE = "practice_segment_end";
            var sound = SoundManager.Instance;
            var clip = AudioClip.Create(CUE, 44100 * 5, 1, 44100, false);
            try
            {
                sound.RegisterPackageAudioClip(CUE, clip);
                foreach (float rate in new[] { .5f, 1.5f })
                {
                    sound.PrepareIngameBGM(CUE, 1f);
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
