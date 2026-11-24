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
            (AchievementCategory.Practice, "practice"), (AchievementCategory.Technique, "technique"),
            (AchievementCategory.Consistency, "consistency"), (AchievementCategory.Course, "course"), (AchievementCategory.Special, "special")
        };

        /// <summary>
        /// The 成就 part on a sample profile in a temporary folder (never the player's own): the
        /// second home page with its chip, the medal there unlit and lit at several times, and the
        /// card growing, every achievement (two that can be
        /// unlocked, two under way) and each filter, two reward cards, one unlock held at several times
        /// and one run with a tap until the list has sorted itself, an earlier level, rows with more
        /// levels than one strip of stars, the card for one goal reached and for three at once
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
                home.ProfileStore = empty;
                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(1f);
                home.HoldIdle(HomeMedalArt.BEAT * 3d);
                yield return null;
                yield return Capture(shot, stem + "-medal-unlit", "the home medal with nothing to unlock", null);
                home.ProfileStore = sample;
                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(1f);
                foreach (float time in _achievementMedalTimes)
                {
                    home.HoldIdle(HomeMedalArt.BEAT * 3d + time);
                    yield return null;
                    yield return Capture(shot, stem + "-medal-lit" + Mathf.RoundToInt(time * 100f), "the home medal with achievements to unlock", null, time);
                }

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
                foreach (string id in new[] { "FC-02", "FC-03" })
                {
                    screen.ShowReward(FindGoal(screen, id));
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Capture(shot, stem + "-reward-" + id, "what " + id + " gives", null);
                    screen.HideReward();
                }

                AchievementRow row = screen.View.FindRow("FC-01");
                AchievementSeries after = screen.Unlock("FC-01");
                foreach (float time in _achievementUnlockTimes)
                {
                    row.SeekUnlock(after, time);
                    yield return null;
                    yield return Capture(shot, stem + "-claim" + Mathf.RoundToInt(time * 100f), "unlocking 全连达人", null, time);
                }

                screen.View.BindList(false);
                yield return null;
                yield return Capture(shot, stem + "-claimed", "after the unlock, in the new order", null);
                screen.View.FindRow("DAN-01").Root.Find("Inner/Hit").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture(shot, stem + "-tapped", "unlocking 初获段位 with a tap, the count gone up and what it gave", null);
                yield return new WaitForSecondsRealtime(2.4f);
                yield return Capture(shot, stem + "-sorted", "after it, complete and last", null);
                screen.View.FindRow("HIT-01").SlideTo(0);
                yield return new WaitForSecondsRealtime(0.15f);
                yield return Capture(shot, stem + "-sliding", "turning to an earlier level", null);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-earlier", "an earlier level", null);

                GameObject gallery = BuildAchievementGallery();
                yield return null;
                yield return Capture(shot, stem + "-levels", "rows with one to eight levels open", null);
                UnityEngine.Object.Destroy(gallery);

                screen.PreviewUnlock(new[] { "FC-01" }, null, 1.6f);
                yield return null;
                yield return Capture(shot, stem + "-unlock", "the card for one goal reached", null, 1.6f);
                screen.PreviewUnlock(new[] { "HIT-01", "FC-02", "AP-01" }, "合并备份后可解锁", 1.6f);
                yield return null;
                yield return Capture(shot, stem + "-unlock-multi", "one card for three goals after a backup merge", null, 1.6f);
                screen.PreviewUnlock(new[] { "DAN-01", "HIT-01" }, null, 0.3f);
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

        /// <summary>Into a lit home medal's beat: swinging, the light crossing with both rings out, the rings fading, at rest.</summary>
        private static readonly float[] _achievementMedalTimes = { 0.11f, 0.58f, 1f, 2.4f };

        private static readonly float[] _achievementUnlockTimes = { 0.2f, 0.34f, 0.5f, 0.75f, 1f, 1.5f, 1.9f };

        private static AchievementProgress FindGoal(AchievementScreen screen, string id)
        {
            foreach (AchievementProgress goal in screen.Items)
            {
                if (goal.Id == id) return goal;
            }

            throw new InvalidOperationException("No goal " + id);
        }

        /// <summary>Rows of a made-up achievement with eight levels, with ever more of them unlocked.</summary>
        private static GameObject BuildAchievementGallery()
        {
            var host = new GameObject("[Achievement gallery]", typeof(RectTransform));
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            UnityEngine.UI.CanvasScaler scaler = host.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            UnityEngine.UI.Image ground = LibraryUi.CreateImage("Ground", host.transform, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(ground.rectTransform);
            RectTransform area = LibraryUi.CreateRect("Area", host.transform);
            LibraryWidgets.Inset(area, 260f, 20f, 260f, 20f);
            RectTransform content = PackWidgets.Scroll(area, out UnityEngine.UI.ScrollRect scroll);
            int[] unlocked = { 0, 3, 4, 5, 8 };
            for (int i = 0; i < unlocked.Length; i++)
            {
                var steps = new System.Collections.Generic.List<AchievementProgress>();
                for (int step = 0; step < 8; step++)
                {
                    int target = 10000 * (step + 1);
                    var goal = new AchievementGoal("sample_" + i + "_" + step, AchievementMetric.ManualHits, 0, target, "sample_" + i,
                        step % 2 == 0 ? "sparkle" : null, step != 1);
                    bool reached = step < unlocked[i] || (step == unlocked[i] && i % 2 == 1);
                    steps.Add(new AchievementProgress(goal, unlocked[i] * 10000 + 6200, reached ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
                        step < unlocked[i] ? DateTimeOffset.UtcNow : (DateTimeOffset?)null));
                }

                var row = new AchievementRow(content, i, scroll, _ => { }, _ => { });
                row.SetTop(i * (AchievementRow.HEIGHT + 12f));
                row.Bind(new AchievementSeries(steps));
            }

            return host;
        }
    }

    /// <summary>
    /// A made-up profile part of the way through the goals: four full combos at Lv.26 to Lv.30 over
    /// the last nine days (so 高阶全连 can be unlocked and 十面全连 stands at 3 of 10), four more clears and
    /// a failed run for about 12,000 hits in all (万键之路 unlocked, the next level under way), no all
    /// perfect, and a passed two-song 段位 (初获段位 can be unlocked).
    /// </summary>
    internal static partial class AchievementSamples
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

            for (int i = 0; i < 4; i++)
            {
                store.History.Submit(Record("achievement-clear-" + i, "Long Tide " + i, 34, PlayResultKind.Completed, false, now.AddDays(-8 + i)), null, out _);
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
            store.UnlockAchievement("HIT-01");
            return store;
        }

        private static CustomMusicScorePlayHistoryRecord Record(string id, string title, int level, PlayResultKind kind, bool fullCombo, DateTime when,
            string difficulty = "master", bool perfect = false)
        {
            int notes = 640 + level * 24;
            bool failed = kind == PlayResultKind.Failed;
            int played = failed ? notes / 3 : notes;
            int greats = perfect ? 0 : fullCombo ? 18 : 40;
            int misses = fullCombo ? 0 : failed ? 60 : 7;
            return new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3, Ranked = true,
                RecordId = id,
                ScoreKey = "sample-achievement-" + title.ToLowerInvariant().Replace(' ', '-'),
                Title = title,
                Difficulty = difficulty,
                PlayLevel = level,
                PlayedAtUtc = when.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                EndReason = kind,
                FormulaVersion = RankingRules.FORMULA_VERSION,
                MaximumScore = RankingRules.MAXIMUM_SCORE,
                TotalScore = perfect ? RankingRules.MAXIMUM_SCORE : fullCombo ? 991200 : failed ? 402000 : 951000,
                Life = failed ? 0 : 640,
                TotalComboCount = notes,
                MaxCombo = fullCombo ? notes : played / 2,
                PerfectCount = Math.Max(0, played - greats - misses),
                GreatCount = greats,
                MissCount = misses,
                IsFullCombo = fullCombo,
                IsAllPerfect = perfect,
                LivePlayMode = "Free"
            };
        }
    }
}
