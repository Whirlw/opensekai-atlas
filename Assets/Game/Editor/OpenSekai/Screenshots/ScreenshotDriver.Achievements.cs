using System;
using System.Collections;
using System.Globalization;
using System.IO;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using Sekai.Core.Live;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        private static readonly (AchievementCategory Category, string Name)[] _achievementFilters =
        {
            (AchievementCategory.Hits, "hits"), (AchievementCategory.Charts, "charts"), (AchievementCategory.Dan, "dan")
        };

        /// <summary>
        /// The 成就 part on a sample profile in a temporary folder (never the player's own): the
        /// second home page with its chip and the card growing, every goal (two unlocked, two under
        /// way, one not started) and each filter, the unlock card for one goal and for three at once
        /// (holding, and mid-entrance), back out to the second page, and last a new profile.
        /// </summary>
        private IEnumerator AchievementsShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Achievements-" + Guid.NewGuid().ToString("N"));
            AchievementScreen screen = null;
            try
            {
                PlayerProfileStore sample = AchievementSamples.Build(Path.Combine(root, "sample"));
                var empty = new PlayerProfileStore(Path.Combine(root, "empty"));
                home.ProfileStore = sample;
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1.2f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-page2", "second home page with the 成就 card", null);
                home.SeekAchievementsHandoff(0.5f);
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-handoff", "成就 card grown halfway", null, 0.5f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return null;
                home.EnterAchievements();
                yield return WaitUntil(() => home.IsAchievementsShown, "the 成就 page after the hand-over");
                yield return new WaitForSecondsRealtime(1.4f);
                screen = UnityEngine.Object.FindAnyObjectByType<AchievementScreen>();
                if (screen == null)
                {
                    throw new InvalidOperationException("The 成就 part did not open.");
                }

                yield return Capture(shot, stem + "-all", "every goal", null);
                foreach ((AchievementCategory category, string name) in _achievementFilters)
                {
                    screen.SetFilter(category);
                    yield return new WaitForSecondsRealtime(0.7f);
                    yield return Capture(shot, stem + "-" + name, "the " + name + " filter", null);
                }

                screen.SetFilter(AchievementCategory.All);
                yield return new WaitForSecondsRealtime(0.7f);
                screen.PreviewUnlock(new[] { "fc_lv25" }, null, 1.6f);
                yield return null;
                yield return Capture(shot, stem + "-unlock", "the unlock card for one goal", null, 1.6f);
                screen.PreviewUnlock(new[] { "manual_hits_10000", "fc_lv28_10", "first_ap" }, "合并备份后解锁", 1.6f);
                yield return null;
                yield return Capture(shot, stem + "-unlock-multi", "one card for three goals after a backup merge", null, 1.6f);
                screen.PreviewUnlock(new[] { "first_dan_pass", "manual_hits_10000" }, null, 0.3f);
                yield return null;
                yield return Capture(shot, stem + "-unlock-enter", "the unlock card dropping in", null, 0.3f);
                screen.EndPreview();

                screen.Back();
                yield return WaitUntil(() => home.IsHomeShown && !home.IsAchievementsShown, "the home page after back");
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-return", "after the 成就 back button, on page " + (home.HomePage + 1), null);

                home.ProfileStore = empty;
                home.ShowAchievementsImmediately();
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-empty", "a new profile", null);
            }
            finally
            {
                screen?.EndPreview();
                home.ProfileStore = null;
                home.ShowLibraryImmediately();
                try
                {
                    Directory.Delete(root, true);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    Log("could not delete " + root + ": " + exception.Message);
                }
            }
        }
    }

    /// <summary>
    /// A made-up profile part of the way through the goals: four full combos at Lv.26 to Lv.30 over
    /// the last nine days (so 高阶全连 is unlocked and 十面全连 stands at 3 of 10), a failed run, no all
    /// perfect, and a passed two-song 段位 (初获段位), for about 6,200 hits in all.
    /// </summary>
    internal static class AchievementSamples
    {
        internal static PlayerProfileStore Build(string root)
        {
            var store = new PlayerProfileStore(root);
            store.SetAppearance("Atlas", "halo");
            DateTime now = DateTime.UtcNow;
            (string Title, int Level, double DaysAgo)[] fullCombos =
            {
                ("Aurora Line", 26, 9.2), ("Night Signal", 28, 6.4), ("Paper Comet", 29, 3.1), ("Glass Garden", 30, 1.3)
            };
            for (int i = 0; i < fullCombos.Length; i++)
            {
                (string title, int level, double daysAgo) = fullCombos[i];
                store.History.Submit(Record("achievement-fc-" + i, title, level, PlayResultKind.Completed, true, now.AddDays(-daysAgo)), null, out _);
            }

            store.History.Submit(Record("achievement-failed", "Lumen Drive", 31, PlayResultKind.Failed, false, now.AddHours(-20)), null, out _);

            var damage = new DanDamage(0, 1, 3, 6, 10);
            var course = new DanDefinition(100, damage,
                new System.Collections.Generic.List<string> { "https://sample.test/levels/echo-bloom", "https://sample.test/levels/orbit-parade" },
                "双曲耐久", "diamond", "#526DC8");
            store.SaveDanDefinition(course);
            DanAttempt attempt = DanAttempt.Begin(course);
            int life = course.InitialLife;
            string[] titles = { "Echo Bloom", "Orbit Parade" };
            for (int i = 0; i < titles.Length; i++)
            {
                int notes = 300 + i * 40;
                var score = new LiveScore
                {
                    life = life - 4, maxCombo = notes, totalComboCount = notes, totalScore = 991000 - i * 3000,
                    perfectCount = notes - 4, greatCount = 4
                };
                attempt = attempt.Append(course, new DanSongResult("achievement-dan-" + i, titles[i], PlayResultKind.Completed,
                    124000 + i * 9000, life, score, DateTimeOffset.UtcNow.AddMinutes(-40 + i * 3)));
                life -= 4;
            }

            store.SaveDanAttempt(course, attempt);
            return store;
        }

        private static CustomMusicScorePlayHistoryRecord Record(string id, string title, int level, PlayResultKind kind, bool fullCombo, DateTime when)
        {
            int notes = 640 + level * 24;
            bool failed = kind == PlayResultKind.Failed;
            int played = failed ? notes / 3 : notes;
            int greats = fullCombo ? 18 : 40;
            int misses = fullCombo ? 0 : failed ? 60 : 7;
            return new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3,
                RecordId = id,
                ScoreKey = "sample-achievement-" + title.ToLowerInvariant().Replace(' ', '-'),
                Title = title,
                Difficulty = "master",
                PlayLevel = level,
                PlayedAtUtc = when.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                EndReason = kind,
                FormulaVersion = RankingRules.FORMULA_VERSION,
                MaximumScore = RankingRules.MAXIMUM_SCORE,
                TotalScore = fullCombo ? 991200 : failed ? 402000 : 951000,
                Life = failed ? 0 : 640,
                TotalComboCount = notes,
                MaxCombo = fullCombo ? notes : played / 2,
                PerfectCount = Math.Max(0, played - greats - misses),
                GreatCount = greats,
                MissCount = misses,
                IsFullCombo = fullCombo,
                LivePlayMode = "Free"
            };
        }
    }
}
