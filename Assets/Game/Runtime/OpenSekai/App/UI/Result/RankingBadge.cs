using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The Ranking Score badge. 星尘 is the plain badge: a hexagon in the tier's color with a
    /// diamond in the middle, its outline deliberately unlike the rectangular 段位 plates. Every
    /// tier above it wears a crest grown from that badge (<see cref="RankingCrestArt"/>): the same
    /// hexagon and diamond with more added at each step. A crest is drawn off the main thread the
    /// first time it is needed; until it is in, the badge shows its plain face in the tier's color
    /// and then fades the crest up.
    ///
    /// Crests move a little on their own (<see cref="Tick"/>): rays turn, the satellite rides its
    /// orbit and glints twinkle. The owner drives an upgrade through <see cref="SetMotion"/>; the
    /// higher the new tier, the more there is to it: more rings and sparks, the flank diamonds
    /// sliding out and the rays spinning up.
    /// </summary>
    internal sealed class RankingBadge
    {
        private const float RING_STROKE = 0.045f;
        private const float INNER_STROKE = 0.02f;
        private const float INNER_SCALE = 0.8f;
        private const float PIP_SIZE = 0.065f;
        private const float CREST_FADE = 0.25f;

        /// <summary>Below this size a crest holds still: glints and satellites would only be specks.</summary>
        private const float MOTION_MIN_SIZE = 90f;

        private const int RINGS = 3;
        private const float GLINT_SIZE = 0.11f;
        private const float SATELLITE_SIZE = 0.09f;
        private const float SATELLITE_PERIOD = 8f;
        /// <summary>How far behind the plaque the flank diamonds start when an upgrade slides them out.</summary>
        private const float SIDE_TUCK = 0.16f;

        /// <summary>How long an upgrade's charge runs before the new tier shows: the badge swells and whitens.</summary>
        internal const float UPGRADE_CHARGE = 0.28f;

        /// <summary>How long the new tier's reveal runs once it shows.</summary>
        internal const float UPGRADE_BURST = 0.6f;

        private readonly float _size;
        private readonly bool _moves;
        private readonly RectTransform _body;
        private readonly Image _glow;
        private readonly Image[] _rings = new Image[RINGS];
        private readonly CanvasGroup _plain;
        private readonly Image _fill;
        private readonly Image _ring;
        private readonly Image _inner;
        private readonly Image _pip;
        private readonly CanvasGroup _crest;
        private readonly Image _rays;
        private readonly Image _orbitBack;
        private readonly Image _satelliteBack;
        private readonly Image _sideLeft;
        private readonly Image _sideRight;
        private readonly Image _core;
        private readonly Image _heart;
        private readonly Image _orbitFront;
        private readonly Image _satelliteFront;
        private readonly RectTransform _glintLayer;
        private readonly Image _flash;
        private Image[] _glints = new Image[0];
        private RankingCrestArt.Point[] _glintPoints = new RankingCrestArt.Point[0];
        private RankingSparks _sparks;
        private RankingCrests.Sprites _sprites;
        private RankingCrestArt.Orbit[] _orbits = new RankingCrestArt.Orbit[0];
        private Color32 _color;
        private float _crestShown;
        private bool _crestWanted;
        private double _time;
        private float _scale = 1f;
        private float _flashAmount;
        private float _reveal = 1f;
        private int _tier = -1;

        /// <param name="animate">
        /// Let the badge run its own idle motion off the frame clock; leave false when the owner calls
        /// <see cref="Tick"/> itself, or for a badge in a list that should hold still.
        /// </param>
        internal RankingBadge(Transform parent, float size, bool animate = false)
        {
            _size = size;
            _moves = size >= MOTION_MIN_SIZE;
            Root = LibraryUi.CreateRect("RankingBadge", parent);
            Root.sizeDelta = new Vector2(size, size);

            _glow = Layer("Glow", Root, LibraryShapes.Glow(), size * 1.9f);
            for (int i = 0; i < RINGS; i++)
            {
                _rings[i] = Layer("Burst" + i, Root, LibraryShapes.Hexagon(RING_STROKE * 0.6f), size);
                _rings[i].enabled = false;
            }

            _body = LibraryUi.CreateRect("Body", Root);
            _body.sizeDelta = new Vector2(size, size);

            // The plain face: 星尘, and every tier while its crest is still being drawn.
            _plain = Group("Plain", _body);
            _fill = Layer("Fill", _plain.transform, LibraryShapes.Hexagon(), size);
            _inner = Layer("Inner", _plain.transform, LibraryShapes.Hexagon(INNER_STROKE / INNER_SCALE), size * INNER_SCALE);
            _ring = Layer("Ring", _plain.transform, LibraryShapes.Hexagon(RING_STROKE), size);
            float pip = size * PIP_SIZE;
            _pip = LibraryWidgets.Panel("Pip", _plain.transform, pip * 0.22f, Color.white);
            _pip.raycastTarget = false;
            _pip.rectTransform.sizeDelta = new Vector2(pip, pip);
            _pip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // The crest, back to front.
            _crest = Group("Crest", _body);
            _rays = Layer("Rays", _crest.transform, null, size);
            _orbitBack = Layer("OrbitBack", _crest.transform, null, size);
            _satelliteBack = Layer("SatelliteBack", _crest.transform, LibraryShapes.Glow(), size * SATELLITE_SIZE);
            _sideLeft = Layer("SideLeft", _crest.transform, null, size);
            _sideRight = Layer("SideRight", _crest.transform, null, size);
            _core = Layer("Core", _crest.transform, null, size);
            _heart = Layer("Heart", _crest.transform, LibraryShapes.Glow(), size * 0.6f);
            _orbitFront = Layer("OrbitFront", _crest.transform, null, size);
            _satelliteFront = Layer("SatelliteFront", _crest.transform, LibraryShapes.Glow(), size * SATELLITE_SIZE);
            _glintLayer = LibraryUi.CreateRect("Glints", _crest.transform);

            _flash = Layer("Flash", _body, LibraryShapes.Hexagon(), size);
            Clock = Root.gameObject.AddComponent<RankingBadgeClock>();
            Clock.Bind(this, animate && _moves);
            SetMotion(1f, 0f, 1f);
        }

        internal RectTransform Root { get; }

        internal int Tier => _tier;

        /// <summary>True once the tier's crest (or 星尘's plain face) is fully shown.</summary>
        internal bool IsSettled => !_crestWanted || _crestShown >= 1f;

        private RankingBadgeClock Clock { get; }

        internal void SetTier(int tier)
        {
            if (tier == _tier)
            {
                return;
            }

            _tier = tier;
            _color = RankingTiers.Color(tier);
            Color32 glow = RankingTiers.IsPrismatic(tier) ? LibraryTheme.Colors.AllPerfectStops[1] : _color;
            _glow.color = new Color32(glow.r, glow.g, glow.b, (byte)(70 + 8 * Mathf.Clamp(tier, 0, RankingTiers.TOP)));
            _ring.color = _color;
            _inner.color = new Color32(_color.r, _color.g, _color.b, 90);
            _fill.color = Color.Lerp(LibraryTheme.Colors.Surface, _color, 0.22f);
            _pip.color = _color;
            for (int i = 0; i < RINGS; i++)
            {
                _rings[i].color = RankingTiers.IsPrismatic(tier) ? LibraryTheme.Colors.AllPerfectStops[i % 3] : _color;
            }

            _crestWanted = RankingCrestArt.HasCrest(tier);
            float hexScale = _crestWanted ? RankingCrestArt.HexScale(tier) : 1f;
            Vector2 hex = new Vector2(_size * hexScale, _size * hexScale);
            _flash.rectTransform.sizeDelta = hex;
            foreach (Image ring in _rings) ring.rectTransform.sizeDelta = hex;

            _sprites = null;
            _crestShown = 0f;
            if (_crestWanted && RankingCrests.TryGet(tier, _size, out RankingCrests.Sprites sprites) && sprites.HasCore)
            {
                // Already drawn for another badge: no need to fade.
                ApplyCrest(sprites);
                _crestShown = 1f;
            }
            else
            {
                HideCrest();
            }

            Clock.Wake();
            Apply();
        }

        /// <summary>Starts drawing <paramref name="tier"/>'s crest now, so an upgrade later shows it at once.</summary>
        internal void Warm(int tier) => RankingCrests.Warm(tier, _size);

        /// <param name="scale">Size of the badge itself.</param>
        /// <param name="flash">0 to 1: how far the face has turned white.</param>
        /// <param name="reveal">
        /// 0 to 1 through an upgrade's reveal, which starts the moment the new tier is shown: rings
        /// fly off, sparks scatter and the flank diamonds slide out. 1 is at rest.
        /// </param>
        internal void SetMotion(float scale, float flash, float reveal)
        {
            _scale = scale;
            _flashAmount = flash;
            _reveal = Mathf.Clamp01(reveal);
            Apply();
        }

        /// <summary>
        /// The upgrade at <paramref name="time"/> seconds from the moment the new tier shows; the
        /// owner swaps the tier at 0. Over the <see cref="UPGRADE_CHARGE"/> before it the badge
        /// swells and turns white; then the new tier pops in large and white and settles while its
        /// reveal plays out over <see cref="UPGRADE_BURST"/>. Earlier than the charge it is at rest.
        /// </summary>
        internal void SetUpgrade(float time)
        {
            if (time < 0f)
            {
                float charge = HomeMotion.Span(time, -UPGRADE_CHARGE, UPGRADE_CHARGE, Ease.InQuad);
                SetMotion(1f + 0.12f * charge, charge, 1f);
                return;
            }

            float settle = HomeMotion.Span(time, 0f, 0.45f, Ease.OutBack);
            SetMotion(Mathf.LerpUnclamped(1.3f, 1f, settle), 1f - HomeMotion.Span(time, 0f, 0.35f), HomeMotion.Span(time, 0f, UPGRADE_BURST));
        }

        /// <summary>The idle motion at <paramref name="time"/> seconds; the same time draws the same badge.</summary>
        internal void Tick(double time)
        {
            _time = time;
            Apply();
        }

        /// <summary>Picks up a crest that has finished drawing and fades it up; the badge's clock calls it each frame.</summary>
        internal void Advance(float deltaTime)
        {
            if (!_crestWanted || _crestShown >= 1f)
            {
                return;
            }

            if (_sprites == null)
            {
                if (!RankingCrests.TryGet(_tier, _size, out RankingCrests.Sprites sprites))
                {
                    return;
                }

                if (!sprites.HasCore)
                {
                    // Drawing failed: stay plain.
                    _crestWanted = false;
                    return;
                }

                ApplyCrest(sprites);
            }

            _crestShown = Mathf.Min(1f, _crestShown + deltaTime / CREST_FADE);
            Apply();
        }

        // ---------------------------------------------------------------- layout

        private void ApplyCrest(RankingCrests.Sprites sprites)
        {
            _sprites = sprites;
            int tier = sprites.Tier;
            Place(_rays, tier, RankingCrestArt.Layer.Rays, false);
            Place(_orbitBack, tier, RankingCrestArt.Layer.OrbitBack, false);
            Place(_sideRight, tier, RankingCrestArt.Layer.Side, false);
            Place(_sideLeft, tier, RankingCrestArt.Layer.Side, true);
            Place(_core, tier, RankingCrestArt.Layer.Core, false);
            Place(_orbitFront, tier, RankingCrestArt.Layer.OrbitFront, false);

            Color32 light = Color.Lerp(_color, Color.white, 0.6f);
            _heart.color = new Color32(light.r, light.g, light.b, 0);
            _heart.enabled = true;

            _orbits = RankingCrestArt.Orbits(tier);
            bool satellite = _orbits.Length > 0;
            _satelliteBack.enabled = satellite;
            _satelliteFront.enabled = satellite;

            _glintPoints = _moves ? RankingCrestArt.Glints(tier) : new RankingCrestArt.Point[0];
            if (_glints.Length < _glintPoints.Length)
            {
                var glints = new Image[_glintPoints.Length];
                for (int i = 0; i < glints.Length; i++)
                {
                    glints[i] = i < _glints.Length ? _glints[i] : Layer("Glint" + i, _glintLayer, RankingCrests.Sparkle(), _size * GLINT_SIZE);
                }

                _glints = glints;
            }

            for (int i = 0; i < _glints.Length; i++)
            {
                bool used = i < _glintPoints.Length;
                _glints[i].enabled = used;
                if (used)
                {
                    _glints[i].rectTransform.anchoredPosition = new Vector2(_glintPoints[i].X, _glintPoints[i].Y) * _size;
                }
            }
        }

        private void HideCrest()
        {
            _sprites = null;
            foreach (Image image in new[] { _rays, _orbitBack, _satelliteBack, _sideLeft, _sideRight, _core, _heart, _orbitFront, _satelliteFront })
            {
                image.enabled = false;
            }

            foreach (Image glint in _glints) glint.enabled = false;
            _glintPoints = new RankingCrestArt.Point[0];
            _orbits = new RankingCrestArt.Orbit[0];
        }

        /// <summary>Sizes a crest layer to its share of the badge and pins it at its turning point.</summary>
        private void Place(Image image, int tier, RankingCrestArt.Layer layer, bool mirror)
        {
            Sprite sprite = _sprites[layer];
            image.enabled = sprite != null;
            if (sprite == null)
            {
                return;
            }

            RankingCrestArt.Box box = RankingCrestArt.Bounds(tier, layer);
            image.sprite = sprite;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(box.Width, box.Height) * _size;
            rect.pivot = new Vector2((box.PivotX - box.X0) / box.Width, (box.PivotY - box.Y0) / box.Height);
            rect.anchoredPosition = new Vector2(mirror ? -box.PivotX : box.PivotX, box.PivotY) * _size;
            rect.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
        }

        // ---------------------------------------------------------------- motion

        /// <summary>Draws the badge from the idle clock, the upgrade's reveal and the crest's fade-in together.</summary>
        private void Apply()
        {
            _body.localScale = new Vector3(_scale, _scale, 1f);
            _flash.color = new Color(1f, 1f, 1f, _flashAmount * 0.85f);
            _flash.enabled = _flashAmount > 0f;

            bool crest = _sprites != null;
            float shown = crest ? _crestShown : 0f;
            _plain.alpha = 1f - shown;
            _crest.alpha = shown;
            ApplyRings();
            _sparks?.Apply(_reveal);
            if (!crest)
            {
                return;
            }

            float r = _reveal;
            float time = _moves ? (float)_time : 0f;
            int tier = _sprites.Tier;

            // Rays: fade in spinning, then turn slowly.
            float raysIn = HomeMotion.Span(r, 0.15f, 0.45f);
            float spin = (1f - EaseUtility.Evaluate(r, Ease.OutCubic)) * 120f;
            _rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(time * (tier >= RankingTiers.TOP ? 8f : 5f) % 360f) - spin);
            SetAlpha(_rays, raysIn);

            // Flank diamonds: slide out from behind the plaque.
            if (_sideRight.enabled)
            {
                float slide = EaseUtility.Evaluate(HomeMotion.Span(r, 0.08f, 0.5f), Ease.OutBack);
                float x = RankingCrestArt.Bounds(tier, RankingCrestArt.Layer.Side).PivotX - SIDE_TUCK * (1f - slide);
                _sideRight.rectTransform.anchoredPosition = new Vector2(x * _size, 0f);
                _sideLeft.rectTransform.anchoredPosition = new Vector2(-x * _size, 0f);
                float sidesIn = HomeMotion.Span(r, 0.06f, 0.15f);
                SetAlpha(_sideRight, sidesIn);
                SetAlpha(_sideLeft, sidesIn);
            }

            float orbitIn = HomeMotion.Span(r, 0.25f, 0.4f);
            SetAlpha(_orbitBack, orbitIn);
            SetAlpha(_orbitFront, orbitIn);
            ApplySatellite(time, orbitIn);

            // The heart breathes; it flares as the new tier lands.
            float flare = 1f - HomeMotion.Span(r, 0f, 0.6f);
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / 3f);
            SetAlpha(_heart, (_moves ? 0.12f + 0.12f * breathe : 0.16f) + 0.6f * flare * flare);

            float glintsIn = HomeMotion.Span(r, 0.55f, 0.3f);
            for (int i = 0; i < _glintPoints.Length; i++)
            {
                // Each twinkles briefly once per 2.4 s, at its own moment.
                float phase = Mathf.Repeat(time / 2.4f + i * 0.382f, 1f);
                float twinkle = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f));
                twinkle *= twinkle * twinkle;
                float grow = 0.45f + 0.55f * twinkle;
                _glints[i].rectTransform.localScale = new Vector3(grow, grow, 1f);
                _glints[i].color = new Color(1f, 1f, 1f, twinkle * 0.95f * glintsIn);
            }
        }

        private void ApplyRings()
        {
            bool revealing = _reveal < 1f;
            int count = !revealing ? 0 : _tier >= 5 ? 3 : _tier >= 3 ? 2 : 1;
            for (int i = 0; i < RINGS; i++)
            {
                float local = (_reveal - i * 0.14f) / 0.7f;
                bool flying = i < count && local > 0f && local < 1f;
                _rings[i].enabled = flying;
                if (!flying)
                {
                    continue;
                }

                float grow = 1f + local * (0.9f + 0.35f * i);
                _rings[i].rectTransform.localScale = new Vector3(grow, grow, 1f);
                Color32 color = _rings[i].color;
                color.a = (byte)Mathf.RoundToInt(255f * (1f - local) * (1f - local));
                _rings[i].color = color;
            }

            if (revealing && _sparks == null)
            {
                _sparks = new RankingSparks(Root, _size);
            }

            _sparks?.SetTier(_tier);
        }

        /// <summary>The satellite rides the first orbit, drawn behind the plaque on the far half and over it on the near half.</summary>
        private void ApplySatellite(float time, float alpha)
        {
            if (_orbits.Length == 0)
            {
                return;
            }

            // At rest it sits on the near half, low on the left.
            float angle = -2.3f + time * Mathf.PI * 2f / SATELLITE_PERIOD;
            RankingCrestArt.Point point = _orbits[0].At(angle);
            bool far = Mathf.Sin(angle) > 0f;
            Vector2 position = new Vector2(point.X, point.Y) * _size;
            _satelliteBack.rectTransform.anchoredPosition = position;
            _satelliteFront.rectTransform.anchoredPosition = position;
            Color tint = Color.Lerp(LibraryTheme.Colors.AllPerfectAt(Mathf.Repeat(time / SATELLITE_PERIOD, 1f)), Color.white, 0.5f);
            tint.a = alpha * (far ? 0.45f : 1f);
            _satelliteBack.color = tint;
            _satelliteFront.color = tint;
            _satelliteBack.enabled = far;
            _satelliteFront.enabled = !far;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static CanvasGroup Group(string name, Transform parent)
        {
            RectTransform rect = LibraryUi.CreateRect(name, parent);
            LibraryUi.Stretch(rect);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
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

    /// <summary>
    /// The sparks an upgrade throws off: more for a higher tier, flying out from the heart and
    /// fading. Built the first time a badge plays an upgrade.
    /// </summary>
    internal sealed class RankingSparks
    {
        private const int MAX = 30;

        private readonly Image[] _sparks = new Image[MAX];
        private readonly float _size;
        private int _count;
        private int _tier = -1;

        internal RankingSparks(Transform parent, float size)
        {
            _size = size;
            RectTransform layer = LibraryUi.CreateRect("Sparks", parent);
            for (int i = 0; i < MAX; i++)
            {
                Image spark = LibraryUi.CreateImage("Spark" + i, layer, Color.white);
                spark.sprite = RankingCrests.Sparkle();
                spark.raycastTarget = false;
                spark.rectTransform.sizeDelta = new Vector2(size * 0.12f, size * 0.12f);
                spark.enabled = false;
                _sparks[i] = spark;
            }
        }

        internal void SetTier(int tier)
        {
            if (tier == _tier)
            {
                return;
            }

            _tier = tier;
            _count = Mathf.Min(MAX, 6 + 4 * Mathf.Clamp(tier, 0, RankingTiers.TOP));
            Color32 light = Color.Lerp(RankingTiers.Color(tier), Color.white, 0.45f);
            for (int i = 0; i < MAX; i++)
            {
                _sparks[i].color = RankingTiers.IsPrismatic(tier) ? (Color)LibraryTheme.Colors.AllPerfectStops[i % 3] : (Color)light;
            }
        }

        /// <summary>Places the sparks <paramref name="reveal"/> of the way through the upgrade; at 1 they are gone.</summary>
        internal void Apply(float reveal)
        {
            bool flying = reveal < 1f;
            float travel = EaseUtility.Evaluate(HomeMotion.Span(reveal, 0f, 0.85f), Ease.OutCubic);
            float fade = 1f - HomeMotion.Span(reveal, 0.35f, 0.6f);
            for (int i = 0; i < MAX; i++)
            {
                Image spark = _sparks[i];
                bool shown = flying && i < _count;
                spark.enabled = shown;
                if (!shown)
                {
                    continue;
                }

                // Golden-angle spread with fixed per-spark speeds, so every upgrade looks the same.
                float angle = i * 2.39996f;
                float speed = 0.55f + 0.4f * Mathf.Repeat(i * 0.618034f, 1f);
                float distance = _size * (0.2f + speed * travel);
                spark.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                float scale = (1f - 0.6f * travel) * (0.7f + 0.3f * speed);
                spark.rectTransform.localScale = new Vector3(scale, scale, 1f);
                Color color = spark.color;
                color.a = fade;
                spark.color = color;
            }
        }
    }
}
