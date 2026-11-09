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

            // 5/60 in at 100 dpi is (5/60) * 100 / 2.54 = 3.28 px.
            Assert.That(tuning.FlickDistanceInPixels(100f), Is.EqualTo(3.2808f).Within(1e-3f));
            // A zero or negative DPI must not produce a negative threshold.
            Assert.That(tuning.FlickDistanceInPixels(0f), Is.EqualTo(0f));
        }

        [Test]
        public void FlickDistanceIsFiveInchesPerSecondAtSixtyFrames()
        {
            // 5/60 in per frame * 60 frames = 5 in/s, i.e. about 12.7 cm/s.
            float inchesPerSecond = JudgeTuning.Shipped.FlickDistanceInches * 60f;
            Assert.That(inchesPerSecond, Is.EqualTo(5f).Within(0.01f));
            Assert.That(inchesPerSecond * 2.54f, Is.EqualTo(12.7f).Within(0.02f));
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
        [Test]
        public void NullDataYieldsNoTolerance()
        {
            JudgeTuning tuning = JudgeTuning.From(null);

            Assert.That(tuning.NormalLaneOffset, Is.EqualTo(0f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.Flick), Is.EqualTo(0f));
        }

        [Test]
        public void ShippedAssetStillCarriesTheOriginalNumbers()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<LiveBundleBuildData>(
                "Assets/Graphics/Resources/live/config/LiveBundleBuildData.asset");
            Assert.That(asset, Is.Not.Null, "the live tuning asset is missing");

            JudgeTuning tuning = JudgeTuning.From(asset);

            Assert.That(tuning.NormalLaneOffset, Is.EqualTo(JudgeTuning.ShippedNormalLaneOffset).Within(1e-5f));
            Assert.That(tuning.LongLaneOffset, Is.EqualTo(JudgeTuning.ShippedLongLaneOffset).Within(1e-5f));
            Assert.That(tuning.FlickLaneOffset, Is.EqualTo(JudgeTuning.ShippedFlickLaneOffset).Within(1e-5f));
            Assert.That(tuning.FlickDistanceInches, Is.EqualTo(JudgeTuning.ShippedFlickDistanceInches).Within(1e-5f));
        }
    }
}
