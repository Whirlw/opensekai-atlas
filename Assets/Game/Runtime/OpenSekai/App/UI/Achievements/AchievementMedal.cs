using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One achievement's medal: a round disc (unlike the hexagonal Ranking badge and the rectangular
    /// 段位 plates, so the three read apart), its mark on the face (10K, FC, AP or a small plate),
    /// and, when asked for, a ring around it that fills with the progress. Locked medals are grey
    /// with the mark dimmed; unlocked ones take the achievement's color, glow and carry a check.
    /// The owner can drive a pop (<see cref="SetMotion"/>) for the unlock popup.
    /// </summary>
    internal sealed class AchievementMedal
    {
        private const float RING_STROKE = 0.05f;
        private const float RING_GAP = 0.05f;

        private readonly float _size;
        private readonly bool _withRing;
        private readonly Image _glow;
        private readonly Image _track;
        private readonly Image _arc;
        private readonly RectTransform _body;
        private readonly Image _disc;
        private readonly Image _rim;
        private readonly Image _inner;
        private readonly TextMeshProUGUI _mark;
        private readonly TextMeshProUGUI _subMark;
        private readonly RectTransform _plate;
        private readonly Image _plateFace;
        private readonly Image _plateBand;
        private readonly Image _plateDiamond;
        private readonly Image _plateLine;
        private readonly Image _trophy;
        private readonly RectTransform _shine;
        private readonly Image _check;
        private readonly Image _checkIcon;

        /// <param name="withRing">Draws the progress ring around the disc; the disc shrinks to make room.</param>
        internal AchievementMedal(Transform parent, string name, float size, bool withRing = true)
        {
            _size = size;
            _withRing = withRing;
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(size, size);
            Vector2 center = new Vector2(0.5f, 0.5f);

            _glow = Layer("MedalGlow", Root, LibraryShapes.Glow(), size * 1.8f);
            _track = Layer("MedalTrack", Root, LibraryShapes.Ring(size * 0.5f, size * RING_STROKE), size);
            _arc = Layer("MedalArc", Root, LibraryShapes.Ring(size * 0.5f, size * RING_STROKE), size);
            _arc.type = Image.Type.Filled;
            _arc.fillMethod = Image.FillMethod.Radial360;
            _arc.fillOrigin = (int)Image.Origin360.Top;
            _arc.fillClockwise = true;
            _track.gameObject.SetActive(withRing);
            _arc.gameObject.SetActive(withRing);

            float disc = withRing ? size * (1f - 2f * (RING_STROKE + RING_GAP)) : size;
            _body = LibraryUi.CreateRect("MedalBody", Root);
            _body.sizeDelta = new Vector2(disc, disc);
            _disc = LibraryWidgets.Panel("MedalDisc", _body, disc * 0.5f, Color.white);
            LibraryUi.Stretch(_disc.rectTransform);
            _disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image fade = LibraryWidgets.Shape("MedalShade", _disc.transform, HomeShapes.FadeDown(), new Color32(0, 0, 0, 70));
            fade.type = Image.Type.Simple;
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            fade.rectTransform.offsetMin = Vector2.zero;
            fade.rectTransform.offsetMax = Vector2.zero;

            // A bar of light that sweeps across the face when the medal pops.
            Image shine = LibraryUi.CreateImage("MedalShine", _disc.transform, new Color32(255, 255, 255, 90));
            shine.raycastTarget = false;
            LibraryWidgets.Place(shine.rectTransform, center, center, Vector2.zero, new Vector2(disc * 0.22f, disc * 1.6f));
            shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 28f);
            _shine = shine.rectTransform;
            _shine.gameObject.SetActive(false);

            _rim = LibraryWidgets.Shape("MedalRim", _body, LibraryShapes.Ring(disc * 0.5f, Mathf.Max(1.5f, disc * 0.035f)), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
            _inner = LibraryWidgets.Shape("MedalInner", _body, LibraryShapes.Ring(disc * 0.39f, Mathf.Max(1f, disc * 0.014f)), Color.white);
            LibraryWidgets.Place(_inner.rectTransform, center, center, Vector2.zero, new Vector2(disc * 0.78f, disc * 0.78f));

            _mark = LibraryWidgets.Label("MedalMark", _body, string.Empty, disc * 0.34f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_mark);
            _mark.overflowMode = TextOverflowModes.Overflow;
            _mark.textWrappingMode = TextWrappingModes.NoWrap;
            _mark.richText = true;
            LibraryWidgets.Place(_mark.rectTransform, center, center, new Vector2(0f, disc * 0.04f), new Vector2(disc, disc * 0.42f));
            _subMark = LibraryWidgets.Label("MedalSubMark", _body, string.Empty, disc * 0.12f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_subMark, 4f);
            _subMark.overflowMode = TextOverflowModes.Overflow;
            _subMark.textWrappingMode = TextWrappingModes.NoWrap;
            LibraryWidgets.Place(_subMark.rectTransform, center, center, new Vector2(0f, -disc * 0.24f), new Vector2(disc, disc * 0.16f));

            // 段位: a small plate with its band and diamond, as on the 段位 page.
            _plate = LibraryUi.CreateRect("MedalPlate", _body);
            float plateWidth = disc * 0.56f;
            float plateHeight = disc * 0.24f;
            LibraryWidgets.Place(_plate, center, center, Vector2.zero, new Vector2(plateWidth, plateHeight));
            float plateRadius = plateHeight * 0.22f;
            _plateFace = LibraryWidgets.Panel("MedalPlateFace", _plate, plateRadius, Color.white);
            LibraryUi.Stretch(_plateFace.rectTransform);
            _plateFace.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _plateBand = LibraryWidgets.Panel("MedalPlateBand", _plateFace.transform, 1f, Color.white);
            _plateBand.rectTransform.anchorMin = Vector2.zero;
            _plateBand.rectTransform.anchorMax = new Vector2(0f, 1f);
            _plateBand.rectTransform.pivot = new Vector2(0f, 0.5f);
            _plateBand.rectTransform.offsetMin = Vector2.zero;
            _plateBand.rectTransform.offsetMax = new Vector2(plateHeight, 0f);
            _plateDiamond = LibraryWidgets.Panel("MedalPlateDiamond", _plate, plateHeight * 0.08f, Color.white);
            LibraryWidgets.Place(_plateDiamond.rectTransform, new Vector2(0f, 0.5f), center, new Vector2(plateHeight * 0.5f, 0f),
                new Vector2(plateHeight * 0.36f, plateHeight * 0.36f));
            _plateDiamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _plateLine = LibraryWidgets.Panel("MedalPlateLine", _plate, plateHeight * 0.06f, Color.white);
            LibraryWidgets.Place(_plateLine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(plateHeight * 1.25f, 0f),
                new Vector2(plateWidth - plateHeight * 1.6f, plateHeight * 0.14f));

            _trophy = LibraryWidgets.Icon(_body, LibraryIcon.Trophy, LibraryTheme.Colors.TextPrimary, disc * 0.42f);

            // The check sits on the rim, at the lower right.
            float check = size * 0.3f;
            _check = LibraryWidgets.Panel("MedalCheck", Root, check * 0.5f, Color.white);
            LibraryWidgets.Place(_check.rectTransform, center, center, new Vector2(size * 0.34f, -size * 0.34f), new Vector2(check, check));
            Image checkRim = LibraryWidgets.Shape("MedalCheckRim", _check.transform, LibraryShapes.Ring(check * 0.5f, Mathf.Max(1.5f, check * 0.09f)),
                LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(checkRim.rectTransform);
            _checkIcon = LibraryWidgets.Icon(_check.transform, LibraryIcon.Check, LibraryTheme.Colors.TextOnAccent, check * 0.56f);

            SetMotion(1f, 0f);
        }

        internal RectTransform Root { get; }

        /// <param name="progress">0 to 1, drawn on the ring; ignored without one.</param>
        internal void Bind(AchievementLook look, bool unlocked, float progress)
        {
            Color32 tint = look.Tint;
            Color32 dim = LibraryTheme.Colors.TextMuted;
            _glow.color = unlocked ? HomeTheme.Colors.WithAlpha(tint, 64) : (Color32)Color.clear;
            _glow.enabled = unlocked;
            _track.color = HomeTheme.Colors.Chip;
            _arc.color = unlocked ? tint : HomeTheme.Colors.WithAlpha(tint, 210);
            _arc.fillAmount = Mathf.Clamp01(progress);
            _arc.enabled = _withRing && progress > 0f;
            _disc.color = unlocked ? Color.Lerp(HomeTheme.Colors.Card, tint, 0.3f) : Color.Lerp(HomeTheme.Colors.Card, Color.white, 0.04f);
            _rim.color = unlocked ? tint : new Color32(255, 255, 255, 40);
            _inner.color = unlocked ? HomeTheme.Colors.WithAlpha(Color.Lerp(tint, Color.white, 0.4f), 110) : new Color32(255, 255, 255, 22);

            bool text = look.Glyph == AchievementGlyph.Hits || look.Glyph == AchievementGlyph.FullCombo || look.Glyph == AchievementGlyph.AllPerfect;
            _mark.gameObject.SetActive(text);
            _subMark.gameObject.SetActive(text && !string.IsNullOrEmpty(look.SubMark));
            _plate.gameObject.SetActive(look.Glyph == AchievementGlyph.Dan);
            _trophy.gameObject.SetActive(look.Glyph == AchievementGlyph.Trophy);
            float markShift = string.IsNullOrEmpty(look.SubMark) ? 0f : _body.sizeDelta.x * 0.06f;
            _mark.rectTransform.anchoredPosition = new Vector2(0f, markShift);
            if (text)
            {
                BindMark(look, unlocked);
                _subMark.text = look.SubMark;
                _subMark.color = unlocked ? (Color)Color.Lerp(tint, Color.white, 0.55f) : (Color)HomeTheme.Colors.WithAlpha(dim, 200);
            }

            Color32 light = Color.Lerp(tint, Color.white, 0.35f);
            _plateFace.color = unlocked ? Color.Lerp(HomeTheme.Colors.Card, tint, 0.35f) : (Color)new Color32(255, 255, 255, 26);
            _plateBand.color = unlocked ? HomeTheme.Colors.WithAlpha(tint, 150) : new Color32(255, 255, 255, 22);
            _plateDiamond.color = unlocked ? Color.white : (Color)HomeTheme.Colors.WithAlpha(dim, 200);
            _plateLine.color = unlocked ? HomeTheme.Colors.WithAlpha(light, 210) : HomeTheme.Colors.WithAlpha(dim, 120);
            _trophy.color = unlocked ? light : HomeTheme.Colors.WithAlpha(dim, 200);

            _check.gameObject.SetActive(unlocked);
            _check.color = tint;
        }

        /// <param name="scale">Size of the disc (the ring keeps its size).</param>
        /// <param name="shine">0 to 1: how far the light has crossed the face; 0 and 1 hide it.</param>
        internal void SetMotion(float scale, float shine)
        {
            _body.localScale = new Vector3(scale, scale, 1f);
            bool crossing = shine > 0f && shine < 1f;
            _shine.gameObject.SetActive(crossing);
            if (crossing)
            {
                float reach = _body.sizeDelta.x * 0.8f;
                _shine.anchoredPosition = new Vector2(Mathf.Lerp(-reach, reach, shine), 0f);
            }
        }

        /// <summary>FULL COMBO's gold, ALL PERFECT's letters in its own colors, the rest in the medal's light.</summary>
        private void BindMark(AchievementLook look, bool unlocked)
        {
            _mark.enableVertexGradient = false;
            if (!unlocked)
            {
                _mark.text = look.Mark;
                _mark.color = HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.TextMuted, 220);
                return;
            }

            switch (look.Glyph)
            {
                case AchievementGlyph.AllPerfect:
                {
                    var text = new System.Text.StringBuilder();
                    float last = Mathf.Max(1, look.Mark.Length - 1);
                    for (int i = 0; i < look.Mark.Length; i++)
                    {
                        Color color = Color.Lerp(AchievementCopy.PrismStart, AchievementCopy.PrismEnd, i / last);
                        text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>').Append(look.Mark[i]).Append("</color>");
                    }

                    _mark.text = text.ToString();
                    _mark.color = Color.white;
                    return;
                }
                case AchievementGlyph.FullCombo:
                    _mark.text = look.Mark;
                    _mark.color = Color.white;
                    _mark.enableVertexGradient = true;
                    Color32 top = Color.Lerp(look.Tint, Color.white, 0.6f);
                    _mark.colorGradient = new VertexGradient(top, top, look.Tint, look.Tint);
                    return;
                default:
                    _mark.text = look.Mark;
                    _mark.color = Color.Lerp(look.Tint, Color.white, 0.45f);
                    return;
            }
        }

        private static Image Layer(string name, Transform parent, Sprite sprite, float size)
        {
            Image image = LibraryUi.CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }
    }
}
