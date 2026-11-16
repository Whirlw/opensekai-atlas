using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Jacket thumbnails for the library page. The first request for a jacket shrinks it, takes its
    /// palette and stores both on disk; later requests decode only that small file. File reads, the
    /// downscale and the palette run on worker threads, decoded sprites sit in a bounded cache, and
    /// concurrent requests for the same jacket share one load.
    /// </summary>
    /// <remarks>
    /// A sprite is destroyed when the cache evicts it, so the capacity must stay above the number
    /// of images that can be on screen at once.
    /// </remarks>
    internal sealed class JacketThumbnails : IDisposable
    {
        internal const int LIST_SIZE = 256;
        internal const int DETAIL_SIZE = 512;
        internal const string FILE_EXTENSION = ".thumb";

        private const int DEFAULT_CAPACITY = 96;
        private const int HEADER_LENGTH = 12;

        // "OSJT": a stored thumbnail is this tag, the accent and glow colors, then a PNG.
        private static readonly byte[] Magic = { 0x4F, 0x53, 0x4A, 0x54 };

        private readonly string _cacheDirectory;
        private readonly int _size;
        private readonly LruCache<string, JacketThumbnail> _loaded;
        private readonly Dictionary<string, UniTaskCompletionSource<JacketThumbnail>> _pending =
            new Dictionary<string, UniTaskCompletionSource<JacketThumbnail>>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _disposed;

        /// <param name="cacheDirectory">Folder for stored thumbnails; created on first write.</param>
        /// <param name="size">Longest side of a thumbnail, in pixels.</param>
        /// <param name="capacity">How many decoded thumbnails stay in memory.</param>
        internal JacketThumbnails(string cacheDirectory, int size = LIST_SIZE, int capacity = DEFAULT_CAPACITY)
        {
            _cacheDirectory = cacheDirectory;
            _size = Mathf.Max(1, size);
            _loaded = new LruCache<string, JacketThumbnail>(capacity, (_, thumbnail) => DestroySprite(thumbnail.Sprite));
        }

        internal int LoadedCount => _loaded.Count;

        /// <summary>Synchronous hit test for a cell that wants to skip the placeholder frame.</summary>
        /// <param name="jacketPath">Full path of the package's jacket image.</param>
        /// <param name="version">Changes when the package changes, e.g. its last write ticks.</param>
        /// <param name="thumbnail">The cached result; its sprite is null when the jacket is unusable.</param>
        internal bool TryGetLoaded(string jacketPath, long version, out JacketThumbnail thumbnail)
        {
            thumbnail = JacketThumbnail.None;
            return !_disposed
                && !string.IsNullOrEmpty(jacketPath)
                && _loaded.TryGet(MemoryKey(jacketPath, version), out thumbnail);
        }

        /// <summary>
        /// Loads a jacket thumbnail; the sprite is null when the file is missing or not an image.
        /// Cancelling <paramref name="cancellationToken"/> only stops this caller from waiting: the
        /// load itself finishes and is cached for the next request.
        /// </summary>
        internal async UniTask<JacketThumbnail> LoadAsync(string jacketPath, long version, CancellationToken cancellationToken)
        {
            if (_disposed || string.IsNullOrEmpty(jacketPath))
            {
                return JacketThumbnail.None;
            }

            string key = MemoryKey(jacketPath, version);
            if (_loaded.TryGet(key, out JacketThumbnail cached))
            {
                return cached;
            }

            if (!_pending.TryGetValue(key, out UniTaskCompletionSource<JacketThumbnail> pending))
            {
                pending = new UniTaskCompletionSource<JacketThumbnail>();
                _pending[key] = pending;
                RunLoadAsync(key, jacketPath, pending).Forget();
            }

            return await pending.Task.AttachExternalCancellation(cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            foreach (UniTaskCompletionSource<JacketThumbnail> pending in _pending.Values)
            {
                pending.TrySetCanceled();
            }

            _pending.Clear();
            _loaded.Clear();
        }

        /// <summary>
        /// File name of a jacket's stored thumbnail. It covers the source's size and write time, so
        /// a replaced jacket gets a new thumbnail instead of showing the old one.
        /// </summary>
        internal static string ThumbnailFileName(string jacketPath, long length, long lastWriteTicks, int size)
        {
            unchecked
            {
                // FNV-1a, 64 bit: stable across runs and platforms, unlike string.GetHashCode.
                ulong hash = 14695981039346656037UL;
                string source = jacketPath + "|" + length + "|" + lastWriteTicks + "|" + size;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16") + FILE_EXTENSION;
            }
        }

        private static string MemoryKey(string jacketPath, long version)
        {
            return jacketPath + "|" + version;
        }

        private async UniTaskVoid RunLoadAsync(
            string key, string jacketPath, UniTaskCompletionSource<JacketThumbnail> pending)
        {
            JacketThumbnail thumbnail = JacketThumbnail.None;
            try
            {
                thumbnail = await LoadCoreAsync(jacketPath, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                _pending.Remove(key);
                pending.TrySetCanceled();
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[JacketThumbnails] Could not load " + jacketPath + ": " + exception.Message);
            }

            if (_disposed)
            {
                DestroySprite(thumbnail.Sprite);
                pending.TrySetCanceled();
                return;
            }

            // A failed load is cached too, so a broken jacket is not retried on every bind.
            _loaded.Set(key, thumbnail);
            _pending.Remove(key);
            pending.TrySetResult(thumbnail);
        }

        private async UniTask<JacketThumbnail> LoadCoreAsync(string jacketPath, CancellationToken cancellationToken)
        {
            string cacheDirectory = _cacheDirectory;
            int size = _size;
            SourceFiles files = await UniTask.RunOnThreadPool(
                () => ReadFiles(jacketPath, cacheDirectory, size), cancellationToken: cancellationToken);

            if (files.StoredImage != null)
            {
                Texture2D stored = Decode(files.StoredImage, markNonReadable: true);
                if (stored != null)
                {
                    return new JacketThumbnail(CreateSprite(stored, jacketPath), files.StoredPalette);
                }

                // An unreadable thumbnail (interrupted write, disk error) is rebuilt from the jacket.
                files = await UniTask.RunOnThreadPool(
                    () => DiscardStored(files, jacketPath), cancellationToken: cancellationToken);
            }

            if (files.JacketBytes == null)
            {
                return JacketThumbnail.None;
            }

            Texture2D full = Decode(files.JacketBytes, markNonReadable: false);
            if (full == null)
            {
                return JacketThumbnail.None;
            }

            var source = new PixelImage(full.GetPixels32(), full.width, full.height);
            DestroyObject(full);

            (PixelImage small, JacketPalette palette) = await UniTask.RunOnThreadPool(
                () =>
                {
                    PixelImage shrunk = ThumbnailResampler.Downscale(source, size);
                    return (shrunk, JacketPalette.From(shrunk));
                },
                cancellationToken: cancellationToken);

            var texture = new Texture2D(small.Width, small.Height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(small.Pixels);
            texture.Apply(false, false);
            byte[] encoded = texture.EncodeToPNG();
            texture.Apply(false, true);

            StoreAsync(files.StoredPath, palette, encoded).Forget();
            return new JacketThumbnail(CreateSprite(texture, jacketPath), palette);
        }

        private static SourceFiles ReadFiles(string jacketPath, string cacheDirectory, int size)
        {
            var jacket = new FileInfo(jacketPath);
            if (!jacket.Exists)
            {
                return default;
            }

            string storedPath = Path.Combine(
                cacheDirectory, ThumbnailFileName(jacketPath, jacket.Length, jacket.LastWriteTimeUtc.Ticks, size));
            if (File.Exists(storedPath) && TryParseStored(File.ReadAllBytes(storedPath), out JacketPalette palette, out byte[] image))
            {
                return new SourceFiles(storedPath, image, palette, null);
            }

            return new SourceFiles(storedPath, null, default, File.ReadAllBytes(jacketPath));
        }

        private static SourceFiles DiscardStored(SourceFiles files, string jacketPath)
        {
            File.Delete(files.StoredPath);
            return new SourceFiles(
                files.StoredPath, null, default, File.Exists(jacketPath) ? File.ReadAllBytes(jacketPath) : null);
        }

        private static bool TryParseStored(byte[] bytes, out JacketPalette palette, out byte[] image)
        {
            palette = default;
            image = null;
            if (bytes.Length <= HEADER_LENGTH)
            {
                return false;
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (bytes[i] != Magic[i])
                {
                    return false;
                }
            }

            palette = new JacketPalette(
                new Color32(bytes[4], bytes[5], bytes[6], bytes[7]),
                new Color32(bytes[8], bytes[9], bytes[10], bytes[11]));
            image = new byte[bytes.Length - HEADER_LENGTH];
            Buffer.BlockCopy(bytes, HEADER_LENGTH, image, 0, image.Length);
            return true;
        }

        private static byte[] BuildStored(JacketPalette palette, byte[] image)
        {
            var bytes = new byte[HEADER_LENGTH + image.Length];
            Buffer.BlockCopy(Magic, 0, bytes, 0, Magic.Length);
            Color32 accent = palette.Accent;
            Color32 glow = palette.Glow;
            bytes[4] = accent.r;
            bytes[5] = accent.g;
            bytes[6] = accent.b;
            bytes[7] = accent.a;
            bytes[8] = glow.r;
            bytes[9] = glow.g;
            bytes[10] = glow.b;
            bytes[11] = glow.a;
            Buffer.BlockCopy(image, 0, bytes, HEADER_LENGTH, image.Length);
            return bytes;
        }

        private static async UniTaskVoid StoreAsync(string path, JacketPalette palette, byte[] image)
        {
            try
            {
                await UniTask.RunOnThreadPool(() => WriteAtomically(path, BuildStored(palette, image)));
            }
            catch (Exception exception)
            {
                // The stored thumbnail is only a cache: the sprite is already in memory, and a
                // failed write just means the jacket is shrunk again next session.
                Debug.LogWarning("[JacketThumbnails] Could not store " + path + ": " + exception.Message);
            }
        }

        private static void WriteAtomically(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path))
            {
                File.Delete(temporary);
                return;
            }

            File.Move(temporary, path);
        }

        private static Texture2D Decode(byte[] bytes, bool markNonReadable)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes, markNonReadable))
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }

            DestroyObject(texture);
            return null;
        }

        private static Sprite CreateSprite(Texture2D texture, string jacketPath)
        {
            // FullRect: the default Tight mesh traces the image outline on the main thread
            // (SpriteMeshGenerator.TraceShape), which a rectangular UI image never uses.
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = Path.GetFileNameWithoutExtension(jacketPath);
            return sprite;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            DestroyObject(sprite);
            DestroyObject(texture);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private readonly struct SourceFiles
        {
            internal SourceFiles(string storedPath, byte[] storedImage, JacketPalette storedPalette, byte[] jacketBytes)
            {
                StoredPath = storedPath;
                StoredImage = storedImage;
                StoredPalette = storedPalette;
                JacketBytes = jacketBytes;
            }

            internal string StoredPath { get; }
            internal byte[] StoredImage { get; }
            internal JacketPalette StoredPalette { get; }
            internal byte[] JacketBytes { get; }
        }
    }
}
