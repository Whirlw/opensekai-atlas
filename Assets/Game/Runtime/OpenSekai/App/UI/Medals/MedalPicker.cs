using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 选择奖牌: which of the two slots to fill, then every medal the player owns, two to a row, with
    /// the one worn there ringed and 不佩戴 first. A tap wears the medal (or empties the slot) and
    /// closes the card; a medal worn in the other slot trades places.
    /// </summary>
    internal sealed class MedalPicker : IDisposable
    {
        private const float CARD_WIDTH = 1220f;
        private const float CARD_HEIGHT = 820f;
        private const float TABS_HEIGHT = 56f;
        private const float TILE_HEIGHT = 128f;
        private const float GAP = 16f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly HomePageTabs _tabs;
        private readonly RectTransform _content;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly Func<int, string, bool> _onChoose;
        private IReadOnlyList<Medal> _medals = Array.Empty<Medal>();
        private IReadOnlyList<Medal> _worn = Array.Empty<Medal>();
        private int _slot;

        /// <param name="onChoose">Wears the medal with this key in this slot (null empties it); true closes the card.</param>
        internal MedalPicker(RectTransform layer, Func<int, string, bool> onChoose)
        {
            _onChoose = onChoose;
            _root = LibraryUi.CreateRect("MedalPicker", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "选择奖牌", Close, withSubtitle: true);
            _modal.Subtitle.text = "通过段位挑战、在成就页解锁成就都会得到奖牌；个人信息和首页会展示两块";
            _tabs = new HomePageTabs(_modal.Body, new[] { MedalCopy.Slot(0), MedalCopy.Slot(1) }, index => ShowSlot(index, true), 170f, TABS_HEIGHT, 21f);
            LibraryWidgets.Place(_tabs.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, _tabs.Root.sizeDelta);
            RectTransform area = LibraryUi.CreateRect("Area", _modal.Body);
            LibraryWidgets.Inset(area, 0f, 0f, 0f, TABS_HEIGHT + 20f);
            _content = PackWidgets.Scroll(area, out _);
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <param name="worn">The medal in each slot, null where none is worn.</param>
        internal void Open(IReadOnlyList<Medal> medals, IReadOnlyList<Medal> worn, int slot)
        {
            _medals = medals;
            _worn = worn;
            int count = medals.Count + 1;
            while (_tiles.Count < count) _tiles.Add(new Tile(_content, _tiles.Count, this));
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                tile.Root.gameObject.SetActive(i < count);
                int column = i % 2;
                int row = i / 2;
                tile.Root.anchorMin = new Vector2(column * 0.5f, 1f);
                tile.Root.anchorMax = new Vector2(column * 0.5f + 0.5f, 1f);
                tile.Root.offsetMin = new Vector2(column == 0 ? 0f : GAP * 0.5f, -(row * (TILE_HEIGHT + GAP) + TILE_HEIGHT));
                tile.Root.offsetMax = new Vector2(column == 0 ? -GAP * 0.5f : 0f, -row * (TILE_HEIGHT + GAP));
            }

            _content.sizeDelta = new Vector2(0f, ((count + 1) / 2) * (TILE_HEIGHT + GAP));
            _content.anchoredPosition = Vector2.zero;
            ShowSlot(slot, false);
            _modal.Open();
        }

        internal void Close() => _modal.Close();

        public void Dispose()
        {
            foreach (Tile tile in _tiles) tile.Dispose();
            _modal.Dispose();
        }

        private void ShowSlot(int slot, bool sound)
        {
            if (sound && slot != _slot) UiSound.Play(UiCue.Select);
            _slot = slot;
            _tabs.Apply(slot);
            for (int i = 0; i <= _medals.Count; i++)
            {
                Medal medal = i == 0 ? null : _medals[i - 1];
                _tiles[i].Bind(medal, SlotOf(medal), slot, _worn[slot] == null);
            }
        }

        private int SlotOf(Medal medal)
        {
            for (int i = 0; medal != null && i < _worn.Count; i++)
            {
                if (_worn[i] != null && string.Equals(_worn[i].Key, medal.Key, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        private void Choose(string key)
        {
            if (_onChoose(_slot, key)) Close();
        }

        private sealed class Tile : IDisposable
        {
            private readonly Image _background;
            private readonly Image _ring;
            private readonly MedalPlate _plate;
            private readonly TextMeshProUGUI _kind;
            private readonly TextMeshProUGUI _state;
            private string _key;

            internal Tile(RectTransform content, int index, MedalPicker owner)
            {
                Root = LibraryUi.CreateRect("Medal" + index, content);
                Root.pivot = new Vector2(0.5f, 1f);
                _background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                _ring = LibraryWidgets.Shape("Ring", Root, LibraryShapes.Ring(LibraryTheme.Radius.ROW, 2.5f), Color.clear);
                LibraryUi.Stretch(_ring.rectTransform);
                PackWidgets.RowButton(Root, () => owner.Choose(_key), UiCue.Select, Root);
                Vector2 left = new Vector2(0f, 0.5f);
                Vector2 right = new Vector2(1f, 0.5f);
                _plate = new MedalPlate(Root, "Plate", new Vector2(400f, 80f), shadow: false);
                LibraryWidgets.Place(_plate.Root, left, left, new Vector2(24f, 0f), _plate.Root.sizeDelta);
                _kind = LibraryWidgets.Label("Kind", Root, string.Empty, 17f, LibraryTheme.Colors.TextMuted,
                    alignment: TextAlignmentOptions.Right);
                LibraryWidgets.Place(_kind.rectTransform, right, right, new Vector2(-20f, 16f), new Vector2(112f, 28f));
                _state = LibraryWidgets.Label("State", Root, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Bold,
                    TextAlignmentOptions.Right);
                LibraryWidgets.Place(_state.rectTransform, right, right, new Vector2(-20f, -14f), new Vector2(112f, 30f));
            }

            internal RectTransform Root { get; }

            /// <param name="medal">Null for 不佩戴.</param>
            /// <param name="wornIn">The slot it is worn in, -1 for none.</param>
            /// <param name="empty">The slot being filled holds nothing.</param>
            internal void Bind(Medal medal, int wornIn, int slot, bool empty)
            {
                _key = medal?.Key;
                Color32 tint = LibraryTheme.Colors.TextMuted;
                if (medal == null)
                {
                    _plate.SetEmpty("不佩戴");
                    _kind.text = string.Empty;
                }
                else
                {
                    MedalLook look = MedalLook.Of(medal);
                    tint = look.Color;
                    _plate.SetLook(look);
                    _plate.SetLocked(false);
                    _kind.text = MedalCopy.Describe(medal);
                }

                bool here = medal == null ? empty : wornIn == slot;
                _ring.color = here ? (Color)DanStyles.Light(tint) : Color.clear;
                _background.color = here ? HomeTheme.Colors.WithAlpha(tint, 30) : LibraryTheme.Colors.Control;
                _state.text = medal == null || wornIn < 0 ? string.Empty : here ? "佩戴中" : MedalCopy.Slot(wornIn);
                _state.color = here ? DanStyles.Light(tint) : LibraryTheme.Colors.TextMuted;
            }

            public void Dispose() => _plate.Dispose();
        }
    }
}
