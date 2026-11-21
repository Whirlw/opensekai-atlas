using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenSekai.MaterialPacks
{
    public static class MaterialPackAudio
    {
        // Slot IDs describe live uses. In particular, an empty tap and a Perfect hit are distinct.
        private static readonly Dictionary<string, string> _cues = new(StringComparer.Ordinal)
        {
            ["audio.tap"] = "se_live_tap",
            ["audio.perfect"] = "se_live_perfect",
            ["audio.great"] = "se_live_great",
            ["audio.good"] = "se_live_good",
            ["audio.critical"] = "se_live_critical",
            ["audio.long"] = "se_live_long",
            ["audio.longCritical"] = "se_live_long_critical",
            ["audio.connect"] = "se_live_connect",
            ["audio.connectCritical"] = "se_live_connect_critical",
            ["audio.flick"] = "se_live_flick",
            ["audio.flickCritical"] = "se_live_flick_critical",
            ["audio.trace"] = "se_live_trace",
            ["audio.traceCritical"] = "se_live_trace_critical",
            ["audio.countdown"] = "SE_LIVE_COUNT_DOWN",
            ["audio.clear"] = "se_live_clear",
            ["audio.fullCombo"] = "se_live_full_combo",
            ["audio.allPerfect"] = "se_live_all_perfect",
            ["audio.startVoice"] = "se_live_start",
            ["audio.finishVoice"] = "se_live_end_finish",
            ["audio.clearVoice"] = "se_live_end_clear",
            ["audio.fullComboVoice"] = "se_live_end_perfect",
            ["audio.allPerfectVoice"] = "se_live_end_all_perfect"
        };

        public static IEnumerable<string> Slots => _cues.Keys;
        public static bool HasSlot(string slot) => _cues.ContainsKey(slot);
        public static string Cue(string slot) => _cues[slot];
        public static bool UsesLoop(string slot) => slot is "audio.long" or "audio.longCritical"
            or "audio.trace" or "audio.traceCritical";
        public static bool AlsoUsesOneShot(string slot) => slot is "audio.trace" or "audio.traceCritical";
    }

    /// <summary>RIFF chunk offsets and frame counts, shared by import validation and playback.</summary>
    public sealed class MaterialPackWave
    {
        public int Channels { get; private set; }
        public int Rate { get; private set; }
        public int Frames { get; private set; }
        private int _offset;

        public static MaterialPackWave Read(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (bytes.Length < 12 || reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() != bytes.Length - 8
                || reader.ReadUInt32() != 0x45564157) throw new InvalidDataException("需要 RIFF WAVE 文件。");
            var wave = new MaterialPackWave();
            int dataSize = 0;
            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < 8) throw new InvalidDataException("WAV 块头不完整。");
                uint id = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                long end = stream.Position + size + (size & 1);
                if (end > stream.Length) throw new InvalidDataException("WAV 块越界。");
                if (id == 0x20746d66)
                {
                    if (wave.Channels != 0 || size < 16 || reader.ReadUInt16() != 1)
                        throw new InvalidDataException("需要单个 PCM fmt 块。");
                    wave.Channels = reader.ReadUInt16();
                    wave.Rate = reader.ReadInt32();
                    if (wave.Channels is not (1 or 2) || wave.Rate is not (44100 or 48000)
                        || reader.ReadInt32() != wave.Rate * wave.Channels * 2
                        || reader.ReadUInt16() != wave.Channels * 2 || reader.ReadUInt16() != 16)
                        throw new InvalidDataException("需要 44.1/48 kHz 单声道或双声道 16 位 PCM。");
                }
                else if (id == 0x61746164)
                {
                    if (wave._offset != 0 || size == 0) throw new InvalidDataException("需要单个非空 data 块。");
                    wave._offset = (int)stream.Position;
                    dataSize = (int)size;
                }
                stream.Position = end;
            }
            if (wave.Channels == 0 || dataSize == 0 || dataSize % (2 * wave.Channels) != 0)
                throw new InvalidDataException("WAV 缺少格式或完整采样帧。");
            wave.Frames = dataSize / (2 * wave.Channels);
            return wave;
        }

        public float[] Decode(byte[] bytes, int start, int end)
        {
            if (start < 0 || start >= end || end > Frames) throw new InvalidDataException("循环采样帧越界。");
            var samples = new float[(end - start) * Channels];
            int offset = _offset + start * Channels * 2;
            for (int i = 0; i < samples.Length; i++, offset += 2)
                samples[i] = (short)(bytes[offset] | bytes[offset + 1] << 8) / 32768f;
            return samples;
        }
    }
}
