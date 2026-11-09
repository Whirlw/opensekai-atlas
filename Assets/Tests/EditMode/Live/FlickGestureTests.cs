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
        private const float Frame = 1f / 60f;
        private const float Threshold = 3f; // pixels per reference frame

        [Test]
        public void SwipeBelowTheThresholdDoesNotCount()
        {
            bool updated = FlickGesture.TryUpdate(
                new Vector2(0f, Threshold - 0.1f), Frame, NoteDirection.Default, Threshold, out bool trueDirection);

            Assert.That(updated, Is.False);
            Assert.That(trueDirection, Is.False);
        }

        [Test]
        public void SwipeAboveTheThresholdCountsForANoteWithoutDirection()
        {
            bool updated = FlickGesture.TryUpdate(
                new Vector2(0f, Threshold + 0.5f), Frame, NoteDirection.Default, Threshold, out bool trueDirection);

            Assert.That(updated, Is.True);
            Assert.That(trueDirection, Is.True, "an undirected note accepts any swipe");
        }

        [Test]
        public void ZeroDeltaIsRejectedEvenWithATinyThreshold()
        {
            Assert.That(FlickGesture.TryUpdate(Vector2.zero, Frame, NoteDirection.Default, 0f, out _), Is.False);
        }

        [Test]
        public void UpwardSwipeSatisfiesEveryDirection()
        {
            Vector2 up = new Vector2(0f, Threshold * 4f);

            foreach (NoteDirection direction in new[] { NoteDirection.Default, NoteDirection.Left, NoteDirection.Right })
            {
                FlickGesture.TryUpdate(up, Frame, direction, Threshold, out bool trueDirection);
                Assert.That(trueDirection, Is.True, $"straight up should satisfy {direction}");
            }
        }

        [Test]
        public void LeftFlickRejectsASwipeToTheRight()
        {
            FlickGesture.TryUpdate(new Vector2(-Threshold * 4f, 0f), Frame, NoteDirection.Left, Threshold, out bool left);
            FlickGesture.TryUpdate(new Vector2(Threshold * 4f, 0f), Frame, NoteDirection.Right, Threshold, out bool right);
            FlickGesture.TryUpdate(new Vector2(Threshold * 4f, 0f), Frame, NoteDirection.Left, Threshold, out bool wrongWay);

            Assert.That(left, Is.True);
            Assert.That(right, Is.True);
            Assert.That(wrongWay, Is.False, "the cone is 180 degrees wide, not 360");
        }

        [Test]
        public void DiagonalSwipeSatisfiesAHorizontalFlick()
        {
            Vector2 diagonal = new Vector2(-Threshold * 3f, Threshold * 3f);

            FlickGesture.TryUpdate(diagonal, Frame, NoteDirection.Left, Threshold, out bool left);
            FlickGesture.TryUpdate(diagonal, Frame, NoteDirection.Right, Threshold, out bool right);

            Assert.That(left, Is.True);
            Assert.That(right, Is.True, "a 45 degree diagonal sits exactly on the cone edge");
        }

        [Test]
        public void DeltaIsNormalisedToSixtyFramesPerSecond()
        {
            // The same physical speed sampled at 120 fps yields half the delta per frame; after
            // normalisation both must clear the threshold, and both must fail when halved.
            Vector2 atSixty = new Vector2(0f, Threshold * 1.5f);
            Vector2 atOneTwenty = atSixty * 0.5f;

            FlickGesture.TryUpdate(atSixty, Frame, NoteDirection.Default, Threshold, out bool sixty);
            FlickGesture.TryUpdate(atOneTwenty, Frame * 0.5f, NoteDirection.Default, Threshold, out bool oneTwenty);
            FlickGesture.TryUpdate(atOneTwenty, Frame, NoteDirection.Default, Threshold, out bool slow);

            Assert.That(sixty, Is.True);
            Assert.That(oneTwenty, Is.True, "a faster sample rate must not make flicks harder");
            Assert.That(slow, Is.False);
        }

        [Test]
        public void NonPositiveDeltaTimeLeavesTheDeltaAlone()
        {
            Vector2 raw = new Vector2(0f, Threshold * 2f);

            Assert.That(FlickGesture.ToReferenceFrame(raw, 0f), Is.EqualTo(raw));
        }
    }
}
