using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    [TestFixture]
    public class JudgmentScheduleTests
    {
        /// <summary>
        /// A pass that reports canned outcomes per candidate and remembers what it was asked to judge.
        /// </summary>
        private struct FakePass : IJudgmentPass
        {
            public JudgmentSchedule.Outcome[] Outcomes;

            public int Visited;

            public int ConsumedIndex;

            public JudgmentSchedule.Outcome Judge(int candidateIndex)
            {
                Visited += 1;
                return Outcomes[candidateIndex];
            }

            public void Consumed(int candidateIndex)
            {
                ConsumedIndex = candidateIndex + 1;
            }
        }

        private static JudgmentSchedule.Outcome Waiting()
        {
            return new JudgmentSchedule.Outcome(consumesSlot: false);
        }

        private static JudgmentSchedule.Outcome Judged()
        {
            return new JudgmentSchedule.Outcome(consumesSlot: true);
        }

        private static JudgmentSchedule.Outcome Finished()
        {
            return new JudgmentSchedule.Outcome(consumesSlot: true);
        }

        private static int Run(JudgmentSchedule.Outcome[] outcomes, out FakePass pass)
        {
            pass = new FakePass();
            pass.Outcomes = outcomes;
            return JudgmentSchedule.Run(outcomes.Length, ref pass);
        }

        [Test]
        public void AWaitingNoteDoesNotConsumeThePress()
        {
            Assert.That(Waiting().ConsumesSlot, Is.False);
        }

        [Test]
        public void AJudgedNoteConsumesThePress()
        {
            Assert.That(Judged().ConsumesSlot, Is.True);
        }

        [Test]
        public void AFinishedNoteConsumesThePress()
        {
            Assert.That(Finished().ConsumesSlot, Is.True);
        }

        [Test]
        public void AWaitingNoteLetsTheNextCandidateBeJudged()
        {
            // This is the regression: a flick or rub that is still waiting for its gesture used to
            // stop the loop, so every note queued behind it on the same finger was never judged.
            FakePass pass;
            int consumed = Run(new[] { Waiting(), Judged() }, out pass);

            Assert.That(consumed, Is.EqualTo(1));
            Assert.That(pass.Visited, Is.EqualTo(2));
        }

        [Test]
        public void TwoWaitingNotesBothGetTheirFrame()
        {
            FakePass pass;
            int consumed = Run(new[] { Waiting(), Waiting(), Finished() }, out pass);

            Assert.That(consumed, Is.EqualTo(2));
            Assert.That(pass.Visited, Is.EqualTo(3));
        }

        [Test]
        public void TheFirstConsumingCandidateStopsTheWalk()
        {
            FakePass pass;
            int consumed = Run(new[] { Judged(), Judged(), Judged() }, out pass);

            Assert.That(consumed, Is.EqualTo(0));
            Assert.That(pass.Visited, Is.EqualTo(1), "a spent press must not judge the notes behind it");
        }

        [Test]
        public void NoCandidateConsumesWhenAllOfThemWait()
        {
            FakePass pass;
            int consumed = Run(new[] { Waiting(), Waiting() }, out pass);

            Assert.That(consumed, Is.EqualTo(-1));
            Assert.That(pass.Visited, Is.EqualTo(2));
            Assert.That(pass.ConsumedIndex, Is.Zero, "the consuming callback must not run");
        }

        [Test]
        public void AnEmptyCandidateListIsFine()
        {
            FakePass pass = new FakePass();
            int consumed = JudgmentSchedule.Run(0, ref pass);

            Assert.That(consumed, Is.EqualTo(-1));
            Assert.That(pass.Visited, Is.Zero);
        }

        [Test]
        public void TheConsumingCallbackRunsOnlyForTheWinner()
        {
            FakePass pass;
            Run(new[] { Waiting(), Finished(), Finished() }, out pass);

            Assert.That(pass.ConsumedIndex, Is.EqualTo(2));
        }
    }
}
