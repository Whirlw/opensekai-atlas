using System;
using System.Collections;
using System.IO;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        /// <summary>Idle time the tier frames hold at: the first shooting star is mid-flight and a pulse half out.</summary>
        private const double RANKING_IDLE_TIME = 11.45d;

        /// <summary>A total inside each tier, for the card's number.</summary>
        private static readonly long[] _rankingTotals = { 3120, 7480, 8650, 9540, 10230, 10720, 11360 };

        private static readonly float[] _galleryRows = { 200f, 148f, 118f, 58f };
        private static readonly float[] _revealFrames = { 0.12f, 0.3f, 0.55f };

        /// <summary>The player's score in the 评级徽章 card: inside 银河, so tiers on both sides of it show.</summary>
        private const long GALLERY_TOTAL = 9540;

        /// <summary>Where the 评级徽章 card's replay frames hold, in seconds from the tier showing.</summary>
        private static readonly float[] _replayFrames = { -0.1f, 0.15f };

        /// <summary>
        /// Every Ranking Score tier: the second home page with the 个人信息 card on each tier (its
        /// idle motion held at one time, on an empty profile in a temporary folder, so the player's
        /// own is never shown), then a gallery of the badges at the sizes the game draws them
        /// (about the home card's, the result page's, the profile's and a history row's), three
        /// frames of each tier's upgrade side by side, and the 评级徽章 card the profile's badge
        /// opens, at rest and with 寰宇's replay charging and bursting.
        /// </summary>
        private IEnumerator RankingShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Ranking-" + Guid.NewGuid().ToString("N"));
            GameObject gallery = null;
            try
            {
                home.ProfileStore = new PlayerProfileStore(Path.Combine(root, "empty"));
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1.2f);
                home.ShowHomeImmediately();
                home.SeekHomePage(1f);

                for (int tier = 0; tier < RankingTiers.Count; tier++)
                {
                    var summary = new HomeProfileSummary("Player", ProfileAvatars.DEFAULT_ID, _rankingTotals[tier], tier,
                        tier == 0 ? 12 : 30, 96 + tier * 41, null, false);
                    home.PreviewProfileCard(summary, RANKING_IDLE_TIME);
                    yield return WaitForCrests();
                    home.PreviewProfileCard(summary, RANKING_IDLE_TIME);
                    yield return null;
                    string name = RankingTiers.LatinName(tier).ToLowerInvariant();
                    yield return Capture(shot, stem + "-home-" + tier + "-" + name, "home card, " + RankingTiers.Name(tier), null);
                }

                home.ShowHomeImmediately();
                gallery = BuildGallery(out RankingBadge[,] badges, out RankingBadge[] reveals);
                yield return WaitForCrests();
                foreach (RankingBadge badge in badges) badge.Tick(1.3d);
                yield return null;
                yield return Capture(shot, stem + "-badges", "badges at 200, 148, 118 and 58", null);

                gallery.transform.Find("Grid").gameObject.SetActive(false);
                gallery.transform.Find("Upgrade").gameObject.SetActive(true);
                foreach (float reveal in _revealFrames)
                {
                    foreach (RankingBadge badge in reveals)
                    {
                        badge.SetUpgrade(reveal * RankingBadge.UPGRADE_BURST);
                        badge.Tick(1.3d);
                    }

                    yield return null;
                    yield return Capture(shot, stem + "-upgrade-" + reveal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                        "each tier's upgrade, " + reveal + " of the way", null, reveal);
                }

                gallery.transform.Find("Upgrade").gameObject.SetActive(false);
                var tiers = new RankingBadgeGallery((RectTransform)gallery.transform);
                tiers.Open(GALLERY_TOTAL);
                yield return WaitForCrests();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(shot, stem + "-gallery", "评级徽章 card at " + GALLERY_TOTAL, null);
                foreach (float time in _replayFrames)
                {
                    tiers.SeekReplay(RankingTiers.TOP, time);
                    yield return null;
                    string at = time.ToString("+0.00;-0.00", System.Globalization.CultureInfo.InvariantCulture);
                    yield return Capture(shot, stem + "-gallery-replay" + at, "评级徽章 card, 寰宇's replay at " + at + " s", null, time);
                }

                tiers.Dispose();
            }
            finally
            {
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

        /// <summary>Waits for every crest to finish drawing and fade up.</summary>
        private static IEnumerator WaitForCrests()
        {
            yield return WaitUntil(() => !RankingCrests.IsBusy, "the Ranking Score crests");
            yield return new WaitForSecondsRealtime(0.45f);
        }

        /// <summary>
        /// An overlay canvas on the home page's ground: one column per tier with its badge at each
        /// gallery size under its name, and a hidden row of result-size badges for the upgrade frames.
        /// </summary>
        private static GameObject BuildGallery(out RankingBadge[,] badges, out RankingBadge[] reveals)
        {
            var host = new GameObject("[Ranking gallery]", typeof(RectTransform));
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var rootRect = (RectTransform)host.transform;
            Image ground = LibraryUi.CreateImage("Ground", rootRect, HomeTheme.Colors.Card);
            LibraryUi.Stretch(ground.rectTransform);

            int tiers = RankingTiers.Count;
            badges = new RankingBadge[tiers, _galleryRows.Length];
            reveals = new RankingBadge[tiers];
            RectTransform grid = LibraryUi.CreateRect("Grid", rootRect);
            LibraryUi.Stretch(grid);
            RectTransform upgrade = LibraryUi.CreateRect("Upgrade", rootRect);
            LibraryUi.Stretch(upgrade);
            upgrade.gameObject.SetActive(false);
            for (int tier = 0; tier < tiers; tier++)
            {
                float x = (tier + 0.5f) / tiers;
                TextMeshProUGUI label = LibraryWidgets.Label("Name" + tier, grid, RankingTiers.Name(tier) + "  " + RankingTiers.LatinName(tier),
                    22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
                RankingTiers.Paint(label, tier);
                LibraryWidgets.Place(label.rectTransform, new Vector2(x, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(260f, 34f));
                float top = 90f;
                for (int row = 0; row < _galleryRows.Length; row++)
                {
                    float size = _galleryRows[row];
                    float cell = size * 1.32f + 16f;
                    RectTransform slot = LibraryUi.CreateRect("Slot", grid);
                    LibraryWidgets.Place(slot, new Vector2(x, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -(top + cell * 0.5f)), new Vector2(size, size));
                    var badge = new RankingBadge(slot, size);
                    badge.SetTier(tier);
                    badges[tier, row] = badge;
                    top += cell;
                }

                RectTransform revealSlot = LibraryUi.CreateRect("Reveal" + tier, upgrade);
                LibraryWidgets.Place(revealSlot, new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(148f, 148f));
                var reveal = new RankingBadge(revealSlot, 148f);
                reveal.SetTier(tier);
                reveals[tier] = reveal;
                TextMeshProUGUI caption = LibraryWidgets.Label("Caption" + tier, upgrade, RankingTiers.Name(tier), 22f,
                    LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
                RankingTiers.Paint(caption, tier);
                LibraryWidgets.Place(caption.rectTransform, new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(200f, 34f));
            }

            return host;
        }
    }
}
