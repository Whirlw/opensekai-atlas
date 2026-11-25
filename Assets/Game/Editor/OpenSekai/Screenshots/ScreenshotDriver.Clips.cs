using System;
using System.Collections;
using System.IO;
using System.Linq;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        // Both pages display sample timestamps down to the minute.
        private static readonly DateTimeOffset _clipDate = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private IEnumerator ClipShot(Shot shot)
        {
            int count = shot.name == "tier-upgrade" ? RankingTiers.Count : shot.name == "achievement-card" ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                string stem = shot.name;
                if (shot.name == "tier-upgrade") stem += "-" + i + "-" + RankingTiers.LatinName(i).ToLowerInvariant();
                if (shot.name == "achievement-card") stem += i == 0 ? "-single" : "-multi";
                BeginSequence(shot, stem);
                switch (shot.name)
                {
                    case "tier-upgrade": yield return TierClip(shot, i); break;
                    case "dan-award": yield return AwardClip(shot); break;
                    case "achievement-unlock": yield return UnlockClip(shot); break;
                    case "achievement-card": yield return GalleryClip(shot, i); break;
                    case "special-medals": yield return GalleryClip(shot, i); break;
                }
                EndSequence();
            }
        }

        private IEnumerator TierClip(Shot shot, int tier)
        {
            RectTransform root = GalleryRoot("Tier upgrade", out GameObject host);
            try
            {
                Image ground = root.Find("Ground").GetComponent<Image>();
                ground.color = _clipGround;
                const float SIZE = 480f;
                Image flare = LibraryUi.CreateImage("Flare", root, Color.clear);
                flare.sprite = LibraryShapes.Glow();
                var center = new Vector2(0.5f, 0.5f);
                LibraryWidgets.Place(flare.rectTransform, center, center, Vector2.zero, Vector2.one * (SIZE * 2f));
                RectTransform slot = LibraryUi.CreateRect("Badge", root);
                LibraryWidgets.Place(slot, center, center, Vector2.zero, Vector2.one * SIZE);
                // Draw the crest at the 4K canvas density. Scale the slot, leaving the upgrade's own scale free.
                const float DENSITY = 1.35f;
                var badge = new RankingBadge(slot, SIZE * DENSITY);
                slot.localScale = Vector3.one / DENSITY;
                int lower = Math.Max(0, tier - 1);
                foreach (int needed in new[] { lower, tier })
                {
                    if (!RankingCrestArt.HasCrest(needed)) continue;
                    yield return WaitUntil(() =>
                    {
                        if (!RankingCrests.TryGet(needed, SIZE * DENSITY, out var sprites)) return false;
                        if (!sprites.HasCore) throw new InvalidOperationException("Could not draw tier crest " + needed);
                        return true;
                    }, "tier crest " + needed);
                }
                Color32 tint = RankingTiers.IsPrismatic(tier) ? LibraryTheme.Colors.AllPerfectStops[1] : RankingTiers.Color(tier);
                yield return DrawSequence(shot, time =>
                {
                    badge.SetTier(time < 0f ? lower : tier);
                    badge.SetUpgrade(time);
                    badge.Tick(time + 0.5d);
                    float light = time < 0f ? 0f : 1f - HomeMotion.Span(time, 0f, RankingBadge.UPGRADE_BURST + 0.1f);
                    flare.color = HomeTheme.Colors.WithAlpha(tint, (byte)Mathf.RoundToInt(light * light * (60f + 16f * tier)));
                }, ground);
            }
            finally { Object.Destroy(host); }
        }

        private IEnumerator GalleryClip(Shot shot, int variant)
        {
            bool medals = shot.name == "special-medals";
            GameObject host;
            if (medals) host = BuildSpecialGallery();
            else GalleryRoot("Achievement card", out host);
            AchievementUnlockPopup card = null;
            try
            {
                Image ground = host.transform.Find("Ground").GetComponent<Image>();
                ground.color = _clipGround;
                Canvas.ForceUpdateCanvases();
                if (!medals)
                {
                    card = new AchievementUnlockPopup((RectTransform)host.transform, 18f);
                    string[] ids = variant == 0 ? new[] { "FC-01" } : new[] { "HIT-01", "FC-02", "AP-01" };
                    card.Bind(ids, variant == 0 ? null : "合并备份后可解锁");
                }
                yield return DrawSequence(shot, time => card?.Seek(time), ground);
            }
            finally
            {
                card?.Dispose();
                Object.Destroy(host);
            }
        }

        private IEnumerator AwardClip(Shot shot)
        {
            string temp = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Dan-" + Guid.NewGuid().ToString("N"));
            RectTransform root = GalleryRoot("Dan award", out GameObject host);
            DanResultPage page = null;
            DanAwardCeremony ceremony = null;
            try
            {
                DanSamples samples = DanSamples.Build(temp);
                DanAttempt pass = samples.Pass;
                var dated = new DanAttempt(pass.Id, pass.DefinitionId, _clipDate, pass.Outcome, pass.Songs.ToArray());
                var model = new DanResultModel(samples.Passed, dated, true, false, null, Array.Empty<DanSongInfo>());
                page = new DanResultPage(root, null, null, null);
                page.Show(model, false);
                Canvas.ForceUpdateCanvases();
                ceremony = new DanAwardCeremony(root, null, null);
                ceremony.Show(model, true);
                if (shot.clean)
                {
                    root.Find("DanAward/Copy").gameObject.SetActive(false);
                    root.Find("DanAward/Buttons").gameObject.SetActive(false);
                }
                yield return DrawSequence(shot, null);
            }
            finally
            {
                ceremony?.Dispose();
                page?.Dispose();
                Object.Destroy(host);
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        private IEnumerator UnlockClip(Shot shot)
        {
            string temp = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Achievement-" + Guid.NewGuid().ToString("N"));
            HomeScreen home = HomeScreen.Current;
            PlayerProfileStore previous = home.ProfileStore;
            try
            {
                home.ProfileStore = AchievementSamples.Build(temp, _clipDate);
                home.ShowAchievementsImmediately();
                var screen = Object.FindFirstObjectByType<AchievementScreen>();
                Canvas.ForceUpdateCanvases();
                _homeClock.Update(1.4d);
                yield return null;
                AchievementRow row = screen.View.FindRow("FC-01");
                AchievementSeries after = screen.Unlock("FC-01");
                if (row == null || after == null) throw new InvalidOperationException("FC-01 could not be unlocked.");
                yield return DrawSequence(shot, time => row.SeekUnlock(after, time));
            }
            finally
            {
                home.ShowLibraryImmediately();
                home.ProfileStore = previous;
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }
    }
}
