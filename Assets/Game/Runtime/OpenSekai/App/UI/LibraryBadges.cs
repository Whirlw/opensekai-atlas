using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// AP or FC beside a chart in the library: a pill in the award's own colors with a gloss, a
    /// bright rim and a soft glow around it. Hidden for every other clear state.
    /// </summary>
    internal sealed class LibraryAwardBadge
    {
        private const float GLOW = 10f;
        private const byte GLOW_OPACITY = 170;

        private readonly Vector2 _size;
        private readonly Image _glow;
        private readonly Image _pill;
        private readonly TextMeshProUGUI _label;

        /// <param name="size">Pill size in canvas units; the glow spreads outside it.</param>
        internal LibraryAwardBadge(Transform parent, Vector2 size)
        {
            _size = size;
            Root = LibraryUi.CreateRect("Clear", parent);
            Root.sizeDelta = size;
            _glow = LibraryWidgets.Shape("Glow", Root, LibraryShapes.Shadow(size.y * 0.5f, GLOW), Color.clear);
            LibraryWidgets.Inset(_glow.rectTransform, -GLOW, -GLOW, -GLOW, -GLOW);
            _pill = LibraryUi.CreateImage("Pill", Root, Color.white);
            _pill.raycastTarget = false;
            LibraryUi.Stretch(_pill.rectTransform);
            _label = LibraryWidgets.Label(
                "Label", Root, string.Empty, Mathf.Round(size.y * 0.66f), LibraryTheme.Colors.TextOnAccent,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_label, 1.5f * 100f / _label.fontSize);
            LibraryUi.Stretch(_label.rectTransform);
            Root.gameObject.SetActive(false);
        }

        /// <summary>The caller places it; its size is the pill's.</summary>
        internal RectTransform Root { get; }

        internal bool Visible => Root.gameObject.activeSelf;

        internal float Width => _size.x;

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

            _pill.sprite = LibraryShapes.AwardPill(allPerfect, _size);
            Color32 glow = allPerfect ? LibraryTheme.Colors.AllPerfectGlow : LibraryTheme.Colors.FullComboGlow;
            glow.a = GLOW_OPACITY;
            _glow.color = glow;
            _label.text = badge;
            _label.color = allPerfect ? LibraryTheme.Colors.AllPerfectInk : LibraryTheme.Colors.FullComboInk;
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
