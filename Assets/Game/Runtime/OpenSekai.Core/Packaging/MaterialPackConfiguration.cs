using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSekai.MaterialPacks
{
    /// <summary>
    /// The nine categories a resource pack can replace, in the order the resource pack page lists
    /// them, with their slot registries. Three of them (note skin, hit effects, hit sounds) ship two
    /// built-in sets; the other six have a single game default.
    /// </summary>
    public static class MaterialCategories
    {
        public const string NOTES = "notes";
        public const string HIT_FX = "hitFx";
        public const string AUDIO = "audio";
        public const string JUDGMENT = "judgment";
        public const string COMBO = "combo";
        public const string LANE = "lane";
        public const string BACKGROUND = "background";
        public const string HUD = "hud";
        public const string RESULT_FX = "resultFx";

        /// <summary>Built-in set numbers run from 1 to this, matching <c>templateSet</c> and a preset's <c>builtinSet</c>.</summary>
        public const int BUILTIN_SET_COUNT = 2;

        public static IReadOnlyList<string> All { get; } = new[] { NOTES, HIT_FX, AUDIO, JUDGMENT, COMBO, LANE, BACKGROUND, HUD, RESULT_FX };

        public static bool IsKnown(string category) => category != null && All.Contains(category);

        /// <summary>True for the three categories with two selectable built-in sets.</summary>
        public static bool HasBuiltinSets(string category) => category is NOTES or HIT_FX or AUDIO;

        public static IEnumerable<string> Slots(string category) => category switch
        {
            NOTES => MaterialNoteSlots.Slots,
            HIT_FX => MaterialHitFxSlots.Slots,
            AUDIO => MaterialPackAudio.Slots,
            JUDGMENT => MaterialJudgmentSlots.Slots,
            COMBO => MaterialComboSlots.Slots,
            LANE => MaterialLaneSlots.Slots,
            BACKGROUND => new[] { MaterialBackgroundScene.SLOT },
            HUD => MaterialHudSlots.Slots,
            RESULT_FX => MaterialResultFxSlots.Slots,
            _ => throw new InvalidDataException("未知资源类别 " + category)
        };

        public static bool HasSlot(string category, string slot) => category switch
        {
            AUDIO => MaterialPackAudio.HasSlot(slot),
            BACKGROUND => slot == MaterialBackgroundScene.SLOT,
            _ => IsKnown(category) && MaterialPackManifest.HasSpriteSlot(category, slot)
        };
    }

    /// <summary>
    /// Which built-in set each of the three two-set categories falls back to when no external group
    /// provides a slot. Set numbers are 1 or 2; the legacy settings stored 0-based indices.
    /// </summary>
    public sealed class MaterialBuiltinSelection
    {
        public int Notes { get; set; } = 1;
        public int HitFx { get; set; } = 1;
        public int Audio { get; set; } = 1;

        public int Get(string category) => category switch
        {
            MaterialCategories.NOTES => Notes,
            MaterialCategories.HIT_FX => HitFx,
            MaterialCategories.AUDIO => Audio,
            _ => throw new InvalidDataException(category + " 没有可选的内置套装。")
        };

        public void Set(string category, int set)
        {
            if (set is < 1 or > MaterialCategories.BUILTIN_SET_COUNT) throw new InvalidDataException("内置套装编号需要 1 或 2。");
            switch (category)
            {
                case MaterialCategories.NOTES: Notes = set; break;
                case MaterialCategories.HIT_FX: HitFx = set; break;
                case MaterialCategories.AUDIO: Audio = set; break;
                default: throw new InvalidDataException(category + " 没有可选的内置套装。");
            }
        }

        public bool IsValid() => InRange(Notes) && InRange(HitFx) && InRange(Audio);

        /// <summary>Reads the three 0-based indices the settings page used to store (note skin, hit effect, hit sound).</summary>
        public static MaterialBuiltinSelection FromLegacy(int noteSkinIndex, int noteEffectIndex, int noteSeIndex) => new()
        {
            Notes = Clamp(noteSkinIndex + 1), HitFx = Clamp(noteEffectIndex + 1), Audio = Clamp(noteSeIndex + 1)
        };

        public MaterialBuiltinSelection Copy() => new() { Notes = Notes, HitFx = HitFx, Audio = Audio };

        public bool SameAs(MaterialBuiltinSelection other) =>
            other != null && Notes == other.Notes && HitFx == other.HitFx && Audio == other.Audio;

        private static bool InRange(int set) => set is >= 1 and <= MaterialCategories.BUILTIN_SET_COUNT;
        private static int Clamp(int set) => Math.Min(MaterialCategories.BUILTIN_SET_COUNT, Math.Max(1, set));
    }

    /// <summary>
    /// The two ways the resource pack page sets up a live. 预设 takes a whole preset (a pack's, or
    /// one of the built-in sets); 混搭 takes the player's own choice per category. Each keeps its
    /// choice while the other is in use.
    /// </summary>
    public static class MaterialPackModes
    {
        public const string PRESET = "preset";
        public const string MIX = "mix";

        public static bool IsKnown(string mode) => mode is PRESET or MIX;
    }

    /// <summary>
    /// The pack revision used in preset mode and the module choices of the built-in bundle.
    /// Older indexes have only BuiltinSet, which selected the same set for all three modules.
    /// </summary>
    public sealed class MaterialPresetChoice
    {
        public string PackId { get; set; }
        public string Digest { get; set; }
        public int BuiltinSet { get; set; } = 1;
        public MaterialBuiltinSelection BuiltinSets { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool IsBuiltin => PackId == null;

        public static MaterialPresetChoice Builtin(int set) => new() { BuiltinSet = set };

        public static MaterialPresetChoice Builtin(MaterialBuiltinSelection sets) =>
            new() { BuiltinSet = sets.Notes, BuiltinSets = sets.Copy() };

        public MaterialBuiltinSelection GetBuiltinSets() => BuiltinSets?.Copy() ??
            new MaterialBuiltinSelection { Notes = BuiltinSet, HitFx = BuiltinSet, Audio = BuiltinSet };

        public static MaterialPresetChoice FromPack(string packId, string digest) => new() { PackId = packId, Digest = digest };

        public bool IsFrom(string packId, string digest) => !IsBuiltin && PackId == packId && Digest == digest;

        public bool SameAs(MaterialPresetChoice other) =>
            other != null && PackId == other.PackId && Digest == other.Digest && (!IsBuiltin || GetBuiltinSets().SameAs(other.GetBuiltinSets()));

        public bool IsValid() => GetBuiltinSets().IsValid() && (IsBuiltin
            ? Digest == null
            : PackId.Length > 0 && Digest?.Length == 64);

        public MaterialPresetChoice Copy() => new() { PackId = PackId, Digest = Digest, BuiltinSet = BuiltinSet, BuiltinSets = BuiltinSets?.Copy() };
    }

    /// <summary>
    /// One complete setup of the nine categories: each category's groups in priority order (the
    /// first is the main group, the rest fall back in turn) and the built-in fallback sets. The
    /// 混搭 mode keeps one; a preset gives one.
    /// </summary>
    public sealed class MaterialPackConfiguration
    {
        public MaterialAudioSelection Audio { get; set; } = new();
        public MaterialGroupSelection Notes { get; set; } = new();
        public MaterialGroupSelection HitFx { get; set; } = new();
        public MaterialGroupSelection Judgment { get; set; } = new();
        public MaterialGroupSelection Combo { get; set; } = new();
        public MaterialGroupSelection Lane { get; set; } = new();
        public MaterialGroupSelection Background { get; set; } = new();
        public MaterialGroupSelection Hud { get; set; } = new();
        public MaterialGroupSelection ResultFx { get; set; } = new();
        public MaterialBuiltinSelection Builtin { get; set; } = new();

        public MaterialGroupSelection Get(string category) => category switch
        {
            MaterialCategories.AUDIO => Audio,
            MaterialCategories.NOTES => Notes,
            MaterialCategories.HIT_FX => HitFx,
            MaterialCategories.JUDGMENT => Judgment,
            MaterialCategories.COMBO => Combo,
            MaterialCategories.LANE => Lane,
            MaterialCategories.BACKGROUND => Background,
            MaterialCategories.HUD => Hud,
            MaterialCategories.RESULT_FX => ResultFx,
            _ => throw new InvalidDataException("未知资源类别 " + category)
        };

        public void Set(string category, MaterialGroupSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            switch (category)
            {
                case MaterialCategories.AUDIO: Audio = MaterialGroupSelection.CopyAs<MaterialAudioSelection>(selection); break;
                case MaterialCategories.NOTES: Notes = selection; break;
                case MaterialCategories.HIT_FX: HitFx = selection; break;
                case MaterialCategories.JUDGMENT: Judgment = selection; break;
                case MaterialCategories.COMBO: Combo = selection; break;
                case MaterialCategories.LANE: Lane = selection; break;
                case MaterialCategories.BACKGROUND: Background = selection; break;
                case MaterialCategories.HUD: Hud = selection; break;
                case MaterialCategories.RESULT_FX: ResultFx = selection; break;
                default: throw new InvalidDataException("未知资源类别 " + category);
            }
        }

        public MaterialPackConfiguration Copy()
        {
            var copy = new MaterialPackConfiguration { Builtin = (Builtin ?? new MaterialBuiltinSelection()).Copy() };
            foreach (string category in MaterialCategories.All)
                copy.Set(category, MaterialGroupSelection.CopyAs<MaterialGroupSelection>(Get(category)));
            return copy;
        }

        /// <summary>Same groups in the same order, and the same built-in sets.</summary>
        public bool SameChoicesAs(MaterialPackConfiguration other) =>
            other != null && (Builtin ?? new MaterialBuiltinSelection()).SameAs(other.Builtin ?? new MaterialBuiltinSelection())
            && MaterialCategories.All.All(category => Get(category).SameAs(other.Get(category)));
    }

    /// <summary>The preset a pack ships in <c>extensions["opensekai.preset"]</c>; group IDs are local to that pack.</summary>
    public sealed class MaterialPackPreset
    {
        public string Name { get; set; }
        public Dictionary<string, MaterialPresetCategory> Categories { get; set; } = new();
    }

    public sealed class MaterialPresetCategory
    {
        public List<string> Groups { get; set; } = new();
        public int? BuiltinSet { get; set; }
    }

    /// <summary>The pack icon from <c>extensions["opensekai.metadata"].icon</c>, a PNG checked at import.</summary>
    public sealed class MaterialPackIcon : MaterialMediaSlot
    {
        public int Width { get; set; }
        public int Height { get; set; }

        internal bool IsValid() => Hash?.Length == 64 && Hash.All(Uri.IsHexDigit)
            && Width is > 0 and <= MaterialSpriteSlot.MAX_DIMENSION && Height is > 0 and <= MaterialSpriteSlot.MAX_DIMENSION;
    }

    public static class MaterialPresets
    {
        /// <summary>
        /// Checks a pack's preset against its own groups: every category is known, every group is
        /// this pack's and of that category, and a built-in set number is only given where the
        /// category has two.
        /// </summary>
        public static void Validate(InstalledMaterialPack pack)
        {
            var preset = pack?.Preset;
            if (preset == null) return;
            if (string.IsNullOrWhiteSpace(preset.Name) || preset.Categories == null) throw Error("需要预设名称与类别。");
            var groups = pack.Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);
            foreach (var item in preset.Categories)
            {
                string category = item.Key;
                var value = item.Value;
                if (!MaterialCategories.IsKnown(category)) throw Error("未知类别 " + category);
                if (value?.Groups == null) throw Error(category + " 的预设无效");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in value.Groups)
                {
                    if (id == null || !seen.Add(id)) throw Error(category + " 的组重复或为空");
                    if (!groups.TryGetValue(id, out var group) || group.Category != category)
                        throw Error(category + " 引用了不属于本包该类别的组 " + id);
                }
                if (value.BuiltinSet.HasValue && (!MaterialCategories.HasBuiltinSets(category)
                    || value.BuiltinSet is < 1 or > MaterialCategories.BUILTIN_SET_COUNT))
                    throw Error(category + " 的 builtinSet 不适用于此类别");
            }
        }

        /// <summary>
        /// The complete configuration the pack's preset gives: declared categories take the
        /// preset's groups bound to this installed revision, with the player's
        /// <paramref name="chosen"/> main group (category to group ID) in place of the preset's
        /// own where it is one of the <see cref="Options"/>. Every missing slot falls back to the
        /// player's built-in bundle, including when an older manifest declares builtinSet.
        /// </summary>
        public static MaterialPackConfiguration Build(InstalledMaterialPack pack, MaterialBuiltinSelection builtin = null,
            IReadOnlyDictionary<string, string> chosen = null)
        {
            if (pack?.Preset == null) throw new InvalidDataException("此资源包没有随包预设。");
            Validate(pack);
            var configuration = new MaterialPackConfiguration { Builtin = builtin?.Copy() ?? new MaterialBuiltinSelection() };
            foreach (var item in pack.Preset.Categories)
            {
                List<string> groups = item.Value.Groups;
                string main = MainGroup(pack, item.Key, chosen);
                IEnumerable<string> ids = main == null ? groups : new[] { main }.Concat(groups.Skip(1).Where(id => id != main));
                configuration.Get(item.Key).Groups.AddRange(ids.Select(pack.GroupReference));
            }
            return configuration;
        }

        /// <summary>
        /// The groups of <paramref name="category"/> the player can pick the preset's main group
        /// from: the pack's groups of that category, except those the preset lists after its main
        /// group, which keep filling in behind whichever one is picked. Empty when the preset
        /// declares no group for the category.
        /// </summary>
        public static IReadOnlyList<MaterialPackGroup> Options(InstalledMaterialPack pack, string category)
        {
            if (pack?.Preset?.Categories == null || category == null
                || !pack.Preset.Categories.TryGetValue(category, out MaterialPresetCategory value) || value?.Groups == null || value.Groups.Count == 0)
                return Array.Empty<MaterialPackGroup>();
            var behind = new HashSet<string>(value.Groups.Skip(1), StringComparer.Ordinal);
            return pack.Groups.Where(g => g.Category == category && !behind.Contains(g.Id)).ToList();
        }

        /// <summary>
        /// The main group the preset uses for <paramref name="category"/>: the player's choice while
        /// it is still one of the <see cref="Options"/>, otherwise the preset's own. Null when the
        /// preset declares no group for the category.
        /// </summary>
        public static string MainGroup(InstalledMaterialPack pack, string category, IReadOnlyDictionary<string, string> chosen)
        {
            IReadOnlyList<MaterialPackGroup> options = Options(pack, category);
            if (options.Count == 0) return null;
            if (chosen != null && chosen.TryGetValue(category, out string id) && options.Any(g => g.Id == id)) return id;
            return pack.Preset.Categories[category].Groups[0];
        }

        /// <summary>A built-in preset: no external groups, and the two-set categories at <paramref name="set"/>.</summary>
        public static MaterialPackConfiguration BuildBuiltin(int set)
        {
            var configuration = new MaterialPackConfiguration();
            foreach (string category in MaterialCategories.All)
                if (MaterialCategories.HasBuiltinSets(category)) configuration.Builtin.Set(category, set);
            return configuration;
        }

        /// <summary>
        /// The configuration a preset choice gives among <paramref name="installed"/>, with the
        /// player's <paramref name="chosen"/> main groups for that pack. A pack that is no longer
        /// installed, or has no preset, falls back to the built-in bundle.
        /// </summary>
        public static MaterialPackConfiguration Build(IEnumerable<InstalledMaterialPack> installed, MaterialPresetChoice choice,
            IReadOnlyDictionary<string, string> chosen = null)
        {
            if (choice == null) return BuildBuiltin(1);
            if (choice.IsBuiltin) return new MaterialPackConfiguration { Builtin = choice.GetBuiltinSets() };
            var pack = installed?.FirstOrDefault(p => choice.IsFrom(p.PackId, p.Digest));
            return pack?.Preset == null ? new MaterialPackConfiguration { Builtin = choice.GetBuiltinSets() } : Build(pack, choice.GetBuiltinSets(), chosen);
        }

        private static InvalidDataException Error(string message) => new("随包预设：" + message);
    }
}
