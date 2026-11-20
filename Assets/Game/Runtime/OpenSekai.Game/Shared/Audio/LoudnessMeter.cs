using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai;
using UnityEngine;

namespace OpenSekai.Shared.Audio
{
    /// <summary>
    /// Integrated loudness of a whole track in LUFS, as ITU-R BS.1770-4 defines it: K-weighted,
    /// 400 ms blocks stepped by 100 ms, an absolute gate at -70 LUFS and a relative gate 10 LU
    /// below the absolute-gated level. Feed it interleaved samples in any chunk size.
    /// </summary>
    public sealed class LoudnessMeter
    {
        private const double ABSOLUTE_GATE = -70.0;
        private const double RELATIVE_GATE = -10.0;
        private const int SEGMENTS_PER_BLOCK = 4; // 400 ms blocks of 100 ms segments

        private readonly int _channels;
        private readonly int _segmentFrames;
        private readonly Biquad[] _shelf;
        private readonly Biquad[] _highPass;
        private readonly List<double> _segments = new List<double>();
        private double _segmentSum;
        private int _segmentFill;

        public LoudnessMeter(int sampleRate, int channels)
        {
            if (sampleRate <= 0 || channels <= 0)
            {
                throw new ArgumentOutOfRangeException(sampleRate <= 0 ? nameof(sampleRate) : nameof(channels));
            }

            _channels = channels;
            _segmentFrames = Math.Max(1, sampleRate / 10);
            _shelf = new Biquad[channels];
            _highPass = new Biquad[channels];
            for (int c = 0; c < channels; c++)
            {
                _shelf[c] = Biquad.Shelf(sampleRate);
                _highPass[c] = Biquad.HighPass(sampleRate);
            }
        }

        /// <summary>Adds <paramref name="frames"/> frames of interleaved samples.</summary>
        public void Feed(float[] interleaved, int frames)
        {
            int index = 0;
            for (int f = 0; f < frames; f++)
            {
                double energy = 0.0;
                for (int c = 0; c < _channels; c++)
                {
                    double y = _highPass[c].Process(_shelf[c].Process(interleaved[index++]));
                    energy += y * y;
                }

                _segmentSum += energy;
                if (++_segmentFill == _segmentFrames)
                {
                    _segments.Add(_segmentSum / _segmentFrames);
                    _segmentSum = 0.0;
                    _segmentFill = 0;
                }
            }
        }

        /// <summary>The track's integrated loudness; null when it is silent or shorter than one block.</summary>
        public float? Result()
        {
            int blockCount = _segments.Count - SEGMENTS_PER_BLOCK + 1;
            if (blockCount <= 0)
            {
                return null;
            }

            var blocks = new double[blockCount];
            double window = 0.0;
            for (int i = 0; i < _segments.Count; i++)
            {
                window += _segments[i];
                if (i >= SEGMENTS_PER_BLOCK)
                {
                    window -= _segments[i - SEGMENTS_PER_BLOCK];
                }

                if (i >= SEGMENTS_PER_BLOCK - 1)
                {
                    blocks[i - SEGMENTS_PER_BLOCK + 1] = Math.Max(0.0, window / SEGMENTS_PER_BLOCK);
                }
            }

            double absoluteMean = GatedMean(blocks, EnergyOf(ABSOLUTE_GATE));
            if (absoluteMean <= 0.0)
            {
                return null;
            }

            double relativeGate = EnergyOf(LoudnessOf(absoluteMean) + RELATIVE_GATE);
            double mean = GatedMean(blocks, Math.Max(relativeGate, EnergyOf(ABSOLUTE_GATE)));
            return mean > 0.0 ? (float)LoudnessOf(mean) : (float?)null;
        }

        /// <summary>
        /// Measures a fully loaded clip (not a streamed one: those have no readable samples). Reads it in
        /// chunks with a few milliseconds of work per frame, so a long track never stalls the page.
        /// </summary>
        public static async UniTask<float?> MeasureAsync(AudioClip clip, CancellationToken token, float frameBudgetMs = 3f)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
            {
                return null;
            }

            const int CHUNK_FRAMES = 4096;
            var meter = new LoudnessMeter(clip.frequency, clip.channels);
            var buffer = new float[CHUNK_FRAMES * clip.channels];
            var watch = Stopwatch.StartNew();
            for (int offset = 0; offset < clip.samples; offset += CHUNK_FRAMES)
            {
                int frames = Math.Min(CHUNK_FRAMES, clip.samples - offset);
                if (frames < CHUNK_FRAMES)
                {
                    // GetData wraps round to the start when the buffer runs past the end.
                    buffer = new float[frames * clip.channels];
                }

                if (!clip.GetData(buffer, offset))
                {
                    return null;
                }

                meter.Feed(buffer, frames);
                if (watch.Elapsed.TotalMilliseconds >= frameBudgetMs)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    watch.Restart();
                }
            }

            return meter.Result();
        }

        private static double GatedMean(double[] blocks, double gate)
        {
            double sum = 0.0;
            int count = 0;
            foreach (double block in blocks)
            {
                if (block > gate)
                {
                    sum += block;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0.0;
        }

        private static double LoudnessOf(double energy) => -0.691 + 10.0 * Math.Log10(energy);

        private static double EnergyOf(double loudness) => Math.Pow(10.0, (loudness + 0.691) / 10.0);

        /// <summary>One K-weighting stage; the coefficients are derived for any sample rate.</summary>
        private struct Biquad
        {
            private double _b0, _b1, _b2, _a1, _a2;
            private double _z1, _z2;

            internal static Biquad Shelf(int sampleRate)
            {
                const double F_0 = 1681.974450955533;
                const double GAIN = 3.999843853973347;
                const double Q = 0.7071752369554196;
                double k = Math.Tan(Math.PI * F_0 / sampleRate);
                double vh = Math.Pow(10.0, GAIN / 20.0);
                double vb = Math.Pow(vh, 0.4996667741545416);
                double a0 = 1.0 + k / Q + k * k;
                return new Biquad
                {
                    _b0 = (vh + vb * k / Q + k * k) / a0,
                    _b1 = 2.0 * (k * k - vh) / a0,
                    _b2 = (vh - vb * k / Q + k * k) / a0,
                    _a1 = 2.0 * (k * k - 1.0) / a0,
                    _a2 = (1.0 - k / Q + k * k) / a0
                };
            }

            internal static Biquad HighPass(int sampleRate)
            {
                const double F_0 = 38.13547087602444;
                const double Q = 0.5003270373238773;
                double k = Math.Tan(Math.PI * F_0 / sampleRate);
                double a0 = 1.0 + k / Q + k * k;
                return new Biquad
                {
                    _b0 = 1.0,
                    _b1 = -2.0,
                    _b2 = 1.0,
                    _a1 = 2.0 * (k * k - 1.0) / a0,
                    _a2 = (1.0 - k / Q + k * k) / a0
                };
            }

            internal double Process(double x)
            {
                // Transposed direct form II.
                double y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return y;
            }
        }
    }

    /// <summary>
    /// The 响度平均 setting: every song's music is scaled toward <see cref="TARGET_LUFS"/> from its
    /// measured loudness. Audio sources cannot go above full volume, so a song quieter than the
    /// target keeps its own level and only louder ones are turned down.
    /// </summary>
    public static class LoudnessNormalization
    {
        public const float TARGET_LUFS = -12f;

        public static bool Enabled => ApplicationLocalSettings.LoadFromStorage().LoudnessNormalization;

        /// <summary>Volume factor for a song of this loudness; 1 when the setting is off or the song is unmeasured.</summary>
        public static float GainFor(float? lufs)
        {
            return Enabled ? GainFor(lufs, TARGET_LUFS) : 1f;
        }

        public static float GainFor(float? lufs, float target)
        {
            if (lufs == null || float.IsNaN(lufs.Value) || float.IsInfinity(lufs.Value))
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.Pow(10f, (target - lufs.Value) / 20f));
        }
    }
}
