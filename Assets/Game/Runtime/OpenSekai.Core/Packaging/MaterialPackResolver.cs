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

        /// <summary>A deep copy as <typeparamref name="T"/> (the audio selection is its own type).</summary>
        public static T CopyAs<T>(MaterialGroupSelection source) where T : MaterialGroupSelection, new() => new()
        {
            Groups = new List<string>(source?.Groups ?? new List<string>()),
            Overrides = new Dictionary<string, string>(source?.Overrides ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        };

        /// <summary>Same groups in the same order and the same per-slot choices.</summary>
        public bool SameAs(MaterialGroupSelection other) =>
            other != null && Groups.SequenceEqual(other.Groups, StringComparer.Ordinal)
            && Overrides.Count == other.Overrides.Count
            && Overrides.All(item => other.Overrides.TryGetValue(item.Key, out string value) && value == item.Value);
    }

    /// <summary>
    /// What one slot resolves to under a selection, for the resource pack page: the source group
    /// (null for the category's built-in), why (<c>override</c>, <c>priority</c>, <c>builtin</c>),
    /// which enabled groups could provide it, and whether the bound media is a sequence or layout.
    /// </summary>
    public sealed class MaterialSlotState
    {
        public string Slot { get; internal set; }
        public string Source { get; internal set; }
        public string Reason { get; internal set; }
        public string[] Candidates { get; internal set; }
        public bool IsSequence { get; internal set; }
        public bool IsLayout { get; internal set; }
        internal string Path { get; set; }
        internal string PackRoot { get; set; }
        internal MaterialMediaSlot Media { get; set; }
        internal MaterialBackgroundScene Background { get; set; }

        /// <summary>Explicitly set to the built-in, as opposed to falling through to it.</summary>
        public bool IsExplicitBuiltin => Reason == REASON_OVERRIDE && Source == null;

        public const string REASON_OVERRIDE = "override";
        public const string REASON_PRIORITY = "priority";
        public const string REASON_BUILTIN = "builtin";
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
            if (MaterialSpriteSlot.IsLayout(Slot)) yield break;
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

        public static List<MaterialSpriteBinding> ResolveResultFx(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection, string installedRoot)
        {
            return ResolveSprites(packs, selection, installedRoot, "resultFx", MaterialResultFxSlots.Slots);
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
                    if (media is MaterialMediaSlot file && !MaterialSpriteSlot.IsLayout(slot))
                    {
                        MaterialPackManifest.CheckPath(file.File);
                        binding.Path = Path.Combine(binding.PackRoot, file.File);
                    }
                }
                bindings.Add(binding);
            }
            return bindings;
        }

        /// <summary>Resolves every category of <paramref name="configuration"/>; throws on the first invalid choice or exceeded budget.</summary>
        public static void Validate(IReadOnlyCollection<InstalledMaterialPack> packs, MaterialPackConfiguration configuration, string installedRoot)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (configuration.Builtin?.IsValid() != true) throw new InvalidDataException("内置套装选择无效。");
            foreach (string category in MaterialCategories.All)
            {
                try { Describe(packs, category, configuration.Get(category), installedRoot); }
                catch (InvalidDataException e) { throw new InvalidDataException(category + "：" + e.Message, e); }
            }
        }

        /// <summary>Every slot of <paramref name="category"/> under <paramref name="selection"/>, in registry order.</summary>
        public static List<MaterialSlotState> Describe(IEnumerable<InstalledMaterialPack> packs, string category,
            MaterialGroupSelection selection, string installedRoot)
        {
            var list = packs as IReadOnlyCollection<InstalledMaterialPack> ?? packs.ToArray();
            switch (category)
            {
                case MaterialCategories.AUDIO:
                    return Resolve(list, MaterialGroupSelection.CopyAs<MaterialAudioSelection>(selection), installedRoot)
                        .Select(b => new MaterialSlotState { Slot = b.Slot, Source = b.Source, Reason = b.Reason,
                            Candidates = b.Candidates, Path = b.Path, PackRoot = b.PackRoot, Media = b.Media }).ToList();
                case MaterialCategories.BACKGROUND:
                    var background = ResolveBackground(list, selection, installedRoot);
                    return new List<MaterialSlotState> { new() { Slot = background.Slot, Source = background.Source,
                        Reason = background.Reason, Candidates = background.Candidates, PackRoot = background.PackRoot,
                        Background = background.Media } };
                default:
                    if (!MaterialCategories.IsKnown(category)) throw new InvalidDataException("未知资源类别 " + category);
                    return ResolveSprites(list, selection, installedRoot, category, MaterialCategories.Slots(category))
                        .Select(b => new MaterialSlotState { Slot = b.Slot, Source = b.Source, Reason = b.Reason,
                            Candidates = b.Candidates, Path = b.Path, PackRoot = b.PackRoot, Media = b.Media,
                            IsSequence = b.Media?.Layers != null, IsLayout = MaterialSpriteSlot.IsLayout(b.Slot) }).ToList();
            }
        }

        /// <summary>
        /// Slots whose own choice does not show under these states because a whole-sequence
        /// override takes over their template: a result event drawn as a sequence hides every
        /// part of that event (and its layout); a hit effect drawn as a sequence hides its trail.
        /// </summary>
        public static HashSet<string> HiddenSlots(string category, IEnumerable<MaterialSlotState> states)
        {
            var hidden = new HashSet<string>(StringComparer.Ordinal);
            var list = states.ToList();
            foreach (var state in list.Where(s => s.Source != null && s.IsSequence))
            {
                if (category == MaterialCategories.RESULT_FX && MaterialResultFxSlots.IsEvent(state.Slot))
                    foreach (var part in list) { if (part.Slot.StartsWith(state.Slot + ".", StringComparison.Ordinal)) hidden.Add(part.Slot); }
                else if (category == MaterialCategories.HIT_FX && !MaterialHitFxSlots.IsTrail(state.Slot))
                    hidden.Add(state.Slot + ".trail");
            }
            return hidden;
        }

        /// <summary>The enabled group whose COMBO layout applies: the first one in order that has a layout, or null.</summary>
        public static string ComboLayoutSource(IEnumerable<InstalledMaterialPack> packs, MaterialGroupSelection selection)
        {
            var layouts = new HashSet<string>(packs.SelectMany(p => p.Groups.Where(g => g.Category == MaterialCategories.COMBO && g.Layout != null)
                .Select(g => p.GroupReference(g.Id))), StringComparer.Ordinal);
            return selection?.Groups?.FirstOrDefault(layouts.Contains);
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
