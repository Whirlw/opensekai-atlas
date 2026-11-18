using UnityEngine;

namespace Sekai.Live
{
    public sealed class NextRushConnectorSegment
    {
        public int Kind { get; }
        public int Ease { get; }
        public float StartAlpha { get; }
        public float EndAlpha { get; }
        internal bool IsGuide => Kind >= 101 && Kind <= 108;

        public NextRushConnectorSegment(int kind, int ease, float startAlpha, float endAlpha)
        {
            Kind = kind;
            Ease = ease;
            // The upstream renderer forces authored alpha to 1 for playable connectors.
            StartAlpha = IsGuide ? startAlpha : 1f;
            EndAlpha = IsGuide ? endAlpha : 1f;
        }

        public static float Interpolate(int ease, float t)
        {
            switch (ease)
            {
                case 2: return t * t;
                case 3: return t * (2 - t);
                case 4: return t < 0.5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);
                case 5: return t < 0.5f ? 2 * t * (1 - t) : 0.5f + 2 * (t - 0.5f) * (t - 0.5f);
                default: return t;
            }
        }

        internal Color GuideColor()
        {
            switch (Kind)
            {
                case 102: return new Color(1f, 0.25f, 0.36f);
                case 103: return new Color(0.22f, 1f, 0.60f);
                case 104: return new Color(0.25f, 0.57f, 1f);
                case 105: return new Color(1f, 0.88f, 0.25f);
                case 106: return new Color(0.75f, 0.42f, 1f);
                case 107: return new Color(0.26f, 0.95f, 1f);
                case 108: return new Color(0.16f, 0.16f, 0.16f);
                default: return Color.white;
            }
        }
    }
}
