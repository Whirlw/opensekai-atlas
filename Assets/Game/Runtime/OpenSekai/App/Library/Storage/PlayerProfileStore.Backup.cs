using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.App.Library
{
    public sealed class ProfileImportPreview
    {
        public string Id { get; internal set; }
        public int Added { get; internal set; }
        public int Duplicates { get; internal set; }
        public IReadOnlyList<string> ConflictingIds { get; internal set; }
        public IReadOnlyList<int> MissingIdLines { get; internal set; }
        public bool RestoresAppearance { get; internal set; }
        public PlayerProfileSnapshot Result { get; internal set; }
    }

    public sealed partial class PlayerProfileStore
    {
        private ProfileImportPreview _preview;
        private RankingHistory _preparedHistory;
        private PlayHistorySummaryCache _preparedSummaries;
        private long _previewHistoryLength;
        private bool _busy;

        private void RequireIdle()
        {
            if (_busy) throw new InvalidOperationException("A backup operation is in progress.");
        }

        public void ExportBackup(string destination)
        {
            RequireIdle();
            ProfileBackupArchive.Write(destination, HistoryPath, ReadAppearance());
        }

        public Task ExportBackupAsync(string destination, CancellationToken cancel = default)
        {
            RequireIdle();
            string history = HistoryPath;
            var appearance = ReadAppearance();
            return RunBackupAsync(() =>
            {
                ProfileBackupArchive.Write(destination, history, appearance, cancel);
                return true;
            }, cancel);
        }

        public Task<ProfileImportPreview> PreviewBackupAsync(string zipPath, CancellationToken cancel = default) =>
            RunBackupAsync(() => Prepare(zipPath, cancel), cancel);

        private async Task<T> RunBackupAsync<T>(Func<T> operation, CancellationToken cancel)
        {
            RequireIdle();
            _busy = true;
            try { return await Task.Run(operation, cancel); }
            finally { _busy = false; }
        }

        public ProfileImportPreview PreviewBackup(string zipPath)
        {
            RequireIdle();
            return Prepare(zipPath, default);
        }

        private ProfileImportPreview Prepare(string zipPath, CancellationToken cancel)
        {
            if (SongPackageStorage.ActivePlayerDirectory(_root) != _directory)
                throw new InvalidOperationException("The active archive changed.");
            CancelPendingPreview();
            // Any unselected generation from an interrupted preview/commit is disposable.
            if (Directory.Exists(Archives))
                foreach (string previous in Directory.GetDirectories(Archives))
                    if (previous != _directory && Guid.TryParseExact(Path.GetFileName(previous), "N", out _))
                        DiscardDirectory(previous);

            string id = Guid.NewGuid().ToString("N");
            string stage = Path.Combine(Archives, id);
            Directory.CreateDirectory(stage);
            try
            {
                string incoming = Path.Combine(stage, "incoming.jsonl");
                var appearance = ProfileBackupArchive.Unpack(zipPath, incoming);
                string merged = Path.Combine(stage, ProfileBackupArchive.HISTORY);
                _previewHistoryLength = HistoryLength();
                var preview = Merge(incoming, merged, cancel);
                File.Delete(incoming);
                if (!preview.RestoresAppearance) appearance = ReadAppearance();
                File.WriteAllText(Path.Combine(stage, PROFILE_FILE), JsonConvert.SerializeObject(appearance));
                var summaries = new PlayHistorySummaryCache();
                summaries.Reload(merged);
                var history = new RankingHistory(merged, summaries);
                preview.Result = new PlayerProfileSnapshot(appearance, history.Read());
                cancel.ThrowIfCancellationRequested();
                preview.Id = id;
                _preparedHistory = history;
                _preparedSummaries = summaries;
                _preview = preview;
                return preview;
            }
            catch
            {
                DiscardDirectory(stage);
                throw;
            }
        }

        public PlayerProfileSnapshot ConfirmImport(string previewId)
        {
            RequirePreview(previewId);
            if (HistoryLength() != _previewHistoryLength ||
                SongPackageStorage.ActivePlayerDirectory(_root) != _directory)
            {
                CancelPendingPreview();
                throw new InvalidOperationException("Local player data changed. Preview the backup again.");
            }
            var result = _preview.Result;
            if (_preview.Added == 0 && !_preview.RestoresAppearance)
            {
                CancelPendingPreview();
                return result;
            }
            string previous = _directory;
            string next = Path.Combine(Archives, previewId);
            History.TransferPendingTo(_preparedHistory);
            // All data and indexes are ready before this single same-volume rename.
            AtomicFile.WriteAllText(Path.Combine(Archives, "active"), previewId);
            _directory = next;
            History = _preparedHistory;
            Summaries = _preparedSummaries;
            ForgetPreview();
            if (previous != _root) DiscardDirectory(previous);
            return result;
        }

        public void CancelImport(string previewId)
        {
            RequirePreview(previewId);
            CancelPendingPreview();
        }

        private void RequirePreview(string id)
        {
            RequireIdle();
            if (_preview == null || _preview.Id != id) throw new InvalidOperationException("Unknown backup preview.");
        }

        private void CancelPendingPreview()
        {
            if (_preview == null) return;
            DiscardDirectory(Path.Combine(Archives, _preview.Id));
            ForgetPreview();
        }

        private void ForgetPreview()
        {
            _preview = null;
            _preparedHistory = null;
            _preparedSummaries = null;
        }

        private static void DiscardDirectory(string directory)
        {
            try { Directory.Delete(directory, true); }
            catch (DirectoryNotFoundException) { }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Profile] Could not clean temporary archive: " + e.Message);
            }
        }

        private long HistoryLength() => File.Exists(HistoryPath) ? new FileInfo(HistoryPath).Length : 0;

        private ProfileImportPreview Merge(string incoming, string merged, CancellationToken cancel)
        {
            var known = new Dictionary<string, string>(StringComparer.Ordinal);
            var missing = new List<int>();
            var conflicts = new List<string>();
            var preview = new ProfileImportPreview();
            bool hasLocalPlays = false;
            using var writer = new StreamWriter(merged, false, ProfileBackupArchive.Utf8);
            if (File.Exists(HistoryPath))
                foreach (string line in ProfileBackupArchive.LocalLines(HistoryPath))
                {
                    cancel.ThrowIfCancellationRequested();
                    // Preserve legacy, unknown fields and even a torn local line verbatim.
                    writer.WriteLine(line);
                    var row = ProfileBackupArchive.TryLocalRow(line);
                    if (row == null) continue;
                    hasLocalPlays = true;
                    string id = (string)row["recordId"];
                    if (!string.IsNullOrWhiteSpace(id)) known.TryAdd(id, line);
                }
            preview.RestoresAppearance = !hasLocalPlays && !File.Exists(ProfilePath);
            int lineNumber = 0;
            foreach (string line in File.ReadLines(incoming, ProfileBackupArchive.Utf8))
            {
                cancel.ThrowIfCancellationRequested();
                lineNumber++;
                var row = ProfileBackupArchive.Parse(line);
                var record = row.ToObject<CustomMusicScorePlayHistoryRecord>();
                ValidateRecord(row, record, lineNumber);
                if (string.IsNullOrWhiteSpace(record.RecordId)) { missing.Add(lineNumber); continue; }
                if (known.TryGetValue(record.RecordId, out string previous))
                {
                    if (JToken.DeepEquals(ProfileBackupArchive.Parse(previous), row)) preview.Duplicates++;
                    else conflicts.Add(record.RecordId);
                    continue;
                }
                writer.WriteLine(line);
                known.Add(record.RecordId, line);
                preview.Added++;
            }
            preview.ConflictingIds = conflicts.AsReadOnly();
            preview.MissingIdLines = missing.AsReadOnly();
            return preview;
        }

        private static void ValidateRecord(JObject row, CustomMusicScorePlayHistoryRecord record, int line)
        {
            if (row["schemaVersion"]?.Type != JTokenType.Integer || record.SchemaVersion < 1 || record.SchemaVersion > 3 ||
                row["recordId"] != null && row["recordId"].Type != JTokenType.Null && row["recordId"].Type != JTokenType.String)
                throw new InvalidDataException("Invalid history schema at line " + line);
            if (record.SchemaVersion != 3) return;
            if (!record.EndReason.HasValue || !Enum.IsDefined(typeof(PlayResultKind), record.EndReason.Value) ||
                record.EndReason == PlayResultKind.LoadError || record.MaximumScore <= 0 ||
                record.FormulaVersion != RankingRules.FormulaVersion || string.IsNullOrWhiteSpace(record.PlayedAtUtc))
                throw new InvalidDataException("Invalid play at line " + line);
            foreach (string field in _judgementFields)
                if (row[field]?.Type != JTokenType.Integer || (long)row[field] < 0)
                    throw new InvalidDataException("Invalid " + field + " at line " + line);
        }

        private static readonly string[] _judgementFields =
            { "perfectCount", "greatCount", "goodCount", "badCount", "missCount", "totalScore", "totalComboCount" };
    }
}
