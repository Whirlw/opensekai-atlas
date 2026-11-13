using NUnit.Framework;
using OpenSekai.Shared.Scoring;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Guards the lane and swipe tolerances: they live in data, so the risk is silent drift, not logic.
    /// </summary>
    public sealed class JudgeTuningTests
    {
        [Test]
        public void LaneOffsetFollowsTheNoteCategory()
        {
            var tuning = JudgeTuning.Shipped;

            Assert.That(tuning.LaneOffsetFor(NoteCategory.Normal), Is.EqualTo(1.25f).Within(1e-5f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.Flick), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.FrictionFlick), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.Long), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.FrictionLong), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.FrictionHideLong), Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void JudgeRangeWidensBothSides()
        {
            var tuning = JudgeTuning.Shipped;

            tuning.GetJudgeLaneRange(NoteCategory.Normal, 4f, 5f, out float start, out float end);

            Assert.That(start, Is.EqualTo(2.75f).Within(1e-5f));
            Assert.That(end, Is.EqualTo(6.25f).Within(1e-5f));
        }

        [Test]
        public void FlickDistanceConvertsInchesToPixelsWithTheScreenDpi()
        {
            var tuning = JudgeTuning.Shipped;

            // 5 cm/s = 5/2.54 in/s, per 1/60 s frame that is 5/2.54/60 in; at 100 dpi it is
            // (5/2.54/60) * 100 / 2.54 = 1.29 px.
            Assert.That(tuning.FlickDistanceInPixels(100f), Is.EqualTo(1.2919f).Within(1e-3f));
            // A zero or negative DPI must not produce a negative threshold.
            Assert.That(tuning.FlickDistanceInPixels(0f), Is.EqualTo(0f));
        }

        [Test]
        public void FlickDistanceIsFiveCentimetresPerSecondAtSixtyFrames()
        {
            // The judge compares one 60 fps frame of travel, so a threshold of 5 cm/s is 5/2.54/60 inches.
            float centimetresPerSecond = JudgeTuning.Shipped.FlickDistanceInches * 60f * 2.54f;
            Assert.That(centimetresPerSecond, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void WidenLaneRangeIsTheSingleFormulaBehindTheCategoryRange()
        {
            JudgeTuning.WidenLaneRange(4f, 5f, 0.75f, out float judgeStart, out float judgeEnd);
            Assert.That(judgeStart, Is.EqualTo(3.25f).Within(1e-4f));
            Assert.That(judgeEnd, Is.EqualTo(5.75f).Within(1e-4f));

            JudgeTuning tuning = JudgeTuning.Shipped;
            float offset = tuning.LaneOffsetFor(Sekai.Live.NoteCategory.Combo);
            tuning.GetJudgeLaneRange(Sekai.Live.NoteCategory.Combo, 4f, 5f, out float byCategoryStart, out float byCategoryEnd);
            JudgeTuning.WidenLaneRange(4f, 5f, offset, out float manualStart, out float manualEnd);

            Assert.That(byCategoryStart, Is.EqualTo(manualStart).Within(1e-4f));
            Assert.That(byCategoryEnd, Is.EqualTo(manualEnd).Within(1e-4f));
        }

    }
}
