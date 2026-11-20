using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The emblem built from its layers (see <see cref="BrandArt.Part"/>), so the splash can draw it
    /// in piece by piece and the header can show it whole. Every layer stretches over the square
    /// root, so resizing the root (the splash's flight into the header) carries all of it along.
    /// The sprites arrive from a worker thread; until then the emblem is simply empty.
    /// </summary>
    internal sealed class BrandEmblem
    {
        private readonly Image _halo;
        private readonly Image _orbitBack;
        private readonly Image _railsWhole;
        private readonly Image _rails;
        private readonly Image _barGlow;
        private readonly Image _crossbar;
        private readonly Image _noteGlow;
        private readonly Image _note;
        private readonly Image _orbitFront;
        private readonly Image _moonGlow;
        private readonly Image _moon;
        private readonly RectTransform _noteRect;
        private readonly RectTransform _moonRect;
        private readonly float[] _reveal = { 1f, 1f, 1f, 1f, 1f, 1f };

        internal BrandEmblem(Transform parent, string name)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(Root, center, center, Vector2.zero, new Vector2(HomeTheme.Layout.BRAND_EMBLEM_SIZE, HomeTheme.Layout.BRAND_EMBLEM_SIZE));

            _halo = Layer("Halo", LibraryShapes.Glow(), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Violet, 0));
            SetAnchors(_halo.rectTransform, new Vector2(-0.35f, -0.3f), new Vector2(1.35f, 1.4f));

            _orbitBack = Layer("OrbitBack", null, Color.white);
            SetOrbitBox(_orbitBack.rectTransform);
            _railsWhole = Layer("RailsWhole", null, Color.white);
            _rails = Layer("Rails", null, Color.white);

            _barGlow = Layer("BarGlow", LibraryShapes.Glow(), new Color(0.82f, 0.78f, 1f, 0f));
            SetAnchors(_barGlow.rectTransform,
                new Vector2(BrandArt.APEX_X - BrandArt.BAR_HALF_WIDTH * 1.9f, BrandArt.BAR_Y - 0.11f),
                new Vector2(BrandArt.APEX_X + BrandArt.BAR_HALF_WIDTH * 1.9f, BrandArt.BAR_Y + 0.11f));
            _crossbar = Layer("Crossbar", null, Color.white);

            _noteGlow = Layer("NoteGlow", LibraryShapes.Glow(), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 0));
            SetAnchors(_noteGlow.rectTransform,
                new Vector2(BrandArt.APEX_X - 0.2f, BrandArt.NOTE_Y - 0.09f),
                new Vector2(BrandArt.APEX_X + 0.2f, BrandArt.NOTE_Y + 0.09f));
            _note = Layer("Note", null, Color.white);
            _noteRect = _note.rectTransform;

            _orbitFront = Layer("OrbitFront", null, Color.white);
            SetOrbitBox(_orbitFront.rectTransform);

            BrandArt.Vec moon = BrandArt.SatelliteCenter();
            _moonGlow = Layer("MoonGlow", LibraryShapes.Glow(), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 0));
            SetAnchors(_moonGlow.rectTransform, new Vector2(moon.X - 0.12f, moon.Y - 0.12f), new Vector2(moon.X + 0.12f, moon.Y + 0.12f));
            _moon = Layer("Moon", null, Color.white);
            _moonRect = _moon.rectTransform;
            // Stretched with zero offsets, so moving the pivot only moves the point it scales around.
            _moonRect.pivot = new Vector2(moon.X, moon.Y);

            // Radial sweeps start at the orbit's left end and run clockwise: over the top (behind
            // the letter), down the right, and back along the front.
            ConfigureSweep(_orbitBack);
            ConfigureSweep(_orbitFront);

            ApplySprites(BrandSprites.Loaded);
            ShowComplete();
        }

        internal RectTransform Root { get; }

        internal bool HasSprites { get; private set; }

        internal void ApplySprites(BrandSprites sprites)
        {
            if (sprites == null)
            {
                return;
            }

            HasSprites = true;
            _orbitBack.sprite = sprites[BrandArt.Part.OrbitBack];
            _railsWhole.sprite = sprites[BrandArt.Part.RailsWhole];
            _rails.sprite = sprites[BrandArt.Part.Rails];
            _crossbar.sprite = sprites[BrandArt.Part.Crossbar];
            _note.sprite = sprites[BrandArt.Part.Note];
            _orbitFront.sprite = sprites[BrandArt.Part.OrbitFront];
            _moon.sprite = sprites[BrandArt.Part.Satellite];
            SetEnabled(true);
            Reveal(_reveal[0], _reveal[1], _reveal[2], _reveal[3], _reveal[4], _reveal[5]);
        }

        /// <summary>The finished emblem, as the header shows it.</summary>
        internal void ShowComplete()
        {
            Reveal(1f, 1f, 1f, 1f, 1f, 1f);
        }

        /// <summary>
        /// Sets how far each part has come in, each 0..1: the legs and crossbar fade in, the orbit
        /// sweeps around, the note drops in from the apex, the moon pops and the glows rise.
        /// </summary>
        internal void Reveal(float rails, float crossbar, float note, float orbit, float moon, float glow)
        {
            // Kept so sprites that arrive later pick up the current state.
            _reveal[0] = rails;
            _reveal[1] = crossbar;
            _reveal[2] = note;
            _reveal[3] = orbit;
            _reveal[4] = moon;
            _reveal[5] = glow;
            // The legs carry the cuts the front of the orbit passes through only once it is there.
            bool cut = orbit >= 1f;
            _railsWhole.enabled = HasSprites && !cut;
            _rails.enabled = HasSprites && cut;
            SetAlpha(_railsWhole, rails);
            SetAlpha(_rails, rails);

            SetAlpha(_crossbar, crossbar);
            SetAlpha(_barGlow, 0.5f * crossbar * glow);

            float drop = (1f - note) * 0.16f;
            _noteRect.anchorMin = new Vector2(0f, drop);
            _noteRect.anchorMax = new Vector2(1f, 1f + drop);
            SetAlpha(_note, Mathf.Clamp01(note * 2.2f));
            SetAlpha(_noteGlow, 0.55f * note * glow);

            // Both halves share the sweep's center and start, so one fill amount draws them in turn.
            _orbitBack.fillAmount = Mathf.Clamp01(orbit);
            _orbitFront.fillAmount = Mathf.Clamp01(orbit);
            SetAlpha(_orbitBack, 1f);
            SetAlpha(_orbitFront, 1f);

            float pop = Mathf.Max(0.0001f, moon);
            _moonRect.localScale = new Vector3(pop, pop, 1f);
            SetAlpha(_moon, Mathf.Clamp01(moon));
            SetAlpha(_moonGlow, 0.7f * Mathf.Clamp01(moon) * glow);

            SetAlpha(_halo, 0.34f * glow);
        }

        private void SetEnabled(bool enabled)
        {
            _orbitBack.enabled = enabled;
            _crossbar.enabled = enabled;
            _note.enabled = enabled;
            _orbitFront.enabled = enabled;
            _moon.enabled = enabled;
        }

        private Image Layer(string name, Sprite sprite, Color color)
        {
            Image image = LibraryUi.CreateImage(name, Root, color);
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            LibraryUi.Stretch(image.rectTransform);
            return image;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Orbit sprites are drawn in a unit box centered on the orbit, not on the emblem.</summary>
        private static void SetOrbitBox(RectTransform rect)
        {
            float half = BrandArt.ORBIT_BOX * 0.5f;
            SetAnchors(rect, new Vector2(BrandArt.ORBIT_X - half, BrandArt.ORBIT_Y - half),
                new Vector2(BrandArt.ORBIT_X + half, BrandArt.ORBIT_Y + half));
        }

        private static void ConfigureSweep(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Left;
            image.fillClockwise = true;
            image.fillAmount = 1f;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
