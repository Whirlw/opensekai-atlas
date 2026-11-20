using System.Collections;
using CriWare;
using NUnit.Framework;
using Sekai;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public class HoldLoopPlaybackTests
    {
        [UnityTest]
        public IEnumerator HoldRepeatsPastClipEndAndTheNextOneShotEndsNormally()
        {
            var clip = AudioClip.Create("short hold", 2205, 1, 44100, false);
            var bank = CriAtomExAcb.CreateFromAudioClip("hold", clip);
            using var player = new CriAtomExPlayer();
            try
            {
                player.SetCue(bank, "hold");
                var hold = player.Start(0, loop: true);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(hold.GetStatus(), Is.EqualTo(CriAtomExPlayback.Status.Playing));
                hold.Stop();
                Assert.That(hold.GetStatus(), Is.EqualTo(CriAtomExPlayback.Status.Stop));

                var hit = player.Start();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(hit.GetStatus(), Is.Not.EqualTo(CriAtomExPlayback.Status.Playing));
            }
            finally
            {
                player.Stop();
                Object.Destroy(clip);
            }
        }

        [UnityTest]
        public IEnumerator PackageAudioSurvivesRetryAndReleasesBothAliasesWhenChangingSongs()
        {
            var sound = new SoundManager();
            const string firstCue = "custom_lifetime_first";
            const string secondCue = "custom_lifetime_second";
            string firstAlias = AudioCatalog.GetLiveMusicBundleNameCandidate(firstCue);
            string secondAlias = AudioCatalog.GetLiveMusicBundleNameCandidate(secondCue);
            var first = AudioClip.Create(firstCue, 22050, 1, 44100, false);
            var second = AudioClip.Create(secondCue, 22050, 1, 44100, false);
            try
            {
                sound.RegisterPackageAudioClip(firstCue, first);
                Assert.That(sound.IsLoadedSoundBundle(firstCue), Is.True);
                Assert.That(sound.IsLoadedSoundBundle(firstAlias), Is.True);
                uint original = sound.PrepareIngameBGM(firstCue, 0f);
                Assert.That(sound.GetPlayback(original).HasValue, Is.True);

                sound.StopIngame();
                Assert.That(sound.GetPlayback(original).HasValue, Is.False);
                uint retry = sound.PrepareIngameBGM(firstCue, 0f);
                Assert.That(sound.GetPlayback(retry).HasValue, Is.True);

                sound.RegisterPackageAudioClip(secondCue, second);
                Assert.That(sound.GetPlayback(retry).HasValue, Is.False);
                Assert.That(sound.IsLoadedSoundBundle(firstCue), Is.False);
                Assert.That(sound.IsLoadedSoundBundle(firstAlias), Is.False);
                Assert.That(sound.ExistsCueName(firstCue), Is.False);
                Assert.That(sound.IsLoadedSoundBundle(secondAlias), Is.True);
                uint playback = sound.PrepareIngameBGM(secondCue, 0f);
                Assert.That(sound.GetPlayback(playback).HasValue, Is.True);
                yield return null;
                Assert.That(first == null, Is.True);
                Assert.That(second != null, Is.True);

                sound.ReleasePackageAudio();
                Assert.That(sound.GetPlayback(playback).HasValue, Is.False);
                Assert.That(sound.IsLoadedSoundBundle(secondCue), Is.False);
                Assert.That(sound.IsLoadedSoundBundle(secondAlias), Is.False);
                yield return null;
                Assert.That(second == null, Is.True);
            }
            finally
            {
                sound.ReleasePackageAudio();
                if (first != null) Object.Destroy(first);
                if (second != null) Object.Destroy(second);
            }
        }
    }
}
