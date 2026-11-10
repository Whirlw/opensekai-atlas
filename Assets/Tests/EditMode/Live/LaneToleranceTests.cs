using NUnit.Framework;
using OpenSekai.Shared.Scoring;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Pins the lane tolerance each note category is judged with. A chart author reads these numbers
    /// as "how far outside the lane a finger may land", so they are part of the contract between the
    /// chart and the player: an ordinary key forgives 1.25 lanes, a bar head and a flick forgive 1.5.
    /// </summary>
    public class LaneToleranceTests
    {
        [Test]
        public void AnOrdinaryKeyForgivesOneAndAQuarterLanes()
        {
            Assert.AreEqual(1.25f, JudgeTuning.ShippedNormalLaneOffset);
        }

        [Test]
        public void ABarHeadAndAFlickForgiveOneAndAHalfLanes()
        {
            Assert.AreEqual(1.5f, JudgeTuning.ShippedLongLaneOffset);
            Assert.AreEqual(1.5f, JudgeTuning.ShippedFlickLaneOffset);
        }

        [Test]
        public void TheRangeGrowsOutwardsOnBothSides()
        {
            JudgeTuning tuning = JudgeTuning.Shipped;

            tuning.GetJudgeLaneRange(NoteCategory.Normal, 4f, 5f, out float normalStart, out float normalEnd);
            Assert.AreEqual(2.75f, normalStart, 0.0001f);
            Assert.AreEqual(6.25f, normalEnd, 0.0001f);

            tuning.GetJudgeLaneRange(NoteCategory.Long, 4f, 5f, out float longStart, out float longEnd);
            Assert.AreEqual(2.5f, longStart, 0.0001f);
            Assert.AreEqual(6.5f, longEnd, 0.0001f);
        }

        [Test]
        public void AFlickAndARubbedFlickShareTheSameTolerance()
        {
            JudgeTuning tuning = JudgeTuning.Shipped;

            Assert.AreEqual(
                tuning.LaneOffsetFor(NoteCategory.Flick),
                tuning.LaneOffsetFor(NoteCategory.FrictionFlick));
            Assert.AreEqual(
                tuning.LaneOffsetFor(NoteCategory.Long),
                tuning.LaneOffsetFor(NoteCategory.FrictionLong));
        }
    }
}
