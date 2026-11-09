using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class HoldInputMatchingTests
    {
        [Test]
        public void NarrowHoldCanDisplaceWideHoldToAnOtherwiseIdleFinger()
        {
            var assignment = new[] { -1, 0 };
            Assert.That(HoldInputMatching.TryAssign(1, 2, (note, input) => note == 0 || input == 1,
                assignment, new bool[2]), Is.True);
            Assert.That(assignment, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void FreeEligibleInputIsPreferredToChangingAnExistingOwner()
        {
            var assignment = new[] { 0, -1 };
            Assert.That(HoldInputMatching.TryAssign(1, 2, (note, input) => true,
                assignment, new bool[2]), Is.True);
            Assert.That(assignment, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void FailedExchangeDoesNotDisplaceTheExistingHold()
        {
            var assignment = new[] { -1, 0 };
            Assert.That(HoldInputMatching.TryAssign(1, 2, (note, input) => input == 1,
                assignment, new bool[2]), Is.False);
            Assert.That(assignment, Is.EqualTo(new[] { -1, 0 }));
        }

        [Test]
        public void ExchangeCanFollowMoreThanOneOccupiedInput()
        {
            var assignment = new[] { 0, 1, -1 };
            bool[,] eligible = { { true, true, false }, { false, true, true }, { true, false, false } };
            Assert.That(HoldInputMatching.TryAssign(2, 3, (note, input) => eligible[note, input],
                assignment, new bool[3]), Is.True);
            Assert.That(assignment, Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void NoInputCannotSustainAHold()
        {
            Assert.That(HoldInputMatching.TryAssign(0, 0, (note, input) => true,
                new int[0], new bool[0]), Is.False);
        }

        [Test]
        public void AnIneligibleFingerIsNeverAssigned()
        {
            var assignment = new[] { -1, -1 };
            Assert.That(HoldInputMatching.TryAssign(0, 2, (note, input) => false,
                assignment, new bool[2]), Is.False);
            Assert.That(assignment, Is.EqualTo(new[] { -1, -1 }));
        }
    }
}
