using NUnit.Framework;
using OpenSekai.Shared.Scoring;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// The rank thresholds decide which letter the result panel shows, and the gauge uses the same
    /// numbers, so both are asserted against the same table.
    /// </summary>
    public class ScoreGaugeTests
    {
        private const int TOTAL = 1000000;

        [TestCase(-1, 0f)]
        [TestCase(450000, 0.225f)]
        [TestCase(900000, 0.45f)]
        [TestCase(925000, 0.525f)]
        [TestCase(950000, 0.60f)]
        [TestCase(962500, 0.675f)]
        [TestCase(975000, 0.75f)]
        [TestCase(982500, 0.825f)]
        [TestCase(990000, 0.90f)]
        [TestCase(995000, 0.95f)]
        public void ScoreFillsTheIntervalBetweenItsMarkers(int score, float fill)
        {
            Assert.That(ScoreGauge.Standard.ProgressOf(score), Is.EqualTo(fill).Within(0.00001f));
        }

        [TestCase(1000000, ScoreRank.S)]
        [TestCase(990000, ScoreRank.S)]
        [TestCase(989999, ScoreRank.A)]
        [TestCase(975000, ScoreRank.A)]
        [TestCase(974999, ScoreRank.B)]
        [TestCase(950000, ScoreRank.B)]
        [TestCase(949999, ScoreRank.C)]
        [TestCase(900000, ScoreRank.C)]
        [TestCase(899999, ScoreRank.D)]
        [TestCase(0, ScoreRank.D)]
        public void RankFollowsTheThresholdTable(int score, ScoreRank expected)
        {
            Assert.That(ScoreGauge.Create(new MasterPlayLevelScore()).RankOf(score), Is.EqualTo(expected));
        }

        [Test]
        public void GaugeIsFullAtTheTopScore()
        {
            ScoreGauge gauge = ScoreGauge.Create(TOTAL);

            Assert.That(gauge.ProgressOf(TOTAL), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void GaugeIsEmptyAtZero()
        {
            ScoreGauge gauge = ScoreGauge.Create(TOTAL);

            Assert.That(gauge.ProgressOf(0), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void GaugeIsMonotonic()
        {
            ScoreGauge gauge = ScoreGauge.Create(TOTAL);
            float previous = -1f;

            for (int score = 0; score <= TOTAL; score += 50000)
            {
                float progress = gauge.ProgressOf(score);
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous), $"score={score}");
                previous = progress;
            }
        }

        [Test]
        public void GaugeToleratesAScoreAboveTheMaximum()
        {
            ScoreGauge gauge = ScoreGauge.Create(TOTAL);

            Assert.That(gauge.ProgressOf(TOTAL * 2), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
