using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// A note only spends the finger's single per-frame judgment slot when it actually finished in that
    /// call. "Finished" means it produced a result or reached Done — nothing else. These cases pin that
    /// down per note type, because a note that merely arms itself (a held bar, a rub still approaching,
    /// a flick waiting for the swipe) must leave the frame to whatever is queued behind it.
    /// </summary>
    [TestFixture]
    public class NotePressDispositionTests
    {
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        /// <summary>A press spends the finger's frame once the note it landed on actually finished.</summary>
        private static bool Finished(NoteBase note)
        {
            return note.State == NoteState.Done || note.Result != NoteResult.None;
        }

        private static LiveTouch Touch(TouchPhase phase, float musicTime, int fingerId = 1)
        {
            return new LiveTouch(fingerId, fingerId, UnityEngine.Vector2.zero, UnityEngine.Vector3.zero, phase, musicTime);
        }

        [Test]
        public void AFreshNoteDoesNotCloseTheSlot()
        {
            var note = new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);

            Assert.That(Finished(note), Is.False);
        }

        [Test]
        public void ANormalNoteClosesTheSlotWhenItFinishes()
        {
            var note = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            LiveTouch touch = Touch(TouchPhase.Began, 1f);

            Assert.That(note.Judgment(ref touch, 4.5f), Is.True);
            Assert.That(Finished(note), Is.True);
        }

        [Test]
        public void ANormalNoteDoesNotCloseTheSlotForAPressThatIsNotABegan()
        {
            var note = new NormalNote(Info(1f), 3, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            LiveTouch touch = Touch(TouchPhase.Moved, 1f);

            Assert.That(note.Judgment(ref touch, 4.5f), Is.False);
            Assert.That(Finished(note), Is.False);
        }

        [Test]
        public void ARubNoteKeepsTheSlotOpenWhileItArmsItself()
        {
            var rub = new FrictionNote(Info(2f), 4, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);
            rub.Excute(Info(1.9f), 0.5f);
            LiveTouch touch = Touch(TouchPhase.Began, 1.9f);

            Assert.That(rub.Progress, Is.LessThan(1f));
            Assert.That(rub.Judgment(ref touch, 4.5f), Is.False);
            Assert.That(Finished(rub), Is.False, "an armed rub must not spend the finger's frame");
        }

        [Test]
        public void ASlideDoesNotCloseTheSlotBeforeItsLateWindow()
        {
            var slide = new ConnectionNote(Info(1f), 5, 4, 5, NoteCategory.Connection, NoteType.Default, 1, 0);
            LiveTouch touch = Touch(TouchPhase.Began, 0.5f);

            Assert.That(slide.Judgment(ref touch, 4.5f), Is.True);
            Assert.That(Finished(slide), Is.False);
        }

        [Test]
        public void AFlickKeepsTheSlotOpenWhileItWaitsForTheSwipe()
        {
            var flick = new FlickNote(Info(1f), 6, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0);
            flick.Excute(Info(0.9f), 0.5f);
            LiveTouch touch = Touch(TouchPhase.Began, 0.9f);

            Assert.That(flick.Judgment(ref touch, 4.5f), Is.False);
            Assert.That(Finished(flick), Is.False);
        }

		[Test]
		public void AnInteriorPointNeverEntersThePressChannel()
		{
			var point = new LongHoldCombo(Info(1f), NoteType.Default, 1f, 0f);

			// The guarantee is structural now, not a flag: an interior point is not a press-step note,
			// so it is never collected as a candidate and cannot spend anyone's frame.
			Assert.That(LiveCandidatePipeline.IsPressStep(point), Is.False,
				"an interior point takes no slot, so a bar's body never blocks the note behind it");
		}
    }
}