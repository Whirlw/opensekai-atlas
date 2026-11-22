using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One line of the Best 30 or the history: a lead column (the place, or the day and time), the
    /// jacket, the song with its difficulty and a detail line, a tag (AP, FC, how the run ended) and
    /// a number on the right with its caption. The list recycles rows, so binding sets every field.
    /// </summary>
    internal sealed class ProfileRow
    {
        internal const float HEIGHT = 96f;
        private const float LEAD_WIDTH = 104f;
        private const float JACKET_SIZE = 68f;
        private const float VALUE_WIDTH = 210f;
        private const float TAG_HEIGHT = 32f;
        private const float TEXT_LEFT = LEAD_WIDTH + JACKET_SIZE + 22f;

        private readonly Image _surface;
        private readonly TextMeshProUGUI _lead;
        private readonly TextMeshProUGUI _leadSub;
        private readonly Image _jacket;
        private readonly GameObject _placeholder;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _difficulty;
        private readonly TextMeshProUGUI _meta;
        private readonly RectTransform _tag;
        private readonly TextMeshProUGUI _value;
        private readonly TextMeshProUGUI _caption;

        internal ProfileRow(Transform parent)
        {
            // The list pins cells by their top-left corner, so the press dip scales a centered body
            // while the hit area stays on the unscaled root and a press near the edge keeps hitting.
            Root = LibraryUi.CreateRect("Row", parent);
            Root.gameObject.AddComponent<Image>().color = Color.clear;
            RectTransform body = LibraryUi.CreateRect("Body", Root);
            LibraryUi.Stretch(body);
            _surface = LibraryWidgets.Panel("Surface", body, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(_surface.rectTransform);
            Hold = ProfileHistoryHold.Attach(Root.gameObject, body);

            Vector2 left = new Vector2(0f, 0.5f);
            _lead = LibraryWidgets.Label("Lead", body, string.Empty, 36f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_lead);
            _lead.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_lead.rectTransform, left, left, new Vector2(4f, 0f), new Vector2(LEAD_WIDTH - 8f, 44f));
            _leadSub = LibraryWidgets.Label("LeadSub", body, string.Empty, 17f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.Place(_leadSub.rectTransform, left, left, new Vector2(4f, -22f), new Vector2(LEAD_WIDTH - 8f, 24f));

            Image frame = LibraryWidgets.Panel("Jacket", body, LibraryTheme.Radius.COVER_SMALL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryWidgets.Place(frame.rectTransform, left, left, new Vector2(LEAD_WIDTH, 0f), new Vector2(JACKET_SIZE, JACKET_SIZE));
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _placeholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 28f).gameObject;
            _jacket = LibraryUi.CreateImage("Image", frame.transform, Color.white);
            _jacket.raycastTarget = false;
            _jacket.enabled = false;
            LibraryUi.Stretch(_jacket.rectTransform);

            _title = PackWidgets.UserLabel("Title", body, string.Empty, 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            Strip(_title.rectTransform, TEXT_LEFT, VALUE_WIDTH + 150f, 16f, 34f);
            _difficulty = LibraryWidgets.Label("Difficulty", body, string.Empty, 18f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
            LibraryUi.UseSpacedCondensedFont(_difficulty, 4f);
            _difficulty.richText = false;
            _difficulty.overflowMode = TextOverflowModes.Overflow;
            _difficulty.rectTransform.anchorMin = new Vector2(0f, 1f);
            _difficulty.rectTransform.anchorMax = new Vector2(0f, 1f);
            _difficulty.rectTransform.pivot = new Vector2(0f, 1f);
            _difficulty.rectTransform.anchoredPosition = new Vector2(TEXT_LEFT, -54f);
            _difficulty.rectTransform.sizeDelta = new Vector2(120f, 26f);
            _meta = PackWidgets.UserLabel("Meta", body, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            Strip(_meta.rectTransform, TEXT_LEFT + 120f, VALUE_WIDTH + 150f, 54f, 26f);

            _tag = PackWidgets.Tag(body, "AP", LibraryTheme.Colors.TextSecondary, out _, TAG_HEIGHT);
            _tag.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 2f;
            LibraryWidgets.Place(_tag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(VALUE_WIDTH + 24f), 0f),
                _tag.sizeDelta);

            _value = LibraryWidgets.Label("Value", body, string.Empty, 42f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                TextAlignmentOptions.BottomRight);
            LibraryUi.UseCondensedFont(_value);
            _value.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -4f),
                new Vector2(VALUE_WIDTH, 54f));
            _caption = PackWidgets.UserLabel("Caption", body, string.Empty, 16f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.TopRight);
            LibraryWidgets.Place(_caption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -60f),
                new Vector2(VALUE_WIDTH + 120f, 24f));
        }

        internal RectTransform Root { get; }
        internal ProfileHistoryHold Hold { get; }

        /// <summary>The list index the row shows; -1 while pooled.</summary>
        internal int Index { get; set; } = -1;

        /// <summary>Changes with each bind, so a jacket that finishes loading late can tell it is stale.</summary>
        internal int Version { get; set; }

        internal void SetLead(string lead, string sub, Color32 color, float size)
        {
            _lead.text = lead;
            _lead.color = color;
            _lead.fontSize = size;
            bool hasSub = !string.IsNullOrEmpty(sub);
            _leadSub.text = sub ?? string.Empty;
            _leadSub.gameObject.SetActive(hasSub);
            _lead.rectTransform.anchoredPosition = new Vector2(4f, hasSub ? 10f : 0f);
        }

        internal void SetSong(string title, string difficulty, Color32 difficultyColor, string meta)
        {
            _title.text = title;
            _difficulty.text = difficulty;
            _difficulty.color = difficultyColor;
            float width = Mathf.Ceil(_difficulty.GetPreferredValues(difficulty).x);
            _difficulty.rectTransform.sizeDelta = new Vector2(width + 4f, 26f);
            _meta.text = string.IsNullOrEmpty(meta) ? string.Empty : "·  " + meta;
            _meta.rectTransform.offsetMin = new Vector2(TEXT_LEFT + width + 12f, _meta.rectTransform.offsetMin.y);
        }

        internal void SetTag(string text, Color32 color)
        {
            bool shown = !string.IsNullOrEmpty(text);
            _tag.gameObject.SetActive(shown);
            if (shown)
            {
                PackWidgets.SetTag(_tag, text, color);
            }
        }

        internal void SetValue(string value, Color32 color, string caption, Color32 captionColor)
        {
            _value.text = value;
            _value.color = color;
            _caption.text = caption ?? string.Empty;
            _caption.color = captionColor;
        }

        internal void SetJacket(Sprite sprite)
        {
            _jacket.sprite = sprite;
            _jacket.enabled = sprite != null;
            _placeholder.SetActive(sprite == null);
        }

        /// <summary>A row for an index whose record could not be read.</summary>
        internal void SetMissing()
        {
            SetLead("—", null, LibraryTheme.Colors.TextMuted, 30f);
            SetSong("记录暂时无法读取", string.Empty, LibraryTheme.Colors.TextMuted, null);
            SetTag(null, default);
            SetValue(string.Empty, LibraryTheme.Colors.TextMuted, null, LibraryTheme.Colors.TextMuted);
            SetJacket(null);
        }

        private static void Strip(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
