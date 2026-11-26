using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.Shared.Audio
{
    /// <summary>
    /// Each level encodes more song time per sample at the original sample rate. Adjacent levels
    /// differ by 4x. Very long clips skip levels to fit the memory budget. A 16x take, for example, plays the second reduced level at 1x.
    /// All levels are ready before the first take. Changing speed during a song requires no decoding.
    /// </summary>
    internal sealed class PracticeAudioBank : IDisposable
    {
        internal const int REDUCTION = 4;
        private const int BLOCK_FRAMES = 8192;
        private const long MAX_PROXY_BYTES = 96L * 1024 * 1024;
        private const float MAX_PITCH = 3f;

        private readonly List<AudioClip> _clips = new List<AudioClip>();

        private readonly int _firstStride;

        private PracticeAudioBank(AudioClip original, int firstStride)
        {
            _firstStride = firstStride;
            _clips.Add(original);
        }

        public AudioClip Original => _clips[0];

        public AudioClip Select(float rate, out int stride)
        {
            PracticeStep.ValidateSpeed(rate);
            stride = 1;
            int index = 0;
            while (rate > MAX_PITCH * stride)
            {
                stride = stride == 1 ? _firstStride : stride * REDUCTION;
                index++;
            }
            return _clips[index];
        }

        public static async UniTask<PracticeAudioBank> PrepareAsync(AudioClip original, CancellationToken token, long budgetBytes = MAX_PROXY_BYTES)
        {
            token.ThrowIfCancellationRequested();
            if (original == null || original.samples <= 0 || original.channels <= 0 || original.frequency <= 0 ||
                original.loadType == AudioClipLoadType.Streaming)
                throw new InvalidDataException("练习模式需要可读取采样的歌曲音频。");

            int firstStride = FirstStrideForBudget(original.samples, original.channels, budgetBytes);
            var bank = new PracticeAudioBank(original, firstStride);
            try
            {
                var source = original;
                int channels = original.channels;
                float[] input = new float[BLOCK_FRAMES * channels];
                double yieldAt = Time.realtimeSinceStartupAsDouble + 0.004;
                for (int stride = 1; PracticeStep.MAX_SPEED > MAX_PITCH * stride;)
                {
                    int reduction = stride == 1 ? firstStride : REDUCTION;
                    stride *= reduction;
                    int frames = source.samples;
                    if (frames == 1)
                    {
                        bank._clips.Add(source);
                        continue;
                    }

                    var proxy = AudioClip.Create(original.name + "-practice-" + stride,
                        ReducedFrames(frames, reduction), channels, original.frequency, false);
                    if (proxy == null) throw new InvalidDataException("设备无法分配练习音频缓存。");
                    bank._clips.Add(proxy);
                    // Every supported stride divides BLOCK_FRAMES, so adjacent writes never
                    // share a partial sample group. Only the very last source block has a tail.
                    var output = new float[BLOCK_FRAMES / reduction * channels];
                    for (int offset = 0; offset < frames;)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = Math.Min(BLOCK_FRAMES, frames - offset);
                        // Unity's array overloads wrap at the clip end. Use the exact tail size
                        // so a partial final block never overwrites the first samples of a clip.
                        var read = count == BLOCK_FRAMES ? input : new float[count * channels];
                        var write = count == BLOCK_FRAMES ? output : new float[ReducedFrames(count, reduction) * channels];
                        if (!source.GetData(read, offset))
                            throw new InvalidDataException("无法读取歌曲的 PCM 采样，无法准备练习变速。");
                        Reduce(read, count, channels, write, reduction);
                        if (!proxy.SetData(write, offset / reduction))
                            throw new InvalidDataException("无法写入练习音频缓存。");
                        offset += count;

                        // Give the loading UI a frame after allocating the first large clip.
                        // Subsequent chunks share a small time budget and stay cancellable.
                        if ((stride == firstStride && offset == BLOCK_FRAMES && offset < frames) ||
                            Time.realtimeSinceStartupAsDouble >= yieldAt)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, token);
                            yieldAt = Time.realtimeSinceStartupAsDouble + 0.004;
                        }
                    }
                    source = proxy;
                }
                return bank;
            }
            catch (Exception error)
            {
                bank.Dispose();
                if (error is OutOfMemoryException)
                    throw new InvalidDataException("设备可用内存不足，无法准备练习音频。", error);
                throw;
            }
        }

        private static int ReducedFrames(int frames, int stride) => 1 + (frames - 1) / stride;

        internal static int FirstStrideForBudget(int frames, int channels, long budgetBytes)
        {
            // Higher rates can still use a coarser clip with a lower native pitch. Keep the
            // original for all rates up to 3x, and choose the finest remaining levels that fit.
            for (int first = REDUCTION; ; first *= REDUCTION)
            {
                if (EstimateBytes(frames, channels, first) <= budgetBytes) return first;
                if (first * MAX_PITCH >= PracticeStep.MAX_SPEED)
                    throw new InvalidDataException("设备无法分配这首歌曲所需的最小练习音频缓存。");
            }
        }

        internal static long EstimateBytes(int frames, int channels, int firstStride = REDUCTION)
        {
            long total = 0;
            for (int stride = 1; PracticeStep.MAX_SPEED > MAX_PITCH * stride && frames > 1;)
            {
                int reduction = stride == 1 ? firstStride : REDUCTION;
                stride *= reduction;
                frames = ReducedFrames(frames, reduction);
                total += (long)frames * channels * sizeof(float);
            }
            return total;
        }

        // Average each channel independently before decimation to attenuate aliases. Keep a
        // partial trailing frame group rather than dropping the end of a selected song.
        internal static void Reduce(float[] input, int frames, int channels, float[] output, int reduction = REDUCTION)
        {
            for (int frame = 0; frame < frames; frame += reduction)
            {
                int count = Math.Min(reduction, frames - frame);
                int index = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sum = 0;
                    for (int i = 0; i < count; i++) sum += input[index + i * channels + channel];
                    output[frame / reduction * channels + channel] = sum / count;
                }
            }
        }

        public void Dispose()
        {
            for (int i = _clips.Count - 1; i > 0; --i)
            {
                if (_clips[i] == _clips[i - 1] || _clips[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_clips[i]);
                else UnityEngine.Object.DestroyImmediate(_clips[i]);
            }
            _clips.Clear();
        }
    }
}
