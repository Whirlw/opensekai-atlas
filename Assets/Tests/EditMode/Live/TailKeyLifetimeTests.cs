using NUnit.Framework;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class TailKeyLifetimeTests
    {
        private const float TAIL_TIME = 2f;
        private const float VISIBLE_FROM = 0.5f;
        private const float EPSILON = 0.0001f;

        [Test]
        public void BeforeVisibleFromIsHidden() =>
            Assert.That(PhaseAt(VISIBLE_FROM - EPSILON), Is.EqualTo(TailKeyPhase.Hidden));

        [Test]
        public void ExactlyAtVisibleFromStartsFollowing() =>
            Assert.That(PhaseAt(VISIBLE_FROM), Is.EqualTo(TailKeyPhase.Following));

        [Test]
        public void JustAfterVisibleFromKeepsFollowing() =>
            Assert.That(PhaseAt(VISIBLE_FROM + EPSILON), Is.EqualTo(TailKeyPhase.Following));

        [Test]
        public void JustBeforeTailTimeKeepsFollowing() =>
            Assert.That(PhaseAt(TAIL_TIME - EPSILON), Is.EqualTo(TailKeyPhase.Following));

        [Test]
        public void ExactlyAtTailTimeStartsSlidingOut() =>
            Assert.That(PhaseAt(TAIL_TIME), Is.EqualTo(TailKeyPhase.SlidingOut));

        [Test]
        public void JustAfterTailTimeKeepsSlidingOut() =>
            Assert.That(PhaseAt(TAIL_TIME + EPSILON), Is.EqualTo(TailKeyPhase.SlidingOut));

        [TestCase(1f)]
        [TestCase(10f)]
        [TestCase(1000000f)]
        public void UnjudgedTailKeepsSlidingWithoutTimeout(float elapsed) =>
            Assert.That(PhaseAt(TAIL_TIME + elapsed), Is.EqualTo(TailKeyPhase.SlidingOut));

        [TestCase(-3f, NoteResult.JustPerfect)]
        [TestCase(0.5f, NoteResult.JustPerfect)]
        [TestCase(2f, NoteResult.JustPerfect)]
        [TestCase(3f, NoteResult.JustPerfect)]
        [TestCase(-3f, NoteResult.Good)]
        [TestCase(0.5f, NoteResult.Good)]
        [TestCase(2f, NoteResult.Good)]
        [TestCase(3f, NoteResult.Good)]
        [TestCase(-3f, NoteResult.Miss)]
        [TestCase(0.5f, NoteResult.Miss)]
        [TestCase(2f, NoteResult.Miss)]
        [TestCase(3f, NoteResult.Miss)]
        public void AnyResultRetiresRegardlessOfTime(float now, NoteResult result) =>
            Assert.That(TailKeyLifetime.Of(TAIL_TIME, VISIBLE_FROM, now, result, false),
                Is.EqualTo(TailKeyPhase.Retired));

        [TestCase(-3f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void EndedBarRetiresRegardlessOfTime(float now) =>
            Assert.That(TailKeyLifetime.Of(TAIL_TIME, VISIBLE_FROM, now, NoteResult.None, true),
                Is.EqualTo(TailKeyPhase.Retired));

        [TestCase(NoteResult.JustPerfect)]
        [TestCase(NoteResult.Good)]
        [TestCase(NoteResult.Miss)]
        public void JudgedTailNeverReappearsWhenTimeRewinds(NoteResult result)
        {
            foreach (float now in new[] { TAIL_TIME + 1f, TAIL_TIME, VISIBLE_FROM, VISIBLE_FROM - 1f })
                Assert.That(TailKeyLifetime.Of(TAIL_TIME, VISIBLE_FROM, now, result, false),
                    Is.EqualTo(TailKeyPhase.Retired));
        }

        [TestCase(-1000000f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(1000000f)]
        public void UnjudgedTailWithActiveBarNeverRetiresItself(float now) =>
            Assert.That(PhaseAt(now), Is.Not.EqualTo(TailKeyPhase.Retired));

        [TestCase(-2f)]
        [TestCase(0f)]
        [TestCase(1.5f)]
        public void CallerControlsVisibleFromIndependentlyOfTailTime(float visibleFrom)
        {
            Assert.That(TailKeyLifetime.Of(TAIL_TIME, visibleFrom, visibleFrom - EPSILON,
                NoteResult.None, false), Is.EqualTo(TailKeyPhase.Hidden));
            Assert.That(TailKeyLifetime.Of(TAIL_TIME, visibleFrom, visibleFrom,
                NoteResult.None, false), Is.EqualTo(TailKeyPhase.Following));
        }

        private static TailKeyPhase PhaseAt(float now) =>
            TailKeyLifetime.Of(TAIL_TIME, VISIBLE_FROM, now, NoteResult.None, false);
    }
}
