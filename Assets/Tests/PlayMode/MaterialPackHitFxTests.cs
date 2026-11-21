using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackHitFxTests
    {
        [UnityTest]
        public IEnumerator BindsParticleTemplatesAndKeepsLayoutThroughPoolReuse() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                await Verify(root, Generate(root));
                string local = Application.isEditor ? Environment.GetEnvironmentVariable("OPENSEKAI_HITFX_TEST_ZIP") : null;
                if (!string.IsNullOrEmpty(local)) await Verify(root, local);
            }
            finally { Directory.Delete(root, true); }
        });

        private static async Task Verify(string root, string path)
        {
            var store = new MaterialPackStore(root);
            var pack = await store.ImportAsync(path);
            var group = pack.Groups.Single(g => g.Category == "hitFx");
            Assert.That(group.Sprites.Keys, Is.EquivalentTo(MaterialHitFxSlots.Slots));
            var selection = new MaterialGroupSelection { Groups = { pack.GroupReference(group.Id) } };
            store.SaveHitFxSelection(selection, store.List().Revision);
            var prepared = new MaterialAudioPreparation(new MaterialPackStore(root));
            var boot = new FreeLiveBootData(1, "master", 0) { MaterialAudio = prepared.Audio, HitFxSkin = prepared.HitFx };
            var host = new GameObject("Hit effects");
            var resources = new Dictionary<string, Texture>();
            string previous = LiveConfig.EffectBundleName;
            try
            {
                var diagnostics = new List<string>();
                await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                for (int index = 0; index < 2; index++)
                {
                    LiveConfig.SetNoteEffectName(index);
                    var set = new GameObject("Particle set");
                    set.transform.SetParent(host.transform);
                    var tap = set.AddComponent<TapEffectView>();
                    tap.Setup(null, boot.HitFxSkin);
                    var holds = set.AddComponent<LongTapEffectView>();
                    holds.Setup(boot.HitFxSkin, autoplay: true);
                    set.AddComponent<ScreenEffectView>().Setup(boot.HitFxSkin);
                    var instances = set.GetComponentsInChildren<ParticleSystemController>(true);
                    var seen = new HashSet<string>();
                    foreach (var template in instances.GroupBy(c => c.name))
                    {
                        var controller = template.First();
                        string name = template.Key.Replace("(Clone)", "");
                        Assert.That(MaterialHitFxSlots.TryGetTemplate(name, out string key), Is.True, name);
                        var prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.EffectBundleName, name);
                        var original = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
                        var rendered = controller.GetComponentsInChildren<ParticleSystemRenderer>(true);
                        for (int r = 0; r < rendered.Length; r++)
                        {
                            var before = original[r].sharedMaterials;
                            var after = rendered[r].sharedMaterials;
                            for (int m = 0; m < before.Length; m++)
                            {
                                if (before[m] == null) continue;
                                string slot = before[m].name == "mat_note_tail_01_v2" ? key + ".trail" : key;
                                Assert.That(after[m], Is.Not.SameAs(before[m]), slot);
                                Assert.That(after[m].shader, Is.SameAs(before[m].shader), slot);
                                var texture = after[m].mainTexture;
                                Assert.That(texture, Is.Not.SameAs(before[m].mainTexture), slot);
                                string file = group.Sprites[slot].File;
                                if (resources.TryGetValue(file, out var cached)) Assert.That(texture, Is.SameAs(cached), slot);
                                else resources.Add(file, texture);
                                seen.Add(slot);
                            }
                        }
                    }
                    Assert.That(seen, Is.EquivalentTo(group.Sprites.Keys));
                    VerifyAutoplayHolds(holds, instances);
                    var note = new NoteBase(default, 1, 5, 6, NoteCategory.Flick, 1, 0, NoteType.Default) { Direction = NoteDirection.Left };
                    note.SetJudgeInfoForDecoration(NoteResult.Perfect);
                    var layout = group.Sprites["hitFx.flick.flash"];
                    // Six uses wrap a five-entry flash pool.
                    for (int i = 0; i < 6; i++) tap.Excute(note, () => true);
                    var flashes = instances.Where(c => c.name == "fx_note_flick_flash(Clone)" && c.IsPlaying).ToArray();
                    Assert.That(flashes, Has.Length.EqualTo(5));
                    foreach (var flash in flashes)
                    {
                        float lane = LiveUtility.CalcLaneTransformX(1) - LiveUtility.CalcLaneTransformX(0);
                        Assert.That(flash.transform.localPosition.x, Is.EqualTo(LiveUtility.CalcLaneTransformX(5.5f) + lane * layout.Position[0]).Within(.0001f));
                        Assert.That(flash.transform.localPosition.y, Is.EqualTo(lane * layout.Position[1]).Within(.0001f));
                        Assert.That(flash.transform.localEulerAngles.z, Is.EqualTo(15f).Within(.001f));
                        var props = new MaterialPropertyBlock();
                        var particle = flash.GetComponentInChildren<ParticleSystemRenderer>(true);
                        particle.GetPropertyBlock(props);
                        var value = props.GetVector("_HitFxLayout");
                        Assert.That(value.x, Is.EqualTo(layout.Scale[0] - 1));
                        Assert.That(value.y, Is.EqualTo(layout.Scale[1] - 1));
                    }
                    var active = flashes[0].GetComponentsInChildren<ParticleSystem>().First(p => p.isPlaying);
                    boot.HitFxSkin.SetPaused(true);
                    Assert.That(active.isPaused, Is.True);
                    boot.HitFxSkin.SetPaused(false);
                    Assert.That(active.isPlaying, Is.True);
                    boot.HitFxSkin.ResetPlayback();
                    Assert.That(flashes.All(f => !f.IsPlaying), Is.True);
                    Object.Destroy(set);
                    await UniTask.NextFrame();
                }
                TestContext.Out.WriteLine("Verified both effect sets / " + group.Sprites.Count + " slots: " + pack.PackId + " / " + pack.Digest);
            }
            finally
            {
                LiveConfig.EffectBundleName = previous;
                Object.Destroy(host);
                boot.ReleaseMaterialAudio();
            }
            await UniTask.NextFrame();
            Assert.That(resources.Values.All(t => t == null), Is.True);
        }

        private static void VerifyAutoplayHolds(LongTapEffectView view, ParticleSystemController[] effects)
        {
            MusicScoreInfo At(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);
            bool Playing(string prefab) => effects.Any(p => p.name == prefab + "(Clone)" && p.IsPlaying);
            var bar = new LongNote(At(1), 1, 2, 6, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(At(2), 2, 2, 6, NoteCategory.Normal, 1, NoteType.Default, 0));
            view.Add(bar);
            view.Excute(.99f);
            Assert.That(Playing(LivePrefabDefine.FX_LONG_HOLD_GEN), Is.False);
            foreach (float time in new[] { 1f, 1.9f })
            {
                bar.AutoJudgment(At(time));
                view.Excute(time);
                Assert.That(bar.HasFinger, Is.False);
                Assert.That(Playing(LivePrefabDefine.FX_LONG_HOLD_GEN) && Playing(LivePrefabDefine.FX_LONG_HOLD_AURA), Is.True);
            }
            CheckAuraWidth(LivePrefabDefine.FX_LONG_HOLD_AURA, 5f);
            view.Excute(2f);
            Assert.That(Playing(LivePrefabDefine.FX_LONG_HOLD_GEN) || Playing(LivePrefabDefine.FX_LONG_HOLD_AURA), Is.False);
            view.Remove(bar);

            var timeline = new NextRushScrollTimeline(Array.Empty<NextRushScrollTimeline.Change>(), 0);
            var carrier = new GuideNote(At(1), 3, 2, 3, NoteCategory.Guide, NoteType.Default, 0f, 1f);
            var end = new HiddenConnectionNote(At(2), 4, 6, 10, NoteCategory.Hidden, NoteType.Default, 1, 0);
            carrier.SetNextRushScroll(timeline, 1d);
            end.SetNextRushScroll(timeline, 2d);
            carrier.NativeConnector = new NextRushConnectorSegment(2, 1, 1, 1, hasActiveInterval: true);
            carrier.AddNoteListAndSetupChildNote(end);
            view.Add(carrier);
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = .02f;
                view.Excute(.99f);
                Assert.That(Playing(LivePrefabDefine.FX_CRITICAL_LONG_HOLD_GEN), Is.False);
                view.Excute(1f);
                var gen = effects.Single(p => p.name == LivePrefabDefine.FX_CRITICAL_LONG_HOLD_GEN + "(Clone)" && p.IsPlaying);
                Assert.That(Playing(LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA), Is.True);
                CheckAuraWidth(LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA, Mathf.Lerp(2, 5, .02f));
                float startX = gen.transform.localPosition.x;
                view.Excute(1.5f);
                Assert.That(gen.transform.localPosition.x, Is.GreaterThan(startX));
                CheckAuraWidth(LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA, Mathf.Lerp(2, 5, .52f));
                view.Excute(2f);
                Assert.That(gen.IsPlaying || Playing(LivePrefabDefine.FX_CRITICAL_LONG_HOLD_AURA), Is.False);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                view.Remove(carrier);
            }

            void CheckAuraWidth(string prefab, float lanes)
            {
                var aura = effects.Single(p => p.name == prefab + "(Clone)" && p.IsPlaying);
                var sheet = aura.GetComponentInChildren<ParticleSystem>(true);
                var camera = new GameObject("Hold width camera").AddComponent<Camera>();
                var mesh = new Mesh();
                try
                {
                    camera.enabled = false;
                    camera.transform.position = Vector3.back * 10;
                    sheet.Simulate(.12f, false, true, false);
                    sheet.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh, camera, ParticleSystemBakeMeshOptions.BakeRotationAndScale);
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                    float expected = LiveUtility.CalcLaneTransformX(lanes) - LiveUtility.CalcLaneTransformX(0);
                    Assert.That(mesh.bounds.size.x, Is.EqualTo(expected).Within(.001f), prefab);
                }
                finally
                {
                    Object.Destroy(mesh);
                    Object.Destroy(camera.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator LayeredHoldPlaysIntroLoopAndReleaseAndFallsBackWhenOneLayerIsDamaged() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var host = new GameObject("Custom hold");
            var boot = new FreeLiveBootData(1, "master", 0);
            try
            {
                var tex = new Texture2D(8, 2, TextureFormat.RGBA32, false);
                byte[] png = tex.EncodeToPNG();
                Object.Destroy(tex);
                string path = Path.Combine(root, "hold.zip");
                const string slot = "hitFx.long.hold.particles";
                var frames = Enumerable.Range(0, 4).Select(i => new { rect = new[] { i * 2, 0, 2, 2 }, durationMs = 100 }).ToArray();
                var sequence = new { kind = "sequence", scale = new[] { 2f, 1f }, position = new[] { 1f, 0f },
                    layers = new[] { "first.png", "second.png" }.Select(file => new { file, anchor = "hit", frames,
                        loopFrameRange = new[] { 1, 3 }, size = new[] { .5f, .25f }, offset = new[] { 0f, .25f } }) };
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    foreach (string file in new[] { "first.png", "second.png" })
                        using (var entry = zip.CreateEntry(file).Open()) entry.Write(png, 0, png.Length);
                    using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
                    writer.Write(JsonConvert.SerializeObject(new { schemaVersion = 1, packId = "test.hold", version = "1",
                        groups = new[] { new { id = "hold", category = "hitFx", slots = new Dictionary<string, object> { [slot] = sequence } } } }));
                }
                var store = new MaterialPackStore(root);
                var installed = await store.ImportAsync(path);
                store.SaveHitFxSelection(new MaterialGroupSelection { Groups = { installed.GroupReference("hold") } }, store.List().Revision);
                var prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.GetEffectBundleName(0), LivePrefabDefine.FX_LONG_HOLD_GEN);
                var diagnostics = new List<string>();
                async Task<ParticleSystemController> Prepare()
                {
                    var prep = new MaterialAudioPreparation(new MaterialPackStore(root));
                    boot.MaterialAudio = prep.Audio;
                    boot.HitFxSkin = prep.HitFx;
                    await prep.PrepareAsync(diagnostics, CancellationToken.None);
                    return ParticleSystemController.Create(prefab, host.transform, prep.HitFx);
                }
                var effect = await Prepare();
                Assert.That(diagnostics, Is.Empty);
                var layers = effect.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(layers.Length, Is.EqualTo(2));
                Assert.That(layers[1].sortingOrder, Is.GreaterThan(layers[0].sortingOrder));
                Assert.That(effect.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r => !r.enabled), Is.True);
                effect.Place(Vector3.zero);
                effect.Play();
                Check(0);
                effect.Sequence.Advance(.21f);
                Check(2);
                boot.HitFxSkin.SetPaused(true);
                effect.Sequence.Advance(1.1f);
                Check(2);
                boot.HitFxSkin.SetPaused(false);
                effect.Sequence.Advance(.11f);
                Check(1);
                effect.ReleaseHold();
                Check(3);
                effect.Sequence.Advance(.12f);
                Assert.That(layers.All(r => !r.enabled), Is.True);
                effect.Play();
                Check(0);
                boot.HitFxSkin.ResetPlayback();
                Assert.That(layers.All(r => !r.enabled), Is.True);
                Object.Destroy(effect.gameObject);
                boot.ReleaseMaterialAudio();
                File.WriteAllBytes(Path.Combine(root, "installed", installed.PackId, installed.Digest, "second.png"), new byte[] { 0 });
                var fallback = await Prepare();
                Assert.That(diagnostics.Single(), Does.Contain("second.png"));
                Assert.That(fallback.Sequence, Is.Null);
                fallback.Play();
                Assert.That(fallback.IsPlaying, Is.True);

                void Check(int frame)
                {
                    foreach (var layer in layers)
                    {
                        Assert.That(layer.enabled, Is.True);
                        Assert.That(layer.sprite.rect.x, Is.EqualTo(frame * 2));
                        Assert.That(layer.transform.localScale.x * 2, Is.EqualTo(.84f).Within(.0001f));
                    }
                }
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                Object.Destroy(host);
                Directory.Delete(root, true);
            }
        });

        private static string Generate(string root)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            var slots = MaterialHitFxSlots.Slots.ToDictionary(key => key, key =>
            {
                var slot = new Dictionary<string, object> { ["file"] = "particles.png" };
                if (!MaterialHitFxSlots.IsTrail(key))
                {
                    slot["scale"] = new[] { 1.5f, .75f };
                    slot["position"] = new[] { .5f, -.25f };
                }
                return slot;
            });
            string path = Path.Combine(root, "test.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var output = zip.CreateEntry("particles.png").Open()) output.Write(png, 0, png.Length);
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(new { schemaVersion = 1, packId = "test.hitfx", version = "1",
                groups = new[] { new { id = "effects", category = "hitFx", slots } } }));
            return path;
        }
    }
}
