using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class NoteSpeedRatioTests
    {
        private static MusicScoreInfo Speed(float barProgress, float speedRatio)
        {
            // bar, barProgress, time, bpm, timeSignature, speedRatio, seVolume
            return new MusicScoreInfo(0, barProgress, barProgress * 2f, 120f, 4f, speedRatio, 1f);
        }

        [Test]
        public void NoteBehindThePlayheadUsesTheFallback()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 4f) }, 5f, 3f, 1.5f), Is.EqualTo(1.5f));
        }

        [Test]
        public void MissingSpeedEventsUseTheFallback()
        {
            Assert.That(NoteSpeedRatio.Resolve(null, 0f, 4f, 2f), Is.EqualTo(2f));
            Assert.That(NoteSpeedRatio.Resolve(new MusicScoreInfo[0], 0f, 4f, 2f), Is.EqualTo(2f));
        }

        [Test]
        public void SingleEventAppliesForTheWholeSpan()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 2f) }, 0f, 1f, 9f), Is.EqualTo(2f));
        }

        [Test]
        public void SegmentsAreAveragedByLength()
        {
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 3f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 4f, 9f), Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void PlayheadInsideASegmentOnlyAveragesTheRemainder()
        {
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 3f) };

            // 1 bar of speed 1 (from 1 to 2) and 1 bar of speed 3 (from 2 to 3).
            Assert.That(NoteSpeedRatio.Resolve(events, 1f, 3f, 9f), Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void NotePastTheLastEventKeepsTheLastSpeed()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 5f) }, 0f, 10f, 1f), Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void NoteOnAnEventBoundaryUsesTheSegmentBeforeIt()
        {
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 3f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2f, 9f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NegativeSpeedIsAveragedWithoutBeingFlipped()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, -2f) }, 0f, 1f, 1f), Is.EqualTo(-2f).Within(1e-5f));
        }

        [Test]
        public void ZeroLengthSpanFallsBack()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 3f) }, 1f, 1f, 4f), Is.EqualTo(4f));
        }
    }
}
