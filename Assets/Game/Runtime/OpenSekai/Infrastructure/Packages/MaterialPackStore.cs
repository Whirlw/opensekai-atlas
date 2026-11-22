using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenSekai.MaterialPacks;
using OpenSekai.Shared.Settings;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
    public sealed class MaterialPackState
    {
        [JsonProperty(Required = Required.Always)]
        public long Revision { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<InstalledMaterialPack> Installed { get; set; } = new();
        [JsonProperty(Required = Required.Always)]
        public MaterialAudioSelection Audio { get; set; } = new();
        public MaterialGroupSelection Background { get; set; } = new();
        public MaterialGroupSelection ResultFx { get; set; } = new();
        public MaterialGroupSelection Hud { get; set; } = new();
        public MaterialGroupSelection Lane { get; set; } = new();
        public MaterialGroupSelection Notes { get; set; } = new();
        public MaterialGroupSelection HitFx { get; set; } = new();
        public MaterialGroupSelection Judgment { get; set; } = new();
        public MaterialGroupSelection Combo { get; set; } = new();

        /// <summary>
        /// The 混搭 built-in fallback sets. Choosing the built-in bundle's sets sets them too, so
        /// 混搭 falls back to the same sets as every bundle. Missing from an index written before
        /// the resource pack page; the store fills it from the old settings when it reads such an index.
        /// </summary>
        public MaterialBuiltinSelection Builtin { get; set; }

        /// <summary>
        /// The main group the player chose per category in each installed bundle, keyed by
        /// <see cref="BundleKey"/>. A category left out uses the bundle's own preset.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> BundleGroups { get; set; } = new();

        /// <summary>
        /// <see cref="MaterialPackModes.PRESET"/> or <see cref="MaterialPackModes.MIX"/>. Missing from
        /// an index written before the two modes; the store decides it when it reads such an index.
        /// </summary>
        public string Mode { get; set; }
        public bool MixInitialized { get; set; }

        /// <summary>The preset 预设 mode uses. 混搭 leaves it as it is, and the other way round.</summary>
        public MaterialPresetChoice Preset { get; set; } = MaterialPresetChoice.Builtin(1);

        [JsonIgnore]
        public bool IsPresetMode => Mode != MaterialPackModes.MIX;

        public MaterialGroupSelection Selection(string category) => category switch
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

        /// <summary>A detached copy of the 混搭 configuration: the nine selections and the built-in sets.</summary>
        public MaterialPackConfiguration MixConfiguration()
        {
            var configuration = new MaterialPackConfiguration { Builtin = Builtin ?? new MaterialBuiltinSelection() };
            foreach (string category in MaterialCategories.All) configuration.Set(category, Selection(category));
            return configuration.Copy();
        }

        /// <summary>The configuration a live gets now: the chosen preset in 预设 mode, the 混搭 one otherwise.</summary>
        public MaterialPackConfiguration ActiveConfiguration() =>
            IsPresetMode ? MaterialPresets.Build(Installed, Preset, BundleGroupsOf(Preset?.PackId, Preset?.Digest)) : MixConfiguration();

        internal MaterialPackConfiguration BundleConfiguration(MaterialPresetChoice choice)
        {
            if (choice == null || !choice.IsValid()) throw new InvalidDataException("预设选择无效。");
            if (choice.IsBuiltin) return new MaterialPackConfiguration { Builtin = choice.GetBuiltinSets() };
            var pack = FindPack(choice.PackId, choice.Digest) ?? throw new InvalidDataException("找不到已安装的资源包修订。");
            return MaterialPresets.Build(pack, Preset.GetBuiltinSets(), BundleGroupsOf(pack.PackId, pack.Digest));
        }

        /// <summary>The main groups the player chose in one installed bundle (category to group ID); null when none.</summary>
        public IReadOnlyDictionary<string, string> BundleGroupsOf(string packId, string digest) =>
            packId != null && BundleGroups != null && BundleGroups.TryGetValue(BundleKey(packId, digest), out var groups) ? groups : null;

        internal static string BundleKey(string packId, string digest) => packId + "/" + digest;

        /// <summary>The installed pack whose preset 预设 mode uses; null for a built-in preset.</summary>
        [JsonIgnore]
        public InstalledMaterialPack PresetPack =>
            Preset == null || Preset.IsBuiltin ? null : FindPack(Preset.PackId, Preset.Digest);

        internal void Apply(MaterialPackConfiguration configuration)
        {
            var copy = configuration.Copy();
            Audio = copy.Audio;
            Notes = copy.Notes;
            HitFx = copy.HitFx;
            Judgment = copy.Judgment;
            Combo = copy.Combo;
            Lane = copy.Lane;
            Background = copy.Background;
            Hud = copy.Hud;
            ResultFx = copy.ResultFx;
            Builtin = copy.Builtin;
        }

        public InstalledMaterialPack FindPack(string packId, string digest) =>
            Installed.Find(p => p.PackId == packId && p.Digest == digest);

        /// <summary>The installed pack and group a reference (<c>packId/digest/groupId</c>) points at.</summary>
        public bool TryFindGroup(string reference, out InstalledMaterialPack pack, out MaterialPackGroup group)
        {
            group = null;
            pack = null;
            if (!InstalledMaterialPack.TryParseReference(reference, out string packId, out string digest, out string groupId)) return false;
            pack = FindPack(packId, digest);
            group = pack?.Groups.Find(g => g.Id == groupId);
            return group != null;
        }
    }

    /// <summary>Another live holds the configuration; writes wait until it ends.</summary>
    public sealed class MaterialPackBusyException : InvalidOperationException
    {
        public MaterialPackBusyException() : base("本局进行中。") { }
    }

    /// <summary>The configuration changed since the caller read it.</summary>
    public sealed class MaterialPackConflictException : InvalidOperationException
    {
        public MaterialPackConflictException(long current) : base("资源包选择已改变，当前修订 " + current) => CurrentRevision = current;
        public long CurrentRevision { get; }
    }

    /// <summary>What removing an installed revision takes out of the two modes.</summary>
    public sealed class MaterialUninstallImpact
    {
        public InstalledMaterialPack Pack { get; internal set; }

        /// <summary>混搭 categories that lose an enabled group (main or fallback).</summary>
        public List<string> Categories { get; } = new();

        /// <summary>Per-slot choices that pointed at one of its groups, as (category, slot).</summary>
        public List<(string Category, string Slot)> Overrides { get; } = new();

        /// <summary>
        /// The name of this revision's preset when 预设 mode had it chosen; the choice goes back to
        /// built-in set 1. Null otherwise.
        /// </summary>
        public string ClearedPreset { get; internal set; }

        public bool ChangesConfiguration => Categories.Count > 0 || Overrides.Count > 0 || ClearedPreset != null;
    }

    /// <summary>
    /// Everything one live (or the resource pack page's demo) draws from the packs: the bindings of
    /// every category under one configuration, and its built-in sets.
    /// </summary>
    internal sealed class MaterialPackBindings
    {
        internal IReadOnlyList<MaterialAudioBinding> Audio = Array.Empty<MaterialAudioBinding>();
        internal IReadOnlyList<MaterialSpriteBinding> Combo = Array.Empty<MaterialSpriteBinding>();
        internal MaterialComboLayout ComboLayout;
        internal IReadOnlyList<MaterialSpriteBinding> Judgment = Array.Empty<MaterialSpriteBinding>();
        internal IReadOnlyList<MaterialSpriteBinding> HitFx = Array.Empty<MaterialSpriteBinding>();
        internal IReadOnlyList<MaterialSpriteBinding> Notes = Array.Empty<MaterialSpriteBinding>();
        internal IReadOnlyList<MaterialSpriteBinding> Lane = Array.Empty<MaterialSpriteBinding>();
        internal MaterialBackgroundBinding Background;
        internal IReadOnlyList<MaterialSpriteBinding> Hud = Array.Empty<MaterialSpriteBinding>();
        internal IReadOnlyList<MaterialSpriteBinding> ResultFx = Array.Empty<MaterialSpriteBinding>();
        internal MaterialBuiltinSelection Builtin = new();
    }

    public enum MaterialPreviewKind
    {
        Image,
        Sequence,
        Audio,
        Layout,
        Jacket
    }

    /// <summary>One thing a source shows on the preview card, with what is needed to load it.</summary>
    public sealed class MaterialPreviewItem
    {
        public string Slot { get; internal set; }
        public MaterialPreviewKind Kind { get; internal set; }
        public string File { get; internal set; }

        /// <summary>Pixel rect inside the PNG, top-left origin ([x, y, w, h]); for a note, its fractional slice.</summary>
        public float[] Rect { get; internal set; }
        public int FrameCount { get; internal set; }
        internal string Path { get; set; }
        internal MaterialMediaSlot Media { get; set; }

        internal static MaterialPreviewItem FromSprite(string slot, MaterialSpriteSlot media, string root)
        {
            if (media == null) return null;
            if (MaterialSpriteSlot.IsLayout(slot)) return new MaterialPreviewItem { Slot = slot, Kind = MaterialPreviewKind.Layout };
            if (media.Layers != null)
            {
                var layer = media.Layers[0];
                var frame = layer.Frames[0].Rect;
                return new MaterialPreviewItem
                {
                    Slot = slot, Kind = MaterialPreviewKind.Sequence, File = layer.File, Path = System.IO.Path.Combine(root, layer.File),
                    Media = new MaterialSpriteSlot { File = layer.File, Hash = layer.Hash, Width = layer.Width, Height = layer.Height },
                    Rect = new float[] { frame[0], frame[1], frame[2], frame[3] }, FrameCount = media.Layers.Sum(l => l.Frames.Length)
                };
            }
            float[] rect = media.NoteSlice ?? (media.Rect == null ? new float[] { 0, 0, media.Width, media.Height }
                : media.Rect.Select(v => (float)v).ToArray());
            return new MaterialPreviewItem
            {
                Slot = slot, Kind = MaterialPreviewKind.Image, File = media.File, Path = System.IO.Path.Combine(root, media.File),
                Media = media, Rect = rect
            };
        }

        internal static IEnumerable<MaterialPreviewItem> FromBackground(MaterialBackgroundScene scene, string root)
        {
            for (int i = 0; i < scene.Layers.Length; i++)
            {
                var layer = scene.Layers[i];
                string key = "background.layers." + i;
                if (layer.Image == null)
                {
                    yield return new MaterialPreviewItem { Slot = key, Kind = MaterialPreviewKind.Jacket };
                    continue;
                }
                var item = FromSprite(MaterialBackgroundScene.SLOT, layer.Image, root);
                item.Slot = key;
                yield return item;
            }
        }
    }

    /// <summary>Everything one installed group provides, for the preview card.</summary>
    public sealed class MaterialGroupPreview
    {
        public InstalledMaterialPack Pack { get; internal set; }
        public MaterialPackGroup Group { get; internal set; }
        public List<MaterialPreviewItem> Items { get; } = new();

        /// <summary>Slots of the category the group provides, against the category's whole registry.</summary>
        public int ProvidedSlots { get; internal set; }
        public int TotalSlots { get; internal set; }
    }

    public sealed class MaterialPackStore
    {
        private static MaterialPackStore _shared;
        public static MaterialPackStore Shared => _shared ??= new MaterialPackStore(
            Path.Combine(Application.persistentDataPath, "material-packs"), ReadLegacyBuiltin);
        private readonly string _root;
        private readonly object _gate = new();
        private MaterialPackState _state;
        private int _sessions;
        private string StatePath => Path.Combine(_root, "state.json");

        /// <param name="legacyBuiltin">
        /// The three built-in choices the settings page used to keep, read once when an index has
        /// none; null leaves the game defaults.
        /// </param>
        internal MaterialPackStore(string root, Func<MaterialBuiltinSelection> legacyBuiltin = null)
        {
            _root = root;
            MaterialPackState backup = null;
            try { backup = ReadState(StatePath + ".bak"); }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
            {
                // A valid primary still wins. Without either generation, preserve the files for recovery.
                if (!File.Exists(StatePath)) throw;
            }
            try { _state = ReadState(StatePath); }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
            {
                if (backup == null) throw;
            }
            _state ??= backup ?? new MaterialPackState();
            // Until the first write the migrated sets live in memory only; the old settings they
            // come from no longer change, so every read gives the same result.
            if (_state.Builtin == null) _state.Builtin = ReadLegacy(legacyBuiltin);
            if (_state.Mode == null) _state.Mode = InitialMode(_state);
            if (!_state.IsPresetMode) _state.MixInitialized = true;
            // Only the process-owned store opens this root. An interrupted import never became selectable.
            string staging = Path.Combine(_root, "staging");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            CollectOrphans(backup ?? _state, _state);
        }

        /// <summary>
        /// The mode for an index written before the two modes existed: 混搭 when it already enables
        /// groups or splits the built-in sets, so nothing changes for the next live; otherwise the
        /// built-in preset of its one set. Like the migrated sets, this stays in memory until a write.
        /// </summary>
        private static string InitialMode(MaterialPackState state)
        {
            var builtin = state.Builtin;
            bool mixed = MaterialCategories.All.Any(c => state.Selection(c).Groups.Count > 0 || state.Selection(c).Overrides.Count > 0)
                || builtin.Notes != builtin.HitFx || builtin.HitFx != builtin.Audio;
            if (mixed) return MaterialPackModes.MIX;
            state.Preset = MaterialPresetChoice.Builtin(builtin.Notes);
            return MaterialPackModes.PRESET;
        }

        private static MaterialBuiltinSelection ReadLegacyBuiltin()
        {
            var settings = LiveSettingsStore.Shared.Load();
            return MaterialBuiltinSelection.FromLegacy(settings.NoteSkinIndex, settings.NoteEffect, settings.NoteSeIndex);
        }

        private static MaterialBuiltinSelection ReadLegacy(Func<MaterialBuiltinSelection> legacyBuiltin)
        {
            try
            {
                var builtin = legacyBuiltin?.Invoke();
                return builtin != null && builtin.IsValid() ? builtin.Copy() : new MaterialBuiltinSelection();
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                Debug.LogWarning("[MaterialPack] 无法读取旧设置中的内置选择：" + e.Message);
                return new MaterialBuiltinSelection();
            }
        }

        private MaterialPackState ReadState(string path)
        {
            if (!File.Exists(path)) return null;
            var state = JsonConvert.DeserializeObject<MaterialPackState>(File.ReadAllText(path));
            if (state?.Installed == null || state.Revision < 0 || state.Audio?.Groups == null || state.Audio.Overrides == null)
                throw new InvalidDataException("资源包索引无效。");
            if (state.Builtin != null && !state.Builtin.IsValid()) throw new InvalidDataException("内置套装索引无效。");
            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pack in state.Installed)
            {
                if (pack?.Groups == null || !versions.Add(VersionPath(pack)))
                    throw new InvalidDataException("资源包修订索引无效。");
                if (pack.Icon != null)
                {
                    if (!pack.Icon.IsValid()) throw new InvalidDataException("资源包图标索引无效：" + pack.PackId);
                    MaterialPackManifest.CheckPath(pack.Icon.File);
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var group in pack.Groups)
                {
                    if (group?.Slots == null || string.IsNullOrEmpty(group.Id) || !ids.Add(group.Id))
                        throw new InvalidDataException("资源包组索引无效。");
                    if (group.Category is not ("audio" or "combo" or "judgment" or "hitFx" or "notes" or "lane" or "hud" or "resultFx" or "background") || group.Sprites == null
                        || (group.Category == "background" ? group.Background?.IsValid() != true || group.Sprites.Count != 0 : group.Background != null)
                        || group.Layout != null && (group.Category != "combo" || !group.Layout.IsValid())
                        || group.Category != "audio" && group.Slots.Count != 0 || group.Category == "audio" && group.Sprites.Count != 0)
                        throw new InvalidDataException("资源包类别无效：" + group.Id);
                    foreach (var item in group.Sprites)
                    {
                        var media = item.Value;
                        bool known = MaterialPackManifest.HasSpriteSlot(group.Category, item.Key);
                        if (known && MaterialSpriteSlot.IsLayout(item.Key) && media?.IsValid(item.Key) == true) continue;
                        if (!known || media?.Hash?.Length != 64 || !media.Hash.All(Uri.IsHexDigit)
                            || !media.IsValid(item.Key)) throw new InvalidDataException(group.Category + " 槽位索引无效：" + item.Key);
                        MaterialPackManifest.CheckPath(media.File);
                    }
                    foreach (var item in group.Slots)
                    {
                        var media = item.Value;
                        if (!MaterialPackAudio.HasSlot(item.Key) || media?.Hash?.Length != 64 || !media.Hash.All(Uri.IsHexDigit)
                            || media.Channels is not (1 or 2) || media.Start < 0 || media.Start >= media.End || media.End > media.Frames)
                            throw new InvalidDataException("资源包槽位索引无效：" + item.Key);
                        MaterialPackManifest.CheckPath(media.File);
                    }
                }
                MaterialPresets.Validate(pack);
            }
            Resolve(state, state.Audio);
            MaterialPackResolver.ResolveCombo(state.Installed, state.Combo, InstalledRoot);
            MaterialPackResolver.ResolveJudgment(state.Installed, state.Judgment, InstalledRoot);
            MaterialPackResolver.ResolveBackground(state.Installed, state.Background, InstalledRoot);
            MaterialPackResolver.ResolveHud(state.Installed, state.Hud, InstalledRoot);
            MaterialPackResolver.ResolveResultFx(state.Installed, state.ResultFx, InstalledRoot);
            MaterialPackResolver.ResolveLane(state.Installed, state.Lane, InstalledRoot);
            MaterialPackResolver.ResolveNotes(state.Installed, state.Notes, InstalledRoot);
            MaterialPackResolver.ResolveHitFx(state.Installed, state.HitFx, InstalledRoot);
            if (state.Mode != null && !MaterialPackModes.IsKnown(state.Mode)) throw new InvalidDataException("资源包模式无效。");
            if (state.Preset == null || !state.Preset.IsValid()) throw new InvalidDataException("预设选择无效。");
            if (!state.Preset.IsBuiltin && state.PresetPack?.Preset == null)
                state.Preset = MaterialPresetChoice.Builtin(state.Preset.GetBuiltinSets());
            // A choice that no longer matches the bundle's groups is ignored where it is read.
            state.BundleGroups ??= new Dictionary<string, Dictionary<string, string>>();
            return state;
        }

        public MaterialPackState List()
        {
            lock (_gate) return Copy(_state);
        }

        /// <summary>True while a live holds the configuration (writes are refused until it ends).</summary>
        public bool IsInSession
        {
            get { lock (_gate) return _sessions != 0; }
        }

        // Call on Unity's thread so the Android free-space query runs before dispatching ZIP I/O.
        public Task<InstalledMaterialPack> ImportAsync(string zipPath, CancellationToken token = default) =>
            InspectAsync(zipPath, true, token);

        public Task<InstalledMaterialPack> ValidateAsync(string zipPath, CancellationToken token = default) =>
            InspectAsync(zipPath, false, token);

        private async Task<InstalledMaterialPack> InspectAsync(string zipPath, bool install, CancellationToken token)
        {
            if (install) lock (_gate) CheckWritable();
            long? available = SafeZipExtractor.GetAvailableBytes(_root);
            string stage = Path.Combine(_root, "staging", Guid.NewGuid().ToString("N"));
            try
            {
                var pack = await Task.Run(() => MaterialPackArchive.Unpack(zipPath, stage, available, token), token);
                foreach (string path in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
                {
                    if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    byte[] bytes = await Task.Run(() => File.ReadAllBytes(path), token);
                    token.ThrowIfCancellationRequested();
                    MaterialPackTexture.Release(MaterialPackTexture.Decode(bytes, Path.GetFileName(path)));
                }
                return await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!install) return pack;
                    lock (_gate)
                    {
                        token.ThrowIfCancellationRequested();
                        CheckWritable();
                        if (_state.Installed.Any(p => p.PackId == pack.PackId && p.Digest == pack.Digest)) return pack;
                        string destination = VersionPath(pack);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        // An orphan here can only come from a crash between publishing files and the index.
                        if (Directory.Exists(destination)) Directory.Delete(destination, true);
                        Directory.Move(stage, destination);
                        var next = Copy(_state);
                        next.Installed.Add(Copy(pack));
                        Commit(next);
                    }
                    return pack;
                }, token);
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }

        public IReadOnlyList<MaterialAudioBinding> Resolve(MaterialAudioSelection selection)
        {
            lock (_gate) return Resolve(_state, Copy(selection));
        }

        public long SaveSelection(MaterialAudioSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.AUDIO, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveCombo(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveCombo(_state.Installed, selection, InstalledRoot);
        }

        public long SaveComboSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.COMBO, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveJudgment(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveJudgment(_state.Installed, selection, InstalledRoot);
        }

        public long SaveJudgmentSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.JUDGMENT, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveHitFx(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveHitFx(_state.Installed, selection, InstalledRoot);
        }

        public long SaveHitFxSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.HIT_FX, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveNotes(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveNotes(_state.Installed, selection, InstalledRoot);
        }

        public long SaveNotesSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.NOTES, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveLane(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveLane(_state.Installed, selection, InstalledRoot);
        }

        public long SaveLaneSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.LANE, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveHud(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveHud(_state.Installed, selection, InstalledRoot);
        }

        public long SaveHudSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.HUD, selection, expectedRevision);

        public IReadOnlyList<MaterialSpriteBinding> ResolveResultFx(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveResultFx(_state.Installed, selection, InstalledRoot);
        }

        public long SaveResultFxSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.RESULT_FX, selection, expectedRevision);

        public MaterialBackgroundBinding ResolveBackground(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveBackground(_state.Installed, selection, InstalledRoot);
        }

        public long SaveBackgroundSelection(MaterialGroupSelection selection, long expectedRevision) =>
            SaveCategory(MaterialCategories.BACKGROUND, selection, expectedRevision);

        /// <summary>Saves one 混搭 category; the live uses 混搭 from then on.</summary>
        private long SaveCategory(string category, MaterialGroupSelection selection, long expectedRevision)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var configuration = _state.MixConfiguration();
                configuration.Set(category, MaterialGroupSelection.CopyAs<MaterialGroupSelection>(selection));
                return CommitMix(configuration);
            }
        }

        /// <summary>Every slot of <paramref name="category"/> under <paramref name="selection"/>, with its source and reason.</summary>
        public IReadOnlyList<MaterialSlotState> Describe(string category, MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.Describe(_state.Installed, category, selection, InstalledRoot);
        }

        /// <summary>
        /// Saves the whole 混搭 configuration in one commit and switches to 混搭, after checking every
        /// category together; on any failure nothing changes.
        /// </summary>
        public long SaveConfiguration(MaterialPackConfiguration configuration, long expectedRevision)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                return CommitMix(configuration);
            }
        }

        /// <summary>The complete configuration a preset choice gives, checked against the budgets; nothing is saved.</summary>
        public MaterialPackConfiguration ResolvePreset(MaterialPresetChoice choice)
        {
            lock (_gate)
            {
                var configuration = _state.BundleConfiguration(choice);
                MaterialPackResolver.Validate(_state.Installed, configuration, InstalledRoot);
                return configuration;
            }
        }

        /// <summary>The complete configuration a pack's preset gives, checked against the budgets; nothing is saved.</summary>
        public MaterialPackConfiguration ResolvePreset(string packId, string digest) =>
            ResolvePreset(MaterialPresetChoice.FromPack(packId, digest));

        /// <summary>Chooses the preset 预设 mode uses and switches to 预设. 混搭 keeps its own choices.</summary>
        public long ChoosePreset(MaterialPresetChoice choice, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                MaterialPackConfiguration configuration = next.BundleConfiguration(choice);
                MaterialPackResolver.Validate(next.Installed, configuration, InstalledRoot);
                if (choice.IsBuiltin) next.Preset = choice.Copy();
                else
                {
                    // Keep the built-in bundle's module choices while an imported bundle is active.
                    next.Preset.PackId = choice.PackId;
                    next.Preset.Digest = choice.Digest;
                }
                next.Mode = MaterialPackModes.PRESET;
                return Commit(next);
            }
        }

        /// <summary>Chooses a pack's preset; see <see cref="ChoosePreset"/>.</summary>
        public long ApplyPreset(string packId, string digest, long expectedRevision) =>
            ChoosePreset(MaterialPresetChoice.FromPack(packId, digest), expectedRevision);

        /// <summary>
        /// Switches between 预设 and 混搭; each keeps its own choices. The first switch to 混搭, while
        /// 混搭 has never been set up (no groups, built-in set 1 everywhere), starts it from the
        /// chosen preset, so changing one category of a preset is one step away.
        /// </summary>
        public long SetMode(string mode, long expectedRevision)
        {
            if (!MaterialPackModes.IsKnown(mode)) throw new ArgumentException("未知模式 " + mode, nameof(mode));
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                if (_state.Mode == mode) return _state.Revision;
                var next = Copy(_state);
                if (mode == MaterialPackModes.MIX && !next.MixInitialized)
                {
                    if (IsUntouched(next.MixConfiguration())) next.Apply(next.ActiveConfiguration());
                    next.MixInitialized = true;
                }
                next.Mode = mode;
                MaterialPackResolver.Validate(next.Installed, next.ActiveConfiguration(), InstalledRoot);
                return Commit(next);
            }
        }

        // The built-in sets follow the built-in bundle, so only groups and per-slot choices count.
        private static bool IsUntouched(MaterialPackConfiguration mix) =>
            MaterialCategories.All.All(c => mix.Get(c).Groups.Count == 0 && mix.Get(c).Overrides.Count == 0);

        /// <summary>
        /// Saves the built-in bundle's set for each two-set category. Every bundle falls back to
        /// these sets, and so does 混搭, whose own copy changes with them. The mode stays.
        /// </summary>
        public long SaveBuiltinBundle(MaterialBuiltinSelection sets, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                if (sets?.IsValid() != true) throw new InvalidDataException("内置整合包的套装选择无效。");
                if (_state.Preset.GetBuiltinSets().SameAs(sets) && (_state.Builtin ?? new MaterialBuiltinSelection()).SameAs(sets))
                    return _state.Revision;
                var next = Copy(_state);
                next.Preset.BuiltinSet = sets.Notes;
                next.Preset.BuiltinSets = sets.Copy();
                next.Builtin = sets.Copy();
                return Commit(next);
            }
        }

        /// <summary>
        /// Chooses which of a bundle's groups its preset uses as the main group of one category
        /// (one of <see cref="MaterialPresets.Options"/>). The choice belongs to that installed
        /// revision: 预设 mode uses it at once when the bundle is the chosen preset, and 混搭 takes
        /// it the next time the bundle is loaded. The mode stays; choosing what is already used
        /// writes nothing.
        /// </summary>
        public long SaveBundleGroup(string packId, string digest, string category, string groupId, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var pack = _state.FindPack(packId, digest) ?? throw new InvalidDataException("找不到已安装的资源包修订。");
                if (pack.Preset == null) throw new InvalidDataException("此资源包没有随包预设。");
                if (!MaterialPresets.Options(pack, category).Any(g => g.Id == groupId))
                    throw new InvalidDataException("资源组 " + groupId + " 不是此整合包在 " + category + " 中的可选项。");
                if (MaterialPresets.MainGroup(pack, category, _state.BundleGroupsOf(packId, digest)) == groupId) return _state.Revision;

                var next = Copy(_state);
                next.BundleGroups ??= new Dictionary<string, Dictionary<string, string>>();
                string key = MaterialPackState.BundleKey(packId, digest);
                if (!next.BundleGroups.TryGetValue(key, out var groups) || groups == null)
                    next.BundleGroups[key] = groups = new Dictionary<string, string>(StringComparer.Ordinal);
                // The preset's own main group needs no entry.
                if (groupId == pack.Preset.Categories[category].Groups[0]) groups.Remove(category);
                else groups[category] = groupId;
                if (groups.Count == 0) next.BundleGroups.Remove(key);
                MaterialPackResolver.Validate(next.Installed, next.BundleConfiguration(MaterialPresetChoice.FromPack(packId, digest)), InstalledRoot);
                return Commit(next);
            }
        }

        private long CommitMix(MaterialPackConfiguration configuration)
        {
            var next = Copy(_state);
            next.Apply(configuration);
            next.Mode = MaterialPackModes.MIX;
            next.MixInitialized = true;
            MaterialPackResolver.Validate(next.Installed, next.MixConfiguration(), InstalledRoot);
            return Commit(next);
        }

        /// <summary>What <see cref="Uninstall(string,string,long)"/> would take out, without changing anything.</summary>
        public MaterialUninstallImpact PreviewUninstall(string packId, string digest)
        {
            lock (_gate) return Remove(Copy(_state), packId, digest);
        }

        public long Uninstall(string packId, string digest, long expectedRevision) => Uninstall(packId, digest, expectedRevision, out _);

        /// <summary>
        /// Removes an installed revision and every reference to its groups from 混搭, then re-resolves
        /// what is left against the remaining fallbacks and built-ins. A preset choice from this
        /// revision goes back to built-in set 1.
        /// </summary>
        public long Uninstall(string packId, string digest, long expectedRevision, out MaterialUninstallImpact impact)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                impact = Remove(next, packId, digest);
                MaterialPackResolver.Validate(next.Installed, next.MixConfiguration(), InstalledRoot);
                return Commit(next);
            }
        }

        private static MaterialUninstallImpact Remove(MaterialPackState next, string packId, string digest)
        {
            var pack = next.FindPack(packId, digest) ?? throw new InvalidDataException("找不到已安装的资源包修订。");
            var impact = new MaterialUninstallImpact { Pack = pack };
            var removed = new HashSet<string>(pack.Groups.Select(g => pack.GroupReference(g.Id)), StringComparer.Ordinal);
            foreach (string category in MaterialCategories.All)
            {
                var selection = next.Selection(category);
                if (selection.Groups.RemoveAll(removed.Contains) > 0) impact.Categories.Add(category);
                foreach (var slot in selection.Overrides.Where(x => x.Value != null && removed.Contains(x.Value)).ToArray())
                {
                    selection.Overrides.Remove(slot.Key);
                    impact.Overrides.Add((category, slot.Key));
                }
            }
            next.Installed.Remove(pack);
            next.BundleGroups?.Remove(MaterialPackState.BundleKey(packId, digest));
            if (next.Preset.IsFrom(packId, digest))
            {
                impact.ClearedPreset = pack.Preset?.Name ?? PackLabel(pack);
                next.Preset = MaterialPresetChoice.Builtin(next.Preset.GetBuiltinSets());
            }
            return impact;
        }

        private static string PackLabel(InstalledMaterialPack pack) =>
            string.IsNullOrWhiteSpace(pack.DisplayName) ? pack.PackId : pack.DisplayName;

        /// <summary>What one installed group shows on the preview card; read-only.</summary>
        public MaterialGroupPreview DescribeGroup(string packId, string digest, string groupId)
        {
            lock (_gate)
            {
                var pack = _state.FindPack(packId, digest) ?? throw new InvalidDataException("找不到已安装的资源包修订。");
                var group = pack.Groups.Find(g => g.Id == groupId) ?? throw new InvalidDataException("找不到资源组 " + groupId);
                string root = VersionPath(pack);
                var preview = new MaterialGroupPreview { Pack = Copy(pack), Group = Copy(group) };
                var slots = MaterialCategories.Slots(group.Category).ToArray();
                preview.TotalSlots = slots.Length;
                preview.ProvidedSlots = slots.Count(group.Provides);
                if (group.Category == MaterialCategories.AUDIO)
                {
                    foreach (string slot in slots.Where(group.Slots.ContainsKey))
                    {
                        var media = group.Slots[slot];
                        preview.Items.Add(new MaterialPreviewItem { Slot = slot, Kind = MaterialPreviewKind.Audio, File = media.File,
                            Path = Path.Combine(root, media.File), Media = media });
                    }
                }
                else if (group.Category == MaterialCategories.BACKGROUND)
                    preview.Items.AddRange(MaterialPreviewItem.FromBackground(group.Background, root));
                else
                    foreach (string slot in slots.Where(group.Sprites.ContainsKey))
                        preview.Items.Add(MaterialPreviewItem.FromSprite(slot, group.Sprites[slot], root));
                return preview;
            }
        }

        /// <summary>What a resolved slot shows on the preview card, or null for a built-in source.</summary>
        public static IEnumerable<MaterialPreviewItem> PreviewOf(MaterialSlotState state)
        {
            if (state?.Source == null) yield break;
            if (state.Background != null)
            {
                foreach (var item in MaterialPreviewItem.FromBackground(state.Background, state.PackRoot)) yield return item;
                yield break;
            }
            if (state.Media is MaterialAudioSlot audio)
            {
                yield return new MaterialPreviewItem { Slot = state.Slot, Kind = MaterialPreviewKind.Audio, File = audio.File,
                    Path = state.Path, Media = audio };
                yield break;
            }
            if (state.Media is MaterialSpriteSlot sprite)
            {
                var item = state.IsLayout ? new MaterialPreviewItem { Slot = state.Slot, Kind = MaterialPreviewKind.Layout }
                    : MaterialPreviewItem.FromSprite(state.Slot, sprite, state.PackRoot ?? string.Empty);
                if (item != null) yield return item;
            }
        }

        /// <summary>Full path of a pack's icon PNG, or null when it has none.</summary>
        internal string IconPath(InstalledMaterialPack pack)
        {
            if (pack?.Icon == null) return null;
            lock (_gate) return Path.Combine(VersionPath(pack), pack.Icon.File);
        }

        /// <summary>
        /// Index entries from before pack names, icons and presets were read get them from their
        /// installed <c>pack.json</c>, once, after checking the files still match the digest.
        /// Returns how many were filled; a revision whose files changed stays as it was, and the
        /// page suggests importing it again.
        /// </summary>
        public async Task<int> BackfillMetadataAsync(CancellationToken token = default)
        {
            List<(string packId, string digest, string root)> pending;
            lock (_gate)
            {
                pending = _state.Installed.Where(p => p.MetadataVersion < InstalledMaterialPack.CURRENT_METADATA_VERSION)
                    .Select(p => (p.PackId, p.Digest, VersionPath(p))).ToList();
            }
            if (pending.Count == 0) return 0;
            var found = await Task.Run(() => pending.Select(item =>
            {
                try { return MaterialPackArchive.Reinspect(item.root, item.digest, token); }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    Debug.LogWarning("[MaterialPack] 无法回填 " + item.packId + " 的元数据：" + e.Message);
                    return null;
                }
            }).ToList(), token);
            lock (_gate)
            {
                if (_sessions != 0) return 0;
                var next = Copy(_state);
                int filled = 0;
                for (int i = 0; i < pending.Count; i++)
                {
                    var target = next.FindPack(pending[i].packId, pending[i].digest);
                    var fresh = found[i];
                    if (target == null || fresh == null || target.MetadataVersion >= InstalledMaterialPack.CURRENT_METADATA_VERSION) continue;
                    target.DisplayName = fresh.DisplayName;
                    target.Icon = fresh.Icon;
                    target.Preset = fresh.Preset;
                    target.MetadataVersion = InstalledMaterialPack.CURRENT_METADATA_VERSION;
                    filled++;
                }
                if (filled > 0) Commit(next);
                return filled;
            }
        }

        /// <summary>
        /// The bindings of the configuration a live gets now (<see cref="MaterialPackState.ActiveConfiguration"/>).
        /// Holds the store until <see cref="Release"/>: no write can change what the live reads.
        /// </summary>
        internal MaterialPackBindings Capture()
        {
            lock (_gate)
            {
                var bindings = Bind(_state, _state.ActiveConfiguration());
                _sessions++;
                return bindings;
            }
        }

        /// <summary>
        /// The bindings of any configuration among the installed packs, for the resource pack page's
        /// demo. Does not hold the store: an uninstall meanwhile only makes the files unreadable,
        /// which the reader reports per file.
        /// </summary>
        internal MaterialPackBindings Bind(MaterialPackConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            lock (_gate) return Bind(_state, configuration);
        }

        internal IReadOnlyList<MaterialAudioBinding> Capture(out IReadOnlyList<MaterialSpriteBinding> combo, out MaterialComboLayout layout,
            out IReadOnlyList<MaterialSpriteBinding> judgment, out IReadOnlyList<MaterialSpriteBinding> hitFx,
            out IReadOnlyList<MaterialSpriteBinding> notes, out IReadOnlyList<MaterialSpriteBinding> lane,
            out MaterialBackgroundBinding background, out IReadOnlyList<MaterialSpriteBinding> hud, out IReadOnlyList<MaterialSpriteBinding> resultFx)
        {
            var bindings = Capture();
            combo = bindings.Combo;
            layout = bindings.ComboLayout;
            judgment = bindings.Judgment;
            hitFx = bindings.HitFx;
            notes = bindings.Notes;
            lane = bindings.Lane;
            background = bindings.Background;
            hud = bindings.Hud;
            resultFx = bindings.ResultFx;
            return bindings.Audio;
        }

        private MaterialPackBindings Bind(MaterialPackState state, MaterialPackConfiguration configuration)
        {
            var installed = state.Installed;
            string root = InstalledRoot;
            static bool Empty(MaterialGroupSelection selection) => selection.Groups.Count == 0 && selection.Overrides.Count == 0;
            var bindings = new MaterialPackBindings { Builtin = (configuration.Builtin ?? new MaterialBuiltinSelection()).Copy() };
            if (!Empty(configuration.Audio)) bindings.Audio = MaterialPackResolver.Resolve(installed, configuration.Audio, root);
            if (!Empty(configuration.Combo)) bindings.Combo = MaterialPackResolver.ResolveCombo(installed, configuration.Combo, root);
            if (!Empty(configuration.Judgment)) bindings.Judgment = MaterialPackResolver.ResolveJudgment(installed, configuration.Judgment, root);
            if (!Empty(configuration.HitFx)) bindings.HitFx = MaterialPackResolver.ResolveHitFx(installed, configuration.HitFx, root);
            if (!Empty(configuration.Notes)) bindings.Notes = MaterialPackResolver.ResolveNotes(installed, configuration.Notes, root);
            if (!Empty(configuration.ResultFx)) bindings.ResultFx = MaterialPackResolver.ResolveResultFx(installed, configuration.ResultFx, root);
            if (!Empty(configuration.Hud)) bindings.Hud = MaterialPackResolver.ResolveHud(installed, configuration.Hud, root);
            if (!Empty(configuration.Lane)) bindings.Lane = MaterialPackResolver.ResolveLane(installed, configuration.Lane, root);
            if (!Empty(configuration.Background)) bindings.Background = MaterialPackResolver.ResolveBackground(installed, configuration.Background, root);
            if (configuration.Combo.Groups.Count > 0)
            {
                var layouts = installed.SelectMany(p => p.Groups.Where(g => g.Layout != null)
                    .Select(g => (id: p.GroupReference(g.Id), g.Layout))).ToDictionary(x => x.id, x => x.Layout);
                foreach (string id in configuration.Combo.Groups)
                    if (layouts.TryGetValue(id, out bindings.ComboLayout)) break;
            }
            return bindings;
        }

        /// <summary>The built-in sets the next live merges into its settings snapshot.</summary>
        internal MaterialBuiltinSelection Builtin
        {
            get { lock (_gate) return (_state.ActiveConfiguration().Builtin ?? new MaterialBuiltinSelection()).Copy(); }
        }

        internal void Release() { lock (_gate) _sessions--; }

        private string InstalledRoot => Path.Combine(_root, "installed");
        private string VersionPath(InstalledMaterialPack pack) => MaterialPackResolver.VersionPath(InstalledRoot, pack);

        private List<MaterialAudioBinding> Resolve(MaterialPackState state, MaterialAudioSelection selection) =>
            MaterialPackResolver.Resolve(state.Installed, selection, InstalledRoot);

        private void CheckWritable(long? expectedRevision = null)
        {
            if (_sessions != 0) throw new MaterialPackBusyException();
            if (expectedRevision.HasValue && expectedRevision != _state.Revision)
                throw new MaterialPackConflictException(_state.Revision);
        }

        private long Commit(MaterialPackState next)
        {
            next.Revision++;
            Directory.CreateDirectory(_root);
            // Keep both generations' media until the new index and its backup are durable.
            AtomicFile.WriteAllText(StatePath + ".bak", JsonConvert.SerializeObject(_state));
            AtomicFile.WriteAllText(StatePath, JsonConvert.SerializeObject(next));
            var previous = _state;
            _state = next;
            CollectOrphans(previous, next);
            return next.Revision;
        }

        private void CollectOrphans(MaterialPackState previous, MaterialPackState next)
        {
            string installed = Path.Combine(_root, "installed");
            if (!Directory.Exists(installed)) return;
            var keep = new HashSet<string>(previous.Installed.Concat(next.Installed).Select(VersionPath));
            try
            {
                foreach (string pack in Directory.GetDirectories(installed))
                    foreach (string version in Directory.GetDirectories(pack))
                        if (!keep.Contains(version)) Directory.Delete(version, true);
            }
            catch (IOException) { /* A later commit will retry collection. */ }
            catch (UnauthorizedAccessException) { }
        }

        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }
}
