using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>The light that breaks out from behind a medal as it is unlocked: a flare, two rings and a spray of diamonds.</summary>
    internal sealed class AchievementBurst
    {
        /// <summary>Seconds until nothing of it is left.</summary>
        internal const float LENGTH = 0.8f;

        private const int SPARKS = 14;

        private readonly float _size;
        private readonly Image _flare;
        private readonly Image[] _rings = new Image[2];
        private readonly Image[] _sparks = new Image[SPARKS];
        private Color32 _tint;

        /// <param name="size">The medal's diameter.</param>
        internal AchievementBurst(Transform parent, string name, float size)
        {
            _size = size;
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(size, size);
            _flare = LibraryUi.CreateImage("Flare", Root, Color.white);
            _flare.sprite = LibraryShapes.Glow();
            _flare.raycastTarget = false;
            Vector2 center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < _rings.Length; i++)
            {
                _rings[i] = LibraryWidgets.Shape("Ring" + i, Root, LibraryShapes.Ring(size * 0.5f, i == 0 ? 4f : 2f), Color.white);
                LibraryWidgets.Place(_rings[i].rectTransform, center, center, Vector2.zero, new Vector2(size, size));
            }

            for (int i = 0; i < SPARKS; i++)
            {
                _sparks[i] = LibraryWidgets.Panel("Spark" + i, Root, 1.5f, Color.white);
                float side = 7f + i * 5 % 7;
                LibraryWidgets.Place(_sparks[i].rectTransform, center, center, Vector2.zero, new Vector2(side, side));
                _sparks[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }

            Root.gameObject.SetActive(false);
        }

        internal RectTransform Root { get; }

        internal void Bind(Color32 tint) => _tint = tint;

        /// <param name="time">Seconds since it broke out; hidden before 0 and after <see cref="LENGTH"/>.</param>
        internal void Apply(float time)
        {
            bool shown = time >= 0f && time < LENGTH;
            if (Root.gameObject.activeSelf != shown) Root.gameObject.SetActive(shown);
            if (!shown) return;

            float flare = 1f - HomeMotion.Span(time, 0f, 0.5f, Ease.OutQuad);
            float spread = _size * Mathf.Lerp(1.6f, 3f, HomeMotion.Span(time, 0f, 0.5f, Ease.OutCubic));
            _flare.rectTransform.sizeDelta = new Vector2(spread, spread);
            _flare.color = HomeTheme.Colors.WithAlpha(_tint, (byte)Mathf.RoundToInt(170f * flare));
            for (int i = 0; i < _rings.Length; i++)
            {
                float ring = HomeMotion.Span(time, i * 0.09f, 0.55f, Ease.OutCubic);
                float scale = Mathf.Lerp(0.85f, i == 0 ? 1.75f : 2.3f, ring);
                _rings[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                _rings[i].color = HomeTheme.Colors.WithAlpha(Color.Lerp(_tint, Color.white, 0.4f), (byte)Mathf.RoundToInt(220f * (1f - ring)));
            }

            for (int i = 0; i < SPARKS; i++)
            {
                // Evenly round, nudged off the beat so the spray does not read as a ring.
                float angle = (i * 360f / SPARKS + i * 37 % 17) * Mathf.Deg2Rad;
                float reach = _size * (0.95f + i * 11 % 8 * 0.09f);
                float way = HomeMotion.Span(time, 0.02f, 0.72f, Ease.OutCubic);
                float distance = Mathf.Lerp(_size * 0.42f, reach, way);
                _sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                float left = 1f - HomeMotion.Span(time, 0.25f, 0.5f, Ease.InQuad);
                _sparks[i].rectTransform.localScale = new Vector3(left, left, 1f);
                _sparks[i].color = Color.Lerp(_tint, Color.white, i % 3 * 0.35f);
            }
        }
    }
}
