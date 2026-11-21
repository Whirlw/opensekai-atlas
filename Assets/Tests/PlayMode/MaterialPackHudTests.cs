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
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackHudTests
    {
        [UnityTest]
        public IEnumerator LayoutAndDynamicGlyphsSurviveRetryAndFallback() => Exercise(false);

        [UnityTest]
        public IEnumerator UpstreamZipDrivesTheGameplayHud() => Exercise(true);

        private static IEnumerator Exercise(bool upstream) => UniTask.ToCoroutine(async () =>
        {
            string sample = Path.GetFullPath("LocalPacks/upstream-hud/32c91d9/upstream-hud.zip");
            if (upstream && (!Application.isEditor || !File.Exists(sample)))
                Assert.Ignore("The upstream HUD is a local editor fixture.");
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new MaterialPackStore(root);
            var boot = new FreeLiveBootData(1, "master", 0);
            var view = Object.Instantiate(Resources.Load<GameObject>("live/view/FrontUIView")).GetComponent<LiveFrontView>();
            Transform Node(string key) => view.transform.Find(MaterialHudSlots.Nodes[key]);
            SpriteRenderer Part(string key) => Node(key).GetComponent<SpriteRenderer>();
            var pause = Part("hud.life.pause");
            int baseOrder = Part("hud.score.base").sortingOrder;
            var original = pause.sprite;
            var oldPosition = pause.transform.localPosition;
            var oldScale = Node("hud.life.layout").localScale;
            var life = view.GetComponentInChildren<LifeView>(true);
            var ring = Node("hud.countdown.3.ring").GetComponent<ParticleSystemRenderer>();
            var ringScale = ring.transform.localScale;
            var block = new MaterialPropertyBlock();
            try
            {
                var pack = await store.ImportAsync(upstream ? sample : Generate(root));
                var group = pack.Groups.Single();
                store.SaveHudSelection(new MaterialGroupSelection { Groups = { pack.GroupReference(group.Id) } }, store.List().Revision);
                var prep = new MaterialAudioPreparation(new MaterialPackStore(root));
                boot.HudSkin = prep.Hud;
                boot.MaterialAudio = prep.Audio;
                var diagnostics = new List<string>();
                await prep.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                for (int retry = 0; retry < 2; retry++)
                {
                    view.SetupHud(boot.HudSkin, null);
                    Assert.That(Part("hud.score.base").sortingOrder, Is.LessThan(Part("hud.score.gaugeBase").sortingOrder));
                    var score = new LiveScore { totalScore = 232, rank = ScoreRank.C };
                    view.ScoreView.UpdateScore(ref score, 23);
                    life.Excute(123);
                    foreach (var slot in group.Sprites.Where(p => MaterialHudSlots.Nodes.ContainsKey(p.Key)
                        && !MaterialHudSlots.IsLayout(p.Key) && !MaterialHudSlots.IsRing(p.Key)))
                        Assert.That(Part(slot.Key).sprite.name, Is.EqualTo(slot.Key));
                    var spec = group.Sprites["hud.life.pause"];
                    Assert.That(pause.size.x, Is.EqualTo(spec.Size[0] * (spec.Scale?[0] ?? 1f) / 108f).Within(.0001f));
                    Assert.That(pause.transform.localPosition.x, Is.EqualTo(spec.Position[0] / 108f).Within(.0001f));
                    Assert.That(Node("hud.life.layout").localScale.x, Is.EqualTo(oldScale.x * group.Sprites["hud.life.layout"].Scale[0]).Within(.0001f));
                    var cells = Node("hud.score.digit.layout").GetComponentsInChildren<SpriteRenderer>(true);
                    Assert.That(cells[0].sprite.name, Is.EqualTo("hud.score.digit.2"));
                    Assert.That(cells[2].sprite, Is.SameAs(cells[0].sprite));
                    Assert.That(cells[2].transform.localPosition.x, Is.LessThan(cells[0].transform.localPosition.x));
                    var gauge = Part("hud.life.damageGauge");
                    Assert.That(gauge.size.x, Is.EqualTo(group.Sprites["hud.life.damageGauge"].Size[0] / 108f * .123f).Within(.0001f));
                    Assert.That(gauge.transform.parent.localScale, Is.EqualTo(Vector3.one));
                    var label = view.ScoreView.transform.Find("ScoreBase/RankBase/ScoreRankText").GetComponent<SpriteRenderer>();
                    Assert.That(label.sprite.name, Is.EqualTo("hud.score.rankLabel.C"));
                    score.rank = ScoreRank.S;
                    score.totalScore = 131;
                    view.ScoreView.UpdateScore(ref score, 0);
                    Assert.That(label.sprite.name, upstream ? Is.EqualTo("hud.score.rankLabel.S") : Is.Not.EqualTo("hud.score.rankLabel.C"));
                    Assert.That(cells[0].sprite.name, Is.Not.EqualTo("hud.score.digit.2"));
                    ring.GetPropertyBlock(block);
                    Assert.That(block.GetTexture("_MainTex"), Is.Not.Null);
                    if (!upstream)
                    {
                        Assert.That(ring.transform.localScale.x, Is.EqualTo(ringScale.x * 6).Within(.0001f));
                        Assert.That(ring.transform.localScale.y, Is.EqualTo(ringScale.y * 12).Within(.0001f));
                    }
                    life.ShowSkip(() => { });
                    Assert.That(Part("hud.life.skip").gameObject.activeSelf, Is.True);
                    life.HideSkip();
                }
                view.SetupHud(null, null);
                Assert.That(Part("hud.score.base").sortingOrder, Is.EqualTo(baseOrder));
                Assert.That(ring.transform.localScale, Is.EqualTo(ringScale));
                Assert.That(pause.sprite, Is.SameAs(original));
                Assert.That(pause.transform.localPosition, Is.EqualTo(oldPosition));
                Assert.That(Node("hud.life.layout").localScale, Is.EqualTo(oldScale));
                ring.GetPropertyBlock(block);
                Assert.That(block.GetTexture("_MainTex"), Is.Null);
            }
            finally
            {
                Object.Destroy(view.gameObject);
                boot.ReleaseMaterialAudio();
                Directory.Delete(root, true);
            }
        });

        private static string Generate(string root)
        {
            var texture = new Texture2D(4, 4);
            var png = texture.EncodeToPNG();
            Object.Destroy(texture);
            var graphic = new { file = "hud.png", size = new[] { 216, 54 }, position = new[] { -20, 42 } };
            var slots = new Dictionary<string, object>
            {
                ["hud.score.gaugeBase"] = graphic,
                ["hud.countdown.layout"] = new { scale = new[] { 3, 4 } },
                ["hud.life.pause"] = graphic, ["hud.life.damageGauge"] = graphic,
                ["hud.score.digit.2"] = graphic, ["hud.score.rankLabel.C"] = graphic,
                ["hud.life.layout"] = new { scale = new[] { 2, 1 } },
                ["hud.countdown.3.ring"] = new { file = "hud.png", scale = new[] { 2, 3 } }
            };
            var manifest = new { schemaVersion = 1, packId = "test.hud", version = "1",
                groups = new[] { new { id = "overlay", category = "hud", slots } } };
            string path = Path.Combine(root, "hud.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(zip.CreateEntry("pack.json").Open())) writer.Write(JsonConvert.SerializeObject(manifest));
            using var stream = zip.CreateEntry("hud.png").Open();
            stream.Write(png, 0, png.Length);
            return path;
        }
    }
}
