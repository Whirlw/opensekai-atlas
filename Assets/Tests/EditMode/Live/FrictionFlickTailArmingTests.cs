using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A friction flick is caught by holding into the line and settled by the flick. When the catch happens at
	/// the edge of the late window, the frame that reaches the line is the same frame the base class closes
	/// the window - and the deferred settle used to run after that, so a caught note with a recorded swipe
	/// still missed. Device log live-20261113-152906.log: childState=Input at now=19.824, Last at 19.830 and
	/// res=Miss at 19.836 for t=19.688, a single combo break in an otherwise perfect run.
	/// </summary>
	public sealed class FrictionFlickTailArmingTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static LiveTouch Touch(float musicTime, Vector2 delta, TouchPhase phase) => new LiveTouch
		{
			fingerId = 1,
			touchId = 1,
			musicTime = musicTime,
			phase = phase,
			worldPosition = LiveConfig.JudgmentPositions[5],
			delta = delta
		};

		[Test]
		public void ACatchAtTheEdgeOfTheLateWindowStillSettlesItsRecordedFlick()
		{
			// Left is asked for; the player swipes right, so the distance is recorded but the direction is not -
			// the note has to settle from its recorded input when it reaches the line, not fall through to the
			// base class's Miss.
			var note = new FrictionFlickNote(Info(1f), 1, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Left, 0.0833f, 0f);

			// The catch: a finger already in the lane, no travel, 138 ms after the note's own time.
			// A note only answers input once it is on screen: the state machine starts at Wait.
			note.Excute(Info(0.9f), 0.5f);
			LiveTouch hold = Touch(1.138f, Vector2.zero, TouchPhase.Stationary);
			Assert.That(note.Judgment(ref hold, 5f), Is.False, "a still finger catches the note, it does not grade it");
			Assert.That(note.State, Is.EqualTo(NoteState.Input));

			// The recorded swipe: travel in the opposite direction for this note (dot < 0, so not the asked way).
			LiveTouch swipe = Touch(1.140f, new Vector2(240f, 0f), TouchPhase.Moved);
			note.Judgment(ref swipe, 5f);
			Assert.That(note.Result, Is.EqualTo(NoteResult.None), "the deferred settle has not run yet");

			// The frame that reaches the line is also the frame where the late window closes.
			note.Excute(Info(1.145f), 0.5f);

			Assert.That(note.State, Is.EqualTo(NoteState.Done), "a caught note with a recorded flick settles");
			Assert.That(note.Result, Is.Not.EqualTo(NoteResult.None), "and it is graded, not missed");
		}

		[Test]
		public void ACaughtNoteWithNoFlickAtAllStillMisses()
		{
			// The counterpart: catching alone is not a flick. With nothing recorded the note must still miss.
			var note = new FrictionFlickNote(Info(1f), 2, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Left, 0.0833f, 0f);
			// A note only answers input once it is on screen: the state machine starts at Wait.
			note.Excute(Info(0.9f), 0.5f);
			LiveTouch hold = Touch(1.138f, Vector2.zero, TouchPhase.Stationary);
			note.Judgment(ref hold, 5f);

			note.Excute(Info(1.145f), 0.5f);
			note.Excute(Info(1.30f), 0.5f);

			Assert.That(note.Result, Is.EqualTo(NoteResult.Miss), "no flick is still a miss");
		}
	}
}
