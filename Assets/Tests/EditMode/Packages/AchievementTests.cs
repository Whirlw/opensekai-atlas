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
            SchemaVersion = 3, RecordId = "run-" + n, ScoreKey = chart, Ranked = true, PlayedAtUtc = _epoch.AddMinutes(n).ToString("O"),
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
            Assert.That(Item("HIT-01").Current, Is.EqualTo(9995));
            var ap = Play(5, "a", ap: true);
            var receipt = Save(ap);
            Assert.That(receipt.ReachedAchievementIds, Does.Contain("HIT-01").And.Contain("FC-01").And.Contain("AP-01"));
            Assert.That(Save(ap).ReachedAchievementIds, Is.EqualTo(receipt.ReachedAchievementIds));
            Save(Play(6, "a"));
            Save(Play(7, "low", level: 24));
            Save(Play(8, "medium", level: 27));
            for (int n = 9; n <= 17; n++) Save(Play(n, "high-" + n, hits: 800));
            Assert.That(Item("FC-03").Current, Is.EqualTo(9));
            Assert.That(Item("FC-03").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(13)));
            var before = _store.ReadAchievements();
            _store = new PlayerProfileStore(_root);
            Assert.That(_store.ReadAchievements().Select(a => (a.Current, a.ReachedAtUtc)),
                Is.EqualTo(before.Select(a => (a.Current, a.ReachedAtUtc))));
            Assert.That(_store.History.Retry(ap.RecordId, out _).ReachedAchievementIds, Is.EqualTo(receipt.ReachedAchievementIds));
            _store.DeleteHistory(_store.ReadHistory(0, 100).Records.Single(p => p.RecordId == ap.RecordId));
            Assert.That(Item("AP-01").ReachedAtUtc, Is.Null);
            Assert.That(Item("FC-03").Current, Is.EqualTo(9));
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
            Assert.That(lastReached, Is.EqualTo(new[] { "DAN-01", "DANPASS-01" }));
            _store.SaveDanAttempt(dan, last, out var repeated);
            Assert.That(repeated, Is.Empty);
            _store.WearMedal(Medal.DanKey(dan.Id));
            string zip = Path.Combine(_root, "player.zip");
            _store.ExportBackup(zip);
            var target = new PlayerProfileStore(Path.Combine(_root, "merged"));
            target.History.Submit(Play(1, "x", 9970, kind: PlayResultKind.Retired), null, out _);
            var preview = target.PreviewBackup(zip);
            Assert.That(preview.ReachedAchievementIds, Is.EquivalentTo(new[] { "HIT-01", "DAN-01", "DANPASS-01" }));
            Assert.That(target.ReadAchievements().All(a => a.ReachedAtUtc == null), Is.True);
            target.ConfirmImport(preview.Id);
            _store = target;
            Assert.That(Item("HIT-01").Current, Is.EqualTo(10010));
            Assert.That(Item("HIT-01").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
            Assert.That(Item("FC-01").Current, Is.Zero);
            Assert.That(Item("DAN-01").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(4)));
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
            Assert.That(Item("HIT-01").Current, Is.EqualTo(9970));
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
            Assert.That(Item("AP-01").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(10)));
            _store.ConfirmImport(preview.Id);
            Assert.That(Item("AP-01").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
            Assert.That(Item("FC-01").Current, Is.EqualTo(1));
            Assert.That(Item("HIT-01").Current, Is.EqualTo(10));
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("AP-01").ReachedAtUtc, Is.EqualTo(_epoch.AddMinutes(1)));
        }

        [Test]
        public void AReachedGoalWaitsForThePlayerAndOnlyThenGivesItsMedalAndAvatar()
        {
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("AP-01"), "not reached");
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("unknown"));
            Save(Play(1, "ap", 500, ap: true));
            _store.UnlockAchievement("AP-01");
            Assert.That(Item("AP-02").State, Is.EqualTo(AchievementState.Claimable));
            Assert.That(Item("AP-02").UnlockedAtUtc, Is.Null);
            Assert.That(_store.ReadMedals(), Is.Empty);
            Assert.That(_store.OwnsAvatar("sparkle"), Is.False);
            Assert.That(_store.OwnsAvatar("default"), Is.True);
            Assert.Throws<InvalidOperationException>(() => _store.SetAppearance("Player", "sparkle"));

            _store.UnlockAchievement("AP-02");
            Assert.That(Item("AP-02").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(_store.ReadMedals().Select(medal => medal.Key), Is.EqualTo(new[] { Medal.AchievementKey("AP-02") }));
            _store.SetAppearance("Player", "sparkle");
            var first = Item("AP-02").UnlockedAtUtc;
            _store.UnlockAchievement("AP-02");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("AP-02").UnlockedAtUtc, Is.EqualTo(first), "it stays, and unlocking again changes nothing");
            Assert.That(_store.OwnsAvatar("sparkle"), Is.True);
        }

        [TestCase("sparkle", "default")]
        [TestCase("note", "note")]
        public void DeletingTheLastUnlockingRunShowsOnlyAnOwnedAvatarAfterReload(string selected, string expected)
        {
            Save(Play(1, "ap", 500, ap: true));
            _store.UnlockAchievement("AP-01");
            _store.UnlockAchievement("AP-02");
            _store.SetAppearance("歌手", selected);
            _store.DeleteHistory(_store.ReadHistory(0, 1).Records.Single());
            foreach (var archive in new[] { _store, new PlayerProfileStore(_root) })
            {
                Assert.That(archive.OwnsAvatar("sparkle"), Is.False);
                Assert.That(archive.Read().Appearance.Nickname, Is.EqualTo("歌手"));
                Assert.That(archive.Read().Appearance.AvatarId, Is.EqualTo(expected));
                Assert.Throws<InvalidOperationException>(() => archive.SetAppearance("歌手", "sparkle"));
            }
        }

        [Test]
        public void DeletingAnUnidentifiedLegacyLinePreservesIndependentRetryEvidence()
        {
            string journal = Path.Combine(_root, OpenSekai.Shared.Packages.SongPackageStorage.PLAY_HISTORY_FILE_NAME);
            File.WriteAllText(journal, "{\"schemaVersion\":2,\"scoreKey\":\"legacy\"}\n");
            Save(Play(1, "chart"));
            _store.RecordRetryStart("run-1", "chart", "prior", "pause");
            _store.RecordRetryStart("never-settled", "chart", "prior", "pause");
            Assert.That(Item("RETRY-02").Current, Is.EqualTo(2));
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single(p => p.RecordId == null));
            Assert.That(Item("RETRY-02").Current, Is.EqualTo(2));
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single());
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("RETRY-02").Current, Is.EqualTo(1));
            _store.ExportBackup(Path.Combine(_root, "retry-retained.zip"));
            _store.DeleteAllHistory();
            Assert.That(Item("RETRY-02").Current, Is.Zero);
        }

        [Test]
        public void LevelsAreUnlockedInOrderAndTheNextOneOpens()
        {
            Save(Play(1, "long", 160000, kind: PlayResultKind.Retired));
            AchievementSeries hits = Series("HIT-01");
            Assert.That(hits.Steps.Select(step => step.Target), Is.EqualTo(new[] { 10000, 100000, 1000000, 10000000 }));
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((0, 1, 0, AchievementState.Claimable)));
            Assert.Throws<InvalidOperationException>(() => _store.UnlockAchievement("HIT-02"), "reached, but the level before it is not unlocked");

            _store.UnlockAchievement("HIT-01");
            hits = Series("HIT-01");
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((1, 2, 1, AchievementState.Claimable)));
            _store.UnlockAchievement("HIT-02");
            hits = Series("HIT-01");
            Assert.That((hits.UnlockedCount, hits.OpenCount, hits.CurrentStep, hits.State), Is.EqualTo((2, 3, 2, AchievementState.InProgress)));
            Assert.That(hits.Steps[2].Current, Is.EqualTo(160000));
        }

        [Test]
        public void WhatCanBeUnlockedComesFirstAndWhatIsCompleteLast()
        {
            Save(Play(1, "ap", ap: true, level: 20));
            Save(Play(2, "fc", level: 26));
            _store.UnlockAchievement("AP-01");
            AchievementSeries[] sorted = AchievementSeries.Sort(AchievementSeries.Group(_store.ReadAchievements())).ToArray();
            Assert.That(sorted.First().State, Is.EqualTo(AchievementState.Claimable));
            Assert.That(sorted.Last().State, Is.EqualTo(AchievementState.InProgress));
        }

        [Test]
        public void DeletingTheRunBehindAnUnlockTakesItBackForGood()
        {
            var ap = Play(1, "ap", ap: true);
            Save(ap);
            Save(Play(2, "fc"));
            _store.UnlockAchievement("AP-01");
            _store.UnlockAchievement("FC-01");
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single(play => play.RecordId == ap.RecordId));
            Assert.That(Item("AP-01").State, Is.EqualTo(AchievementState.InProgress));
            Assert.That(Item("FC-01").State, Is.EqualTo(AchievementState.Unlocked), "another run still reaches it");
            Save(Play(3, "ap", ap: true));
            Assert.That(Item("AP-01").State, Is.EqualTo(AchievementState.Claimable), "reached again, it is unlocked again");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("FC-01").State, Is.EqualTo(AchievementState.Unlocked));
        }

        [Test]
        public void ABackupCarriesTheUnlocks()
        {
            Save(Play(1, "ap", ap: true));
            _store.UnlockAchievement("AP-01");
            string zip = Path.Combine(_root, "unlocks.zip");
            _store.ExportBackup(zip);

            var fresh = new PlayerProfileStore(Path.Combine(_root, "fresh"));
            var preview = fresh.PreviewBackup(zip);
            Assert.That(preview.AchievementsAdded, Is.EqualTo(1));
            Assert.That(preview.ReachedAchievementIds, Does.Contain("FC-01").And.Not.Contain("AP-01"), "what is unlocked already is not announced");
            fresh.ConfirmImport(preview.Id);
            Assert.That(fresh.ReadMedals().Select(medal => medal.Key), Is.Empty);

            // The same runs, not unlocked here yet: the unlocks alone are worth merging.
            var same = new PlayerProfileStore(Path.Combine(_root, "same"));
            same.SetAppearance("Local", "default");
            same.History.Submit(Play(1, "ap", ap: true), null, out _);
            preview = same.PreviewBackup(zip);
            Assert.That((preview.Added, preview.HasChanges), Is.EqualTo((0, true)));
            same.ConfirmImport(preview.Id);
            Assert.That(same.ReadAchievements().Single(goal => goal.Id == "AP-01").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(same.PreviewBackup(zip).HasChanges, Is.False);
        }

        [Test]
        public void CatalogHasAllRewardsAndSeparateFrameAndGrade()
        {
            var goals = AchievementGoals.All;
            Assert.That(goals.Count, Is.EqualTo(145));
            Assert.That(goals.Select(g => g.Id), Is.Unique);
            Assert.That(goals.GroupBy(g => g.Series).Count(), Is.EqualTo(28));
            Assert.That(goals.Count(g => g.GivesMedal), Is.EqualTo(109));
            Assert.That(goals.Count(g => g.AvatarId != null), Is.EqualTo(18));
            Assert.That(goals.Where(g => g.AvatarId != null).Select(g => g.AvatarId), Is.Unique);
            Assert.That(goals.Where(g => g.GivesMedal).All(g => MedalGrades.IsKnown(g.MedalGrade)), Is.True);
            Assert.That(AchievementGoals.Find("FCLVL-11").MedalFrameTier, Is.EqualTo(MedalTiers.SPECIAL));
            Assert.That(AchievementGoals.Find("CODA-01").MedalFrameTier, Is.EqualTo(MedalTiers.TOP));
            Assert.That(AchievementGoals.Find("CODA-01").MedalDisplayName, Is.EqualTo("终乐章 ω"));
        }

        private static CustomMusicScorePlayHistoryRecord With(CustomMusicScorePlayHistoryRecord play,
            params (string field, object value)[] changes)
        {
            var json = Newtonsoft.Json.Linq.JObject.FromObject(play);
            foreach (var (field, value) in changes) json[field] = Newtonsoft.Json.Linq.JToken.FromObject(value);
            return json.ToObject<CustomMusicScorePlayHistoryRecord>();
        }

        [Test]
        public void EncoreAccumulatesPerContentKeyAcrossInterruptions()
        {
            for (int i = 1; i <= 99; i++) Save(Play(i, "main", level: 33));
            Save(Play(100, "different", level: 39));
            Save(Play(101, "main", level: 33, kind: PlayResultKind.Retired));
            Save(Play(102, "main", level: 32));
            Save(With(Play(103, "main", level: 33), ("ranked", false)));
            Assert.That(Item("ENCORE-01").Current, Is.EqualTo(99));
            var last = Play(104, "main", level: 33);
            Assert.That(Save(last).ReachedAchievementIds, Does.Contain("ENCORE-01"));
            Save(last);
            Assert.That(Item("ENCORE-01").Current, Is.EqualTo(100));
        }

        [Test]
        public void RankingUsesTheSameBestThirtyAndRejectsUnrankedContributions()
        {
            for (int i = 0; i < 32; i++) Save(Play(i, "chart-" + i, level: 34, ap: true));
            Save(With(Play(32, "ignored", level: 99, ap: true), ("ranked", false)));
            Save(Play(33, "chart-0", level: 34, ap: true));
            Assert.That(Item("B30-01").Current, Is.EqualTo(_store.Read().Best30.Total));
            Assert.That(_store.Read().Best30.Entries.Count, Is.EqualTo(30));
            Assert.That(Item("B30-01").ReachedAtUtc.HasValue, Is.True);
        }

        [Test]
        public void SavedRetirementBreaksTheStreakAndOneBreakCanLaterBeRepaired()
        {
            Save(Play(1, "a", level: 25));
            Save(Play(2, "b", level: 26));
            Save(Play(3, "end", kind: PlayResultKind.Retired));
            for (int i = 4; i <= 6; i++) Save(Play(i, "chart-" + i, level: 25 + i));
            Assert.That(Item("CHAIN-02").ReachedAtUtc.HasValue, Is.True);
            Assert.That(Item("CHAIN-03").Current, Is.EqualTo(3));
            Assert.That(Item("STAIR-02").ReachedAtUtc.HasValue, Is.True);
            Save(With(Play(7, "fix", 1000), ("isFullCombo", false), ("greatCount", 0), ("goodCount", 1)));
            Assert.That(Item("GREAT-01").Current, Is.EqualTo(1));
            Assert.That(Item("FIX-01").Current, Is.Zero);
            Save(With(Play(8, "fix", 1000), ("perfectCount", 998), ("greatCount", 2)));
            Save(Play(9, "fix", 1000, ap: true));
            Assert.That(Item("FIX-01").Current, Is.EqualTo(1));
            Assert.That(Item("GREAT-01").Current, Is.EqualTo(1));
        }

        [Test]
        public void ReviewCountsOnlyDistinctGameDaysForTheSameChart()
        {
            for (int i = 1; i <= 10; i++)
                Save(With(Play(i, "same", 800), ("gameDay", "2026-01-" + i.ToString("00")), ("rank", "S")));
            Save(With(Play(11, "same", 800), ("gameDay", "2026-01-10"), ("rank", "S")));
            Save(With(Play(12, "same", 800), ("rank", "S")));
            Assert.That(Item("REVIEW-05").Current, Is.EqualTo(10));
            Assert.That(Item("FC-03").Current, Is.EqualTo(1));
            Assert.That(Item("SETLIST-01").Current, Is.EqualTo(1));
        }

        [Test]
        public void SpecializedChartEvidenceAndIntegerBoundaries()
        {
            Save(With(Play(1, "a", 499, 25, ap: true), ("ranked", false)));
            Assert.That(Item("FC-01").Current, Is.Zero);
            Save(Play(2, "b", 499, 25, ap: true));
            Assert.That(Item("AP-01").ReachedAtUtc, Is.Not.Null);
            Assert.That(Item("AP-02").Current, Is.Zero);
            Save(Play(3, "c", 500, 25, ap: true));
            Assert.That(Item("FC-02").ReachedAtUtc, Is.Not.Null);
            Assert.That(Item("AP-02").ReachedAtUtc, Is.Not.Null);
            Save(With(Play(4, "slow", 1200), ("musicLengthMs", 150001), ("rank", "S")));
            Assert.That(Item("DENSE-02").Current, Is.Zero);
            Save(With(Play(5, "exact", 1200), ("musicLengthMs", 150000), ("rank", "S")));
            Assert.That(Item("DENSE-02").Current, Is.EqualTo(1));
            Save(With(Play(6, "accuracy", 1000), ("perfectCount", 994), ("greatCount", 6)));
            long before = Item("EAR-05").Current;
            Save(With(Play(7, "accuracy", 1000), ("perfectCount", 995), ("greatCount", 5)));
            Assert.That(Item("EAR-05").Current, Is.EqualTo(before + 1));
            Save(With(Play(8, "hold"), ("holdChains", 29), ("holdPoints", 50), ("perfectHoldPoints", 50)));
            Save(With(Play(9, "hold"), ("holdChains", 30), ("holdPoints", 50), ("perfectHoldPoints", 49)));
            Assert.That(Item("HOLD-01").Current, Is.Zero);
            Save(With(Play(10, "hold", 500), ("holdChains", 30), ("holdPoints", 50), ("perfectHoldPoints", 50),
                ("flickJudgments", 15), ("perfectFlicks", 15)));
            Assert.That(Item("HOLD-01").Current, Is.EqualTo(1));
            Assert.That(Item("FLICK-01").Current, Is.EqualTo(1));
        }

        [Test]
        public void PrismAndCalendarRequireOrderedAndDatedEvidence()
        {
            string[] colors = { "expert", "hard", "easy", "normal", "master" };
            for (int i = 0; i < colors.Length; i++)
                Save(With(Play(i, colors[i], ap: true), ("difficulty", colors[i]), ("gameDay", i < 2 ? "2026-01-01" : "2026-01-02")));
            Assert.That(Item("PRISM-01").ReachedAtUtc, Is.Not.Null);
            Assert.That(Item("DAY-01").Current, Is.EqualTo(1));
            Assert.That(CustomMusicScorePlayHistoryStorage.GameDayOf(new DateTimeOffset(2026, 1, 2, 3, 59, 0, TimeSpan.FromHours(8))),
                Is.EqualTo("2026-01-01"));
            Assert.That(CustomMusicScorePlayHistoryStorage.GameDayOf(new DateTimeOffset(2026, 1, 2, 4, 0, 0, TimeSpan.FromHours(8))),
                Is.EqualTo("2026-01-02"));
        }

        [Test]
        public void RetryStartSurvivesWithoutSettlementAndMergesOnce()
        {
            _store.RecordRetryStart("new", "chart", "previous", "pause");
            _store.RecordRetryStart("new", "chart", "previous", "pause");
            string zip = Path.Combine(_root, "retry.zip");
            _store.ExportBackup(zip);
            _store = new PlayerProfileStore(Path.Combine(_root, "restore"));
            _store.ConfirmImport(_store.PreviewBackup(zip).Id);
            Assert.That(Item("RETRY-01").Current, Is.EqualTo(1));
            Assert.That(_store.PreviewBackup(zip).HasChanges, Is.False);
        }

        [Test]
        public void LegacyClaimsRequireEvidenceAndPreserveWornKeysInBackup()
        {
            Save(With(Play(1, "old", 50000, ap: true), ("ranked", false)));
            File.WriteAllText(Path.Combine(_root, PlayerProfileStore.ACHIEVEMENTS_FILE),
                "{'unlocked':{'manual_hits_50000':'2026-01-02T12:00:00Z','first_ap':'2026-01-02T12:00:00Z'}}");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("AP-01").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(_store.ReadAchievements().Count(g => g.State == AchievementState.Unlocked), Is.EqualTo(1));
            _store.WearMedal(0, Medal.AchievementKey("manual_hits_50000"));
            _store.WearMedal(1, Medal.AchievementKey("first_ap"));
            Assert.That(_store.OwnsAvatar("sparkle"), Is.True);
            string zip = Path.Combine(_root, "legacy.zip");
            _store.ExportBackup(zip);
            _store = new PlayerProfileStore(Path.Combine(_root, "restore"));
            _store.ConfirmImport(_store.PreviewBackup(zip).Id);
            Assert.That(_store.Read().WornMedals.Select(m => m.Key),
                Is.EqualTo(new[] { Medal.AchievementKey("manual_hits_50000"), Medal.AchievementKey("first_ap") }));
            Assert.That(Save(Play(2, "new", 500, 25, ap: true)).ReachedAchievementIds, Does.Not.Contain("AP-01"));
            var claim = _store.ClaimAchievement("AP-02");
            Assert.That(claim, Is.EqualTo((true, Medal.AchievementKey("AP-02"), (string)null)));
            Assert.That(_store.ReadMedals().Count, Is.EqualTo(3));
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single(r => r.RecordId == "run-1"));
            Assert.That(_store.Read().WornMedals[0], Is.Null);
            Assert.That(_store.Read().WornMedals[1]?.Key, Is.EqualTo(Medal.AchievementKey("first_ap")));
            Assert.That(_store.OwnsAvatar("sparkle"), Is.True);
            _store.DeleteHistory(_store.ReadHistory(0, 10).Records.Single());
            Assert.That(_store.Read().WornMedals, Is.All.Null);
            Assert.That(_store.OwnsAvatar("sparkle"), Is.False);
        }

        [Test]
        public void InheritedHitPlateRetainsItsAppearanceAndSingleInventorySlot()
        {
            Save(Play(1, "old", 10000, kind: PlayResultKind.Retired));
            File.WriteAllText(Path.Combine(_root, PlayerProfileStore.ACHIEVEMENTS_FILE),
                "{'unlocked':{'manual_hits_10000':'2026-01-02T12:00:00Z'}}");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("HIT-01").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(_store.ReadMedals().Single().Key, Is.EqualTo(Medal.AchievementKey("manual_hits_10000")));
        }

        [Test]
        public void OldTenChartClaimIncludesFirstLevelAndPreservesCrestWithUnrankedEvidence()
        {
            for (int i = 0; i < 10; i++) Save(With(Play(i, "old-" + i), ("ranked", false)));
            File.WriteAllText(Path.Combine(_root, PlayerProfileStore.ACHIEVEMENTS_FILE),
                "{'unlocked':{'fc_lv28_10':'2026-01-02T12:00:00Z'}}");
            _store = new PlayerProfileStore(_root);
            Assert.That(Item("FC-01").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(Item("FC-02").State, Is.EqualTo(AchievementState.Unlocked));
            Assert.That(Item("FC-03").Current, Is.Zero);
            Assert.That(_store.OwnsAvatar("crest"), Is.True);
            _store.DeleteHistory(_store.ReadHistory(0, 1).Records.Single());
            Assert.That(Item("FC-01").State, Is.EqualTo(AchievementState.InProgress));
            Assert.That(_store.OwnsAvatar("crest"), Is.False);
        }

        [TestCase(4, 32, true, 1000, true, true)]
        [TestCase(3, 32, true, 1000, false, true)]
        [TestCase(5, 32, true, 990, false, true)]
        [TestCase(5, 32, true, 989, false, false)]
        [TestCase(4, 31, true, 1000, false, true)]
        [TestCase(4, 32, false, 1000, false, true)]
        public void CourseChecksEverySongIncludingRepeatedKeys(int count, int level, bool ranked, int lastPerfect, bool coda, bool aureole)
        {
            var dan = new DanDefinition(10, new DanDamage(0, 1, 2, 3, 4),
                Enumerable.Repeat("https://test.org/levels/a", count).ToArray(), "Dan", "star", "#112233");
            var attempt = DanAttempt.Begin(dan);
            for (int i = 0; i < count; i++)
            {
                int perfect = i + 1 == count ? lastPerfect : 1000;
                attempt = attempt.Append(dan, new DanSongResult("same", "Same", PlayResultKind.Completed, 15, 10,
                    new LiveScore { life = 10, totalComboCount = 1000, maxCombo = 1000, perfectCount = perfect, greatCount = 1000 - perfect },
                    _epoch.AddMinutes(i), ranked, level));
                _store.SaveDanAttempt(dan, attempt);
            }
            Assert.That(Item("CODA-01").ReachedAtUtc.HasValue, Is.EqualTo(coda));
            Assert.That(Item("GOLRIOLE-01").ReachedAtUtc.HasValue, Is.EqualTo(aureole));
            Assert.That(Item("HIT-01").Current, Is.EqualTo(count * 1000));
            Assert.That(Item("AP-01").Current, Is.Zero);
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
            Assert.That(saved.ReachedAchievementIds, Does.Contain("FC-01").And.Contain("AP-01"));
            Assert.That(store.History.Retry(ap.RecordId, out _).ReachedAchievementIds, Is.EqualTo(saved.ReachedAchievementIds));
        }
    }
}
