using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// How each Ranking Score tier looks. The ids are <see cref="OpenSekai.Shared.Playback.RankingRules.Tier"/>'s
    /// (0 to 4); the score each tier starts at belongs to the backend, so it is not repeated here.
    /// An id past the table reads as the top tier.
    /// </summary>
    internal static class RankingTiers
    {
        private static readonly string[] _names = { "星尘", "新星", "流星", "银河", "天穹" };
        private static readonly string[] _latinNames = { "STARDUST", "NOVA", "METEOR", "GALAXY", "ZENITH" };

        private static readonly Color32[] _colors =
        {
            new Color32(150, 163, 186, 255),
            new Color32(110, 214, 192, 255),
            new Color32(122, 182, 255, 255),
            new Color32(182, 146, 255, 255),
            new Color32(255, 198, 112, 255)
        };

        internal static int Count => _names.Length;

        internal static string Name(int tier) => _names[Clamp(tier)];

        internal static string LatinName(int tier) => _latinNames[Clamp(tier)];

        internal static Color32 Color(int tier) => _colors[Clamp(tier)];

        private static int Clamp(int tier) => Mathf.Clamp(tier, 0, _names.Length - 1);
    }

    /// <summary>
    /// The Ranking Score badge: a hexagon in the tier's color with one diamond per tier. Its
    /// outline is deliberately unlike the rectangular 段位 plates, so the two read apart when shown
    /// side by side. The owner drives the upgrade through <see cref="SetMotion"/>.
    /// </summary>
    internal sealed class RankingBadge
    {
        private const float RING_STROKE = 0.045f;
        private const float INNER_STROKE = 0.02f;
        private const float INNER_SCALE = 0.8f;
        // Five diamonds (the top tier) span about 0.56 of the badge, inside the inner outline.
        private const float PIP_SIZE = 0.065f;
        private const float PIP_GAP = 0.03f;

        private readonly RectTransform _body;
        private readonly Image _glow;
        private readonly Image _fill;
        private readonly Image _ring;
        private readonly Image _inner;
        private readonly Image _flash;
        private readonly Image _burst;
        private readonly Image[] _pips = new Image[RankingTiers.Count];
        private readonly float _size;
        private int _tier = -1;

        internal RankingBadge(Transform parent, float size)
        {
            _size = size;
            Root = LibraryUi.CreateRect("RankingBadge", parent);
            Root.sizeDelta = new Vector2(size, size);

            _glow = Layer("Glow", Root, LibraryShapes.Glow(), size * 1.9f);
            _burst = Layer("Burst", Root, LibraryShapes.Hexagon(RING_STROKE * 0.6f), size);
            _body = LibraryUi.CreateRect("Body", Root);
            _body.sizeDelta = new Vector2(size, size);
            _fill = Layer("Fill", _body, LibraryShapes.Hexagon(), size);
            _inner = Layer("Inner", _body, LibraryShapes.Hexagon(INNER_STROKE / INNER_SCALE), size * INNER_SCALE);
            _ring = Layer("Ring", _body, LibraryShapes.Hexagon(RING_STROKE), size);

            float pip = size * PIP_SIZE;
            for (int i = 0; i < _pips.Length; i++)
            {
                _pips[i] = LibraryWidgets.Panel("Pip" + i, _body, pip * 0.22f, Color.white);
                _pips[i].rectTransform.sizeDelta = new Vector2(pip, pip);
                _pips[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            _flash = Layer("Flash", _body, LibraryShapes.Hexagon(), size);
            SetMotion(1f, 0f, 0f);
        }

        internal RectTransform Root { get; }

        internal int Tier => _tier;

        internal void SetTier(int tier)
        {
            if (tier == _tier)
            {
                return;
            }

            _tier = tier;
            Color32 color = RankingTiers.Color(tier);
            _ring.color = color;
            _inner.color = new Color32(color.r, color.g, color.b, 90);
            _fill.color = Color.Lerp(LibraryTheme.Colors.Surface, color, 0.22f);
            _glow.color = new Color32(color.r, color.g, color.b, 70);
            _burst.color = color;

            // One diamond per tier, in a row through the middle; the top tier fills all five.
            int count = Mathf.Clamp(tier + 1, 1, _pips.Length);
            float pip = _size * PIP_SIZE;
            float step = pip * 1.414f + _size * PIP_GAP;
            float left = -(count - 1) * step * 0.5f;
            for (int i = 0; i < _pips.Length; i++)
            {
                bool shown = i < count;
                _pips[i].gameObject.SetActive(shown);
                if (shown)
                {
                    _pips[i].color = color;
                    _pips[i].rectTransform.anchoredPosition = new Vector2(left + i * step, 0f);
                }
            }
        }

        /// <param name="scale">Size of the badge itself.</param>
        /// <param name="flash">0 to 1: how far the face has turned white.</param>
        /// <param name="burst">0 to 1: how far the outline thrown off by an upgrade has flown; 0 hides it.</param>
        internal void SetMotion(float scale, float flash, float burst)
        {
            _body.localScale = new Vector3(scale, scale, 1f);
            _flash.color = new Color(1f, 1f, 1f, flash * 0.85f);
            _flash.enabled = flash > 0f;
            bool flying = burst > 0f && burst < 1f;
            _burst.enabled = flying;
            if (flying)
            {
                float grow = 1f + burst * 0.9f;
                _burst.rectTransform.localScale = new Vector3(grow, grow, 1f);
                Color32 color = _burst.color;
                color.a = (byte)Mathf.RoundToInt(255f * (1f - burst) * (1f - burst));
                _burst.color = color;
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
