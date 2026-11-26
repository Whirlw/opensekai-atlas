using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>选择等级: 无, I to X and α to ω as tiles. A tap picks the grade and closes the card.</summary>
    internal sealed class MedalGradePicker : IDisposable
    {
        private const float CARD_WIDTH = 1040f;
        private const float CARD_HEIGHT = 480f;
        private const float GAP = 12f;
        private const float NONE_WIDTH = 190f;

        /// <summary>The Greek letters take two rows of this many.</summary>
        private const int GREEK_COLUMNS = 12;

        private readonly LibraryModal _modal;
        private readonly List<(string Grade, Image Tile, Image Ring)> _tiles = new List<(string, Image, Image)>();
        private readonly Action<string> _onChoose;

        internal MedalGradePicker(RectTransform layer, Action<string> onChoose)
        {
            _onChoose = onChoose;
            RectTransform root = LibraryUi.CreateRect("MedalGradePicker", layer);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "选择等级", Close);
            RectTransform body = _modal.Body;
            float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f;
            AddTile(body, null, "不显示", 0f, 0f, NONE_WIDTH);

            const int ROMAN = MedalGrades.ROMAN_COUNT;
            float tile = (width - GAP * (GREEK_COLUMNS - 1)) / GREEK_COLUMNS;
            float roman = (width - GAP * (ROMAN - 1)) / ROMAN;
            float row = 72f + GAP;
            for (int i = 0; i < MedalGrades.All.Count; i++)
            {
                string grade = MedalGrades.All[i];
                if (i < ROMAN) AddTile(body, grade, grade, i * (roman + GAP), row, roman);
                else
                {
                    int index = i - ROMAN;
                    AddTile(body, grade, grade, index % GREEK_COLUMNS * (tile + GAP), row * (2 + index / GREEK_COLUMNS), tile);
                }
            }

            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(string grade)
        {
            foreach ((string id, Image tile, Image ring) in _tiles)
            {
                bool chosen = string.Equals(id, grade, StringComparison.Ordinal);
                ring.color = chosen ? (Color)LibraryTheme.Colors.TextPrimary : Color.clear;
                tile.color = chosen ? LibraryTheme.Colors.ControlActive : LibraryTheme.Colors.Control;
            }

            _modal.Open();
        }

        internal void Close() => _modal.Close();

        public void Dispose() => _modal.Dispose();

        private void AddTile(RectTransform body, string grade, string text, float left, float top, float width)
        {
            Vector2 corner = new Vector2(0f, 1f);
            Image tile = LibraryWidgets.Panel("Grade" + text, body, 16f, LibraryTheme.Colors.Control);
            tile.raycastTarget = true;
            LibraryWidgets.Place(tile.rectTransform, corner, corner, new Vector2(left, -top), new Vector2(width, 72f));
            Image ring = LibraryWidgets.Shape("Ring", tile.transform, LibraryShapes.Ring(16f, 2.5f), Color.clear);
            LibraryUi.Stretch(ring.rectTransform);
            bool roman = MedalGrades.IsRoman(grade);
            TextMeshProUGUI label = LibraryWidgets.Label("Label", tile.transform, text, grade == null ? 22f : roman ? 34f : 32f,
                LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
            if (roman) LibraryUi.UseCondensedFont(label);
            label.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(label.rectTransform);
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Choose(grade));
            UiSound.Bind(button, UiCue.Select);
            LibraryPressFeedback.Attach(tile.gameObject);
            _tiles.Add((grade, tile, ring));
        }

        private void Choose(string grade)
        {
            Close();
            _onChoose(grade);
        }
    }
}
