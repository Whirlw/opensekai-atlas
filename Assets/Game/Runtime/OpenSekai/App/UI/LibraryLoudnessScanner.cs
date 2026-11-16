using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Audio;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Measures the loudness of every cached song that has none yet, one at a time, and saves each
    /// result into the library cache. An import leaves only the new song unmeasured; a refresh or a
    /// new cache version leaves all of them. Runs whether or not 响度平均 is on, so turning it on
    /// takes effect at once.
    /// </summary>
    internal sealed class LibraryLoudnessScanner : IDisposable
    {
        /// <summary>Measurements kept in memory before the cache file is written again.</summary>
        private const int SAVE_EVERY = 8;

        private readonly SongPackageManager _packages;
        private readonly Action<string> _status;
        private CancellationTokenSource _run;

        internal LibraryLoudnessScanner(SongPackageManager packages, Action<string> status)
        {
            _packages = packages;
            _status = status;
        }

        internal bool IsRunning => _run != null;

        /// <summary>Starts measuring unless a pass is already running; it picks up songs added meanwhile.</summary>
        internal void Start()
        {
            if (_run != null || _packages == null)
            {
                return;
            }

            _run = new CancellationTokenSource();
            RunAsync(_run.Token).Forget();
        }

        /// <summary>Stops after the current song; what was measured so far is saved.</summary>
        internal void Stop()
        {
            if (_run == null)
            {
                return;
            }

            _run.Cancel();
            _run.Dispose();
            _run = null;
        }

        public void Dispose() => Stop();

        private async UniTaskVoid RunAsync(CancellationToken token)
        {
            int done = 0;
            int unsaved = 0;
            try
            {
                // Songs imported during the pass join at the end, so it loops until none are left.
                while (!token.IsCancellationRequested)
                {
                    IReadOnlyList<SongPackageItem> pending = _packages.ItemsWithoutLoudness();
                    if (pending.Count == 0)
                    {
                        break;
                    }

                    int total = done + pending.Count;
                    foreach (SongPackageItem item in pending)
                    {
                        token.ThrowIfCancellationRequested();
                        _status?.Invoke("正在分析响度 " + (done + 1) + "/" + total);
                        float? lufs = await MeasureAsync(item, token);
                        _packages.SetLoudness(item, lufs);
                        done++;
                        if (++unsaved >= SAVE_EVERY)
                        {
                            _packages.SaveLoudness();
                            unsaved = 0;
                        }
                    }
                }

                if (done > 0)
                {
                    _status?.Invoke("响度分析完成：" + done + " 首");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Page left or a live is starting; the rest is measured next time.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _packages.SaveLoudness();
                if (_run != null && _run.Token == token)
                {
                    _run.Dispose();
                    _run = null;
                }
            }
        }

        /// <summary>
        /// The song's loudness; null when its audio cannot be read, which is recorded too so a
        /// broken file is not retried until the next refresh.
        /// </summary>
        private static async UniTask<float?> MeasureAsync(SongPackageItem item, CancellationToken token)
        {
            AudioClip clip = null;
            try
            {
                // A fully decoded clip: streamed ones have no samples to read.
                clip = await PackageAudio.LoadClipAsync(item.Entry, token);
                return await LoudnessMeter.MeasureAsync(clip, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Library] Could not measure loudness of " + item.Entry.RootDirectory + ": " + exception.Message);
                return null;
            }
            finally
            {
                if (clip != null)
                {
                    UnityEngine.Object.Destroy(clip);
                }
            }
        }
    }
}
