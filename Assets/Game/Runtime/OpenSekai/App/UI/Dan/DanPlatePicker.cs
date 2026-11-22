using System;
using System.Collections.Generic;
using OpenSekai.App.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 选择段位牌: every plate the player has earned, two to a row, with the worn one ringed, and
    /// 不佩戴 first. A tap wears the plate (or takes it off) and closes the card.
    /// </summary>
    internal sealed class DanPlatePicker : IDisposable
    {
        private const float CARD_WIDTH = 1220f;
        private const float CARD_HEIGHT = 780f;
        private const float TILE_HEIGHT = 128f;
        private const float GAP = 16f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _content;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly Func<string, bool> _onChoose;

        /// <param name="onChoose">Wears the plate with this id (null takes it off); true closes the card.</param>
        internal DanPlatePicker(RectTransform layer, Func<string, bool> onChoose)
        {
            _onChoose = onChoose;
            _root = LibraryUi.CreateRect("DanPlates", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "选择段位牌", Close, withSubtitle: true);
            _modal.Subtitle.text = "通过的段位都会留下段位牌；佩戴的一块会展示在个人信息和首页上";
            RectTransform area = LibraryUi.CreateRect("Area", _modal.Body);
            LibraryUi.Stretch(area);
            _content = PackWidgets.Scroll(area, out _);
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(IReadOnlyList<DanDefinition> earned, string equippedId)
        {
            int count = earned.Count + 1;
            while (_tiles.Count < count) _tiles.Add(new Tile(_content, _tiles.Count, this));
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                bool used = i < count;
                tile.Root.gameObject.SetActive(used);
                if (!used) continue;
                int column = i % 2;
                int row = i / 2;
                tile.Root.anchorMin = new Vector2(column * 0.5f, 1f);
                tile.Root.anchorMax = new Vector2(column * 0.5f + 0.5f, 1f);
                tile.Root.offsetMin = new Vector2(column == 0 ? 0f : GAP * 0.5f, -(row * (TILE_HEIGHT + GAP) + TILE_HEIGHT));
                tile.Root.offsetMax = new Vector2(column == 0 ? -GAP * 0.5f : 0f, -row * (TILE_HEIGHT + GAP));
                DanDefinition definition = i == 0 ? null : earned[i - 1];
                tile.Bind(definition, string.Equals(definition?.Id, equippedId, StringComparison.Ordinal));
            }

            _content.sizeDelta = new Vector2(0f, ((count + 1) / 2) * (TILE_HEIGHT + GAP));
            _modal.Open();
        }

        internal void Close() => _modal.Close();

        public void Dispose()
        {
            foreach (Tile tile in _tiles) tile.Dispose();
            _modal.Dispose();
        }

        private void Choose(string id)
        {
            if (_onChoose(id)) Close();
        }

        private sealed class Tile : IDisposable
        {
            private readonly Image _background;
            private readonly Image _ring;
            private readonly DanPlate _plate;
            private readonly TextMeshProUGUI _state;
            private string _id;

            internal Tile(RectTransform content, int index, DanPlatePicker owner)
            {
                Root = LibraryUi.CreateRect("Plate" + index, content);
                Root.pivot = new Vector2(0.5f, 1f);
                _background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                _ring = LibraryWidgets.Shape("Ring", Root, LibraryShapes.Ring(LibraryTheme.Radius.ROW, 2.5f), Color.clear);
                LibraryUi.Stretch(_ring.rectTransform);
                PackWidgets.RowButton(Root, () => owner.Choose(_id), UiCue.Select, Root);
                _plate = new DanPlate(Root, "Plate", new Vector2(400f, 80f), shadow: false);
                LibraryWidgets.Place(_plate.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), _plate.Root.sizeDelta);
                _state = LibraryWidgets.Label("State", Root, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Bold,
                    TextAlignmentOptions.Right);
                LibraryWidgets.Place(_state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(120f, 32f));
            }

            internal RectTransform Root { get; }

            internal void Bind(DanDefinition definition, bool worn)
            {
                _id = definition?.Id;
                if (definition == null) _plate.SetEmpty("不佩戴");
                else
                {
                    _plate.SetStyle(definition.Label, definition.Emblem, definition.Color);
                    _plate.SetLocked(false);
                }

                Color32 tint = definition != null ? DanStyles.ParseColor(definition.Color) : LibraryTheme.Colors.TextMuted;
                _ring.color = worn ? (Color)DanStyles.Light(tint) : Color.clear;
                _background.color = worn ? HomeTheme.Colors.WithAlpha(tint, 30) : LibraryTheme.Colors.Control;
                _state.text = worn ? "佩戴中" : string.Empty;
                _state.color = DanStyles.Light(tint);
            }

            public void Dispose() => _plate.Dispose();
        }
    }
}
