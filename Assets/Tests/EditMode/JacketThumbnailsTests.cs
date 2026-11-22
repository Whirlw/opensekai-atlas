using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.App.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.EditMode
{
    public sealed class JacketThumbnailsTests
    {
        private const int TIMEOUT_MS = 20000;

        private string _root;
        private string _cacheDirectory;
        private string _jacketPath;
        private JacketThumbnails _thumbnails;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "OpenSekai-JacketThumbnails-" + Guid.NewGuid().ToString("N"));
            _cacheDirectory = Path.Combine(_root, "cache");
            _jacketPath = Path.Combine(_root, "jacket.png");
            Directory.CreateDirectory(_root);
            WriteJacket(_jacketPath, 512, 384);
            _thumbnails = new JacketThumbnails(_cacheDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            _thumbnails.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void ThumbnailFileNameFollowsTheSourceFile()
        {
            const int SIZE = JacketThumbnails.LIST_SIZE;
            string name = JacketThumbnails.ThumbnailFileName("a/jacket.png", 100, 5, SIZE);

            Assert.That(name, Is.EqualTo(JacketThumbnails.ThumbnailFileName("a/jacket.png", 100, 5, SIZE)));
            Assert.That(name, Is.Not.EqualTo(JacketThumbnails.ThumbnailFileName("a/jacket.png", 100, 6, SIZE)), "rewritten");
            Assert.That(name, Is.Not.EqualTo(JacketThumbnails.ThumbnailFileName("a/jacket.png", 101, 5, SIZE)), "resized");
            Assert.That(name, Is.Not.EqualTo(JacketThumbnails.ThumbnailFileName("b/jacket.png", 100, 5, SIZE)), "other package");
            Assert.That(name, Is.Not.EqualTo(JacketThumbnails.ThumbnailFileName("a/jacket.png", 100, 5, JacketThumbnails.DETAIL_SIZE)));
        }

        [UnityTest]
        [Timeout(TIMEOUT_MS)]
        public IEnumerator FirstLoadShrinksTheJacketAndStoresItOnDisk() => UniTask.ToCoroutine(async () =>
        {
            JacketThumbnail thumbnail = await _thumbnails.LoadAsync(_jacketPath, 1, CancellationToken.None);
            Sprite sprite = thumbnail.Sprite;

            Assert.That(sprite, Is.Not.Null);
            Assert.That((sprite.texture.width, sprite.texture.height), Is.EqualTo((256, 192)));
            Assert.That(_thumbnails.TryGetLoaded(_jacketPath, 1, out JacketThumbnail loaded), Is.True);
            Assert.That(loaded.Sprite, Is.SameAs(sprite));

            await UniTask.WaitUntil(() => StoredThumbnailCount() == 1);
        });

        [UnityTest]
        [Timeout(TIMEOUT_MS)]
        public IEnumerator LaterSessionsDecodeTheStoredThumbnail() => UniTask.ToCoroutine(async () =>
        {
            await _thumbnails.LoadAsync(_jacketPath, 1, CancellationToken.None);
            await UniTask.WaitUntil(() => StoredThumbnailCount() == 1);
            string stored = Directory.GetFiles(_cacheDirectory, "*" + JacketThumbnails.FILE_EXTENSION)[0];
            DateTime written = File.GetLastWriteTimeUtc(stored);

            using (var nextSession = new JacketThumbnails(_cacheDirectory))
            {
                Sprite sprite = (await nextSession.LoadAsync(_jacketPath, 1, CancellationToken.None)).Sprite;

                Assert.That(sprite, Is.Not.Null);
                Assert.That((sprite.texture.width, sprite.texture.height), Is.EqualTo((256, 192)));
            }

            Assert.That(StoredThumbnailCount(), Is.EqualTo(1));
            Assert.That(File.GetLastWriteTimeUtc(stored), Is.EqualTo(written), "the thumbnail was reused, not rebuilt");
        });

        [UnityTest]
        [Timeout(TIMEOUT_MS)]
        public IEnumerator ConcurrentRequestsShareOneLoad() => UniTask.ToCoroutine(async () =>
        {
            (JacketThumbnail first, JacketThumbnail second) = await UniTask.WhenAll(
                _thumbnails.LoadAsync(_jacketPath, 1, CancellationToken.None),
                _thumbnails.LoadAsync(_jacketPath, 1, CancellationToken.None));

            Assert.That(first.Sprite, Is.Not.Null);
            Assert.That(second.Sprite, Is.SameAs(first.Sprite));
            Assert.That(_thumbnails.LoadedCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Timeout(TIMEOUT_MS)]
        public IEnumerator MissingJacketLoadsAsNull() => UniTask.ToCoroutine(async () =>
        {
            string missing = Path.Combine(_root, "missing.png");

            Sprite sprite = (await _thumbnails.LoadAsync(missing, 1, CancellationToken.None)).Sprite;

            Assert.That(sprite, Is.Null);
            Assert.That(_thumbnails.TryGetLoaded(missing, 1, out _), Is.True, "the miss is remembered");
        });

        [UnityTest]
        [Timeout(TIMEOUT_MS)]
        public IEnumerator ACancelledCallerDoesNotLoseTheLoad() => UniTask.ToCoroutine(async () =>
        {
            using (var caller = new CancellationTokenSource())
            {
                UniTask<JacketThumbnail> abandoned = _thumbnails.LoadAsync(_jacketPath, 1, caller.Token);
                caller.Cancel();
                bool cancelled = false;
                try
                {
                    await abandoned;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.That(cancelled, Is.True);
            }

            Sprite sprite = (await _thumbnails.LoadAsync(_jacketPath, 1, CancellationToken.None)).Sprite;
            Assert.That(sprite, Is.Not.Null);
        });

        private int StoredThumbnailCount()
        {
            return Directory.Exists(_cacheDirectory)
                ? Directory.GetFiles(_cacheDirectory, "*" + JacketThumbnails.FILE_EXTENSION).Length
                : 0;
        }

        private static void WriteJacket(string path, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(31, 58, 138, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
