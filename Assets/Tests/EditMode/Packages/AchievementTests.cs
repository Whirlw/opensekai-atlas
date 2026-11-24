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
            Assert.That(receipt.ReachedAchievementIds, Is.EquivalentTo(new[] { "manual_hits_10000", "fc_lv25", "first_ap" }));
            Assert.That(Save(ap).ReachedAchievementIds, Is.EqualTo(receipt.ReachedAchievementIds));
            Save(Play(6, "a"));
            Save(Play(7, "low", level: 24));
            Save(Play(8, "medium", level: 27));
            for (int n = 9; n <= 17; n++) Save(Play(n, "high-" + n));
            Assert.That(Item("fc_lv28_10").Current, Is.EqualTo(10));
            Assert.That(Item("fc_lv28_10").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(17)));
            var before = _store.ReadAchievements();
            _store = new PlayerProfileStore(_root);
            Assert.That(_store.ReadAchievements().Select(a => (a.Current, a.ReachedAtUtc)),
                Is.EqualTo(before.Select(a => (a.Current, a.ReachedAtUtc))));
            Assert.That(_store.History.Retry(ap.RecordId, out _).ReachedAchievementIds, Is.EqualTo(receipt.ReachedAchievementIds));
            _store.DeleteHistory(_store.ReadHistory(0, 100).Records.Single(p => p.RecordId == ap.RecordId));
            Assert.That(Item("first_ap").ReachedAtUtc, Is.Null);
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
            _store.SaveDanAttempt(dan, first, out var firstReached);
            Assert.That(firstReached, Is.Empty);
            var last = first.Append(dan, Song(4));
            _store.SaveDanAttempt(dan, last, out var lastReached);
            Assert.That(lastReached, Is.EqualTo(new[] { "first_dan_pass" }));
            _store.SaveDanAttempt(dan, last, out var repeated);
            Assert.That(repeated, Is.Empty);
            _store.WearMedal(Medal.DanKey(dan.Id));
            string zip = Path.Combine(_root, "player.zip");
            _store.ExportBackup(zip);
            var target = new PlayerProfileStore(Path.Combine(_root, "merged"));
            target.History.Submit(Play(1, "x", 9970, kind: PlayResultKind.Retired), null, out _);
            var preview = target.PreviewBackup(zip);
            Assert.That(preview.ReachedAchievementIds, Is.EquivalentTo(new[] { "manual_hits_10000", "first_dan_pass" }));
            Assert.That(target.ReadAchievements().All(a => a.ReachedAtUtc == null), Is.True);
            target.ConfirmImport(preview.Id);
            _store = target;
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(10010));
            Assert.That(Item("manual_hits_10000").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
            Assert.That(Item("fc_lv25").Current, Is.Zero);
            Assert.That(Item("first_dan_pass").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
            var repeat = target.PreviewBackup(zip);
            Assert.That(repeat.ReachedAchievementIds, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => target.DeleteDanDefinition(dan.Id));
            target.CancelImport(repeat.Id);
            target.WearMedal(Medal.DanKey(dan.Id));
            target.DeleteDanDefinition(dan.Id);
            target.DeleteDanDefinition(dan.Id);
            _store = new PlayerProfileStore(Path.Combine(_root, "merged"));
            Assert.That(_store.ReadDanDefinitions(), Is.Empty);
            Assert.That(_store.ReadDanHistory(), Is.Empty);
            Assert.That(_store.Read().WornMedals, Is.All.Null);
            Assert.That(_store.Read().EarnedDanIds, Is.Empty);
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(9970));
            Assert.That(_store.ReadAchievements().All(a => a.ReachedAtUtc == null), Is.True);
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
            Assert.That(preview.ReachedAchievementIds, Is.Empty);
            Assert.That(Item("first_ap").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(10)));
            _store.ConfirmImport(preview.Id);
            Assert.That(Item("first_ap").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
            Assert.That(Item("fc_lv28_10").Current, Is.EqualTo(1));
            Assert.That(Item("manual_hits_10000").Current, Is.EqualTo(10));
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("first_ap").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
        }

        [Test]
        public void AReachedGoalWaitsForThePlayerAndOnlyThenGivesItsMedalAndAvatar()
        {
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("first_ap"), "not reached");
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("unknown"));
            Save(Play(1, "ap", ap: true));
            Assert.That(Item("first_ap").State, Is.EqualTo(AchievementState.Claimable));
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.Null);
            Assert.That(_store.ReadMedals(), Is.Empty);
            Assert.That(_store.OwnsAvatar("sparkle"), Is.False);
            Assert.That(_store.OwnsAvatar("default"), Is.True);
            Assert.Throws<InvalidOperationException>(() => _store.SetAppearance("Player", "sparkle"));

            _store.UnlockAchievement("first_ap");
            Assert.That(Item("first_ap").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(_store.ReadMedals().Select(medal => medal.Key), Is.EqualTo(new[] { Medal.AchievementKey("first_ap") }));
            _store.SetAppearance("Player", "sparkle");
            var first = Item("first_ap").UnlockedAtUtc;
            _store.UnlockAchievement("first_ap");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("first_ap").UnlockedAtUtc, Is.EqualTo(first), "it stays, and unlocking again changes nothing");
            Assert.That(_store.OwnsAvatar("sparkle"), Is.True);
        }

        [Test]
        public void LevelsAreUnlockedInOrderAndTheNextOneOpens()
        {
            Save(Play(1, "long", 60000, kind: PlayResultKind.Retired));
            AchievementSeries hits = Series("manual_hits_10000");
            Assert.That(hits.Steps.Select(step => step.Target), Is.EqualTo(new[] { 10000, 50000, 100000, 500000, 1000000 }));
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((0, 1, 0, AchievementState.Claimable)));
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("manual_hits_50000"), "reached, but the level before it is not unlocked");

            _store.UnlockAchievement("manual_hits_10000");
            hits = Series("manual_hits_10000");
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((1, 2, 1, AchievementState.Claimable)));
            _store.UnlockAchievement("manual_hits_50000");
            hits = Series("manual_hits_10000");
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((2, 3, 2, AchievementState.InProgress)));
            Assert.That(hits.Steps[2].Current, Is.EqualTo(60000));
        }

        [Test]
        public void WhatCanBeUnlockedComesFirstAndWhatIsCompleteLast()
        {
            Save(Play(1, "ap", ap: true, level: 20));
            Save(Play(2, "fc", level: 26));
            _store.UnlockAchievement("first_ap");
            AchievementSeries[] sorted = AchievementSeries.Sort(AchievementSeries.Group(_store.ReadAchievements())).ToArray();
            Assert.That(sorted.Select(series => series.Id), Is.EqualTo(new[] { "full_combo", "manual_hits", "first_dan_pass", "first_ap" }));
            Assert.That(sorted.Last().IsComplete, Is.True);
        }

        [Test]
        public void DeletingTheRunBehindAnUnlockTakesItBackForGood()
        {
            var ap = Play(1, "ap", ap: true);
            Save(ap);
            Save(Play(2, "fc"));
            _store.UnlockAchievement("first_ap");
            _store.UnlockAchievement("fc_lv25");
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single(play => play.RecordId == ap.RecordId));
            Assert.That(Item("first_ap").State, Is.EqualTo(AchievementState.InProgress));
            Assert.That(Item("fc_lv25").State, Is.EqualTo(AchievementState.Unlocked), "another run still reaches it");
            Save(Play(3, "ap", ap: true));
            Assert.That(Item("first_ap").State, Is.EqualTo(AchievementState.Claimable), "reached again, it is unlocked again");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("fc_lv25").State, Is.EqualTo(AchievementState.Unlocked));
        }

        [Test]
        public void ABackupCarriesTheUnlocks()
        {
            Save(Play(1, "ap", ap: true));
            _store.UnlockAchievement("first_ap");
            string zip = Path.Combine(_root, "unlocks.zip");
            _store.ExportBackup(zip);

            var fresh = new PlayerProfileStore(Path.Combine(_root, "fresh"));
            var preview = fresh.PreviewBackup(zip);
            Assert.That(preview.AchievementsAdded, Is.EqualTo(1));
            Assert.That(preview.ReachedAchievementIds, Is.EqualTo(new[] { "fc_lv25" }), "what is unlocked already is not announced");
            fresh.ConfirmImport(preview.Id);
            Assert.That(fresh.ReadMedals().Select(medal => medal.Key), Is.EqualTo(new[] { Medal.AchievementKey("first_ap") }));

            // The same runs, not unlocked here yet: the unlocks alone are worth merging.
            var same = new PlayerProfileStore(Path.Combine(_root, "same"));
            same.SetAppearance("Local", "default");
            same.History.Submit(Play(1, "ap", ap: true), null, out _);
            preview = same.PreviewBackup(zip);
            Assert.That((preview.Added, preview.HasChanges), Is.EqualTo((0, true)));
            same.ConfirmImport(preview.Id);
            Assert.That(same.ReadAchievements().Single(goal => goal.Id == "first_ap").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(same.PreviewBackup(zip).HasChanges, Is.False);
        }

        private AchievementSeries Series(string id) => AchievementSeries.Group(_store.ReadAchievements()).Single(series => series.IndexOf(id) >= 0);

        [Test]
        public void SaveFailureAwardsOnlyOnSuccessfulRetry()
        {
            string blocked = Path.Combine(_root, "blocked");
            File.WriteAllText(blocked, "blocked");
            var store = new PlayerProfileStore(blocked);
            var ap = Play(1, "ap", ap: true);
            var failed = store.History.Submit(ap, null, out _);
            Assert.That(failed.SaveState, Is.EqualTo(HistorySaveState.SaveFailed));
            Assert.That(failed.ReachedAchievementIds, Is.Empty);
            Assert.That(store.ReadAchievements().All(a => a.Current == 0), Is.True);
            File.Delete(blocked);
            var saved = store.History.Retry(ap.RecordId, out _);
            Assert.That(saved.ReachedAchievementIds, Is.EquivalentTo(new[] { "fc_lv25", "first_ap" }));
            Assert.That(store.History.Retry(ap.RecordId, out _).ReachedAchievementIds, Is.EqualTo(saved.ReachedAchievementIds));
        }
    }
}
