using NUnit.Framework;
using OpenSekai.Shared.Scoring;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Pins the judge ladder: window order, the direction-miss rule, and the early/late description.
    /// Offsets are positive when the player is early.
    /// </summary>
    public sealed class JudgeWindowTests
    {
        private const float ON_TIME_WINDOW_HALF = 0.5f;

        /// <summary>A normal note: 2.5 / 5 / 6.5 / 7.5 half-frames, the shipped table.</summary>
        private static JudgeTimeData Window(
            float perfectBefore = 2.5f,
            float perfectAfter = 2.5f,
            float greatBefore = 5f,
            float greatAfter = 5f)
        {
            return new JudgeTimeData(
                JudgeFrameType.Normal,
                perfectBefore, perfectAfter,
                greatBefore, greatAfter,
                6.5f, 6.5f,
                7.5f, 7.5f);
        }

        private static NoteResult Result(JudgeTimeData data, float offset, bool isDirection = true)
        {
            return JudgeWindow.ResultFor(data, offset, isDirection, ON_TIME_WINDOW_HALF).Item1;
        }

        [Test]
        public void AdmissionAndExpiryUseTheSameAsymmetricBounds()
        {
            var window = new JudgeTimeData(JudgeFrameType.Normal, 1f, 1f, 2f, 2f, 3f, 3f, 4f, 5f);
            Assert.That(JudgeWindow.Accepts(window, 3.99f), Is.True);
            Assert.That(JudgeWindow.Accepts(window, 4f), Is.False);
            Assert.That(JudgeWindow.Accepts(window, -4.99f), Is.True);
            Assert.That(JudgeWindow.Accepts(window, -5f), Is.False);
            Assert.That(JudgeWindow.IsPast(window, -5f), Is.False);
            Assert.That(JudgeWindow.IsPast(window, -5.01f), Is.True);
        }

        [Test]
        public void CenteredInputsArePerfect()
        {
            Assert.That(Result(Window(), 0f), Is.EqualTo(NoteResult.Perfect));
            Assert.That(Result(Window(), ON_TIME_WINDOW_HALF), Is.EqualTo(NoteResult.Perfect));
            Assert.That(Result(Window(), -ON_TIME_WINDOW_HALF), Is.EqualTo(NoteResult.Perfect));
        }

        [Test]
        public void PerfectGradeExtendsBeyondTheOnTimeDescriptionWindow()
        {
            Assert.That(Result(Window(), 0.51f), Is.EqualTo(NoteResult.Perfect));
            Assert.That(Result(Window(), -0.51f), Is.EqualTo(NoteResult.Perfect));
        }

        [Test]
        public void OnTimeDescriptionDoesNotExpandThePerfectGradeWindow()
        {
            var grade = JudgeWindow.ResultFor(Window(perfectAfter: 0.25f), -0.4f, true, ON_TIME_WINDOW_HALF);

            Assert.That(grade, Is.EqualTo((NoteResult.Great, NoteResultDescription.Just)));
        }

        [Test]
        public void TheWindowsAreNotSymmetricWhenTheTableSaysSo()
        {
            // The "before" value is the early bound (positive offset), the "after" value the late bound.
            JudgeTimeData longEnd = Window(perfectBefore: 3.5f, perfectAfter: 4f);

            Assert.That(Result(longEnd, 3.4f), Is.EqualTo(NoteResult.Perfect), "inside the early bound");
            Assert.That(Result(longEnd, 3.6f), Is.EqualTo(NoteResult.Great), "past the early bound");
            Assert.That(Result(longEnd, -3.9f), Is.EqualTo(NoteResult.Perfect), "inside the late bound");
            Assert.That(Result(longEnd, -4.1f), Is.EqualTo(NoteResult.Great), "past the late bound");
        }

        [Test]
        public void GreatGoodBadAndMissFallThroughInOrder()
        {
            JudgeTimeData data = Window();

            Assert.That(Result(data, 4.9f), Is.EqualTo(NoteResult.Great));
            Assert.That(Result(data, 5.1f), Is.EqualTo(NoteResult.Good));
            Assert.That(Result(data, 6.6f), Is.EqualTo(NoteResult.Bad));
            Assert.That(Result(data, 7.6f), Is.EqualTo(NoteResult.Miss));
            Assert.That(Result(data, -7.6f), Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void WrongDirectionInsideThePerfectWindowIsAGreatWithTheFlickMissReason()
        {
            (NoteResult result, NoteResultDescription description) = JudgeWindow.ResultFor(Window(), 1f, false, ON_TIME_WINDOW_HALF);

            Assert.That(result, Is.EqualTo(NoteResult.Great));
            Assert.That(description, Is.EqualTo(NoteResultDescription.FlickMiss));
        }

        [Test]
        public void WrongDirectionOutsideThePerfectWindowStillFollowsTheLadder()
        {
            Assert.That(Result(Window(), 6f, false), Is.EqualTo(NoteResult.Good));
            Assert.That(Result(Window(), 9f, false), Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void DescriptionSeparatesEarlyFromLate()
        {
            Assert.That(JudgeWindow.DescriptionFor(0.2f, ON_TIME_WINDOW_HALF), Is.EqualTo(NoteResultDescription.Just));
            Assert.That(JudgeWindow.DescriptionFor(2f, ON_TIME_WINDOW_HALF), Is.EqualTo(NoteResultDescription.Fast));
            Assert.That(JudgeWindow.DescriptionFor(-2f, ON_TIME_WINDOW_HALF), Is.EqualTo(NoteResultDescription.Late));
        }
    }
}
