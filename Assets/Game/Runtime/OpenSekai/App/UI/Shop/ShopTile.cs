using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One item in the 商店's grid: its picture on top, its name, its kind as a tag and its price in
    /// coins, or 已拥有 once it is bought (then its outline and wash take the item's color). A tap
    /// opens its exchange card. The view places the tiles <see cref="COLUMNS"/> to a row.
    /// </summary>
    internal sealed class ShopTile
    {
        internal const int COLUMNS = 4;
        internal const float HEIGHT = 300f;
        internal const float GAP = 20f;
        internal const float STEP = HEIGHT + GAP;
        private const float RADIUS = 24f;
        private const float INSET = 12f;
        private const float PREVIEW = 176f;
        private const float POP = 0.6f;

        private readonly Action<ShopItem> _open;
        private readonly RectTransform _face;
        private readonly CanvasGroup _group;
        private readonly Image _background;
        private readonly Image _outline;
        private readonly Image _flash;
        private readonly ShopPreview _preview;
        private readonly TextMeshProUGUI _title;
        private readonly RectTransform _tag;
        private readonly RectTransform _price;
        private readonly RectTransform _coin;
        private readonly TextMeshProUGUI _priceText;
        private readonly RectTransform _owned;
        private MotionHandle _pop;

        internal ShopTile(RectTransform content, int index, Action<ShopItem> open)
        {
            _open = open;
            Root = LibraryUi.CreateRect("Item" + index, content);
            Vector2 center = new Vector2(0.5f, 0.5f);

            _face = LibraryUi.CreateRect("Face", Root);
            LibraryUi.Stretch(_face);
            _group = _face.gameObject.AddComponent<CanvasGroup>();
            _background = LibraryWidgets.Panel("Background", _face, RADIUS, LibraryTheme.Colors.Control);
            _background.raycastTarget = true;
            LibraryUi.Stretch(_background.rectTransform);
            Button button = _background.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (Item != null) _open?.Invoke(Item); });
            UiSound.Bind(button, UiCue.Open);
            LibraryPressFeedback.Attach(_background.gameObject, _face, 0.985f);
            _outline = LibraryWidgets.Shape("Outline", _face, LibraryShapes.Ring(RADIUS, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(_outline.rectTransform);
            // Over the outline: the gold ring of a purchase.
            _flash = LibraryWidgets.Shape("Flash", _face, LibraryShapes.Ring(RADIUS + 3f, 3f), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, 0));
            LibraryWidgets.Inset(_flash.rectTransform, -3f, -3f, -3f, -3f);

            RectTransform picture = LibraryUi.CreateRect("Picture", _face);
            LibraryWidgets.TopStrip(picture, INSET, INSET, INSET, PREVIEW);
            _preview = new ShopPreview(picture, "Preview", PREVIEW, RADIUS - INSET * 0.5f);

            _title = LibraryWidgets.Label("Title", _face, string.Empty, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_title.rectTransform, 22f, 22f, INSET + PREVIEW + 16f, 32f);

            _tag = PackWidgets.Tag(_face, ShopCopy.Name(ShopCategory.Avatar), HomeTheme.Colors.Violet, out float tagWidth, 30f);
            LibraryWidgets.Place(_tag, Vector2.zero, Vector2.zero, new Vector2(20f, 20f), new Vector2(tagWidth, 30f));

            _price = LibraryUi.CreateRect("PriceGroup", _face);
            LibraryWidgets.Place(_price, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(160f, 38f));
            _priceText = LibraryWidgets.Label("Price", _price, string.Empty, 30f, HomeTheme.Colors.Gold, FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(_priceText);
            _priceText.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_priceText.rectTransform);
            _coin = LibraryWidgets.CoinIcon(_price, 26f);

            _owned = LibraryUi.CreateRect("Owned", _face);
            LibraryWidgets.Place(_owned, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(140f, 30f));
            TextMeshProUGUI ownedText = LibraryWidgets.Label("OwnedLabel", _owned, ShopCopy.OWNED, 18f, ShopCopy.OwnedTint, FontStyles.Bold,
                TextAlignmentOptions.Right);
            LibraryUi.Stretch(ownedText.rectTransform);
            float ownedWidth = Mathf.Ceil(ownedText.GetPreferredValues(ShopCopy.OWNED).x);
            Image check = LibraryWidgets.Icon(_owned, LibraryIcon.Check, ShopCopy.OwnedTint, 20f);
            LibraryWidgets.Place(check.rectTransform, new Vector2(1f, 0.5f), center, new Vector2(-ownedWidth - 18f, 0f), new Vector2(20f, 20f));
        }

        internal RectTransform Root { get; }

        internal ShopItem Item { get; private set; }

        internal bool IsOwned { get; private set; }

        /// <summary>Puts the tile in its cell: column by column, row by row from the top.</summary>
        internal void Place(int index)
        {
            int column = index % COLUMNS;
            int row = index / COLUMNS;
            Root.anchorMin = new Vector2(column / (float)COLUMNS, 1f);
            Root.anchorMax = new Vector2((column + 1) / (float)COLUMNS, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.offsetMin = new Vector2(GAP * 0.5f, -row * STEP - HEIGHT);
            Root.offsetMax = new Vector2(-GAP * 0.5f, -row * STEP);
        }

        /// <param name="affordable">Whether the balance covers the price; when it does not, the price is muted.</param>
        internal void Bind(ShopItem item, bool owned, bool affordable)
        {
            Stop();
            Item = item;
            IsOwned = owned;
            _preview.Bind(item);
            _title.text = item.Title;
            Color32 kind = ShopCopy.Tint(item.Category);
            PackWidgets.SetTag(_tag, ShopCopy.Name(item.Category), kind);
            _background.color = owned ? HomeTheme.Colors.WithAlpha(item.Tint, 26) : LibraryTheme.Colors.Control;
            _outline.sprite = LibraryShapes.Ring(RADIUS, owned ? 2f : 1f);
            _outline.color = owned ? HomeTheme.Colors.WithAlpha(item.Tint, 150) : LibraryTheme.Colors.Hairline;

            _price.gameObject.SetActive(!owned);
            _owned.gameObject.SetActive(owned);
            if (!owned)
            {
                string price = ShopCopy.Price(item.Price);
                _priceText.text = price;
                _priceText.color = affordable ? (Color)HomeTheme.Colors.Gold : LibraryTheme.Colors.TextMuted;
                float width = Mathf.Ceil(_priceText.GetPreferredValues(price).x);
                LibraryWidgets.Place(_coin, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width - 8f - 13f, 0f), new Vector2(26f, 26f));
                _coin.GetComponent<Image>().color = affordable ? HomeTheme.Colors.Gold : Color.Lerp(HomeTheme.Colors.Gold, LibraryTheme.Colors.TextMuted, 0.6f);
            }
        }

        /// <param name="shown">0 to 1: the tile rises a little and fades in.</param>
        internal void Apply(float shown)
        {
            _group.alpha = shown;
            _face.anchoredPosition = new Vector2(0f, -18f * (1f - shown));
        }

        /// <summary>Just bought: the tile swells once inside a gold ring.</summary>
        internal void Pop()
        {
            _pop.TryCancel();
            _pop = LMotion.Create(0f, 1f, POP)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnCancel(() => ApplyPop(1f))
                .Bind(this, static (progress, tile) => tile.ApplyPop(progress));
        }

        internal void Stop()
        {
            _pop.TryCancel();
            _pop = default;
            ApplyPop(1f);
        }

        private void ApplyPop(float progress)
        {
            float swell = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            _face.localScale = new Vector3(1f + 0.05f * swell, 1f + 0.05f * swell, 1f);
            _flash.color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, (byte)Mathf.RoundToInt(230f * Mathf.Sqrt(swell)));
        }
    }

    /// <summary>
    /// A kind of goods with nothing on sale yet: how it will look, what it is for and 即将上架. The
    /// 全部 tab shows the three side by side; a kind's own tab shows its one.
    /// </summary>
    internal sealed class ShopShelf
    {
        private const float RADIUS = 24f;
        private const float INSET = 14f;
        private const float PREVIEW = 240f;

        private readonly RectTransform _face;
        private readonly CanvasGroup _group;

        internal ShopShelf(RectTransform parent, ShopCategory category)
        {
            Category = category;
            Color32 tint = ShopCopy.Tint(category);
            Root = LibraryUi.CreateRect("Shelf" + category, parent);
            _face = LibraryUi.CreateRect("Face", Root);
            LibraryUi.Stretch(_face);
            _group = _face.gameObject.AddComponent<CanvasGroup>();
            Image background = LibraryWidgets.Panel("Background", _face, RADIUS, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(background.rectTransform);
            Image outline = LibraryWidgets.Shape("Outline", _face, LibraryShapes.Ring(RADIUS, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);

            RectTransform picture = LibraryUi.CreateRect("Picture", _face);
            LibraryWidgets.TopStrip(picture, INSET, INSET, INSET, PREVIEW);
            // Not yet on sale: the picture is held back a little.
            picture.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f;
            var preview = new ShopPreview(picture, "Preview", PREVIEW, RADIUS - INSET * 0.5f);
            preview.BindCategory(category);
            RectTransform coming = PackWidgets.Tag(_face, ShopCopy.COMING, tint, out float comingWidth, 32f);
            LibraryWidgets.Place(coming, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(INSET + 14f, -INSET - 14f), new Vector2(comingWidth, 32f));

            TextMeshProUGUI title = LibraryWidgets.Label("Title", _face, ShopCopy.Name(category), 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(title.rectTransform, 26f, 26f, INSET + PREVIEW + 20f, 36f);
            TextMeshProUGUI pitch = PackWidgets.Paragraph(_face, "Pitch", ShopCopy.Pitch(category), 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(pitch.rectTransform, 26f, 26f, INSET + PREVIEW + 62f, 62f);
        }

        internal RectTransform Root { get; }

        internal ShopCategory Category { get; }

        /// <param name="shown">0 to 1: the shelf rises a little and fades in.</param>
        internal void Apply(float shown)
        {
            _group.alpha = shown;
            _face.anchoredPosition = new Vector2(0f, -18f * (1f - shown));
        }
    }
}
