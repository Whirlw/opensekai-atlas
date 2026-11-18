using System;
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
        public bool ThroughJudgeLine { get; }
        internal const double ThroughJudgeLineDespawnDelay = 5;
        internal bool IsGuide => Kind >= 101 && Kind <= 108;
        internal bool IsCritical => Kind == 2 || Kind >= 21 && Kind <= 28;
        internal bool HasColor => Kind >= 11 && Kind <= 28;
        private readonly NoteBase _styleHead;

        public NextRushConnectorSegment(int kind, int ease, float startAlpha, float endAlpha, NoteBase styleHead = null,
            bool throughJudgeLine = false)
        {
            Kind = kind;
            Ease = ease;
            // The upstream renderer forces authored alpha to 1 for playable connectors.
            StartAlpha = IsGuide ? startAlpha : 1f;
            EndAlpha = IsGuide ? endAlpha : 1f;
            _styleHead = styleHead;
            ThroughJudgeLine = throughJudgeLine;
        }

        public static float Interpolate(int ease, float t) =>
            ease == 0 || ease == 38 ? 0f : (float)NextRushScrollTimeline.EaseValue(ease, t);

        internal bool IsHidden(double time, bool fallback) =>
            _styleHead?.NativeScroll?.SampleFrame(time).Hidden ?? fallback;

        internal static bool TryClip(NextRushConnectorSegment segment, double now, double startTime, double endTime,
            ref double start, ref double end, out double first, out double last)
        {
            bool through = segment?.ThroughJudgeLine == true;
            first = 0;
            last = 1;
            if (now >= endTime + (through ? ThroughJudgeLineDespawnDelay : 0)) return false;

            double limit = through ? LiveConfig.NativeKeyProgressEnd : 1;
            if (!through && now > startTime)
            {
                first = (now - startTime) / (endTime - startTime);
                start = 1;
            }
            double extent = end - start;
            if (extent == 0) return start > 0 && start <= limit;

            double top = -start / extent, bottom = (limit - start) / extent;
            double from = Math.Max(0, Math.Min(top, bottom)), to = Math.Min(1, Math.Max(top, bottom));
            if (to <= from) return false;
            last = first + (1 - first) * to;
            first += (1 - first) * from;
            start = Math.Clamp(start + extent * from, 0, limit);
            end = Math.Clamp(end - extent * (1 - to), 0, limit);
            return true;
        }

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
