using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    internal sealed class HomeShop : IDisposable
    {
        private readonly LibraryModal _modal;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private const float ROW_HEIGHT = 66f;
        private readonly List<(RectTransform Root, TextMeshProUGUI Item, TextMeshProUGUI Time, TextMeshProUGUI Amount)> _rows = new();
        private readonly TextMeshProUGUI _balance;
        private readonly TextMeshProUGUI _empty;

        internal HomeShop(RectTransform parent)
        {
            var root = LibraryUi.CreateRect("Shop", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(880f, 580f), "商店", Close);
            var body = _modal.Body;
            var wallet = LibraryWidgets.Panel("Wallet", body, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryWidgets.TopStrip(wallet.rectTransform, 0f, 0f, 0f, 120f);
            var coin = LibraryWidgets.CoinIcon(wallet.rectTransform, 64f);
            LibraryWidgets.Place(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), coin.sizeDelta);
            var label = LibraryWidgets.Label("Currency", wallet.transform, "金币余额", 20f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(label.rectTransform, 110f, 22f, 16f, 28f);
            _balance = LibraryWidgets.Label("Balance", wallet.transform, string.Empty, 42f, HomeTheme.Colors.Gold, FontStyles.Bold);
            LibraryWidgets.TopStrip(_balance.rectTransform, 110f, 22f, 46f, 58f);
            var stock = LibraryWidgets.Label("Stock", body, "暂无商品", 22f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(stock.rectTransform, 0f, 0f, 132f, 44f);
            var title = LibraryWidgets.Label("PurchasesTitle", body, "消费记录", 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(title.rectTransform, 0f, 0f, 198f, 40f);
            var list = LibraryUi.CreateRect("Purchases", body);
            LibraryWidgets.Inset(list, 0f, 0f, 0f, 250f);
            _content = PackWidgets.Scroll(list, out _scroll);
            _empty = LibraryWidgets.Label("Empty", list, "暂无消费记录", 21f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(_empty.rectTransform, 0f, 0f, 0f, 44f);
            Close();
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(PlayerProfileStore store)
        {
            _balance.text = ProfileCopy.Number(store.ReadCoins());
            var purchases = store.ReadPurchases();
            _empty.gameObject.SetActive(purchases.Count == 0);
            for (int i = 0; i < purchases.Count; i++)
            {
                if (i == _rows.Count)
                {
                    var root = LibraryUi.CreateRect("Purchase", _content);
                    LibraryWidgets.TopStrip(root, 0f, 0f, i * ROW_HEIGHT, ROW_HEIGHT);
                    var item = LibraryWidgets.Label("Item", root, string.Empty, 23f, LibraryTheme.Colors.TextPrimary);
                    item.richText = false;
                    LibraryWidgets.TopStrip(item.rectTransform, 0f, 190f, 0f, 32f);
                    var time = LibraryWidgets.Label("Time", root, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
                    LibraryWidgets.TopStrip(time.rectTransform, 0f, 190f, 32f, 24f);
                    var amount = LibraryWidgets.Label("Amount", root, string.Empty, 22f, LibraryTheme.Colors.TextPrimary,
                        FontStyles.Normal, TextAlignmentOptions.Right);
                    LibraryWidgets.TopStrip(amount.rectTransform, 0f, 0f, 0f, 32f);
                    _rows.Add((root, item, time, amount));
                }
                var purchase = purchases[i];
                var row = _rows[i];
                row.Root.gameObject.SetActive(true);
                row.Item.text = purchase.Item;
                row.Time.text = purchase.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                row.Amount.text = "−" + ProfileCopy.Number(purchase.Amount) + " 金币";
            }
            for (int i = purchases.Count; i < _rows.Count; i++) _rows[i].Root.gameObject.SetActive(false);
            _content.sizeDelta = new Vector2(0f, purchases.Count * ROW_HEIGHT);
            _modal.Open();
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        internal void Close() => _modal.Close();
        public void Dispose() => _modal.Dispose();
    }
}
