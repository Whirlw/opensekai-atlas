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
    /// Each level encodes four times as much song time per sample as the previous level, at the
    /// original sample rate. A 16x take, for example, plays the second reduced level at 1x.
    /// All levels are ready before the first take. Changing speed during a song requires no decoding.
    /// </summary>
    internal sealed class PracticeAudioBank : IDisposable
    {
        internal const int REDUCTION = 4;
        private const int BLOCK_FRAMES = 8192;
        private const long MAX_PROXY_BYTES = 96L * 1024 * 1024;
        private const float MAX_PITCH = 3f;

        private readonly List<AudioClip> _clips = new List<AudioClip>();

        private PracticeAudioBank(AudioClip original)
        {
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
                stride *= REDUCTION;
                index++;
            }
            return _clips[index];
        }

        public static async UniTask<PracticeAudioBank> PrepareAsync(AudioClip original, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (original == null || original.samples <= 0 || original.channels <= 0 || original.frequency <= 0 ||
                original.loadType == AudioClipLoadType.Streaming)
                throw new InvalidDataException("练习模式需要可读取采样的歌曲音频。");

            long extraBytes = EstimateBytes(original.samples, original.channels);
            if (extraBytes > MAX_PROXY_BYTES)
                throw new InvalidDataException("这首歌曲的练习音频缓存超过 96 MiB，请缩短音频后重试。");

            var bank = new PracticeAudioBank(original);
            try
            {
                var source = original;
                int channels = original.channels;
                float[] input = new float[BLOCK_FRAMES * channels];
                float[] output = new float[BLOCK_FRAMES / REDUCTION * channels];
                double yieldAt = Time.realtimeSinceStartupAsDouble + 0.004;
                for (int stride = 1; PracticeStep.MAX_SPEED > MAX_PITCH * stride; stride *= REDUCTION)
                {
                    int frames = source.samples;
                    if (frames == 1)
                    {
                        bank._clips.Add(source);
                        continue;
                    }

                    var proxy = AudioClip.Create(original.name + "-practice-" + (stride * REDUCTION),
                        ReducedFrames(frames), channels, original.frequency, false);
                    if (proxy == null) throw new InvalidDataException("设备无法分配练习音频缓存。");
                    bank._clips.Add(proxy);
                    for (int offset = 0; offset < frames;)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = Math.Min(BLOCK_FRAMES, frames - offset);
                        // Unity's array overloads wrap at the clip end. Use the exact tail size
                        // so a partial final block never overwrites the first samples of a clip.
                        var read = count == BLOCK_FRAMES ? input : new float[count * channels];
                        var write = count == BLOCK_FRAMES ? output : new float[ReducedFrames(count) * channels];
                        if (!source.GetData(read, offset))
                            throw new InvalidDataException("无法读取歌曲的 PCM 采样，无法准备练习变速。");
                        Reduce(read, count, channels, write);
                        if (!proxy.SetData(write, offset / REDUCTION))
                            throw new InvalidDataException("无法写入练习音频缓存。");
                        offset += count;

                        if (Time.realtimeSinceStartupAsDouble >= yieldAt)
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

        private static int ReducedFrames(int frames) => 1 + (frames - 1) / REDUCTION;

        internal static long EstimateBytes(int frames, int channels)
        {
            long total = 0;
            for (int stride = 1; PracticeStep.MAX_SPEED > MAX_PITCH * stride && frames > 1; stride *= REDUCTION)
            {
                frames = ReducedFrames(frames);
                total += (long)frames * channels * sizeof(float);
            }
            return total;
        }

        // Average each channel independently before decimation to attenuate aliases. Keep a
        // partial trailing frame group rather than dropping the end of a selected song.
        internal static void Reduce(float[] input, int frames, int channels, float[] output)
        {
            for (int frame = 0; frame < frames; frame += REDUCTION)
            {
                int count = Math.Min(REDUCTION, frames - frame);
                int index = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sum = 0;
                    for (int i = 0; i < count; i++) sum += input[index + i * channels + channel];
                    output[frame / REDUCTION * channels + channel] = sum / count;
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
