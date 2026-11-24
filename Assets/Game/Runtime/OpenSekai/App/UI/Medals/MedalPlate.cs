using System;
using LitMotion;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One medal's plate: a rectangle tinted with the medal's color, its emblem or mark in a band on
    /// the left, its text, and its grade on the right when it has one. Rectangular on purpose,
    /// unlike the hexagonal Ranking Score badge. The tier picks the frame: 初阶 is a plain outline
    /// and the others are drawn by <see cref="MedalArt"/>. 段位 medals carry a diamond lattice on
    /// the face, which is the same size in every tier. <see cref="SetLook"/> redraws it; <see cref="SetLocked"/> greys it out for a medal
    /// not earned yet. <see cref="Shine"/> sweeps a light across it.
    /// </summary>
    internal sealed class MedalPlate : IDisposable
    {
        // Shares of the face's height.
        private const float BAND = 1.04f;
        private const float GRADE = 1.16f;

        /// <summary>Kept clear at the plate's end, where the frames have a diamond.</summary>
        private const float GRADE_END = 0.06f;

        /// <summary>The grade's ground fades out past its divider, so the pattern thins out before the grade.</summary>
        private const float GRADE_GROUND = 1.9f;
        private const float EMBLEM = 0.58f;
        private const float LABEL = 0.38f;

        private readonly Vector2 _size;
        private readonly RectTransform _body;
        private readonly Image _face;
        private readonly Image _pattern;
        private readonly Image _patternFade;
        private readonly Image _light;
        private readonly Image _band;
        private readonly Image _divider;
        private readonly Image _rim;
        private readonly Image _glow;
        private readonly Image _frame;
        private readonly Image _frameLights;
        private readonly RectTransform _emblem;
        private readonly TextMeshProUGUI _mark;
        private readonly TextMeshProUGUI _label;
        private readonly Image _gradeGround;
        private readonly Image _gradeLine;
        private readonly TextMeshProUGUI _roman;
        private readonly TextMeshProUGUI _greek;
        private readonly RectTransform _shine;
        private readonly Image _shineImage;
        private MotionHandle _shineMotion;
        private MedalLook _look;
        private (string Id, Color32 Ink, float Size) _drawnEmblem;
        private bool _locked;
        private bool _empty;

        /// <param name="glow">A soft halo of the medal's color behind it, for the large plates.</param>
        internal MedalPlate(Transform parent, string name, Vector2 size, bool glow = false, bool shadow = true)
        {
            _size = size;
            float radius = Mathf.Max(6f, size.y * MedalArt.RADIUS);
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = size;
            Vector2 center = new Vector2(0.5f, 0.5f);

            _glow = Picture("Glow", Root, LibraryShapes.Glow());
            _glow.enabled = glow;
            LibraryWidgets.Place(_glow.rectTransform, center, center, Vector2.zero, new Vector2(size.x * 1.5f, size.y * 3.2f));
            _body = LibraryUi.CreateRect("Body", Root);
            // The room round it is for the frames.
            float inset = size.y * MedalArt.INSET;
            LibraryWidgets.Inset(_body, inset, inset, inset, inset);
            if (shadow)
            {
                LibraryWidgets.Shadow(_body, radius, Mathf.Max(10f, size.y * 0.3f), Mathf.Max(3f, size.y * 0.08f), 120);
            }

            _face = LibraryWidgets.Panel("Face", _body, radius, Color.white);
            LibraryUi.Stretch(_face.rectTransform);
            _face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _pattern = Picture("Pattern", _face.transform, null);
            _pattern.type = Image.Type.Tiled;
            LibraryUi.Stretch(_pattern.rectTransform);
            // Thins the pattern out toward the left, where the text is.
            _patternFade = Picture("PatternFade", _face.transform, HomeShapes.FadeRight());
            LibraryUi.Stretch(_patternFade.rectTransform);
            _light = Picture("Light", _face.transform, HomeShapes.FadeUp());
            _light.color = HomeTheme.Colors.WithAlpha(Color.white, 26);

            _band = Picture("Band", _face.transform, null);
            _divider = Picture("Divider", _face.transform, null);
            _emblem = LibraryUi.CreateRect("Emblem", _face.transform);
            _mark = LibraryWidgets.Label("Mark", _face.transform, string.Empty, 10f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Normal, TextAlignmentOptions.MidlineGeoAligned);
            LibraryUi.UseCondensedFont(_mark);
            _mark.overflowMode = TextOverflowModes.Overflow;

            _gradeGround = Picture("GradeGround", _face.transform, HomeShapes.FadeRight());
            _gradeGround.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            _gradeLine = Picture("GradeLine", _face.transform, null);
            _roman = Grade("GradeRoman");
            LibraryUi.UseCondensedFont(_roman);
            _greek = Grade("GradeGreek");

            _label = PackWidgets.UserLabel("Label", _face.transform, string.Empty, 10f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Left);
            _label.enableAutoSizing = true;

            _shineImage = Picture("Shine", _face.transform, null);
            _shineImage.color = HomeTheme.Colors.WithAlpha(Color.white, 90);
            _shine = _shineImage.rectTransform;
            LibraryWidgets.Place(_shine, center, center, new Vector2(-size.x, 0f), new Vector2(size.y * 0.42f, size.y * 2.6f));
            _shine.localEulerAngles = new Vector3(0f, 0f, 22f);
            _shineImage.enabled = false;

            _rim = LibraryWidgets.Shape("Rim", _body, LibraryShapes.Ring(radius, Mathf.Max(1.5f, size.y * 0.028f)), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
            _frame = Picture("Frame", Root, null);
            _frameLights = Picture("FrameLights", Root, null);
            Vector2 framed = new Vector2(size.x + MedalArt.MARGIN_X * 2f * size.y, size.y + MedalArt.MARGIN_Y * 2f * size.y);
            LibraryWidgets.Place(_frame.rectTransform, center, center, Vector2.zero, framed);
            LibraryWidgets.Place(_frameLights.rectTransform, center, center, Vector2.zero, framed);
            LayOut();
            SetEmpty(string.Empty);
        }

        internal RectTransform Root { get; }

        internal TextMeshProUGUI Label => _label;

        /// <summary>Redraws the plate as <paramref name="look"/>.</summary>
        internal void SetLook(MedalLook look)
        {
            _look = look;
            _empty = false;
            Apply();
        }

        /// <summary>Greys the plate out (a medal not earned); its text stays readable.</summary>
        internal void SetLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;
            Apply();
        }

        /// <summary>The plate with no medal behind it: a grey plain plate with <paramref name="text"/>.</summary>
        internal void SetEmpty(string text)
        {
            _look = new MedalLook(text, "halo", null, LibraryTheme.Colors.TextMuted, MedalTiers.LOWEST, null, false);
            _locked = true;
            _empty = true;
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

        // ---------------------------------------------------------------- drawing

        private void Apply()
        {
            Color32 tint = _locked ? (Color32)Color.Lerp(_look.Color, LibraryTheme.Colors.TextMuted, 0.75f) : _look.Color;
            Color32 light = DanStyles.Light(tint);
            Color face = Color.Lerp(HomeTheme.Colors.Card, tint, _locked ? 0.1f : 0.26f);
            _face.color = face;
            _band.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 34 : 70));
            _divider.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 60 : 190));
            _glow.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 0 : 70));
            ApplyFrame(tint);

            _pattern.enabled = _look.Patterned;
            _patternFade.enabled = _look.Patterned;
            _pattern.color = HomeTheme.Colors.WithAlpha(Color.Lerp(tint, Color.white, 0.25f), (byte)(_locked ? 40 : 86));
            _patternFade.color = HomeTheme.Colors.WithAlpha(face, 160);

            bool marked = _look.Mark != null;
            _mark.text = marked ? _look.Mark : string.Empty;
            _mark.color = light;
            _emblem.gameObject.SetActive(!marked);
            if (!marked)
            {
                Color32 ink = _empty ? HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.TextMuted, 120) : light;
                // Drawing builds the emblem from parts; only do it when it changes.
                (string Id, Color32 Ink, float Size) wanted = (_look.Emblem, ink, _emblem.sizeDelta.y);
                if (!wanted.Equals(_drawnEmblem))
                {
                    _drawnEmblem = wanted;
                    DanEmblemArt.Draw(_emblem, wanted.Id, wanted.Ink, wanted.Size);
                }
            }

            _label.text = _look.Label;
            _label.color = _locked ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextPrimary;
            ApplyGrade(tint, light, face);
        }

        private void ApplyFrame(Color32 tint)
        {
            bool framed = _look.Tier > MedalTiers.LOWEST;
            _rim.enabled = !framed;
            _frame.enabled = framed;
            _frameLights.enabled = framed;
            _rim.color = HomeTheme.Colors.WithAlpha(tint, (byte)(_locked ? 90 : 190));
            if (!framed) return;
            MedalFrames.Frame frame = MedalFrames.Get(_look.Tier, _size);
            _frame.sprite = frame.Strokes;
            _frameLights.sprite = frame.Lights;
            bool prism = MedalArt.IsPrismatic(_look.Tier);
            // The prism is in the sprite; a locked one is dulled by the grey over it.
            Color32 stroke = prism && !_locked ? new Color32(255, 255, 255, 255) : tint;
            _frame.color = HomeTheme.Colors.WithAlpha(stroke, (byte)(_locked ? 140 : 255));
            _frameLights.color = prism && !_locked
                ? Color.white
                : (Color)HomeTheme.Colors.WithAlpha(Color.Lerp(tint, Color.white, 0.65f), (byte)(_locked ? 90 : 255));
        }

        private void ApplyGrade(Color32 tint, Color32 light, Color face)
        {
            string grade = _look.Grade;
            bool graded = grade != null;
            bool roman = graded && MedalGrades.IsRoman(grade);
            _gradeGround.enabled = graded;
            _gradeLine.enabled = graded;
            _roman.text = roman ? grade : string.Empty;
            _greek.text = graded && !roman ? grade : string.Empty;
            float height = FaceHeight;
            LibraryWidgets.Inset(_label.rectTransform, height * (BAND + 0.26f), 0f, height * (graded ? GRADE + 0.14f : 0.3f), 0f);
            if (!graded) return;
            _gradeGround.color = Color.Lerp(face, tint, _locked ? 0.1f : 0.24f);
            _gradeLine.color = HomeTheme.Colors.WithAlpha(light, (byte)(_locked ? 70 : 120));
            TextMeshProUGUI label = roman ? _roman : _greek;
            bool prism = MedalArt.IsPrismatic(_look.Tier) && !_locked;
            label.enableVertexGradient = prism;
            if (prism)
            {
                Color32[] stops = LibraryTheme.Colors.AllPerfectStops;
                Color top = Color.Lerp(stops[0], Color.white, 0.45f);
                Color bottom = Color.Lerp(stops[stops.Length - 1], Color.white, 0.35f);
                label.color = Color.white;
                label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }
            else
            {
                label.color = _locked ? LibraryTheme.Colors.TextSecondary : (Color32)Color.Lerp(tint, Color.white, 0.72f);
            }
        }

        /// <summary>Sizes everything on the face to what the frames leave of the plate.</summary>
        private void LayOut()
        {
            float height = FaceHeight;
            _pattern.sprite = MedalFrames.Pattern(height);
            LibraryWidgets.TopStrip(_light.rectTransform, 0f, 0f, 0f, height * 0.62f);

            Vector2 left = new Vector2(0f, 0.5f);
            Vector2 center = new Vector2(0.5f, 0.5f);
            float band = height * BAND;
            LibraryWidgets.Place(_band.rectTransform, left, left, Vector2.zero, new Vector2(band, height));
            LibraryWidgets.Place(_divider.rectTransform, left, center, new Vector2(band, 0f), new Vector2(Mathf.Max(1.5f, height * 0.03f), height));
            LibraryWidgets.Place(_emblem, left, center, new Vector2(band * 0.5f, 0f), new Vector2(height * EMBLEM, height * EMBLEM));
            LibraryWidgets.Place(_mark.rectTransform, left, left, Vector2.zero, new Vector2(band, height));
            _mark.fontSize = height * 0.46f;

            _label.fontSizeMax = height * LABEL;
            _label.fontSizeMin = height * 0.2f;

            Vector2 right = new Vector2(1f, 0.5f);
            float grade = height * GRADE;
            float ground = height * GRADE_GROUND;
            // Mirrored about its middle, so it is solid at the plate's end.
            LibraryWidgets.Place(_gradeGround.rectTransform, right, center, new Vector2(-ground * 0.5f, 0f), new Vector2(ground, height));
            LibraryWidgets.Place(_gradeLine.rectTransform, right, center, new Vector2(-grade, 0f), new Vector2(1.5f, height * 0.56f));
            float end = height * GRADE_END;
            LibraryWidgets.Place(_roman.rectTransform, right, right, new Vector2(-end, 0f), new Vector2(grade - end, height));
            LibraryWidgets.Place(_greek.rectTransform, right, right, new Vector2(-end, 0f), new Vector2(grade - end, height));
            _roman.fontSize = height * 0.54f;
            _greek.fontSize = height * 0.56f;
        }

        private float FaceHeight => _size.y * (1f - 2f * MedalArt.INSET);

        private TextMeshProUGUI Grade(string name)
        {
            TextMeshProUGUI label = LibraryWidgets.Label(name, _face.transform, string.Empty, 10f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.MidlineGeoAligned);
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite)
        {
            Image image = LibraryUi.CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
