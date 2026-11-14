using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;

namespace OpenSekai.Tests.Core
{
    public sealed class TimingCompensationTests
    {
        private const float Audio = 10f;

        [Test]
        public void AudioLatencyMovesJudgementAndPictureTogether()
        {
            var compensation = new TimingCompensation(30f, 0f, 0f);
            Assert.That(compensation.LogicTime(Audio), Is.EqualTo(9.97f).Within(1e-5f));
            Assert.That(compensation.ViewTime(Audio), Is.EqualTo(9.97f).Within(1e-5f));
            Assert.That(compensation.ViewLeadSeconds, Is.Zero);
        }

        [Test]
        public void VisualLatencyMovesOnlyThePicture()
        {
            var compensation = new TimingCompensation(0f, 40f, 0f);
            Assert.That(compensation.LogicTime(Audio), Is.EqualTo(Audio).Within(1e-5f), "judgement is untouched");
            Assert.That(compensation.ViewTime(Audio), Is.EqualTo(10.04f).Within(1e-5f), "the picture is drawn ahead");
            Assert.That(compensation.ViewLeadSeconds, Is.EqualTo(0.04f).Within(1e-6f));
        }

        [Test]
        public void JudgeOffsetMovesOnlyJudgement()
        {
            // Positive: "I hit late". The logic clock runs behind, so a late press lands on the beat;
            // the picture is drawn ahead by the same amount and does not move.
            var compensation = new TimingCompensation(0f, 0f, 25f);
            Assert.That(compensation.LogicTime(Audio), Is.EqualTo(9.975f).Within(1e-5f));
            Assert.That(compensation.ViewTime(Audio), Is.EqualTo(Audio).Within(1e-5f), "the picture is untouched");
            Assert.That(compensation.LogicTime(Audio) + compensation.ViewLeadSeconds,
                Is.EqualTo(compensation.ViewTime(Audio)).Within(1e-5f), "keys and bodies share one picture clock");
        }

        [Test]
        public void AllThreeCombine()
        {
            var compensation = new TimingCompensation(30f, 20f, -10f);
            Assert.That(compensation.LogicTime(Audio), Is.EqualTo(10f - 0.02f).Within(1e-5f));
            Assert.That(compensation.ViewTime(Audio), Is.EqualTo(10f - 0.01f).Within(1e-5f));
            Assert.That(compensation.LogicTime(Audio) + compensation.ViewLeadSeconds,
                Is.EqualTo(compensation.ViewTime(Audio)).Within(1e-5f));
        }

        [Test]
        public void ClocksNeverGoBelowZero()
        {
            var compensation = new TimingCompensation(500f, 0f, 200f);
            Assert.That(compensation.LogicTime(0.1f), Is.Zero);
            Assert.That(compensation.ViewTime(0.1f), Is.Zero);
        }

        [Test]
        public void ValuesAreClampedToTheirRanges()
        {
            var compensation = new TimingCompensation(9999f, -9999f, float.NaN);
            Assert.That(compensation.AudioLatencyMs, Is.EqualTo(TimingCompensation.MaxAudioLatencyMs));
            Assert.That(compensation.VisualLatencyMs, Is.EqualTo(TimingCompensation.MinVisualLatencyMs));
            Assert.That(compensation.JudgeOffsetMs, Is.Zero);
        }

        [TestCase(0f, 0f)]
        [TestCase(1f, -16.6667f)]
        [TestCase(-1.5f, 25f)]
        public void TheOldFrameShiftBecomesAnAudioLatency(float frames, float expectedMs)
        {
            // The old setting did audio + frames/60; the new one does audio - L.
            Assert.That(TimingCompensation.AudioLatencyFromLegacyFrames(frames), Is.EqualTo(expectedMs).Within(1e-3f));
        }

        [Test]
        public void ATravellingKeyIsDrawnAheadButNeverPastTheLine()
        {
            Assert.That(TimingCompensation.ViewProgress(0.5f, 1f, 0.04f), Is.EqualTo(0.54f).Within(1e-5f));
            Assert.That(TimingCompensation.ViewProgress(0.99f, 1f, 0.04f), Is.EqualTo(1f), "it waits on the line");
        }

        [Test]
        public void AKeyTheLogicPutOnOrPastTheLineIsDrawnWhereTheLogicSays()
        {
            Assert.That(TimingCompensation.ViewProgress(1f, 1f, 0.04f), Is.EqualTo(1f), "a pinned key stays pinned");
            Assert.That(TimingCompensation.ViewProgress(1.2f, 1f, 0.04f), Is.EqualTo(1.2f), "a passing key is not pushed further");
        }

        [Test]
        public void ANegativeLeadDrawsATravellingKeyBehind()
        {
            Assert.That(TimingCompensation.ViewProgress(0.5f, 1f, -0.05f), Is.EqualTo(0.45f).Within(1e-5f));
        }

        [Test]
        public void NoLeadOrNoWindowChangesNothing()
        {
            Assert.That(TimingCompensation.ViewProgress(0.5f, 1f, 0f), Is.EqualTo(0.5f));
            Assert.That(TimingCompensation.ViewProgress(0.5f, 0f, 0.04f), Is.EqualTo(0.5f));
        }
    }
}
