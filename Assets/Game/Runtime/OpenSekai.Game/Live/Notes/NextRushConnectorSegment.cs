using System;
using OpenSekai.Charts;
using UnityEngine;

namespace Sekai.Live
{
    public enum NextRushConnectorLayer
    {
        Top,
        Bottom,
        Under,
        Over
    }

    public sealed class NextRushConnectorSegment
    {
        public int Kind { get; }
        public int Ease { get; }
        public float StartAlpha { get; }
        public float EndAlpha { get; }
        public bool ThroughJudgeLine { get; }
        public NextRushConnectorLayer Layer { get; }
        public bool FullScreen { get; }
        internal const double ThroughJudgeLineDespawnDelay = 5;
        internal bool IsGuide => Kind >= 101 && Kind <= 108;
        internal bool IsCritical => Kind == 2 || Kind >= 21 && Kind <= 28;
        internal bool HasColor => Kind >= 11 && Kind <= 28;
        private readonly NoteBase _styleHead;
        private readonly float _easeFrom;
        private readonly float _easeTo;
        private readonly float _easedFrom;
        private readonly float _easedExtent;

        public NextRushConnectorSegment(int kind, int ease, float startAlpha, float endAlpha, NoteBase styleHead = null,
            bool throughJudgeLine = false, NextRushConnectorLayer layer = NextRushConnectorLayer.Top, bool fullScreen = false,
            float easeFrom = 0f, float easeTo = 1f)
        {
            Kind = kind;
            Ease = ease;
            // The upstream renderer forces authored alpha to 1 for playable connectors.
            StartAlpha = IsGuide ? startAlpha : 1f;
            EndAlpha = IsGuide ? endAlpha : 1f;
            _styleHead = styleHead;
            ThroughJudgeLine = throughJudgeLine;
            Layer = layer;
            FullScreen = fullScreen;
            _easeFrom = easeFrom;
            _easeTo = easeTo;
            _easedFrom = EaseEndpoint(ease, easeFrom);
            _easedExtent = EaseEndpoint(ease, easeTo) - _easedFrom;
        }

        public static float Interpolate(int ease, float t) =>
            ease == 0 || ease == 38 ? 0f : (float)NextRushScrollTimeline.EaseValue(ease, t);

        private static float EaseEndpoint(int ease, float value) =>
            value <= 0f ? 0f : value >= 1f ? 1f : Interpolate(ease, value);

        internal float Interpolate(float fraction)
        {
            if (_easeFrom == 0f && _easeTo == 1f) return Interpolate(Ease, fraction);
            if (Ease == 0 || Ease == 38) return 0f;
            if (_easedExtent == 0f) return fraction;
            if (fraction <= 0f || fraction >= 1f) return fraction;
            return (Interpolate(Ease, Mathf.Lerp(_easeFrom, _easeTo, fraction)) - _easedFrom) / _easedExtent;
        }

        internal float StepBoundary => Ease == 40 && _easedExtent != 0f
            ? (0.5f - _easeFrom) / (_easeTo - _easeFrom) : float.NaN;

        internal bool IsHidden(double time, bool fallback) =>
            _styleHead?.NativeScroll?.SampleFrame(time).Hidden ?? fallback;

        internal bool TryFullScreenAlpha(double now, double startTime, double endTime, out float alpha)
        {
            alpha = 0;
            if (now < startTime || now >= endTime) return false;
            alpha = Mathf.Lerp(StartAlpha, EndAlpha, (float)((now - startTime) / (endTime - startTime)));
            return alpha > 0;
        }

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

        // The NextRUSH+ v3 skin uses solid guide swatches for the full-screen presentation.
        internal Color FullScreenGuideColor() => Kind switch
        {
            101 => new Color32(237, 237, 237, 255),
            102 => new Color32(214, 115, 123, 255),
            103 => new Color32(99, 255, 172, 252),
            104 => new Color32(115, 123, 214, 255),
            105 => new Color32(255, 205, 53, 252),
            106 => new Color32(214, 115, 205, 255),
            107 => new Color32(115, 172, 214, 255),
            108 => new Color32(0, 0, 0, 255),
            _ => Color.white
        };
    }
}
