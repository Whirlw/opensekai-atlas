using System.Linq;
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

        private static MusicScoreInfo[] ThreeSpeeds() => new[]
        {
            Speed(0f, 1f), Speed(2f, 2f), Speed(4f, 0.5f)
        };

        [TestCase(2f, 1f)]
        [TestCase(4f, 1.5f)]
        public void ThreeSegmentBoundaryUsesTheWeightedTravel(float noteProgress, float expected)
        {
            // At 2: (2 * 1) / 2 = 1; at 4: (2 * 1 + 2 * 2) / 4 = 1.5.
            Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, noteProgress, 9f),
                Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void TravelAcrossTheSecondAndThirdSegmentsIsWeighted()
        {
            // [3,4] at 2 plus [4,6] at 0.5: (1 * 2 + 2 * 0.5) / 3 = 1.
            Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 3f, 6f, 9f),
                Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TravelWhollyInsideTheSecondSegmentUsesItsSpeed()
        {
            // [2.25,3.75] is entirely at 2: (1.5 * 2) / 1.5 = 2.
            Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 2.25f, 3.75f, 9f),
                Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void EmptyThreeSegmentInputUsesTheGivenFallback()
        {
            // No segment lengths exist to weight, so the supplied fallback is 9.
            Assert.That(NoteSpeedRatio.Resolve(new MusicScoreInfo[0], 2.25f, 3.75f, 9f),
                Is.EqualTo(9f));
        }

        private static MusicScoreInfo At(float bar, float time, float speedRatio)
        {
            return new MusicScoreInfo(0, bar, time, 120f, 4f, speedRatio, 1f);
        }

        /// <summary>799 for 0.0078 bar (about 9 ms), then 1.0: the spike at bar 80.5 of the fixture.</summary>
        private static MusicScoreInfo[] AFlashThenNormal() => new[]
        {
            At(0f, 0f, 1f), At(2f, 4f, 799f), At(2.0078f, 4.009f, 1f), At(6f, 12f, 1f)
        };

        [Test]
        public void AFlashSegmentAddsNoTravelToTheNotesBehindIt()
        {
            // Folding the flash into the span gave this note an average of ~9.4 instead of 1: the lead was a
            // few milliseconds long, so the note only appeared once the flash had left the playhead, part
            // way down the screen.
            Assert.That(NoteSpeedRatio.Resolve(AFlashThenNormal(), 0f, 5f, 1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(NoteSpeedRatio.Resolve(AFlashThenNormal(), 2.5f, 5f, 1f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TheNoteInsideAFlashIsDrawnWithTheFlashSpeed()
        {
            Assert.That(NoteSpeedRatio.Resolve(AFlashThenNormal(), 0f, 2.004f, 1f), Is.EqualTo(799f).Within(1e-4f));
        }

        [Test]
        public void AFastRampStepIsNotAFlash()
        {
            // The same 15 ticks, but the speed only moves from 0.5 to 0.9: that is a ramp step, not a spike.
            // Its travel is a hundredth of a bar, so it stays in the average and a note sitting inside it
            // keeps the ramp's speed instead of being drawn at 0.5 (lead 4 s) next to a note at 0.9.
            MusicScoreInfo[] events =
            {
                At(0f, 0f, 1f), At(2f, 4f, 0.5f), At(2.0078f, 4.009f, 0.9f), At(3f, 6f, 1f)
            };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2.004f, 1f), Is.EqualTo(1f).Within(0.02f));
            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2.5f, 1f), Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void TheLastEventIsNeverTreatedAsAFlash()
        {
            // Nothing follows it, so there is no width to compare; the span uses its speed as written.
            // (2 bars at 1 plus half a bar at 40 over 2.5 bars = 8.8.)
            MusicScoreInfo[] events = { At(0f, 0f, 1f), At(2f, 4f, 40f) };

            Assert.That(NoteSpeedRatio.Resolve(events, 0f, 2.5f, 1f), Is.EqualTo(8.8f).Within(1e-4f));
        }

        [Test]
        public void TheNotesBehindTheBar80SpikeKeepOneSteadyLead()
        {
            // The chart itself: at bar 81+3/4 the lead must stay the 1.1 segment's lead (2 s / 1.1 = 1.818 s)
            // for the whole approach, so those notes fall in from the top edge instead of appearing part way
            // down the screen once the spike has left the playhead.
            const float window = 2f;
            using var harness = new LiveHarness();
            harness.Arrange(AutoplayCompletionTests.LoadFixture("deinos-phainein"), null, window);

            var track = harness.Logic.Notes.Notes
                .Where(note => note != null
                    && System.Math.Abs(note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress - 81.75f) < 0.002f)
                .ToArray();
            Assert.That(track.Length, Is.GreaterThanOrEqualTo(3), "the fixture must still hold the notes at tick 156960");

            var leads = new System.Collections.Generic.List<float>();
            var fastLeads = new System.Collections.Generic.List<float>();
            float end = track.Max(note => note.MusicScoreInfo.time) + 1f;
            for (float now = 0f; now < end; now += 1f / 60f)
            {
                harness.OnUpdate(now, 10d + now);
                harness.Logic.OnAutoInput();
                foreach (Sekai.Live.NoteBase note in track)
                {
                    if (note.offsetTime == 0f || now >= note.MusicScoreInfo.time)
                    {
                        continue;
                    }

                    (System.Math.Abs(note.speedRatio - 1f) < 0.01f ? leads : fastLeads).Add(note.offsetTime);
                }
            }

            Assert.That(leads.Count, Is.GreaterThan(0), "the ordinary notes at tick 156960");
            float expected = window / 1.1f;
            float smallest = leads.OrderBy(value => value).First();
            float largest = leads.OrderByDescending(value => value).First();
            Assert.That(smallest, Is.GreaterThan(expected * 0.98f),
                $"smallest lead {smallest:F4}s, largest {largest:F4}s; the spike must not shorten them");
            // Slightly longer is honest: the slow ramp right after the spike is part of the span for a while.
            Assert.That(largest, Is.LessThan(expected * 1.15f),
                $"largest lead {largest:F4}s, smallest {smallest:F4}s; the spike must not stretch them either");
            Assert.That(fastLeads.Count, Is.GreaterThan(0), "the notes written at 3x speed");
            Assert.That(fastLeads.OrderBy(value => value).First(),
                Is.EqualTo(expected / 3f).Within(expected / 3f * 0.02f),
                "a 3x note keeps a third of the lead from the start, not just at the end");
        }
    }
}
