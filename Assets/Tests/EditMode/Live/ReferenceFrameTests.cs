using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    [TestFixture]
    public class ReferenceFrameTests
    {
        [Test]
        public void OneReferenceFrameIsASixtiethOfASecond()
        {
            Assert.That(ReferenceFrame.Seconds, Is.EqualTo(1f / 60f).Within(1e-6f));
        }

        [Test]
        public void FramesConvertToSecondsWithoutDependingOnTheRefreshRate()
        {
            Assert.That(ReferenceFrame.FromFrames(1f), Is.EqualTo(1f / 60f).Within(1e-6f));
            Assert.That(ReferenceFrame.FromFrames(2.5f), Is.EqualTo(2.5f / 60f).Within(1e-6f));
            Assert.That(ReferenceFrame.FromFrames(0f), Is.EqualTo(0f));
        }

        [Test]
        public void AnInputWithinTheToleranceIsStillFresh()
        {
            Assert.That(ReferenceFrame.IsStillFresh(1f, 1f + ReferenceFrame.Seconds - 0.001f, ReferenceFrame.Seconds), Is.True);
        }

        [Test]
        public void AnInputOlderThanTheToleranceIsStale()
        {
            Assert.That(ReferenceFrame.IsStillFresh(1f, 1f + ReferenceFrame.Seconds + 0.001f, ReferenceFrame.Seconds), Is.False);
        }

        [Test]
        public void TheBoundaryMomentCountsAsFresh()
        {
            Assert.That(ReferenceFrame.IsStillFresh(1f, 1f + ReferenceFrame.Seconds, ReferenceFrame.Seconds), Is.True);
        }

        [Test]
        public void ASixtyHertzDeviceGetsMoreSlackThanAPreviousFrameCheck()
        {
            // The bug this replaces: at 165 Hz "one frame" is 6 ms, so a hold refreshed at the start
            // of a frame was already stale by the next one. With the reference frame the tolerance is
            // the same on every device.
            float tolerance = ReferenceFrame.Seconds;
            Assert.That(tolerance, Is.GreaterThan(1f / 165f));
            Assert.That(tolerance, Is.GreaterThan(1f / 120f));
            Assert.That(ReferenceFrame.IsStillFresh(0f, 1f / 165f, tolerance), Is.True,
                "a 165 Hz device must not consider the hold stale one frame later");
        }
    }
}
