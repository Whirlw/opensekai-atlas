using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.Tests.EditMode
{
    public sealed class ResultPageTests
    {
        private static readonly Color32 _accent = new Color32(200, 120, 255, 255);

        private GameObject _canvas;
        private RankingHistory _previousHistory;
        private string _historyRoot;
        private float _previousVolume;

        [SetUp]
        public void SetUp()
        {
            _previousVolume = UiSound.Volume;
            UiSound.Volume = 0f;
            _previousHistory = CustomMusicScorePlayHistoryStorage.Ranking;
            _historyRoot = Path.Combine(Path.GetTempPath(), "OpenSekai-ResultPage-" + Guid.NewGuid().ToString("N"));
            CustomMusicScorePlayHistoryStorage.Ranking = new RankingHistory(
                Path.Combine(_historyRoot, "PlayHistory.jsonl"), new PlayHistorySummaryCache());
            ResultPage.ForgetCelebrations();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1670f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_canvas);
            CustomMusicScorePlayHistoryStorage.Ranking = _previousHistory;
            UiSound.Volume = _previousVolume;
            if (Directory.Exists(_historyRoot)) Directory.Delete(_historyRoot, true);
        }

        [TestCase(SettlementExample.FirstClear, "CLEAR")]
        [TestCase(SettlementExample.SameChartFullCombo, "FULL COMBO")]
        [TestCase(SettlementExample.NewChartAllPerfectTierUp, "ALL PERFECT")]
        [TestCase(SettlementExample.Auto, "AUTO")]
        [TestCase(SettlementExample.Failed, "FAILED")]
        [TestCase(SettlementExample.Retired, "RETIRED")]
        public void TheAwardMarkFollowsTheRun(SettlementExample example, string mark)
        {
            Assert.That(ResultCopy.Award(SettlementSamples.Create(example)), Is.EqualTo(mark));
        }

        [Test]
        public void EveryExcludedRunSaysWhy()
        {
            PlayResult failed = SettlementSamples.Create(SettlementExample.Failed);
            PlayResult retired = SettlementSamples.Create(SettlementExample.Retired);
            var reasons = Enum.GetValues(typeof(RankingEligibility)).Cast<RankingEligibility>()
                .Where(eligibility => eligibility != RankingEligibility.Eligible)
                .Select(eligibility => ResultCopy.Exclusion(failed, Excluded(eligibility)))
                .ToList();
            Assert.That(reasons, Has.None.Null.And.None.Empty);
            Assert.That(reasons, Is.Unique);
            Assert.That(ResultCopy.Exclusion(retired, Excluded(RankingEligibility.Incomplete)),
                Is.Not.EqualTo(ResultCopy.Exclusion(failed, Excluded(RankingEligibility.Incomplete))),
                "a retired run and a failed one give different reasons");
        }

        [TestCase(SettlementExample.FirstClear, "0  →  267", "267", "第 1 名", "基础 267")]
        [TestCase(SettlementExample.SameChartFullCombo, "267  →  272", "267  →  272", "第 1 名", "基础 267   FC 奖励 +5")]
        [TestCase(SettlementExample.NewChartAllPerfectTierUp, "7,790  →  7,850", "310", "第 1 名", "基础 300   AP 奖励 +10")]
        [TestCase(SettlementExample.OutsideBest30, "谱面新纪录在 Best 30 之外，总分持平", "167  →  172", "榜外", "基础 167   FC 奖励 +5")]
        public void TheContributionLinesDescribeTheRanking(
            SettlementExample example, string standing, string best, string position, string breakdown)
        {
            RankingSettlement ranking = SettlementSamples.Create(example).Settlement;
            Assert.That(ResultCopy.Standing(ranking), Is.EqualTo(standing));
            Assert.That(ResultCopy.ChartBest(ranking), Is.EqualTo(best));
            Assert.That(ResultCopy.Position(ranking), Is.EqualTo(position));
            Assert.That(ResultCopy.Breakdown(ranking), Is.EqualTo(breakdown));
        }

        [Test]
        public void ASettledTierUpShowsTheGainAndTheNewBadge()
        {
            ResultPage page = Create();
            PlayResult played = SettlementSamples.Create(SettlementExample.NewChartAllPerfectTierUp);
            page.Show(played, null, _accent);
            Assert.That(Text(page, "Total"), Is.EqualTo("7,790"), "the total starts where it was");
            Assert.That(Text(page, "TierName"), Is.EqualTo(RankingTiers.Name(played.Settlement.Before.TierId)));

            page.Skip();

            Assert.That(Text(page, "Score"), Is.EqualTo("1,000,000"));
            Assert.That(Text(page, "Points"), Is.EqualTo("310"));
            Assert.That(Text(page, "Total"), Is.EqualTo("7,850"));
            Assert.That(ChipText(page, "Delta"), Is.EqualTo("+60"));
            Assert.That(ChipText(page, "Record"), Is.EqualTo(ResultCopy.NEW_RECORD));
            Assert.That(ChipText(page, "TierUp"), Is.EqualTo(ResultCopy.TIER_UP));
            Assert.That(Text(page, "TierName"), Is.EqualTo(RankingTiers.Name(played.Settlement.After.TierId)));
            Assert.That(Text(page, "Mark"), Does.Contain("<color="), "ALL PERFECT runs through its own colors");
        }

        [Test]
        public void ReopeningARunDoesNotPlayItsUpgradeAgain()
        {
            ResultPage page = Create();
            PlayResult played = SettlementSamples.Create(SettlementExample.NewChartAllPerfectTierUp);
            page.Show(played, null, _accent);
            float first = page.Duration;
            page.Hide();

            page.Show(played, null, _accent);
            page.Seek(0f);

            Assert.That(page.Duration, Is.LessThan(first));
            Assert.That(Text(page, "TierName"), Is.EqualTo(RankingTiers.Name(played.Settlement.After.TierId)),
                "a reopened run shows the tier it reached from the start");
        }

        [Test]
        public void AFailedSaveKeepsTheTotalAndOffersToSaveAgain()
        {
            ResultPage page = Create();
            page.Show(SettlementSamples.Create(SettlementExample.SaveFailed), null, _accent);
            page.Skip();

            Assert.That(Text(page, "Status"), Is.EqualTo(ResultCopy.NOT_SAVED));
            Assert.That(Text(page, "Total"), Is.EqualTo("267"));
            Assert.That(Find<Button>(page, "SaveRetry").gameObject.activeSelf, Is.True);
            Assert.That(Find<RectTransform>(page, "Delta").gameObject.activeSelf, Is.False, "no gain is shown until it is saved");

            // The sample was never written, so saving again finds nothing and the page stays as it was.
            Find<Button>(page, "SaveRetry").onClick.Invoke();
            Assert.That(Text(page, "Total"), Is.EqualTo("267"));
            Assert.That(Find<Button>(page, "SaveRetry").gameObject.activeSelf, Is.True);
        }

        [TestCase(SettlementExample.Auto, "自动演奏不计入 Ranking Score")]
        [TestCase(SettlementExample.Retired, "中途退出，本局不计入 Ranking Score")]
        public void AnExcludedRunShowsNoPointsAndItsReason(SettlementExample example, string reason)
        {
            ResultPage page = Create();
            page.Show(SettlementSamples.Create(example), null, _accent);
            page.Skip();

            Assert.That(Text(page, "Points"), Is.EqualTo("—"));
            Assert.That(Text(page, "Status"), Is.EqualTo(reason));
            Assert.That(Find<RectTransform>(page, "Record").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ALoadErrorShowsItsReasonAndTheCurrentTotal()
        {
            ResultPage page = Create();
            page.Show(PlayResult.FromLoadError("Broken", false, string.Empty, "找不到音频文件。"), null, _accent);
            page.Skip();

            Assert.That(Text(page, "Body"), Is.EqualTo("找不到音频文件。"));
            Assert.That(Find<RectTransform>(page, "Judgements").gameObject.activeSelf, Is.False);
            Assert.That(Text(page, "Total"), Is.EqualTo("0"));
            Assert.That(Text(page, "Status"), Is.EqualTo("曲包没能载入，本局没有开始"));
            Assert.That(Find<Button>(page, "Retry").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("重试"));
        }

        [Test]
        public void BackAndRetryCloseThePageAndReportTheRun()
        {
            PlayResult retried = null;
            bool closed = false;
            ResultPage page = ResultPage.Create(_canvas.transform, result => retried = result, "返回曲库");
            page.OnClose = () => closed = true;
            PlayResult played = SettlementSamples.Create(SettlementExample.FirstClear);

            page.Show(played, null, _accent);
            Find<Button>(page, "Retry").onClick.Invoke();
            Assert.That(retried, Is.SameAs(played));
            Assert.That(page.IsOpen, Is.False);

            page.Show(played, null, _accent);
            Find<Button>(page, "Back").onClick.Invoke();
            Assert.That(closed, Is.True);
            Assert.That(page.IsOpen, Is.False);
        }

        private ResultPage Create() => ResultPage.Create(_canvas.transform, null, "返回曲库");

        private static RankingSettlement Excluded(RankingEligibility eligibility)
        {
            var standing = new RankingStanding(0, null, null);
            return new RankingSettlement("id", "key", eligibility, HistorySaveState.NotRecorded, ClearAward.None, 0, standing, standing);
        }

        private static T Find<T>(ResultPage page, string name) where T : Component
        {
            return page.GetComponentsInChildren<T>(true).Single(component => component.name == name);
        }

        private static string Text(ResultPage page, string name) => Find<TextMeshProUGUI>(page, name).text;

        private static string ChipText(ResultPage page, string name)
        {
            return Find<RectTransform>(page, name).GetComponentInChildren<TextMeshProUGUI>(true).text;
        }
    }
}
