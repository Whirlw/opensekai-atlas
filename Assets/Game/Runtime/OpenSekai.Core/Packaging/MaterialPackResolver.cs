using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public sealed class MaterialAudioSelection
    {
        public List<string> Groups { get; set; } = new();
        // A null value explicitly chooses the live's built-in cue.
        public Dictionary<string, string> Overrides { get; set; } = new();
    }

    public sealed class MaterialAudioBinding
    {
        public string Slot { get; internal set; }
        public string Source { get; internal set; }
        public string Reason { get; internal set; }
        public string[] Candidates { get; internal set; }
        internal string Path { get; set; }
        internal MaterialAudioSlot Media { get; set; }
    }

    public static class MaterialPackResolver
    {
        public const int MAX_DECODED_BYTES = 32 * 1024 * 1024;

        public static List<MaterialAudioBinding> Resolve(IEnumerable<InstalledMaterialPack> packs, MaterialAudioSelection selection, string installedRoot)
        {
            if (selection?.Groups == null || selection.Overrides == null)
                throw new InvalidDataException("音效选择无效。");
            var groups = packs.SelectMany(pack => pack.Groups.Select(group => (pack, group)))
                .ToDictionary(x => x.pack.GroupReference(x.group.Id), StringComparer.Ordinal);
            foreach (string id in selection.Groups)
                if (id == null || !groups.ContainsKey(id)) throw new InvalidDataException("未知音效组 " + id);
            foreach (var item in selection.Overrides)
                if (!MaterialPackAudio.HasSlot(item.Key) || item.Value != null &&
                    (!groups.TryGetValue(item.Value, out var g) || !g.group.Slots.ContainsKey(item.Key)))
                    throw new InvalidDataException("无效的逐槽选择 " + item.Key);
            var bindings = new List<MaterialAudioBinding>();
            var decoded = new HashSet<(string hash, int start, int end)>();
            long bytes = 0;
            foreach (string slot in MaterialPackAudio.Slots)
            {
                var candidates = selection.Groups.Where(id => groups[id].group.Slots.ContainsKey(slot)).Distinct().ToArray();
                bool assigned = selection.Overrides.TryGetValue(slot, out string source);
                if (!assigned) source = candidates.FirstOrDefault();
                var binding = new MaterialAudioBinding { Slot = slot, Candidates = candidates, Source = source,
                    Reason = assigned ? "override" : source == null ? "builtin" : "priority" };
                if (source != null)
                {
                    var (pack, group) = groups[source];
                    var media = group.Slots[slot];
                    binding.Media = media;
                    MaterialPackManifest.CheckPath(media.File);
                    binding.Path = Path.Combine(VersionPath(installedRoot, pack), media.File);
                    if (decoded.Add((media.Hash, media.Start, media.End))) bytes += (long)(media.End - media.Start) * media.Channels * 4;
                    if (MaterialPackAudio.AlsoUsesOneShot(slot) && decoded.Add((media.Hash, 0, media.Frames)))
                        bytes += (long)media.Frames * media.Channels * 4;
                    if (bytes > MAX_DECODED_BYTES)
                        throw new InvalidDataException($"{slot}：选中 PCM 数据 {bytes} 字节，预算 {MAX_DECODED_BYTES} 字节。");
                }
                bindings.Add(binding);
            }
            return bindings;
        }

        internal static string VersionPath(string installedRoot, InstalledMaterialPack pack)
        {
            MaterialPackManifest.CheckPath(pack.PackId);
            if (pack.PackId.Contains("/") || pack.Digest?.Length != 64 || !pack.Digest.All(Uri.IsHexDigit))
                throw new InvalidDataException("资源包修订路径无效。");
            return Path.Combine(installedRoot, pack.PackId, pack.Digest);
        }

    }
}
