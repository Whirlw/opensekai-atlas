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
using Sekai.Live;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackLaneTests
    {
        private static readonly (string slot, string child)[] _parts =
        {
            ("lane.base", "LaneDefault"), ("lane.dividers", "LaneLine"), ("lane.judgeLine", "JudgeLane")
        };

        [UnityTest]
        public IEnumerator CustomLayoutSurvivesRetryAndPartialFallback() => Exercise(false);

        [UnityTest]
        public IEnumerator UpstreamZipMatchesLanePrefabAndTextures() => Exercise(true);

        private static IEnumerator Exercise(bool upstream) => UniTask.ToCoroutine(async () =>
        {
            string folder = Path.GetFullPath("LocalPacks/upstream-lane/32c91d9");
            string path = Path.Combine(folder, "upstream-lane.zip");
            if (upstream && (!Application.isEditor || !File.Exists(path)))
                Assert.Ignore("The upstream lane skin is a local editor fixture.");
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new MaterialPackStore(root);
            var boot = new FreeLiveBootData(1, "master", 0);
            var prefab = Resources.Load<GameObject>("live/view/FrontUIView");
            var view = Object.Instantiate(prefab.GetComponentInChildren<LaneView>(true));
            var renderers = view.GetComponentsInChildren<SpriteRenderer>(true).ToDictionary(r => r.name);
            float oldAlpha = LiveConfig.JudgeLineAlpha;
            try
            {
                var pack = await store.ImportAsync(upstream ? path : Generate(root));
                var group = pack.Groups.Single();
                Assert.That(group.Sprites.Keys, Is.EquivalentTo(_parts.Select(p => p.slot)));
                var selection = new MaterialGroupSelection { Groups = { pack.GroupReference(group.Id) } };
                store.SaveLaneSelection(selection, store.List().Revision);
                var prep = new MaterialAudioPreparation(new MaterialPackStore(root));
                boot.MaterialAudio = prep.Audio;
                boot.LaneSkin = prep.Lane;
                var diagnostics = new List<string>();
                await prep.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                var settings = new LiveSettingData { LaneTransparent = .4f };
                LiveConfig.JudgeLineAlpha = .7f;
                var original = _parts.ToDictionary(p => p.slot, p =>
                {
                    var r = renderers[p.child];
                    return (r.sprite, r.transform.localPosition, r.transform.localScale, r.drawMode, r.sortingOrder);
                });
                view.Setup(settings, boot.LaneSkin);
                view.Setup(settings, boot.LaneSkin);
                foreach (var (slot, child) in _parts)
                {
                    var r = renderers[child];
                    var spec = group.Sprites[slot];
                    Assert.That(r.sprite.name, Is.EqualTo(slot));
                    Assert.That(r.sortingOrder, Is.EqualTo(original[slot].sortingOrder));
                    Assert.That(r.transform.localScale, Is.EqualTo(original[slot].localScale));
                    Assert.That(r.color.a, Is.EqualTo(slot == "lane.base" ? .4f : .7f));
                    Assert.That(r.enabled, Is.True);
                    for (int axis = 0; axis < 2; axis++)
                    {
                        Assert.That(r.transform.localPosition[axis], Is.EqualTo(spec.Position[axis] / 108f).Within(.00001f));
                        Assert.That(r.size[axis], Is.EqualTo(spec.Size[axis] * spec.Scale[axis] / 108f).Within(.00001f));
                        Assert.That(r.bounds.size[axis], Is.EqualTo(r.size[axis] * r.transform.lossyScale[axis]).Within(.0001f));
                    }
                    if (upstream)
                    {
                        Assert.That(Vector3.Distance(r.transform.localPosition, original[slot].localPosition), Is.LessThan(.00001f));
                        CheckUpstreamGeometry(original[slot].sprite, r);
                        string png = Path.Combine(folder, "source/Assets/Texture2D", original[slot].sprite.name + ".png");
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(png))).Replace("-", "").ToLowerInvariant(), Is.EqualTo(spec.Hash));
                    }
                }
                settings.LaneTransparent = 0;
                view.Setup(settings, boot.LaneSkin);
                Assert.That(renderers["LaneDefault"].enabled, Is.False);
                Assert.That(renderers["LaneLine"].enabled && renderers["JudgeLane"].enabled, Is.True);
                var texture = renderers["LaneLine"].sprite.texture;
                boot.ReleaseMaterialAudio();
                await UniTask.NextFrame();
                Assert.That(texture == null, Is.True);
                // An explicitly built-in slot must recover its exact sprite and transform on the reused view.
                selection.Overrides["lane.judgeLine"] = null;
                store.SaveLaneSelection(selection, store.List().Revision);
                prep = new MaterialAudioPreparation(store);
                boot.MaterialAudio = prep.Audio;
                boot.LaneSkin = prep.Lane;
                await prep.PrepareAsync(diagnostics, CancellationToken.None);
                view.Setup(settings, boot.LaneSkin);
                Assert.That(renderers["JudgeLane"].sprite, Is.SameAs(original["lane.judgeLine"].sprite));
                Assert.That(renderers["LaneLine"].sprite.name, Is.EqualTo("lane.dividers"));
                view.Setup(settings);
                foreach (var (slot, child) in _parts)
                {
                    Assert.That(renderers[child].sprite, Is.SameAs(original[slot].sprite));
                    Assert.That(renderers[child].drawMode, Is.EqualTo(original[slot].drawMode));
                    Assert.That(renderers[child].transform.localPosition, Is.EqualTo(original[slot].localPosition));
                    Assert.That(renderers[child].transform.localScale, Is.EqualTo(original[slot].localScale));
                }
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                LiveConfig.JudgeLineAlpha = oldAlpha;
                Object.Destroy(view.gameObject);
                Directory.Delete(root, true);
            }
        });

        private static void CheckUpstreamGeometry(Sprite original, SpriteRenderer rendered)
        {
            // Every original mesh vertex must still sample the same source texel at its local position.
            var uv = original.uv;
            var vertices = original.vertices;
            var sprite = rendered.sprite;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 pixel = sprite.rect.min + sprite.pivot + vertices[i] / rendered.size * sprite.rect.size;
                Vector2 expected = Vector2.Scale(uv[i], new Vector2(sprite.texture.width, sprite.texture.height));
                Assert.That(Vector2.Distance(pixel, expected), Is.LessThan(.02f), original.name);
            }
        }

        private static string Generate(string root)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            var slots = new Dictionary<string, object>();
            for (int i = 0; i < _parts.Length; i++)
                slots.Add(_parts[i].slot, new { file = "lane.png", rect = new[] { 0, 1, 3, 2 },
                    size = new[] { 180 + 40 * i, 30 + i }, scale = new[] { .5f + i, 2f }, position = new[] { 13 * i, -17 - i } });
            var manifest = new { schemaVersion = 1, packId = "test.lane", version = "1",
                groups = new[] { new { id = "lane", category = "lane", slots } } };
            string path = Path.Combine(root, "lane.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var stream = zip.CreateEntry("lane.png").Open()) stream.Write(png, 0, png.Length);
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(manifest));
            return path;
        }
    }
}
