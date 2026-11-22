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
using Sekai.Live;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackNoteTests
    {
        private sealed class VisibleNote : NoteBase
        {
            public VisibleNote(int left, int right, NoteCategory category, NoteType type)
                : base(default, 1, left, right, category, 1, 0, type)
            {
                Progress = 1;
                SetStateUnnotice(NoteState.Scrolling);
            }
        }

        [UnityTest]
        public IEnumerator NotePoolsAndLinesUseSelectedSpritesAndLayout() =>
            UniTask.ToCoroutine(async () => await RunInTemporaryStore(root => Verify(root, Generate(root), synthetic: true)));

        [UnityTest]
        public IEnumerator UpstreamPacksBindToBothNoteStyles() => UniTask.ToCoroutine(async () =>
        {
            if (!Application.isEditor) Assert.Ignore("The upstream note packs are local editor fixtures.");
            string directory = Environment.GetEnvironmentVariable("OPENSEKAI_NOTES_TEST_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.GetFullPath("LocalPacks/upstream-notes/32c91d9");
                if (!File.Exists(Path.Combine(directory, "custom01.zip"))) Assert.Ignore("Local upstream notes are unavailable.");
            }
            await RunInTemporaryStore(async root =>
            {
                await Verify(root, Path.Combine(directory, "custom01.zip"), synthetic: false);
                await Verify(root, Path.Combine(directory, "custom02.zip"), synthetic: false);
            });
        });

        private static async Task RunInTemporaryStore(Func<string, Task> run)
        {
            string root = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string previous = LiveConfig.NoteBundleName;
            try { await run(root); }
            finally
            {
                LiveConfig.NoteBundleName = previous;
                Directory.Delete(root, true);
            }
        }

        private static async Task Verify(string root, string zip, bool synthetic)
        {
            var store = new MaterialPackStore(root);
            var installed = await store.ImportAsync(zip);
            var group = installed.Groups.Single(g => g.Category == "notes");
            store.SaveNotesSelection(new MaterialGroupSelection { Groups = { installed.GroupReference(group.Id) } }, store.List().Revision);
            var prepared = new MaterialAudioPreparation(new MaterialPackStore(root));
            var boot = new FreeLiveBootData(1, "master", 0) { MaterialAudio = prepared.Audio, NoteSkin = prepared.Notes };
            var textures = new Dictionary<string, Texture>();
            try
            {
                var diagnostics = new List<string>();
                await prepared.PrepareAsync(diagnostics, CancellationToken.None);
                Assert.That(diagnostics, Is.Empty);
                for (int style = 0; style < 2; style++)
                {
                    LiveConfig.SetNoteSkinAssetBundleName(style);
                    var host = new GameObject("Note skin test");
                    try
                    {
                        var seen = new HashSet<string>();
                        foreach (var category in new[] { NoteCategory.Normal, NoteCategory.Long, NoteCategory.Connection,
                            NoteCategory.Flick, NoteCategory.Friction, NoteCategory.FrictionFlick, NoteCategory.FrictionLong })
                        foreach (var type in new[] { NoteType.Default, NoteType.Critical })
                        {
                            string name = category + (type == NoteType.Default ? "Note" : "CrtNote");
                            var prefab = AssetBundleUtility.LoadAsset<GameObject>(LiveConfig.NoteBundleName, name, false);
                            var pool = Child<NotePool>(host);
                            pool.Setup(1, prefab, 1, prepared.Notes);
                            var builtin = Child<NotePool>(host);
                            builtin.Setup(1, prefab, 1);
                            if (category == NoteCategory.Friction && type == NoteType.Default)
                                Check(new NextRushDamageNote(default, 1, 1, 1));
                            int widths = category == NoteCategory.Flick || category == NoteCategory.FrictionFlick ? 6 : 1;
                            for (int width = 1; width <= widths; width++)
                            {
                                Check(new VisibleNote(1, width, category, type));
                                if (widths > 1) Check(new VisibleNote(1, width, category, type) { Direction = NoteDirection.Left });
                            }

                            void Check(NoteBase note)
                            {
                                var view = pool.Spawn(note);
                                var original = builtin.Spawn(note);
                                var actual = view.GetComponentsInChildren<SpriteRenderer>(true);
                                var expected = original.GetComponentsInChildren<SpriteRenderer>(true);
                                for (int i = 0; i < actual.Length; i++)
                                {
                                    var reference = expected[i].sprite;
                                    if (reference == null) continue;
                                    string key = "notes." + reference.name;
                                    if (!group.Sprites.TryGetValue(key, out var slot))
                                    {
                                        Assert.That(actual[i].sprite, Is.SameAs(reference), key);
                                        continue;
                                    }
                                    var sprite = actual[i].sprite;
                                    CheckTexture(slot.File, sprite.texture, reference.texture, key);
                                    if (slot.NoteSlice != null)
                                    {
                                        float[] r = slot.NoteSlice;
                                        Assert.That(sprite.rect.x, Is.EqualTo(r[0]).Within(.0001f), key);
                                        Assert.That(sprite.rect.y, Is.EqualTo(slot.Height - r[1] - r[3]).Within(.0001f), key);
                                        Assert.That(sprite.rect.width, Is.EqualTo(r[2]).Within(.0001f), key);
                                        Assert.That(sprite.rect.height, Is.EqualTo(r[3]).Within(.0001f), key);
                                        Vector2 pivot = slot.Pivot == null ? reference.pivot / reference.rect.size
                                            : new Vector2(slot.Pivot[0], slot.Pivot[1]);
                                        Assert.That(Vector2.Distance(sprite.pivot / sprite.rect.size, pivot), Is.LessThan(.0001f), key);
                                    }
                                    Assert.That(sprite.border.x / sprite.rect.width, Is.EqualTo(reference.border.x / reference.rect.width).Within(.0001f), key);
                                    seen.Add(key);
                                }
                                if (note is VisibleNote)
                                {
                                    original.Move(.3f);
                                    for (int frame = 0; frame < 2; frame++) view.MoveWithSkin(.3f);
                                    var rootRenderer = original.GetComponent<SpriteRenderer>();
                                    if (rootRenderer != null && group.Sprites.TryGetValue("notes." + rootRenderer.sprite.name, out var body))
                                    {
                                        float progress = original.transform.localScale.y;
                                        Assert.That(progress, Is.GreaterThan(0));
                                        Assert.That(view.transform.localScale.x, Is.EqualTo(progress * body.Scale[0]).Within(.0001f));
                                        Assert.That(view.transform.localPosition.y, Is.EqualTo(original.transform.localPosition.y
                                            + progress * LiveConfig.widthX * body.Position[1]).Within(.0001f));
                                    }
                                }
                                view.Unspawn();
                                original.Unspawn();
                                Assert.That(actual.Select(r => r.sprite), Is.EqualTo(expected.Select(r => r.sprite)));
                            }
                        }
                        foreach (string kind in new[] { "long", "guide", "simultaneous" })
                        {
                            string key = "notes.line." + kind;
                            Material material;
                            string file = kind == "long" ? "longNoteLine.png" : kind == "guide" ? "traceLine_eff.png" : "simultaneousLine.png";
                            if (kind == "simultaneous")
                            {
                                var pair = Child<PairNoteLineView>(host);
                                pair.Setup(skin: prepared.Notes);
                                material = pair.Material;
                                var builtin = Child<PairNoteLineView>(host);
                                builtin.Setup();
                                var left = new VisibleNote(1, 1, NoteCategory.Normal, NoteType.Default);
                                left.SetPairNote(new VisibleNote(5, 5, NoteCategory.Normal, NoteType.Default));
                                Vector3[] Strip(PairNoteLineView line)
                                {
                                    line.Add(left);
                                    line.Execute(0);
                                    return line.GetComponent<MeshFilter>().sharedMesh.vertices;
                                }
                                var a = Strip(builtin);
                                var b = Strip(pair);
                                Assert.That(a[1].x - a[0].x, Is.GreaterThan(0));
                                Assert.That(b[1].x-b[0].x, Is.EqualTo((a[1].x-a[0].x)*group.Sprites[key].Scale[0]).Within(.0001f));
                                Assert.That(b[0].y-b[2].y, Is.EqualTo((a[0].y-a[2].y)*group.Sprites[key].Scale[1]).Within(.0001f));
                            }
                            else
                            {
                                var line = Child<NoteLineView>(host);
                                line.Setup(null, file, 1, guide: kind == "guide", skin: prepared.Notes);
                                material = line.Material;
                                var builtin = Child<NoteLineView>(host);
                                builtin.Setup(null, file, 1, guide: kind == "guide");
                                CheckConnectorLayout(builtin, line, kind == "guide", group.Sprites[key]);
                            }
                            CheckTexture(group.Sprites[key].File, material.mainTexture, AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, file, false), key);
                            seen.Add(key);
                        }
                        Assert.That(seen, Is.EquivalentTo(group.Sprites.Keys), group.Id);
                        if (!synthetic) Assert.That(seen.Count, Is.EqualTo(38));
                    }
                    finally { Object.Destroy(host); }
                    await UniTask.NextFrame();
                }
                TestContext.Out.WriteLine("Verified " + installed.PackId + " on both note sets, "
                    + group.Sprites.Count + " slots and " + textures.Count + " textures / " + installed.Digest);
            }
            finally { boot.ReleaseMaterialAudio(); }
            await UniTask.NextFrame();
            Assert.That(textures.Values.All(t => t == null), Is.True);

            void CheckTexture(string file, Texture texture, Texture builtin, string key)
            {
                Assert.That(texture, Is.Not.Null, key);
                Assert.That(texture, Is.Not.SameAs(builtin), key);
                if (textures.TryGetValue(file, out var shared)) Assert.That(texture, Is.SameAs(shared), key);
                else textures.Add(file, texture);
            }
        }

        private static void CheckConnectorLayout(NoteLineView builtin, NoteLineView skinned, bool guide, MaterialSpriteSlot slot)
        {
            MusicScoreInfo At(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);
            var timeline = new NextRushScrollTimeline(Array.Empty<NextRushScrollTimeline.Change>(), 0);
            var bar = new LongNote(At(1), 1, 2, 3, NoteCategory.Long, NoteType.Default, 1, 0);
            var end = new HiddenConnectionNote(At(2), 2, 6, 10, NoteCategory.Hidden, NoteType.Default, 1, 0);
            bar.SetNextRushScroll(timeline, 1);
            end.SetNextRushScroll(timeline, 2);
            bar.NativeConnector = new NextRushConnectorSegment(guide ? 101 : 1, 1, 1, 1);
            bar.AddNoteListAndSetupChildNote(end);
            bar.PrepareNativeVisual(1);
            end.PrepareNativeVisual(1);
            Vector3[] Draw(NoteLineView line)
            {
                line.Add(bar);
                line.Excute(1.1f);
                return line.GetComponentsInChildren<MeshFilter>().Single(f => f.GetComponent<MeshRenderer>().enabled).sharedMesh.vertices;
            }
            var a = Draw(builtin);
            var b = Draw(skinned);
            Assert.That(a.Length, Is.GreaterThanOrEqualTo(8));
            Assert.That(b.Length, Is.EqualTo(a.Length));
            // The first and last horizontal edges of each cross-section follow the same moving center.
            float judgeY = LiveConfig.JudgmentPositions[0].y;
            for (int i = 0; i < a.Length; i++)
            {
                int first = (i / 8) * 8 + (i % 2);
                float center = (a[first].x + a[first + 6].x) * .5f;
                float progress = (a[i].y - LiveConfig.SpawnPosition.y) / (judgeY - LiveConfig.SpawnPosition.y);
                Assert.That(b[i].x, Is.EqualTo(center + (a[i].x - center) * slot.Scale[0]
                    + LiveConfig.widthX * slot.Position[0] * progress).Within(.0001f));
                Assert.That(b[i].y, Is.EqualTo(judgeY + (a[i].y - judgeY) * slot.Scale[1]
                    + LiveConfig.widthX * slot.Position[1] * progress).Within(.0001f));
            }
        }

        private static T Child<T>(GameObject parent) where T : Component
        {
            var child = new GameObject(typeof(T).Name);
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent<T>();
        }

        private static string Generate(string root)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            byte[] png = texture.EncodeToPNG();
            Object.Destroy(texture);
            var slots = MaterialNoteSlots.Slots.ToDictionary(key => key, key =>
            {
                var slot = new Dictionary<string, object> { ["file"] = "test.png", ["scale"] = new[] { 1.5f, .75f },
                    ["position"] = new[] { -.25f, .5f } };
                if (!MaterialNoteSlots.IsLine(key))
                {
                    slot["rect"] = new[] { 2.5f, 4f, 24f, 16f };
                    slot["pivot"] = new[] { .25f, .75f };
                }
                return slot;
            });
            string path = Path.Combine(root, "notes.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var output = zip.CreateEntry("test.png").Open()) output.Write(png, 0, png.Length);
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(new { schemaVersion = 1, packId = "test.notes", version = "1",
                groups = new[] { new { id = "keys", category = "notes", slots } } }));
            return path;
        }
    }
}
