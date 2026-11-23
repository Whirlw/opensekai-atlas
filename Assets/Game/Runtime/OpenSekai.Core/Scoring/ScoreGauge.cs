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
        public const float MARKER_S = 0.9f;
        public const float MARKER_A = 0.75f;
        public const float MARKER_B = 0.6f;
        public const float MARKER_C = 0.45f;

        public const int MAXIMUM_SCORE = 1_000_000;
        public const int SCORE_C = 900_000;
        public const int SCORE_B = 950_000;
        public const int SCORE_A = 975_000;
        public const int SCORE_S = 990_000;
        public static readonly ScoreGauge Standard = Create(MAXIMUM_SCORE);

        private readonly int _maximum;

        private readonly int _rankS;
        private readonly int _rankA;
        private readonly int _rankB;
        private readonly int _rankC;

        private ScoreGauge(int rankS, int rankA, int rankB, int rankC, int maximum)
        {
            _maximum = maximum;
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
                return Standard;
            }

            return Create(score.s, score.a, score.b, score.c);
        }

        public static ScoreGauge Create(int totalScore)
        {
            int Threshold(int score) => (int)Math.Ceiling((decimal)score * totalScore / MAXIMUM_SCORE);
            return new ScoreGauge(Threshold(SCORE_S), Threshold(SCORE_A), Threshold(SCORE_B), Threshold(SCORE_C), totalScore);
        }

        public static ScoreGauge Create(int rankS, int rankA, int rankB, int rankC) =>
            new ScoreGauge(rankS, rankA, rankB, rankC, Math.Max(MAXIMUM_SCORE, rankS));

        public ScoreRank RankOf(int totalScore)
        {
            if (totalScore >= _rankS)
            {
                return ScoreRank.S;
            }

            if (totalScore >= _rankA)
            {
                return ScoreRank.A;
            }

            if (totalScore >= _rankB)
            {
                return ScoreRank.B;
            }

            return totalScore >= _rankC ? ScoreRank.C : ScoreRank.D;
        }

        /// <summary>
        /// Fill each score interval between its two HUD markers, including S to full score.
        /// </summary>
        public float ProgressOf(int totalScore)
        {
            if (totalScore <= 0) return 0f;
            if (totalScore >= _maximum) return 1f;
            if (totalScore >= _rankS) return Interpolate(_rankS, _maximum, MARKER_S, 1f, totalScore);

            if (totalScore >= _rankA)
            {
                return Interpolate(_rankA, _rankS, MARKER_A, MARKER_S, totalScore);
            }

            if (totalScore >= _rankB)
            {
                return Interpolate(_rankB, _rankA, MARKER_B, MARKER_A, totalScore);
            }

            if (totalScore >= _rankC)
            {
                return Interpolate(_rankC, _rankB, MARKER_C, MARKER_B, totalScore);
            }

            return Interpolate(0, _rankC, 0f, MARKER_C, totalScore);
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
