using OpenSekai.MaterialPacks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AcbDecoder;
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
        public IEnumerator SelectedHitSoundPackPlaysItsOwnBankWithOppositeBuiltInSetting() => UniTask.ToCoroutine(async () =>
        {
            string folder = Path.GetFullPath("LocalPacks/upstream-audio/32c91d9");
            var styles = new[] { "custom01", "custom02" };
            if (!Application.isEditor || styles.Any(s => !File.Exists(Path.Combine(folder, "hit-sounds", s + ".zip"))))
                Assert.Ignore("The upstream hit sound packs are local editor fixtures.");
            string root = Path.Combine(Application.temporaryCachePath, "hit-sounds-" + Guid.NewGuid().ToString("N"));
            var store = new MaterialPackStore(root);
            var previous = UserDataManager.Instance.FreeLiveBootData;
            string previousSe = LiveConfig.NoteSeName, previousView = LiveConfig.NoteSeViewName;
            var sound = SoundManager.Instance;
            try
            {
                for (int i = 0; i < styles.Length; i++)
                {
                    string style = styles[i];
                    LiveConfig.SetNoteSeName(1 - i);
                    sound.LoadSoundBundle("live/tap_se/" + LiveConfig.NoteSeName, true);
                    var pack = await store.ImportAsync(Path.Combine(folder, "hit-sounds", style + ".zip"));
                    var group = pack.Groups.Single();
                    var bank = AcbFile.Load(Path.Combine(folder, "source", style + ".acb"));
                    Assert.That(group.Slots.Keys.Select(MaterialPackAudio.Cue), Is.EquivalentTo(bank.CueWaveforms.Keys));
                    Assert.That(group.Slots.Count, Is.EqualTo(13));
                    store.SaveSelection(new MaterialAudioSelection { Groups = { pack.GroupReference(group.Id) } }, store.List().Revision);
                    var prepared = new MaterialAudioPreparation(new MaterialPackStore(root));
                    var boot = new FreeLiveBootData(1, "master", 0) { MaterialAudio = prepared.Audio };
                    try
                    {
                        var diagnostics = new List<string>();
                        await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                        Assert.That(diagnostics, Is.Empty);
                        UserDataManager.Instance.FreeLiveBootData = boot;
                        foreach (var slot in group.Slots)
                        {
                            string cue = MaterialPackAudio.Cue(slot.Key);
                            bool loop = MaterialPackAudio.UsesLoop(slot.Key);
                            uint playback = loop ? sound.PlayHoldLoop(cue) : sound.PlayIngameSE(cue);
                            Assert.That(playback, Is.Not.Zero, cue);
                            CheckUpstreamMix(bank, cue, Playing(slot.Value.File, loop).clip, loop);
                            sound.StopSE(playback);
                            if (MaterialPackAudio.AlsoUsesOneShot(slot.Key))
                            {
                                sound.PlayIngameSEOneShot(cue);
                                CheckUpstreamMix(bank, cue, Playing(slot.Value.File, false).clip, false);
                            }
                        }
                    }
                    finally { boot.ReleaseMaterialAudio(); }
                }
            }
            finally
            {
                UserDataManager.Instance.FreeLiveBootData = previous;
                LiveConfig.NoteSeName = previousSe;
                LiveConfig.NoteSeViewName = previousView;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        });

        private static void CheckUpstreamMix(AcbFile bank, string cue, AudioClip clip, bool loop)
        {
            // The local PCM16 exports share one gain so layering keeps the relative volume across both banks.
            const float MIX_GAIN = .70483696f;
            var layers = bank.CueWaveforms[cue].Select(layer =>
                (wave: bank.Decode(bank.Entries.Single(e => e.WaveId == layer.WaveId)), gain: layer.VolumeScale)).ToArray();
            var reference = layers[0].wave;
            int start = loop && reference.HasLoop ? reference.LoopStartSample : 0;
            int end = loop && reference.HasLoop ? reference.LoopEndSample : layers.Max(x => x.wave.SampleCount);
            Assert.That(clip.channels, Is.EqualTo(reference.Channels), cue);
            Assert.That(clip.frequency, Is.EqualTo(reference.SampleRate), cue);
            Assert.That(clip.samples, Is.EqualTo(end - start), cue);
            var actual = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(actual, 0), Is.True, cue);
            for (int i = 0; i < actual.Length; i++)
            {
                int index = start * clip.channels + i;
                float mixed = 0f;
                foreach (var layer in layers)
                    if (index < layer.wave.Samples.Length) mixed += layer.wave.Samples[index] * layer.gain;
                if (Math.Abs(actual[i] - mixed * MIX_GAIN) > 2f / 32768f)
                    Assert.Fail($"{cue} differs from its ACB at PCM sample {index}.");
            }
        }

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
