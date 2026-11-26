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
        [TestCase(SettlementExample.FirstClear, 267, 0, 267)]
        [TestCase(SettlementExample.SameChartFullCombo, 272, 267, 272)]
        [TestCase(SettlementExample.NewChartAllPerfectTierUp, 310, 7790, 7850)]
        [TestCase(SettlementExample.OutsideBest30, 172, 7790, 7790)]
        [TestCase(SettlementExample.Auto, 0, 0, 0)]
        [TestCase(SettlementExample.Failed, 0, 0, 0)]
        [TestCase(SettlementExample.Retired, 0, 0, 0)]
        [TestCase(SettlementExample.SaveFailed, 272, 267, 267)]
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

        [TestCase(0, 0, 6000)]
        [TestCase(6000, 1, 6900)]
        [TestCase(6900, 2, 7800)]
        [TestCase(7800, 3, 9000)]
        [TestCase(9000, 4, 9900)]
        [TestCase(9900, 5, 10500)]
        [TestCase(10500, 6, null)]
        public void RankingTierStartsAtItsThreshold(int threshold, int tierId, int? nextThreshold)
        {
            Assert.That(RankingRules.Tier(threshold - 1), Is.EqualTo(tierId - 1));
            Assert.That(RankingRules.Tier(threshold), Is.EqualTo(tierId));
            Assert.That(RankingRules.NextTierThreshold(threshold - 1), Is.EqualTo(threshold));
            Assert.That(RankingRules.NextTierThreshold(threshold), Is.EqualTo(nextThreshold));
        }

        [TestCase(31, 990000, 290)]
        [TestCase(33, 975000, 290)]
        [TestCase(33, 899999, 0)]
        [TestCase(33, 900000, 250)]
        [TestCase(33, 925000, 260)]
        [TestCase(33, 950000, 270)]
        [TestCase(33, 962500, 280)]
        [TestCase(33, 982500, 300)]
        [TestCase(33, 995000, 320)]
        [TestCase(34, 1000000, 345)]
        [TestCase(35, 990000, 340)]
        [TestCase(35, 1100000, 360)]
        public void RankingPointsFollowGradeAndCorrectedLevel(int level, int raw, int expected)
        {
            Assert.That(RankingRules.BasePoints(level, raw, 1000000), Is.EqualTo(expected));
            Assert.That(RankingRules.BasePoints(level, raw * 2, 2000000), Is.EqualTo(expected));
        }

        [Test]
        public void BelowCFullComboStaysOutOfBest30AndOldRankedScoresUseCurrentRules()
        {
            var board = new Best30Index();
            var below = board.Evaluate(Play("fc", "below", 899999, fullCombo: true), HistorySaveState.Saved);
            Assert.That(below.Eligibility, Is.EqualTo(RankingEligibility.BelowRankC));
            Assert.That(below.Award, Is.EqualTo(ClearAward.FullCombo));
            Assert.That(below.Points, Is.Zero);
            Assert.That(board.Snapshot().Entries, Is.Empty);
            var old = board.Evaluate(Play("old", "chart", 900000, formulaVersion: 1), HistorySaveState.Saved);
            Assert.That(old.Eligibility, Is.EqualTo(RankingEligibility.Eligible));
            Assert.That(board.Snapshot().Total, Is.EqualTo(220));
        }

        [TestCase("2026-01-01T12:00:00+09:00", "2026-01-01T09:00:00Z")]
        [TestCase("2026-01-01T10:00:00Z", "2026-01-01T10:00:00.1Z")]
        public void ImportedTimestampsOrderHistoryAndBreakBestTiesByInstant(string early, string late)
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                string path = Path.Combine(root, "history.jsonl");
                var history = new RankingHistory(path, new PlayHistorySummaryCache());
                history.Submit(Play("first", "song", 970000, early), null, out _);
                history.Submit(Play("second", "song", 970000, late), null, out var song);
                var rebuilt = new RankingHistory(path, new PlayHistorySummaryCache());
                foreach (var index in new[] { history, rebuilt })
                {
                    Assert.That(index.ReadPage(0, 2).Records.Select(row => row.RecordId),
                        Is.EqualTo(new[] { "second", "first" }), "history lists later plays first");
                    Assert.That(index.Read().Entries.Single().Play.RecordId, Is.EqualTo("first"), "earlier score wins a tie");
                }
                Assert.That(song.LastPlayedAtUtc, Is.EqualTo(late));
                Assert.That(song.Best.RecordId, Is.EqualTo("second"), "song summaries use the latest equivalent performance");
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DifferentTimestampFormatsOfOneInstantBreakTiesByStableRecordId()
        {
            var board = new Best30Index();
            board.Evaluate(Play("z", "song", 970000, "2026-01-01T00:00:00.000Z"), HistorySaveState.Saved);
            board.Evaluate(Play("a", "song", 970000, "2026-01-01T01:00:00+01:00"), HistorySaveState.Saved);
            Assert.That(board.Snapshot().Entries.Single().Play.RecordId, Is.EqualTo("a"));
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
                Assert.That(refresh.After.ChartBest, Is.EqualTo(267));
                Assert.That(history.Read().Entries.Count, Is.EqualTo(1));

                File.AppendAllText(path, "{broken");
                var reopened = new RankingHistory(path, new PlayHistorySummaryCache());
                var duplicate = reopened.Submit(first, null, out var summary);
                Assert.That(summary, Is.Null);
                Assert.That(duplicate.Delta, Is.EqualTo(initial.Delta));
                reopened.Submit(Play("run-c", "other", 980000), null, out _);
                Assert.That(reopened.Read().Total, Is.EqualTo(534));
                Assert.That(File.ReadLines(path).Count(), Is.EqualTo(4));
                Assert.That(new RankingHistory(path, new PlayHistorySummaryCache()).Read().Total, Is.EqualTo(534));
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

        private static CustomMusicScorePlayHistoryRecord Play(string id, string key, int raw, string utc = "2026-11-22",
            int formulaVersion = RankingRules.FORMULA_VERSION, bool fullCombo = false)
            => new() { SchemaVersion = 3, RecordId = id, ScoreKey = key, Ranked = true, FormulaVersion = formulaVersion, IsFullCombo = fullCombo,
                MaximumScore = RankingRules.MAXIMUM_SCORE, EndReason = PlayResultKind.Completed, PlayedAtUtc = utc,
                Life = 100, TotalComboCount = 300, TotalScore = raw, PlayLevel = 30 };

        [Test]
        public void UnrankedAndLegacyPlaysStayInHistoryAndDoNotDisplaceARankedBest()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-rank-status-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "history.jsonl");
            try
            {
                var history = new RankingHistory(path, new PlayHistorySummaryCache());
                var ranked = history.Submit(Play("ranked", "same", 975000), null, out _);
                var legacy = Newtonsoft.Json.Linq.JObject.FromObject(Play("old", "same", 1000000));
                legacy.Remove("ranked");
                var play = legacy.ToObject<CustomMusicScorePlayHistoryRecord>();
                var receipt = history.Submit(play, null, out var summary);
                Assert.That(receipt.Eligibility, Is.EqualTo(RankingEligibility.Unranked));
                Assert.That(receipt.Points, Is.Zero);
                Assert.That(summary.Best.RecordId, Is.EqualTo("old"));
                Assert.That(receipt.SaveState, Is.EqualTo(HistorySaveState.Saved));
                var reopened = new RankingHistory(path, new PlayHistorySummaryCache());
                Assert.That(reopened.Read().Total, Is.EqualTo(ranked.Points));
                Assert.That(reopened.ReadPage(0, 10).TotalCount, Is.EqualTo(2));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
