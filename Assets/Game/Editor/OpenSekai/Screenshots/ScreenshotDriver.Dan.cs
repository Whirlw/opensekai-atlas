using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <summary>
        /// The 段位 part on sample courses in a temporary profile (never the player's own): the
        /// second home page and the hand-over, the page on a passed and on a failed course, the
        /// maker, a shared text and a broken one, the delete card, the preparation card, the rest
        /// screen between songs, the result of a pass (and its award ceremony at three moments), a
        /// failure and an abandoned course, and last the 个人信息 card wearing medals with their
        /// picker.
        /// </summary>
        private IEnumerator DanShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Dan-" + Guid.NewGuid().ToString("N"));
            DanCourseOverlay overlay = null;
            GameObject gallery = null;
            try
            {
                DanSamples samples = DanSamples.Build(Path.Combine(root, "sample"));
                home.ProfileStore = samples.Store;
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1.2f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-page2", "second home page with the 段位 card", null);
                home.SeekDanHandoff(0.5f);
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-handoff", "段位 card grown halfway", null, 0.5f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return null;
                home.EnterDan();
                yield return WaitUntil(() => home.IsDanShown, "the 段位 page after the hand-over");
                yield return new WaitForSecondsRealtime(1.2f);
                DanScreen screen = UnityEngine.Object.FindFirstObjectByType<DanScreen>();
                if (screen == null)
                {
                    throw new InvalidOperationException("The 段位 part did not open.");
                }

                yield return Capture(shot, stem + "-new", "a course not tried yet", null);
                screen.Select(samples.Passed.Id);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(shot, stem + "-passed", "a passed and worn course", null);
                screen.RequestDelete();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-delete", "deleting the worn course", null);
                screen.TryHandleBack();
                screen.Select(samples.Failed.Id);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(shot, stem + "-failed", "a course failed twice", null);

                screen.OpenEdit();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-editor", "editing the five-song course", null);
                screen.Editor.OpenGradePicker();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-grades", "choosing the plate's grade", null);
                screen.TryHandleBack();
                screen.TryHandleBack();
                screen.OpenCreate();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-create", "a new course", null);
                screen.TryHandleBack();

                screen.OpenPaste(samples.Shared.ShareText);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-paste", "a shared course", null);
                screen.Share.Close();
                screen.OpenPaste(DanDefinition.PREFIX + "\n{\"initialLife\":100,\"damage\":{\"perfect\":0,\"great\":1,\"good\":3,\"bad\":6,\"miss\":10},"
                    + "\"songs\":[\"https://sample.test/levels/aurora-line\",\"not a link\"],\"style\":{\"label\":\"双曲\",\"emblem\":\"diamond\",\"color\":\"#526DC8\"}}");
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-paste-error", "a shared text with a broken link", null);
                screen.Share.Close();

                DanRecord failed = screen.Records.First(r => r.Definition.Id == samples.Failed.Id);
                IReadOnlyList<DanSongInfo> songs = screen.SongsOf(failed);
                screen.Prepare.Show(samples.Failed, songs);
                screen.Prepare.Apply(DanPhase.Preparing, 3, null);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture(shot, stem + "-prepare", "preparing song 3 of 5", null);
                screen.Prepare.Apply(DanPhase.Error, 3, "无法连接服务器 sample.test，请检查网络后重试");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-prepare-error", "song 3 could not be prepared", null);
                screen.Prepare.Hide();

                overlay = DanCourseOverlay.Ensure();
                overlay.PreviewRest(samples.Failed, samples.Resting, 3, samples.Resting.RemainingLife(samples.Failed), songs, 42f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture(shot, stem + "-rest", "the rest before song 3 of 5", null);
                overlay.EndPreview();

                DanRecord passed = screen.Records.First(r => r.Definition.Id == samples.Passed.Id);
                var passModel = new DanResultModel(samples.Passed, samples.Pass, true, false, null, screen.SongsOf(passed));
                screen.ShowResult(passModel, false);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(shot, stem + "-result-pass", "a passed course", null);
                foreach (float time in new[] { 0.45f, 0.75f, 2.1f })
                {
                    screen.Result.SeekCeremony(time);
                    yield return null;
                    yield return Capture(shot, FileStem(shot, time) + "-award", "award ceremony", null, time);
                }

                screen.Result.Hide();
                screen.ShowResult(new DanResultModel(samples.Failed, samples.Fail, false, false, null, songs), false);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(shot, stem + "-result-fail", "a failed course", null);
                screen.ShowResult(new DanResultModel(samples.Failed, samples.Quit, false, false, null, songs), true);
                screen.Result.Seek(0.9f);
                yield return null;
                yield return Capture(shot, stem + "-result-quit", "an abandoned course, 0.9 s into the opening", null, 0.9f);
                screen.Result.Hide();

                screen.Back();
                yield return WaitUntil(() => home.IsHomeShown && !home.IsDanShown, "the home page after back");
                yield return new WaitForSecondsRealtime(1f);
                home.ShowProfileImmediately();
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-profile", "the profile wearing medals", null);
                ProfileScreen profile = UnityEngine.Object.FindFirstObjectByType<ProfileScreen>();
                profile?.OpenMedals(1);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-medals", "choosing a medal", null);
                profile?.TryHandleBack();
                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(0.3f);
                gallery = BuildMedalGallery();
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-medal-tiers", "every medal tier and grade", null);
            }
            finally
            {
                overlay?.EndPreview();
                if (gallery != null) UnityEngine.Object.Destroy(gallery);
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
    /// Made-up but plausible 段位 data for the screenshots: four courses (one passed and worn, one
    /// failed twice, one shared from a friend and not tried, one new), with attempts that follow
    /// the course rules (each song starts with the life the last one ended on).
    /// </summary>
    internal sealed class DanSamples
    {
        private static readonly string[] _titles =
        {
            "Aurora Line", "Night Signal", "Paper Comet", "Glass Garden", "Lumen Drive", "Echo Bloom", "Orbit Parade", "Silver Static"
        };

        internal PlayerProfileStore Store { get; private set; }
        internal DanDefinition Passed { get; private set; }
        internal DanDefinition Failed { get; private set; }
        internal DanDefinition Shared { get; private set; }
        internal DanAttempt Pass { get; private set; }
        internal DanAttempt Fail { get; private set; }
        internal DanAttempt Quit { get; private set; }

        /// <summary>The failed course after two songs, as the rest screen before song 3 sees it.</summary>
        internal DanAttempt Resting { get; private set; }

        internal static DanSamples Build(string root)
        {
            var samples = new DanSamples();
            var store = new PlayerProfileStore(root);
            store.SetAppearance("Atlas", "halo");
            var damage = new DanDamage(0, 1, 3, 6, 10);
            samples.Failed = new DanDefinition(100, damage, Links(0, 5), "极限五连", "star", "#F58080", 4, "ω");
            samples.Passed = new DanDefinition(100, damage, Links(5, 3), "三曲耐久", "chevron", "#526DC8", 2, "VIII");
            samples.Shared = new DanDefinition(60, new DanDamage(0, 0, 2, 5, 8), Links(2, 4), "夜行四曲", "halo", "#6ED6C0", 1, "III");
            var fresh = new DanDefinition(150, new DanDamage(0, 0, 1, 3, 5), Links(1, 2), "新手入门", "note", "#7BD88F");
            store.SaveDanDefinition(samples.Failed);
            store.SaveDanDefinition(samples.Passed);
            store.SaveDanDefinition(fresh);

            samples.Pass = Play(store, samples.Passed, 5, new[] { 18, 23, 31 }, PlayResultKind.Completed);
            Play(store, samples.Passed, 5, new[] { 40, 38, 30 }, PlayResultKind.Failed);
            store.WearMedal(Medal.DanKey(samples.Passed.Id));
            store.UnlockAchievement("first_dan_pass");
            store.WearMedal(Medal.AchievementKey("first_dan_pass"));
            samples.Fail = Play(store, samples.Failed, 0, new[] { 21, 17, 26, 36 }, PlayResultKind.Failed);
            samples.Quit = Play(store, samples.Failed, 0, new[] { 12, 9 }, PlayResultKind.Retired);
            samples.Resting = Play(null, samples.Failed, 0, new[] { 21, 17 }, PlayResultKind.Completed);
            samples.Store = store;
            return samples;
        }

        private static List<string> Links(int start, int count)
        {
            var links = new List<string>();
            for (int i = 0; i < count; i++)
            {
                links.Add("https://sample.test/levels/" + _titles[(start + i) % _titles.Length].ToLowerInvariant().Replace(' ', '-'));
            }

            return links;
        }

        /// <param name="costs">Life each song costs; with <paramref name="last"/> Failed the last song takes what is left.</param>
        /// <param name="store">Saves the attempt there; null only builds it.</param>
        /// <param name="first">Index of the course's first song in the sample titles (the same offset <see cref="Links"/> took).</param>
        private static DanAttempt Play(PlayerProfileStore store, DanDefinition definition, int first, int[] costs, PlayResultKind last)
        {
            DanAttempt attempt = DanAttempt.Begin(definition);
            int life = definition.InitialLife;
            for (int i = 0; i < costs.Length; i++)
            {
                bool final = i == costs.Length - 1;
                PlayResultKind kind = final ? last : PlayResultKind.Completed;
                int end = kind == PlayResultKind.Failed ? 0 : Math.Max(1, life - costs[i]);
                int misses = Math.Max(0, (life - end) / 10);
                int greats = Math.Max(0, life - end - misses * 10);
                int notes = 880 + i * 140;
                var score = new LiveScore
                {
                    life = end,
                    maxCombo = kind == PlayResultKind.Completed ? notes - misses * 31 : notes / 3,
                    totalComboCount = notes,
                    totalScore = kind == PlayResultKind.Completed ? 978000 - misses * 4200 - greats * 600 : 512000 + i * 9000,
                    perfectCount = Math.Max(0, notes - misses - greats - 3),
                    greatCount = greats,
                    goodCount = 2,
                    badCount = 1,
                    missCount = misses
                };
                long position = kind == PlayResultKind.Completed ? 128000 + i * 7000 : 61000 + i * 5000;
                var song = new DanSongResult("sample-dan-" + definition.Id.Substring(0, 6) + "-" + i, _titles[(first + i) % _titles.Length],
                    kind, position, life, score);
                attempt = attempt.Append(definition, song);
                life = end;
            }

            store?.SaveDanAttempt(definition, attempt);
            return attempt;
        }
    }
}
