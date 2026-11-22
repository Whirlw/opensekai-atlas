using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        /// <summary>
        /// The second home page and the 个人信息 part, on a sample profile in a temporary folder, so
        /// the frames never show or change the player's own: the second page at rest and mid-swipe,
        /// the 个人信息 card growing over it, the real tap in, the Best 30 and the history, the
        /// 编辑资料 card, a backup's merge preview, back out (it lands on the second page), and last
        /// an empty profile on the card and the page. Sample songs are the installed packages
        /// (with their jackets), topped up with made-up titles.
        /// </summary>
        private IEnumerator ProfileShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Profile-" + Guid.NewGuid().ToString("N"));
            try
            {
                PlayerProfileStore sample = ProfileSamples.Build(Path.Combine(root, "sample"), out string backup);
                var empty = new PlayerProfileStore(Path.Combine(root, "empty"));
                home.ProfileStore = sample;
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1.2f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-page2", "second home page", null);
                home.SeekHomePage(0.42f);
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-swipe", "home pages mid-swipe", null, 0.42f);
                foreach (float amount in new[] { 0.35f, 0.7f })
                {
                    home.SeekProfileHandoff(amount);
                    yield return null;
                    yield return null;
                    yield return Capture(shot, FileStem(shot, amount), "profile card grown " + amount.ToString("0.00", CultureInfo.InvariantCulture), null, amount);
                }

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return null;
                home.EnterProfile();
                yield return WaitUntil(() => home.IsProfileShown, "the profile page after the hand-over");
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Capture(shot, stem + "-best30", "Best 30", null);

                ProfileScreen screen = UnityEngine.Object.FindFirstObjectByType<ProfileScreen>();
                if (screen == null)
                {
                    throw new InvalidOperationException("The profile part did not open.");
                }

                screen.ShowTab(ProfileTab.History);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture(shot, stem + "-history", "history", null);

                screen.OpenEditor();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-edit", "edit card", null);
                screen.TryHandleBack();

                ProfileImportPreview preview = sample.PreviewBackup(backup);
                screen.ShowImportPreview(preview, sample);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-import", "merge preview", null);
                screen.TryHandleBack();
                screen.ShowTab(ProfileTab.Best30);

                screen.Back();
                yield return WaitUntil(() => home.IsHomeShown && !home.IsProfileShown, "the home page after back");
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-return", "after the profile's back button, on page " + (home.HomePage + 1), null);

                home.ProfileStore = empty;
                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-page2-empty", "second home page, new profile", null);
                home.ShowProfileImmediately();
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-empty", "new profile", null);
            }
            finally
            {
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
    /// A made-up but plausible local profile for the 个人信息 screenshots: a few dozen runs over the
    /// last three weeks across the installed packages (topped up with invented titles), with clears,
    /// full combos, all perfects, a failed, a retired, an autoplay and a legacy run; plus a backup
    /// from "another device" that shares some runs, differs on one and adds a few.
    /// </summary>
    internal static class ProfileSamples
    {
        private static readonly string[] _titles =
        {
            "Aurora Line", "Night Signal", "Paper Comet", "Glass Garden", "Lumen Drive", "Echo Bloom", "Orbit Parade",
            "Silver Static", "Velvet Prism", "Cobalt Hour", "Neon Tide", "Starlit Atlas", "Hollow Bell", "Crimson Lattice",
            "Ivory Circuit", "Moth and Lantern", "Tidal Index", "Quiet Engine", "Saffron Relay", "Faraway Choir",
            "Lantern Code", "Northbound"
        };

        private static readonly string[] _difficulties = { "master", "expert", "append", "master", "hard" };

        private readonly struct Chart
        {
            internal Chart(string key, string title, string difficulty, int level)
            {
                Key = key;
                Title = title;
                Difficulty = difficulty;
                Level = level;
            }

            internal string Key { get; }
            internal string Title { get; }
            internal string Difficulty { get; }
            internal int Level { get; }
        }

        /// <param name="backup">A backup ZIP to preview against the returned profile.</param>
        internal static PlayerProfileStore Build(string root, out string backup)
        {
            var random = new System.Random(20261122);
            List<Chart> charts = Charts(random, 22);
            DateTime now = DateTime.UtcNow;
            var plays = new List<CustomMusicScorePlayHistoryRecord>();
            int serial = 0;
            for (int i = 0; i < charts.Count; i++)
            {
                Chart chart = charts[i];
                int runs = 1 + random.Next(3);
                for (int run = 0; run < runs; run++)
                {
                    bool last = run == runs - 1;
                    double roll = random.NextDouble();
                    ClearAward award = last && roll < 0.22 ? ClearAward.AllPerfect : last && roll < 0.55 ? ClearAward.FullCombo : ClearAward.Clear;
                    int score = award == ClearAward.AllPerfect ? 1000000
                        : award == ClearAward.FullCombo ? 990000 + random.Next(9000)
                        : 930000 + random.Next(55000);
                    DateTime when = now.AddHours(-(random.NextDouble() * 24 * 18 + 2) + run * 3);
                    plays.Add(Record("sample-" + (++serial), chart, score, PlayResultKind.Completed, award, false, when));
                }
            }

            plays.Add(Record("sample-failed", charts[1], 612000, PlayResultKind.Failed, ClearAward.None, false, now.AddMinutes(-38)));
            plays.Add(Record("sample-retired", charts[4], 248000, PlayResultKind.Retired, ClearAward.None, false, now.AddHours(-5)));
            plays.Add(Record("sample-auto", charts[2], 1000000, PlayResultKind.Completed, ClearAward.None, true, now.AddHours(-26)));
            plays.Add(Record("sample-latest", charts[0], 998420, PlayResultKind.Completed, ClearAward.FullCombo, false, now.AddMinutes(-12)));
            plays.Sort((a, b) => string.CompareOrdinal(a.PlayedAtUtc, b.PlayedAtUtc));

            var store = new PlayerProfileStore(Path.Combine(root, "local"));
            store.SetAppearance("Atlas", "halo");
            store.History.Submit(Legacy("sample-legacy", charts[6], now.AddDays(-40)), null, out _);
            foreach (CustomMusicScorePlayHistoryRecord play in plays)
            {
                store.History.Submit(play, null, out _);
            }

            var other = new PlayerProfileStore(Path.Combine(root, "other"));
            other.SetAppearance("Atlas", "halo");
            for (int i = 0; i < 5; i++)
            {
                other.History.Submit(plays[i], null, out _);
            }

            CustomMusicScorePlayHistoryRecord changed = plays[5];
            other.History.Submit(Record(changed.RecordId, charts.First(c => c.Key == changed.ScoreKey), changed.TotalScore - 1200,
                PlayResultKind.Completed, ClearAward.Clear, false, DateTime.Parse(changed.PlayedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)), null, out _);
            for (int i = 0; i < 6; i++)
            {
                Chart chart = charts[(i * 5 + 3) % charts.Count];
                other.History.Submit(Record("other-" + i, chart, 990000 + i * 1500, PlayResultKind.Completed,
                    i % 3 == 0 ? ClearAward.AllPerfect : ClearAward.FullCombo, false, now.AddDays(-1 - i)), null, out _);
            }

            // A fresh profile keeps its history in its own root until a restore switches archives.
            string otherHistory = Path.Combine(SongPackageStorage.ActivePlayerDirectory(Path.Combine(root, "other")),
                SongPackageStorage.PLAY_HISTORY_FILE_NAME);
            File.AppendAllText(otherHistory, "{\"schemaVersion\":1,\"title\":\"Early Bird\"}\n{\"schemaVersion\":2,\"title\":\"Morning Dew\"}\n");
            backup = Path.Combine(root, "backup.zip");
            other.ExportBackup(backup);
            return store;
        }

        private static List<Chart> Charts(System.Random random, int count)
        {
            var charts = new List<Chart>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (SongPackageItem item in SongPackageManager.Shared.LoadItems())
                {
                    if (charts.Count >= count) break;
                    if (string.IsNullOrEmpty(item?.ScoreKey) || item.Entry?.Manifest == null || !keys.Add(item.ScoreKey)) continue;
                    var manifest = item.Entry.Manifest;
                    int level = manifest.playLevel > 0 ? manifest.playLevel : 24 + random.Next(9);
                    string difficulty = string.IsNullOrEmpty(manifest.musicDifficultyType) ? "master" : manifest.musicDifficultyType;
                    charts.Add(new Chart(item.ScoreKey, LibraryModel.BuildTitle(item), difficulty, level));
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Screenshots] Could not list the installed packages: " + exception.Message);
            }

            for (int i = 0; charts.Count < count; i++)
            {
                charts.Add(new Chart("sample-chart-" + i, _titles[i % _titles.Length], _difficulties[i % _difficulties.Length],
                    24 + random.Next(9)));
            }

            return charts;
        }

        private static CustomMusicScorePlayHistoryRecord Record(string id, Chart chart, int score, PlayResultKind kind, ClearAward award,
            bool auto, DateTime when)
        {
            int notes = 640 + chart.Level * 24;
            bool fullCombo = award == ClearAward.FullCombo || award == ClearAward.AllPerfect;
            int misses = kind == PlayResultKind.Completed && !fullCombo && !auto ? 3 + score % 9 : 0;
            int greats = award == ClearAward.AllPerfect || auto ? 0 : Math.Max(1, (1000000 - score) / 900);
            return new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3,
                RecordId = id,
                ScoreKey = chart.Key,
                Title = chart.Title,
                Difficulty = chart.Difficulty,
                PlayLevel = chart.Level,
                PlayedAtUtc = when.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                EndReason = kind,
                FormulaVersion = RankingRules.FormulaVersion,
                MaximumScore = RankingRules.MaximumScore,
                TotalScore = score,
                Life = kind == PlayResultKind.Failed ? 0 : 640,
                TotalComboCount = notes,
                MaxCombo = fullCombo || auto ? notes : notes / 2 + misses * 17,
                PerfectCount = auto ? 0 : Math.Max(0, notes - greats - misses),
                GreatCount = auto ? 0 : greats,
                MissCount = misses,
                AutoCount = auto ? notes : 0,
                IsFullCombo = fullCombo,
                IsAllPerfect = award == ClearAward.AllPerfect,
                LivePlayMode = "Free"
            };
        }

        private static CustomMusicScorePlayHistoryRecord Legacy(string id, Chart chart, DateTime when) => new CustomMusicScorePlayHistoryRecord
        {
            SchemaVersion = 2,
            RecordId = id,
            ScoreKey = chart.Key,
            Title = chart.Title,
            Difficulty = chart.Difficulty,
            PlayLevel = chart.Level,
            PlayedAtUtc = when.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            TotalScore = 951200,
            TotalComboCount = 900,
            MaxCombo = 610,
            LivePlayMode = "Free"
        };
    }
}
