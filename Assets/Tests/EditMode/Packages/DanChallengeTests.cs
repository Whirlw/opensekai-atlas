using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class DanChallengeTests
    {
        private readonly string[] _songs = { "https://charts.test/levels/a", "sonolus://charts.test/levels/b" };
        private DanDefinition Definition(string label = "双曲") =>
            new(100, new DanDamage(0, 1, 3, 6, 10), _songs, label, "diamond", "#526dc8");
        private DanDefinition Graded() =>
            new(100, new DanDamage(0, 1, 3, 6, 10), _songs, "双曲", "diamond", "#526dc8", MedalTiers.TOP, "ω");

        [Test]
        public void DanRestExcludesTimeBetweenBackgroundAndResume()
        {
            var service = new DanChallengeService();
            typeof(DanChallengeService).GetProperty(nameof(service.RestRemaining)).SetValue(service, 50f);
            typeof(DanChallengeService).GetProperty(nameof(service.Phase)).SetValue(service, DanPhase.BetweenSongs);
            service.UpdateRest(1);
            service.SetApplicationPaused(true, 2);
            service.UpdateRest(10);
            service.SetApplicationPaused(false, 100);
            service.UpdateRest(101);
            Assert.That(service.RestRemaining, Is.EqualTo(47));
        }

        [Test]
        public void DanRestCountsTheFullForegroundIntervalAtFourFramesPerSecond()
        {
            float remaining = DanChallengeService.REST_SECONDS;
            for (int frame = 0; frame < 200; frame++)
                remaining = DanChallengeService.RestAfterFrame(remaining, 0.25f);
            Assert.That(remaining, Is.Zero);
        }

        [Test]
        public void ShareRoundTripCanonicalizesWrappersButPreservesSongOrderAndStyle()
        {
            var dan = Definition();
            var copy = DanDefinition.Parse(dan.ShareText.Replace("\n", "\r\n")
                .Replace("https://charts.test/levels/a", "https://open.sonolus.com/CHARTS.test/levels/a"));
            Assert.That(copy.ShareText, Is.EqualTo(dan.ShareText));
            Assert.That(copy.Id, Is.EqualTo(dan.Id));
            var reversed = new DanDefinition(100, dan.Damage, dan.Songs.Reverse(), dan.Label, dan.Emblem, dan.Color);
            Assert.That(reversed.Id, Is.Not.EqualTo(dan.Id));
            Assert.That(Definition("新牌面").Id, Is.Not.EqualTo(dan.Id));
        }

        [Test]
        public void TierAndGradeTravelInTheShareTextAndAPlainPlateKeepsItsOldText()
        {
            var graded = DanDefinition.Parse(Graded().ShareText);
            Assert.That(graded.Tier, Is.EqualTo(MedalTiers.TOP));
            Assert.That(graded.Grade, Is.EqualTo("ω"));
            Assert.That(graded.Id, Is.EqualTo(Graded().Id));
            var plain = Definition();
            Assert.That(graded.Id, Is.Not.EqualTo(plain.Id));
            Assert.That(plain.ShareText, Does.EndWith("\"style\":{\"label\":\"双曲\",\"emblem\":\"diamond\",\"color\":\"#526DC8\"}}"));
            var spelledOut = DanDefinition.Parse(plain.ShareText.Replace("\"#526DC8\"", "\"#526DC8\",\"tier\":0"));
            Assert.That(spelledOut.Id, Is.EqualTo(plain.Id));
            Assert.That(spelledOut.Grade, Is.Null);
        }

        [Test]
        public void SharedWebSongMatchesRedownloadedChartAndAudioWithoutSharingDownloadUrl()
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Dan-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var author = new SongPackageManager(Path.Combine(root, "author"));
                var reader = new SongPackageManager(Path.Combine(root, "reader"));
                var original = author.CreateNewEntry();
                File.WriteAllText(original.AudioPath, "matching audio");
                const string PAGE = "https://charts.test/song?id=曲#attachments";
                SongPackageSource.FromWeb(PAGE, "https://cdn.test/chart.zip?secret=temporary").WriteWeb(original.RootDirectory);
                string reference = DanLibrarySong.ReferenceFor(author.RescanItems().Single());
                var dan = new DanDefinition(100, Definition().Damage, new[] { reference, _songs[1] }, "混合", "diamond", "#112233");
                Assert.That(dan.ShareText, Does.Not.Contain("temporary"));
                var copy = DanDefinition.Parse(dan.ShareText);
                Assert.That(copy.Id, Is.EqualTo(dan.Id));
                Assert.That(DanLibrarySong.TryParse(copy.Songs[0], out var song), Is.True);
                Assert.That(song.PageUrl, Is.EqualTo(PAGE));
                Assert.That(song.FindInstalled(reader), Is.Null);

                string zip = author.ExportZip(original, Path.Combine(root, "chart.zip"));
                var imported = reader.ImportZip(zip);
                Assert.That(song.FindInstalled(reader)?.Entry.RootDirectory, Is.EqualTo(imported.RootDirectory));
                File.WriteAllText(imported.AudioPath, "different audio");
                Assert.That(song.FindInstalled(reader), Is.Null);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase("initialLife", "0")]
        [TestCase("damage.great", "-1")]
        [TestCase("damage.miss", "null")]
        [TestCase("songs", "[]")]
        [TestCase("songs", "[\"https://charts.test/levels/a\"]")]
        [TestCase("songs[1]", "\"https://charts.test\"")]
        [TestCase("style.color", "\"green\"")]
        [TestCase("style.tier", "5")]
        [TestCase("style.tier", "\"2\"")]
        [TestCase("style.grade", "\"XI\"")]
        [TestCase("style.grade", "3")]
        public void InvalidShareIdentifiesField(string field, string replacement)
        {
            var share = Graded().ShareText.Split('\n');
            var body = JObject.Parse(share[1]);
            body.SelectToken(field).Replace(JToken.Parse(replacement));
            var error = Assert.Throws<DanFieldException>(() => DanDefinition.Parse(share[0] + "\n" + body));
            Assert.That(error.Field, Is.EqualTo(field));
        }

        private static DanSongResult Row(int start, int life, PlayResultKind kind) =>
            new("sample-key", "sample title", kind, 42, start, new LiveScore { life = life, missCount = 1 });

        [TestCase(0, DanOutcome.Failed)]
        [TestCase(1, DanOutcome.Passed)]
        public void CheckpointsAwardsAndEquipmentSurviveRestoreAndHistoryDeletion(int life, DanOutcome outcome)
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Dan-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                string source = Path.Combine(root, "player");
                var store = new PlayerProfileStore(source);
                var dan = Definition();
                store.SaveDanDefinition(dan);
                store.SaveDanDefinition(DanDefinition.Parse(dan.ShareText));
                var first = DanAttempt.Begin(dan).Append(dan, Row(100, 63, PlayResultKind.Completed));
                store.SaveDanAttempt(dan, first);
                store = new PlayerProfileStore(source);
                var stopped = store.ReadDanHistory().Single();
                Assert.That(stopped.Outcome, Is.EqualTo(DanOutcome.Interrupted));
                Assert.That(stopped.SongNumber(dan), Is.EqualTo(2));
                Assert.Throws<InvalidOperationException>(() => store.WearMedal(Medal.DanKey(dan.Id)));
                var last = first.Append(dan, Row(stopped.RemainingLife(dan), life, life == 0 ? PlayResultKind.Failed : PlayResultKind.Completed));
                Assert.That(store.SaveDanAttempt(dan, last), Is.EqualTo(life > 0));
                Assert.That(store.SaveDanAttempt(dan, last), Is.False);
                Assert.That(last.Outcome, Is.EqualTo(outcome));
                if (life > 0) store.WearMedal(Medal.DanKey(dan.Id));
                store.SaveDanDefinition(Definition("改版"));
                Assert.That(store.ReadDanDefinitions().Count, Is.EqualTo(2));
                Assert.That(store.ReadDanHistory().Count, Is.EqualTo(1));
                Assert.That(store.ReadHistory(0, 10).TotalCount, Is.Zero);
                Assert.That(store.Read().Best30.Total, Is.Zero);
                string backup = Path.Combine(root, "dan.zip");
                store.ExportBackup(backup);
                store.DeleteAllHistory();
                Assert.That(store.ReadDanHistory().Single().Id, Is.EqualTo(last.Id));

                var restored = new PlayerProfileStore(Path.Combine(root, "other"));
                var preview = restored.PreviewBackup(backup);
                Assert.That(preview.DanDefinitionsAdded, Is.EqualTo(2));
                Assert.That(preview.DanAttemptsAdded, Is.EqualTo(1));
                var profile = restored.ConfirmImport(preview.Id);
                Assert.That(profile.WornMedals[0]?.Dan.Id, Is.EqualTo(life > 0 ? dan.Id : null));
                Assert.That(restored.ReadDanHistory().Single().Songs[1].Score.life, Is.EqualTo(life));
                var repeat = restored.PreviewBackup(backup);
                Assert.That(repeat.DanDuplicates, Is.EqualTo(3));
                restored.CancelImport(repeat.Id);
                var checkpointStore = new PlayerProfileStore(Path.Combine(root, "checkpoint"));
                checkpointStore.SaveDanAttempt(dan, first);
                var conflict = checkpointStore.PreviewBackup(backup);
                Assert.That(conflict.DanConflictingIds, Is.EqualTo(new[] { first.Id }));
                Assert.That(checkpointStore.ConfirmImport(conflict.Id).EarnedDanIds, Is.Empty);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DeleteProtectsActiveCourseAndFailedWriteLeavesEarnedPlateIntact()
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Dan-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var store = new PlayerProfileStore(root);
                var dan = Definition();
                var other = Definition("另一段位");
                var passed = DanAttempt.Begin(dan).Append(dan, Row(100, 63, PlayResultKind.Completed))
                    .Append(dan, Row(63, 1, PlayResultKind.Completed));
                store.SaveDanAttempt(dan, passed);
                store.SaveDanDefinition(other);
                store.WearMedal(Medal.DanKey(dan.Id));
                store.ActiveDanId = dan.Id;
                Assert.Throws<InvalidOperationException>(() => store.DeleteDanDefinition(dan.Id));
                store.DeleteDanDefinition(other.Id);
                store.ActiveDanId = null;
                string path = Path.Combine(root, "dan.json");
                File.Move(path, path + ".original");
                Directory.CreateDirectory(path);
                var error = Assert.Catch(() => store.DeleteDanDefinition(dan.Id));
                Assert.That(error, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
                Assert.That(store.Read().IsWorn(Medal.DanKey(dan.Id)), Is.True);
                Directory.Delete(path);
                File.Move(path + ".original", path);
                var reopened = new PlayerProfileStore(root);
                Assert.That(reopened.ReadDanHistory().Single().Id, Is.EqualTo(passed.Id));
                Assert.That(reopened.ReadDanDefinitions().Single().Id, Is.EqualTo(dan.Id));
                Assert.That(reopened.Read().IsWorn(Medal.DanKey(dan.Id)), Is.True);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DanOnlyMergeCanBeConfirmedOnAnExistingProfileAndEditsInvalidatePreview()
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Dan-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var source = new PlayerProfileStore(Path.Combine(root, "source"));
                var target = new PlayerProfileStore(Path.Combine(root, "target"));
                target.SetAppearance("Local", "default");
                var dan = Definition();
                source.SaveDanDefinition(dan);
                string path = Path.Combine(root, "player.zip");
                source.ExportBackup(path);
                var preview = target.PreviewBackup(path);
                Assert.That(preview.HasChanges, Is.True);
                Assert.That(preview.Added, Is.Zero);
                Assert.That(preview.RestoresAppearance, Is.False);
                target.SaveDanDefinition(Definition("本地"));
                Assert.Throws<InvalidOperationException>(() => target.ConfirmImport(preview.Id));
                target.ConfirmImport(target.PreviewBackup(path).Id);
                Assert.That(target.ReadDanDefinitions().Count, Is.EqualTo(2));
                Assert.That(target.Read().Appearance.Nickname, Is.EqualTo("Local"));
                Assert.That(target.PreviewBackup(path).HasChanges, Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ImportingAVersionOneBackupPreservesLocalChallenges()
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Dan-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var store = new PlayerProfileStore(root);
                var dan = Definition();
                store.SaveDanDefinition(dan);
                string path = Path.Combine(root, "legacy.zip");
                store.ExportBackup(path);
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    zip.GetEntry("dan.json").Delete();
                    zip.GetEntry("achievements.json").Delete();
                    var entry = zip.GetEntry("manifest.json");
                    JObject manifest;
                    using (var reader = new StreamReader(entry.Open())) manifest = JObject.Parse(reader.ReadToEnd());
                    manifest["version"] = 1;
                    manifest.Remove("danSha256");
                    manifest.Remove("achievementsSha256");
                    entry.Delete();
                    using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
                    writer.Write(manifest);
                }
                store.ConfirmImport(store.PreviewBackup(path).Id);
                Assert.That(store.ReadDanDefinitions().Single().Id, Is.EqualTo(dan.Id));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
