using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Characterises the swipe test: distance threshold after frame normalisation, plus the direction cone.
    /// FlickNote and FrictionFlickNote both delegate here, so these assertions cover both.
    /// </summary>
    public sealed class FlickGestureTests
    {
        private const float FRAME = 1f / 60f;
        private const float THRESHOLD = 3f; // pixels per reference frame

        [Test]
        public void SwipeBelowTheThresholdDoesNotCount()
        {
            bool updated = FlickGesture.TryUpdate(
                new Vector2(0f, THRESHOLD - 0.1f), FRAME, NoteDirection.Default, THRESHOLD, out bool trueDirection);

            Assert.That(updated, Is.False);
            Assert.That(trueDirection, Is.False);
        }

        [Test]
        public void SwipeAboveTheThresholdCountsForANoteWithoutDirection()
        {
            bool updated = FlickGesture.TryUpdate(
                new Vector2(0f, THRESHOLD + 0.5f), FRAME, NoteDirection.Default, THRESHOLD, out bool trueDirection);

            Assert.That(updated, Is.True);
            Assert.That(trueDirection, Is.True, "an undirected note accepts any swipe");
        }

        [Test]
        public void ZeroDeltaIsRejectedEvenWithATinyThreshold()
        {
            Assert.That(FlickGesture.TryUpdate(Vector2.zero, FRAME, NoteDirection.Default, 0f, out _), Is.False);
        }

        [Test]
        public void UpwardSwipeSatisfiesEveryDirection()
        {
            Vector2 up = new Vector2(0f, THRESHOLD * 4f);

            foreach (NoteDirection direction in new[] { NoteDirection.Default, NoteDirection.Left, NoteDirection.Right })
            {
                FlickGesture.TryUpdate(up, FRAME, direction, THRESHOLD, out bool trueDirection);
                Assert.That(trueDirection, Is.True, $"straight up should satisfy {direction}");
            }
        }

        [Test]
        public void LeftFlickRejectsASwipeToTheRight()
        {
            FlickGesture.TryUpdate(new Vector2(-THRESHOLD * 4f, 0f), FRAME, NoteDirection.Left, THRESHOLD, out bool left);
            FlickGesture.TryUpdate(new Vector2(THRESHOLD * 4f, 0f), FRAME, NoteDirection.Right, THRESHOLD, out bool right);
            FlickGesture.TryUpdate(new Vector2(THRESHOLD * 4f, 0f), FRAME, NoteDirection.Left, THRESHOLD, out bool wrongWay);

            Assert.That(left, Is.True);
            Assert.That(right, Is.True);
            Assert.That(wrongWay, Is.False, "the cone is 180 degrees wide, not 360");
        }

        [Test]
        public void DiagonalSwipeSatisfiesAHorizontalFlick()
        {
            Vector2 diagonal = new Vector2(-THRESHOLD * 3f, THRESHOLD * 3f);

            FlickGesture.TryUpdate(diagonal, FRAME, NoteDirection.Left, THRESHOLD, out bool left);
            FlickGesture.TryUpdate(diagonal, FRAME, NoteDirection.Right, THRESHOLD, out bool right);

            Assert.That(left, Is.True);
            Assert.That(right, Is.True, "a 45 degree diagonal sits exactly on the cone edge");
        }

        [Test]
        public void DeltaIsNormalisedToSixtyFramesPerSecond()
        {
            // The same physical speed sampled at 120 fps yields half the delta per frame; after
            // normalisation both must clear the threshold, and both must fail when halved.
            Vector2 atSixty = new Vector2(0f, THRESHOLD * 1.5f);
            Vector2 atOneTwenty = atSixty * 0.5f;

            FlickGesture.TryUpdate(atSixty, FRAME, NoteDirection.Default, THRESHOLD, out bool sixty);
            FlickGesture.TryUpdate(atOneTwenty, FRAME * 0.5f, NoteDirection.Default, THRESHOLD, out bool oneTwenty);
            FlickGesture.TryUpdate(atOneTwenty, FRAME, NoteDirection.Default, THRESHOLD, out bool slow);

            Assert.That(sixty, Is.True);
            Assert.That(oneTwenty, Is.True, "a faster sample rate must not make flicks harder");
            Assert.That(slow, Is.False);
        }

        [Test]
        public void NonPositiveDeltaTimeLeavesTheDeltaAlone()
        {
            Vector2 raw = new Vector2(0f, THRESHOLD * 2f);

            Assert.That(FlickGesture.ToReferenceFrame(raw, 0f), Is.EqualTo(raw));
        }
    }
}
