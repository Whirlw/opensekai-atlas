using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public class CandidateOrderTests
    {
        private const int Flick = CandidateOrder.FlickRank;
        private const int Touch = CandidateOrder.TouchRank;
        private const int Held = CandidateOrder.HeldRank;

        [Test]
        public void LowerResultValueRanksFirst()
        {
            // The original comparator sorts by NoteResult ascending (None=0 .. JustPerfect=8), so a
            // candidate that would only earn Great comes before one that would earn JustPerfect.
            Assert.That(
                CandidateOrder.Compare(Touch, NoteResult.Great, 5f, 9f, Touch, NoteResult.JustPerfect, 1f, 0f),
                Is.LessThan(0));
        }

        [Test]
        public void SameResultFallsBackToTheEarlierNote()
        {
            Assert.That(
                CandidateOrder.Compare(Touch, NoteResult.Great, 1f, 9f, Touch, NoteResult.Great, 2f, 0f),
                Is.LessThan(0));
        }

        [Test]
        public void SameResultAndTimeFallsBackToTheCloserLane()
        {
            Assert.That(
                CandidateOrder.Compare(Touch, NoteResult.Great, 1f, 0.25f, Touch, NoteResult.Great, 1f, 4f),
                Is.LessThan(0));
        }

        [Test]
        public void IdenticalCandidatesCompareEqual()
        {
            Assert.That(
                CandidateOrder.Compare(Touch, NoteResult.Good, 1f, 0.5f, Touch, NoteResult.Good, 1f, 0.5f),
                Is.Zero);
        }

        [Test]
        public void AFlickOutranksEverything()
        {
            Assert.That(
                CandidateOrder.Compare(Flick, NoteResult.Bad, 9f, 9f, Touch, NoteResult.JustPerfect, 0f, 0f),
                Is.LessThan(0));

            Assert.That(
                CandidateOrder.Compare(Flick, NoteResult.Bad, 9f, 9f, Held, NoteResult.JustPerfect, 0f, 0f),
                Is.LessThan(0));
        }

        [Test]
        public void ANoteJudgedByTheTouchOutranksAHeldBar()
        {
            // A rub (friction) note has to set isTouched on some frame before its progress reaches 1;
            // the bar it sits on is refreshed every frame and does not need the slot.
            Assert.That(
                CandidateOrder.Compare(Touch, NoteResult.Bad, 9f, 9f, Held, NoteResult.JustPerfect, 0f, 0f),
                Is.LessThan(0),
                "a rub note must not be starved by the bar the same finger holds");

            Assert.That(
                CandidateOrder.Compare(Held, NoteResult.JustPerfect, 0f, 0f, Touch, NoteResult.Bad, 9f, 9f),
                Is.GreaterThan(0));
        }

        [Test]
        public void TwoHeldNotesStillUseTheNormalOrder()
        {
            Assert.That(
                CandidateOrder.Compare(Held, NoteResult.Great, 1f, 9f, Held, NoteResult.Great, 2f, 0f),
                Is.LessThan(0));
        }

        [Test]
        public void TwoFlicksUseTheNormalOrder()
        {
            Assert.That(
                CandidateOrder.Compare(Flick, NoteResult.Great, 1f, 9f, Flick, NoteResult.Great, 2f, 0f),
                Is.LessThan(0));
        }

        [Test]
        public void SameJudgmentTimeKeepsTheCloserNote()
        {
            Assert.That(CandidateOrder.KeepsFirst(Touch, 0.25f, Touch, 4f), Is.True);
            Assert.That(CandidateOrder.KeepsFirst(Touch, 4f, Touch, 0.25f), Is.False);
        }

        [Test]
        public void SameJudgmentTimeTieKeepsTheEarlierEntry()
        {
            Assert.That(CandidateOrder.KeepsFirst(Touch, 1f, Touch, 1f), Is.True);
        }

        [Test]
        public void HigherRankIsNotDroppedForACloserNote()
        {
            Assert.That(CandidateOrder.KeepsFirst(Flick, 4f, Held, 0.25f), Is.True);
            Assert.That(CandidateOrder.KeepsFirst(Held, 0.25f, Flick, 4f), Is.False);
            Assert.That(CandidateOrder.KeepsFirst(Touch, 4f, Held, 0.25f), Is.True);
            Assert.That(CandidateOrder.KeepsFirst(Held, 0.25f, Touch, 4f), Is.False);
        }
    }
}
