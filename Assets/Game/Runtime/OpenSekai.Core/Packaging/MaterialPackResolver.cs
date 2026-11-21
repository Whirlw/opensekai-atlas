using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    public class MaterialGroupSelection
    {
        public List<string> Groups { get; set; } = new();
        // A null value explicitly selects the built-in resource for this slot.
        public Dictionary<string, string> Overrides { get; set; } = new();
    }

    public sealed class MaterialAudioSelection : MaterialGroupSelection { }
    public sealed class MaterialBackgroundBinding : MaterialBinding<MaterialBackgroundScene>
    {
        internal IEnumerable<MaterialSpriteBinding> Images()
        {
            for (int i = 0; i < Media.Layers.Length; i++)
            {
                var layer = Media.Layers[i];
                string key = MaterialBackgroundScene.LayerKey(i);
                if (layer.Image != null) yield return Bind(key, layer.Image);
                if (layer.Masks == null) continue;
                for (int j = 0; j < layer.Masks.Length; j++) yield return Bind(MaterialBackgroundScene.MaskKey(i, j), layer.Masks[j].Image);
            }
        }

        private MaterialSpriteBinding Bind(string key, MaterialSpriteSlot image) => new()
        {
            Slot = key, Source = Source, Media = image, Path = System.IO.Path.Combine(PackRoot, image.File)
        };
    }

    public sealed class MaterialAudioBinding : MaterialBinding<MaterialAudioSlot> { }
    public sealed class MaterialSpriteBinding : MaterialBinding<MaterialSpriteSlot>
    {
        internal IEnumerable<MaterialSpriteBinding> Images()
        {
            if (MaterialHudSlots.IsLayout(Slot)) yield break;
            if (Media.Layers == null) { yield return this; yield break; }
            for (int i = 0; i < Media.Layers.Length; i++)
            {
                var layer = Media.Layers[i];
                yield return new MaterialSpriteBinding { Slot = Slot + "/" + i, Source = Source,
                    Path = System.IO.Path.Combine(PackRoot, layer.File),
                    Media = new MaterialSpriteSlot { File = layer.File, Hash = layer.Hash, Width = layer.Width, Height = layer.Height } };
            }
        }
    }

    public class MaterialBinding<T> where T : class
    {
        public string Slot { get; internal set; }
        public string Source { get; internal set; }
        public string Reason { get; internal set; }
        public string[] Candidates { get; internal set; }
        internal string PackRoot { get; set; }
        internal string Path { get; set; }
        internal T Media { get; set; }
    }

    public static class MaterialPackResolver
    {
        public const int MAX_DECODED_BYTES = 32 * 1024 * 1024;

        public static List<MaterialAudioBinding> Resolve(IEnumerable<InstalledMaterialPack> packs, MaterialAudioSelection selection, string installedRoot)
        {
            var bindings = ResolveSlots<MaterialAudioSlot, MaterialAudioBinding>(packs, selection, installedRoot,
                "audio", MaterialPackAudio.Slots, group => group.Slots);
            var decoded = new HashSet<(string hash, int start, int end)>();
            long bytes = 0;
            foreach (var binding in bindings.Where(b => b.Source != null))
            {
                var media = binding.Media;
                if (decoded.Add((media.Hash, media.Start, media.End))) bytes += (long)(media.End - media.Start) * media.Channels * 4;
                if (MaterialPackAudio.AlsoUsesOneShot(binding.Slot) && decoded.Add((media.Hash, 0, media.Frames)))
                    bytes += (long)media.Frames * media.Channels * 4;
                if (bytes > MAX_DECODED_BYTES) throw new InvalidDataException($"{binding.Slot}：选中 PCM 数据 {bytes} 字节，预算 {MAX_DECODED_BYTES} 字节。");
            }
            return bindings;
        }

        public static List<MaterialSpriteBinding> ResolveCombo(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "combo", MaterialComboSlots.Slots);
        }

        public static List<MaterialSpriteBinding> ResolveJudgment(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "judgment", MaterialJudgmentSlots.Slots);
        }

        public static List<MaterialSpriteBinding> ResolveHitFx(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "hitFx", MaterialHitFxSlots.Slots);
        }

        public static List<MaterialSpriteBinding> ResolveNotes(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "notes", MaterialNoteSlots.Slots);
        }

        public static List<MaterialSpriteBinding> ResolveLane(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "lane", MaterialLaneSlots.Slots);
        }

        public static List<MaterialSpriteBinding> ResolveHud(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "hud", MaterialHudSlots.Slots);
        }

        public static MaterialBackgroundBinding ResolveBackground(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            var binding = ResolveSlots<MaterialBackgroundScene, MaterialBackgroundBinding>(packs, selection, installedRoot, "background",
                new[] { MaterialBackgroundScene.SLOT }, group => new Dictionary<string, MaterialBackgroundScene> { [MaterialBackgroundScene.SLOT] = group.Background }).Single();
            if (binding.Source != null) CheckTextureBudget("background", binding.Images());
            return binding;
        }

        private static List<MaterialSpriteBinding> ResolveSprites(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection,
            string installedRoot, string category, IEnumerable<string> slots)
        {
            var bindings = ResolveSlots<MaterialSpriteSlot, MaterialSpriteBinding>(packs, selection, installedRoot,
                category, slots, group => group.Sprites);
            CheckTextureBudget(category, bindings.Where(b => b.Source != null).SelectMany(b => b.Images()));
            return bindings;
        }

        private static void CheckTextureBudget(string category, IEnumerable<MaterialSpriteBinding> images)
        {
            long bytes = images.GroupBy(b => b.Media.Hash)
                .Sum(g => (long)g.First().Media.Width * g.First().Media.Height * 4);
            if (bytes > MAX_DECODED_BYTES) throw new InvalidDataException($"{category} 纹理需要 {bytes} 字节，预算 {MAX_DECODED_BYTES} 字节。");
        }

        private static List<TBinding> ResolveSlots<TMedia, TBinding>(IEnumerable<InstalledMaterialPack> packs,
            MaterialGroupSelection selection, string installedRoot, string category, IEnumerable<string> slots,
            Func<MaterialPackGroup, Dictionary<string, TMedia>> entries)
            where TMedia : class where TBinding : MaterialBinding<TMedia>, new()
        {
            if (selection?.Groups == null || selection.Overrides == null)
                throw new InvalidDataException(category + " 选择无效。");
            var groups = packs.SelectMany(pack => pack.Groups.Where(group => group.Category == category).Select(group => (pack, group)))
                .ToDictionary(x => x.pack.GroupReference(x.group.Id), StringComparer.Ordinal);
            foreach (string id in selection.Groups)
                if (id == null || !groups.ContainsKey(id)) throw new InvalidDataException("未知资源组 " + id);
            foreach (var item in selection.Overrides)
                if (!slots.Contains(item.Key) || item.Value != null &&
                    (!groups.TryGetValue(item.Value, out var g) || !entries(g.group).ContainsKey(item.Key)))
                    throw new InvalidDataException("无效的逐槽选择 " + item.Key);
            var bindings = new List<TBinding>();
            foreach (string slot in slots)
            {
                var candidates = selection.Groups.Where(id => entries(groups[id].group).ContainsKey(slot)).Distinct().ToArray();
                bool assigned = selection.Overrides.TryGetValue(slot, out string source);
                if (!assigned) source = candidates.FirstOrDefault();
                var binding = new TBinding { Slot = slot, Candidates = candidates, Source = source,
                    Reason = assigned ? "override" : source == null ? "builtin" : "priority" };
                if (source != null)
                {
                    var (pack, group) = groups[source];
                    var media = entries(group)[slot];
                    binding.Media = media;
                    binding.PackRoot = VersionPath(installedRoot, pack);
                    if (media is MaterialMediaSlot file && !MaterialHudSlots.IsLayout(slot))
                    {
                        MaterialPackManifest.CheckPath(file.File);
                        binding.Path = Path.Combine(binding.PackRoot, file.File);
                    }
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
