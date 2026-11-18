using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Sonolus;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Covers of levels on a server. The first request downloads the image into a disk cache
    /// (named by its SHA-1 when the server gives one, else by its URL); decoding, shrinking and the
    /// palette are then the library's own <see cref="JacketThumbnails"/>. At most a few downloads
    /// run at once, and requests for the same cover share one download.
    /// </summary>
    internal sealed class RemoteCovers : IDisposable
    {
        private const int MAX_PARALLEL = 4;
        private const long MAX_BYTES = 8L * 1024 * 1024;
        private const string DIRECTORY = "SonolusCovers";

        private readonly string _directory;
        private readonly JacketThumbnails _small;
        private readonly JacketThumbnails _large;
        private readonly Dictionary<string, UniTaskCompletionSource<string>> _pending = new Dictionary<string, UniTaskCompletionSource<string>>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private int _running;
        private bool _disposed;

        internal RemoteCovers()
        {
            _directory = Path.Combine(Application.temporaryCachePath, DIRECTORY);
            string thumbnails = Path.Combine(Application.temporaryCachePath, "SonolusThumbnails");
            _small = new JacketThumbnails(thumbnails, JacketThumbnails.LIST_SIZE, 128);
            _large = new JacketThumbnails(thumbnails, JacketThumbnails.DETAIL_SIZE, 6);
        }

        /// <summary>A cover already decoded at list size, for a cell that wants to skip the placeholder.</summary>
        internal bool TryGetLoaded(SonolusServerAddress server, SonolusSrl cover, out JacketThumbnail thumbnail)
        {
            thumbnail = JacketThumbnail.None;
            string path = PathOf(server, cover);
            return path != null && File.Exists(path) && _small.TryGetLoaded(path, 0, out thumbnail);
        }

        /// <param name="large">The detail page's size rather than a list cell's.</param>
        internal async UniTask<JacketThumbnail> LoadAsync(
            SonolusServerAddress server, SonolusSrl cover, bool large, CancellationToken cancellationToken)
        {
            string path = await FetchAsync(server, cover, cancellationToken);
            if (path == null || _disposed)
            {
                return JacketThumbnail.None;
            }

            return await (large ? _large : _small).LoadAsync(path, 0, cancellationToken);
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
            foreach (UniTaskCompletionSource<string> pending in _pending.Values)
            {
                pending.TrySetResult(null);
            }

            _pending.Clear();
            _small.Dispose();
            _large.Dispose();
        }

        private string PathOf(SonolusServerAddress server, SonolusSrl cover)
        {
            if (server == null || cover == null || cover.IsEmpty)
            {
                return null;
            }

            string name = !string.IsNullOrEmpty(cover.Hash) && IsHex(cover.Hash)
                ? cover.Hash.ToLowerInvariant()
                : Fnv(server.Resolve(cover.Url) ?? cover.Url);
            return Path.Combine(_directory, name + ".img");
        }

        private async UniTask<string> FetchAsync(SonolusServerAddress server, SonolusSrl cover, CancellationToken cancellationToken)
        {
            string path = PathOf(server, cover);
            if (path == null || _disposed)
            {
                return null;
            }

            if (File.Exists(path))
            {
                return path;
            }

            if (!_pending.TryGetValue(path, out UniTaskCompletionSource<string> pending))
            {
                pending = new UniTaskCompletionSource<string>();
                _pending[path] = pending;
                DownloadAsync(server, cover, path, pending).Forget();
            }

            return await pending.Task.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid DownloadAsync(SonolusServerAddress server, SonolusSrl cover, string path, UniTaskCompletionSource<string> pending)
        {
            string result = null;
            try
            {
                while (_running >= MAX_PARALLEL && !_disposed)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _lifetime.Token);
                }

                _running++;
                try
                {
                    byte[] bytes = await new SonolusClient(server).DownloadAsync(cover, MAX_BYTES, null, _lifetime.Token);
                    if (SonolusLevelInstaller.ImageExtension(bytes) != null)
                    {
                        await UniTask.RunOnThreadPool(() =>
                        {
                            Directory.CreateDirectory(_directory);
                            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            File.WriteAllBytes(temporary, bytes);
                            if (!File.Exists(path))
                            {
                                File.Move(temporary, path);
                            }
                            else
                            {
                                File.Delete(temporary);
                            }
                        });
                        result = path;
                    }
                }
                finally
                {
                    _running--;
                }
            }
            catch (OperationCanceledException)
            {
                // The page closed.
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Cover download failed: " + exception.Message);
            }
            finally
            {
                _pending.Remove(path);
                pending.TrySetResult(result);
            }
        }

        private static bool IsHex(string text)
        {
            if (text.Length < 16 || text.Length > 128)
            {
                return false;
            }

            foreach (char c in text)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        private static string Fnv(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (byte b in Encoding.UTF8.GetBytes(text ?? string.Empty))
                {
                    hash ^= b;
                    hash *= 1099511628211UL;
                }

                return "u" + hash.ToString("x16");
            }
        }
    }
}
