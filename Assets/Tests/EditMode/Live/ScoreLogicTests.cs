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

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(64)]
        [TestCase(221)]
        [TestCase(348)]
        [TestCase(1000)]
        [TestCase(2019)]
        public void AllJustPerfectSumsToExactlyOneMillion(int noteCount)
        {
            ScoreLogic logic = Setup(noteCount);
            int total = 0;

            for (int i = 0; i < noteCount; i++)
            {
                total += logic.CalculateAddScore(new TestNote(NoteResult.JustPerfect));
            }

            Assert.That(total, Is.EqualTo(1000000), $"n={noteCount}");
            Assert.That(logic.Score.totalScore, Is.EqualTo(1000000));
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
                running += logic.CalculateAddScore(new TestNote(NoteResult.JustPerfect));
                Assert.That(running, Is.LessThanOrEqualTo(1000000), $"after {i + 1} notes");
            }
        }

        [Test]
        public void PerfectPaysTheSameAsAJustPerfect()
        {
            // The tighter window is a visual nuance: an all-PERFECT play has to reach 1,000,000.
            ScoreLogic logic = Setup(1000);
            int baseScore = logic.BaseNoteScore;

            int gained = logic.CalculateAddScore(new TestNote(NoteResult.Perfect));

            Assert.That(gained, Is.EqualTo(baseScore));
        }

        [Test]
        public void AnAllPerfectPlayReachesTheFullScore()
        {
            const int notes = 2019;
            ScoreLogic logic = Setup(notes);
            int total = 0;

            for (int i = 0; i < notes; i++)
            {
                total += logic.CalculateAddScore(new TestNote(NoteResult.Perfect));
            }

            Assert.That(total, Is.EqualTo(1_000_000));
        }

        [Test]
        public void GreatAndGoodPaySeventyAndFiftyPercent()
        {
            ScoreLogic perfectLogic = Setup(1000);
            int baseScore = perfectLogic.BaseNoteScore;

            int great = perfectLogic.CalculateAddScore(new TestNote(NoteResult.Great));
            int good = perfectLogic.CalculateAddScore(new TestNote(NoteResult.Good));

            Assert.That(great, Is.EqualTo(Mathf.RoundToInt(baseScore * 0.7f)));
            Assert.That(good, Is.EqualTo(Mathf.RoundToInt(baseScore * 0.5f)));
        }

        [Test]
        public void SkillFactorScalesTheGain()
        {
            ScoreLogic logic = Setup(1000);
            int baseScore = logic.BaseNoteScore;

            int doubled = logic.CalculateAddScore(new TestNote(NoteResult.JustPerfect), 2f);

            Assert.That(doubled, Is.EqualTo(baseScore * 2));
        }

        [Test]
        public void DecorationNotesScoreNothing()
        {
            ScoreLogic logic = Setup(10);

            int gained = logic.CalculateAddScore(new TestNote(NoteResult.JustPerfect, decoration: true));

            Assert.That(gained, Is.EqualTo(0));
            Assert.That(logic.Score.totalScore, Is.EqualTo(0));
        }

        [Test]
        public void MissAndBadScoreNothing()
        {
            ScoreLogic logic = Setup(10);

            Assert.That(logic.CalculateAddScore(new TestNote(NoteResult.Miss)), Is.EqualTo(0));
            Assert.That(logic.CalculateAddScore(new TestNote(NoteResult.Bad)), Is.EqualTo(0));
        }

        [Test]
        public void NoteResultCountersFollowTheJudgement()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateNoteResult(new TestNote(NoteResult.JustPerfect));

            Assert.That(logic.Score.justPerfectCount, Is.EqualTo(1));
            Assert.That(logic.Score.perfectCount, Is.EqualTo(1), "a just perfect also counts as perfect");

            logic.UpdateNoteResult(new TestNote(NoteResult.Great));
            logic.UpdateNoteResult(new TestNote(NoteResult.Miss));

            Assert.That(logic.Score.greatCount, Is.EqualTo(1));
            Assert.That(logic.Score.missCount, Is.EqualTo(1));
        }

        [Test]
        public void DecorationNotesDoNotTouchCounters()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateNoteResult(new TestNote(NoteResult.JustPerfect, decoration: true));

            Assert.That(logic.Score.justPerfectCount, Is.EqualTo(0));
            Assert.That(logic.Score.perfectCount, Is.EqualTo(0));
        }

        [Test]
        public void ComboGrowsOnGreatOrBetterAndResetsBelow()
        {
            ScoreLogic logic = Setup(10);

            logic.UpdateCombo(new TestNote(NoteResult.JustPerfect));
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
            Assert.That(logic.CalculateAddScore(new TestNote(NoteResult.JustPerfect)), Is.EqualTo(0));
        }
    }
}
