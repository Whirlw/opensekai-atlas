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
using Sekai.Live;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackJudgmentTests
    {
        [Test]
        public void BuiltinTimingCaptionFollowsTheDisplayedGrade()
        {
            var source = Resources.Load<GameObject>("live/view/FrontUIView");
            var main = Object.Instantiate(source.GetComponentInChildren<JudgmentView>(true));
            var timing = Object.Instantiate(source.GetComponentInChildren<JudgmentDescriptionView>(true));
            try
            {
                var grade = main.GetComponentInChildren<SpriteRenderer>(true);
                var caption = timing.GetComponentInChildren<SpriteRenderer>(true);
                main.Setup(null, false);
                timing.Setup(null, new LiveSettingData { ShowFastLate = true });
                foreach (var result in new[] { NoteResult.Great, NoteResult.Good, NoteResult.Bad })
                {
                    Assert.That(main.Excute((result, default)), Is.True);
                    foreach (var direction in new[] { NoteResultDescription.Fast, NoteResultDescription.Late })
                    {
                        timing.Excute((result, direction));
                        Assert.That(caption.sprite.name, Is.EqualTo("judge_timing_" + direction.ToString().ToLowerInvariant()));
                        float gradeTop = grade.transform.localPosition.y + grade.sprite.bounds.max.y;
                        float captionBottom = caption.transform.localPosition.y + caption.sprite.bounds.min.y;
                        Assert.That(captionBottom, Is.GreaterThan(gradeTop));
                    }
                }
                foreach (var result in new[] { NoteResult.Perfect, NoteResult.Miss, NoteResult.Auto })
                {
                    timing.Excute((NoteResult.Bad, NoteResultDescription.Fast));
                    timing.Excute((result, NoteResultDescription.Late));
                    Assert.That(caption.sprite, Is.Null);
                }
                timing.Excute((NoteResult.Great, NoteResultDescription.FlickMiss));
                Assert.That(caption.sprite, Is.Null);
                timing.Setup(null, new LiveSettingData { ShowFlickMiss = true });
                timing.Excute((NoteResult.Great, NoteResultDescription.FlickMiss));
                Assert.That(caption.sprite.name, Is.EqualTo("judge_timing_flick"));
                timing.Excute((NoteResult.Great, NoteResultDescription.Fast));
                Assert.That(caption.sprite, Is.Null);
            }
            finally
            {
                Object.Destroy(main.gameObject);
                Object.Destroy(timing.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DrawsNineSlotsWithConfiguredLayoutAndRestoresBuiltinOnRetry() => UniTask.ToCoroutine(async () =>
        {
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                await ExercisePack(root, GeneratePack(root));
                string local = Application.isEditor ? Environment.GetEnvironmentVariable("OPENSEKAI_JUDGMENT_TEST_ZIP") : null;
                if (!string.IsNullOrEmpty(local)) await ExercisePack(root, local);
            }
            finally { Directory.Delete(root, true); }
        });

        private static async Task ExercisePack(string root, string zip)
        {
            var store = new MaterialPackStore(root);
            var pack = await store.ImportAsync(zip);
            var group = pack.Groups.Single(g => g.Category == "judgment");
            var selection = new MaterialGroupSelection { Groups = { pack.GroupReference(group.Id) } };
            store.SaveJudgmentSelection(selection, store.List().Revision);
            store = new MaterialPackStore(root);
            Assert.That(store.ResolveJudgment(store.List().Judgment).Select(b => b.Source).Distinct(), Is.EqualTo(selection.Groups));
            Assert.That(group.Sprites.Keys, Is.EquivalentTo(MaterialJudgmentSlots.Slots));
            Assert.That(group.Sprites.Count, Is.EqualTo(9));
            var source = Resources.Load<GameObject>("live/view/FrontUIView");
            var main = Object.Instantiate(source.GetComponentInChildren<JudgmentView>(true));
            var timing = Object.Instantiate(source.GetComponentInChildren<JudgmentDescriptionView>(true));
            var renderer = main.GetComponentInChildren<SpriteRenderer>(true);
            var caption = timing.GetComponentInChildren<SpriteRenderer>(true);
            Vector3 originalPosition = renderer.transform.localPosition;
            var originalMode = renderer.drawMode;
            var boot = new FreeLiveBootData(1, "master", 0);
            try
            {
                var diagnostics = new List<string>();
                async Task Prepare()
                {
                    var prepared = new MaterialAudioPreparation(store);
                    boot.MaterialAudio = prepared.Audio;
                    boot.JudgmentSkin = prepared.Judgment;
                    await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                    Assert.That(diagnostics, Is.Empty);
                }
                await Prepare();
                main.Setup(null, false);
                main.Excute((NoteResult.Miss, default));
                var builtinMiss = renderer.sprite;
                main.Setup(boot.JudgmentSkin, false);
                timing.Setup(boot.JudgmentSkin, new LiveSettingData { ShowFastLate = true, ShowFlickMiss = true });
                foreach (var result in new[] { NoteResult.Auto, NoteResult.Perfect, NoteResult.Great, NoteResult.Good, NoteResult.Bad, NoteResult.Miss })
                {
                    Assert.That(main.Excute((result, default)), Is.True);
                    Check(renderer, result.ToString().ToLowerInvariant());
                }
                Assert.That(main.Excute((NoteResult.Great, default)), Is.False);
                Assert.That(main.Excute((NoteResult.Miss, default)), Is.True);
                Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.zero));
                foreach (var (description, slot) in new[] { (NoteResultDescription.Fast, "fast"),
                    (NoteResultDescription.Late, "late"), (NoteResultDescription.FlickMiss, "flickMiss") })
                {
                    timing.Excute((NoteResult.Great, description));
                    Check(caption, slot);
                }
                timing.Setup(boot.JudgmentSkin, new LiveSettingData());
                timing.Excute((NoteResult.Great, NoteResultDescription.FlickMiss));
                Assert.That(caption.sprite, Is.Null);
                timing.Excute((NoteResult.Bad, NoteResultDescription.Fast));
                Assert.That(caption.sprite, Is.Null);
                main.Setup(boot.JudgmentSkin, true);
                main.Excute((NoteResult.Auto, default));
                Check(renderer, "perfect");
                var image = renderer.sprite.texture;
                boot.ReleaseMaterialAudio();
                await UniTask.NextFrame();
                Assert.That(image == null, Is.True);

                selection.Overrides["judgment.miss"] = null;
                store.SaveJudgmentSelection(selection, store.List().Revision);
                await Prepare();
                main.Setup(boot.JudgmentSkin, false);
                main.Excute((NoteResult.Perfect, default));
                main.Excute((NoteResult.Miss, default));
                Assert.That(renderer.sprite, Is.SameAs(builtinMiss));
                Assert.That(renderer.transform.localPosition, Is.EqualTo(originalPosition));
                Assert.That(renderer.drawMode, Is.EqualTo(originalMode));
                TestContext.Out.WriteLine("Verified nine judgment slots: " + pack.PackId + " / " + pack.Digest);
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                Object.Destroy(main.gameObject);
                Object.Destroy(timing.gameObject);
            }

            void Check(SpriteRenderer target, string suffix)
            {
                string slot = "judgment." + suffix;
                var def = group.Sprites[slot];
                Assert.That(target.sprite.name, Is.EqualTo(slot));
                for (int axis = 0; axis < 2; axis++)
                {
                    float position = def.Position[axis] + (def.Offset?[axis] ?? 0f);
                    float size = def.Size[axis] * (def.Scale?[axis] ?? 1f);
                    Assert.That(target.transform.localPosition[axis] * 108, Is.EqualTo(position).Within(.001f), slot);
                    Assert.That(target.size[axis] * 108, Is.EqualTo(size).Within(.001f), slot);
                }
            }
        }

        private static string GeneratePack(string root)
        {
            var texture = new Texture2D(4, 4);
            byte[] png = texture.EncodeToPNG();
            Object.Destroy(texture);
            var slots = MaterialJudgmentSlots.Slots.ToDictionary(k => k, k => new { file = "glyph.png",
                size = new[] { 240, 80 }, position = new[] { 10, -50 }, offset = new[] { -2, 16 }, scale = new[] { .8f, 1.5f } });
            var manifest = new { schemaVersion = 1, packId = "test.judgment", version = "1",
                groups = new[] { new { id = "main", category = "judgment", slots } } };
            string path = Path.Combine(root, "sample.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var output = zip.CreateEntry("glyph.png").Open()) output.Write(png, 0, png.Length);
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(manifest));
            return path;
        }
    }
}
