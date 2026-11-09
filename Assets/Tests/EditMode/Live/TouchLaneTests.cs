using NUnit.Framework;
using OpenSekai.Shared.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Pins the screen-position to lane mapping, including the fractional result between lanes and the
    /// three ways a touch falls outside the playable area.
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
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(160f, -10f, 0f), true, 10f, 20f), Is.EqualTo(4f));
        }

        [Test]
        public void BetweenTwoLanesIsFractional()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(180f, -10f, 0f), true, 10f, 20f), Is.EqualTo(4.5f));
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(20f, -10f, 0f), true, 10f, 20f), Is.EqualTo(0.5f));
        }

        [Test]
        public void LeftOfTheFirstLaneInsideTheToleranceStillCounts()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(-8f, -10f, 0f), true, 10f, 20f), Is.EqualTo(0f));
        }

        [Test]
        public void LeftOfTheToleranceIsOutside()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(-12f, -10f, 0f), true, 10f, 20f), Is.EqualTo(TouchLane.Outside));
        }

        [Test]
        public void RightOfTheLastLaneInsideTheToleranceClampsToTheLastLane()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(448f, -10f, 0f), true, 10f, 20f), Is.EqualTo(11f));
        }

        [Test]
        public void RightOfTheToleranceIsOutside()
        {
            Assert.That(TouchLane.Resolve(Lanes(), new Vector3(452f, -10f, 0f), true, 10f, 20f), Is.EqualTo(TouchLane.Outside));
        }

        [Test]
        public void AboveTheJudgmentLineIsOutsideOnlyWhenTheCheckIsOn()
        {
            var touch = new Vector3(160f, 25f, 0f);

            Assert.That(TouchLane.Resolve(Lanes(), touch, true, 10f, 20f), Is.EqualTo(TouchLane.Outside));
            Assert.That(TouchLane.Resolve(Lanes(), touch, false, 10f, 20f), Is.EqualTo(4f));
        }

        [Test]
        public void NoLanesMeansOutside()
        {
            Assert.That(TouchLane.Resolve(null, Vector3.zero, true, 10f, 20f), Is.EqualTo(TouchLane.Outside));
            Assert.That(TouchLane.Resolve(new Vector2[0], Vector3.zero, true, 10f, 20f), Is.EqualTo(TouchLane.Outside));
        }
    }
}
