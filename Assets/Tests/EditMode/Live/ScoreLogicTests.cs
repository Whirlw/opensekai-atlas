using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Scoring is the one place where a rounding bug turns a full-combo run into 999,405 instead of
    /// 1,000,000, so the cumulative split is pinned for several note counts.
    /// </summary>
    public class ScoreLogicTests
    {
        /// <summary>NoteBase exposes its judgement only through a protected setter, so the tests set it here.</summary>
        private sealed class TestNote : NoteBase
        {
            public TestNote(NoteResult result, bool decoration = false)
                : base(new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f), 1, 0, 0, NoteCategory.Normal, 1f, 0f)
            {
                IsDecoration = decoration;
                JudgeInfo = (result, NoteResultDescription.None);
            }
        }

        private LiveBundleBuildData _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LiveBundleBuildData>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        private static MusicScore ScoreWithNotes(int noteCount)
        {
            MusicScoreInfo info = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);
            var roots = new NoteBase[noteCount];
            for (int i = 0; i < noteCount; i++)
            {
                roots[i] = new NoteBase(info, i + 1, 0, 0, NoteCategory.Normal, 1f, 0f);
            }

            return new MusicScore(new[] { info }, new EventBase[0], roots);
        }

        private ScoreLogic Setup(int noteCount)
        {
            var logic = new ScoreLogic(_config);
            logic.Setup(null, ScoreWithNotes(noteCount));
            return logic;
        }

        [TestCase(0, 0, 0)]
        [TestCase(2, 500, 500)]
        [TestCase(39, 385, 500)]
        [TestCase(41, 366, 488)]
        [TestCase(999, 16, 21)]
        [TestCase(1000, 15, 20)]
        [TestCase(2000, 8, 10)]
        public void NormalDamageScalesInverselyWithNoteCountAndCapsAt500(int noteCount, int badDamage, int missDamage)
        {
            var logic = Setup(noteCount);
            var bad = new TestNote(NoteResult.Bad);
            logic.UpdateCombo(bad);
            logic.Damage(bad);
            Assert.That(logic.Score.life, Is.EqualTo(1000 - badDamage));

            var miss = new TestNote(NoteResult.Miss);
            logic.UpdateCombo(miss);
            logic.Damage(miss);
            Assert.That(logic.Score.life, Is.EqualTo(1000 - badDamage - missDamage));
        }

        [Test]
        public void DanDamageUsesFiveGradesAndHazardsOnlyOnContactThenRestoresChartLife()
        {
            var logic = Setup(1);
            var chart = new MusicScore(null, null, null, initialLife: 325);
            var boot = new FreeLiveBootData(1, "master", 1);
            boot.DanSong = new OpenSekai.Shared.Playback.DanLiveSong(100, 63,
                new OpenSekai.Shared.Playback.DanDamage(1, 2, 3, 4, 10), null, null, null);
            logic.Setup(boot, chart);
            Assert.That(logic.Score.life, Is.EqualTo(63));
            foreach (var grade in new[] { NoteResult.Perfect, NoteResult.Great,
                         NoteResult.Good, NoteResult.Bad, NoteResult.Miss })
                logic.Damage(new TestNote(grade));
            logic.Damage(new TestNote(NoteResult.Miss, decoration: true));
            Assert.That(logic.Score.life, Is.EqualTo(43));
            var hazard = new NextRushDamageNote(default, 1, 0, 0);
            hazard.Resolve(false);
            logic.Damage(hazard);
            Assert.That(logic.Score.life, Is.EqualTo(43));
            hazard = new NextRushDamageNote(default, 2, 0, 0);
            hazard.Resolve(true);
            logic.Damage(hazard);
            Assert.That(logic.Score.life, Is.EqualTo(33));
            boot.DanSong = null;
            logic.Setup(boot, chart);
            Assert.That(logic.Score.life, Is.EqualTo(325));
            logic.Damage(hazard);
            Assert.That(logic.Score.life, Is.EqualTo(325 - NextRushDamageNote.LIFE_PENALTY));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(64)]
        [TestCase(221)]
        [TestCase(348)]
        [TestCase(1000)]
        [TestCase(2019)]
        public void AllPerfectSumsToExactlyOneMillion(int noteCount)
        {
            ScoreLogic logic = Setup(noteCount);
            int total = 0;

            for (int i = 0; i < noteCount; i++)
            {
                total += logic.CalculateAddScore(new TestNote(NoteResult.Perfect));
            }

            Assert.That(total, Is.EqualTo(1000000), $"n={noteCount}");
            Assert.That(logic.Score.totalScore, Is.EqualTo(1000000));
        }

        [Test]
        public void SubtractiveHudDeductsOnlyLostPointsAndRetirementUsesEarnedPoints()
        {
            var boot = new FreeLiveBootData(1, "master", 1)
            {
                LiveSettingData = new LiveSettingData { UseSubtractiveScore = true }
            };
            var chart = ScoreWithNotes(6);
            var logic = new ScoreLogic(_config);
            logic.Setup(boot, chart);
            Assert.That(logic.HudScore.totalScore, Is.EqualTo(1_000_000));
            Assert.That(logic.HudScore.rank, Is.EqualTo(ScoreRank.S));
            Assert.That(logic.Score.totalScore, Is.Zero);

            void Judge(NoteResult grade, int expectedHud, bool decoration = false)
            {
                var note = new TestNote(grade, decoration);
                logic.UpdateCombo(note);
                logic.CalculateAddScore(note);
                Assert.That(logic.HudScore.totalScore, Is.EqualTo(expectedHud), grade.ToString());
            }

            Judge(NoteResult.Miss, 1_000_000, decoration: true);
            Judge(NoteResult.Perfect, 1_000_000);
            Judge(NoteResult.Great, 975_000);
            var retired = OpenSekai.Shared.Playback.PlayResult.FromBootData(
                boot, OpenSekai.Shared.Playback.PlayResultKind.Retired, logic.Score);
            Assert.That(retired.Score, Is.EqualTo(308_333));
            Assert.That(retired.Rank, Is.EqualTo(ScoreRank.D));
            Judge(NoteResult.Good, 891_667);
            Judge(NoteResult.Bad, 758_333);
            Judge(NoteResult.Miss, 591_667);
            Judge(NoteResult.Auto, 591_667);
            Assert.That(logic.HudScore.totalScore, Is.EqualTo(logic.Score.totalScore));

            logic.Setup(boot, chart);
            Assert.That(logic.HudScore.totalScore, Is.EqualTo(1_000_000));
            boot.LiveSettingData.UseSubtractiveScore = false;
            logic.Setup(boot, chart);
            Assert.That(logic.HudScore.totalScore, Is.Zero);
        }

        [Test]
        public void AllAutoSumsToExactlyOneMillion()
        {
            ScoreLogic logic = Setup(221);
            int total = 0;

            for (int i = 0; i < 221; i++)
            {
                total += logic.CalculateAddScore(new TestNote(NoteResult.Auto));
            }

            Assert.That(total, Is.EqualTo(1000000));
        }

        [Test]
        public void ConsecutiveScoresNeverOvershootTheMaximum()
        {
            ScoreLogic logic = Setup(221);
            int running = 0;

            for (int i = 0; i < 221; i++)
            {
                running += logic.CalculateAddScore(new TestNote(NoteResult.Perfect));
                Assert.That(running, Is.LessThanOrEqualTo(1000000), $"after {i + 1} notes");
            }
        }

        [TestCase(NoteResult.Perfect, 1000)]
        [TestCase(NoteResult.Great, 850)]
        [TestCase(NoteResult.Good, 500)]
        [TestCase(NoteResult.Bad, 200)]
        [TestCase(NoteResult.Miss, 0)]
        public void JudgementAwardsItsShareOfTheNoteBaseScore(NoteResult grade, int expected)
        {
            ScoreLogic logic = Setup(1000);
            Assert.That(logic.CalculateAddScore(new TestNote(grade)), Is.EqualTo(expected));
        }

        [Test]
        public void SkillFactorScalesTheGain()
        {
            ScoreLogic logic = Setup(1000);
            int baseScore = logic.BaseNoteScore;

            int doubled = logic.CalculateAddScore(new TestNote(NoteResult.Perfect), 2f);

            Assert.That(doubled, Is.EqualTo(baseScore * 2));
        }

        [Test]
        public void DecorationNotesScoreNothing()
        {
            ScoreLogic logic = Setup(10);

            int gained = logic.CalculateAddScore(new TestNote(NoteResult.Perfect, decoration: true));

            Assert.That(gained, Is.EqualTo(0));
            Assert.That(logic.Score.totalScore, Is.EqualTo(0));
        }

        [Test]
        public void NoteResultCountersFollowTheJudgement()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateNoteResult(new TestNote(NoteResult.Perfect));

            Assert.That(logic.Score.perfectCount, Is.EqualTo(1));

            logic.UpdateNoteResult(new TestNote(NoteResult.Great));
            logic.UpdateNoteResult(new TestNote(NoteResult.Miss));

            Assert.That(logic.Score.greatCount, Is.EqualTo(1));
            Assert.That(logic.Score.missCount, Is.EqualTo(1));
        }

        [Test]
        public void DecorationNotesDoNotTouchCounters()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateNoteResult(new TestNote(NoteResult.Perfect, decoration: true));

            Assert.That(logic.Score.perfectCount, Is.EqualTo(0));
        }

        [Test]
        public void ComboGrowsOnGreatOrBetterAndResetsBelow()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateCombo(new TestNote(NoteResult.Perfect));
            logic.UpdateCombo(new TestNote(NoteResult.Great));
            Assert.That(logic.Score.combo, Is.EqualTo(2));
            Assert.That(logic.Score.maxCombo, Is.EqualTo(2));

            logic.UpdateCombo(new TestNote(NoteResult.Good));
            Assert.That(logic.Score.combo, Is.EqualTo(0));
            Assert.That(logic.Score.maxCombo, Is.EqualTo(2));
        }

        [Test]
        public void EmptyScoreKeepsBaseNoteScoreAtZero()
        {
            ScoreLogic logic = Setup(0);

            Assert.That(logic.BaseNoteScore, Is.EqualTo(0));
            Assert.That(logic.CalculateAddScore(new TestNote(NoteResult.Perfect)), Is.EqualTo(0));
        }
    }
}
