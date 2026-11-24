using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Storage;

namespace OpenSekai.App.Library
{
    internal sealed class DanArchive
    {
        [JsonProperty(Required = Required.Always)]
        public List<string> Definitions { get; set; } = new();
        [JsonProperty(Required = Required.Always)]
        public List<DanAttempt> Attempts { get; set; } = new();

        /// <summary>The key of the medal worn in each slot (<see cref="Medal.Key"/>), null for an empty one.</summary>
        public string[] Worn { get; set; } = new string[Medal.SLOTS];

        // Archives from before medals wore one 段位, by its id.
        [JsonProperty]
        private string Equipped { set => Worn[0] ??= value == null ? null : Medal.DanKey(value); }

        private Dictionary<string, DanDefinition> _decoded;
        internal Dictionary<string, DanDefinition> DecodeDefinitions() => _decoded ??=
            Definitions.Select(DanDefinition.Parse).ToDictionary(d => d.Id, StringComparer.Ordinal);

        private string[] _earned;
        internal IReadOnlyList<string> Earned() => _earned ??= Attempts.Where(a => a.Outcome == DanOutcome.Passed)
            .Select(a => a.DefinitionId).Distinct(StringComparer.Ordinal).ToArray();

        internal DanArchive Copy() => new()
        {
            Definitions = new List<string>(Definitions),
            Attempts = new List<DanAttempt>(Attempts),
            Worn = (string[])Worn.Clone()
        };

        /// <summary>Empties the slot that wears <paramref name="key"/>; false when none does.</summary>
        internal bool TakeOff(string key)
        {
            int slot = Array.IndexOf(Worn, key);
            if (slot < 0) return false;
            Worn[slot] = null;
            return true;
        }

        internal void InvalidateDerived()
        {
            _decoded = null;
            _earned = null;
        }

        internal static DanArchive Decode(string json)
        {
            var archive = ProfileBackupArchive.Parse(json).ToObject<DanArchive>() ?? throw new InvalidDataException("Missing challenge archive.");
            var definitions = archive.DecodeDefinitions();
            archive.Definitions = definitions.Values.Select(d => d.ShareText).ToList();
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attempt in archive.Attempts)
            {
                if (attempt == null || !known.Add(attempt.Id) || !definitions.TryGetValue(attempt.DefinitionId, out var definition))
                    throw new InvalidDataException("Invalid challenge history reference.");
                attempt.Validate(definition);
            }
            if (archive.Worn?.Length != Medal.SLOTS || archive.Worn[0] != null && archive.Worn[0] == archive.Worn[1])
                throw new InvalidDataException("Invalid worn medals.");
            // Achievements follow from the play history, which is not at hand here; a lost one just does not show.
            if (archive.Worn.Select(Medal.DanIdOf).Any(id => id != null && !archive.Earned().Contains(id)))
                throw new InvalidDataException("Worn challenge has not been passed.");
            return archive;
        }

        internal void Merge(DanArchive incoming, ProfileImportPreview preview)
        {
            var definitions = DecodeDefinitions();
            foreach (var pair in incoming.DecodeDefinitions())
            {
                if (definitions.TryAdd(pair.Key, pair.Value)) { Definitions.Add(pair.Value.ShareText); preview.DanDefinitionsAdded++; }
                else preview.DanDuplicates++;
            }
            var attempts = Attempts.ToDictionary(a => a.Id);
            var conflicts = new List<string>();
            foreach (var attempt in incoming.Attempts)
            {
                if (attempts.TryAdd(attempt.Id, attempt)) { Attempts.Add(attempt); preview.DanAttemptsAdded++; }
                else if (JsonConvert.SerializeObject(attempts[attempt.Id]) == JsonConvert.SerializeObject(attempt))
                    preview.DanDuplicates++;
                else conflicts.Add(attempt.Id);
            }
            preview.DanConflictingIds = conflicts.AsReadOnly();
            _earned = null;
            if (preview.RestoresAppearance) Worn = (string[])incoming.Worn.Clone();
        }
    }

    public sealed partial class PlayerProfileStore
    {
        internal const string DAN_FILE = "dan.json";
        private string DanPath => Path.Combine(_directory, DAN_FILE);
        private DanArchive _cachedDan;
        private DanArchive ReadDanArchive() => _cachedDan ??= LoadDanArchive();
        private DanArchive LoadDanArchive() => File.Exists(DanPath) ? DanArchive.Decode(File.ReadAllText(DanPath)) : new DanArchive();

        public IReadOnlyList<DanDefinition> ReadDanDefinitions() =>
            Array.AsReadOnly(ReadDanArchive().DecodeDefinitions().Values.ToArray());

        public IReadOnlyList<DanAttempt> ReadDanHistory() => Array.AsReadOnly(ReadDanArchive().Attempts
            .OrderByDescending(a => a.StartedUtc).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray());

        public void SaveDanDefinition(DanDefinition definition)
        {
            RequireIdle();
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var archive = ReadDanArchive().Copy();
            if (archive.Definitions.Contains(definition.ShareText)) return;
            archive.Definitions.Add(definition.ShareText);
            WriteDan(archive);
        }

        internal string ActiveDanId { get; set; }

        public void DeleteDanDefinition(string id)
        {
            RequireIdle();
            if (_preview != null) throw new InvalidOperationException("A backup preview is open.");
            if (id == null) return;
            if (id == ActiveDanId) throw new InvalidOperationException("The challenge is running.");
            var current = ReadDanArchive();
            if (!current.DecodeDefinitions().TryGetValue(id, out var definition)) return;
            var next = current.Copy();
            next.Definitions.Remove(definition.ShareText);
            next.Attempts.RemoveAll(a => a.DefinitionId == id);
            next.TakeOff(Medal.DanKey(id));
            WriteDan(next);
            History.InvalidateAchievements();
            ForgetUnreachedClaims();
        }

        internal bool SaveDanAttempt(DanDefinition definition, DanAttempt attempt) =>
            SaveDanAttempt(definition, attempt, out _);

        internal bool SaveDanAttempt(DanDefinition definition, DanAttempt attempt, out IReadOnlyList<string> reached)
        {
            RequireIdle();
            attempt.Validate(definition);
            var archive = ReadDanArchive().Copy();
            bool firstAward = attempt.Outcome == DanOutcome.Passed && !archive.Earned().Contains(definition.Id);
            if (!archive.Definitions.Contains(definition.ShareText)) archive.Definitions.Add(definition.ShareText);
            int index = archive.Attempts.FindIndex(a => a.Id == attempt.Id);
            var previous = index >= 0 ? archive.Attempts[index] : null;
            if (index < 0) archive.Attempts.Add(attempt);
            else archive.Attempts[index] = attempt;
            var before = ReadAchievements();
            WriteDan(archive);
            // Append() retains the immutable previous song objects. Replacements (including old
            // checkpoints) can remove contributions and require replay from the committed archive.
            int prefix = previous?.Songs.Count ?? 0;
            if (previous != null && (previous.StartedUtc != attempt.StartedUtc ||
                previous.DefinitionId != attempt.DefinitionId || !attempt.Songs.Take(prefix).SequenceEqual(previous.Songs)))
                History.InvalidateAchievements();
            else
                for (int i = prefix; i < attempt.Songs.Count; i++)
                    History.AppendAchievement(AchievementEvent.FromSong(attempt, i));
            reached = AchievementIndex.NewlyReached(before, ReadAchievements());
            return firstAward;
        }

        private void WriteDan(DanArchive archive)
        {
            AtomicFile.WriteAllText(DanPath, JsonConvert.SerializeObject(archive));
            // Mutations can change both the definitions and the derived list of earned badges.
            archive.InvalidateDerived();
            _cachedDan = archive;
            CancelPendingPreview();
        }
    }
}
