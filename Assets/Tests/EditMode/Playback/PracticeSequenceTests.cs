using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSekai.Shared.Playback;

namespace OpenSekai.Tests.EditMode.Playback
{
    public sealed class PracticeSequenceTests
    {
        private static PracticeTemplate Template(params PracticeStep[] steps) => new PracticeTemplate("Tempo", steps);

        [Test]
        public void ReplaysTheSameSelectionAtEachSpeedAndFinishesAfterTheTrailingRest()
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(0.5f), PracticeStep.Rest(2),
                PracticeStep.Play(0.7f), PracticeStep.Rest(2)), 100, 108);

            Assert.That(run.StartSeconds, Is.EqualTo(100));
            Assert.That(run.EndSeconds, Is.EqualTo(108));
            Assert.That(run.Speed, Is.EqualTo(0.5f));
            Assert.That(run.Advance(107.99, 20), Is.False);
            Assert.That(run.Advance(108, 0.4), Is.True);
            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Resting));
            Assert.That(run.RestRemainingSeconds, Is.EqualTo(2));
            Assert.That(run.Advance(108, 1.5), Is.False);
            Assert.That(run.RestRemainingSeconds, Is.EqualTo(0.5));
            Assert.That(run.Advance(108, 0.5), Is.True);
            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Playing));
            Assert.That(run.Speed, Is.EqualTo(0.7f));
            Assert.That(run.StepIndex, Is.EqualTo(2));
            Assert.That(run.Advance(100, 0.01), Is.False);
            Assert.That(run.Advance(109, 1), Is.True);
            Assert.That(run.Advance(108, 2), Is.True);
            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Completed));
            Assert.That(run.Advance(108, 2), Is.False);
        }

        [Test]
        public void OneTemplateCanStartIndependentRunsOnDifferentSelections()
        {
            var slow = PracticeStep.Play(0.25f);
            var template = Template(slow, PracticeStep.Play(0.5f));
            var first = new PracticeSequence(template, 0, 3);
            var another = new PracticeSequence(template, 50, 90);

            first.SetSpeed(1.35f);
            Assert.That(first.Speed, Is.EqualTo(1.35f));
            Assert.That(another.Speed, Is.EqualTo(0.25f));
            Assert.That(first.Advance(2, 100), Is.False);
            Assert.That(first.Advance(3, 0), Is.True);
            Assert.That(first.Speed, Is.EqualTo(0.5f));
            Assert.That(first.StepIndex, Is.EqualTo(1));
            Assert.That(another.Advance(60, 100), Is.False);
            Assert.That(another.StepIndex, Is.Zero);
            Assert.That(slow.Speed, Is.EqualTo(0.25f));
        }

        [Test]
        public void SeekingBackwardInTheCurrentPassDoesNotCompleteIt()
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(0.8f)), 60, 90);
            Assert.That(run.Advance(88, 0.1), Is.False);
            Assert.That(run.Advance(60, 0), Is.False);
            Assert.That(run.Advance(89, 10), Is.False);
            Assert.That(run.Advance(90, 0), Is.True);
            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Completed));
        }

        [Test]
        public void RepeatResetsAnOverriddenSpeedEvenWhenThereIsOnlyOneStep()
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(2)), 2, 4, true);
            run.SetSpeed(0.3f);
            Assert.That(run.Advance(4, 0), Is.True);
            Assert.That(run.StepIndex, Is.Zero);
            Assert.That(run.Speed, Is.EqualTo(2));
            Assert.That(run.Advance(2, 0), Is.False);
        }

        [Test]
        public void LongFrameCarriesRealTimeAcrossAdjacentRestsAndStopsAtTheFirstPlay()
        {
            var run = new PracticeSequence(Template(PracticeStep.Rest(1), PracticeStep.Rest(2),
                PracticeStep.Play(3), PracticeStep.Rest(4)), 10, 12, true);

            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Resting));
            Assert.That(run.Advance(0, 1.5), Is.True);
            Assert.That(run.StepIndex, Is.EqualTo(1));
            Assert.That(run.RestRemainingSeconds, Is.EqualTo(1.5));
            run.SetSpeed(0.25f);
            Assert.That(run.Speed, Is.Zero);
            Assert.That(run.Advance(12, 100), Is.True);
            Assert.That(run.Phase, Is.EqualTo(PracticePhase.Playing));
            Assert.That(run.Speed, Is.EqualTo(3));
            Assert.That(run.Advance(12, 1), Is.True);
            Assert.That(run.Advance(12, 4), Is.True);
            Assert.That(run.StepIndex, Is.Zero);
            Assert.That(run.RestRemainingSeconds, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorOwnsItsSequenceAndDoesNotRetainTheMutableInputList()
        {
            var input = new List<PracticeStep> { PracticeStep.Play(1) };
            var template = new PracticeTemplate("  热身  ", input);
            input[0] = PracticeStep.Rest(1);
            input.Clear();
            Assert.That(template.Name, Is.EqualTo("热身"));
            Assert.That(template.Steps[0].Kind, Is.EqualTo(PracticeStepKind.Play));
        }

        [Test]
        public void ASelectionBeforeTheBgmBeginsCanPlayAcrossZero()
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(1)), -2.25, 0.5);
            Assert.That(run.StartSeconds, Is.EqualTo(-2.25));
            Assert.That(run.Advance(-1, 1.25), Is.False);
            Assert.That(run.Advance(0, 1), Is.False);
            Assert.That(run.Advance(0.5, 0.5), Is.True);
        }

        [TestCase(double.NegativeInfinity, 1)]
        [TestCase(3, 3)]
        [TestCase(4, 2)]
        [TestCase(double.NaN, 1)]
        [TestCase(0, double.NaN)]
        [TestCase(0, double.PositiveInfinity)]
        public void RejectsInvalidSelection(double start, double end)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PracticeSequence(Template(PracticeStep.Play(1)), start, end));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(0.001f)]
        [TestCase(3.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSpeedsCannotEnterATemplateOrChangeARunningPass(float speed)
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(1)), 0, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => PracticeStep.Play(speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.SetSpeed(speed));
            Assert.That(run.Speed, Is.EqualTo(1));
        }

        [TestCase(0.01f)]
        [TestCase(0.6137f)]
        [TestCase(3f)]
        public void DoesNotSnapPositiveSpeedsToAPreset(float speed)
        {
            Assert.That(PracticeStep.Play(speed).Speed, Is.EqualTo(speed));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsInvalidRestDuration(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PracticeStep.Rest(value));
        }

        [Test]
        public void InvalidClockInputDoesNotAdvanceTheRun()
        {
            var run = new PracticeSequence(Template(PracticeStep.Play(1), PracticeStep.Rest(1)), 0, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(double.NaN, 0));
            Assert.That(run.StepIndex, Is.Zero);
            run.Advance(1, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(1, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(1, -1));
            Assert.That(run.RestRemainingSeconds, Is.EqualTo(1));
            Assert.That(run.Advance(1, 0), Is.False);
        }

        [Test]
        public void RejectsUnplayableTemplatesBeforeTheyCanRepeatForever()
        {
            Assert.Throws<ArgumentException>(() => Template());
            Assert.Throws<ArgumentException>(() => Template(PracticeStep.Rest(2)));
            Assert.Throws<ArgumentException>(() => Template(PracticeStep.Play(1), null));
            Assert.Throws<ArgumentNullException>(() => Template(null));
            Assert.Throws<ArgumentException>(() => new PracticeTemplate(" ", new[] { PracticeStep.Play(1) }));
        }
    }
}
