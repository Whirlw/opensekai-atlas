using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// AP or FC beside a chart in the library: a chip in the award's own colors with a gloss, a
    /// bright rim and a soft glow around it. Its letters stand the same distance from all four
    /// edges. Hidden for every other clear state.
    /// </summary>
    internal sealed class LibraryAwardBadge
    {
        private const float GLOW = 10f;
        private const byte GLOW_OPACITY = 170;

        private readonly float _radius;
        private readonly Image _glow;
        private readonly Image _chip;
        private readonly TextMeshProUGUI _label;

        /// <param name="height">Chip height in canvas units; the width follows the letters and the glow spreads outside.</param>
        /// <param name="radius">Corner radius, the same as the chips beside it.</param>
        internal LibraryAwardBadge(Transform parent, float height, float radius)
        {
            _radius = radius;
            Root = LibraryUi.CreateRect("Clear", parent);
            Root.sizeDelta = new Vector2(height, height);
            _glow = LibraryWidgets.Shape("Glow", Root, LibraryShapes.Shadow(radius, GLOW), Color.clear);
            LibraryWidgets.Inset(_glow.rectTransform, -GLOW, -GLOW, -GLOW, -GLOW);
            _chip = LibraryUi.CreateImage("Chip", Root, Color.white);
            _chip.raycastTarget = false;
            LibraryUi.Stretch(_chip.rectTransform);
            _label = LibraryWidgets.Label(
                "Label", Root, string.Empty, Mathf.Round(height * 0.66f), LibraryTheme.Colors.TextOnAccent,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_label, 1.5f * 100f / _label.fontSize);
            LibraryUi.Stretch(_label.rectTransform);
            Root.gameObject.SetActive(false);
        }

        /// <summary>The caller places it; <see cref="Show"/> sizes it to its letters.</summary>
        internal RectTransform Root { get; }

        internal bool Visible => Root.gameObject.activeSelf;

        internal float Width => Root.sizeDelta.x;

        /// <param name="badge">What <see cref="LibraryModel.ClearBadge"/> returned for the chart.</param>
        internal void Show(string badge)
        {
            bool allPerfect = badge == LibraryModel.BADGE_ALL_PERFECT;
            bool visible = allPerfect || badge == LibraryModel.BADGE_FULL_COMBO;
            Root.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_label.text != badge)
            {
                _label.text = badge;
                FitToLetters();
                _chip.sprite = LibraryShapes.AwardChip(allPerfect, Root.sizeDelta, _radius);
            }

            Color32 glow = allPerfect ? LibraryTheme.Colors.AllPerfectGlow : LibraryTheme.Colors.FullComboGlow;
            glow.a = GLOW_OPACITY;
            _glow.color = glow;
            _label.color = allPerfect ? LibraryTheme.Colors.AllPerfectInk : LibraryTheme.Colors.FullComboInk;
        }

        /// <summary>
        /// Gives the letters' sides the margin their cap height leaves above and below, and centers
        /// them by what is drawn: side bearings and the line box would set them off-center.
        /// </summary>
        private void FitToLetters()
        {
            _label.ForceMeshUpdate(true);
            TMP_TextInfo text = _label.textInfo;
            TMP_CharacterInfo first = text.characterInfo[0];
            TMP_CharacterInfo last = text.characterInfo[text.characterCount - 1];
            GlyphMetrics firstGlyph = first.textElement.glyph.metrics;
            GlyphMetrics lastGlyph = last.textElement.glyph.metrics;
            float left = first.origin + firstGlyph.horizontalBearingX * first.scale;
            float right = last.origin + (lastGlyph.horizontalBearingX + lastGlyph.width) * last.scale;
            float capHeight = _label.font.faceInfo.capLine * first.scale;

            float height = Root.sizeDelta.y;
            Root.sizeDelta = new Vector2(Mathf.Round(right - left + height - capHeight), height);
            _label.rectTransform.anchoredPosition = new Vector2(
                -(left + right) * 0.5f, -(first.baseLine + capHeight * 0.5f));
        }
    }

    /// <summary>The RANKED tag on a ranked chart's cell: gold letters in a gold hairline. Hidden otherwise.</summary>
    internal sealed class LibraryRankedTag
    {
        internal const string TEXT = "RANKED";
        private const float PADDING = 10f;
        private const float LETTER_SPACING = 1.5f;

        internal LibraryRankedTag(Transform parent, float height, float fontSize, Color32 background)
        {
            Image panel = LibraryWidgets.Panel("Ranked", parent, LibraryTheme.Radius.CHIP, background);
            Root = panel.rectTransform;
            Color32 line = LibraryTheme.Colors.Ranked;
            line.a = 190;
            Image ring = LibraryWidgets.Shape("Ring", Root, LibraryShapes.Ring(LibraryTheme.Radius.CHIP, 1.5f), line);
            LibraryUi.Stretch(ring.rectTransform);

            TextMeshProUGUI label = LibraryWidgets.Label(
                "Label", Root, TEXT, fontSize, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(label, LETTER_SPACING * 100f / fontSize);
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(
                LibraryTheme.Colors.RankedLight, LibraryTheme.Colors.RankedLight,
                LibraryTheme.Colors.Ranked, LibraryTheme.Colors.Ranked);
            LibraryUi.Stretch(label.rectTransform);

            Width = Mathf.Ceil(label.GetPreferredValues(TEXT).x) + PADDING * 2f;
            Root.sizeDelta = new Vector2(Width, height);
            Root.gameObject.SetActive(false);
        }

        /// <summary>The caller places it; its size is fixed at construction.</summary>
        internal RectTransform Root { get; }

        internal float Width { get; }

        internal bool Visible => Root.gameObject.activeSelf;

        internal void Show(bool ranked) => Root.gameObject.SetActive(ranked);
    }
}
