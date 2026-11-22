using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class AchievementPageTests
    {
        private static readonly DateTimeOffset _epoch = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Regex _rowName = new Regex(@"^Achievement\d+$");

        private readonly System.Collections.Generic.List<AchievementScreen> _screens = new System.Collections.Generic.List<AchievementScreen>();
        private string _root;
        private GameObject _canvas;
        private RankingHistory _previousHistory;
        private float _previousVolume;

        [SetUp]
        public void SetUp()
        {
            _previousVolume = UiSound.Volume;
            UiSound.Volume = 0f;
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OpenSekai-Achievements-" + Guid.NewGuid().ToString("N"))).FullName;
            _previousHistory = CustomMusicScorePlayHistoryStorage.Ranking;
            CustomMusicScorePlayHistoryStorage.Ranking = new RankingHistory(Path.Combine(_root, "results", "PlayHistory.jsonl"),
                new PlayHistorySummaryCache());
            ResultPage.ForgetCelebrations();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_canvas);
            foreach (AchievementScreen screen in _screens)
            {
                if (screen != null) UnityEngine.Object.DestroyImmediate(screen.gameObject);
            }

            _screens.Clear();

            CustomMusicScorePlayHistoryStorage.Ranking = _previousHistory;
            UiSound.Volume = _previousVolume;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void EveryGoalTheBackendKnowsHasItsOwnCopyAndFilter()
        {
            var goals = new PlayerProfileStore(Directory.CreateDirectory(Path.Combine(_root, "profile")).FullName).ReadAchievements();
            Assert.That(goals, Is.Not.Empty);
            var looks = goals.Select(AchievementCopy.For).ToList();
            Assert.That(looks.Select(look => look.Title), Is.Unique.And.None.EqualTo("新成就"));
            Assert.That(looks.Select(look => look.Goal), Has.None.Empty);
            for (int i = 0; i < goals.Count; i++)
            {
                Assert.That(looks[i].Category, Is.EqualTo(AchievementCopy.CategoryOf(goals[i].Metric)), goals[i].Id);
            }
        }

        [Test]
        public void AGoalThisBuildDoesNotKnowReadsFromItsMetric()
        {
            var hits = new AchievementProgress("manual_hits_50000", AchievementMetric.ManualHits, 0, 50000);
            AchievementLook look = AchievementCopy.For(hits);
            Assert.That(look.Title, Does.Contain("50,000"));
            Assert.That(look.Mark, Is.EqualTo("50K"));
            Assert.That(look.Category, Is.EqualTo(AchievementCategory.Hits));

            var charts = new AchievementProgress("fc_lv30_5", AchievementMetric.FullComboCharts, 30, 5);
            Assert.That(AchievementCopy.For(charts).Goal, Is.EqualTo("在 5 张 Lv.30 及以上的谱面取得 FULL COMBO"));
            Assert.That(AchievementCopy.For("someday").Title, Is.EqualTo("新成就"), "an unknown id without its progress still has a name");
        }

        [Test]
        public void ProgressNeverReadsAsDoneBeforeTheGoalIsUnlocked()
        {
            var under = new AchievementProgress("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000, 6240);
            Assert.That(AchievementCopy.Percent(under), Is.EqualTo(62));
            Assert.That(AchievementCopy.Count(under), Is.EqualTo("6,240 / 10,000"));

            var almost = new AchievementProgress("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000, 9999);
            Assert.That(AchievementCopy.Percent(almost), Is.EqualTo(99));

            var past = new AchievementProgress("manual_hits_10000", AchievementMetric.ManualHits, 0, 10000, 12480, _epoch);
            Assert.That(AchievementCopy.Percent(past), Is.EqualTo(100));
            Assert.That(AchievementCopy.Fraction(past), Is.EqualTo(1f));
            Assert.That(AchievementCopy.Total(past), Is.EqualTo("累计 12,480 键"));

            var charts = new AchievementProgress("fc_lv28_10", AchievementMetric.FullComboCharts, 28, 10, 3);
            Assert.That(AchievementCopy.Count(charts), Is.EqualTo("3 / 10 张"));
        }

        [Test]
        public void NamesStayOnOneLine()
        {
            Assert.That(AchievementCopy.Names(new[] { "fc_lv25", "first_ap" }), Is.EqualTo("「高阶全连」「完美无缺」"));
            Assert.That(AchievementCopy.Names(new[] { "fc_lv25", "first_ap", "first_dan_pass", "manual_hits_10000" }),
                Does.EndWith("等 4 项"));
        }

        [Test]
        public void GoalsReachedTogetherShareOneCard()
        {
            var popup = new AchievementUnlockPopup((RectTransform)_canvas.transform, 18f);
            Assert.That(popup.Bind(Array.Empty<string>(), null), Is.False);

            Assert.That(popup.Bind(new[] { "manual_hits_10000", "fc_lv28_10" }, "合并备份后解锁"), Is.True);
            popup.Seek(1f);
            Assert.That(popup.IsShowing, Is.True);
            Assert.That(Text(_canvas, "UnlockTitle"), Is.EqualTo("万键之路、十面全连"));
            Assert.That(Text(_canvas, "UnlockLine"), Is.EqualTo("同时解锁 2 项成就  ·  合并备份后解锁"));

            popup.Seek(AchievementUnlockPopup.LENGTH);
            Assert.That(popup.IsShowing, Is.False, "the card lifts away at the end of its timeline");
            popup.Dispose();
        }

        [Test]
        public void ARunAnnouncesWhatItUnlockedOnce()
        {
            PlayResult played = SettlementSamples.Create(SettlementExample.SameChartFullCombo);
            Assume.That(played.Settlement.UnlockedAchievementIds, Is.EqualTo(new[] { "fc_lv25" }));
            ResultPage page = ResultPage.Create(_canvas.transform, null, "返回曲库");
            page.Show(played, null, LibraryTheme.Colors.DefaultAccent);
            float announcing = page.Duration;
            page.Skip();
            Assert.That(Find(page.gameObject, "AchievementUnlock").activeSelf, Is.True, "skipping still shows the card");
            Assert.That(Text(page.gameObject, "UnlockTitle"), Is.EqualTo("高阶全连"));

            page.Hide();
            page.Show(played, null, LibraryTheme.Colors.DefaultAccent);
            page.Skip();
            Assert.That(page.Duration, Is.LessThan(announcing));
            Assert.That(Find(page.gameObject, "AchievementUnlock").activeSelf, Is.False, "a reopened run does not announce again");
        }

        [Test]
        public void AnUnsavedRunAnnouncesNothing()
        {
            ResultPage page = ResultPage.Create(_canvas.transform, null, "返回曲库");
            page.Show(SettlementSamples.Create(SettlementExample.SaveFailed), null, LibraryTheme.Colors.DefaultAccent);
            page.Skip();
            Assert.That(Find(page.gameObject, "AchievementUnlock").activeSelf, Is.False);
        }

        [Test]
        public void ThePageListsEveryGoalAndFiltersThem()
        {
            var store = new PlayerProfileStore(Directory.CreateDirectory(Path.Combine(_root, "profile")).FullName);
            for (int i = 0; i < 4; i++)
            {
                store.History.Submit(FullCombo(i, 26 + i), null, out _);
            }

            AchievementScreen screen = AchievementScreen.Create();
            _screens.Add(screen);
            screen.Store = store;
            screen.SetPresented(true, false);
            int total = screen.Items.Count;
            Assert.That(Rows(screen), Is.EqualTo(total));
            Assert.That(Text(screen.gameObject, "Count"), Is.EqualTo("1"), "高阶全连 is unlocked");
            Assert.That(Text(screen.gameObject, "Total"), Is.EqualTo("/ " + total));

            screen.SetFilter(AchievementCategory.Charts);
            Assert.That(Rows(screen), Is.EqualTo(screen.Items.Count(item => AchievementCopy.CategoryOf(item.Metric) == AchievementCategory.Charts)));
            Assert.That(Text(screen.gameObject, "FilterCount"), Is.EqualTo("已解锁 1 / " + Rows(screen)));

            screen.SetFilter(AchievementCategory.Dan);
            Assert.That(Rows(screen), Is.EqualTo(1));
            screen.SetPresented(false, false);
        }

        private static CustomMusicScorePlayHistoryRecord FullCombo(int n, int level) => new CustomMusicScorePlayHistoryRecord
        {
            SchemaVersion = 3, RecordId = "fc-" + n, ScoreKey = "chart-" + n, PlayedAtUtc = _epoch.AddHours(n).ToString("O"),
            EndReason = PlayResultKind.Completed, Life = 100, PlayLevel = level, MaximumScore = 1000000, FormulaVersion = 1,
            TotalScore = 990000, TotalComboCount = 500, MaxCombo = 500, PerfectCount = 490, GreatCount = 10, IsFullCombo = true
        };

        private static int Rows(AchievementScreen screen) =>
            screen.GetComponentsInChildren<RectTransform>(false).Count(rect => _rowName.IsMatch(rect.name));

        private static GameObject Find(GameObject root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name).gameObject;

        private static string Text(GameObject root, string name) =>
            root.GetComponentsInChildren<TextMeshProUGUI>(true).Single(label => label.name == name).text;
    }
}
