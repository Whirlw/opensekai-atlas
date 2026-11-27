using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 兑换: one item large, what it is, and the coins: its price, the balance and what is left after
    /// it. 兑换 spends them; when the balance falls short or the item is already owned, the button
    /// says so instead and cannot be pressed.
    /// </summary>
    internal sealed class ShopPurchasePanel : IDisposable
    {
        private const float WIDTH = 980f;
        private const float HEIGHT = 620f;
        private const float PICTURE = 360f;
        private const float COLUMN_GAP = 40f;
        private const float LINE_HEIGHT = 60f;
        private const float BUTTON_WIDTH = 260f;
        private const float CANCEL_WIDTH = 200f;

        private readonly ShopScreen _screen;
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly ShopPreview _preview;
        private readonly TextMeshProUGUI _description;
        private readonly Line _price;
        private readonly Line _balance;
        private readonly Line _after;
        private readonly RectTransform _ledger;
        private readonly RectTransform _priceCoin;
        private readonly TextMeshProUGUI _note;
        private readonly Button _exchange;
        private readonly Button _cancel;
        private ShopItem _item;

        internal ShopPurchasePanel(RectTransform layer, ShopScreen screen)
        {
            _screen = screen;
            _root = LibraryUi.CreateRect("ShopPurchase", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(WIDTH, HEIGHT), string.Empty, Close, withSubtitle: true);
            RectTransform body = _modal.Body;
            Vector2 topLeft = new Vector2(0f, 1f);

            RectTransform picture = LibraryUi.CreateRect("Picture", body);
            LibraryWidgets.Place(picture, topLeft, topLeft, Vector2.zero, new Vector2(PICTURE, PICTURE));
            _preview = new ShopPreview(picture, "Preview", PICTURE, LibraryTheme.Radius.PANEL);
            Image frame = LibraryWidgets.Shape("Outline", picture, LibraryShapes.Ring(LibraryTheme.Radius.PANEL, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(frame.rectTransform);

            float left = PICTURE + COLUMN_GAP;
            _description = PackWidgets.Paragraph(body, "Description", string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(_description.rectTransform, left, 0f, 0f, PICTURE - LINE_HEIGHT * 3f - 20f);

            _ledger = LibraryWidgets.Panel("Ledger", body, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control).rectTransform;
            PlaceLedger(3);
            _price = new Line(_ledger, "PriceLine", "价格", 0);
            _balance = new Line(_ledger, "BalanceLine", ShopCopy.BALANCE, 1);
            _after = new Line(_ledger, "AfterLine", "兑换后", 2);
            _priceCoin = LibraryWidgets.CoinIcon(_price.Root, 26f);

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _note = LibraryWidgets.Label("Note", body, string.Empty, 19f, LibraryTheme.Colors.TextMuted);
            _note.richText = false;
            // Under the picture, level with the buttons.
            LibraryWidgets.BottomStrip(_note.rectTransform, 4f, BUTTON_WIDTH + CANCEL_WIDTH + 40f, 0f, height);
            _exchange = ServerWidgets.LabelledButton(body, "Exchange", ShopCopy.EXCHANGE, LibraryIcon.Check, OnExchange, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_exchange.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BUTTON_WIDTH, height));
            _cancel = LibraryWidgets.PillButton("Cancel", body, "取消", Close, CANCEL_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)_cancel.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)_cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-(BUTTON_WIDTH + 16f), 0f), new Vector2(CANCEL_WIDTH, height));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>The item on show; null while closed.</summary>
        internal ShopItem Item => IsOpen ? _item : null;

        internal Button ExchangeButton => _exchange;

        internal void Open(ShopItem item)
        {
            Bind(item);
            _modal.Open();
        }

        /// <summary>Redraws the card for <paramref name="item"/> as the wallet stands now.</summary>
        internal void Bind(ShopItem item)
        {
            _item = item;
            bool owned = _screen.IsOwned(item);
            long balance = _screen.Balance;
            long after = balance - item.Price;
            bool affordable = after >= 0;
            _modal.Title.text = item.Title;
            _modal.Subtitle.text = ShopCopy.Name(item.Category) + "  ·  " + ShopCopy.Usage(item.Category);
            _preview.Bind(item);
            _description.text = string.IsNullOrEmpty(item.Description) ? ShopCopy.Pitch(item.Category) : item.Description;

            string price = ShopCopy.Price(item.Price);
            _price.Bind(price, HomeTheme.Colors.Gold);
            float priceWidth = Mathf.Ceil(_price.Value.GetPreferredValues(price).x);
            LibraryWidgets.Place(_priceCoin, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f - priceWidth - 8f - 13f, 0f),
                new Vector2(26f, 26f));
            _balance.Root.gameObject.SetActive(!owned);
            _after.Root.gameObject.SetActive(!owned);
            _balance.Bind(ShopCopy.Coins(balance), balance < 0 ? LibraryTheme.Colors.Danger : LibraryTheme.Colors.TextPrimary);
            _after.Bind(ShopCopy.Coins(after), affordable ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.Danger);
            // Owned, only the price is left to say.
            PlaceLedger(owned ? 1 : 3);
            _price.SetDivider(!owned);

            BindNote(owned, affordable, after);
            if (owned)
            {
                ServerWidgets.SetLabelledButton(_exchange, ShopCopy.OWNED, LibraryIcon.Check, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary);
                _exchange.interactable = false;
            }
            else if (!affordable)
            {
                ServerWidgets.SetLabelledButton(_exchange, ShopCopy.SHORT, null, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary);
                _exchange.interactable = false;
            }
            else
            {
                ServerWidgets.SetLabelledButton(_exchange, ShopCopy.EXCHANGE, LibraryIcon.Check, LibraryTheme.Colors.TextPrimary,
                    LibraryTheme.Colors.TextOnAccent);
                _exchange.interactable = true;
            }

            _cancel.GetComponentInChildren<TextMeshProUGUI>().text = owned ? "关闭" : "取消";
        }

        internal void Close() => _modal.Close();

        public void Dispose() => _modal.Dispose();

        /// <summary>The ledger ends level with the picture, so the description has the space above it.</summary>
        private void PlaceLedger(int lines) =>
            LibraryWidgets.TopStrip(_ledger, PICTURE + COLUMN_GAP, 0f, PICTURE - LINE_HEIGHT * lines, LINE_HEIGHT * lines);

        /// <summary>Beside the buttons: when an owned item was bought, or how many coins are missing.</summary>
        private void BindNote(bool owned, bool affordable, long after)
        {
            if (owned)
            {
                var purchase = _screen.PurchaseOf(_item);
                _note.text = purchase != null ? ShopCopy.BoughtWhen(purchase.Time) : string.Empty;
                _note.color = LibraryTheme.Colors.TextMuted;
                return;
            }

            _note.text = affordable ? string.Empty : ShopCopy.Short(-after);
            _note.color = LibraryTheme.Colors.Danger;
        }

        private void OnExchange()
        {
            if (_item != null) _screen.Exchange(_item);
        }

        /// <summary>One line of the ledger: what it is on the left, the coins on the right.</summary>
        private sealed class Line
        {
            private readonly Image _divider;

            internal Line(RectTransform parent, string name, string caption, int index)
            {
                Root = LibraryUi.CreateRect(name, parent);
                LibraryWidgets.TopStrip(Root, 0f, 0f, index * LINE_HEIGHT, LINE_HEIGHT);
                TextMeshProUGUI label = LibraryWidgets.Label("Caption", Root, caption, 20f, LibraryTheme.Colors.TextSecondary);
                LibraryWidgets.Inset(label.rectTransform, 24f, 0f, 200f, 0f);
                Value = LibraryWidgets.Label("Value", Root, string.Empty, 32f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.Right);
                LibraryUi.UseCondensedFont(Value);
                Value.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Inset(Value.rectTransform, 200f, 0f, 24f, 0f);
                _divider = LibraryUi.CreateImage("Divider", Root, LibraryTheme.Colors.Hairline);
                _divider.raycastTarget = false;
                LibraryWidgets.BottomStrip(_divider.rectTransform, 20f, 20f, 0f, 1f);
                _divider.enabled = index < 2;
            }

            internal RectTransform Root { get; }

            internal TextMeshProUGUI Value { get; }

            internal void Bind(string value, Color32 color)
            {
                Value.text = value;
                Value.color = color;
            }

            internal void SetDivider(bool shown) => _divider.enabled = shown;
        }
    }
}
