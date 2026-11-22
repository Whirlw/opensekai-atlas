using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.App.Library
{
    public sealed partial class PlayerProfileStore
    {
        // Within an archive the journal is append-only. Any rewrite selects a fresh archive.
        private const string BACKUP_MARKER = "history-backup.bytes";

        public bool HasCurrentBackup
        {
            get
            {
                string marker = Path.Combine(_directory, BACKUP_MARKER);
                return File.Exists(marker) && File.ReadAllText(marker) == HistoryLength().ToString(CultureInfo.InvariantCulture);
            }
        }

        internal void MarkBackupSaved(string directory, long bytes)
        {
            if (_directory == directory && HistoryLength() == bytes && SongPackageStorage.ActivePlayerDirectory(_root) == directory)
                AtomicFile.WriteAllText(Path.Combine(directory, BACKUP_MARKER), bytes.ToString(CultureInfo.InvariantCulture));
        }

        public void DeleteHistory(CustomMusicScorePlayHistoryRecord play)
        {
            if (play == null) throw new ArgumentNullException(nameof(play));
            ReplaceHistory(play);
        }

        public void DeleteAllHistory() => ReplaceHistory(null);

        private void ReplaceHistory(CustomMusicScorePlayHistoryRecord play)
        {
            RequireIdle();
            if (SongPackageStorage.ActivePlayerDirectory(_root) != _directory)
                throw new InvalidOperationException("The active archive changed.");
            bool backedUp = HasCurrentBackup;
            if (play == null && !backedUp) throw new InvalidOperationException("Export the current history first.");
            if (play != null && !History.Contains(play)) throw new InvalidOperationException("The selected run changed.");

            CancelPendingPreview();
            string id = Guid.NewGuid().ToString("N");
            string stage = Path.Combine(Archives, id);
            Directory.CreateDirectory(stage);
            try
            {
                string path = Path.Combine(stage, SongPackageStorage.PLAY_HISTORY_FILE_NAME);
                using (var writer = new StreamWriter(path, false, ProfileBackupArchive.Utf8))
                {
                    if (play != null) CopyExcept(play, writer);
                }
                if (File.Exists(DanPath)) File.Copy(DanPath, Path.Combine(stage, DAN_FILE));
                if (File.Exists(ProfilePath)) File.Copy(ProfilePath, Path.Combine(stage, PROFILE_FILE));
                var (history, summaries) = IndexHistory(path);
                if (play != null) History.TransferPendingTo(history, play.RecordId);
                if (play != null && backedUp)
                    File.WriteAllText(Path.Combine(stage, BACKUP_MARKER), new FileInfo(path).Length.ToString(CultureInfo.InvariantCulture));
                ActivateHistory(id, history, summaries);
            }
            catch
            {
                if (_directory != stage) DiscardDirectory(stage);
                throw;
            }
        }

        private void CopyExcept(CustomMusicScorePlayHistoryRecord play, StreamWriter writer)
        {
            int lineNumber = 0;
            bool removed = false;
            foreach (string line in ProfileBackupArchive.LocalLines(HistoryPath))
            {
                lineNumber++;
                bool matches = lineNumber == play.SourceLine;
                if (!string.IsNullOrWhiteSpace(play.RecordId))
                {
                    // Duplicate copies of one ID represent a single timeline entry.
                    try { matches = JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(line)?.RecordId == play.RecordId; }
                    catch (JsonException) { matches = false; }
                }
                if (matches) removed = true;
                else writer.WriteLine(line);
            }
            if (!removed) throw new InvalidOperationException("The selected run no longer exists.");
        }

        private static (RankingHistory, PlayHistorySummaryCache) IndexHistory(string path)
        {
            var summaries = new PlayHistorySummaryCache();
            summaries.Reload(path);
            var history = new RankingHistory(path, summaries);
            history.Read();
            return (history, summaries);
        }

        private void ActivateHistory(string id, RankingHistory history, PlayHistorySummaryCache summaries)
        {
            string previous = _directory;
            string next = Path.Combine(Archives, id);
            AtomicFile.WriteAllText(Path.Combine(Archives, "active"), id);
            _directory = next;
            _cachedDan = null;
            History = history;
            History.DanSource = ReadDanArchive;
            Summaries = summaries;
            if (previous != _root) DiscardDirectory(previous);
            else
            {
                // The initial archive shares its directory with installed songs.
                string original = Path.Combine(_root, SongPackageStorage.PLAY_HISTORY_FILE_NAME);
                try
                {
                    File.Delete(original);
                    File.Delete(PlayHistorySummaryCache.SnapshotPathOf(original));
                    File.Delete(Path.Combine(_root, BACKUP_MARKER));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning("[Profile] Could not clean retired history: " + e.Message);
                }
            }
        }
    }
}
