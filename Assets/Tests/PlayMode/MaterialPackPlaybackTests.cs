using OpenSekai.MaterialPacks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CriWare;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using Sekai;
using Sekai.Live;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackPlaybackTests
    {
        [UnityTest]
        public IEnumerator SamplePlaysThroughLiveCuesAndReleasesOnExit() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, "material-play-" + Guid.NewGuid().ToString("N"));
            var store = new MaterialPackStore(root);
            var previous = UserDataManager.Instance.FreeLiveBootData;
            var boot = new FreeLiveBootData(1, "master", 0);
            var sound = new SoundManager();
            try
            {
                var pack = await store.ImportAsync(await SamplePath(root));
                var selection = new MaterialAudioSelection { Groups = pack.Groups.Select(g => pack.GroupReference(g.Id)).ToList() };
                store.SaveSelection(selection, 1);
                var prepared = new MaterialAudioPreparation(store);
                boot.MaterialAudio = prepared.Audio;
                var diagnostics = new List<string>();
                await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                UserDataManager.Instance.FreeLiveBootData = boot;
                uint hold = sound.PlayHoldLoop(LiveSoundDefine.SE_LIVE_LONG);
                var loop = Playing("sound/hold.wav", true);
                Assert.That(loop.clip.samples, Is.EqualTo(4800));
                sound.PlayIngameSEOneShot(LiveSoundDefine.SE_LIVE_TRACE);
                Assert.That(Playing("sound/hold.wav", false).clip.samples, Is.EqualTo(5760));
                sound.PlayIngameSE(LiveSoundDefine.SE_LIVE_TAP);
                var click = Playing("sound/click.wav", false).clip;
                sound.PlayIngameSE(LiveSoundDefine.SE_LIVE_PERFECT);
                Assert.That(Resources.FindObjectsOfTypeAll<AudioSource>().Count(x => x.clip == click), Is.EqualTo(2));
                await UniTask.Delay(250, ignoreTimeScale: true);
                Assert.That(new CriAtomExPlayback(hold).GetStatus(), Is.EqualTo(CriAtomExPlayback.Status.Playing));
                boot.MaterialAudio.Stop();
                Assert.That(new CriAtomExPlayback(hold).GetStatus(), Is.EqualTo(CriAtomExPlayback.Status.Stop));
                uint retry = sound.PlayHoldLoop(LiveSoundDefine.SE_LIVE_LONG);
                Assert.That(retry, Is.Not.Zero);
                boot.ReleaseMaterialAudio();
                Assert.That(new CriAtomExPlayback(retry).GetStatus(), Is.EqualTo(CriAtomExPlayback.Status.Stop));
                await UniTask.Yield();
                Assert.That(click == null, Is.True);
                store.SaveSelection(new MaterialAudioSelection(), 2);
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                UserDataManager.Instance.FreeLiveBootData = previous;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        });

        [UnityTest]
        public IEnumerator DamagedInstalledWaveFallsBackWhileOtherCuesPlay() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, "material-fallback-" + Guid.NewGuid().ToString("N"));
            var store = new MaterialPackStore(root);
            var previous = UserDataManager.Instance.FreeLiveBootData;
            var boot = new FreeLiveBootData(1, "master", 0);
            var sound = new SoundManager();
            var fallback = AudioClip.Create("built-in fallback", 4800, 1, 48000, false);
            try
            {
                var pack = await store.ImportAsync(await SamplePath(root));
                store.SaveSelection(new MaterialAudioSelection { Groups = pack.Groups.Select(g => pack.GroupReference(g.Id)).ToList() }, 1);
                File.WriteAllBytes(Path.Combine(root, "installed", pack.PackId, pack.Digest, "sound/click.wav"), new byte[4]);
                var prepared = new MaterialAudioPreparation(store);
                boot.MaterialAudio = prepared.Audio;
                var warnings = new List<string>();
                await prepared.PrepareAsync(warnings, CancellationToken.None);
                Assert.That(warnings.Single(), Does.Contain("audio.perfect"));
                UserDataManager.Instance.FreeLiveBootData = boot;
                sound.RegisterExternalAudioClip("fixture", LiveSoundDefine.SE_LIVE_TAP, fallback);
                sound.PlayIngameSE(LiveSoundDefine.SE_LIVE_TAP);
                Assert.That(Playing("built-in fallback", false).clip, Is.SameAs(fallback));
                sound.PlayHoldLoop(LiveSoundDefine.SE_LIVE_LONG);
                Assert.That(Playing("sound/hold.wav", true).clip.samples, Is.EqualTo(4800));
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                sound.StopAll();
                UnityEngine.Object.Destroy(fallback);
                UserDataManager.Instance.FreeLiveBootData = previous;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        });

        private static async UniTask<string> SamplePath(string root)
        {
            if (Application.isEditor) return Path.GetFullPath("Tools/fixtures/material-audio.zip");
            string source = Application.streamingAssetsPath + "/material-audio.zip";
            if (!source.Contains("://")) return source;
            using var request = UnityWebRequest.Get(source);
            await request.SendWebRequest();
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "sample.zip");
            File.WriteAllBytes(path, request.downloadHandler.data);
            return path;
        }

        private static AudioSource Playing(string name, bool loop) =>
            Resources.FindObjectsOfTypeAll<AudioSource>().First(x => x.clip != null && x.clip.name == name && x.loop == loop);
    }
}
