using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenSekai.MaterialPacks;
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
    }

    public sealed class MaterialPackStore
    {
        private static MaterialPackStore _shared;
        public static MaterialPackStore Shared => _shared ??= new MaterialPackStore(Path.Combine(Application.persistentDataPath, "material-packs"));
        private readonly string _root;
        private readonly object _gate = new();
        private MaterialPackState _state;
        private int _sessions;
        private string StatePath => Path.Combine(_root, "state.json");

        internal MaterialPackStore(string root)
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
            // Only the process-owned store opens this root. An interrupted import never became selectable.
            string staging = Path.Combine(_root, "staging");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            CollectOrphans(backup ?? _state, _state);
        }

        private MaterialPackState ReadState(string path)
        {
            if (!File.Exists(path)) return null;
            var state = JsonConvert.DeserializeObject<MaterialPackState>(File.ReadAllText(path));
            if (state?.Installed == null || state.Revision < 0 || state.Audio?.Groups == null || state.Audio.Overrides == null)
                throw new InvalidDataException("资源包索引无效。");
            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pack in state.Installed)
            {
                if (pack?.Groups == null || !versions.Add(VersionPath(pack)))
                    throw new InvalidDataException("资源包修订索引无效。");
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
            return state;
        }

        public MaterialPackState List()
        {
            lock (_gate) return Copy(_state);
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

        public long SaveSelection(MaterialAudioSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Audio = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                Resolve(next, next.Audio);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveCombo(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveCombo(_state.Installed, selection, InstalledRoot);
        }

        public long SaveComboSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Combo = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveCombo(next.Installed, next.Combo, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveJudgment(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveJudgment(_state.Installed, selection, InstalledRoot);
        }

        public long SaveJudgmentSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Judgment = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveJudgment(next.Installed, next.Judgment, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveHitFx(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveHitFx(_state.Installed, selection, InstalledRoot);
        }

        public long SaveHitFxSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.HitFx = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveHitFx(next.Installed, next.HitFx, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveNotes(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveNotes(_state.Installed, selection, InstalledRoot);
        }

        public long SaveNotesSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Notes = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveNotes(next.Installed, next.Notes, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveLane(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveLane(_state.Installed, selection, InstalledRoot);
        }

        public long SaveLaneSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Lane = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveLane(next.Installed, next.Lane, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveHud(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveHud(_state.Installed, selection, InstalledRoot);
        }

        public long SaveHudSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Hud = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveHud(next.Installed, next.Hud, InstalledRoot);
                return Commit(next);
            }
        }

        public IReadOnlyList<MaterialSpriteBinding> ResolveResultFx(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveResultFx(_state.Installed, selection, InstalledRoot);
        }

        public long SaveResultFxSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.ResultFx = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveResultFx(next.Installed, next.ResultFx, InstalledRoot);
                return Commit(next);
            }
        }

        public MaterialBackgroundBinding ResolveBackground(MaterialGroupSelection selection)
        {
            lock (_gate) return MaterialPackResolver.ResolveBackground(_state.Installed, selection, InstalledRoot);
        }

        public long SaveBackgroundSelection(MaterialGroupSelection selection, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                next.Background = Copy(selection ?? throw new ArgumentNullException(nameof(selection)));
                MaterialPackResolver.ResolveBackground(next.Installed, next.Background, InstalledRoot);
                return Commit(next);
            }
        }

        public long Uninstall(string packId, string digest, long expectedRevision)
        {
            lock (_gate)
            {
                CheckWritable(expectedRevision);
                var next = Copy(_state);
                var pack = next.Installed.Find(p => p.PackId == packId && p.Digest == digest)
                    ?? throw new InvalidDataException("找不到已安装的资源包修订。");
                var removed = new HashSet<string>(pack.Groups.Select(g => pack.GroupReference(g.Id)));
                foreach (var selection in new MaterialGroupSelection[] { next.Audio, next.Combo, next.Judgment, next.HitFx, next.Notes, next.Lane, next.Hud, next.ResultFx, next.Background })
                {
                    selection.Groups.RemoveAll(removed.Contains);
                    foreach (var slot in selection.Overrides.Where(x => removed.Contains(x.Value)).ToArray())
                        selection.Overrides.Remove(slot.Key);
                }
                next.Installed.Remove(pack);
                MaterialPackResolver.ResolveHud(next.Installed, next.Hud, InstalledRoot);
                MaterialPackResolver.ResolveResultFx(next.Installed, next.ResultFx, InstalledRoot);
                Resolve(next, next.Audio);
                MaterialPackResolver.ResolveCombo(next.Installed, next.Combo, InstalledRoot);
                MaterialPackResolver.ResolveBackground(next.Installed, next.Background, InstalledRoot);
                MaterialPackResolver.ResolveLane(next.Installed, next.Lane, InstalledRoot);
                MaterialPackResolver.ResolveNotes(next.Installed, next.Notes, InstalledRoot);
                MaterialPackResolver.ResolveJudgment(next.Installed, next.Judgment, InstalledRoot);
                MaterialPackResolver.ResolveHitFx(next.Installed, next.HitFx, InstalledRoot);
                return Commit(next);
            }
        }

        internal IReadOnlyList<MaterialAudioBinding> Capture(out IReadOnlyList<MaterialSpriteBinding> combo, out MaterialComboLayout layout,
            out IReadOnlyList<MaterialSpriteBinding> judgment, out IReadOnlyList<MaterialSpriteBinding> hitFx,
            out IReadOnlyList<MaterialSpriteBinding> notes, out IReadOnlyList<MaterialSpriteBinding> lane,
            out MaterialBackgroundBinding background, out IReadOnlyList<MaterialSpriteBinding> hud, out IReadOnlyList<MaterialSpriteBinding> resultFx)
        {
            lock (_gate)
            {
                IReadOnlyList<MaterialAudioBinding> bindings = _state.Audio.Groups.Count == 0 && _state.Audio.Overrides.Count == 0
                    ? Array.Empty<MaterialAudioBinding>() : Resolve(_state, _state.Audio);
                combo = _state.Combo.Groups.Count == 0 && _state.Combo.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveCombo(_state.Installed, _state.Combo, InstalledRoot);
                judgment = _state.Judgment.Groups.Count == 0 && _state.Judgment.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveJudgment(_state.Installed, _state.Judgment, InstalledRoot);
                hitFx = _state.HitFx.Groups.Count == 0 && _state.HitFx.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveHitFx(_state.Installed, _state.HitFx, InstalledRoot);
                notes = _state.Notes.Groups.Count == 0 && _state.Notes.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveNotes(_state.Installed, _state.Notes, InstalledRoot);
                resultFx = _state.ResultFx.Groups.Count == 0 && _state.ResultFx.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveResultFx(_state.Installed, _state.ResultFx, InstalledRoot);
                hud = _state.Hud.Groups.Count == 0 && _state.Hud.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveHud(_state.Installed, _state.Hud, InstalledRoot);
                lane = _state.Lane.Groups.Count == 0 && _state.Lane.Overrides.Count == 0
                    ? Array.Empty<MaterialSpriteBinding>() : MaterialPackResolver.ResolveLane(_state.Installed, _state.Lane, InstalledRoot);
                background = _state.Background.Groups.Count == 0 && _state.Background.Overrides.Count == 0
                    ? null : MaterialPackResolver.ResolveBackground(_state.Installed, _state.Background, InstalledRoot);
                layout = null;
                if (_state.Combo.Groups.Count > 0)
                {
                    var layouts = _state.Installed.SelectMany(p => p.Groups.Where(g => g.Layout != null)
                        .Select(g => (id: p.GroupReference(g.Id), g.Layout))).ToDictionary(x => x.id, x => x.Layout);
                    foreach (string id in _state.Combo.Groups)
                        if (layouts.TryGetValue(id, out layout)) break;
                }
                _sessions++;
                return bindings;
            }
        }

        internal void Release() { lock (_gate) _sessions--; }

        private string InstalledRoot => Path.Combine(_root, "installed");
        private string VersionPath(InstalledMaterialPack pack) => MaterialPackResolver.VersionPath(InstalledRoot, pack);

        private List<MaterialAudioBinding> Resolve(MaterialPackState state, MaterialAudioSelection selection) =>
            MaterialPackResolver.Resolve(state.Installed, selection, InstalledRoot);

        private void CheckWritable(long? expectedRevision = null)
        {
            if (_sessions != 0) throw new InvalidOperationException("本局进行中。");
            if (expectedRevision.HasValue && expectedRevision != _state.Revision)
                throw new InvalidOperationException("资源包选择已改变，当前修订 " + _state.Revision);
        }

        private long Commit(MaterialPackState next)
        {
            next.Revision++;
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
