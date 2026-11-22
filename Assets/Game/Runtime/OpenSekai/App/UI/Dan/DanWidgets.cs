using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>Pieces the 段位 pages share: number and multi-line fields, a song line and the five-judgment table.</summary>
    internal static class DanWidgets
    {
        internal const int NUMBER_DIGITS = 6;

        /// <summary>A rounded field that takes digits only (so never a negative number), centered.</summary>
        internal static TMP_InputField NumberField(Transform parent, string name, float height, float fontSize = 24f)
        {
            TMP_InputField field = LibraryWidgets.TextField(name, parent, height, "0", null, fontSize);
            field.characterLimit = NUMBER_DIGITS;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.keyboardType = TouchScreenKeyboardType.NumberPad;
            field.onValidateInput = (text, index, character) => character >= '0' && character <= '9' ? character : '\0';
            field.textComponent.alignment = TextAlignmentOptions.Center;
            ((TextMeshProUGUI)field.placeholder).alignment = TextAlignmentOptions.Center;
            RectTransform area = field.textViewport;
            LibraryWidgets.Inset(area, 12f, 6f, 12f, 6f);
            return field;
        }

        /// <summary>A tall rounded field whose text wraps and scrolls, for pasting a shared 段位.</summary>
        internal static TMP_InputField MultiLineField(Transform parent, string name, string placeholder, float fontSize)
        {
            const float RADIUS = 18f;
            Image background = LibraryWidgets.Panel(name, parent, RADIUS, LibraryTheme.Colors.Control);
            background.raycastTarget = true;
            LibraryWidgets.Outline(background, RADIUS);
            background.gameObject.SetActive(false);
            RectTransform area = LibraryUi.CreateRect("TextArea", background.transform);
            LibraryWidgets.Inset(area, 22f, 18f, 22f, 18f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI hint = LibraryWidgets.Label("Placeholder", area, placeholder, fontSize, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            hint.textWrappingMode = TextWrappingModes.Normal;
            LibraryUi.Stretch(hint.rectTransform);
            TextMeshProUGUI text = LibraryWidgets.Label("Text", area, string.Empty, fontSize, LibraryTheme.Colors.TextPrimary,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            LibraryUi.Stretch(text.rectTransform);

            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.transition = Selectable.Transition.None;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = text.font;
            input.pointSize = fontSize;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.richText = false;
            input.caretColor = LibraryTheme.Colors.TextPrimary;
            input.customCaretColor = true;
            input.selectionColor = new Color32(157, 184, 255, 96);
            input.onSelect.AddListener(_ => background.color = LibraryTheme.Colors.ControlActive);
            input.onDeselect.AddListener(_ => background.color = LibraryTheme.Colors.Control);
            background.gameObject.SetActive(true);
            return input;
        }

        /// <summary>Draws a field's hairline in the danger color while it holds what was refused.</summary>
        internal static void SetFieldError(TMP_InputField field, bool error)
        {
            if (field == null) return;
            Transform outline = field.transform.Find("Outline");
            if (outline != null && outline.TryGetComponent(out Image image))
            {
                image.color = error ? LibraryTheme.Colors.Danger : LibraryTheme.Colors.Hairline;
            }
        }

        /// <summary>The digits of a <see cref="NumberField"/>, or null when it is empty.</summary>
        internal static int? ReadNumber(TMP_InputField field)
        {
            string text = field.text;
            return !string.IsNullOrEmpty(text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                ? value
                : (int?)null;
        }

        internal static TextMeshProUGUI Caption(Transform parent, string text, float left, float top, float width = 0f)
        {
            TextMeshProUGUI caption = LibraryWidgets.Label("Caption", parent, text, LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            if (width > 0f)
            {
                LibraryWidgets.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), new Vector2(width, 26f));
            }
            else
            {
                LibraryWidgets.TopStrip(caption.rectTransform, left, 0f, top, 26f);
            }

            return caption;
        }
    }

    /// <summary>One song of a 段位 in a list: its number in a disc, title, a muted line and a tag.</summary>
    internal sealed class DanSongLine
    {
        private readonly Image _disc;
        private readonly TextMeshProUGUI _number;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _meta;
        private readonly RectTransform _tag;
        private readonly TextMeshProUGUI _value;

        /// <param name="valueWidth">Room on the right for a value (a score, a life change); 0 for none.</param>
        internal DanSongLine(Transform parent, string name, float height, float valueWidth = 0f)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(Background.rectTransform);
            float disc = Mathf.Min(48f, height - 24f);
            _disc = LibraryWidgets.Panel("Disc", Root, disc * 0.5f, HomeTheme.Colors.Chip);
            LibraryWidgets.Place(_disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(disc, disc));
            _number = LibraryWidgets.Label("Number", _disc.transform, "1", disc * 0.5f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_number);
            _number.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_number.rectTransform);

            float left = 18f + disc + 18f;
            float right = 20f + (valueWidth > 0f ? valueWidth + 16f : 0f);
            _title = PackWidgets.UserLabel("Title", Root, string.Empty, 23f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Inset(_title.rectTransform, left, height * 0.5f - 2f, right + 150f, 0f);
            _title.alignment = TextAlignmentOptions.BottomLeft;
            _meta = PackWidgets.UserLabel("Meta", Root, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Inset(_meta.rectTransform, left, 0f, right, height * 0.5f + 4f);
            _meta.alignment = TextAlignmentOptions.TopLeft;

            _tag = PackWidgets.Tag(Root, string.Empty, LibraryTheme.Colors.TextMuted, out _, 32f);
            if (valueWidth > 0f)
            {
                _value = LibraryWidgets.Label("Value", Root, string.Empty, 30f, LibraryTheme.Colors.TextPrimary,
                    FontStyles.Normal, TextAlignmentOptions.Right);
                LibraryUi.UseCondensedFont(_value);
                _value.richText = true;
                _value.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Place(_value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                    new Vector2(valueWidth, height - 16f));
            }

            PlaceTag(valueWidth);
            _valueWidth = valueWidth;
        }

        private readonly float _valueWidth;

        internal RectTransform Root { get; }
        internal Image Background { get; }

        internal void Bind(int number, string title, string meta, string tag, Color32 tagColor, Color32 discColor, string value = null)
        {
            _number.text = number.ToString(CultureInfo.InvariantCulture);
            _disc.color = discColor;
            _title.text = title ?? string.Empty;
            _meta.text = meta ?? string.Empty;
            bool hasTag = !string.IsNullOrEmpty(tag);
            _tag.gameObject.SetActive(hasTag);
            if (hasTag)
            {
                PackWidgets.SetTag(_tag, tag, tagColor);
                PlaceTag(_valueWidth);
            }

            if (_value != null) _value.text = value ?? string.Empty;
        }

        internal void SetNumberColor(Color32 color) => _number.color = color;

        private void PlaceTag(float valueWidth)
        {
            float right = 20f + (valueWidth > 0f ? valueWidth + 16f : 0f);
            Vector2 size = _tag.sizeDelta;
            LibraryWidgets.Place(_tag, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f), size);
        }
    }

    /// <summary>Five columns, PERFECT to MISS, each with its colored name over a number.</summary>
    internal sealed class DanJudgeTable
    {
        private readonly TextMeshProUGUI[] _values = new TextMeshProUGUI[5];

        internal DanJudgeTable(Transform parent, string name, float valueSize = 30f, bool boxed = true)
        {
            Root = LibraryUi.CreateRect(name, parent);
            for (int i = 0; i < 5; i++)
            {
                RectTransform cell = LibraryUi.CreateRect(DanCopy.JudgeNames[i], Root);
                cell.anchorMin = new Vector2(i / 5f, 0f);
                cell.anchorMax = new Vector2((i + 1) / 5f, 1f);
                cell.offsetMin = new Vector2(i == 0 ? 0f : 5f, 0f);
                cell.offsetMax = new Vector2(i == 4 ? 0f : -5f, 0f);
                if (boxed)
                {
                    Image box = LibraryWidgets.Panel("Box", cell, 14f, HomeTheme.Colors.WithAlpha(DanCopy.JudgeColors[i], 22));
                    LibraryUi.Stretch(box.rectTransform);
                }

                TextMeshProUGUI label = LibraryWidgets.Label("Name", cell, DanCopy.JudgeNames[i], 14f, DanCopy.JudgeColors[i],
                    FontStyles.Bold, TextAlignmentOptions.Center);
                LibraryUi.UseSpacedCondensedFont(label, 4f);
                LibraryWidgets.TopStrip(label.rectTransform, 0f, 0f, boxed ? 8f : 0f, 20f);
                _values[i] = LibraryWidgets.Label("Value", cell, "0", valueSize, LibraryTheme.Colors.TextPrimary,
                    FontStyles.Normal, TextAlignmentOptions.Center);
                LibraryUi.UseCondensedFont(_values[i]);
                _values[i].overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Inset(_values[i].rectTransform, 0f, boxed ? 6f : 0f, 0f, boxed ? 26f : 20f);
            }
        }

        internal RectTransform Root { get; }

        internal void Set(int judge, string value) => _values[judge].text = value;
    }
}
