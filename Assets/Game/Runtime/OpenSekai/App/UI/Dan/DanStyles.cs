using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The built-in looks of a 段位 plate. A definition stores only an emblem id and a color, so a
    /// shared text rebuilds the same plate on another device. Ids are never renamed or reused; an
    /// id this table does not know (a newer version's) is kept as it is and drawn as the diamond.
    /// </summary>
    internal static class DanStyles
    {
        internal const string DEFAULT_EMBLEM = "diamond";
        internal const string DEFAULT_COLOR = "#526DC8";

        /// <summary>Longest plate text the maker accepts; longer text shrinks the plate's type too far.</summary>
        internal const int LABEL_LIMIT = 16;

        internal enum Kind
        {
            Diamond,
            Hexagon,
            Star,
            Chevron,
            Halo,
            Heart,
            Note,
            Trophy
        }

        internal readonly struct Emblem
        {
            internal Emblem(string id, string name, Kind kind)
            {
                Id = id;
                Name = name;
                Kind = kind;
            }

            internal string Id { get; }
            internal string Name { get; }
            internal Kind Kind { get; }
        }

        private static readonly Emblem[] _emblems =
        {
            new Emblem(DEFAULT_EMBLEM, "菱形", Kind.Diamond),
            new Emblem("hexagon", "六角", Kind.Hexagon),
            new Emblem("star", "八芒星", Kind.Star),
            new Emblem("chevron", "军阶", Kind.Chevron),
            new Emblem("halo", "光环", Kind.Halo),
            new Emblem("heart", "心", Kind.Heart),
            new Emblem("note", "音符", Kind.Note),
            new Emblem("trophy", "奖杯", Kind.Trophy)
        };

        /// <summary>The maker's swatches; any other #RRGGBB can be typed in.</summary>
        internal static readonly string[] Swatches =
        {
            DEFAULT_COLOR, "#B692FF", "#FF8CBA", "#7AB6FF", "#6ED6C0",
            "#7BD88F", "#FFC670", "#F58080", "#E8AA80", "#C5CEDA"
        };

        internal static IReadOnlyList<Emblem> Emblems => _emblems;

        internal static Emblem FindEmblem(string id)
        {
            for (int i = 0; i < _emblems.Length; i++)
            {
                if (string.Equals(_emblems[i].Id, id, StringComparison.Ordinal)) return _emblems[i];
            }

            return _emblems[0];
        }

        /// <summary>The plate color of a definition's "#RRGGBB"; <paramref name="fallback"/> for anything else.</summary>
        internal static Color32 ParseColor(string hex, Color32 fallback)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#'
                || !int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return fallback;
            }

            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }

        internal static Color32 ParseColor(string hex) => ParseColor(hex, ParseColor(DEFAULT_COLOR, Color.white));

        /// <summary>Lighter tone of a plate color, for emblems and text that sit on the plate.</summary>
        internal static Color32 Light(Color32 color) => Color.Lerp(color, Color.white, 0.55f);
    }

    /// <summary>
    /// Draws one emblem into a square rect, from the library's shapes. Used on the plates and on
    /// the maker's emblem tiles; the owner sizes the rect.
    /// </summary>
    internal static class DanEmblemArt
    {
        internal static void Draw(RectTransform root, string emblemId, Color32 color, float size)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }

            Color32 faint = HomeTheme.Colors.WithAlpha(color, 140);
            float s = size;
            switch (DanStyles.FindEmblem(emblemId).Kind)
            {
                case DanStyles.Kind.Hexagon:
                    Simple(root, "Outer", LibraryShapes.Hexagon(0.07f), color, s * 0.86f, 0f);
                    Simple(root, "Inner", LibraryShapes.Hexagon(), color, s * 0.38f, 0f);
                    break;
                case DanStyles.Kind.Star:
                    // A sparkle: two long points across, two short ones on the diagonals.
                    Ray(root, "RayA", color, s * 0.98f, 0.26f, 0f);
                    Ray(root, "RayB", color, s * 0.98f, 0.26f, 90f);
                    Ray(root, "RayC", faint, s * 0.58f, 0.3f, 45f);
                    Ray(root, "RayD", faint, s * 0.58f, 0.3f, 135f);
                    break;
                case DanStyles.Kind.Chevron:
                    for (int i = 0; i < 3; i++)
                    {
                        Image chevron = Simple(root, "Chevron" + i, LibraryShapes.Chevron(), i == 0 ? color : (i == 1 ? faint : HomeTheme.Colors.WithAlpha(color, 80)),
                            0f, 180f);
                        chevron.rectTransform.sizeDelta = new Vector2(s * 0.7f, s * 0.3f);
                        chevron.rectTransform.anchoredPosition = new Vector2(0f, s * (0.24f - i * 0.22f));
                    }

                    break;
                case DanStyles.Kind.Halo:
                    for (int i = 0; i < 3; i++)
                    {
                        float diameter = s * (0.28f + i * 0.25f);
                        Image ring = LibraryWidgets.Shape("Ring" + i, root, LibraryShapes.Ring(diameter * 0.5f, Mathf.Max(1.5f, s * (0.07f - i * 0.018f))),
                            HomeTheme.Colors.WithAlpha(color, (byte)(255 - i * 75)));
                        Center(ring.rectTransform, diameter, 0f);
                    }

                    break;
                case DanStyles.Kind.Heart:
                    Center(LibraryWidgets.Icon(root, LibraryIcon.Heart, color, s * 0.78f).rectTransform, s * 0.78f, 0f);
                    break;
                case DanStyles.Kind.Note:
                    Block(root, "Note", color, s * 0.11f, new Vector2(s * 0.78f, s * 0.24f), new Vector2(0f, s * 0.06f), 0f);
                    Block(root, "Line", faint, s * 0.02f, new Vector2(s * 0.92f, s * 0.05f), new Vector2(0f, -s * 0.22f), 0f);
                    break;
                case DanStyles.Kind.Trophy:
                    Center(LibraryWidgets.Icon(root, LibraryIcon.Trophy, color, s * 0.8f).rectTransform, s * 0.8f, 0f);
                    break;
                default:
                {
                    Image outer = LibraryWidgets.Shape("Outer", root, LibraryShapes.Ring(s * 0.07f, Mathf.Max(1.5f, s * 0.07f)), color);
                    Center(outer.rectTransform, s * 0.6f, 45f);
                    Block(root, "Inner", color, s * 0.04f, new Vector2(s * 0.24f, s * 0.24f), Vector2.zero, 45f);
                    break;
                }
            }
        }

        private static Image Block(RectTransform root, string name, Color32 color, float radius, Vector2 size, Vector2 position, float angle)
        {
            Image block = LibraryWidgets.Panel(name, root, radius, color);
            LibraryWidgets.Place(block.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            block.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            return block;
        }

        /// <summary>A diamond <paramref name="length"/> long and <paramref name="ratio"/> as wide, turned by <paramref name="angle"/>.</summary>
        private static void Ray(RectTransform root, string name, Color32 color, float length, float ratio, float angle)
        {
            RectTransform arm = LibraryUi.CreateRect(name, root);
            Center(arm, length, angle);
            arm.localScale = new Vector3(1f, ratio, 1f);
            float side = length * 0.7071f;
            Block(arm, "Point", color, Mathf.Max(1f, length * 0.02f), new Vector2(side, side), Vector2.zero, 45f);
        }

        private static Image Simple(RectTransform root, string name, Sprite sprite, Color32 color, float size, float angle)
        {
            Image image = LibraryWidgets.Shape(name, root, sprite, color);
            image.type = Image.Type.Simple;
            Center(image.rectTransform, size, angle);
            return image;
        }

        private static void Center(RectTransform rect, float size, float angle)
        {
            LibraryWidgets.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }

    /// <summary>
    /// One 段位 plate: a rectangle tinted with the plate's color, its emblem in a band on the left
    /// and the plate's text on the right, lit from above. Rectangular on purpose, unlike the
    /// hexagonal Ranking Score badge. <see cref="SetStyle"/> redraws it; <see cref="SetLocked"/>
    /// greys it out for a plate not earned yet. <see cref="Shine"/> sweeps a light across it.
    /// </summary>
    internal sealed class DanPlate : IDisposable
    {
        private readonly float _height;
        private readonly Image _face;
        private readonly Image _band;
        private readonly Image _divider;
        private readonly Image _rim;
        private readonly Image _glow;
        private readonly RectTransform _emblem;
        private readonly TextMeshProUGUI _label;
        private readonly RectTransform _shine;
        private readonly Image _shineImage;
        private MotionHandle _shineMotion;
        private string _emblemId;
        private Color32 _color;
        private bool _locked;

        /// <param name="glow">A soft halo of the plate's color behind it, for the large plates.</param>
        internal DanPlate(Transform parent, string name, Vector2 size, bool glow = false, bool shadow = true)
        {
            _height = size.y;
            float radius = Mathf.Max(6f, size.y * 0.16f);
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = size;

            _glow = LibraryUi.CreateImage("Glow", Root, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            _glow.enabled = glow;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x * 1.5f, size.y * 3.2f));
            if (shadow)
            {
                LibraryWidgets.Shadow(Root, radius, Mathf.Max(10f, size.y * 0.3f), Mathf.Max(3f, size.y * 0.08f), 120);
                _glow.transform.SetAsFirstSibling();
            }

            _face = LibraryWidgets.Panel("Face", Root, radius, Color.white);
            LibraryUi.Stretch(_face.rectTransform);
            _face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image light = LibraryWidgets.Shape("Light", _face.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(Color.white, 26));
            light.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(light.rectTransform, 0f, 0f, 0f, size.y * 0.62f);

            _band = LibraryUi.CreateImage("Band", _face.transform, Color.white);
            _band.raycastTarget = false;
            _band.rectTransform.anchorMin = Vector2.zero;
            _band.rectTransform.anchorMax = new Vector2(0f, 1f);
            _band.rectTransform.pivot = new Vector2(0f, 0.5f);
            _band.rectTransform.offsetMin = Vector2.zero;
            _band.rectTransform.offsetMax = new Vector2(size.y * 1.04f, 0f);
            _divider = LibraryUi.CreateImage("Divider", _face.transform, Color.white);
            _divider.raycastTarget = false;
            _divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            _divider.rectTransform.anchorMax = new Vector2(0f, 1f);
            _divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _divider.rectTransform.sizeDelta = new Vector2(Mathf.Max(1.5f, size.y * 0.03f), 0f);
            _divider.rectTransform.anchoredPosition = new Vector2(size.y * 1.04f, 0f);

            _emblem = LibraryUi.CreateRect("Emblem", _face.transform);
            float emblem = size.y * 0.58f;
            LibraryWidgets.Place(_emblem, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.52f, 0f),
                new Vector2(emblem, emblem));

            _label = PackWidgets.UserLabel("Label", _face.transform, string.Empty, size.y * 0.38f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Left);
            _label.enableAutoSizing = true;
            _label.fontSizeMax = size.y * 0.38f;
            _label.fontSizeMin = size.y * 0.2f;
            LibraryWidgets.Inset(_label.rectTransform, size.y * 1.04f + size.y * 0.26f, 0f, size.y * 0.3f, 0f);

            _shineImage = LibraryUi.CreateImage("Shine", _face.transform, HomeTheme.Colors.WithAlpha(Color.white, 90));
            _shineImage.raycastTarget = false;
            _shine = _shineImage.rectTransform;
            LibraryWidgets.Place(_shine, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size.x, 0f),
                new Vector2(size.y * 0.42f, size.y * 2.6f));
            _shine.localEulerAngles = new Vector3(0f, 0f, 22f);
            _shineImage.enabled = false;

            _rim = LibraryWidgets.Shape("Rim", Root, LibraryShapes.Ring(radius, Mathf.Max(1.5f, size.y * 0.028f)), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
            SetStyle(string.Empty, DanStyles.DEFAULT_EMBLEM, DanStyles.DEFAULT_COLOR);
        }

        internal RectTransform Root { get; }

        internal TextMeshProUGUI Label => _label;

        /// <summary>Redraws the plate for a definition's text, emblem id and "#RRGGBB" color.</summary>
        internal void SetStyle(string label, string emblemId, string color)
        {
            _label.text = label ?? string.Empty;
            Color32 tint = DanStyles.ParseColor(color);
            if (!string.Equals(_emblemId, emblemId, StringComparison.Ordinal) || !tint.Equals(_color))
            {
                _emblemId = emblemId;
                _color = tint;
                Apply();
            }
        }

        /// <summary>Greys the plate out (a plate not earned, or the empty plate); its text stays readable.</summary>
        internal void SetLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;
            Apply();
        }

        /// <summary>The plate with no definition behind it: a dashed-looking grey plate with <paramref name="text"/>.</summary>
        internal void SetEmpty(string text)
        {
            _label.text = text;
            _emblemId = null;
            _color = LibraryTheme.Colors.TextMuted;
            _locked = true;
            Apply();
        }

        /// <summary>Sweeps one light across the plate; <paramref name="delay"/> seconds from now.</summary>
        internal void Shine(float delay = 0f, float duration = 0.7f)
        {
            _shineMotion.TryCancel();
            float width = Root.rect.width > 0f ? Root.rect.width : Root.sizeDelta.x;
            _shineImage.enabled = true;
            _shineMotion = LMotion.Create(-width * 0.7f, width * 0.7f, duration)
                .WithDelay(delay)
                .WithEase(Ease.InOutQuad)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _shineImage.enabled = false)
                .WithOnCancel(() => _shineImage.enabled = false)
                .Bind(x => _shine.anchoredPosition = new Vector2(x, 0f));
        }

        public void Dispose() => _shineMotion.TryCancel();

        private void Apply()
        {
            Color32 tint = _locked ? (Color32)Color.Lerp(_color, LibraryTheme.Colors.TextMuted, 0.75f) : _color;
            _face.color = Color.Lerp(HomeTheme.Colors.Card, tint, _locked ? 0.1f : 0.26f);
            _band.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 34 : 70));
            _divider.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 60 : 190));
            _rim.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 90 : 190));
            _glow.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 0 : 70));
            _label.color = _locked ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextPrimary;
            if (_emblemId == null)
            {
                DanEmblemArt.Draw(_emblem, "halo", HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.TextMuted, 120), _height * 0.58f);
                return;
            }

            DanEmblemArt.Draw(_emblem, _emblemId, DanStyles.Light(tint), _height * 0.58f);
        }
    }
}
