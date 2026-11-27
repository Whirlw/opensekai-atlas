using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        private static readonly (ShopCategory Category, string Name)[] _shopFilters =
        {
            (ShopCategory.Avatar, "avatars"), (ShopCategory.Theme, "themes"), (ShopCategory.Background, "backgrounds")
        };

        /// <summary>Into the home card's coin: turning, the light crossing it, at rest with the goods further round.</summary>
        private static readonly float[] _shopCardTimes = { 0.45f, 2.9f, 9f };

        /// <summary>The wallet coin held part way round: coming off the face, half way, almost side on, the back coming in.</summary>
        private static readonly float[] _shopCoinTurns = { 0.06f, 0.14f, 0.23f, 0.33f };

        /// <summary>
        /// The 商店 part on the 成就 sample profile in a temporary folder (never the player's own):
        /// the second home page with the shop card at three times, the card growing, the page as it
        /// ships (nothing on sale: every kind's shelf, then one kind's), then a sample catalog with two
        /// things already bought: every item and each kind, the exchange card for something
        /// affordable, something owned and something too dear, one purchase as it lands and settled,
        /// back out to the second page, a new profile, and last that page's coin held at four points
        /// of a turn.
        /// </summary>
        private IEnumerator ShopShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            string root = Path.Combine(Path.GetTempPath(), "OpenSekaiShots-Shop-" + Guid.NewGuid().ToString("N"));
            ShopScreen screen = null;
            try
            {
                PlayerProfileStore sample = AchievementSamples.Build(Path.Combine(root, "sample"));
                var empty = new PlayerProfileStore(Path.Combine(root, "empty"));
                ShopCatalog catalog = ShopSamples.Catalog(sample.ReadCoins());
                home.ProfileStore = sample;
                home.SeekIntro(HomeScreen.IntroLength);
                yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
                yield return new WaitForSecondsRealtime(1.2f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return new WaitForSecondsRealtime(1f);
                foreach (float time in _shopCardTimes)
                {
                    home.HoldIdle(ShopCoin.BEAT * 4d + time);
                    yield return null;
                    yield return Capture(shot, stem + "-card" + Mathf.RoundToInt(time * 100f), "the home shop card", null, time);
                }

                home.SeekShopHandoff(0.5f);
                yield return null;
                yield return null;
                yield return Capture(shot, stem + "-handoff", "商店 card grown halfway", null, 0.5f);

                home.ShowHomeImmediately();
                home.SeekHomePage(1f);
                yield return null;
                home.EnterShop();
                yield return WaitUntil(() => home.IsShopShown, "the 商店 page after the hand-over");
                yield return new WaitForSecondsRealtime(1.4f);
                screen = UnityEngine.Object.FindAnyObjectByType<ShopScreen>();
                if (screen == null)
                {
                    throw new InvalidOperationException("The 商店 part did not open.");
                }

                yield return Capture(shot, stem + "-coming", "nothing on sale yet: every kind's shelf", null);
                screen.SetFilter(ShopCategory.Theme);
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Capture(shot, stem + "-coming-theme", "nothing on sale yet: the 主题 shelf", null);

                // Bought only now: with nothing on sale, their records would read as the items' ids.
                ShopSamples.BuySome(sample, catalog);
                screen.Catalog = catalog;
                screen.SetFilter(ShopCategory.All);
                screen.Reload();
                screen.View.BindGoods(true);
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Capture(shot, stem + "-goods", "a sample catalog, two things bought", null);
                foreach ((ShopCategory category, string name) in _shopFilters)
                {
                    screen.SetFilter(category);
                    yield return new WaitForSecondsRealtime(0.7f);
                    yield return Capture(shot, stem + "-" + name, "the " + name, null);
                }

                screen.SetFilter(ShopCategory.All);
                yield return new WaitForSecondsRealtime(0.7f);
                ShopItem next = ShopSamples.Cheapest(screen, catalog);
                foreach ((ShopItem item, string name) in new[]
                {
                    (next, "exchange"), (catalog.Find(ShopSamples.OWNED_THEME), "owned"), (catalog.Find(ShopSamples.DEAR), "short")
                })
                {
                    if (item == null) continue;
                    screen.OpenItem(item);
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Capture(shot, stem + "-card-" + name, "the exchange card: " + name, null);
                    screen.PurchasePanel.Close();
                }

                if (next != null)
                {
                    screen.OpenItem(next);
                    yield return new WaitForSecondsRealtime(0.4f);
                    screen.Exchange(next);
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Capture(shot, stem + "-bought", "buying " + next.Title + ": the tile pops, the balance counts down", null);
                    yield return new WaitForSecondsRealtime(1.4f);
                    yield return Capture(shot, stem + "-bought-settled", "after it", null);
                }

                screen.Back();
                yield return WaitUntil(() => home.IsHomeShown && !home.IsShopShown, "the home page after back");
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-return", "after the 商店 back button, on page " + (home.HomePage + 1), null);

                screen.Catalog = null;
                home.ProfileStore = empty;
                home.ShowShopImmediately();
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(shot, stem + "-empty", "a new profile, nothing on sale", null);
                foreach (float turn in _shopCoinTurns)
                {
                    screen.View.HoldCoin(turn);
                    yield return null;
                    yield return Capture(shot, stem + "-coin" + Mathf.RoundToInt(turn * 100f), "the wallet coin " + turn + " of a turn round", null);
                }
            }
            finally
            {
                if (screen != null) screen.Catalog = null;
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

    /// <summary>A made-up catalog for screenshots: what the 商店 could look like with goods on sale.</summary>
    internal static class ShopSamples
    {
        internal const string OWNED_AVATAR = "sample-avatar-halo";
        internal const string OWNED_THEME = "sample-theme-dusk";
        internal const string DEAR = "sample-background-starsea";

        /// <param name="balance">The sample profile's coins: the dearest item costs more than them.</param>
        internal static ShopCatalog Catalog(long balance)
        {
            Color32 violet = HomeTheme.Colors.Violet;
            Color32 rose = HomeTheme.Colors.Rose;
            Color32 gold = HomeTheme.Colors.Gold;
            Color32 periwinkle = HomeTheme.Colors.Periwinkle;
            var teal = new Color32(110, 214, 192, 255);
            int dear = (int)Math.Max(800L, (balance + 260L) / 10L * 10L);
            return new ShopCatalog(new List<ShopItem>
            {
                new ShopItem(OWNED_AVATAR, ShopCategory.Avatar, "光环", "一圈柔光围着头像缓缓转动", 120, violet, avatarId: "halo"),
                new ShopItem("sample-avatar-wave", ShopCategory.Avatar, "声波", "随节拍起伏的声波", 160, periwinkle, avatarId: "wave"),
                new ShopItem("sample-avatar-sparkle", ShopCategory.Avatar, "星辉", "四角星和细碎的光点", 240, gold, avatarId: "sparkle"),
                new ShopItem("sample-avatar-orbit", ShopCategory.Avatar, "轨道", "绕着中心运行的小行星", 300, rose, avatarId: "orbit"),
                new ShopItem(OWNED_THEME, ShopCategory.Theme, "黄昏", "暖色的光晕和金色强调", 400, rose, palette: new[] { rose, gold, violet }),
                new ShopItem("sample-theme-deep", ShopCategory.Theme, "深海", "冷色调，青绿的强调色", 400, teal, palette: new[] { teal, periwinkle, violet }),
                new ShopItem("sample-background-aurora", ShopCategory.Background, "极光", "轨道上方缓缓流动的极光", 500, periwinkle,
                    palette: new[] { periwinkle, violet, teal }),
                new ShopItem(DEAR, ShopCategory.Background, "星海", "满天星光下的舞台", dear, violet, palette: new[] { violet, rose, periwinkle })
            });
        }

        /// <summary>Buys the 光环 avatar and the 黄昏 theme, as far as the coins go.</summary>
        internal static void BuySome(PlayerProfileStore store, ShopCatalog catalog)
        {
            foreach (string id in new[] { OWNED_AVATAR, OWNED_THEME })
            {
                ShopItem item = catalog.Find(id);
                if (store.ReadCoins() >= item.Price) store.SpendCoins(ShopCopy.TransactionId(item), item.Id, item.Price);
            }
        }

        /// <summary>The cheapest item not owned that the balance covers; null when there is none.</summary>
        internal static ShopItem Cheapest(ShopScreen screen, ShopCatalog catalog)
        {
            ShopItem cheapest = null;
            foreach (ShopItem item in catalog.Items)
            {
                if (screen.IsOwned(item) || item.Price > screen.Balance) continue;
                if (cheapest == null || item.Price < cheapest.Price) cheapest = item;
            }

            return cheapest;
        }
    }
}
