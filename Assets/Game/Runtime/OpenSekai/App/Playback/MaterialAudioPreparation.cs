using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.Playback
{
    internal sealed class MaterialAudioPreparation
    {
        private readonly IReadOnlyList<MaterialAudioBinding> _bindings;
        internal LiveMaterialAudio Audio { get; }

        internal MaterialAudioPreparation(MaterialPackStore store)
        {
            _bindings = store.Capture();
            Audio = new LiveMaterialAudio(store.Release);
        }

        internal async Task PrepareAsync(List<string> diagnostics, CancellationToken token)
        {
            var cache = new Dictionary<(string hash, int start, int end), AudioClip>();
            foreach (var file in _bindings.Where(x => x.Source != null).GroupBy(x => x.Path))
            {
                token.ThrowIfCancellationRequested();
                byte[] bytes;
                MaterialPackWave wave;
                try
                {
                    (bytes, wave) = await Task.Run(() => Read(file.Key, file.First().Media), token);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    diagnostics.Add(string.Join(", ", file.Select(x => x.Slot)) + " (" + file.First().Media.File + ")：" + e.Message);
                    continue;
                }
                foreach (var binding in file)
                {
                    try
                    {
                        var media = binding.Media;
                        var clip = await GetClipAsync(media, media.Start, media.End);
                        var hit = MaterialPackAudio.AlsoUsesOneShot(binding.Slot) ? await GetClipAsync(media, 0, wave.Frames) : null;
                        string cue = MaterialPackAudio.Cue(binding.Slot);
                        Audio.Bind(cue, MaterialPackAudio.UsesLoop(binding.Slot), clip);
                        if (hit != null) Audio.Bind(cue, false, hit);
                    }
                    catch (Exception e) when (e is InvalidDataException or UnityException)
                    {
                        diagnostics.Add(binding.Slot + " (" + binding.Media.File + ")：" + e.Message);
                    }
                }

                async Task<AudioClip> GetClipAsync(MaterialAudioSlot media, int start, int end)
                {
                    var key = (media.Hash, start, end);
                    if (cache.TryGetValue(key, out var clip)) return clip;
                    float[] samples = await Task.Run(() => wave.Decode(bytes, start, end), token);
                    token.ThrowIfCancellationRequested();
                    clip = Audio.CreateClip(media.File, end - start, wave.Channels, wave.Rate, samples);
                    cache.Add(key, clip);
                    return clip;
                }
            }
        }

        private static (byte[] bytes, MaterialPackWave wave) Read(string path, MaterialAudioSlot expected)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaterialPackArchive.MAX_FILE_BYTES) throw new InvalidDataException("文件大小已改变。");
            using var reader = new BinaryReader(stream);
            var bytes = reader.ReadBytes((int)stream.Length);
            if (MaterialPackArchive.Hex(MaterialPackArchive.Hash(bytes)) != expected.Hash)
                throw new InvalidDataException("文件内容与安装修订不符。");
            return (bytes, MaterialPackWave.Read(bytes));
        }
    }
}
