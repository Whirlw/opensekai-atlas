using System;
using Sekai;
using Sekai.Core.Live;

namespace OpenSekai.Shared.Scoring
{
    /// <summary>
    /// Turns a running total into the gauge fill the score bar shows, and a total into a rank.
    /// The behaviour is pinned by the scoring tests; the shape is ours — no mutable list of
    /// key/value pairs, no constructor that takes one, and the thresholds are plain fields.
    /// </summary>
    public sealed class ScoreGauge
    {
        public const float RANK_RATE_S = 0.9f;
        public const float RANK_RATE_A = 0.75f;
        public const float RANK_RATE_B = 0.6f;
        public const float RANK_RATE_C = 0.45f;

        private const int DEFAULT_TOTAL_SCORE = 1000000;

        private readonly int _rankS;
        private readonly int _rankA;
        private readonly int _rankB;
        private readonly int _rankC;

        private ScoreGauge(int rankS, int rankA, int rankB, int rankC)
        {
            _rankS = rankS;
            _rankA = rankA;
            _rankB = rankB;
            _rankC = rankC;
        }

        /// <summary>Uses the chart's own thresholds, falling back to a perfect-score chart when it has none.</summary>
        public static ScoreGauge Create(MasterPlayLevelScore score)
        {
            if (score == null || (score.s <= 0 && score.a <= 0 && score.b <= 0 && score.c <= 0))
            {
                return Create(DEFAULT_TOTAL_SCORE);
            }

            return Create(score.s, score.a, score.b, score.c);
        }

        public static ScoreGauge Create(int totalScore)
        {
            float total = totalScore;
            return Create(
                (int)Math.Floor(RANK_RATE_S * total),
                (int)Math.Floor(RANK_RATE_A * total),
                (int)Math.Floor(RANK_RATE_B * total),
                (int)Math.Floor(RANK_RATE_C * total));
        }

        public static ScoreGauge Create(int rankS, int rankA, int rankB, int rankC)
        {
            return new ScoreGauge(rankS, rankA, rankB, rankC);
        }

        public static ScoreRank GetRank(MasterPlayLevelScore thresholds, int totalScore)
        {
            if (thresholds == null)
            {
                return ScoreRank.D;
            }

            if (totalScore >= thresholds.s)
            {
                return ScoreRank.S;
            }

            if (totalScore >= thresholds.a)
            {
                return ScoreRank.A;
            }

            if (totalScore >= thresholds.b)
            {
                return ScoreRank.B;
            }

            return totalScore >= thresholds.c ? ScoreRank.C : ScoreRank.D;
        }

        /// <summary>
        /// Fill ratio for the score bar: piecewise linear between 0 → C → B → A → S, clamped to 1.
        /// Above the S threshold the bar stays full.
        /// </summary>
        public float ProgressOf(int totalScore)
        {
            if (totalScore >= _rankS)
            {
                return 1f;
            }

            if (totalScore >= _rankA)
            {
                return Interpolate(_rankA, _rankS, RANK_RATE_A, RANK_RATE_S, totalScore);
            }

            if (totalScore >= _rankB)
            {
                return Interpolate(_rankB, _rankA, RANK_RATE_B, RANK_RATE_A, totalScore);
            }

            if (totalScore >= _rankC)
            {
                return Interpolate(_rankC, _rankB, RANK_RATE_C, RANK_RATE_B, totalScore);
            }

            return totalScore <= 0 ? 0f : Interpolate(0, _rankC, 0f, RANK_RATE_C, totalScore);
        }

        /// <summary>Splits one gauge between several players: each gets a share proportional to their score.</summary>
        public float[] ProgressOf(LiveScore[] scores)
        {
            if (scores == null)
            {
                return Array.Empty<float>();
            }

            var totals = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
            {
                totals[i] = scores[i].totalScore;
            }

            return ProgressOf(totals);
        }

        public float[] ProgressOf(int[] totals)
        {
            if (totals == null)
            {
                return Array.Empty<float>();
            }

            long sum = 0;
            for (int i = 0; i < totals.Length; i++)
            {
                sum += totals[i];
            }

            float totalProgress = ProgressOf((int)Math.Min(sum, int.MaxValue));
            var values = new float[totals.Length];
            for (int i = 0; i < totals.Length; i++)
            {
                float ratio = sum > 0 ? (float)totals[i] / sum : 0f;
                values[i] = Clamp01(totalProgress * ratio);
            }

            return values;
        }

        private static float Interpolate(int fromScore, int toScore, float fromValue, float toValue, int totalScore)
        {
            int span = toScore - fromScore;
            if (span <= 0)
            {
                return Clamp01(toValue);
            }

            float progress = (totalScore - fromScore) / (float)span;
            return Clamp01(fromValue + progress * (toValue - fromValue));
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
