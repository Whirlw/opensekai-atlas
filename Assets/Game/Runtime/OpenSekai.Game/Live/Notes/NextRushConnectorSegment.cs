using OpenSekai.Charts;
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
        internal bool IsCritical => Kind == 2 || Kind >= 21 && Kind <= 28;
        internal bool HasColor => Kind >= 11 && Kind <= 28;
        private readonly NoteBase _styleHead;

        public NextRushConnectorSegment(int kind, int ease, float startAlpha, float endAlpha, NoteBase styleHead = null)
        {
            Kind = kind;
            Ease = ease;
            // The upstream renderer forces authored alpha to 1 for playable connectors.
            StartAlpha = IsGuide ? startAlpha : 1f;
            EndAlpha = IsGuide ? endAlpha : 1f;
            _styleHead = styleHead;
        }

        public static float Interpolate(int ease, float t) =>
            ease == 0 || ease == 38 ? 0f : (float)NextRushScrollTimeline.EaseValue(ease, t);

        internal bool IsHidden(double time, bool fallback) =>
            _styleHead?.NativeScroll?.SampleFrame(time).Hidden ?? fallback;

        internal Color StyleColor()
        {
            switch (Kind % 10)
            {
                case 2: return new Color(1f, 0.25f, 0.36f);
                case 3: return new Color(0.22f, 1f, 0.60f);
                case 4: return new Color(0.25f, 0.57f, 1f);
                case 5: return new Color(1f, 0.88f, 0.25f);
                case 6: return new Color(0.75f, 0.42f, 1f);
                case 7: return new Color(0.26f, 0.95f, 1f);
                case 8: return new Color(0.16f, 0.16f, 0.16f);
                default: return Color.white;
            }
        }
    }
}
