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
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class MaterialPackBackgroundTests
    {
        [UnityTest]
        public IEnumerator OrderedImagesAndSongJacketsSurviveMissingArtAndRetry() => Exercise(false);

        [UnityTest]
        public IEnumerator UpstreamCompositionMatchesOriginalWithJacketsAndMasks() => Exercise(true);

        private static IEnumerator Exercise(bool upstream) => UniTask.ToCoroutine(async () =>
        {
            string folder = Path.GetFullPath("LocalPacks/upstream-background/32c91d9");
            string sample = Path.Combine(folder, "upstream-background.zip");
            if (upstream && (!Application.isEditor || !File.Exists(sample)))
                Assert.Ignore("The upstream background is a local editor fixture.");
            string root = Path.Combine(upstream ? folder : Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new MaterialPackStore(root);
            var boot = new FreeLiveBootData(1, "master", 0) { TemporaryJacketData = MakePng(Color.magenta, upstream), MusicCategory = MusicCategory.mv_2d };
            var host = new GameObject("Background test controller");
            host.SetActive(false);
            var controller = host.AddComponent<BaseLiveController>();
            typeof(BaseLiveController).GetProperty(nameof(controller.BootData)).SetValue(controller, boot);
            var view = Object.Instantiate(Resources.Load<GameObject>("live/view/Background2DView")).GetComponent<Background2DView>();
            var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
            var movie = renderers.Single(r => r.name == "Movie");
            var camera = view.GetComponentsInChildren<Camera>(true).Single(c => c.name == "jacketCamera");
            var diagnostics = new List<string>();
            try
            {
                string video = Path.Combine(folder, "validation.mp4");
                if (!upstream && Application.isEditor && File.Exists(video))
                    SetVideo();
                var pack = await store.ImportAsync(upstream ? sample : MakeZip(root));
                var selection = new MaterialGroupSelection { Groups = { pack.GroupReference(pack.Groups.Single().Id) } };
                store.SaveBackgroundSelection(selection, store.List().Revision);
                await Load();
                Assert.That(diagnostics, Is.Empty);
                Assert.That(movie.gameObject.activeSelf, Is.False);
                Assert.That(camera.transform.parent.gameObject.activeSelf, Is.False);
                var custom = view.transform.Find("Background layers");
                var planes = custom.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(r => r.sortingOrder).ToArray();
                Assert.That(planes.Length, Is.EqualTo(upstream ? 16 : 4));
                Assert.That(view.GetComponentInChildren<VideoPlayer>(true), Is.Null);
                view.Retry();
                view.MusicPlaybackStart(0);
                view.OnUpdate(1);
                if (upstream)
                {
                    Assert.That(custom.GetComponentsInChildren<SpriteMask>(true).Length, Is.EqualTo(pack.Groups.Single().Background.Layers.Sum(l => l.Masks?.Length ?? 0)));
                    CompareUpstream(folder, view, custom.GetComponentInChildren<Camera>(true));
                }
                else
                {
                    var packed = ReadFrame(controller.BackgroundTexture);
                    var center = packed.GetPixel(packed.width / 2, packed.height / 2);
                    Assert.That(center.g, Is.GreaterThan(.9f));
                    Assert.That(center.r, Is.LessThan(.1f));
                    var outside = packed.GetPixel(1, packed.height / 2);
                    Assert.That(outside.b, Is.GreaterThan(.9f));
                    Object.Destroy(packed);
                    Assert.That(planes[1].sprite, Is.SameAs(planes[3].sprite));
                    var plane = planes[2].transform;
                    float units = 20f * Mathf.Tan(21f * Mathf.Deg2Rad) / 1080f;
                    CheckVector(plane.localPosition, new Vector2(108, -54) * units);
                    CheckVector(Vector3.Scale(planes[2].sprite.bounds.size, plane.localScale), new Vector2(972, 216) * units);
                    var oldTexture = planes[2].sprite.texture;
                    boot.ReleaseMaterialAudio();
                    await UniTask.NextFrame();
                    Assert.That(oldTexture == null, Is.True);
                    string installed = Directory.GetFiles(Path.Combine(root, "installed"), "full.png", SearchOption.AllDirectories).Single();
                    File.WriteAllBytes(installed, new byte[] { 0 });
                    await Load();
                    Assert.That(diagnostics.Single(), Does.Contain("background.layers.2"));
                    var fallback = ReadFrame(controller.BackgroundTexture);
                    Assert.That(fallback.GetPixel(fallback.width / 2, fallback.height / 2).r, Is.GreaterThan(.9f));
                    Object.Destroy(fallback);
                }
                boot.ReleaseMaterialAudio();
                boot.CustomMusicScorePath = null;
                store.SaveBackgroundSelection(new MaterialGroupSelection(), store.List().Revision);
                await Load();
                Assert.That(boot.BackgroundSkin, Is.Null);
                Assert.That(camera.transform.parent.gameObject.activeSelf, Is.True);
                var builtIn = camera.transform.parent;
                Assert.That(builtIn.Find("Left"), Is.Null);
                Assert.That(builtIn.Find("Right"), Is.Null);
                var jackets = renderers.Where(r => r.name == "Jacket").ToArray();
                Assert.That(jackets.Select(r => r.transform.parent.name), Is.EquivalentTo(new[] { "Main", "Bottom" }));
                Assert.That(jackets.All(r => r.gameObject.activeSelf && r.sprite != null), Is.True);
                if (!upstream)
                {
                    boot.ReleaseMaterialAudio();
                    string coverZip = Path.Combine(root, "cover.zip");
                    using (var archive = ZipFile.Open(coverZip, ZipArchiveMode.Create))
                    using (var writer = new StreamWriter(archive.CreateEntry("pack.json").Open()))
                        writer.Write(JsonConvert.SerializeObject(new { schemaVersion = 1, packId = "test.cover", version = "1",
                            groups = new[] { new { id = "cover", category = "background", layers = new[] { new { type = "jacket", fit = "contain" } } } } }));
                    var cover = await store.ImportAsync(coverZip);
                    store.SaveBackgroundSelection(new MaterialGroupSelection { Groups = { cover.GroupReference("cover") } }, store.List().Revision);
                    await Load();
                    var frame = ReadFrame(controller.BackgroundTexture);
                    Assert.That(frame.GetPixel(frame.width / 2, frame.height / 2).r, Is.GreaterThan(.9f));
                    if (frame.width > frame.height)
                        Assert.That(frame.GetPixel(0, frame.height / 2).r, Is.LessThan(.1f));
                    Object.Destroy(frame);
                    boot.ReleaseMaterialAudio();
                    boot.TemporaryJacketData = null;
                    await Load();
                    Assert.That(camera.transform.parent.gameObject.activeSelf, Is.True);
                }
                if (upstream && File.Exists(video))
                {
                    SetVideo();
                    boot.MusicData = null;
                    foreach (var choice in new[] { new MaterialGroupSelection(), selection })
                    {
                        boot.ReleaseMaterialAudio();
                        store.SaveBackgroundSelection(choice, store.List().Revision);
                        await Load();
                        var player = movie.GetComponent<VideoPlayer>();
                        Assert.That(player, Is.Not.Null);
                        Assert.That(camera.transform.parent.gameObject.activeSelf, Is.False);
                        view.MusicStart(0);
                        float timeout = Time.realtimeSinceStartup + 10;
                        while ((!player.isPrepared || player.width != player.targetTexture.width) && Time.realtimeSinceStartup < timeout)
                            await UniTask.NextFrame();
                        Assert.That(player.isPrepared, Is.True);
                        Assert.That(player.targetTexture.width, Is.EqualTo((int)player.width));
                        view.MusicPlaybackStart(0);
                        await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true);
                        string mode = choice.Groups.Count == 0 ? "builtin" : "pack";
                        File.WriteAllText(Path.Combine(folder, "mv-" + mode + ".txt"), $"frame={player.frame}, playing={player.isPlaying}, frames={player.frameCount}");
                        var movieCam = view.GetComponentsInChildren<Camera>(true).Single(c => c.name == "movieCamera");
                        Assert.That(movieCam.enabled, Is.True);
                        Assert.That(movieCam.targetTexture, Is.SameAs(controller.BackgroundTexture));
                        movieCam.Render();
                        var movieFrame = ReadFrame(controller.BackgroundTexture);
                        File.WriteAllBytes(Path.Combine(folder, "mv-" + mode + ".png"), movieFrame.EncodeToPNG());
                        var colors = movieFrame.GetPixels32();
                        Object.Destroy(movieFrame);
                        Assert.That(colors.Max(c => c.r) - colors.Min(c => c.r), Is.GreaterThan(64), "The movie surface must contain the decoded test pattern.");
                        view.OnUnload();
                    }
                }

                void SetVideo()
                {
                    File.Copy(video, Path.Combine(root, "video.mp4"));
                    File.WriteAllText(Path.Combine(root, "manifest.json"), JsonConvert.SerializeObject(new { id = "bg", videoFileName = "video.mp4" }));
                    boot.CustomMusicScorePath = root;
                }
            }
            finally
            {
                boot.ReleaseMaterialAudio();
                Object.Destroy(view.gameObject);
                if (controller.BackgroundTexture != null)
                {
                    camera.targetTexture = null;
                    controller.BackgroundTexture.Release();
                    Object.Destroy(controller.BackgroundTexture);
                }
                Object.Destroy(host);
                Directory.Delete(root, true);
            }

            async System.Threading.Tasks.Task Load()
            {
                var prep = new MaterialAudioPreparation(new MaterialPackStore(root));
                boot.MaterialAudio = prep.Audio;
                boot.BackgroundSkin = prep.Background;
                await prep.PrepareAsync(diagnostics, CancellationToken.None);
                view.Setup(controller);
                view.OnLoad();
            }
        });

        private static void CompareUpstream(string folder, Background2DView view, Camera camera)
        {
            // The reference was captured from the pinned upstream prefab with all six jacket surfaces.
            var reference = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            bool wasActive = view.gameObject.activeSelf;
            RenderTexture target = null;
            Texture2D actual = null;
            try
            {
                Assert.That(reference.LoadImage(File.ReadAllBytes(Path.Combine(folder, "reference.png"))), Is.True);
                target = RenderTexture.GetTemporary(reference.width, reference.height, 24, RenderTextureFormat.ARGB32);
                view.gameObject.SetActive(true);
                camera.targetTexture = target;
                camera.aspect = (float)reference.width / reference.height;
                camera.ResetProjectionMatrix();
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                actual = ReadFrame(target);
                File.WriteAllBytes(Path.Combine(folder, "pack-render.png"), actual.EncodeToPNG());
                var expected = reference.GetPixels32();
                var observed = actual.GetPixels32();
                int different = expected.Where((color, i) => Diff(color, observed[i]) > 3).Count();
                Assert.That(different, Is.LessThanOrEqualTo(expected.Length / 1000), "Pixels differing by more than 3/255");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                view.gameObject.SetActive(wasActive);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                Object.Destroy(actual);
                Object.Destroy(reference);
            }
        }

        private static int Diff(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        private static Texture2D ReadFrame(RenderTexture target)
        {
            var old = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally { RenderTexture.active = old; }
        }

        private static void CheckVector(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.0001f));

        private static byte[] MakePng(Color color, bool pattern = false)
        {
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Range(0, 32).Select(i => pattern ? new Color(i % 8 / 7f, i / 8 / 3f, i % 3 / 2f, 1) : color).ToArray());
            texture.Apply();
            byte[] bytes = texture.EncodeToPNG();
            Object.Destroy(texture);
            return bytes;
        }

        private static string MakeZip(string root)
        {
            var layers = new object[]
            {
                new { type = "image", file = "base.png", fit = "cover" },
                new { type = "jacket", size = new[] { 200, 200 }, fit = "contain" },
                new { type = "image", file = "full.png", size = new[] { 648, 432 }, scale = new[] { 1.5f, .5f }, position = new[] { 108, -54 } },
                new { type = "jacket", size = new[] { 108, 108 }, position = new[] { -430, 0 } }
            };
            var manifest = new { schemaVersion = 1, packId = "test.background", version = "1",
                groups = new[] { new { id = "background", category = "background", preferVideo = false, layers } } };
            string path = Path.Combine(root, "background.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var pair in new[] { ("base.png", Color.blue), ("full.png", Color.green) })
            {
                var bytes = MakePng(pair.Item2);
                using var stream = zip.CreateEntry(pair.Item1).Open();
                stream.Write(bytes, 0, bytes.Length);
            }
            using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
            writer.Write(JsonConvert.SerializeObject(manifest));
            return path;
        }
    }
}
