using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class AchievementTests
    {
        private static readonly DateTimeOffset _epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private string _root;
        private PlayerProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
            _store = new PlayerProfileStore(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        private static CustomMusicScorePlayHistoryRecord Play(int n, string chart, int hits = 5, int level = 28,
            PlayResultKind kind = PlayResultKind.Completed, bool ap = false, int auto = 0) => new()
        {
            SchemaVersion = 3, RecordId = "run-" + n, ScoreKey = chart, PlayedAtUtc = _epoch.AddMinutes(n).ToString("O"),
            EndReason = kind, Life = kind == PlayResultKind.Failed ? 0 : 100, PlayLevel = level, MaximumScore = 1000000,
            FormulaVersion = 1, TotalScore = 900000, TotalComboCount = hits, MaxCombo = hits, AutoCount = auto,
            PerfectCount = ap ? hits : hits - 1, GreatCount = ap ? 0 : 1, IsAllPerfect = ap,
            IsFullCombo = kind == PlayResultKind.Completed
        };

        private RankingSettlement Save(CustomMusicScorePlayHistoryRecord row) => _store.History.Submit(row, null, out _);

        private AchievementProgress Item(string id) => _store.ReadAchievements().Single(a => a.Id == id);

        [Test]
        public void ManualHitsAndDistinctChartsAgreeWithReplayAndOlderReceipt()
        {
            Save(Play(1, "failed", 9900, kind: PlayResultKind.Failed));
            Save(Play(2, "quit", 95, kind: PlayResultKind.Retired));
            Save(Play(3, "auto", 50, auto: 1));
            _store.History.Submit(Play(4, "editor"), RankingEligibility.TestPlay, out _);
            Save(new CustomMusicScorePlayHistoryRecord { SchemaVersion = 2, RecordId = "old", PerfectCount = 500 });
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(9995));
            var ap = Play(5, "a", ap: true);
            var receipt = Save(ap);
            Assert.That(receipt.UnlockedAchievementIds, Is.EquivalentTo(new[] { "manual_hits_10000", "fc_lv25", "first_ap" }));
            Assert.That(Save(ap).UnlockedAchievementIds, Is.EqualTo(receipt.UnlockedAchievementIds));
            Save(Play(6, "a"));
            Save(Play(7, "low", level: 24));
            Save(Play(8, "medium", level: 27));
            for (int n = 9; n <= 17; n++) Save(Play(n, "high-" + n));
            Assert.That(Item("fc_lv28_10").Current, Is.EqualTo(10));
            Assert.That(Item("fc_lv28_10").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(17)));
            var before = _store.ReadAchievements();
            _store = new PlayerProfileStore(_root);
            Assert.That(_store.ReadAchievements().Select(a => (a.Current, a.UnlockedAtUtc)),
                Is.EqualTo(before.Select(a => (a.Current, a.UnlockedAtUtc))));
            Assert.That(_store.History.Retry(ap.RecordId, out _).UnlockedAchievementIds, Is.EqualTo(receipt.UnlockedAchievementIds));
            _store.DeleteHistory(_store.ReadHistory(0, 100).Records.Single(p => p.RecordId == ap.RecordId));
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.Null);
            Assert.That(Item("fc_lv28_10").Current, Is.EqualTo(10));
        }

        [Test]
        public void CheckpointsMergeByTimeAndDeletingDanRemovesOnlyItsEvidence()
        {
            var dan = new DanDefinition(10, new DanDamage(0, 1, 2, 3, 4),
                new[] { "https://test.org/levels/a", "https://test.org/levels/b" }, "Dan", "star", "#112233");
            var begin = new DanAttempt(Guid.NewGuid().ToString("N"), dan.Id, _epoch, DanOutcome.Interrupted, Array.Empty<DanSongResult>());
            DanSongResult Song(int minute) => new("chart", "Title", PlayResultKind.Completed, 15, 10,
                new LiveScore { life = 10, perfectCount = 10, greatCount = 5, goodCount = 3, badCount = 2, missCount = 8 },
                _epoch.AddMinutes(minute));
            var first = begin.Append(dan, Song(2));
            _store.SaveDanAttempt(dan, first, out var firstUnlocks);
            Assert.That(firstUnlocks, Is.Empty);
            var last = first.Append(dan, Song(4));
            _store.SaveDanAttempt(dan, last, out var lastUnlocks);
            Assert.That(lastUnlocks, Is.EqualTo(new[] { "first_dan_pass" }));
            _store.SaveDanAttempt(dan, last, out var repeated);
            Assert.That(repeated, Is.Empty);
            _store.EquipDan(dan.Id);
            string zip = Path.Combine(_root, "player.zip");
            _store.ExportBackup(zip);
            var target = new PlayerProfileStore(Path.Combine(_root, "merged"));
            target.History.Submit(Play(1, "x", 9970, kind: PlayResultKind.Retired), null, out _);
            var preview = target.PreviewBackup(zip);
            Assert.That(preview.UnlockedAchievementIds, Is.EquivalentTo(new[] { "manual_hits_10000", "first_dan_pass" }));
            Assert.That(target.ReadAchievements().All(a => a.UnlockedAtUtc == null), Is.True);
            target.ConfirmImport(preview.Id);
            _store = target;
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(10010));
            Assert.That(Item("manual_hits_10000").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
            Assert.That(Item("fc_lv25").Current, Is.Zero);
            Assert.That(Item("first_dan_pass").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
            var repeat = target.PreviewBackup(zip);
            Assert.That(repeat.UnlockedAchievementIds, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => target.DeleteDanDefinition(dan.Id));
            target.CancelImport(repeat.Id);
            target.EquipDan(dan.Id);
            target.DeleteDanDefinition(dan.Id);
            target.DeleteDanDefinition(dan.Id);
            _store = new PlayerProfileStore(Path.Combine(_root, "merged"));
            Assert.That(_store.ReadDanDefinitions(), Is.Empty);
            Assert.That(_store.ReadDanHistory(), Is.Empty);
            Assert.That(_store.Read().EquippedDanId, Is.Null);
            Assert.That(_store.Read().EarnedDanIds, Is.Empty);
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(9970));
            Assert.That(_store.ReadAchievements().All(a => a.UnlockedAtUtc == null), Is.True);
        }

        [Test]
        public void ImportingEarlierApMovesFirstUnlockTimeWithoutAnnouncingItAgain()
        {
            Save(Play(10, "ap", ap: true));
            var old = new PlayerProfileStore(Path.Combine(_root, "older"));
            old.History.Submit(Play(1, "ap", ap: true), null, out _);
            string zip = Path.Combine(_root, "older.zip");
            old.ExportBackup(zip);
            var preview = _store.PreviewBackup(zip);
            Assert.That(preview.UnlockedAchievementIds, Is.Empty);
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(10)));
            _store.ConfirmImport(preview.Id);
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
            Assert.That(Item("fc_lv28_10").Current, Is.EqualTo(1));
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(10));
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
        }

        [Test]
        public void SaveFailureAwardsOnlyOnSuccessfulRetry()
        {
            string blocked = Path.Combine(_root, "blocked");
            File.WriteAllText(blocked, "blocked");
            var store = new PlayerProfileStore(blocked);
            var ap = Play(1, "ap", ap: true);
            var failed = store.History.Submit(ap, null, out _);
            Assert.That(failed.SaveState, Is.EqualTo(HistorySaveState.SaveFailed));
            Assert.That(failed.UnlockedAchievementIds, Is.Empty);
            Assert.That(store.ReadAchievements().All(a => a.Current == 0), Is.True);
            File.Delete(blocked);
            var saved = store.History.Retry(ap.RecordId, out _);
            Assert.That(saved.UnlockedAchievementIds, Is.EquivalentTo(new[] { "fc_lv25", "first_ap" }));
            Assert.That(store.History.Retry(ap.RecordId, out _).UnlockedAchievementIds, Is.EqualTo(saved.UnlockedAchievementIds));
        }
    }
}
