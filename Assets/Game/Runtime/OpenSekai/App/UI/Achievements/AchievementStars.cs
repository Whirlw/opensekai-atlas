using System.Collections.Generic;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The levels of an achievement as stars under its medal: lit for the unlocked ones, then one for
    /// the next level, grey while it is under way and blinking once it can be unlocked. The star of
    /// the level on show is the large one. Five fit side by side; a sixth starts a new strip, and the
    /// strips slide with the levels.
    /// </summary>
    internal sealed class AchievementStars
    {
        internal const int PER_STRIP = 5;
        internal const float HEIGHT = 34f;

        private const float PITCH = 23f;
        private const float SIZE = 18f;
        private const float SELECTED = 1.5f;

        /// <summary>Room at the strip's ends for a large star; less than the gap to the next strip's first.</summary>
        private const float EDGE = 2f;

        private static readonly Color32 _dark = new Color32(255, 255, 255, 52);

        private readonly RectTransform _content;
        private readonly List<Image> _stars = new List<Image>();
        private Color32 _tint;
        private int _lit;
        private bool _nextReached;

        internal AchievementStars(Transform parent, string name)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(PER_STRIP * PITCH + EDGE * 2f, HEIGHT);
            Root.gameObject.AddComponent<RectMask2D>();
            _content = LibraryUi.CreateRect("Strips", Root);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_content, center, center, Vector2.zero, Root.sizeDelta);
        }

        internal RectTransform Root { get; }

        /// <summary>How many stars there are to see.</summary>
        internal int Count { get; private set; }

        /// <param name="lit">How many levels are unlocked.</param>
        /// <param name="nextReached">The level after them can be unlocked.</param>
        internal void Bind(Color32 tint, int lit, bool nextReached)
        {
            _tint = Color.Lerp(tint, Color.white, 0.2f);
            _lit = lit;
            _nextReached = nextReached;
        }

        /// <param name="position">The level on show, from 0; between two while they slide.</param>
        /// <param name="count">How many stars show; a fraction is the last one growing in.</param>
        /// <param name="blink">0 to 1: how bright a star that can be unlocked is.</param>
        /// <param name="popped">A star that is popping, -1 for none.</param>
        /// <param name="pop">How much larger it is for now.</param>
        internal void Apply(float position, float count, float blink, int popped = -1, float pop = 0f)
        {
            Count = Mathf.CeilToInt(count);
            while (_stars.Count < Count) _stars.Add(Create(_stars.Count));
            for (int i = 0; i < _stars.Count; i++)
            {
                Image star = _stars[i];
                bool used = i < Count;
                star.gameObject.SetActive(used);
                if (!used) continue;
                int strip = i / PER_STRIP;
                float inStrip = Mathf.Clamp(count - strip * PER_STRIP, 1f, PER_STRIP);
                float x = strip * PER_STRIP * PITCH + (i % PER_STRIP - (inStrip - 1f) * 0.5f) * PITCH;
                float selected = Mathf.Clamp01(1f - Mathf.Abs(position - i));
                float scale = Mathf.Lerp(1f, SELECTED, selected) * Mathf.Clamp01(count - i);
                if (i == popped) scale *= 1f + pop;
                star.rectTransform.anchoredPosition = new Vector2(x, 0f);
                star.rectTransform.localScale = new Vector3(scale, scale, 1f);
                star.color = i < _lit ? _tint : i == _lit && _nextReached ? Color.Lerp(_dark, _tint, blink) : _dark;
            }

            int last = Mathf.Max(0, Count - 1);
            float clamped = Mathf.Clamp(position, 0f, last);
            int low = Mathf.FloorToInt(clamped);
            float strips = Mathf.Lerp(low / PER_STRIP, Mathf.Min(low + 1, last) / PER_STRIP, clamped - low);
            _content.anchoredPosition = new Vector2(-strips * PER_STRIP * PITCH, 0f);
        }

        private Image Create(int index)
        {
            Image star = LibraryWidgets.Icon(_content, LibraryIcon.Star, Color.white, SIZE);
            star.name = "Star" + index;
            return star;
        }
    }
}
