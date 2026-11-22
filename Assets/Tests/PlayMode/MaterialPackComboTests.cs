using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using Sekai;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackComboTests
    {
        [UnityTest]
        public IEnumerator CompleteComboSkinDrawsAllSlotsAndReusesTexturesOnRetry() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new MaterialPackStore(root);
            var boot = new FreeLiveBootData(1, "master", 0);
            ComboView view = null;
            try
            {
                // An author can exercise a complete local atlas with the same renderer assertions.
                string path = Application.isEditor ? Environment.GetEnvironmentVariable("OPENSEKAI_COMBO_TEST_ZIP") : null;
                bool generated = string.IsNullOrEmpty(path);
                var pack = await store.ImportAsync(generated ? GenerateAtlasPack(root) : path);
                TestContext.Out.WriteLine("COMBO pack " + pack.PackId + " / " + pack.Digest);
                var selected = new MaterialGroupSelection { Groups = pack.Groups.Where(g => g.Category == "combo").Select(g => pack.GroupReference(g.Id)).ToList() };
                store.SaveComboSelection(selected, store.List().Revision);
                store = new MaterialPackStore(root);
                var prepared = new MaterialAudioPreparation(store);
                boot.MaterialAudio = prepared.Audio;
                boot.ComboSkin = prepared.Combo;
                var diagnostics = new List<string>();
                await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                var prefab = Resources.Load<GameObject>("live/view/FrontUIView").GetComponentInChildren<ComboView>(true);
                view = Object.Instantiate(prefab);
                view.Setup(false);
                view.Excute(11, false);
                var builtinOne = view.NumberSpriteRenderers[0].sprite;
                view.Setup(true, boot.ComboSkin);
                var sprites = new Dictionary<string, Sprite>();
                foreach (bool ap in new[] { false, true })
                {
                    for (int digit = 0; digit < 10; digit++)
                    {
                        view.Excute(digit + 10, ap);
                        var renderer = view.NumberSpriteRenderers[0];
                        Assert.That(renderer.sprite.name, Is.EqualTo((ap ? "combo.ap.digit." : "combo.digit.") + digit));
                        Assert.That(view.EffectNumberSpriteRenderers[0].sprite, Is.SameAs(renderer.sprite));
                        foreach (var part in view.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            if (part.gameObject.activeInHierarchy && part.sprite != null && part.sprite.name.StartsWith("combo.", StringComparison.Ordinal))
                                sprites[part.sprite.name] = part.sprite;
                        }
                    }
                }
                Assert.That(sprites.Keys, Is.EquivalentTo(MaterialComboSlots.Slots));
                Assert.That(sprites.Values.Select(s => s.texture).Distinct().Count(), Is.EqualTo(1));
                var sample = sprites["combo.ap.digit.0"];
                var image = sample.texture;
                var definition = pack.Groups.SelectMany(g => g.Sprites).First(x => x.Key == sample.name).Value;
                var r = definition.Rect;
                Assert.That(sample.rect, Is.EqualTo(new Rect(r[0], definition.Height - r[1] - r[3], r[2], r[3])));
                if (generated)
                {
                    Assert.That(sample.pivot, Is.EqualTo(new Vector2(1, 3)));
                    var row = view.NumberSpriteRenderers[0].transform.parent;
                    Assert.That(row.localPosition.y, Is.EqualTo(-.5f));
                    Assert.That(row.parent.localPosition.x, Is.EqualTo(3));
                    Assert.That(row.parent.localScale.x, Is.EqualTo(1.5f));
                    Assert.That(view.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.sprite != null && r.sprite.name == "combo.ap.label").transform.localPosition.y, Is.EqualTo(1.5f));
                    view.Excute(10, true);
                    Assert.That(view.NumberSpriteRenderers[0].size, Is.EqualTo(new Vector2(2, 3)));
                    Assert.That(view.NumberSpriteRenderers[0].transform.localPosition.y, Is.EqualTo(0.5f).Within(.0001f));
                    Assert.That(view.NumberSpriteRenderers[1].transform.localPosition.x, Is.EqualTo(-1f).Within(.0001f));
                }
                view.Clear();
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.zero));
                view.Setup(true, boot.ComboSkin);
                view.Excute(10, true);
                Assert.That(view.NumberSpriteRenderers[0].sprite, Is.SameAs(sample));
                boot.ReleaseMaterialAudio();
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                await UniTask.NextFrame();
                Assert.That(image == null && sample == null, Is.True);
                selected.Overrides.Add("combo.ap.digit.0", null);
                store.SaveComboSelection(selected, store.List().Revision);
                var second = new MaterialAudioPreparation(store);
                boot.MaterialAudio = second.Audio;
                boot.ComboSkin = second.Combo;
                await second.PrepareAsync(diagnostics, CancellationToken.None);
                view.Setup(true, boot.ComboSkin);
                view.Excute(10, true);
                Assert.That(view.NumberSpriteRenderers[0].sprite.name, Is.Not.EqualTo("combo.ap.digit.0"));
                Assert.That(view.NumberSpriteRenderers[0].transform.localPosition.y, Is.Zero);

                boot.ReleaseMaterialAudio();
                File.WriteAllBytes(Path.Combine(root, "installed", pack.PackId, pack.Digest, definition.File), new byte[] { 0 });
                var damaged = new MaterialAudioPreparation(store);
                boot.MaterialAudio = damaged.Audio;
                boot.ComboSkin = damaged.Combo;
                await damaged.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics.Single(), Does.Contain("combo.digit.1"));
                view.Setup(true, boot.ComboSkin);
                view.Excute(10, false);
                Assert.That(view.NumberSpriteRenderers[1].sprite, Is.SameAs(builtinOne));
            }
            finally
            {
                if (view != null) Object.Destroy(view.gameObject);
                boot.ReleaseMaterialAudio();
                Directory.Delete(root, true);
            }
        });

        private static string GenerateAtlasPack(string root)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            var slots = MaterialComboSlots.Slots.ToDictionary(slot => slot, slot =>
                new Dictionary<string, object> { ["file"] = "combo.png", ["rect"] = new[] { 1, 1, 4, 4 } });
            slots["combo.ap.digit.0"]["pivot"] = new[] { .25f, .75f };
            slots["combo.ap.digit.0"]["offset"] = new[] { 0, 54 };
            slots["combo.ap.digit.0"]["advance"] = 216;
            slots["combo.ap.digit.0"]["size"] = new[] { 108, 216 };
            slots["combo.ap.digit.0"]["scale"] = new[] { 2f, 1.5f };
            slots["combo.ap.label"]["position"] = new[] { 0, 162 };
            slots["combo.glow.label"]["opacity"] = 0;
            var layout = new { position = new[] { 324, 108 }, scale = new[] { 1.5f, 1f }, digitsPosition = new[] { 0, -54 } };
            var manifest = new { schemaVersion = 1, packId = "test.combo", version = "1", groups = new[] { new { id = "all", category = "combo", layout, slots } } };
            string path = Path.Combine(root, "sample.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var output = zip.CreateEntry("combo.png").Open()) output.Write(png, 0, png.Length);
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(manifest));
            return path;
        }
    }
}
