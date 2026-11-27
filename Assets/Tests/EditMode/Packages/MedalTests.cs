using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class MedalTests
    {
        private static readonly string _dan = Medal.AchievementKey("HIT-01");
        private string _root;
        private PlayerProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Medals-" + Guid.NewGuid().ToString("N"))).FullName;
            _store = new PlayerProfileStore(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void PassedCoursesAndUnlockedAchievementsAreTheMedals()
        {
            Assert.That(_store.ReadMedals(), Is.Empty);
            _store.SaveDanDefinition(Course("未通过"));
            string first = Pass("一段", unlock: false);
            Assert.That(_store.ReadMedals().Select(medal => medal.Key), Is.EqualTo(new[] { first }), "reached, not unlocked yet");
            string second = Pass("二段");
            Assert.That(_store.ReadMedals().Select(medal => medal.Key), Is.EqualTo(new[] { second, first, _dan }));
            Assert.That(_store.ReadMedals().Select(medal => medal.Kind),
                Is.EqualTo(new[] { MedalKind.Dan, MedalKind.Dan, MedalKind.Achievement }));
            Assert.That(_store.Read().WornMedals, Is.EqualTo(new Medal[Medal.SLOTS]));
        }

        [Test]
        public void TwoSlotsFillInOrderThenTheLastGivesWay()
        {
            string first = Pass("一段");
            string second = Pass("二段");
            Assert.That(_store.WearMedal(first), Is.EqualTo(0));
            Assert.That(_store.WearMedal(_dan), Is.EqualTo(1));
            Assert.That(_store.WearMedal(first), Is.EqualTo(0));
            AssertWorn(first, _dan);
            Assert.That(_store.WearMedal(second), Is.EqualTo(1));
            AssertWorn(first, second);
            _store.RemoveMedal(first);
            AssertWorn(null, second);
            Assert.That(_store.WearMedal(_dan), Is.EqualTo(0));
            AssertWorn(_dan, second);
        }

        [Test]
        public void AMedalWornInTheOtherSlotTradesPlaces()
        {
            string first = Pass("一段");
            _store.WearMedal(0, first);
            _store.WearMedal(1, _dan);
            _store.WearMedal(0, _dan);
            AssertWorn(_dan, first);
            _store.WearMedal(1, null);
            AssertWorn(_dan, null);
            _store.WearMedal(1, _dan);
            AssertWorn(null, _dan);
            Assert.Throws<ArgumentOutOfRangeException>(() => _store.WearMedal(Medal.SLOTS, first));
            Assert.Throws<InvalidOperationException>(() => _store.WearMedal(0, Medal.AchievementKey("first_ap")));
            Assert.Throws<InvalidOperationException>(() => _store.WearMedal(0, "unknown:1"));
        }

        [Test]
        public void DeletedCourseFreesItsSlotAndTheClaimedAchievementStaysWorn()
        {
            string first = Pass("一段");
            string second = Pass("二段");
            _store.WearMedal(first);
            _store.WearMedal(_dan);
            _store.DeleteDanDefinition(Medal.DanIdOf(first));
            AssertWorn(null, _dan);
            _store.DeleteDanDefinition(Medal.DanIdOf(second));
            AssertWorn(null, _dan);
            string third = Pass("三段");
            AssertWorn(null, _dan);
            Assert.That(_store.WearMedal(third), Is.EqualTo(0));
            AssertWorn(third, _dan);
        }

        [Test]
        public void ABackupRestoresBothSlotsOnlyIntoAnEmptyProfile()
        {
            string first = Pass("一段");
            _store.WearMedal(first);
            _store.WearMedal(_dan);
            string zip = Path.Combine(_root, "player.zip");
            _store.ExportBackup(zip);

            var fresh = new PlayerProfileStore(Path.Combine(_root, "fresh"));
            var preview = fresh.PreviewBackup(zip);
            Assert.That(preview.Result.WornMedals.Select(medal => medal?.Key), Is.EqualTo(new[] { first, _dan }));
            fresh.ConfirmImport(preview.Id);
            Assert.That(fresh.Read().WornMedals.Select(medal => medal?.Key), Is.EqualTo(new[] { first, _dan }));

            var used = new PlayerProfileStore(Path.Combine(_root, "used"));
            used.SetAppearance("Local", "default");
            used.ConfirmImport(used.PreviewBackup(zip).Id);
            Assert.That(used.Read().WornMedals, Is.All.Null);
            Assert.That(used.ReadMedals().Count, Is.EqualTo(2));
        }

        [Test]
        public void AnArchiveThatWoreOneCourseKeepsItInTheFirstSlot()
        {
            string first = Pass("一段");
            string path = Path.Combine(_root, PlayerProfileStore.DAN_FILE);
            JObject archive = JObject.Parse(File.ReadAllText(path));
            archive.Remove("Worn");
            archive["Equipped"] = Medal.DanIdOf(first);
            File.WriteAllText(path, archive.ToString());
            _store = new PlayerProfileStore(_root);
            AssertWorn(first, null);
            _store.WearMedal(_dan);
            AssertWorn(first, _dan);
            Assert.That(JObject.Parse(File.ReadAllText(path)).ContainsKey("Equipped"), Is.False);

            archive.Remove("Equipped");
            archive["Worn"] = new JArray(first, first);
            Assert.Throws<InvalidDataException>(() => DanArchive.Decode(archive.ToString()));
            archive["Worn"] = new JArray(Medal.DanKey("missing"), null);
            Assert.Throws<InvalidDataException>(() => DanArchive.Decode(archive.ToString()));
        }

        [Test]
        public void GradesRunFromRomanNumeralsToGreekLetters()
        {
            Assert.That(MedalGrades.All.Count, Is.EqualTo(MedalGrades.ROMAN_COUNT + 24));
            Assert.That(MedalGrades.All, Is.Unique);
            Assert.That(MedalGrades.All[0], Is.EqualTo("I"));
            Assert.That(MedalGrades.All[MedalGrades.ROMAN_COUNT - 1], Is.EqualTo("X"));
            Assert.That(MedalGrades.All[MedalGrades.ROMAN_COUNT], Is.EqualTo("α"));
            Assert.That(MedalGrades.All[MedalGrades.All.Count - 1], Is.EqualTo("ω"));
            Assert.That(MedalGrades.All.Count(MedalGrades.IsRoman), Is.EqualTo(MedalGrades.ROMAN_COUNT));
        }

        [Test]
        public void OnlyCoursesCarryThePattern()
        {
            Pass("一段");
            var looks = _store.ReadMedals().Select(MedalLook.Of).ToArray();
            Assert.That(looks[0].Patterned, Is.True);
            Assert.That(looks[0].Grade, Is.EqualTo("III"));
            Assert.That(looks[0].Tier, Is.EqualTo(2));
            Assert.That(looks[1].Patterned, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryTierAboveTheLowestDrawsItsOwnSymmetricFrame(bool compact)
        {
            byte[] previous = null;
            for (int tier = MedalTiers.LOWEST + 1; tier <= MedalTiers.TOP; tier++)
            {
                byte[] strokes;
                int width, height;
                if (compact)
                {
                    strokes = MedalArt.RenderCompactFrame(tier, 5f, 48f, out width, out height);
                }
                else
                {
                    MedalArt.RenderFrame(tier, 5f, 48f, out strokes, out byte[] lights, out width, out height);
                    Assert.That(lights.Any(value => value != 0), Is.True);
                }

                Assert.That(previous == null || !strokes.SequenceEqual(previous), Is.True, "tier " + tier);
                for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    byte alpha = strokes[(row * width + column) * 4 + 3];
                    Assert.That(strokes[(row * width + width - 1 - column) * 4 + 3], Is.EqualTo(alpha));
                    Assert.That(strokes[((height - 1 - row) * width + column) * 4 + 3], Is.EqualTo(alpha));
                }

                // Nothing may be cut off at the texture's edge.
                for (int column = 0; column < width; column++) Assert.That(strokes[column * 4 + 3], Is.LessThan(16), "tier " + tier);
                for (int row = 0; row < height; row++) Assert.That(strokes[row * width * 4 + 3], Is.LessThan(16), "tier " + tier);
                previous = strokes;
            }
        }

        private void AssertWorn(string first, string second) =>
            Assert.That(_store.Read().WornMedals.Select(medal => medal?.Key), Is.EqualTo(new[] { first, second }));

        private static DanDefinition Course(string label) => new DanDefinition(10, new DanDamage(0, 1, 2, 3, 4),
            new[] { "https://test.org/levels/a", "https://test.org/levels/b" }, label, "star", "#112233", 2, "III");

        /// <param name="unlock">Also unlocks 万键之路, for its medal.</param>
        private string Pass(string label, bool unlock = true)
        {
            DanDefinition course = Course(label);
            DanAttempt attempt = DanAttempt.Begin(course);
            for (int i = 0; i < course.Songs.Count; i++)
            {
                attempt = attempt.Append(course, new DanSongResult("chart", "Title", PlayResultKind.Completed, 15, 10,
                    new LiveScore { life = 10, perfectCount = 5000, totalComboCount = 5000 }));
            }

            _store.SaveDanAttempt(course, attempt);
            if (unlock) _store.UnlockAchievement("HIT-01");
            return Medal.DanKey(course.Id);
        }
    }
}
