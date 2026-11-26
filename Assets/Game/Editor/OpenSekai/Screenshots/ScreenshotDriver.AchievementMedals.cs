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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        private static readonly float[] _specialTimes = { 0.35f, 1.2f, 2.1f, 3f };

        /// <summary>
        /// The achievements' medals and avatars on a sample profile in a temporary folder: every series'
        /// emblem, the special medals at each size with their motions held at several times, all the
        /// achievements' avatars, and then the pages that show them: the home card, the profile, the
        /// medal picker, the avatar picker and the 成就 page's special rows and reward cards.
        /// </summary>
        private IEnumerator MedalsShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null) throw new InvalidOperationException("The Library scene has no home page.");
            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Medals-" + Guid.NewGuid().ToString("N"));
            GameObject gallery = null;
            try
            {
                PlayerProfileStore sample = AchievementSamples.BuildMaster(Path.Combine(root, "sample"));
                home.ProfileStore = sample;
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1f);

                gallery = BuildSeriesGallery();
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-series", "every series' highest plain medal at each size", null);
                UnityEngine.Object.Destroy(gallery);
                foreach (float time in _specialTimes)
                {
                    MedalClock.Held = time;
                    gallery = BuildSpecialGallery();
                    yield return null;
                    yield return null;
                    yield return Capture(shot, stem + "-special" + Mathf.RoundToInt(time * 100f), "the special medals", null, time);
                    UnityEngine.Object.Destroy(gallery);
                }

                gallery = BuildAvatarGallery();
                yield return null;
                yield return Capture(shot, stem + "-avatars", "the achievements' avatars", null);
                UnityEngine.Object.Destroy(gallery);

                string[][] worn =
                {
                    new[] { "FCLVL-14", "APLVL-14" }, new[] { "PRISM-01", "CODA-01" }, new[] { "ENCORE-01", "FCLVL-11" }, new[] { "APLVL-12", "FCLVL-08" }
                };
                for (int i = 0; i < worn.Length; i++)
                {
                    for (int slot = 0; slot < worn[i].Length; slot++) sample.WearMedal(slot, Medal.AchievementKey(worn[i][slot]));
                    sample.SetAppearance("Atlas", new[] { "prism_eye", "seventh_light", "ranker", "flawless_atlas" }[i]);
                    MedalClock.Held = 1.2f + i * 0.6f;
                    home.ShowHomeImmediately();
                    home.SeekHomePage(1f);
                    home.ProfileStore = null;
                    home.ProfileStore = sample;
                    home.ShowProfileImmediately();
                    yield return new WaitForSecondsRealtime(0.9f);
                    yield return Capture(shot, stem + "-profile" + (i + 1), "the profile wearing " + string.Join(" and ", worn[i]), null);
                    home.ShowHomeImmediately();
                    home.SeekHomePage(1f);
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Capture(shot, stem + "-home" + (i + 1), "the home card wearing " + string.Join(" and ", worn[i]), null);
                }

                home.SeekProfileHandoff(0.4f);
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-handoff", "the card growing over the page, its medals fading", null, 0.4f);
                home.ShowProfileImmediately();
                yield return new WaitForSecondsRealtime(0.8f);
                ProfileScreen profile = UnityEngine.Object.FindFirstObjectByType<ProfileScreen>();
                profile.OpenMedals(0);
                yield return new WaitForSecondsRealtime(0.6f);
                ScrollRect picker = FindScroll(profile.gameObject, "MedalPicker");
                for (int i = 0; i < 4; i++)
                {
                    picker.verticalNormalizedPosition = 1f - i / 3f;
                    yield return null;
                    yield return null;
                    yield return Capture(shot, stem + "-picker" + i, "choosing a medal, part " + (i + 1), null);
                }

                profile.TryHandleBack();
                yield return new WaitForSecondsRealtime(0.4f);
                profile.OpenEditor();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Capture(shot, stem + "-edit", "choosing an avatar", null);
                FindScroll(profile.gameObject, "ProfileEdit").verticalNormalizedPosition = 0f;
                yield return null;
                yield return Capture(shot, stem + "-edit-end", "choosing an avatar, the end of the list", null);
                profile.TryHandleBack();

                home.ShowAchievementsImmediately();
                yield return new WaitForSecondsRealtime(0.8f);
                AchievementScreen screen = UnityEngine.Object.FindAnyObjectByType<AchievementScreen>();
                yield return Capture(shot, stem + "-page", "the page of a player far along", null);
                screen.SetFilter(AchievementCategory.Special);
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Capture(shot, stem + "-page-special", "the special goals unlocked", null);
                screen.SetFilter(AchievementCategory.Technique);
                yield return new WaitForSecondsRealtime(0.9f);
                foreach (string series in new[] { "FCLVL-14", "APLVL-14" })
                {
                    AchievementRow row = screen.View.FindRow(series);
                    ScrollRect list = row.Root.GetComponentInParent<ScrollRect>();
                    list.content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, row.Top - 20f));
                    yield return null;
                    yield return null;
                    yield return Capture(shot, stem + "-page-" + series, "the row of " + series, null);
                }

                foreach (string id in new[] { "FCLVL-11", "FCLVL-14", "APLVL-12", "APLVL-14", "PRISM-01", "ENCORE-01", "CODA-01", "SET-06", "B30-06" })
                {
                    MedalClock.Held = 2.2f;
                    screen.ShowReward(FindGoal(screen, id));
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Capture(shot, stem + "-reward-" + id, "what " + id + " gives", null);
                    screen.HideReward();
                }

                foreach (float time in new[] { 0.08f, 0.3f, 1.6f })
                {
                    screen.PreviewUnlock(new[] { "FCLVL-14", "APLVL-11", "ENCORE-01" }, null, time);
                    yield return null;
                    yield return Capture(shot, stem + "-unlock" + Mathf.RoundToInt(time * 100f), "the card for special goals reached", null, time);
                }

                screen.EndPreview();
            }
            finally
            {
                MedalClock.Held = null;
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

        private static ScrollRect FindScroll(GameObject root, string name)
        {
            foreach (ScrollRect scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                for (Transform at = scroll.transform; at != null; at = at.parent)
                    if (at.name == name) return scroll;
            }

            throw new InvalidOperationException("No list under " + name);
        }

        private static RectTransform GalleryRoot(string name, out GameObject host)
        {
            host = new GameObject("[" + name + "]", typeof(RectTransform));
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)host.transform;
            Image ground = LibraryUi.CreateImage("Ground", root, HomeTheme.Colors.Card);
            LibraryUi.Stretch(ground.rectTransform);
            return root;
        }

        private static MedalLook LookOf(string id) => MedalLook.Of(AchievementCopy.For(id));

        private static void AddDisc(RectTransform root, string id, AchievementState state, float x, float y, float size, bool ring)
        {
            var medal = new AchievementMedal(root, "Disc", size, ring);
            LibraryWidgets.Place(medal.Root, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -y), new Vector2(size, size));
            medal.Bind(AchievementCopy.For(id), state, state == AchievementState.InProgress ? 0.6f : 1f);
        }

        private static void AddThumb(RectTransform root, string id, float left, float top)
        {
            var thumb = new MedalThumb(root, "Thumb", new Vector2(84f, 42f));
            LibraryWidgets.Place(thumb.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), new Vector2(84f, 42f));
            thumb.SetLook(LookOf(id));
        }

        /// <summary>The last plain level of every series that gives a medal, and the discs of those that give none.</summary>
        private static GameObject BuildSeriesGallery()
        {
            RectTransform root = GalleryRoot("Series gallery", out GameObject host);
            List<AchievementGoal> goals = AchievementGoals.All.GroupBy(goal => goal.Series)
                .Select(steps => steps.LastOrDefault(goal => goal.GivesMedal && goal.MedalTier < MedalTiers.SPECIAL) ?? steps.Last())
                .Where(goal => goal.Category != AchievementCategoryGroup.Special).ToList();
            goals.AddRange(AchievementGoals.All.Where(goal => goal.Series == "B30" && goal.Id != "B30-06"));
            for (int i = 0; i < goals.Count; i++)
            {
                float left = 24f + i % 3 * 632f;
                float top = 18f + i / 3 * 96f;
                string id = goals[i].Id;
                AddDisc(root, id, AchievementState.Unlocked, left + 42f, top + 42f, 80f, false);
                if (!goals[i].GivesMedal) continue;
                AddMedal(root, LookOf(id), left + 96f, top + 14f, new Vector2(300f, 60f), false);
                AddThumb(root, id, left + 416f, top + 22f);
                AddChip(root, LookOf(id), left + 520f, top + 26f, 104f);
            }

            return host;
        }

        private static readonly string[] _specialIds =
        {
            "FCLVL-11", "FCLVL-12", "FCLVL-13", "FCLVL-14", "APLVL-11", "APLVL-12", "APLVL-13", "APLVL-14", "PRISM-01", "ENCORE-01", "CODA-01"
        };

        private static GameObject BuildSpecialGallery()
        {
            RectTransform root = GalleryRoot("Special gallery", out GameObject host);
            for (int i = 0; i < _specialIds.Length; i++)
            {
                float top = 16f + i * 96f;
                string id = _specialIds[i];
                AddDisc(root, id, AchievementState.Unlocked, 64f, top + 48f, 72f, false);
                AddDisc(root, id, AchievementState.InProgress, 160f, top + 48f, 72f, true);
                AddMedal(root, LookOf(id), 230f, top + 8f, new Vector2(400f, 80f), false);
                AddMedal(root, LookOf(id), 680f, top + 17f, new Vector2(488f, 62f), false);
                AddMedal(root, LookOf(id), 1206f, top + 18f, new Vector2(258f, 60f), true);
                AddThumb(root, id, 1506f, top + 27f);
                AddChip(root, LookOf(id), 1632f, top + 30f);
            }

            return host;
        }

        private static GameObject BuildAvatarGallery()
        {
            RectTransform root = GalleryRoot("Avatar gallery", out GameObject host);
            List<AchievementGoal> givers = AchievementGoals.All.Where(goal => goal.AvatarId != null).ToList();
            for (int i = 0; i < givers.Count; i++)
            {
                float x = 170f + i % 6 * 316f;
                float y = 160f + i / 6 * 350f;
                foreach ((float size, float dx, float dy) in new[] { (220f, 0f, 0f), (56f, -70f, 150f), (40f, 0f, 150f), (104f, 92f, 150f) })
                {
                    var view = new ProfileAvatarView(root, "Avatar", size, glow: size > 200f);
                    LibraryWidgets.Place(view.Root, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + dx, -(y + dy)), new Vector2(size, size));
                    view.SetAvatar(givers[i].AvatarId);
                }

                TextMeshProUGUI name = LibraryWidgets.Label("Name", root, givers[i].AvatarName, 20f, LibraryTheme.Colors.TextSecondary,
                    FontStyles.Normal, TextAlignmentOptions.Center);
                LibraryWidgets.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -(y - 136f)), new Vector2(260f, 30f));
            }

            return host;
        }
    }

    internal static partial class AchievementSamples
    {
        /// <summary>
        /// A player who has done nearly everything: an ALL PERFECT at Lv.39, the five colors in a row, the
        /// same Lv.33 chart a hundred times, a course of four Lv.32 ALL PERFECTs and thirty charts for the
        /// Ranking Score; every goal it reached is unlocked.
        /// </summary>
        internal static PlayerProfileStore BuildMaster(string root)
        {
            var store = new PlayerProfileStore(root);
            store.SetAppearance("Atlas", "halo");
            DateTime start = DateTime.UtcNow.AddDays(-30);
            int n = 0;
            void Play(string title, int level, string difficulty, bool perfect)
            {
                store.History.Submit(Record("master-" + n, title, level, PlayResultKind.Completed, true, start.AddMinutes(n * 6), difficulty, perfect),
                    null, out _);
                n++;
            }

            foreach (string difficulty in new[] { "expert", "hard", "easy", "normal", "master" }) Play("Prism " + difficulty, 34, difficulty, true);
            for (int i = 0; i < 30; i++) Play("Summit " + i, 36 + i % 4, "master", true);
            for (int i = 0; i < 100; i++) Play("Encore", 33, "master", false);

            var course = new DanDefinition(100, new DanDamage(0, 1, 2, 3, 4),
                new List<string> { "https://sample.test/levels/a", "https://sample.test/levels/b", "https://sample.test/levels/c", "https://sample.test/levels/d" },
                "四曲终章", "star", "#B692FF", MedalTiers.TOP, "X");
            store.SaveDanDefinition(course);
            DanAttempt attempt = DanAttempt.Begin(course);
            for (int i = 0; i < 4; i++)
            {
                attempt = attempt.Append(course, new DanSongResult("master-dan-" + i, "Finale " + (i + 1), PlayResultKind.Completed, 130000, 100,
                    new LiveScore { life = 100, totalComboCount = 1000, maxCombo = 1000, perfectCount = 1000, totalScore = 1000000 },
                    DateTimeOffset.UtcNow.AddMinutes(-40 + i * 3), true, 33));
            }

            store.SaveDanAttempt(course, attempt);
            foreach (AchievementProgress goal in store.ReadAchievements())
            {
                if (goal.State == AchievementState.Claimable) store.UnlockAchievement(goal.Id);
            }

            return store;
        }
    }
}
