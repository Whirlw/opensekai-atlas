using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One medal in small, for places with room for a line of text only: the medal's color, its text
    /// and its grade, without the band, the emblem and the light. The frame keeps what tells the
    /// tiers apart (see <see cref="MedalArt.RenderCompactFrame"/>) and reaches a little past the
    /// rectangle, so the face is the same in every tier; a 段位 medal still has its lattice. It is as
    /// wide as its text asks; the owner sets the width with <see cref="SetWidth"/>.
    /// </summary>
    internal sealed class MedalChip
    {
        internal const float HEIGHT = 36f;

        private const float PADDING = 18f;
        private const float MIN_WIDTH = 120f;
        private const float GRADE = 46f;

        /// <summary>Kept clear at the chip's end, where the frames have a diamond.</summary>
        private const float GRADE_END = 5f;

        /// <summary>Widths go in these steps, so chips with different texts share their frames.</summary>
        private const float STEP = 8f;

        private readonly Image _face;
        private readonly Image _pattern;
        private readonly Image _rim;
        private readonly Image _frame;
        private readonly TextMeshProUGUI _label;
        private readonly Image _gradeLine;
        private readonly TextMeshProUGUI _roman;
        private readonly TextMeshProUGUI _greek;
        private MedalLook _look;

        internal MedalChip(Transform parent, string name)
        {
            float radius = HEIGHT * MedalArt.COMPACT_RADIUS;
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(MIN_WIDTH, HEIGHT);
            _face = LibraryWidgets.Panel("Face", Root, radius, Color.white);
            LibraryUi.Stretch(_face.rectTransform);
            _face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _pattern = LibraryWidgets.Shape("Pattern", _face.transform, MedalFrames.Pattern(HEIGHT), Color.white);
            _pattern.type = Image.Type.Tiled;
            LibraryUi.Stretch(_pattern.rectTransform);
            _rim = LibraryWidgets.Shape("Rim", Root, LibraryShapes.Ring(radius, 1f), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
            _frame = LibraryWidgets.Shape("Frame", Root, null, Color.white);
            _frame.type = Image.Type.Simple;

            _label = PackWidgets.UserLabel("Label", Root, string.Empty, 18f, LibraryTheme.Colors.TextPrimary,
                alignment: TextAlignmentOptions.Center);
            _label.enableAutoSizing = true;
            _label.fontSizeMax = 18f;
            _label.fontSizeMin = 13f;
            Vector2 right = new Vector2(1f, 0.5f);
            _gradeLine = LibraryUi.CreateImage("GradeLine", Root, Color.white);
            _gradeLine.raycastTarget = false;
            LibraryWidgets.Place(_gradeLine.rectTransform, right, right, new Vector2(-GRADE, 0f), new Vector2(1f, HEIGHT * 0.5f));
            _roman = Grade("GradeRoman", 22f);
            LibraryUi.UseCondensedFont(_roman);
            _greek = Grade("GradeGreek", 20f);
            SetEmpty(string.Empty);
        }

        internal RectTransform Root { get; }

        /// <summary>How wide the chip is when it may be as wide as its text.</summary>
        internal float FitWidth { get; private set; }

        internal void SetLook(MedalLook look)
        {
            _look = look;
            Color32 tint = look.Color;
            Color32 light = DanStyles.Light(tint);
            _face.color = Color.Lerp(HomeTheme.Colors.Card, tint, 0.24f);
            _pattern.enabled = look.Patterned;
            _pattern.color = HomeTheme.Colors.WithAlpha(light, 34);

            bool framed = look.Tier > MedalTiers.LOWEST;
            bool prism = MedalArt.IsPrismatic(look.Tier);
            _rim.enabled = !framed;
            _rim.color = HomeTheme.Colors.WithAlpha(light, 170);
            _frame.enabled = framed;
            _frame.color = prism ? Color.white : Color.Lerp(tint, Color.white, 0.3f);

            bool roman = MedalGrades.IsRoman(look.Grade);
            _gradeLine.enabled = look.Grade != null;
            _gradeLine.color = HomeTheme.Colors.WithAlpha(light, 110);
            _roman.text = roman ? look.Grade : string.Empty;
            _greek.text = roman ? string.Empty : look.Grade ?? string.Empty;
            Color32 ink = Color.Lerp(tint, Color.white, 0.72f);
            _roman.color = ink;
            _greek.color = ink;
            SetText(look.Label, LibraryTheme.Colors.TextPrimary);
        }

        /// <summary>The chip with no medal behind it: a plain grey one with <paramref name="text"/>.</summary>
        internal void SetEmpty(string text)
        {
            _look = null;
            _face.color = HomeTheme.Colors.Chip;
            _pattern.enabled = false;
            _rim.enabled = true;
            _rim.color = LibraryTheme.Colors.Hairline;
            _frame.enabled = false;
            _gradeLine.enabled = false;
            _roman.text = string.Empty;
            _greek.text = string.Empty;
            SetText(text, LibraryTheme.Colors.TextMuted);
        }

        internal void SetWidth(float width)
        {
            Root.sizeDelta = new Vector2(width, HEIGHT);
            if (!_frame.enabled)
            {
                return;
            }

            _frame.sprite = MedalFrames.Compact(_look.Tier, Root.sizeDelta);
            float margin = HEIGHT * 2f;
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_frame.rectTransform, center, center, Vector2.zero,
                new Vector2(width + MedalArt.MARGIN_X * margin, HEIGHT + MedalArt.MARGIN_Y * margin));
        }

        private void SetText(string text, Color32 color)
        {
            _label.text = text;
            _label.color = color;
            float grade = _gradeLine.enabled ? GRADE - 8f : 0f;
            LibraryWidgets.Inset(_label.rectTransform, PADDING - 4f, 0f, PADDING - 4f + grade, 0f);
            float width = Mathf.Max(MIN_WIDTH, Mathf.Ceil(_label.GetPreferredValues(text).x) + PADDING * 2f + grade);
            FitWidth = Mathf.Ceil(width / STEP) * STEP;
            SetWidth(FitWidth);
        }

        private TextMeshProUGUI Grade(string name, float size)
        {
            TextMeshProUGUI label = LibraryWidgets.Label(name, Root, string.Empty, size, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.MidlineGeoAligned);
            label.overflowMode = TextOverflowModes.Overflow;
            Vector2 right = new Vector2(1f, 0.5f);
            LibraryWidgets.Place(label.rectTransform, right, right, new Vector2(-GRADE_END, 0f), new Vector2(GRADE - GRADE_END, HEIGHT));
            return label;
        }
    }
}
