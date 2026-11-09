using System;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Shared.Scoring
{
    /// <summary>
    /// The lane and swipe tolerances the judge uses, in one immutable place.
    ///
    /// The numbers come from <see cref="LiveBundleBuildData"/> (the original game's configuration),
    /// not from code, so this type only decides how they are applied:
    /// <list type="bullet">
    /// <item>lane offsets widen a note's judge range: start - offset .. end + offset;</item>
    /// <item>the flick distance is stored in inches and must be converted to pixels with the screen DPI.</item>
    /// </list>
    /// </summary>
    public readonly struct JudgeTuning
    {
        /// <summary>The shipped values, kept here so a test can fail when the asset drifts.</summary>
        public const float ShippedNormalLaneOffset = 1.25f;
        public const float ShippedLongLaneOffset = 1.5f;
        public const float ShippedFlickLaneOffset = 1.5f;
        // 5 inches per second expressed per 1/60 s frame, which is what the judge compares against.
        public const float ShippedFlickDistanceInches = 5f / 60f;

        public JudgeTuning(float normalLaneOffset, float longLaneOffset, float flickLaneOffset, float flickDistanceInches)
        {
            NormalLaneOffset = normalLaneOffset;
            LongLaneOffset = longLaneOffset;
            FlickLaneOffset = flickLaneOffset;
            FlickDistanceInches = flickDistanceInches;
        }

        public float NormalLaneOffset { get; }
        public float LongLaneOffset { get; }
        public float FlickLaneOffset { get; }

        /// <summary>Minimum swipe length per 1/60 s frame, in inches.</summary>
        public float FlickDistanceInches { get; }

        public static JudgeTuning Shipped { get; } = new JudgeTuning(
            ShippedNormalLaneOffset, ShippedLongLaneOffset, ShippedFlickLaneOffset, ShippedFlickDistanceInches);

        public static JudgeTuning From(LiveBundleBuildData data)
        {
            if (data == null)
            {
                return default;
            }

            return new JudgeTuning(
                data.NormalNoteOffsetX, data.LongNoteOffsetX, data.FlickNoteOffsetX, data.FlickDistance);
        }

        /// <summary>Lane offset for a note category; unknown categories fall back to the normal one.</summary>
        public float LaneOffsetFor(NoteCategory category)
        {
            return category switch
            {
                NoteCategory.Flick or NoteCategory.FrictionFlick => FlickLaneOffset,
                NoteCategory.Long or NoteCategory.FrictionLong or NoteCategory.FrictionHideLong => LongLaneOffset,
                _ => NormalLaneOffset
            };
        }

        /// <summary>Widened judge range for a note that occupies <paramref name="laneStart"/>..<paramref name="laneEnd"/>.</summary>
        public void GetJudgeLaneRange(NoteCategory category, float laneStart, float laneEnd, out float judgeStart, out float judgeEnd)
        {
            WidenLaneRange(laneStart, laneEnd, LaneOffsetFor(category), out judgeStart, out judgeEnd);
        }

        /// <summary>
        /// The one place that widens a lane range by an offset, so every producer of a note agrees on the
        /// direction: the range grows outwards on both sides, it is never narrowed.
        /// </summary>
        public static void WidenLaneRange(float laneStart, float laneEnd, float laneOffset, out float judgeStart, out float judgeEnd)
        {
            judgeStart = laneStart - laneOffset;
            judgeEnd = laneEnd + laneOffset;
        }

        /// <summary>Inches per 1/60 s frame converted to pixels for the given screen DPI.</summary>
        public float FlickDistanceInPixels(float screenDpi)
        {
            return MathF.Max(0f, screenDpi / 2.54f * FlickDistanceInches);
        }
    }
}
