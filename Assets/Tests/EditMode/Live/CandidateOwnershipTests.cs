using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public class CandidateOwnershipTests
    {
        [Test]
        public void CloserInputKeepsTheNote()
        {
            Assert.That(CandidateOwnership.Loser(0.2f, 0.5f), Is.EqualTo(CandidateSide.Other));
        }

        [Test]
        public void FartherInputDropsTheNote()
        {
            Assert.That(CandidateOwnership.Loser(0.5f, 0.2f), Is.EqualTo(CandidateSide.Current));
        }

        [Test]
        public void TieGoesToTheOtherInput()
        {
            Assert.That(CandidateOwnership.Loser(0.3f, 0.3f), Is.EqualTo(CandidateSide.Current));
        }

        [Test]
        public void TwoUnresolvedCandidatesTieAndTheCurrentInputDrops()
        {
            Assert.That(
                CandidateOwnership.Loser(float.MaxValue, float.MaxValue),
                Is.EqualTo(CandidateSide.Current));
        }

        [Test]
        public void UnresolvedOtherInputLeavesTheNoteToTheCurrentInput()
        {
            Assert.That(
                CandidateOwnership.Loser(0.4f, float.MaxValue),
                Is.EqualTo(CandidateSide.Other));
        }

        [Test]
        public void AHeldNoteStaysWithItsHolderEvenWhenTheOtherFingerIsCloser()
        {
            Assert.That(
                CandidateOwnership.Loser(currentHolds: true, otherHolds: false, currentDistance: 40f, otherDistance: 0.5f),
                Is.EqualTo(CandidateSide.Other));
        }

        [Test]
        public void AHeldNoteIsGivenUpByTheFingerThatDoesNotHoldIt()
        {
            Assert.That(
                CandidateOwnership.Loser(currentHolds: false, otherHolds: true, currentDistance: 0.5f, otherDistance: 40f),
                Is.EqualTo(CandidateSide.Current));
        }

        [Test]
        public void TwoHeldNotesFallBackToTheDistanceRule()
        {
            Assert.That(
                CandidateOwnership.Loser(currentHolds: true, otherHolds: true, currentDistance: 5f, otherDistance: 1f),
                Is.EqualTo(CandidateSide.Current));
        }

        [Test]
        public void TwoUnheldNotesFallBackToTheDistanceRule()
        {
            Assert.That(
                CandidateOwnership.Loser(currentHolds: false, otherHolds: false, currentDistance: 1f, otherDistance: 5f),
                Is.EqualTo(CandidateSide.Other));
        }
    }
}
