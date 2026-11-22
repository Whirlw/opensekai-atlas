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
    public sealed class MaterialPackResultFxTests
    {
        [UnityTest]
        public IEnumerator FiveUpstreamTemplatesPlayThroughLiveResultView() => Exercise(true);

        [UnityTest]
        public IEnumerator SequenceLayoutCancellationAndBrokenLayerFallback() => Exercise(false);

        private static IEnumerator Exercise(bool upstream) => UniTask.ToCoroutine(async () =>
        {
            string sample = Path.GetFullPath("LocalPacks/upstream-result-fx/32c91d9/upstream-result-fx.zip");
            if (upstream && (!Application.isEditor || !File.Exists(sample))) Assert.Ignore("Local upstream result effect fixture.");
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new MaterialPackStore(root);
            var boot = new FreeLiveBootData(1, "master", 0);
            var view = Object.Instantiate(Resources.Load<GameObject>("live/view/FrontUIView").GetComponentInChildren<LiveResultView>(true));
            view.gameObject.SetActive(true);
            var diagnostics = new List<string>();
            var background = view.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.sortingOrder == 199);
            var original = background.sprite;
            try
            {
                var pack = await store.ImportAsync(upstream ? sample : Generate(root));
                var group = pack.Groups.Single(g => g.Category == "resultFx");
                store.SaveResultFxSelection(new MaterialGroupSelection { Groups = { pack.GroupReference(group.Id) } }, store.List().Revision);
                async Task Prepare()
                {
                    var prepared = new MaterialAudioPreparation(new MaterialPackStore(root));
                    boot.ResultFxSkin = prepared.ResultFx;
                    boot.MaterialAudio = prepared.Audio;
                    await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                    view.Setup(false, boot.ResultFxSkin);
                }
                await Prepare();
                Assert.That(diagnostics, Is.Empty);
                Assert.That(background.sprite.name, Is.EqualTo("resultFx.background"));
                if (upstream)
                {
                    var seen = new HashSet<string> { "resultFx.background" };
                    foreach (LiveResultAnimationType outcome in Enum.GetValues(typeof(LiveResultAnimationType)))
                    {
                        view.Execute(outcome);
                        if (outcome == LiveResultAnimationType.None)
                        {
                            Assert.That(background.gameObject.activeSelf, Is.False);
                            continue;
                        }
                        await UniTask.Delay(350, ignoreTimeScale: true);
                        var anim = view.GetComponentsInChildren<Animator>(true).Single(a => a.transform != view.transform && a.runtimeAnimatorController != null);
                        var instance = anim.transform;
                        while (instance.parent != view.transform) instance = instance.parent;
                        string key = MaterialResultFxSlots.ForTemplate(instance.name.Replace("(Clone)", ""));
                        Assert.That(key, Is.EqualTo("resultFx." + char.ToLowerInvariant(outcome.ToString()[0]) + outcome.ToString().Substring(1)));
                        seen.Add(key + ".layout");
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        {
                            if (renderer is SpriteRenderer sprite)
                            {
                                string slot = key + "." + MaterialResultFxSlots.SpritePart(sprite.name);
                                Assert.That(sprite.sprite.name, Is.EqualTo(slot));
                                Assert.That(sprite.sprite.texture.name, Is.EqualTo(group.Sprites[slot].File));
                                seen.Add(slot);
                            }
                            else
                            {
                                string slot = key + (renderer is ParticleSystemRenderer ? ".particles" : ".mesh");
                                Assert.That(renderer.sharedMaterial.mainTexture.name, Is.EqualTo(group.Sprites[slot].File));
                                seen.Add(slot);
                            }
                        }
                    }
                    Assert.That(seen, Is.EquivalentTo(group.Sprites.Keys));
                }
                else
                {
                    view.Execute(LiveResultAnimationType.Failure);
                    view.Unload();
                    await UniTask.Delay(300, ignoreTimeScale: true);
                    Assert.That(view.transform.Find("resultFx.failure"), Is.Null);
                    view.Execute(LiveResultAnimationType.Failure);
                    await UniTask.Delay(350, ignoreTimeScale: true);
                    var sequence = view.transform.Find("resultFx.failure");
                    Assert.That(sequence, Is.Not.Null);
                    Assert.That(sequence.localPosition, Is.EqualTo(new Vector3(1, -2, 0)));
                    var layer = sequence.GetChild(0).GetComponent<SpriteRenderer>();
                    Assert.That(layer.gameObject.layer, Is.EqualTo(0));
                    Assert.That(layer.sortingOrder, Is.GreaterThan(background.sortingOrder));
                    Assert.That(layer.transform.localPosition, Is.EqualTo(new Vector3(1, 0, 0)));
                    Assert.That(layer.sprite.name, Does.EndWith("/0/1"));
                    Assert.That(layer.bounds.size.x, Is.EqualTo(2f * view.transform.lossyScale.x).Within(.0001f));
                    Assert.That(sequence.GetComponentsInChildren<Animator>(), Is.Empty);
                    await UniTask.Delay(600, ignoreTimeScale: true);
                    Assert.That(sequence == null || !sequence.gameObject.activeSelf, Is.True);

                    view.Setup(false);
                    boot.ReleaseMaterialAudio();
                    File.WriteAllBytes(Path.Combine(root, "installed", pack.PackId, pack.Digest, "second.png"), new byte[] { 0 });
                    await Prepare();
                    Assert.That(diagnostics.Single(), Does.Contain("second.png"));
                    view.Execute(LiveResultAnimationType.Failure);
                    await UniTask.Delay(350, ignoreTimeScale: true);
                    var fallback = view.transform.Find("fx_result_failed_v2(Clone)");
                    Assert.That(fallback, Is.Not.Null);
                    Assert.That(fallback.localScale, Is.EqualTo(new Vector3(2, 3, 1)));
                    Assert.That(fallback.localPosition, Is.EqualTo(new Vector3(-2, 1, 0)));
                    Assert.That(fallback.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sprite.name == "resultFx.failure.text"), Is.True);
                }
                view.Execute(LiveResultAnimationType.None);
                view.Setup(false);
                Assert.That(background.sprite, Is.SameAs(original));
                Assert.That(background.gameObject.activeSelf, Is.False);
            }
            finally
            {
                view.Unload();
                Object.Destroy(view.gameObject);
                boot.ReleaseMaterialAudio();
                Directory.Delete(root, true);
            }
        });

        private static string Generate(string root)
        {
            var tex = new Texture2D(4, 4);
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            var layers = new[] { "first.png", "second.png" }.Select(file => new { file, size = new[] { 108, 54 },
                offset = new[] { 54, 0 }, frames = new[] { new { rect = new[] { 0, 0, 2, 4 }, durationMs = 10 },
                    new { rect = new[] { 2, 0, 2, 4 }, durationMs = 600 } } }).ToArray();
            var slots = new Dictionary<string, object> { ["resultFx.failure"] = new { kind = "sequence", position = new[] { 108, -216 }, scale = new[] { 2, 1 }, layers },
                ["resultFx.failure.layout"] = new { scale = new[] { 2, 3 }, position = new[] { -216, 108 } },
                ["resultFx.failure.text"] = new { file = "first.png" }, ["resultFx.background"] = new { file = "first.png" } };
            var manifest = new { schemaVersion = 1, packId = "test.results", version = "1", groups = new[] { new { id = "results", category = "resultFx", slots } } };
            string path = Path.Combine(root, "result.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(zip.CreateEntry("pack.json").Open())) writer.Write(JsonConvert.SerializeObject(manifest));
            foreach (string name in new[] { "first.png", "second.png" })
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(png, 0, png.Length);
            }
            return path;
        }
    }
}
