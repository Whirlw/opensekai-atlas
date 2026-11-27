using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Layout and binding of the 商店 page. On the left, the wallet: the turning coin and the
    /// balance, what has been spent and how many things were bought, and every purchase, latest
    /// first. On the right, the tabs by kind and the goods as a grid of tiles (<see cref="ShopTile"/>);
    /// while a kind has nothing on sale, its shelf says what it will offer. Sizes are canvas units at
    /// the 1920×1080 reference.
    /// </summary>
    internal sealed class ShopView : IDisposable
    {
        private const float LEFT_WIDTH = 560f;
        private const float COLUMN_GAP = 28f;
        private const float CARD_RADIUS = 30f;
        private const float PAD = 32f;
        private const float COIN = 132f;
        private const float HERO_TOP = 84f;
        private const float HERO_HEIGHT = 156f;
        private const float STAT_TOP = HERO_TOP + HERO_HEIGHT + 24f;
        private const float STAT_HEIGHT = 96f;
        private const float HISTORY_TOP = STAT_TOP + STAT_HEIGHT + 30f;
        private const float GRID_TOP = 108f;
        private const float GRID_SIDE = 14f;
        private const float TAB_WIDTH = 150f;
        private const float TAB_HEIGHT = 60f;
        private const float SHELF_TOP = 56f;
        private const float SHELF_HEIGHT = 400f;
        private const float SHELF_WIDTH = 440f;
        private const float ENTER = 0.9f;
        private const float GOODS_STAGGER = 0.04f;
        private const float GOODS_FADE = 0.28f;
        private const float COUNT = 0.7f;

        private readonly ShopScreen _screen;
        private readonly RectTransform _root;
        private readonly CanvasGroup _leftGroup;
        private readonly RectTransform _leftCard;
        private readonly CanvasGroup _rightGroup;
        private readonly RectTransform _rightCard;

        private readonly ShopCoin _coin;
        private readonly TextMeshProUGUI _balance;
        private readonly TextMeshProUGUI _negative;
        private readonly Stat[] _stats = new Stat[2];
        private readonly RectTransform _historyContent;
        private readonly ScrollRect _historyScroll;
        private readonly List<PurchaseRow> _historyRows = new List<PurchaseRow>();
        private readonly TextMeshProUGUI _historyEmpty;

        private readonly HomePageTabs _tabs;
        private readonly TextMeshProUGUI _ownedCount;
        private readonly RectTransform _gridContent;
        private readonly ScrollRect _gridScroll;
        private readonly List<ShopTile> _tiles = new List<ShopTile>();
        private readonly RectTransform _shelves;
        private readonly ShopShelf[] _shelfCards = new ShopShelf[3];
        private readonly TextMeshProUGUI _shelfTitle;
        private readonly TextMeshProUGUI _goodsError;

        private long _target;
        private long _shownCount = long.MinValue;
        private int _tilesInUse;
        private float _extraTurn;
        private MotionHandle _enter;
        private MotionHandle _goodsMotion;
        private MotionHandle _countMotion;
        private MotionHandle _coinMotion;
        private MotionHandle _spinMotion;

        internal ShopView(RectTransform parent, ShopScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("ShopPage", parent);
            LibraryUi.Stretch(_root);

            ServerWidgets.TopBar(_root, ShopCopy.TITLE, screen.Back, screen.OpenSettings, out _, out TextMeshProUGUI subtitle, out _);
            subtitle.text = ShopCopy.SUBTITLE;

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", _root);
            LibraryWidgets.Inset(body, margin, 48f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            Vector2 topLeft = new Vector2(0f, 1f);

            // ---------------------------------------------------------------- the wallet
            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _leftGroup = left.gameObject.AddComponent<CanvasGroup>();
            _leftCard = left;
            RectTransform wallet = Card(left, "WalletCard");
            TextMeshProUGUI walletTitle = LibraryWidgets.Label("Title", wallet, ShopCopy.WALLET, 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(walletTitle.rectTransform, PAD, PAD, 26f, 40f);

            _coin = new ShopCoin(wallet, "WalletCoin", COIN);
            LibraryWidgets.Place(_coin.Root, topLeft, new Vector2(0.5f, 0.5f), new Vector2(PAD + 8f + COIN * 0.5f, -(HERO_TOP + HERO_HEIGHT * 0.5f)),
                new Vector2(COIN, COIN));
            float textLeft = PAD + 8f + COIN + 36f;
            TextMeshProUGUI balanceCaption = LibraryWidgets.Label("BalanceCaption", wallet, ShopCopy.BALANCE, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(balanceCaption.rectTransform, textLeft, PAD, HERO_TOP + 18f, 26f);
            _balance = LibraryWidgets.Label("Balance", wallet, "0", 88f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(_balance);
            _balance.overflowMode = TextOverflowModes.Overflow;
            _balance.alignment = TextAlignmentOptions.Left;
            LibraryWidgets.TopStrip(_balance.rectTransform, textLeft, PAD, HERO_TOP + 44f, 92f);
            _negative = LibraryWidgets.Label("Negative", wallet, ShopCopy.NEGATIVE, 16f, LibraryTheme.Colors.Danger);
            _negative.textWrappingMode = TextWrappingModes.Normal;
            LibraryWidgets.TopStrip(_negative.rectTransform, textLeft, PAD, HERO_TOP + 136f, 44f);

            float statWidth = (LEFT_WIDTH - PAD * 2f - 16f) * 0.5f;
            for (int i = 0; i < _stats.Length; i++)
            {
                _stats[i] = new Stat(wallet, i);
                LibraryWidgets.Place(_stats[i].Root, topLeft, topLeft, new Vector2(PAD + i * (statWidth + 16f), -STAT_TOP), new Vector2(statWidth, STAT_HEIGHT));
            }

            DanWidgets.Caption(wallet, ShopCopy.HISTORY, PAD + 2f, HISTORY_TOP);
            RectTransform historyArea = LibraryUi.CreateRect("HistoryArea", wallet);
            LibraryWidgets.Inset(historyArea, 16f, 16f, 16f, HISTORY_TOP + 38f);
            _historyContent = PackWidgets.Scroll(historyArea, out _historyScroll);
            _historyEmpty = PackWidgets.Paragraph(historyArea, "Empty", string.Empty, 19f, LibraryTheme.Colors.TextMuted);
            _historyEmpty.alignment = TextAlignmentOptions.Top;
            LibraryWidgets.Inset(_historyEmpty.rectTransform, 24f, 0f, 24f, 18f);

            // ---------------------------------------------------------------- the goods
            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + COLUMN_GAP, 0f, 0f, 0f);
            _rightGroup = right.gameObject.AddComponent<CanvasGroup>();
            _rightCard = right;
            RectTransform goods = Card(right, "GoodsCard");
            var names = new string[ShopCopy.CategoryNames.Length];
            Array.Copy(ShopCopy.CategoryNames, names, names.Length);
            _tabs = new HomePageTabs(goods, names, index => screen.SetFilter((ShopCategory)index), TAB_WIDTH, TAB_HEIGHT, 20f);
            LibraryWidgets.Place(_tabs.Root, topLeft, topLeft, new Vector2(28f, -26f), _tabs.Root.sizeDelta);
            _ownedCount = LibraryWidgets.Label("OwnedCount", goods, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Right);
            LibraryWidgets.TopStrip(_ownedCount.rectTransform, 28f + _tabs.Root.sizeDelta.x + 20f, PAD, 40f, 30f);

            RectTransform gridArea = LibraryUi.CreateRect("GridArea", goods);
            LibraryWidgets.Inset(gridArea, GRID_SIDE, 16f, GRID_SIDE, GRID_TOP);
            _gridContent = PackWidgets.Scroll(gridArea, out _gridScroll);
            // As far as the card goes: a tile that pops after a purchase reaches past its cell.
            _gridScroll.viewport.GetComponent<RectMask2D>().padding = new Vector4(-14f, -16f, -14f, -12f);

            _shelves = LibraryUi.CreateRect("Shelves", gridArea);
            LibraryWidgets.Inset(_shelves, ShopTile.GAP * 0.5f, 0f, ShopTile.GAP * 0.5f, 0f);
            _shelfTitle = LibraryWidgets.Label("ShelfTitle", _shelves, string.Empty, 20f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.TopStrip(_shelfTitle.rectTransform, 0f, 0f, 10f, 30f);
            for (int i = 0; i < _shelfCards.Length; i++)
            {
                _shelfCards[i] = new ShopShelf(_shelves, (ShopCategory)(i + 1));
            }

            _goodsError = PackWidgets.Paragraph(gridArea, "Error", string.Empty, 20f, LibraryTheme.Colors.TextMuted);
            _goodsError.alignment = TextAlignmentOptions.Center;
            LibraryWidgets.Inset(_goodsError.rectTransform, 24f, 0f, 24f, 120f);
        }

        internal RectTransform Root => _root;

        /// <summary>Redraws the wallet and the goods from the screen's coins.</summary>
        internal void Bind()
        {
            BindTabs();
            BindWallet();
            _countMotion.TryCancel();
            ApplyBalance(_target);
            BindGoods(false);
        }

        /// <summary>Redraws the goods for the screen's filter; <paramref name="animate"/> fades the tiles in one after another.</summary>
        /// <param name="keepScroll">Stay where the grid was scrolled to (after a purchase); otherwise back to the top.</param>
        internal void BindGoods(bool animate, bool keepScroll = false)
        {
            _tabs.Apply((int)_screen.Filter);
            IReadOnlyList<ShopItem> items = _screen.Filtered();
            bool failed = _screen.LoadError != null;
            int shown = failed ? 0 : items.Count;
            _ownedCount.text = failed || _screen.Catalog.Items.Count == 0
                ? string.Empty
                : ShopCopy.OwnedCount(_screen.OwnedCount(_screen.Catalog.Items), _screen.Catalog.Items.Count);

            while (_tiles.Count < shown)
            {
                _tiles.Add(new ShopTile(_gridContent, _tiles.Count, _screen.OpenItem));
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                bool used = i < shown;
                _tiles[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                _tiles[i].Place(i);
                _tiles[i].Bind(items[i], _screen.IsOwned(items[i]), _screen.Balance >= items[i].Price);
            }

            _tilesInUse = shown;
            int rows = (shown + ShopTile.COLUMNS - 1) / ShopTile.COLUMNS;
            _gridContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, rows * ShopTile.STEP - ShopTile.GAP));
            if (!keepScroll)
            {
                _gridContent.anchoredPosition = Vector2.zero;
                _gridScroll.StopMovement();
            }

            _gridScroll.gameObject.SetActive(!failed && shown > 0);

            _goodsError.gameObject.SetActive(failed);
            _goodsError.text = _screen.LoadError ?? string.Empty;
            BindShelves(!failed && shown == 0);

            _goodsMotion.TryCancel();
            int count = shown > 0 ? shown : ShelvesShown();
            float length = GOODS_FADE + count * GOODS_STAGGER;
            if (!animate)
            {
                ApplyGoods(length);
                return;
            }

            ApplyGoods(0f);
            _goodsMotion = LMotion.Create(0f, length, length)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (time, view) => view.ApplyGoods(time));
        }

        /// <summary>The tile that shows <paramref name="id"/>; null when the filter hides it.</summary>
        internal ShopTile FindTile(string id)
        {
            for (int i = 0; i < _tilesInUse; i++)
            {
                if (_tiles[i].Item != null && string.Equals(_tiles[i].Item.Id, id, StringComparison.Ordinal)) return _tiles[i];
            }

            return null;
        }

        /// <summary>After a purchase: the coin turns, the balance counts down from <paramref name="before"/> and the tile pops.</summary>
        internal void Bought(ShopItem item, long before)
        {
            BindTabs();
            BindWallet();
            BindGoods(false, keepScroll: true);
            _countMotion.TryCancel();
            ApplyBalance(before);
            _countMotion = LMotion.Create((float)before, _target, COUNT)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (value, view) => view.ApplyBalance(value));
            Spin();
            FindTile(item.Id)?.Pop();
        }

        internal void PlayEnter()
        {
            _enter.TryCancel();
            ApplyEnter(0f);
            _enter = LMotion.Create(0f, ENTER, ENTER)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnCancel(() => ApplyEnter(ENTER))
                .Bind(this, static (time, view) => view.ApplyEnter(time));
            BindGoods(true);
            StartCoin();
        }

        /// <summary>Shows the page where its opening ends.</summary>
        internal void SkipEnter()
        {
            _enter.TryCancel();
            _enter = default;
            ApplyEnter(ENTER);
            StartCoin();
        }

        /// <summary>Drops every motion as the page goes away; it is bound again when it comes back.</summary>
        internal void Stop()
        {
            _enter.TryCancel();
            _goodsMotion.TryCancel();
            _countMotion.TryCancel();
            _coinMotion.TryCancel();
            _spinMotion.TryCancel();
            _extraTurn = 0f;
            foreach (ShopTile tile in _tiles) tile.Stop();
        }

        public void Dispose() => Stop();

        /// <summary>Stops the coin <paramref name="turn"/> turns round, for screenshots; entering again sets it going.</summary>
        internal void HoldCoin(float turn)
        {
            _coinMotion.TryCancel();
            _coin.Apply(turn, -1f);
        }

        // ---------------------------------------------------------------- motion

        private void StartCoin()
        {
            _coinMotion.TryCancel();
            _coinMotion = LMotion.Create(0f, ShopCoin.BEAT, ShopCoin.BEAT)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (time, view) => view.ApplyCoin(time));
        }

        /// <summary>Two quick turns on top of the coin's own rhythm.</summary>
        private void Spin()
        {
            _spinMotion.TryCancel();
            _spinMotion = LMotion.Create(0f, 2f, 0.9f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _extraTurn = 0f)
                .Bind(this, static (turn, view) => view._extraTurn = turn);
        }

        private void ApplyCoin(float time)
        {
            ShopCoin.Idle(time, out float turn, out float sweep);
            _coin.Apply(turn + _extraTurn, sweep);
            _coin.SetGlow(0.22f + 0.06f * Mathf.Sin(time / ShopCoin.BEAT * Mathf.PI * 2f));
        }

        private void ApplyEnter(float time)
        {
            Rise(_leftCard, _leftGroup, HomeMotion.Span(time, 0.02f, 0.45f, Ease.OutCubic));
            Rise(_rightCard, _rightGroup, HomeMotion.Span(time, 0.1f, 0.45f, Ease.OutCubic));
            // A balance below zero is shown as it is: counting up to it from zero would read as a loss.
            ApplyBalance(_target <= 0 ? _target : _target * HomeMotion.Span(time, 0.18f, 0.7f, Ease.OutCubic));
        }

        private void ApplyBalance(float value)
        {
            long shown = (long)Math.Round(value);
            if (shown == _shownCount) return;
            _shownCount = shown;
            _balance.text = _screen.LoadError != null ? "—" : ShopCopy.Coins(shown);
            _balance.color = shown < 0 ? LibraryTheme.Colors.Danger : LibraryTheme.Colors.TextPrimary;
        }

        private void ApplyGoods(float time)
        {
            for (int i = 0; i < _tilesInUse; i++)
            {
                _tiles[i].Apply(HomeMotion.Span(time, i * GOODS_STAGGER, GOODS_FADE, Ease.OutCubic));
            }

            int shelf = 0;
            foreach (ShopShelf card in _shelfCards)
            {
                if (!card.Root.gameObject.activeSelf) continue;
                card.Apply(HomeMotion.Span(time, shelf * GOODS_STAGGER * 2f, GOODS_FADE, Ease.OutCubic));
                shelf++;
            }
        }

        private static void Rise(RectTransform rect, CanvasGroup group, float progress)
        {
            group.alpha = progress;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -36f * (1f - progress));
        }

        // ---------------------------------------------------------------- binding

        private void BindTabs()
        {
            for (int i = 0; i < ShopCopy.CategoryNames.Length; i++)
            {
                _tabs.SetLabel(i, ShopCopy.CategoryNames[i] + "  " + _screen.Catalog.Count((ShopCategory)i).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The balance's target, the totals and the purchases; the number itself follows in <see cref="ApplyBalance"/>.</summary>
        private void BindWallet()
        {
            bool failed = _screen.LoadError != null;
            _target = _screen.Balance;
            _shownCount = long.MinValue;
            _negative.gameObject.SetActive(!failed && _target < 0);
            int bought = 0;
            var counted = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoinPurchase purchase in _screen.Purchases)
            {
                if (counted.Add(purchase.Item)) bought++;
            }

            _stats[0].Bind(failed ? "—" : ProfileCopy.Number(_screen.Spent), ShopCopy.SPENT, HomeTheme.Colors.Gold);
            _stats[1].Bind(failed ? "—" : bought.ToString(CultureInfo.InvariantCulture), "已兑换", HomeTheme.Colors.Violet);
            BindHistory(failed);
        }

        private void BindHistory(bool failed)
        {
            IReadOnlyList<CoinPurchase> purchases = _screen.Purchases;
            while (_historyRows.Count < purchases.Count)
            {
                var row = new PurchaseRow(_historyContent, _historyRows.Count);
                row.Root.anchoredPosition = new Vector2(0f, -_historyRows.Count * PurchaseRow.STEP);
                _historyRows.Add(row);
            }

            for (int i = 0; i < _historyRows.Count; i++)
            {
                bool used = i < purchases.Count;
                _historyRows[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                string title = _screen.Describe(purchases[i], out ShopItem item);
                _historyRows[i].Bind(purchases[i], title, item);
            }

            _historyContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, purchases.Count * PurchaseRow.STEP - PurchaseRow.GAP));
            _historyContent.anchoredPosition = Vector2.zero;
            _historyScroll.StopMovement();
            _historyEmpty.gameObject.SetActive(failed || purchases.Count == 0);
            _historyEmpty.text = _screen.LoadError ?? ShopCopy.HISTORY_EMPTY;
        }

        /// <summary>
        /// With nothing on sale under the tab: every kind's shelf under 全部, or the one kind's in the
        /// middle, each saying what is coming.
        /// </summary>
        private void BindShelves(bool shown)
        {
            _shelves.gameObject.SetActive(shown);
            if (!shown) return;
            ShopCategory filter = _screen.Filter;
            _shelfTitle.text = ShopCopy.Empty(filter);
            for (int i = 0; i < _shelfCards.Length; i++)
            {
                ShopShelf shelf = _shelfCards[i];
                bool all = filter == ShopCategory.All;
                bool used = all ? _screen.Catalog.Count(shelf.Category) == 0 : shelf.Category == filter;
                shelf.Root.gameObject.SetActive(used);
            }

            // The shelves left side by side, centered together.
            int count = ShelvesShown();
            int slot = 0;
            for (int i = 0; i < _shelfCards.Length; i++)
            {
                ShopShelf shelf = _shelfCards[i];
                if (!shelf.Root.gameObject.activeSelf) continue;
                if (filter == ShopCategory.All && count == _shelfCards.Length)
                {
                    shelf.Root.anchorMin = new Vector2(slot / 3f, 1f);
                    shelf.Root.anchorMax = new Vector2((slot + 1) / 3f, 1f);
                    shelf.Root.pivot = new Vector2(0.5f, 1f);
                    shelf.Root.offsetMin = new Vector2(ShopTile.GAP * 0.5f, -SHELF_TOP - SHELF_HEIGHT);
                    shelf.Root.offsetMax = new Vector2(-ShopTile.GAP * 0.5f, -SHELF_TOP);
                }
                else
                {
                    float x = (slot - (count - 1) * 0.5f) * (SHELF_WIDTH + ShopTile.GAP);
                    LibraryWidgets.Place(shelf.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -SHELF_TOP),
                        new Vector2(SHELF_WIDTH, SHELF_HEIGHT));
                }

                slot++;
            }
        }

        private int ShelvesShown()
        {
            if (!_shelves.gameObject.activeSelf) return 0;
            int count = 0;
            foreach (ShopShelf shelf in _shelfCards)
            {
                if (shelf.Root.gameObject.activeSelf) count++;
            }

            return count;
        }

        /// <summary>The page's own card: a raised surface with the home cards' light from above.</summary>
        private static RectTransform Card(RectTransform parent, string name)
        {
            RectTransform root = LibraryUi.CreateRect(name, parent);
            LibraryUi.Stretch(root);
            LibraryWidgets.Shadow(root, CARD_RADIUS, LibraryTheme.CoverShadow.BLUR, LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image surface = LibraryWidgets.Panel("Surface", root, CARD_RADIUS, HomeTheme.Colors.Card);
            LibraryUi.Stretch(surface.rectTransform);
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image highlight = LibraryWidgets.Shape("Highlight", surface.transform, HomeShapes.FadeUp(), HomeTheme.Colors.CardHighlight);
            highlight.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(highlight.rectTransform, 0f, 0f, 0f, 140f);
            RectTransform content = LibraryUi.CreateRect("Content", surface.transform);
            LibraryUi.Stretch(content);
            Image outline = LibraryWidgets.Shape("Outline", root, LibraryShapes.Ring(CARD_RADIUS, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
            return content;
        }

        /// <summary>A running total under the balance: a big number with what it counts.</summary>
        private sealed class Stat
        {
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _caption;
            private readonly Image _accent;

            internal Stat(RectTransform parent, int index)
            {
                Image tile = LibraryWidgets.Panel("Stat" + index, parent, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                Root = tile.rectTransform;
                _accent = LibraryWidgets.Panel("Accent", Root, 3f, Color.white);
                LibraryWidgets.Place(_accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(5f, STAT_HEIGHT - 40f));
                _value = LibraryWidgets.Label("StatValue" + index, Root, "0", 44f, LibraryTheme.Colors.TextPrimary);
                LibraryUi.UseCondensedFont(_value);
                _value.overflowMode = TextOverflowModes.Overflow;
                _value.alignment = TextAlignmentOptions.BottomLeft;
                LibraryWidgets.TopStrip(_value.rectTransform, 34f, 12f, 4f, 54f);
                _caption = LibraryWidgets.Label("StatCaption" + index, Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.BottomStrip(_caption.rectTransform, 36f, 12f, 12f, 24f);
            }

            internal RectTransform Root { get; }

            internal void Bind(string value, string caption, Color32 tint)
            {
                _value.text = value;
                _caption.text = caption;
                _accent.color = tint;
            }
        }

        /// <summary>One purchase in the wallet: what it bought (its picture while it is on sale), when, and what it cost.</summary>
        private sealed class PurchaseRow
        {
            internal const float HEIGHT = 84f;
            internal const float GAP = 8f;
            internal const float STEP = HEIGHT + GAP;
            private const float GLYPH = 56f;

            private readonly RectTransform _glyph;
            private readonly ShopPreview _preview;
            private readonly Image _plain;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _time;
            private readonly TextMeshProUGUI _amount;

            internal PurchaseRow(RectTransform content, int index)
            {
                Root = LibraryUi.CreateRect("Purchase" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, HEIGHT);
                Image background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(background.rectTransform);

                _glyph = LibraryUi.CreateRect("Glyph", Root);
                LibraryWidgets.Place(_glyph, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(GLYPH, GLYPH));
                _preview = new ShopPreview(_glyph, "Preview", GLYPH, 14f);
                // Anything no longer on sale shows the coin it cost.
                _plain = LibraryWidgets.Panel("Plain", _glyph, 14f, Color.Lerp(HomeTheme.Colors.Card, HomeTheme.Colors.Gold, 0.16f));
                LibraryUi.Stretch(_plain.rectTransform);
                LibraryWidgets.CoinIcon(_plain.transform, 34f);

                _title = LibraryWidgets.Label("Item", Root, string.Empty, 21f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_title.rectTransform, 88f, 150f, 14f, 30f);
                _time = LibraryWidgets.Label("Time", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(_time.rectTransform, 88f, 150f, 46f, 24f);
                _amount = LibraryWidgets.Label("Amount", Root, string.Empty, 20f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold,
                    TextAlignmentOptions.Right);
                LibraryWidgets.Place(_amount.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(140f, 30f));
            }

            internal RectTransform Root { get; }

            internal void Bind(CoinPurchase purchase, string title, ShopItem item)
            {
                _preview.Root.gameObject.SetActive(item != null);
                _plain.gameObject.SetActive(item == null);
                if (item != null) _preview.Bind(item);
                _title.text = title;
                string when = HomeScreen.DescribeWhen(purchase.Time);
                _time.text = item != null ? ShopCopy.Name(item.Category) + "  ·  " + when : when;
                _amount.text = ShopCopy.Spent(purchase.Amount);
            }
        }
    }
}
