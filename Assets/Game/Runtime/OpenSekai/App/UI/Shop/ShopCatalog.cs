using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>What the 商店 sells; <see cref="All"/> is the tab that shows every kind.</summary>
    internal enum ShopCategory
    {
        All,
        Avatar,
        Theme,
        Background
    }

    /// <summary>
    /// One thing on the shelf. <see cref="Id"/> is stable: it is what a purchase records as its item,
    /// so it is never renamed or reused. An avatar names the built-in avatar it is drawn as; a theme
    /// and a background are drawn from <see cref="Palette"/> until they have pictures of their own.
    /// </summary>
    internal sealed class ShopItem
    {
        internal ShopItem(string id, ShopCategory category, string title, string description, int price, Color32 tint,
            string avatarId = null, IReadOnlyList<Color32> palette = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A shop item needs an id.", nameof(id));
            if (category == ShopCategory.All) throw new ArgumentException("A shop item belongs to one category.", nameof(category));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price), "A shop item costs at least one coin.");
            Id = id;
            Category = category;
            Title = string.IsNullOrWhiteSpace(title) ? id : title;
            Description = description ?? string.Empty;
            Price = price;
            Tint = tint;
            AvatarId = avatarId;
            Palette = palette != null && palette.Count > 0 ? palette : new[] { tint };
        }

        internal string Id { get; }
        internal ShopCategory Category { get; }
        internal string Title { get; }
        internal string Description { get; }
        internal int Price { get; }

        /// <summary>The item's own color: its tile's outline and wash once it is owned.</summary>
        internal Color32 Tint { get; }

        /// <summary>The built-in avatar an avatar item is drawn as; null for the other kinds.</summary>
        internal string AvatarId { get; }

        /// <summary>The colors a theme or a background is drawn in, the main one first; never empty.</summary>
        internal IReadOnlyList<Color32> Palette { get; }

        /// <summary>The color at <paramref name="index"/>, or the last one when the palette is shorter.</summary>
        internal Color32 Color(int index) => Palette[Mathf.Clamp(index, 0, Palette.Count - 1)];
    }

    /// <summary>
    /// The goods on sale, in shelf order. <see cref="Listed"/> is what the game sells; it is empty
    /// until the first avatars, themes and backgrounds go on sale, and the page then shows what is
    /// coming. Tools and tests swap in their own through <see cref="ShopScreen.Catalog"/>.
    /// </summary>
    internal sealed class ShopCatalog
    {
        private readonly ShopItem[] _items;
        private readonly Dictionary<string, ShopItem> _byId = new Dictionary<string, ShopItem>(StringComparer.Ordinal);

        internal ShopCatalog(IEnumerable<ShopItem> items)
        {
            var list = new List<ShopItem>();
            foreach (ShopItem item in items ?? Array.Empty<ShopItem>())
            {
                if (item == null) continue;
                if (_byId.ContainsKey(item.Id)) throw new ArgumentException("Two shop items share the id " + item.Id + ".", nameof(items));
                _byId.Add(item.Id, item);
                list.Add(item);
            }

            _items = list.ToArray();
        }

        /// <summary>What the game has on sale.</summary>
        internal static ShopCatalog Listed { get; } = new ShopCatalog(Array.Empty<ShopItem>());

        internal IReadOnlyList<ShopItem> Items => _items;

        /// <summary>The item with <paramref name="id"/>; null when nothing on sale has it (a retired item, another tool's purchase).</summary>
        internal ShopItem Find(string id) => id != null && _byId.TryGetValue(id, out ShopItem item) ? item : null;

        /// <summary>How many items <paramref name="category"/> has; <see cref="ShopCategory.All"/> counts every one.</summary>
        internal int Count(ShopCategory category)
        {
            if (category == ShopCategory.All) return _items.Length;
            int count = 0;
            foreach (ShopItem item in _items)
            {
                if (item.Category == category) count++;
            }

            return count;
        }

        /// <summary>The items of <paramref name="category"/> in shelf order; <see cref="ShopCategory.All"/> lists every one.</summary>
        internal List<ShopItem> In(ShopCategory category)
        {
            var items = new List<ShopItem>();
            foreach (ShopItem item in _items)
            {
                if (category == ShopCategory.All || item.Category == category) items.Add(item);
            }

            return items;
        }
    }
}
