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
    }
}