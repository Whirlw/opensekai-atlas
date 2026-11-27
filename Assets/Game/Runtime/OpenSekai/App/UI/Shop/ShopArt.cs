using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 商店's gold coin, struck like a real one: a rim lit from the upper left and shaded
    /// toward the lower right, a recessed field with an embossed star, and a reeded edge.
    /// Now and then it turns on its upright axis: the face narrows by the cosine of the turn and
    /// slides toward the side coming forward, and the edge shows on the side turning away, its
    /// grooves running across the coin's thickness. In between a light sweeps across the face and a
    /// glint flashes on the rim. The wallet and the home card's shop sign both draw it;
    /// <see cref="Idle"/> is their shared rhythm. Below <see cref="DETAILED"/> units the bevels,
    /// grooves and glint are left out: so small, their masks would only roughen the edge.
    /// </summary>
    internal sealed class ShopCoin
    {
        /// <summary>Seconds from one turn to the next.</summary>
        internal const float BEAT = 5f;

        /// <summary>The smallest coin drawn with its bevels, grooves and glint.</summary>
        internal const float DETAILED = 64f;

        private const float TURN_SECONDS = 1.1f;
        private const float SWEEP_START = 2.6f;
        private const float SWEEP_SECONDS = 0.8f;

        /// <summary>How thick the coin is, as a share of its width.</summary>
        private const float THICKNESS = 0.1f;

        /// <summary>The grooves on the half of the edge in sight.</summary>
        private const int GROOVES = 48;

        /// <summary>The narrowest the face is drawn, side on: at nothing, its masks would let nothing through.</summary>
        private const float THINNEST = 0.04f;

        internal static readonly Color32 Edge = new Color32(176, 112, 44, 255);
        internal static readonly Color32 Ink = new Color32(135, 83, 30, 255);
        private static readonly Color32 _light = new Color32(255, 240, 204, 255);
        private static readonly Color32 _fieldGold = new Color32(244, 178, 92, 255);
        private static readonly Color32 _edgeLit = new Color32(214, 148, 70, 255);
        private static readonly Color32 _edgeShade = new Color32(146, 90, 34, 255);

        private readonly float _size;
        private readonly Image _side;
        private readonly RectTransform _face;
        private readonly Image _disc;
        private readonly Image _field;
        private readonly RectTransform _shine;
        private readonly Image _glow;
        private readonly Image _glint;
        private readonly Image _glintStar;

        internal ShopCoin(RectTransform parent, string name, float size, bool glow = true)
        {
            _size = size;
            Color32 gold = HomeTheme.Colors.Gold;
            Vector2 center = new Vector2(0.5f, 0.5f);
            bool detailed = size >= DETAILED;
            float line = Mathf.Max(1f, size * 0.012f);
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(size, size);

            _glow = LibraryUi.CreateImage("Glow", Root, HomeTheme.Colors.WithAlpha(gold, 70));
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            _glow.enabled = glow;
            LibraryWidgets.Place(_glow.rectTransform, center, center, Vector2.zero, new Vector2(size * 2.2f, size * 2.2f));

            // The edge is the face carried back through the coin's thickness. Turned, its outline is
            // the face's drawn out sideways, a disc lengthened into a stadium; the face over it leaves
            // the band on the side turning away.
            _side = LibraryWidgets.Panel("Side", Root, size * 0.5f, Edge);
            LibraryWidgets.Place(_side.rectTransform, center, center, Vector2.zero, new Vector2(size, size));
            if (detailed) Reed(_side, size);

            _face = LibraryUi.CreateRect("Face", Root);
            LibraryUi.Stretch(_face);
            _disc = LibraryWidgets.Panel("Disc", _face, size * 0.5f, gold);
            LibraryUi.Stretch(_disc.rectTransform);

            // The rim is raised: lit where the light comes from, shaded on the far side.
            float rim = Mathf.Max(1.5f, size * 0.055f);
            // A mask has a hard edge: the bevel stops a unit inside the disc, which keeps its smooth one.
            if (detailed)
            {
                Bevel(_face, "Rim", size - 2f, Mathf.Max(1f, rim - 1f), HomeTheme.Colors.WithAlpha(_light, 235),
                    HomeTheme.Colors.WithAlpha(Edge, 200));
            }

            Image rimLine = LibraryWidgets.Shape("RimLine", _face, LibraryShapes.Ring(size * 0.445f, line), HomeTheme.Colors.WithAlpha(Edge, 200));
            LibraryWidgets.Place(rimLine.rectTransform, center, center, Vector2.zero, new Vector2(size * 0.89f, size * 0.89f));

            // The field sits lower than the rim: shaded under its upper lip, lit along its lower one.
            float fieldSize = size * 0.7f;
            _field = LibraryWidgets.Panel("Field", _face, fieldSize * 0.5f, _fieldGold);
            LibraryWidgets.Place(_field.rectTransform, center, center, Vector2.zero, new Vector2(fieldSize, fieldSize));
            float lip = Mathf.Max(1f, size * 0.03f);
            if (detailed) Bevel(_face, "FieldLip", fieldSize, lip, HomeTheme.Colors.WithAlpha(Edge, 170), HomeTheme.Colors.WithAlpha(_light, 190));

            // A mask has a hard edge: this one ends under the rim, so that the disc keeps its smooth one.
            float inner = size * 0.9f;
            Image window = LibraryWidgets.Panel("Window", _face, inner * 0.5f, Color.white);
            LibraryWidgets.Place(window.rectTransform, center, center, Vector2.zero, new Vector2(inner, inner));
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            if (detailed)
            {
                Image sheen = LibraryWidgets.Shape("Sheen", window.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(Color.white, 70));
                sheen.type = Image.Type.Simple;
                LibraryWidgets.TopStrip(sheen.rectTransform, 0f, 0f, 0f, inner * 0.55f);
            }

            Image shine = LibraryUi.CreateImage("Shine", window.transform, HomeTheme.Colors.WithAlpha(Color.white, 130));
            shine.raycastTarget = false;
            LibraryWidgets.Place(shine.rectTransform, center, center, new Vector2(-size, 0f), new Vector2(size * 0.2f, size * 1.6f));
            shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 24f);
            _shine = shine.rectTransform;

            // The star is struck up from the field: its lit edge peeks out above and to the left.
            float star = size * 0.42f;
            float offset = Mathf.Max(0.75f, size * 0.012f);
            Image starLight = LibraryWidgets.Icon(_face, LibraryIcon.Star, HomeTheme.Colors.WithAlpha(_light, 230), star);
            starLight.name = "StarLight";
            starLight.rectTransform.anchoredPosition = new Vector2(-offset, offset);
            Image starInk = LibraryWidgets.Icon(_face, LibraryIcon.Star, Ink, star);
            starInk.name = "Star";
            starInk.rectTransform.anchoredPosition = new Vector2(offset * 0.5f, -offset * 0.5f);

            if (detailed)
            {
                _glint = LibraryUi.CreateImage("Glint", _face, HomeTheme.Colors.WithAlpha(Color.white, 0));
                _glint.sprite = LibraryShapes.Glow();
                _glint.raycastTarget = false;
                Vector2 at = new Vector2(-size * 0.3f, size * 0.3f);
                LibraryWidgets.Place(_glint.rectTransform, center, center, at, new Vector2(size * 0.34f, size * 0.34f));
                _glintStar = LibraryWidgets.Icon(_glint.transform, LibraryIcon.Star, HomeTheme.Colors.WithAlpha(Color.white, 0), size * 0.17f);
            }

            Apply(0f, -1f);
        }

        internal RectTransform Root { get; }

        /// <summary>Where the coin is at <paramref name="time"/> seconds: how far it has turned, and where the light is.</summary>
        internal static void Idle(double time, out float turn, out float sweep)
        {
            float beat = (float)(time % BEAT);
            turn = HomeMotion.Span(beat, 0f, TURN_SECONDS, Ease.InOutCubic);
            sweep = beat >= SWEEP_START && beat <= SWEEP_START + SWEEP_SECONDS ? (beat - SWEEP_START) / SWEEP_SECONDS : -1f;
        }

        /// <param name="turn">In whole turns; 0 and 1 show the face straight on.</param>
        /// <param name="sweep">0 to 1 as the light crosses the face; outside that, no light.</param>
        internal void Apply(float turn, float sweep)
        {
            float angle = turn * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float facing = Mathf.Abs(cos);
            float width = Mathf.Max(THINNEST, facing);
            // How far the face in sight has come round: above zero it slides right and the edge shows
            // on its left, below zero the other way. It changes sign side on, where the back takes over.
            float across = Mathf.Sin(angle) * Mathf.Sign(cos);
            float thickness = _size * THICKNESS;
            _face.localScale = new Vector3(width, 1f, 1f);
            _face.anchoredPosition = new Vector2(thickness * 0.5f * across, 0f);
            bool turned = Mathf.Abs(across) > 0.001f;
            if (_side.gameObject.activeSelf != turned) _side.gameObject.SetActive(turned);
            if (turned)
            {
                // Squeezed with the face, so the outline's ends are as narrow as the face is.
                RectTransform side = _side.rectTransform;
                side.localScale = new Vector3(width, 1f, 1f);
                side.sizeDelta = new Vector2(_size + thickness * Mathf.Abs(across) / width, _size);
                // Showing on the left, the edge faces the light; on the right, away from it.
                _side.color = Color.Lerp(_edgeShade, _edgeLit, 0.5f + 0.5f * facing * Mathf.Sign(across));
            }

            // Turned away from the light, the face darkens toward its edge's color.
            float lit = 0.45f + 0.55f * facing;
            _disc.color = Color.Lerp(Edge, HomeTheme.Colors.Gold, lit);
            _field.color = Color.Lerp(Edge, _fieldGold, lit);
            bool sweeping = sweep >= 0f && sweep <= 1f;
            _shine.gameObject.SetActive(sweeping);
            if (sweeping) _shine.anchoredPosition = new Vector2(Mathf.Lerp(-_size * 0.75f, _size * 0.75f, sweep), 0f);
            if (_glint == null) return;
            // The glint flashes as the light starts across, where the rim catches it first.
            float flash = sweeping ? Mathf.Sin(Mathf.Clamp01(sweep * 2.2f) * Mathf.PI) : 0f;
            _glint.color = HomeTheme.Colors.WithAlpha(Color.white, (byte)Mathf.RoundToInt(200f * flash));
            _glintStar.color = HomeTheme.Colors.WithAlpha(Color.white, (byte)Mathf.RoundToInt(255f * flash));
            float scale = 0.6f + 0.6f * flash;
            _glint.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        internal void SetGlow(float alpha)
        {
            Color color = _glow.color;
            color.a = alpha;
            _glow.color = color;
        }

        /// <summary>
        /// Grooves across the edge, evenly spaced round it, so that seen from the side they crowd
        /// toward the top and bottom and fade there as the edge turns from sight. Lit from above, each
        /// groove's lower wall catches the light. Every one runs the edge's whole length, clipped to
        /// its outline, and the face covers what lies under it: whatever the turn, the grooves show
        /// only on the band in sight, and nothing has to move them.
        /// </summary>
        private static void Reed(Image side, float size)
        {
            side.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image light = LibraryWidgets.Shape("Light", side.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(_light, 80));
            light.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(light.rectTransform, 0f, 0f, 0f, size * 0.4f);
            float groove = Mathf.Max(1f, size * 0.011f);
            for (int i = 0; i < GROOVES; i++)
            {
                float angle = ((i + 0.5f) / GROOVES - 0.5f) * Mathf.PI;
                float y = size * 0.5f * Mathf.Sin(angle);
                float facing = Mathf.Cos(angle);
                Across(side.rectTransform, "Groove" + i, y, groove, HomeTheme.Colors.WithAlpha(Ink, (byte)Mathf.RoundToInt(200f * facing)));
                Across(side.rectTransform, "Ridge" + i, y - groove, groove * 0.8f, HomeTheme.Colors.WithAlpha(_light, (byte)Mathf.RoundToInt(110f * facing)));
            }
        }

        /// <summary>A line <paramref name="height"/> thick right across <paramref name="parent"/>, <paramref name="y"/> above its middle.</summary>
        private static void Across(RectTransform parent, string name, float y, float height, Color32 color)
        {
            RectTransform line = LibraryWidgets.Panel(name, parent, height * 0.5f, color).rectTransform;
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(0f, height);
            line.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>
        /// A ring of <paramref name="diameter"/> shaded across: <paramref name="upperLeft"/> fading in
        /// from the upper left, <paramref name="lowerRight"/> from the lower right. Raised, the light
        /// comes first; sunk, the shade does.
        /// </summary>
        private static void Bevel(RectTransform parent, string name, float diameter, float thickness, Color32 upperLeft, Color32 lowerRight)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Image band = LibraryUi.CreateImage(name, parent, Color.white);
            band.sprite = LibraryShapes.Ring(diameter * 0.5f, thickness);
            band.raycastTarget = false;
            LibraryWidgets.Place(band.rectTransform, center, center, Vector2.zero, new Vector2(diameter, diameter));
            band.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // A square turned any way still covers the circle inside it.
            foreach ((Color32 color, float turn, string part) in new[] { (upperLeft, 45f, "UpperLeft"), (lowerRight, 225f, "LowerRight") })
            {
                Image ramp = LibraryWidgets.Shape(part, band.transform, HomeShapes.FadeUp(), color);
                ramp.type = Image.Type.Simple;
                LibraryWidgets.Place(ramp.rectTransform, center, center, Vector2.zero, new Vector2(diameter, diameter));
                ramp.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
            }
        }
    }

    /// <summary>
    /// The picture of a shop item, drawn from the item alone so that nothing ships as an image: an
    /// avatar on a wash of its color, a theme as a small page in its colors under their glow, a
    /// background as a stage of lanes under its light. Fills its parent, rounded and masked; the
    /// drawing scales with <c>height</c>, the height it is built for.
    /// </summary>
    internal sealed class ShopPreview
    {
        private readonly float _height;
        private readonly Image _base;
        private readonly RectTransform _drawing;

        /// <summary>The item or kind drawn now; drawing the same again is skipped.</summary>
        private object _drawn;

        internal ShopPreview(RectTransform parent, string name, float height, float radius)
        {
            _height = height;
            _base = LibraryWidgets.Panel(name, parent, radius, HomeTheme.Colors.Card);
            LibraryUi.Stretch(_base.rectTransform);
            _base.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _drawing = LibraryUi.CreateRect("Drawing", _base.transform);
            LibraryUi.Stretch(_drawing);
        }

        internal RectTransform Root => _base.rectTransform;

        internal void Bind(ShopItem item)
        {
            if (ReferenceEquals(_drawn, item)) return;
            _drawn = item;
            Draw(item.Category, item.Palette, item.AvatarId);
        }

        /// <summary>What a kind of goods looks like before any of it is on sale.</summary>
        internal void BindCategory(ShopCategory category)
        {
            if (_drawn is ShopCategory drawn && drawn == category) return;
            _drawn = category;
            Draw(category, ShopCopy.SamplePalette(category), null);
        }

        private void Draw(ShopCategory category, IReadOnlyList<Color32> palette, string avatarId)
        {
            for (int i = _drawing.childCount - 1; i >= 0; i--)
            {
                GameObject child = _drawing.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }

            Color32 main = palette[0];
            Color32 second = palette[Mathf.Min(1, palette.Count - 1)];
            Color32 third = palette[Mathf.Min(2, palette.Count - 1)];
            switch (category)
            {
                case ShopCategory.Avatar:
                    DrawAvatar(avatarId, main);
                    break;
                case ShopCategory.Theme:
                    DrawTheme(main, second, third);
                    break;
                default:
                    DrawBackground(main, second, third);
                    break;
            }
        }

        private void DrawAvatar(string avatarId, Color32 tint)
        {
            ProfileAvatars.Avatar avatar = ProfileAvatars.Find(avatarId);
            Color32 color = avatarId != null ? avatar.Color : tint;
            _base.color = Color.Lerp(HomeTheme.Colors.Card, color, 0.12f);
            Glow(new Vector2(0.5f, 0.5f), Vector2.zero, _height * 1.4f, color, 64);
            var view = new ProfileAvatarView(_drawing, "Avatar", _height * 0.6f, glow: false);
            view.SetAvatar(avatar.Id);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(view.Root, center, center, Vector2.zero, view.Root.sizeDelta);
        }

        /// <summary>A small page in the theme: its glows behind a card with a title bar, an accent button and the swatches.</summary>
        private void DrawTheme(Color32 main, Color32 second, Color32 third)
        {
            float h = _height;
            _base.color = Color.Lerp(LibraryTheme.Colors.Ground, main, 0.12f);
            Glow(Vector2.one, new Vector2(-h * 0.2f, -h * 0.15f), h * 1.5f, main, 110);
            Glow(Vector2.zero, new Vector2(h * 0.2f, h * 0.1f), h * 1.3f, second, 80);

            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 card = new Vector2(h * 0.8f, h * 0.52f);
            float radius = h * 0.08f;
            Image page = LibraryWidgets.Panel("Page", _drawing, radius, Color.Lerp(HomeTheme.Colors.Card, main, 0.1f));
            LibraryWidgets.Place(page.rectTransform, center, center, Vector2.zero, card);
            Image outline = LibraryWidgets.Shape("Outline", page.transform, LibraryShapes.Ring(radius, 1f), HomeTheme.Colors.WithAlpha(main, 90));
            LibraryUi.Stretch(outline.rectTransform);

            Vector2 topLeft = new Vector2(0f, 1f);
            float pad = h * 0.07f;
            Image title = LibraryWidgets.Panel("TitleBar", page.transform, h * 0.025f, HomeTheme.Colors.WithAlpha(Color.white, 70));
            LibraryWidgets.Place(title.rectTransform, topLeft, topLeft, new Vector2(pad, -pad), new Vector2(card.x * 0.46f, h * 0.05f));
            Image line = LibraryWidgets.Panel("Line", page.transform, h * 0.02f, HomeTheme.Colors.WithAlpha(Color.white, 28));
            LibraryWidgets.Place(line.rectTransform, topLeft, topLeft, new Vector2(pad, -pad - h * 0.09f), new Vector2(card.x * 0.32f, h * 0.04f));

            float dot = h * 0.085f;
            Color32[] swatches = { main, second, third };
            for (int i = 0; i < swatches.Length; i++)
            {
                Image swatch = LibraryWidgets.Panel("Swatch" + i, page.transform, dot * 0.5f, swatches[i]);
                LibraryWidgets.Place(swatch.rectTransform, Vector2.zero, Vector2.zero, new Vector2(pad + i * (dot + h * 0.03f), pad), new Vector2(dot, dot));
            }

            Image button = LibraryWidgets.Panel("Accent", page.transform, h * 0.045f, main);
            LibraryWidgets.Place(button.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad, pad), new Vector2(card.x * 0.3f, h * 0.09f));
        }

        /// <summary>A stage under the background's light: lanes running to the horizon and the judgment line across them.</summary>
        private void DrawBackground(Color32 main, Color32 second, Color32 third)
        {
            float h = _height;
            _base.color = Color.Lerp(LibraryTheme.Colors.Ground, main, 0.28f);
            Image sky = LibraryWidgets.Shape("Sky", _drawing, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(second, 120));
            sky.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(sky.rectTransform, 0f, 0f, 0f, h * 0.75f);
            Glow(new Vector2(0.5f, 1f), new Vector2(0f, -h * 0.34f), h * 1.1f, third, 120);
            Glow(new Vector2(0.5f, 1f), new Vector2(0f, -h * 0.36f), h * 0.36f, Color.white, 90);

            // The lanes meet a little above the middle and open toward the bottom edge.
            Vector2 horizon = new Vector2(0f, h * 0.08f);
            Vector2 center = new Vector2(0.5f, 0.5f);
            float[] angles = { -52f, -26f, 0f, 26f, 52f };
            for (int i = 0; i < angles.Length; i++)
            {
                bool edge = i == 0 || i == angles.Length - 1;
                Image lane = LibraryWidgets.Panel("Lane" + i, _drawing, 1f, HomeTheme.Colors.WithAlpha(Color.white, (byte)(edge ? 120 : 46)));
                LibraryWidgets.Place(lane.rectTransform, center, new Vector2(0.5f, 1f), horizon, new Vector2(Mathf.Max(1.5f, h * 0.012f), h * 0.9f));
                lane.rectTransform.localEulerAngles = new Vector3(0f, 0f, angles[i]);
            }

            Image judge = LibraryWidgets.Panel("JudgeLine", _drawing, 1f, HomeTheme.Colors.WithAlpha(Color.white, 170));
            LibraryWidgets.BottomStrip(judge.rectTransform, 0f, 0f, h * 0.2f, Mathf.Max(2f, h * 0.018f));
            Image glow = LibraryWidgets.Shape("JudgeGlow", _drawing, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(main, 90));
            glow.type = Image.Type.Simple;
            // Turned over about its middle, so that it rises from the bottom edge.
            RectTransform rise = glow.rectTransform;
            rise.anchorMin = Vector2.zero;
            rise.anchorMax = new Vector2(1f, 0f);
            rise.pivot = center;
            rise.sizeDelta = new Vector2(0f, h * 0.2f);
            rise.anchoredPosition = new Vector2(0f, h * 0.1f);
            rise.localEulerAngles = new Vector3(0f, 0f, 180f);
        }

        private void Glow(Vector2 anchor, Vector2 position, float size, Color32 tint, byte alpha)
        {
            Image glow = LibraryUi.CreateImage("Glow", _drawing, HomeTheme.Colors.WithAlpha(tint, alpha));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        }
    }
}
