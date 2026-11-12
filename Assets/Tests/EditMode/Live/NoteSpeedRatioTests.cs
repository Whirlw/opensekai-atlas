using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// The speed a note is drawn at is the speed of its own segment, so the screen length of a span scales
    /// with the speed the chart asked for. These cases used to pin the length-weighted average between the
    /// playhead and the note; that average made a body change shape as the playhead moved and made a speed
    /// change look like it started a bar early, so it was replaced.
    /// </summary>
    public sealed class NoteSpeedRatioTests
    {
        private static MusicScoreInfo Speed(float barProgress, float speedRatio)
        {
            // bar, barProgress, time, bpm, timeSignature, speedRatio, seVolume
            return new MusicScoreInfo(0, barProgress, barProgress * 2f, 120f, 4f, speedRatio, 1f);
        }

        private static MusicScoreInfo SpeedInBar(int bar, float barProgress, float speedRatio)
        {
            return new MusicScoreInfo(bar, barProgress, bar + barProgress, 120f, 4f, speedRatio, 1f);
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
        public void EachSegmentKeepsItsOwnSpeed()
        {
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 3f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 1f, 9f), Is.EqualTo(1f));
            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 3f, 9f), Is.EqualTo(3f));
        }

        [Test]
        public void NoteOnAnEventBoundaryStartsTheNewSegment()
        {
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 3f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2f, 9f), Is.EqualTo(3f),
                "the segment that starts on the boundary is the one the note travels in");
        }

        [Test]
        public void NotePastTheLastEventKeepsTheLastSpeed()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 5f) }, 0f, 10f, 1f), Is.EqualTo(5f));
        }

        [Test]
        public void ReverseScrollKeepsItsDirection()
        {
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, -2f) }, 0f, 1f, 1f), Is.EqualTo(-2f));
        }

        [Test]
        public void ThePlayheadDoesNotChangeANotesSpeed()
        {
            // The body geometry has to be stable while it is on screen: the same note answers the same at
            // every playhead position, which the old average did not.
            MusicScoreInfo[] events = { SpeedInBar(0, 0f, 2f), SpeedInBar(2, 0f, 5f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2.2f, 1f), Is.EqualTo(5f));
            Assert.That(NoteSpeedRatio.Resolve(events, 1.9f, 2.2f, 1f), Is.EqualTo(5f));
            Assert.That(NoteSpeedRatio.Resolve(events, 2.1f, 2.2f, 1f), Is.EqualTo(5f));
        }

        [Test]
        public void ASegmentBehindTheNoteIsNotChargedIntoIt()
        {
            // The reported case: the playhead is at 18.5, the note at 19.2, and the previous segment (2x,
            // running since bar 15) sits between them. The old average returned ~2.5 and drew the note at a
            // speed the chart never asked for; the note's own segment is 5x.
            MusicScoreInfo[] events = { SpeedInBar(15, 0f, 2f), SpeedInBar(19, 0f, 5f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 18.5f, 19.2f, 1f), Is.EqualTo(5f));
        }

        [Test]
        public void ASpikeIsClampedToTheDisplayCeiling()
        {
            // A 799x spike is how a chart makes notes appear abruptly; it must not become a scroll speed.
            MusicScoreInfo[] events = { Speed(0f, 1f), Speed(2f, 799f), Speed(3f, 1f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 2.5f, 2.6f, 1.5f), Is.EqualTo(NoteSpeedRatio.MaxSegmentSpeedRatio));
            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 1f, 1.5f), Is.EqualTo(1f), "sane segments are untouched");
            Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, -325f) }, 0f, 1f, 1f),
                Is.EqualTo(-NoteSpeedRatio.MaxSegmentSpeedRatio), "reverse scroll keeps its sign");
        }

        private static MusicScoreInfo[] ThreeSpeeds() => new[]
        {
            Speed(0f, 1f), Speed(2f, 2f), Speed(4f, 0.5f)
        };

        [TestCase(1f, 1f)]
        [TestCase(3f, 2f)]
        [TestCase(6f, 0.5f)]
        public void TravelUsesTheSpeedOfTheSegmentTheNoteSitsIn(float noteProgress, float expected)
        {
            Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, noteProgress, 9f),
                Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void EmptyThreeSegmentInputUsesTheGivenFallback()
        {
            Assert.That(NoteSpeedRatio.Resolve(new MusicScoreInfo[0], 2.25f, 3.75f, 9f),
                Is.EqualTo(9f));
        }
    }
}
