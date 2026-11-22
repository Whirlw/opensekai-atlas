using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;

namespace OpenSekai.Tests.EditMode.Packages
{
    public class RankingHistoryTests
    {
        [TestCase(SettlementExample.FirstClear, 288, 0, 288)]
        [TestCase(SettlementExample.SameChartFullCombo, 293, 288, 293)]
        [TestCase(SettlementExample.NewChartAllPerfectTierUp, 310, 8390, 8430)]
        [TestCase(SettlementExample.OutsideBest30, 197, 8390, 8390)]
        [TestCase(SettlementExample.Auto, 0, 0, 0)]
        [TestCase(SettlementExample.Failed, 0, 0, 0)]
        [TestCase(SettlementExample.Retired, 0, 0, 0)]
        [TestCase(SettlementExample.SaveFailed, 293, 288, 288)]
        public void PreviewMatchesProductionRules(SettlementExample example, int points, int before, int after)
        {
            var result = SettlementSamples.Create(example);
            var s = CustomMusicScorePlayHistoryStorage.ForDisplay(result);
            Assert.That(s.Points, Is.EqualTo(points));
            Assert.That(s.Before.Total, Is.EqualTo(before));
            Assert.That(s.After.Total, Is.EqualTo(after));
            Assert.That(s.TierUp, Is.EqualTo(example == SettlementExample.NewChartAllPerfectTierUp));
            Assert.That(s.Award, Is.EqualTo(result.Award));
            if (example == SettlementExample.OutsideBest30)
            {
                Assert.That(s.ChartImproved, Is.True);
                Assert.That(s.After.ChartPosition, Is.Null);
            }
            if (example == SettlementExample.SaveFailed)
                Assert.That(s.SaveState, Is.EqualTo(HistorySaveState.SaveFailed));
        }

        [Test]
        public void ReplayAndRetryNeverCountTheSamePlayOrChartTwice()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-ranking-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "PlayHistory.jsonl");
            try
            {
                var history = new RankingHistory(path, new PlayHistorySummaryCache());
                Assert.That(history.Read().Entries, Is.Empty);
                var first = Play("run-a", "same", 975000);
                var better = Play("run-b", "same", 980000);
                var initial = history.Submit(first, null, out _);
                var refresh = history.Submit(better, null, out _);
                Assert.That(refresh.Before.ChartBest, Is.EqualTo(initial.Points));
                Assert.That(refresh.After.ChartBest, Is.EqualTo(288));
                Assert.That(history.Read().Entries.Count, Is.EqualTo(1));

                File.AppendAllText(path, "{broken");
                var reopened = new RankingHistory(path, new PlayHistorySummaryCache());
                var duplicate = reopened.Submit(first, null, out var summary);
                Assert.That(summary, Is.Null);
                Assert.That(duplicate.Delta, Is.EqualTo(initial.Delta));
                reopened.Submit(Play("run-c", "other", 980000), null, out _);
                Assert.That(reopened.Read().Total, Is.EqualTo(576));
                Assert.That(File.ReadLines(path).Count(), Is.EqualTo(4));
                Assert.That(new RankingHistory(path, new PlayHistorySummaryCache()).Read().Total, Is.EqualTo(576));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void FailedWriteCanRetryWithoutPrematurePoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-ranking-" + Guid.NewGuid().ToString("N"));
            string blocked = Path.Combine(root, "blocked");
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(blocked, "occupies parent path");
                var history = new RankingHistory(Path.Combine(blocked, "history.jsonl"), new PlayHistorySummaryCache());
                var failed = history.Submit(Play("retry", "chart", 1000000), null, out _);
                Assert.That(failed.SaveState, Is.EqualTo(HistorySaveState.SaveFailed));
                Assert.That(failed.Delta, Is.Zero);
                File.Delete(blocked);
                Assert.That(history.Retry("retry", out _).Delta, Is.EqualTo(300));
                Assert.That(history.Retry("retry", out var duplicate).Delta, Is.EqualTo(300));
                Assert.That(duplicate, Is.Null);
                Assert.That(history.Read().Entries.Count, Is.EqualTo(1));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void SamePointsKeepEarlierPlayThenStableIdAtTheCutoff()
        {
            var board = new Best30Index();
            for (int i = 31; i >= 1; i--)
                board.Evaluate(Play(i.ToString("D2"), "chart-" + i, 1000000), HistorySaveState.Saved);
            var top = board.Snapshot();
            Assert.That(top.Total, Is.EqualTo(9000));
            Assert.That(top.Entries.Count, Is.EqualTo(30));
            Assert.That(top.Entries[29].Play.RecordId, Is.EqualTo("30"));
            board.Evaluate(Play("late", "chart-1", 1000000, "2026-11-23"), HistorySaveState.Saved);
            Assert.That(board.Snapshot().Entries[0].Play.RecordId, Is.EqualTo("01"));
        }

        private static CustomMusicScorePlayHistoryRecord Play(string id, string key, int raw, string utc = "2026-11-22")
            => new() { SchemaVersion = 3, RecordId = id, ScoreKey = key, FormulaVersion = RankingRules.FormulaVersion,
                MaximumScore = RankingRules.MaximumScore, EndReason = PlayResultKind.Completed, PlayedAtUtc = utc,
                Life = 100, TotalComboCount = 300, TotalScore = raw, PlayLevel = 30 };
    }
}
