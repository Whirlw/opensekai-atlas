using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;

namespace OpenSekai.App.Playback
{
    public enum SettlementExample
    {
        FirstClear, SameChartFullCombo, NewChartAllPerfectTierUp, OutsideBest30,
        Auto, Failed, Retired, SaveFailed
    }

    /// <summary>Deterministic, memory-only results for the result screen. Safe to call from a preview.</summary>
    public static class SettlementSamples
    {
        public static PlayResult Create(SettlementExample example)
        {
            var board = new Best30Index();
            var prior = new List<CustomMusicScorePlayHistoryRecord>();
            void Seed(string id, string chart, int chartLevel, LiveScore live)
            {
                var saved = Record(id, chart, chart, chartLevel, live, PlayResultKind.Completed);
                prior.Add(saved);
                board.Evaluate(saved, HistorySaveState.Saved);
            }
            var kind = PlayResultKind.Completed;
            var state = HistorySaveState.Saved;
            var mode = (RankingEligibility?)null;
            string key = "preview-chart";
            int level = 30;
            var score = NormalClear();
            switch (example)
            {
                case SettlementExample.FirstClear:
                    break;
                case SettlementExample.SameChartFullCombo:
                    Seed("prior", key, level, NormalClear());
                    score = FullCombo();
                    break;
                case SettlementExample.NewChartAllPerfectTierUp:
                case SettlementExample.OutsideBest30:
                    for (int i = 1; i <= 30; i++)
                        Seed("seed-" + i, "chart-" + i, i == 30 ? 26 : 27, AllPerfect());
                    if (example == SettlementExample.OutsideBest30)
                    {
                        level = 20;
                        Seed("off-board", key, level, NormalClear());
                        score = FullCombo();
                    }
                    else score = AllPerfect();
                    break;
                case SettlementExample.Auto:
                    score = new LiveScore { autoCount = 300, totalComboCount = 300, totalScore = 1000000, life = 1000 };
                    mode = RankingEligibility.Auto;
                    state = HistorySaveState.NotRecorded;
                    break;
                case SettlementExample.Failed:
                case SettlementExample.Retired:
                    kind = example == SettlementExample.Failed ? PlayResultKind.Failed : PlayResultKind.Retired;
                    score = new LiveScore { perfectCount = 90, missCount = 10, totalComboCount = 300,
                        totalScore = 300000, maxCombo = 90, rank = Sekai.ScoreRank.D, life = kind == PlayResultKind.Failed ? 0 : 500 };
                    break;
                case SettlementExample.SaveFailed:
                    Seed("prior", key, level, NormalClear());
                    score = FullCombo();
                    state = HistorySaveState.SaveFailed;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(example));
            }

            string title = "联调谱面 · " + level;
            var record = Record("preview-" + example, key, title, level, score, kind);
            IReadOnlyList<string> unlocks = Array.Empty<string>();
            if (state == HistorySaveState.Saved)
            {
                var before = AchievementIndex.Build(prior, null).Snapshot();
                prior.Add(record);
                unlocks = AchievementIndex.NewUnlocks(before, AchievementIndex.Build(prior, null).Snapshot());
            }
            var receipt = board.Evaluate(record, state, mode, unlocks);
            return new PlayResult(kind, mode == RankingEligibility.Auto, string.Empty, title, level,
                score, settlement: receipt, difficulty: "master");
        }

        private static CustomMusicScorePlayHistoryRecord Record(string id, string key, string title, int level,
            LiveScore score, PlayResultKind kind)
        {
            var award = RankingRules.Award(kind, score.autoCount > 0, score);
            return new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3, RecordId = id, ScoreKey = key, Ranked = true, Title = title, PlayLevel = level,
                PlayedAtUtc = "2026-11-22T00:00:00.0000000Z", EndReason = kind,
                FormulaVersion = RankingRules.FORMULA_VERSION, MaximumScore = RankingRules.MAXIMUM_SCORE,
                TotalScore = score.totalScore, Life = score.life, TotalComboCount = score.totalComboCount,
                AutoCount = score.autoCount, PerfectCount = score.perfectCount, GreatCount = score.greatCount,
                MissCount = score.missCount, MaxCombo = score.maxCombo, Difficulty = "master",
                IsFullCombo = award == ClearAward.FullCombo || award == ClearAward.AllPerfect,
                IsAllPerfect = award == ClearAward.AllPerfect
            };
        }

        private static LiveScore NormalClear() => new() { totalScore = 980000, totalComboCount = 300,
            perfectCount = 294, missCount = 6, maxCombo = 180, life = 640, rank = Sekai.ScoreRank.A };
        private static LiveScore FullCombo() => new() { totalScore = 980000, totalComboCount = 300,
            perfectCount = 280, greatCount = 20, maxCombo = 300, life = 1000, rank = Sekai.ScoreRank.A };
        private static LiveScore AllPerfect() => new() { totalScore = 1000000, totalComboCount = 300,
            perfectCount = 300, maxCombo = 300, life = 1000, rank = Sekai.ScoreRank.S };
    }
}
