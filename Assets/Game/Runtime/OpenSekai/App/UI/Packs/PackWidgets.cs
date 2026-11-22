using System;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.MaterialPacks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Pieces the resource pack pages share, in the home page's look: captions and grouped panels
    /// like the settings card, whole-row buttons, the pack icon with its drawn fallback, and the
    /// little drawing each category tile carries.
    /// </summary>
    internal static class PackWidgets
    {
        internal const float ROW_RADIUS = LibraryTheme.Radius.ROW;
        internal const float SMALL_BUTTON = 56f;

        internal static Color32 Accent => HomeTheme.Colors.Rose;

        internal static string PackName(InstalledMaterialPack pack) =>
            pack == null ? "未知资源包" : string.IsNullOrWhiteSpace(pack.DisplayName) ? pack.PackId : pack.DisplayName;

        internal static string GroupName(MaterialPackGroup group) =>
            group == null ? "未知资源组" : string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName;

        /// <summary>"v1.2.0 · a1b2c3d4": what tells two revisions of one pack apart.</summary>
        internal static string Revision(InstalledMaterialPack pack) => pack == null ? string.Empty : "v" + pack.Version + " · " + pack.ShortDigest;

        /// <summary>A muted caption above a group, bottom aligned like the settings card's.</summary>
        internal static TextMeshProUGUI Caption(Transform parent, string text, float left, float right, float top)
        {
            TextMeshProUGUI caption = LibraryWidgets.Label("Caption", parent, text, LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            caption.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.TopStrip(caption.rectTransform, left + 8f, right, top, LibraryTheme.Layout.SECTION_CAPTION_HEIGHT - 10f);
            return caption;
        }

        /// <summary>The rounded, hairlined group the settings card uses for its lines.</summary>
        internal static Image Group(Transform parent, string name)
        {
            Image group = LibraryWidgets.Panel(name, parent, ROW_RADIUS, LibraryTheme.Colors.Control);
            LibraryWidgets.Outline(group, ROW_RADIUS);
            return group;
        }

        /// <summary>Text that comes from a pack (names, IDs): never parsed as rich text.</summary>
        internal static TextMeshProUGUI UserLabel(string name, Transform parent, string text, float size, Color32 color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI label = LibraryWidgets.Label(name, parent, text, size, color, style, alignment);
            label.richText = false;
            return label;
        }

        internal static TextMeshProUGUI Eyebrow(Transform parent, string text, Color32 tint, float size = 20f, float spacing = 24f)
        {
            TextMeshProUGUI label = LibraryWidgets.Label("Eyebrow", parent, text, size, tint);
            LibraryUi.UseSpacedCondensedFont(label, spacing);
            return label;
        }

        /// <summary>
        /// Makes the whole rect a button that lights up while pressed: an invisible overlay drawn
        /// right above the rect's own graphic and under its other children, so buttons inside the
        /// row still take their own taps.
        /// </summary>
        internal static Button RowButton(RectTransform row, UnityAction onClick, UiCue sound = UiCue.Press, Transform pressTarget = null)
        {
            Image hit = LibraryUi.CreateImage("Hit", row, Color.white);
            LibraryUi.Stretch(hit.rectTransform);
            hit.transform.SetAsFirstSibling();
            hit.raycastTarget = true;
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = LibraryTheme.Motion.FAST;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            UiSound.Bind(button, sound);
            if (pressTarget != null) LibraryPressFeedback.Attach(hit.gameObject, pressTarget, 0.985f);
            return button;
        }

        /// <summary>A small square-ish icon button in the control color.</summary>
        internal static Button SmallIconButton(Transform parent, string name, LibraryIcon icon, UnityAction onClick,
            float size = SMALL_BUTTON, UiCue sound = UiCue.Press)
        {
            Button button = LibraryWidgets.PillButton(name, parent, string.Empty, onClick, size, size,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: sound);
            LibraryWidgets.Outline((Image)button.targetGraphic, size * 0.5f);
            Image glyph = LibraryWidgets.Icon(button.transform, icon, LibraryTheme.Colors.TextSecondary, size * 0.42f);
            glyph.name = "Glyph";
            return button;
        }

        /// <summary>Dims a button and makes it ignore taps, or restores it.</summary>
        internal static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            // Not ??: a missing component is a Unity fake null, which ?? does not treat as null.
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.38f;
            group.blocksRaycasts = enabled;
        }

        /// <summary>A small pill of text, placed by the caller; returns its width.</summary>
        internal static RectTransform Tag(Transform parent, string text, Color32 tint, out float width, float height = 34f, bool filled = false)
        {
            Color32 background = filled ? tint : HomeTheme.Colors.WithAlpha(tint, 34);
            Color32 foreground = filled ? LibraryTheme.Colors.TextOnAccent : tint;
            return ServerWidgets.Chip(parent, text, null, background, foreground, out width, height, 16f);
        }

        internal static void SetTag(RectTransform tag, string text, Color32 tint, bool filled = false)
        {
            if (tag == null) return;
            Image background = tag.GetComponent<Image>();
            if (background != null) background.color = filled ? tint : HomeTheme.Colors.WithAlpha(tint, 34);
            TextMeshProUGUI label = tag.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.text = text;
            label.color = filled ? LibraryTheme.Colors.TextOnAccent : tint;
            float height = tag.sizeDelta.y;
            float padding = height * 0.42f;
            tag.sizeDelta = new Vector2(Mathf.Ceil(label.GetPreferredValues(text).x) + padding * 2f, height);
        }

        /// <summary>Wrapping paragraph for explanations, placed by the caller.</summary>
        internal static TextMeshProUGUI Paragraph(Transform parent, string name, string text, float size, Color32 color)
        {
            TextMeshProUGUI paragraph = LibraryWidgets.Label(name, parent, text, size, color, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            paragraph.textWrappingMode = TextWrappingModes.Normal;
            paragraph.overflowMode = TextOverflowModes.Ellipsis;
            paragraph.richText = false;
            paragraph.lineSpacing = 8f;
            return paragraph;
        }

        /// <summary>A vertical scroll area whose content the caller lays out absolutely (top anchored).</summary>
        internal static RectTransform Scroll(RectTransform parent, out ScrollRect scroll)
        {
            return ServerWidgets.VerticalList(parent, out scroll, 0f, stack: false);
        }

        // ------------------------------------------------------------------ drawings

        /// <summary>
        /// The little drawing at a category tile's corner: a note for the note skin, rings for hit
        /// effects, a wave for sounds, a PERFECT for judgment, digits for COMBO and so on. Built from
        /// the library's shapes, so it takes the tile's tint.
        /// </summary>
        internal static void CategoryGlyph(RectTransform root, string category, Color32 tint)
        {
            Color32 soft = HomeTheme.Colors.WithAlpha(tint, 170);
            Color32 faint = HomeTheme.Colors.WithAlpha(tint, 70);
            Vector2 center = new Vector2(0.5f, 0.5f);
            switch (category)
            {
                case MaterialCategories.NOTES:
                {
                    Image glow = Glow(root, tint, 150f, 60f);
                    glow.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                    Image note = LibraryWidgets.Panel("Note", root, 11f, tint);
                    LibraryWidgets.Place(note.rectTransform, center, center, new Vector2(0f, 8f), new Vector2(96f, 22f));
                    Image core = LibraryWidgets.Panel("Core", note.transform, 6f, new Color32(255, 255, 255, 150));
                    LibraryWidgets.Place(core.rectTransform, center, center, Vector2.zero, new Vector2(70f, 6f));
                    Image line = LibraryWidgets.Panel("Line", root, 2f, faint);
                    LibraryWidgets.Place(line.rectTransform, center, center, new Vector2(0f, -22f), new Vector2(118f, 4f));
                    break;
                }
                case MaterialCategories.HIT_FX:
                {
                    Glow(root, tint, 150f, 150f);
                    Ring(root, 46f, 3f, soft, 92f);
                    Ring(root, 24f, 2f, faint, 48f);
                    for (int i = 0; i < 4; i++)
                    {
                        Image spark = LibraryWidgets.Panel("Spark" + i, root, 3f, tint);
                        float angle = i * 90f + 45f;
                        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                        LibraryWidgets.Place(spark.rectTransform, center, center, direction * 58f, new Vector2(18f, 6f));
                        spark.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                    }
                    break;
                }
                case MaterialCategories.AUDIO:
                {
                    Glow(root, tint, 130f, 90f);
                    Image wave = LibraryWidgets.Icon(root, LibraryIcon.Wave, tint, 84f);
                    wave.rectTransform.anchoredPosition = Vector2.zero;
                    break;
                }
                case MaterialCategories.JUDGMENT:
                {
                    Glow(root, tint, 150f, 70f);
                    TextMeshProUGUI word = LibraryWidgets.Label("Word", root, "PERFECT", 34f, tint, FontStyles.Normal, TextAlignmentOptions.Center);
                    LibraryUi.UseSpacedCondensedFont(word, 6f);
                    word.fontStyle = FontStyles.Italic;
                    LibraryWidgets.Place(word.rectTransform, center, center, new Vector2(0f, 6f), new Vector2(170f, 48f));
                    Image underline = LibraryWidgets.Panel("Underline", root, 2f, faint);
                    LibraryWidgets.Place(underline.rectTransform, center, center, new Vector2(0f, -22f), new Vector2(110f, 4f));
                    break;
                }
                case MaterialCategories.COMBO:
                {
                    Glow(root, tint, 120f, 100f);
                    TextMeshProUGUI digits = LibraryWidgets.Label("Digits", root, "128", 64f, tint, FontStyles.Normal, TextAlignmentOptions.Center);
                    LibraryUi.UseCondensedFont(digits);
                    LibraryWidgets.Place(digits.rectTransform, center, center, new Vector2(0f, 8f), new Vector2(160f, 74f));
                    TextMeshProUGUI label = LibraryWidgets.Label("Label", root, "COMBO", 18f, soft, FontStyles.Normal, TextAlignmentOptions.Center);
                    LibraryUi.UseSpacedCondensedFont(label, 18f);
                    LibraryWidgets.Place(label.rectTransform, center, center, new Vector2(0f, -36f), new Vector2(160f, 24f));
                    break;
                }
                case MaterialCategories.LANE:
                {
                    Glow(root, tint, 120f, 110f);
                    for (int i = 0; i < 4; i++)
                    {
                        Image rail = LibraryUi.CreateImage("Rail" + i, root, i is 0 or 3 ? soft : faint);
                        rail.sprite = HomeShapes.FadeDown();
                        rail.raycastTarget = false;
                        float x = -45f + i * 30f;
                        LibraryWidgets.Place(rail.rectTransform, center, center, new Vector2(x, 0f), new Vector2(3f, 100f));
                        rail.rectTransform.localEulerAngles = new Vector3(0f, 0f, -x * 0.18f);
                    }
                    Image judge = LibraryWidgets.Panel("Judge", root, 3f, tint);
                    LibraryWidgets.Place(judge.rectTransform, center, center, new Vector2(0f, -32f), new Vector2(124f, 6f));
                    break;
                }
                case MaterialCategories.BACKGROUND:
                {
                    Glow(root, tint, 130f, 110f);
                    Image frame = LibraryWidgets.Shape("Frame", root, LibraryShapes.Ring(14f, 2f), soft);
                    LibraryWidgets.Place(frame.rectTransform, center, center, Vector2.zero, new Vector2(118f, 78f));
                    Image wash = LibraryWidgets.Shape("Wash", root, HomeShapes.FadeUp(), faint);
                    wash.type = Image.Type.Simple;
                    LibraryWidgets.Place(wash.rectTransform, center, center, new Vector2(0f, -8f), new Vector2(110f, 54f));
                    Image sun = LibraryWidgets.Panel("Sun", root, 9f, tint);
                    LibraryWidgets.Place(sun.rectTransform, center, center, new Vector2(28f, 14f), new Vector2(18f, 18f));
                    break;
                }
                case MaterialCategories.HUD:
                {
                    Glow(root, tint, 130f, 90f);
                    Image plate = LibraryWidgets.Panel("Plate", root, 8f, faint);
                    LibraryWidgets.Place(plate.rectTransform, center, center, new Vector2(0f, 14f), new Vector2(118f, 30f));
                    Image fill = LibraryWidgets.Panel("Fill", plate.transform, 5f, tint);
                    LibraryWidgets.Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(72f, 12f));
                    Image life = LibraryWidgets.Panel("Life", root, 5f, soft);
                    LibraryWidgets.Place(life.rectTransform, center, center, new Vector2(-18f, -22f), new Vector2(82f, 10f));
                    Image pause = LibraryWidgets.Shape("Pause", root, LibraryShapes.Ring(14f, 2f), soft);
                    LibraryWidgets.Place(pause.rectTransform, center, center, new Vector2(44f, -22f), new Vector2(28f, 28f));
                    break;
                }
                case MaterialCategories.RESULT_FX:
                {
                    Glow(root, tint, 150f, 150f);
                    Image trophy = LibraryWidgets.Icon(root, LibraryIcon.Trophy, tint, 76f);
                    trophy.rectTransform.anchoredPosition = new Vector2(0f, 4f);
                    Ring(root, 50f, 2f, faint, 108f);
                    break;
                }
            }
        }

        private static Image Glow(RectTransform root, Color32 tint, float width, float height)
        {
            Image glow = LibraryUi.CreateImage("Glow", root, HomeTheme.Colors.WithAlpha(tint, 60));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(glow.rectTransform, center, center, Vector2.zero, new Vector2(width * 1.6f, height * 1.6f));
            return glow;
        }

        private static void Ring(RectTransform root, float radius, float thickness, Color32 color, float size)
        {
            Image ring = LibraryWidgets.Shape("Ring", root, LibraryShapes.Ring(radius, thickness), color);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(ring.rectTransform, center, center, Vector2.zero, new Vector2(size, size));
        }
    }

    /// <summary>
    /// A pack's icon in a rounded frame: its own PNG once loaded, until then (or when it has none,
    /// or it cannot be read) a drawn default of three fanned cards in the brand colors.
    /// </summary>
    internal sealed class PackIconView
    {
        private readonly Image _picture;
        private readonly RectTransform _fallback;

        internal PackIconView(Transform parent, float size, string name = "Icon")
        {
            float radius = Mathf.Round(size * 0.24f);
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(size, size);
            Image frame = LibraryWidgets.Panel("Frame", Root, radius, HomeTheme.Colors.Card);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _fallback = LibraryUi.CreateRect("Default", frame.transform);
            LibraryUi.Stretch(_fallback);
            Image wash = LibraryWidgets.Shape("Wash", _fallback, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 70));
            wash.type = Image.Type.Simple;
            LibraryUi.Stretch(wash.rectTransform);
            Color32[] tints = { HomeTheme.Colors.Periwinkle, HomeTheme.Colors.Violet, HomeTheme.Colors.Rose };
            float[] angles = { 14f, 2f, -10f };
            Vector2 center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                Image card = LibraryWidgets.Panel("Card" + i, _fallback, size * 0.08f, Color.Lerp(HomeTheme.Colors.Card, tints[i], 0.55f));
                LibraryWidgets.Place(card.rectTransform, center, center, new Vector2((1 - i) * size * 0.11f, (1 - i) * size * 0.03f),
                    new Vector2(size * 0.36f, size * 0.48f));
                card.rectTransform.localEulerAngles = new Vector3(0f, 0f, angles[i]);
                Image rim = LibraryWidgets.Shape("Rim", card.transform, LibraryShapes.Ring(size * 0.08f, 1f), HomeTheme.Colors.WithAlpha(tints[i], 200));
                LibraryUi.Stretch(rim.rectTransform);
            }

            _picture = LibraryUi.CreateImage("Picture", frame.transform, Color.white);
            _picture.raycastTarget = false;
            _picture.preserveAspect = true;
            LibraryUi.Stretch(_picture.rectTransform);
            _picture.enabled = false;

            Image outline = LibraryWidgets.Shape("Outline", Root, LibraryShapes.Ring(radius, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(outline.rectTransform);
        }

        internal RectTransform Root { get; }

        internal void SetSprite(Sprite sprite)
        {
            _picture.sprite = sprite;
            _picture.enabled = sprite != null;
            _fallback.gameObject.SetActive(sprite == null);
        }
    }
}
