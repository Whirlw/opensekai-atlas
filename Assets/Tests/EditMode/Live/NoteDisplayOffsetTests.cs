using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class NoteDisplayOffsetTests
    {
        [Test]
        public void OffsetScalesWithTheEffectiveSpeed()
        {
            bool negative = false;

            Assert.That(NoteDisplayOffset.Resolve(2f, 1.5f, 2f, ref negative), Is.EqualTo(2f / 3f).Within(1e-5f));
            Assert.That(negative, Is.False);
        }

        [Test]
        public void NegativeEffectiveSpeedUsesTheMagnitudeAndFlagsTheNote()
        {
            bool negative = false;

            Assert.That(NoteDisplayOffset.Resolve(2f, 1.5f, -2f, ref negative), Is.EqualTo(2f / 3f).Within(1e-5f));
            Assert.That(negative, Is.True);
        }

        [Test]
        public void ZeroChartSpeedReturnsZeroAndLeavesTheFlagAlone()
        {
            bool negative = true;

            Assert.That(NoteDisplayOffset.Resolve(2f, 0f, 3f, ref negative), Is.EqualTo(0f));
            Assert.That(negative, Is.True, "a neutral speed says nothing about the note, so the old flag survives");
        }

        [Test]
        public void ZeroNoteSpeedReturnsZeroWithoutFlagging()
        {
            bool negative = true;

            Assert.That(NoteDisplayOffset.Resolve(2f, 1.5f, 0f, ref negative), Is.EqualTo(0f));
            Assert.That(negative, Is.False);
        }

        [Test]
        public void AnExtremeSpeedSpikeIsDividedOutInFull()
        {
            // Upstream behaviour, pinned because it looks like a bug and is not one: the chart uses a huge
            // speed value to make notes appear abruptly, and the display offset really does become tiny.
            // The clamp that was tried here did not fix the reported flash (the chart never produced a
            // collapsed offset), so nothing clamps it - do not "fix" this by flooring it.
            bool negative = false;

            Assert.That(NoteDisplayOffset.Resolve(1f, 799f, 1f, ref negative), Is.EqualTo(1f / 799f).Within(1e-6f));
            Assert.That(negative, Is.False);
        }
    }
}
