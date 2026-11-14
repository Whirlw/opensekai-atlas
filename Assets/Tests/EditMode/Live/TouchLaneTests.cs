using NUnit.Framework;
using OpenSekai.Shared.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Pins the screen-position to lane mapping: the fractional result between lanes, the two ways a
    /// touch falls outside the playable area on the lane axis, and the vertical gate that lives on its own
    /// because it is asked exactly once, when a touch is born.
    /// </summary>
    public sealed class TouchLaneTests
    {
        /// <summary>Twelve lanes, 40 units apart, all on y = 0.</summary>
        private static Vector2[] Lanes()
        {
            var lanes = new Vector2[12];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = new Vector2(i * 40f, 0f);
            }

            return lanes;
        }

        [Test]
        public void ExactLaneCentreIsAWholeLane()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(160f, -10f, 0f), 10f), Is.EqualTo(4f));
        }

        [Test]
        public void BetweenTwoLanesIsFractional()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(180f, -10f, 0f), 10f), Is.EqualTo(4.5f));
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(20f, -10f, 0f), 10f), Is.EqualTo(0.5f));
        }

        [Test]
        public void LeftOfTheFirstLaneInsideTheToleranceStillCounts()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(-8f, -10f, 0f), 10f), Is.EqualTo(0f));
        }

        [Test]
        public void LeftOfTheToleranceIsOutside()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(-12f, -10f, 0f), 10f), Is.EqualTo(TouchLane.Outside));
        }

        [Test]
        public void RightOfTheLastLaneInsideTheToleranceClampsToTheLastLane()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(448f, -10f, 0f), 10f), Is.EqualTo(11f));
        }

        [Test]
        public void RightOfTheToleranceIsOutside()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(452f, -10f, 0f), 10f), Is.EqualTo(TouchLane.Outside));
        }

        [Test]
        public void TheLaneAxisDoesNotLookAtYAtAll()
        {
            // The vertical question is the gate's, not the resolver's: a position above the line still
            // has a lane, and whoever holds the touch decides whether that lane is an input.
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(160f, 900f, 0f), 10f), Is.EqualTo(4f));
        }

        [Test]
        public void AboveTheLineIsTheSingleVerticalGate()
        {
            Assert.That(TouchLane.IsAboveTheLine(Lanes(), new Vector3(160f, 25f, 0f), 20f), Is.True);
            Assert.That(TouchLane.IsAboveTheLine(Lanes(), new Vector3(160f, 10f, 0f), 20f), Is.False,
                "inside the tolerance is still on the board");
            Assert.That(TouchLane.IsAboveTheLine(Lanes(), new Vector3(160f, -10f, 0f), 20f), Is.False,
                "below the line is where the notes are");
        }

        [Test]
        public void NoLanesMeansOutsideAndNoLine()
        {
            Assert.That(TouchLane.Resolve(null, Vector3.zero, 10f), Is.EqualTo(TouchLane.Outside));
            Assert.That(TouchLane.Resolve(new Vector2[0], Vector3.zero, 10f), Is.EqualTo(TouchLane.Outside));
            Assert.That(TouchLane.IsAboveTheLine(null, Vector3.zero, 20f), Is.False);
        }
    }
}
