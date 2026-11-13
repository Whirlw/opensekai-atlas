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
        public void AStoppedChartKeepsTheOffsetTheNoteAlreadyHad()
        {
            // A chart that ramps its scroll speed through zero and holds it (deinos-phainein, tick 71520,
            // 47.07..47.37 s) used to turn into an offset of zero. Zero is not a slow note: every sample
            // clamps onto the judgement line and NoteLineView draws no mesh at all, so the bars in play
            // vanished for the 19 frames of the stop and then came back. A stop keeps the speed the note
            // had, and says nothing about its direction.
            bool negative = true;

            Assert.That(NoteDisplayOffset.Resolve(2f, 0f, 3f, ref negative, previousOffset: 30f), Is.EqualTo(30f));
            Assert.That(negative, Is.True, "a neutral speed says nothing about the note, so the old flag survives");
        }

        [Test]
        public void AStoppedChartLetsANoteThatWasNeverDrawnKeepAWholeWindow()
        {
            bool negative = true;

            Assert.That(NoteDisplayOffset.Resolve(2f, 0f, 3f, ref negative), Is.EqualTo(2f),
                "a note with no offset yet scrolls one screen per display window instead of landing on the line");
        }

        [Test]
        public void AZeroWindowIsTheOnlyZeroOffsetLeft()
        {
            bool negative = false;

            Assert.That(NoteDisplayOffset.Resolve(0f, 0f, 3f, ref negative), Is.EqualTo(0f));
            Assert.That(NoteDisplayOffset.Resolve(0f, 1.5f, 1f, ref negative), Is.EqualTo(0f));
        }

        [Test]
        public void AZeroNoteSpeedAlsoKeepsTheOffsetInsteadOfCollapsing()
        {
            bool negative = true;

            Assert.That(NoteDisplayOffset.Resolve(2f, 1.5f, 0f, ref negative, previousOffset: 1.25f), Is.EqualTo(1.25f));
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
