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
        private const float JustPerfectHalf = 0.5f;

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
            return JudgeWindow.ResultFor(data, offset, isDirection, JustPerfectHalf).Item1;
        }

        [Test]
        public void DeadOnIsJustPerfect()
        {
            Assert.That(Result(Window(), 0f), Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(Result(Window(), JustPerfectHalf), Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(Result(Window(), -JustPerfectHalf), Is.EqualTo(NoteResult.JustPerfect));
        }

        [Test]
        public void JustOutsideTheJustWindowIsPerfectOnBothSides()
        {
            Assert.That(Result(Window(), 0.51f), Is.EqualTo(NoteResult.Perfect));
            Assert.That(Result(Window(), -0.51f), Is.EqualTo(NoteResult.Perfect));
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
            (NoteResult result, NoteResultDescription description) = JudgeWindow.ResultFor(Window(), 1f, false, JustPerfectHalf);

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
            Assert.That(JudgeWindow.DescriptionFor(0.2f, JustPerfectHalf), Is.EqualTo(NoteResultDescription.Just));
            Assert.That(JudgeWindow.DescriptionFor(2f, JustPerfectHalf), Is.EqualTo(NoteResultDescription.Fast));
            Assert.That(JudgeWindow.DescriptionFor(-2f, JustPerfectHalf), Is.EqualTo(NoteResultDescription.Late));
        }
    }
}
