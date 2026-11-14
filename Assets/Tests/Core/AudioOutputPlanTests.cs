using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;

namespace OpenSekai.Tests.Core
{
    public sealed class AudioOutputPlanTests
    {
        [Test]
        public void TheOutputMovesToTheNativeRate()
        {
            AudioOutputDecision decision = AudioOutputPlan.Choose(44100, 256, 48000, 256);
            Assert.That(decision.SampleRate, Is.EqualTo(48000));
            Assert.That(decision.DspBufferSize, Is.EqualTo(256));
            Assert.That(decision.Changed, Is.True);
        }

        [Test]
        public void ANativeConfigurationIsLeftAlone()
        {
            AudioOutputDecision decision = AudioOutputPlan.Choose(48000, 256, 48000, 256);
            Assert.That(decision.Changed, Is.False);
        }

        [TestCase(192, 192)]
        [TestCase(240, 240)]
        [TestCase(96, 192)]
        [TestCase(64, 128)]
        public void TheBlockIsAlignedToTheBurstWithoutDroppingBelowTheFloor(int burst, int expected)
        {
            AudioOutputDecision decision = AudioOutputPlan.Choose(48000, 256, 48000, burst);
            Assert.That(decision.DspBufferSize, Is.EqualTo(expected));
            Assert.That(decision.DspBufferSize % burst, Is.Zero, "a whole number of bursts");
        }

        [TestCase(480)]
        [TestCase(512)]
        public void TheBlockNeverGrows(int burst)
        {
            // A larger block is more latency; aligning is not worth that.
            AudioOutputDecision decision = AudioOutputPlan.Choose(48000, 256, 48000, burst);
            Assert.That(decision.DspBufferSize, Is.EqualTo(256));
        }

        [TestCase(0, 0)]
        [TestCase(-1, -1)]
        [TestCase(8000, 4096)]
        public void ADeviceThatReportsNothingUsefulKeepsItsConfiguration(int rate, int burst)
        {
            AudioOutputDecision decision = AudioOutputPlan.Choose(44100, 256, rate, burst);
            Assert.That(decision.SampleRate, Is.EqualTo(44100));
            Assert.That(decision.DspBufferSize, Is.EqualTo(256));
            Assert.That(decision.Changed, Is.False);
        }
    }
}
