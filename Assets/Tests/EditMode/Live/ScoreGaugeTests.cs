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
        private const int Total = 1000000;

        private static MasterPlayLevelScore Thresholds() => new MasterPlayLevelScore
        {
            s = 900000, a = 750000, b = 600000, c = 450000
        };

        [Test]
        public void RankRatesMatchTheDesignTable()
        {
            Assert.That(ScoreGauge.RANK_RATE_S, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(ScoreGauge.RANK_RATE_A, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(ScoreGauge.RANK_RATE_B, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(ScoreGauge.RANK_RATE_C, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [TestCase(1000000, ScoreRank.S)]
        [TestCase(900000, ScoreRank.S)]
        [TestCase(899999, ScoreRank.A)]
        [TestCase(750000, ScoreRank.A)]
        [TestCase(749999, ScoreRank.B)]
        [TestCase(600000, ScoreRank.B)]
        [TestCase(599999, ScoreRank.C)]
        [TestCase(450000, ScoreRank.C)]
        [TestCase(449999, ScoreRank.D)]
        [TestCase(0, ScoreRank.D)]
        public void RankFollowsTheThresholdTable(int score, ScoreRank expected)
        {
            Assert.That(ScoreGauge.GetRank(Thresholds(), score), Is.EqualTo(expected));
        }

        [Test]
        public void GaugeIsFullAtTheTopScore()
        {
            ScoreGauge gauge = ScoreGauge.Create(Total);

            Assert.That(gauge.ProgressOf(Total), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void GaugeIsEmptyAtZero()
        {
            ScoreGauge gauge = ScoreGauge.Create(Total);

            Assert.That(gauge.ProgressOf(0), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void GaugeIsMonotonic()
        {
            ScoreGauge gauge = ScoreGauge.Create(Total);
            float previous = -1f;

            for (int score = 0; score <= Total; score += 50000)
            {
                float progress = gauge.ProgressOf(score);
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous), $"score={score}");
                previous = progress;
            }
        }

        [Test]
        public void GaugeToleratesAScoreAboveTheMaximum()
        {
            ScoreGauge gauge = ScoreGauge.Create(Total);

            Assert.That(gauge.ProgressOf(Total * 2), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
