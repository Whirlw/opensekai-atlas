using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The page behind the splash and the home cards: the library's dark ground, three slow aurora
    /// lights in the brand's colors and a drift of faint motes rising through them. It sits in a
    /// canvas of its own, so the per-frame drift never rebuilds the cards' batches.
    /// </summary>
    internal sealed class HomeBackdrop
    {
        private const int MOTE_COUNT = 26;

        private readonly RectTransform _root;
        private readonly Image[] _lights = new Image[3];
        private readonly Light[] _lightSettings =
        {
            // anchor, size, drift (canvas units), integer cycles per idle period, base alpha
            new Light(new Vector2(0.16f, 0.22f), 1700f, new Vector2(150f, 90f), 5, 3, 0.2f),
            new Light(new Vector2(0.62f, 0.7f), 1500f, new Vector2(190f, 110f), 4, 6, 0.24f),
            new Light(new Vector2(0.95f, 0.95f), 1250f, new Vector2(120f, 140f), 7, 5, 0.15f)
        };

        private readonly Image[] _motes = new Image[MOTE_COUNT];
        private readonly Mote[] _moteSettings = new Mote[MOTE_COUNT];
        private float _intensity = 1f;
        private float _accentMix;
        private Color _accent = Color.clear;

        internal HomeBackdrop(Transform parent)
        {
            _root = LibraryUi.CreateRect("Backdrop", parent);
            LibraryUi.Stretch(_root);
            _root.gameObject.AddComponent<Canvas>();

            Image ground = LibraryUi.CreateImage("Ground", _root, LibraryTheme.Colors.Ground);
            ground.raycastTarget = false;
            LibraryUi.Stretch(ground.rectTransform);

            Color32[] colors = { HomeTheme.Colors.Periwinkle, HomeTheme.Colors.Violet, HomeTheme.Colors.Rose };
            for (int i = 0; i < _lights.Length; i++)
            {
                Image light = LibraryUi.CreateImage("Aurora" + i, _root, colors[i]);
                light.sprite = LibraryShapes.Glow();
                light.raycastTarget = false;
                Light settings = _lightSettings[i];
                LibraryWidgets.Place(light.rectTransform, settings.Anchor, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(settings.Size, settings.Size));
                _lights[i] = light;
            }

            // A fixed seed keeps the motes identical between runs, so screenshots stay comparable.
            var random = new System.Random(1117);
            for (int i = 0; i < MOTE_COUNT; i++)
            {
                var mote = new Mote
                {
                    X = (float)random.NextDouble(),
                    Y = (float)random.NextDouble(),
                    Speed = 8f + (float)random.NextDouble() * 16f,
                    Size = 3f + (float)random.NextDouble() * 6f,
                    Alpha = 0.12f + (float)random.NextDouble() * 0.3f,
                    Twinkle = 3 + random.Next(9),
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f,
                    Sway = 6f + (float)random.NextDouble() * 18f
                };
                _moteSettings[i] = mote;
                Image image = LibraryUi.CreateImage("Mote" + i, _root, i % 3 == 0 ? HomeTheme.Colors.Rose : Color.white);
                image.sprite = LibraryShapes.Glow();
                image.raycastTarget = false;
                LibraryWidgets.Place(image.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(mote.Size * 3f, mote.Size * 3f));
                _motes[i] = image;
            }

            Tick(0d);
        }

        /// <summary>0 shows only the ground; the splash raises it as the stage lights up.</summary>
        internal void SetIntensity(float intensity)
        {
            _intensity = Mathf.Clamp01(intensity);
        }

        /// <summary>Tints the middle light toward a jacket's glow; 0 keeps the brand violet.</summary>
        internal void SetAccent(Color accent, float mix)
        {
            _accent = accent;
            _accentMix = Mathf.Clamp01(mix);
        }

        /// <param name="time">Seconds on the idle clock; continuous, never wrapped.</param>
        internal void Tick(double time)
        {
            float phase = (float)(time / HomeTheme.Motion.IDLE_PERIOD * Mathf.PI * 2d);
            for (int i = 0; i < _lights.Length; i++)
            {
                Light settings = _lightSettings[i];
                RectTransform rect = _lights[i].rectTransform;
                rect.anchoredPosition = new Vector2(
                    Mathf.Sin(phase * settings.CyclesX + i) * settings.Drift.x,
                    Mathf.Sin(phase * settings.CyclesY + i * 2.1f) * settings.Drift.y);
                float breathe = 1f + 0.06f * Mathf.Sin(phase * (settings.CyclesX + settings.CyclesY) + i);
                rect.localScale = new Vector3(breathe, breathe, 1f);

                Color color = i == 1 ? Color.Lerp(HomeTheme.Colors.Violet, _accent, _accentMix) : (Color)_lights[i].color;
                color.a = settings.Alpha * _intensity;
                _lights[i].color = color;
            }

            Rect area = _root.rect;
            float height = area.height + 60f;
            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _moteSettings[i];
                float rise = (float)((mote.Y * height + time * mote.Speed) % height);
                float x = mote.X * area.width + Mathf.Sin(phase * mote.Twinkle * 0.5f + mote.Phase) * mote.Sway;
                _motes[i].rectTransform.anchoredPosition = new Vector2(x, rise - 30f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(phase * mote.Twinkle * 4f + mote.Phase);
                // Motes fade in from the floor and out toward the top.
                float edge = Mathf.Clamp01(rise / 140f) * Mathf.Clamp01((height - rise) / 220f);
                Color color = _motes[i].color;
                color.a = mote.Alpha * twinkle * edge * _intensity;
                _motes[i].color = color;
            }
        }

        private readonly struct Light
        {
            internal readonly Vector2 Anchor;
            internal readonly float Size;
            internal readonly Vector2 Drift;
            internal readonly int CyclesX;
            internal readonly int CyclesY;
            internal readonly float Alpha;

            internal Light(Vector2 anchor, float size, Vector2 drift, int cyclesX, int cyclesY, float alpha)
            {
                Anchor = anchor;
                Size = size;
                Drift = drift;
                CyclesX = cyclesX;
                CyclesY = cyclesY;
                Alpha = alpha;
            }
        }

        private struct Mote
        {
            internal float X;
            internal float Y;
            internal float Speed;
            internal float Size;
            internal float Alpha;
            internal int Twinkle;
            internal float Phase;
            internal float Sway;
        }
    }
}
