using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class PlayerProfileStoreTests
    {
        private string _root, _sourceRoot, _targetRoot, _backup;
        private PlayerProfileStore _source, _target;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "OpenSekaiProfile-" + Guid.NewGuid().ToString("N"));
            _sourceRoot = Path.Combine(_root, "source");
            _targetRoot = Path.Combine(_root, "target");
            _backup = Path.Combine(_root, "player.zip");
            _source = new PlayerProfileStore(_sourceRoot);
            _target = new PlayerProfileStore(_targetRoot);
            _source.SetAppearance("初音", "miku");
            Save(_source, "a", "A");
            Save(_source, "b", "B");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void RestoreReopenAppendAndRepeatUseSameRankingAndHistory()
        {
            _source.ExportBackup(_backup);
            var preview = _target.PreviewBackup(_backup);
            Assert.That(preview.Added, Is.EqualTo(2));
            Assert.That(_target.Read().Best30.Total, Is.Zero);
            File.Delete(_backup);
            var restored = _target.ConfirmImport(preview.Id);
            Assert.That(restored.Appearance.Nickname, Is.EqualTo("初音"));
            Assert.That(restored.Best30.Total, Is.EqualTo(restored.Best30.Entries.Sum(entry => entry.Points)));
            Assert.That(restored.Best30.Total, Is.EqualTo(600));
            _target = new PlayerProfileStore(_targetRoot);
            Assert.That(_target.Read().Appearance.AvatarId, Is.EqualTo("miku"));
            Save(_target, "c", "C");
            _source.ExportBackup(_backup);
            var repeat = _target.PreviewBackup(_backup);
            Assert.That(repeat.Duplicates, Is.EqualTo(2));
            Assert.That(repeat.Added, Is.Zero);
            Assert.That(_target.ConfirmImport(repeat.Id).Best30.Total, Is.EqualTo(900));
            var page = _target.ReadHistory(1, 1);
            Assert.That(page.TotalCount, Is.EqualTo(3));
            Assert.That(page.Records.Single().RecordId, Is.EqualTo("b"));
            Assert.That(_target.ReadHistory(int.MaxValue, 1).Records, Is.Empty);
        }

        [Test]
        public void ConflictsKeepLocalAndMissingLegacyIdsAreReported()
        {
            File.AppendAllText(Path.Combine(_sourceRoot, "PlayHistory.jsonl"),
                "{\"schemaVersion\":1,\"title\":\"no id\"}\n{\"schemaVersion\":2,\"recordId\":\"legacy\"}\n");
            _source.ExportBackup(_backup);
            Save(_target, "a", "local");
            _target.SetAppearance("Local", "default");
            var preview = _target.PreviewBackup(_backup);
            Assert.That(preview.ConflictingIds, Is.EqualTo(new[] { "a" }));
            Assert.That(preview.MissingIdLines, Is.EqualTo(new[] { 3 }));
            Assert.That(preview.Added, Is.EqualTo(2));
            Assert.That(_target.ConfirmImport(preview.Id).Appearance.Nickname, Is.EqualTo("Local"));
            var history = _target.ReadHistory(0, 10);
            Assert.That(history.TotalCount, Is.EqualTo(3));
            Assert.That(history.Records.Single(p => p.RecordId == "a").ScoreKey, Is.EqualTo("local"));
            Assert.That(_target.Read().Best30.Total, Is.EqualTo(600));
            string path = Path.Combine(SongPackageStorage.ActivePlayerDirectory(_targetRoot), "PlayHistory.jsonl");
            Assert.That(_target.Summaries.Load(path)["local"].PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelInterruptedPreviewAndConcurrentPlayKeepLiveArchive()
        {
            _source.ExportBackup(_backup);
            var initial = _target.PreviewBackup(_backup);
            _target.CancelImport(initial.Id);
            Assert.Throws<InvalidOperationException>(() => _target.ConfirmImport(initial.Id));
            var stale = _target.PreviewBackup(_backup);
            Save(_target, "c", "C");
            Assert.Throws<InvalidOperationException>(() => _target.ConfirmImport(stale.Id));
            var abandoned = _target.PreviewBackup(_backup);
            var restarted = new PlayerProfileStore(_targetRoot);
            Assert.That(restarted.ReadHistory(0, 10).TotalCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => restarted.ConfirmImport(abandoned.Id));
            Assert.That(restarted.PreviewBackup(_backup).Result.Best30.Total, Is.EqualTo(900));
        }

        [TestCase("modified")]
        [TestCase("extra")]
        [TestCase("version")]
        [TestCase("truncated")]
        public void InvalidArchiveCannotChangeProfile(string damage)
        {
            _source.ExportBackup(_backup);
            if (damage == "truncated") File.WriteAllText(_backup, "truncated");
            else
            {
                using var zip = ZipFile.Open(_backup, ZipArchiveMode.Update);
                string name = damage == "extra" ? "../other.json" : damage == "version" ? "manifest.json" : "PlayHistory.jsonl";
                zip.GetEntry(name)?.Delete();
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("{\"format\":\"OpenSekai-Player\",\"version\":2}");
            }
            Assert.Throws<InvalidDataException>(() => _target.PreviewBackup(_backup));
            Assert.That(_target.Read().Best30.Total, Is.Zero);
            Assert.That(_target.Read().Appearance.AvatarId, Is.EqualTo("default"));
        }

        [Test]
        public async Task AsyncFileOperationsSupportCancellationAndConfirm()
        {
            await _source.ExportBackupAsync(_backup);
            var cancelled = new CancellationToken(true);
            try
            {
                await _target.PreviewBackupAsync(_backup, cancelled);
                Assert.Fail("Expected cancellation.");
            }
            catch (OperationCanceledException) { }
            Assert.That(_target.Read().Best30.Total, Is.Zero);
            var preview = await _target.PreviewBackupAsync(_backup);
            Assert.That(_target.ConfirmImport(preview.Id).Best30.Total, Is.EqualTo(600));
        }

        [Test]
        public void FailedPointerWriteLeavesOriginalDataAndPreviewCanRetry()
        {
            _source.ExportBackup(_backup);
            Save(_target, "c", "C");
            var preview = _target.PreviewBackup(_backup);
            string pointer = Path.Combine(_targetRoot, "PlayerArchives", "active");
            Directory.CreateDirectory(pointer);
            Assert.Catch<IOException>(() => _target.ConfirmImport(preview.Id));
            Assert.That(new PlayerProfileStore(_targetRoot).Read().Best30.Total, Is.EqualTo(300));
            Directory.Delete(pointer);
            Assert.That(_target.ConfirmImport(preview.Id).Best30.Total, Is.EqualTo(900));
        }

        [Test]
        public void DeletingBestThenClearingAndRestoringReplacesWarmAndPersistedIndexes()
        {
            Save(_source, "older", "A", 900000, "2026-11-21T00:00:00Z");
            Assert.Throws<InvalidOperationException>(() => _source.DeleteAllHistory());
            _source.ExportBackup(_backup);
            var reopened = new PlayerProfileStore(_sourceRoot);
            Assert.That(reopened.HasCurrentBackup, Is.True);
            string Journal() => Path.Combine(SongPackageStorage.ActivePlayerDirectory(_sourceRoot), "PlayHistory.jsonl");
            Assert.That(reopened.Summaries.Load(Journal())["A"].PlayCount, Is.EqualTo(2));
            var best = reopened.ReadHistory(0, 3).Records.Single(p => p.RecordId == "a");
            reopened.DeleteHistory(best);
            Assert.That(reopened.ReadHistory(0, 10).TotalCount, Is.EqualTo(2));
            Assert.That(reopened.Read().Best30.Entries.Single(e => e.Play.ScoreKey == "A").Play.RecordId, Is.EqualTo("older"));
            var fallback = reopened.Summaries.Load(Journal())["A"];
            Assert.That(fallback.PlayCount, Is.EqualTo(1));
            Assert.That(fallback.Best.RecordId, Is.EqualTo("older"));
            Assert.That(fallback.LastPlayedAtUtc, Is.EqualTo("2026-11-21T00:00:00Z"));
            Assert.That(reopened.History.Retry("a", out _), Is.Null);
            Assert.Throws<InvalidOperationException>(() => reopened.DeleteHistory(best));
            reopened.DeleteAllHistory();
            Assert.That(reopened.Read().Best30.Total, Is.Zero);
            Assert.That(reopened.Summaries.Load(Journal()), Is.Empty);
            Assert.That(reopened.ReadHistory(0, 10).Records, Is.Empty);
            Assert.That(reopened.Read().Appearance.Nickname, Is.EqualTo("初音"));
            reopened = new PlayerProfileStore(_sourceRoot);
            Assert.That(reopened.HasCurrentBackup, Is.False);
            Assert.That(reopened.Summaries.Load(Journal()), Is.Empty);
            var preview = reopened.PreviewBackup(_backup);
            reopened.ConfirmImport(preview.Id);
            Assert.That(reopened.HasCurrentBackup, Is.False);
            Assert.That(reopened.ReadHistory(0, 10).TotalCount, Is.EqualTo(3));
            Assert.That(reopened.Read().Best30.Total, Is.EqualTo(600));
            Assert.That(reopened.Summaries.Load(Journal())["A"].Best.RecordId, Is.EqualTo("a"));
            Assert.That(new PlayerProfileStore(_sourceRoot).Summaries.Load(Journal())["A"].PlayCount, Is.EqualTo(2));
        }

        [Test]
        public async Task TemporaryCopyAndLatePickerSuccessCannotAuthorizeClearingNewScores()
        {
            var copy = await _source.WriteBackupCopyAsync(_backup, default);
            Assert.That(_source.HasCurrentBackup, Is.False);
            Save(_source, "c", "C");
            _source.MarkBackupSaved(copy.Directory, copy.Bytes);
            Assert.Throws<InvalidOperationException>(() => _source.DeleteAllHistory());
            await _source.ExportBackupAsync(_backup);
            Assert.That(_source.HasCurrentBackup, Is.True);
            // A retired run changes the journal even though it has no library summary.
            _source.History.Submit(new CustomMusicScorePlayHistoryRecord
                { RecordId = "retired", EndReason = PlayResultKind.Retired }, null, out var summary);
            Assert.That(summary, Is.Null);
            Assert.That(new PlayerProfileStore(_sourceRoot).HasCurrentBackup, Is.False);
            Assert.Throws<InvalidOperationException>(() => _source.DeleteAllHistory());
        }

        [Test]
        public void IdenticalIdlessLegacyPlaysCanBeDeletedIndividuallyAndRestoredAfterClearing()
        {
            string path = Path.Combine(_sourceRoot, "PlayHistory.jsonl");
            const string LEGACY_PLAY = "{\"schemaVersion\":1,\"title\":\"legacy\"}";
            File.AppendAllLines(path, new[] { LEGACY_PLAY, "{torn", LEGACY_PLAY });
            _source.ExportBackup(_backup);
            var selected = _source.ReadHistory(0, 10).Records.Last(p => p.Title == "legacy");
            _source.DeleteHistory(selected);
            string current = Path.Combine(SongPackageStorage.ActivePlayerDirectory(_sourceRoot), "PlayHistory.jsonl");
            Assert.That(File.ReadAllLines(current).Count(line => line == LEGACY_PLAY), Is.EqualTo(1));
            Assert.That(File.ReadAllLines(current), Does.Contain("{torn"));
            _source.DeleteAllHistory();
            _source.ConfirmImport(_source.PreviewBackup(_backup).Id);
            Assert.That(_source.ReadHistory(0, 10).Records.Count(p => p.Title == "legacy"), Is.EqualTo(2));
        }

        private static void Save(PlayerProfileStore store, string id, string chart, int score = 1000000,
            string when = "2026-11-22T00:00:00Z")
        {
            var play = new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3, RecordId = id, ScoreKey = chart, EndReason = PlayResultKind.Completed,
                PlayedAtUtc = when, FormulaVersion = RankingRules.FORMULA_VERSION,
                MaximumScore = 1000000, Life = 100, PlayLevel = 30, TotalScore = score, TotalComboCount = 10
            };
            store.History.Submit(play, null, out _);
        }
    }
}
