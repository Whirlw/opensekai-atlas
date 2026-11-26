using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The sky around the 个人信息 card's Ranking Score badge. 星尘 has twinkling stars and two
    /// orbits with their satellites; each tier above keeps everything below it and adds more:
    /// 新星 a pulse thrown off the badge and its brightest stars lit into sparkles, 流星 shooting
    /// stars, 银河 a third orbit and satellites trailing light, 星海 a slow drift of small stars
    /// and constellation lines, 天穹 turning beams of light and rising motes, 寰宇 the ALL PERFECT
    /// prism through all of it, a second pulse and nebulae drifting behind. All in the card's flat
    /// language: thin lines, glowing dots and soft light.
    ///
    /// Units are canvas units from the badge's center. <see cref="Tick"/> takes the home page's
    /// idle clock, so the same time always draws the same sky.
    /// </summary>
    internal sealed class HomeRankingSky
    {
        private const float BADGE_SIZE = 260f;
        private const int STARS = 9;
        private const int ORBITS = 3;
        private const int TRAIL = 3;
        private const int PULSES = 2;
        private const int METEORS = 3;
        private const int DRIFT = 24;
        private const int BEAMS = 24;
        private const int MOTES = 10;
        private const int NEBULAE = 3;
        private const float DRIFT_WIDTH = 820f;

        private static readonly Vector2[] _starPositions =
        {
            new Vector2(-250f, 210f), new Vector2(-120f, 290f), new Vector2(170f, 250f), new Vector2(280f, 120f),
            new Vector2(300f, -150f), new Vector2(150f, -280f), new Vector2(-200f, -240f), new Vector2(-300f, -40f),
            new Vector2(40f, 330f)
        };

        // Diameter, tilt, squash and seconds per turn (negative turns clockwise) of each orbit.
        private static readonly float[] _orbitDiameters = { 470f, 660f, 860f };
        private static readonly float[] _orbitTilts = { -14f, 10f, -4f };
        private static readonly float[] _orbitSquash = { 0.34f, 0.26f, 0.2f };
        private static readonly float[] _orbitPeriods = { 9f, -14f, 20f };

        /// <summary>Stars the constellation lines join, in pairs, from <see cref="_starPositions"/>.</summary>
        private static readonly int[] _links = { 0, 1, 1, 8, 8, 2, 2, 3, 6, 5 };

        private static readonly Vector2[] _meteorStarts = { new Vector2(330f, 300f), new Vector2(120f, 360f), new Vector2(420f, 120f) };
        private static readonly float[] _meteorPeriods = { 5.5f, 7f, 8.5f };

        private readonly Image[] _nebulae = new Image[NEBULAE];
        private readonly RectTransform _beamRoot;
        private readonly Image[] _beams = new Image[BEAMS];
        private readonly Image[] _drift = new Image[DRIFT];
        private readonly Vector3[] _driftSeeds = new Vector3[DRIFT];
        private readonly Image[] _lines = new Image[_links.Length / 2];
        private readonly Image[] _stars = new Image[STARS];
        private readonly Image[] _meteors = new Image[METEORS];
        private readonly RectTransform[] _orbits = new RectTransform[ORBITS];
        private readonly Image[] _orbitPaths = new Image[ORBITS];
        private readonly Image[] _satellites = new Image[ORBITS];
        private readonly Image[,] _trails = new Image[ORBITS, TRAIL];
        private readonly Image[] _motes = new Image[MOTES];
        private readonly Image[] _pulses = new Image[PULSES];
        private int _tier;
        private Color32 _accent;

        internal HomeRankingSky(RectTransform root)
        {
            for (int i = 0; i < NEBULAE; i++)
            {
                _nebulae[i] = Dot("Nebula" + i, root, LibraryShapes.Glow(), Vector2.zero, 720f);
            }

            _beamRoot = LibraryUi.CreateRect("Beams", root);
            for (int i = 0; i < BEAMS; i++)
            {
                RectTransform spoke = LibraryUi.CreateRect("Spoke" + i, _beamRoot);
                spoke.localEulerAngles = new Vector3(0f, 0f, i * 360f / BEAMS);
                Image beam = LibraryUi.CreateImage("Beam", spoke, Color.white);
                beam.sprite = HomeShapes.Beam();
                beam.raycastTarget = false;
                bool isLong = i % 2 == 0;
                LibraryWidgets.Place(beam.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f),
                    new Vector2(isLong ? 440f : 300f, isLong ? 7f : 5f));
                _beams[i] = beam;
            }

            for (int i = 0; i < DRIFT; i++)
            {
                // Fixed per-star seeds: x, y, and how near (bigger, faster).
                float near = Hash(i, 3);
                _driftSeeds[i] = new Vector3(Hash(i, 1) * DRIFT_WIDTH, Mathf.Lerp(-330f, 330f, Hash(i, 2)), near);
                _drift[i] = Dot("Drift" + i, root, LibraryShapes.Glow(), Vector2.zero, Mathf.Lerp(7f, 15f, near));
            }

            for (int i = 0; i < _lines.Length; i++)
            {
                Vector2 a = _starPositions[_links[i * 2]];
                Vector2 b = _starPositions[_links[i * 2 + 1]];
                Image line = LibraryUi.CreateImage("Link" + i, root, Color.white);
                line.raycastTarget = false;
                Vector2 delta = b - a;
                LibraryWidgets.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (a + b) * 0.5f,
                    new Vector2(delta.magnitude - 30f, 1.5f));
                line.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                _lines[i] = line;
            }

            for (int i = 0; i < STARS; i++)
            {
                _stars[i] = Dot("Star" + i, root, LibraryShapes.Glow(), _starPositions[i], i % 3 == 0 ? 26f : 16f);
            }

            for (int i = 0; i < METEORS; i++)
            {
                Image meteor = LibraryUi.CreateImage("Meteor" + i, root, Color.white);
                meteor.sprite = HomeShapes.Streak();
                meteor.raycastTarget = false;
                LibraryWidgets.Place(meteor.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.92f, 0.5f), _meteorStarts[i],
                    new Vector2(240f, 14f));
                _meteors[i] = meteor;
            }

            for (int i = 0; i < ORBITS; i++)
            {
                float diameter = _orbitDiameters[i];
                RectTransform orbit = LibraryUi.CreateRect("Orbit" + i, root);
                LibraryWidgets.Place(orbit, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));
                orbit.localEulerAngles = new Vector3(0f, 0f, _orbitTilts[i]);
                RectTransform squash = LibraryUi.CreateRect("Squash", orbit);
                LibraryUi.Stretch(squash);
                squash.localScale = new Vector3(1f, _orbitSquash[i], 1f);
                _orbitPaths[i] = LibraryWidgets.Shape("Path", squash, LibraryShapes.Ring(150f, 2f), Color.white);
                _orbitPaths[i].type = Image.Type.Simple;
                LibraryUi.Stretch(_orbitPaths[i].rectTransform);
                for (int k = 0; k < TRAIL; k++)
                {
                    _trails[i, k] = Dot("Trail" + k, orbit, LibraryShapes.Glow(), Vector2.zero, 30f * (1f - 0.2f * (k + 1)));
                }

                _satellites[i] = Dot("Satellite", orbit, LibraryShapes.Glow(), Vector2.zero, 30f);
                _orbits[i] = orbit;
            }

            for (int i = 0; i < MOTES; i++)
            {
                _motes[i] = Dot("Mote" + i, root, LibraryShapes.Glow(), Vector2.zero, Mathf.Lerp(8f, 15f, Hash(i, 7)));
            }

            for (int i = 0; i < PULSES; i++)
            {
                _pulses[i] = Dot("Pulse" + i, root, LibraryShapes.Hexagon(0.012f), Vector2.zero, BADGE_SIZE);
            }

            SetTier(0, LibraryTheme.Colors.DefaultAccent);
        }

        internal void SetTier(int tier, Color32 accent)
        {
            _tier = Mathf.Clamp(tier, 0, RankingTiers.TOP);
            _accent = accent;
            bool prism = RankingTiers.IsPrismatic(_tier);

            foreach (Image nebula in _nebulae) nebula.enabled = prism;
            _beamRoot.gameObject.SetActive(_tier >= 5);
            for (int i = 0; i < BEAMS; i++)
            {
                // 天穹 has every other spoke; 寰宇 all of them.
                _beams[i].enabled = _tier >= 5 && (prism || i % 2 == 0);
            }

            foreach (Image star in _drift) star.enabled = _tier >= 4;
            foreach (Image line in _lines) line.enabled = _tier >= 4;
            for (int i = 0; i < METEORS; i++)
            {
                _meteors[i].enabled = _tier >= 2 && i < (_tier >= 4 ? 3 : 2);
            }

            for (int i = 0; i < ORBITS; i++)
            {
                bool shown = i < (_tier >= 3 ? 3 : 2);
                _orbits[i].gameObject.SetActive(shown);
                for (int k = 0; k < TRAIL; k++) _trails[i, k].enabled = _tier >= 3;
            }

            foreach (Image mote in _motes) mote.enabled = _tier >= 5;
            for (int i = 0; i < PULSES; i++)
            {
                _pulses[i].enabled = _tier >= 1 && i < (_tier >= 5 ? 2 : 1);
                float hex = BADGE_SIZE * (RankingCrestArt.HasCrest(_tier) ? RankingCrestArt.HexScale(_tier) : 1f);
                _pulses[i].rectTransform.sizeDelta = new Vector2(hex, hex);
            }

            // 新星 and up light the brightest stars into sparkles.
            for (int i = 0; i < STARS; i++)
            {
                bool sparkle = _tier >= 1 && i % 3 == 0;
                _stars[i].sprite = sparkle ? RankingCrests.Sparkle() : LibraryShapes.Glow();
                float size = i % 3 == 0 ? (sparkle ? 40f : 26f) : 16f;
                _stars[i].rectTransform.sizeDelta = new Vector2(size, size);
            }

            ApplyColors();
        }

        internal void Tick(double time)
        {
            float t = (float)time;
            bool prism = RankingTiers.IsPrismatic(_tier);

            if (prism)
            {
                for (int i = 0; i < NEBULAE; i++)
                {
                    float phase = i * 2.1f;
                    _nebulae[i].rectTransform.anchoredPosition = new Vector2(
                        140f * Mathf.Cos(t / 17f + phase) + (i - 1) * 120f, 90f * Mathf.Sin(t / 13f + phase * 1.3f));
                }
            }

            if (_tier >= 5)
            {
                _beamRoot.localEulerAngles = new Vector3(0f, 0f, -(t * 3f % 360f));
                if (prism)
                {
                    for (int i = 0; i < BEAMS; i++)
                    {
                        Color color = LibraryTheme.Colors.AllPerfectAt(Mathf.PingPong(i * 2f / BEAMS + t / 12f, 1f));
                        color.a = i % 2 == 0 ? 0.24f : 0.15f;
                        _beams[i].color = color;
                    }
                }
            }

            if (_tier >= 4)
            {
                for (int i = 0; i < DRIFT; i++)
                {
                    Vector3 seed = _driftSeeds[i];
                    float speed = Mathf.Lerp(5f, 16f, seed.z);
                    float x = Mathf.Repeat(seed.x - t * speed, DRIFT_WIDTH) - DRIFT_WIDTH * 0.5f;
                    _drift[i].rectTransform.anchoredPosition = new Vector2(x, seed.y);
                    float twinkle = 0.5f + 0.5f * Mathf.Sin(t * (0.8f + seed.z) + i * 2.3f);
                    Color color = _drift[i].color;
                    color.a = (0.12f + 0.38f * twinkle) * Mathf.Lerp(0.6f, 1f, seed.z) * Edge(x);
                    _drift[i].color = color;
                }

                for (int i = 0; i < _lines.Length; i++)
                {
                    Color color = _lines[i].color;
                    color.a = 0.14f + 0.08f * Mathf.Sin(t * 0.7f + i * 1.3f);
                    _lines[i].color = color;
                }
            }

            for (int i = 0; i < STARS; i++)
            {
                float twinkle = 0.5f + 0.5f * Mathf.Sin((float)(time * (0.6d + i * 0.07d)) + i * 1.7f);
                Color star = _stars[i].color;
                star.a = 0.12f + 0.5f * twinkle * twinkle;
                _stars[i].color = star;
            }

            for (int i = 0; i < METEORS; i++)
            {
                if (!_meteors[i].enabled) continue;
                // Each crosses once per period and is gone the rest of it.
                float local = Mathf.Repeat(t + i * 2.3f, _meteorPeriods[i]) / 1.1f;
                bool flying = local < 1f;
                Color color = _meteors[i].color;
                color.a = flying ? Mathf.Sin(local * Mathf.PI) * 0.9f : 0f;
                _meteors[i].color = color;
                if (flying)
                {
                    Vector2 direction = new Vector2(-1f, -0.5f).normalized;
                    float travel = 1f - (1f - local) * (1f - local);
                    _meteors[i].rectTransform.anchoredPosition = _meteorStarts[i] + direction * (560f * travel);
                    _meteors[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
            }

            for (int i = 0; i < ORBITS; i++)
            {
                if (!_orbits[i].gameObject.activeSelf) continue;
                float period = _orbitPeriods[i];
                float turn = Mathf.Sign(period);
                float angle = (float)(time % Mathf.Abs(period) / Mathf.Abs(period)) * Mathf.PI * 2f * turn + i * 2.1f;
                PlaceOnOrbit(_satellites[i], i, angle, 1f);
                for (int k = 0; k < TRAIL; k++)
                {
                    PlaceOnOrbit(_trails[i, k], i, angle - turn * (k + 1) * 0.13f, 0.5f - 0.14f * k);
                }
            }

            if (_tier >= 5)
            {
                for (int i = 0; i < MOTES; i++)
                {
                    float rise = Mathf.Repeat(t / 7f + i * 0.1f + Hash(i, 9) * 0.3f, 1f);
                    float x = Mathf.Lerp(-260f, 260f, Hash(i, 8)) + 14f * Mathf.Sin(t * 0.9f + i);
                    _motes[i].rectTransform.anchoredPosition = new Vector2(x, Mathf.Lerp(-300f, 300f, rise));
                    Color color = _motes[i].color;
                    color.a = Mathf.Sin(rise * Mathf.PI) * 0.55f;
                    _motes[i].color = color;
                }
            }

            for (int i = 0; i < PULSES; i++)
            {
                if (!_pulses[i].enabled) continue;
                float p = Mathf.Repeat(t / 3.2f + i * 0.5f, 1f);
                float grow = 1f + 1.7f * EaseOut(p);
                _pulses[i].rectTransform.localScale = new Vector3(grow, grow, 1f);
                Color color = _pulses[i].color;
                color.a = 0.5f * (1f - p) * (1f - p);
                _pulses[i].color = color;
            }
        }

        private void ApplyColors()
        {
            bool prism = RankingTiers.IsPrismatic(_tier);
            Color32[] stops = LibraryTheme.Colors.AllPerfectStops;
            Color light = Color.Lerp(_accent, Color.white, 0.5f);

            for (int i = 0; i < NEBULAE; i++)
            {
                _nebulae[i].color = HomeTheme.Colors.WithAlpha(stops[i % stops.Length], 30);
            }

            for (int i = 0; i < BEAMS; i++)
            {
                _beams[i].color = HomeTheme.Colors.WithAlpha(_accent, (byte)(i % 2 == 0 ? 60 : 38));
            }

            for (int i = 0; i < DRIFT; i++)
            {
                _drift[i].color = prism ? Color.Lerp(stops[i % stops.Length], Color.white, 0.5f) : Color.Lerp(light, Color.white, 0.5f);
            }

            foreach (Image line in _lines) line.color = Color.Lerp(_accent, Color.white, 0.4f);
            for (int i = 0; i < STARS; i++)
            {
                _stars[i].color = prism && i % 3 == 0 ? Color.Lerp(stops[i / 3 % stops.Length], Color.white, 0.4f) : Color.white;
            }

            for (int i = 0; i < METEORS; i++)
            {
                _meteors[i].color = prism ? Color.Lerp(stops[i % stops.Length], Color.white, 0.4f) : Color.Lerp(_accent, Color.white, 0.6f);
            }

            for (int i = 0; i < ORBITS; i++)
            {
                Color32 tone = prism ? stops[i % stops.Length] : _accent;
                _orbitPaths[i].color = HomeTheme.Colors.WithAlpha(tone, (byte)(i == 0 ? 90 : i == 1 ? 50 : 36));
                Color satellite = Color.Lerp(tone, Color.white, 0.5f);
                _satellites[i].color = satellite;
                for (int k = 0; k < TRAIL; k++) _trails[i, k].color = satellite;
            }

            foreach (Image mote in _motes) mote.color = light;
            for (int i = 0; i < PULSES; i++)
            {
                _pulses[i].color = prism ? (Color)stops[i * 2 % stops.Length] : (Color)_accent;
            }
        }

        /// <summary>Puts a dot on orbit <paramref name="orbit"/>, dimmed on the far half, where it passes behind the badge.</summary>
        private void PlaceOnOrbit(Image dot, int orbit, float angle, float strength)
        {
            float half = _orbitDiameters[orbit] * 0.5f;
            dot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * half, Mathf.Sin(angle) * half * _orbitSquash[orbit]);
            Color color = dot.color;
            color.a = (Mathf.Sin(angle) > 0f ? 0.25f : 0.9f) * strength;
            dot.color = color;
        }

        /// <summary>Drifting stars fade out at the ends of their lane instead of popping.</summary>
        private static float Edge(float x) => Mathf.Clamp01((DRIFT_WIDTH * 0.5f - Mathf.Abs(x)) / 80f);

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        private static float Hash(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)index * 374761393u + (uint)salt * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static Image Dot(string name, Transform parent, Sprite sprite, Vector2 position, float size)
        {
            Image image = LibraryUi.CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;
            LibraryWidgets.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            return image;
        }
    }
}
