using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LitMotion;
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
    public sealed class ShopPageTests
    {
        private static readonly DateTimeOffset _epoch = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Regex _tileName = new Regex(@"^Item\d+$");
        private static readonly Regex _rowName = new Regex(@"^Purchase\d+$");

        private readonly List<ShopScreen> _screens = new List<ShopScreen>();
        private string _root;
        private GameObject _canvas;
        private RankingHistory _previousHistory;
        private IMotionScheduler _previousScheduler;
        private ManualMotionDispatcher _clock;
        private float _previousVolume;

        [SetUp]
        public void SetUp()
        {
            _previousVolume = UiSound.Volume;
            UiSound.Volume = 0f;
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OpenSekai-Shop-" + Guid.NewGuid().ToString("N"))).FullName;
            _previousHistory = CustomMusicScorePlayHistoryStorage.Ranking;
            CustomMusicScorePlayHistoryStorage.Ranking = new RankingHistory(Path.Combine(_root, "results", "PlayHistory.jsonl"),
                new PlayHistorySummaryCache());
            _previousScheduler = HomeMotion.CaptureScheduler;
            _clock = new ManualMotionDispatcher();
            HomeMotion.CaptureScheduler = _clock.Scheduler;
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_canvas);
            foreach (ShopScreen screen in _screens)
            {
                if (screen == null) continue;
                // Edit mode never calls OnDestroy, so the page's motions end here.
                screen.SetPresented(false, false);
                UnityEngine.Object.DestroyImmediate(screen.gameObject);
            }

            _screens.Clear();
            HomeMotion.CaptureScheduler = _previousScheduler;
            CustomMusicScorePlayHistoryStorage.Ranking = _previousHistory;
            UiSound.Volume = _previousVolume;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ACatalogKeepsItsShelfOrderAndCountsEachKind()
        {
            ShopCatalog catalog = Sample();
            Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { "avatar-halo", "theme-dusk", "avatar-wave", "background-aurora" }));
            Assert.That(catalog.Count(ShopCategory.All), Is.EqualTo(4));
            Assert.That(catalog.Count(ShopCategory.Avatar), Is.EqualTo(2));
            Assert.That(catalog.Count(ShopCategory.Theme), Is.EqualTo(1));
            Assert.That(catalog.In(ShopCategory.Avatar).Select(item => item.Id), Is.EqualTo(new[] { "avatar-halo", "avatar-wave" }));
            Assert.That(catalog.Find("theme-dusk").Title, Is.EqualTo("黄昏"));
            Assert.That(catalog.Find("节拍头像"), Is.Null);

            Assert.Throws<ArgumentException>(() => new ShopCatalog(new[] { Avatar("same", 10), Avatar("same", 20) }));
            Assert.Throws<ArgumentException>(() => new ShopItem("all", ShopCategory.All, "全部", null, 10, Color.white));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShopItem("free", ShopCategory.Theme, "免费", null, 0, Color.white));
            Assert.That(ShopCatalog.Listed.Items, Is.Empty, "nothing is on sale yet");
        }

        [Test]
        public void WithNothingOnSaleEveryKindSaysWhatIsComing()
        {
            PlayerProfileStore store = WithCoins();
            ShopScreen screen = Open(store);
            Assert.That(Text(screen.gameObject, "Balance"), Is.EqualTo(ShopCopy.Coins(store.ReadCoins())));
            Assert.That(Tiles(screen), Is.Zero);
            Assert.That(Shelves(screen), Is.EqualTo(new[] { "ShelfAvatar", "ShelfTheme", "ShelfBackground" }));
            Assert.That(Text(screen.gameObject, "ShelfTitle"), Is.EqualTo("商品正在准备中，先攒一些金币吧"));
            Assert.That(Text(screen.gameObject, "OwnedCount"), Is.Empty);
            Assert.That(Find(screen.gameObject, "Empty").activeSelf, Is.True, "no purchases yet");

            screen.SetFilter(ShopCategory.Background);
            Assert.That(Shelves(screen), Is.EqualTo(new[] { "ShelfBackground" }));
            Assert.That(Text(screen.gameObject, "ShelfTitle"), Is.EqualTo("游戏背景即将上架"));
        }

        [Test]
        public void TheGoodsAreFilteredByKindAndAKindWithoutGoodsShowsItsShelf()
        {
            ShopScreen screen = Open(WithCoins(), Sample());
            Assert.That(Tiles(screen), Is.EqualTo(4));
            Assert.That(Shelves(screen), Is.Empty);
            Assert.That(Text(screen.gameObject, "OwnedCount"), Is.EqualTo("已拥有 0 / 4"));

            screen.SetFilter(ShopCategory.Avatar);
            Assert.That(Tiles(screen), Is.EqualTo(2));
            Assert.That(screen.View.FindTile("avatar-wave"), Is.Not.Null);
            Assert.That(screen.View.FindTile("theme-dusk"), Is.Null);

            var catalog = new ShopCatalog(new[] { Avatar("avatar-halo", 30) });
            screen.Catalog = catalog;
            screen.Reload();
            screen.SetFilter(ShopCategory.Theme);
            Assert.That(Tiles(screen), Is.Zero);
            Assert.That(Shelves(screen), Is.EqualTo(new[] { "ShelfTheme" }));
        }

        [Test]
        public void ExchangingSpendsThePriceOnceAndTheItemIsOwnedFromThen()
        {
            PlayerProfileStore store = WithCoins();
            long before = store.ReadCoins();
            ShopScreen screen = Open(store, Sample());
            ShopItem halo = screen.Catalog.Find("avatar-halo");
            screen.OpenItem(halo);
            Assert.That(screen.PurchasePanel.IsOpen, Is.True);
            Assert.That(screen.PurchasePanel.ExchangeButton.interactable, Is.True);
            Assert.That(Text(screen.PurchasePanel.ExchangeButton.gameObject, "Label"), Is.EqualTo("兑换"));
            Assert.That(Text(screen.gameObject, "Value", "AfterLine"), Is.EqualTo(ShopCopy.Coins(before - halo.Price)));

            screen.PurchasePanel.ExchangeButton.onClick.Invoke();
            Assert.That(screen.PurchasePanel.IsOpen, Is.False);
            Assert.That(store.ReadCoins(), Is.EqualTo(before - halo.Price));
            CoinPurchase purchase = store.ReadPurchases().Single();
            Assert.That((purchase.Id, purchase.Item, purchase.Amount), Is.EqualTo(("shop:avatar-halo", "avatar-halo", halo.Price)));
            Assert.That(screen.IsOwned(halo), Is.True);
            Assert.That(screen.View.FindTile("avatar-halo").IsOwned, Is.True);
            Assert.That(Text(screen.gameObject, "OwnedCount"), Is.EqualTo("已拥有 1 / 4"));

            // The balance counts down to what is left.
            Assert.That(Text(screen.gameObject, "Balance"), Is.EqualTo(ShopCopy.Coins(before)));
            _clock.Update(1d);
            Assert.That(Text(screen.gameObject, "Balance"), Is.EqualTo(ShopCopy.Coins(before - halo.Price)));
            Assert.That(Rows(screen), Is.EqualTo(1));
            Assert.That(Text(screen.gameObject, "Item"), Is.EqualTo("光环"));
            Assert.That(Text(screen.gameObject, "Amount"), Is.EqualTo("−30 金币"));
            Assert.That(Text(screen.gameObject, "StatValue0"), Is.EqualTo("30"));
            Assert.That(Text(screen.gameObject, "StatValue1"), Is.EqualTo("1"));

            // Owned, the card says so and spends nothing more.
            screen.OpenItem(halo);
            Assert.That(screen.PurchasePanel.ExchangeButton.interactable, Is.False);
            Assert.That(Text(screen.PurchasePanel.ExchangeButton.gameObject, "Label"), Is.EqualTo("已拥有"));
            Assert.That(Find(screen.gameObject, "AfterLine").activeSelf, Is.False);
            Assert.That(screen.Buy(halo), Is.True);
            Assert.That(store.ReadPurchases().Count, Is.EqualTo(1), "nothing more is spent");

            // A page opened again reads the same.
            screen.SetPresented(false, false);
            screen.SetPresented(true, false);
            Assert.That(screen.IsOwned(halo), Is.True);
            Assert.That(Text(screen.gameObject, "Balance"), Is.EqualTo(ShopCopy.Coins(before - halo.Price)));
        }

        [Test]
        public void WhatTheBalanceDoesNotCoverCannotBeExchanged()
        {
            PlayerProfileStore store = WithCoins();
            long balance = store.ReadCoins();
            var catalog = new ShopCatalog(new[] { Avatar("avatar-halo", (int)balance + 25) });
            ShopScreen screen = Open(store, catalog);
            ShopItem halo = catalog.Find("avatar-halo");
            Assert.That(Text(screen.gameObject, "Price"), Is.EqualTo(ShopCopy.Price(halo.Price)));

            screen.OpenItem(halo);
            Assert.That(screen.PurchasePanel.ExchangeButton.interactable, Is.False);
            Assert.That(Text(screen.PurchasePanel.ExchangeButton.gameObject, "Label"), Is.EqualTo("金币不足"));
            Assert.That(Text(screen.gameObject, "Note"), Is.EqualTo("还差 25 金币"));
            Assert.That(Text(screen.gameObject, "Value", "AfterLine"), Is.EqualTo("−25"));

            Assert.That(screen.Buy(halo), Is.False);
            Assert.That(store.ReadPurchases(), Is.Empty);
            Assert.That(store.ReadCoins(), Is.EqualTo(balance));
        }

        [Test]
        public void TheWalletListsEveryPurchaseAndShowsABalanceBelowZero()
        {
            PlayerProfileStore store = WithCoins();
            store.SpendCoins("receipt", "节拍头像", 40);
            store.DeleteHistory(store.ReadHistory(0, 1).Records.Single());
            Assert.That(store.ReadCoins(), Is.EqualTo(-40), "the play's coins were taken back after they were spent");

            ShopScreen screen = Open(store, Sample());
            Assert.That(Text(screen.gameObject, "Balance"), Is.EqualTo("−40"));
            Assert.That(Find(screen.gameObject, "Negative").activeSelf, Is.True);
            Assert.That(Rows(screen), Is.EqualTo(1));
            // Not on sale, the purchase keeps the text it was recorded with.
            Assert.That(Text(screen.gameObject, "Item"), Is.EqualTo("节拍头像"));
            Assert.That(Text(screen.gameObject, "Amount"), Is.EqualTo("−40 金币"));
            Assert.That(Find(screen.gameObject, "Plain").activeSelf, Is.True);
            Assert.That(Find(screen.gameObject, "Empty").activeSelf, Is.False);
        }

        [Test]
        public void TheHomeCardsGoodsPassBehindAndInFrontOfTheShop()
        {
            RectTransform root = LibraryUi.CreateRect("Art", _canvas.transform);
            var art = new HomeShopArt(root);
            try
            {
                Transform behind = Find(root.gameObject, "Behind").transform;
                Transform ahead = Find(root.gameObject, "Ahead").transform;
                Transform avatar = Find(root.gameObject, "Avatar").transform;
                var parents = new HashSet<Transform>();
                for (float time = 0f; time < 18f; time += 0.5f)
                {
                    art.Tick(time);
                    parents.Add(avatar.parent);
                    Assert.That(avatar.localScale.x, Is.InRange(0.69f, 1.03f));
                }

                Assert.That(parents, Is.EquivalentTo(new[] { behind, ahead }), "an orbit goes round the shop");
                Assert.That(Find(root.gameObject, "Store").transform.GetSiblingIndex(), Is.EqualTo(behind.GetSiblingIndex() + 1));
                Assert.That(Find(root.gameObject, "SignCoin").transform.IsChildOf(Find(root.gameObject, "Sign").transform), Is.True,
                    "the coin hangs on the shop's sign");
            }
            finally
            {
                art.Dispose();
            }
        }

        // ---------------------------------------------------------------- helpers

        private ShopScreen Open(PlayerProfileStore store, ShopCatalog catalog = null)
        {
            ShopScreen screen = ShopScreen.Create();
            _screens.Add(screen);
            screen.Store = store;
            if (catalog != null) screen.Catalog = catalog;
            screen.SetPresented(true, false);
            return screen;
        }

        /// <summary>A profile with one Ranked FULL COMBO on record, and the coins it gave.</summary>
        private PlayerProfileStore WithCoins()
        {
            var store = new PlayerProfileStore(Directory.CreateDirectory(Path.Combine(_root, "profile")).FullName);
            store.History.Submit(new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 3, Ranked = true, RecordId = "fc-1", ScoreKey = "chart-1", PlayedAtUtc = _epoch.ToString("O"),
                EndReason = PlayResultKind.Completed, Life = 100, PlayLevel = 28, MaximumScore = 1000000, FormulaVersion = RankingRules.FORMULA_VERSION,
                TotalScore = 1000000, TotalComboCount = 10, MaxCombo = 10, PerfectCount = 10, IsFullCombo = true
            }, null, out _);
            Assert.That(store.ReadCoins(), Is.GreaterThan(30), "the sample play pays for the cheapest item");
            return store;
        }

        private static ShopCatalog Sample() => new ShopCatalog(new[]
        {
            Avatar("avatar-halo", 30, "光环"),
            new ShopItem("theme-dusk", ShopCategory.Theme, "黄昏", "暖色的界面", 60, HomeTheme.Colors.Rose,
                palette: new Color32[] { HomeTheme.Colors.Rose, HomeTheme.Colors.Gold, HomeTheme.Colors.Violet }),
            Avatar("avatar-wave", 45, "声波"),
            new ShopItem("background-aurora", ShopCategory.Background, "极光", "轨道后的极光", 80, HomeTheme.Colors.Periwinkle)
        });

        private static ShopItem Avatar(string id, int price, string title = null) =>
            new ShopItem(id, ShopCategory.Avatar, title ?? id, null, price, HomeTheme.Colors.Violet, avatarId: "halo");

        private static int Tiles(ShopScreen screen) =>
            screen.GetComponentsInChildren<RectTransform>(false).Count(rect => _tileName.IsMatch(rect.name));

        private static int Rows(ShopScreen screen) =>
            screen.GetComponentsInChildren<RectTransform>(false).Count(rect => _rowName.IsMatch(rect.name));

        private static string[] Shelves(ShopScreen screen) =>
            screen.GetComponentsInChildren<RectTransform>(false).Where(rect => rect.name.StartsWith("Shelf") && rect.name != "Shelves" &&
                rect.name != "ShelfTitle").Select(rect => rect.name).ToArray();

        private static GameObject Find(GameObject root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name).gameObject;

        private static string Text(GameObject root, string name) =>
            root.GetComponentsInChildren<TextMeshProUGUI>(true).Single(label => label.name == name).text;

        /// <summary>The label <paramref name="name"/> under the object <paramref name="parent"/>.</summary>
        private static string Text(GameObject root, string name, string parent) =>
            Find(root, parent).GetComponentsInChildren<TextMeshProUGUI>(true).Single(label => label.name == name).text;
    }
}
