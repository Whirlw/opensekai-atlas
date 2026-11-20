using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Trace flicks have distinct hand registration and grading windows.
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

		[TestCase(false, -0.110f, false)]
		[TestCase(true, -0.120f, true)]
		[TestCase(false, 0.124f, true)]
		[TestCase(false, 0.126f, false)]
		[TestCase(true, 0.138f, true)]
		[TestCase(true, 0.142f, false)]
		public void TraceFlickRegistersWithinItsOwnWindow(bool tail, float offset, bool accepts)
		{
			var note = new FrictionFlickNote(Info(1f), 1, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Left, 0.0833f, 0f);
			if (tail) new LongNote().AddNoteListAndSetupChildNote(note);
			note.Excute(Info(0.9f), 0.5f);
			var swipe = Touch(1f + offset, new Vector2(-240f, 0f), TouchPhase.Moved);
			Assert.That(note.ReadNextRushHeadSample(ref swipe, 5f, swipe.musicTime), Is.EqualTo(accepts));
			if (!accepts) return;
			note.SettleNextRushHead(swipe.musicTime, true);
			note.Excute(Info(System.Math.Max(1f, swipe.musicTime)), 0.5f);
			Assert.That(note.Result, Is.EqualTo(offset > 0.125f ? NoteResult.Miss : NoteResult.Perfect));
		}

		[Test]
		public void ACaughtNoteWithNoFlickAtAllStillMisses()
		{
			// The counterpart: catching alone is not a flick. With nothing recorded the note must still miss.
			var note = new FrictionFlickNote(Info(1f), 2, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Left, 0.0833f, 0f);
			// A note only answers input once it is on screen: the state machine starts at Wait.
			note.Excute(Info(0.9f), 0.5f);
			LiveTouch hold = Touch(1.118f, Vector2.zero, TouchPhase.Stationary);
			note.Judgment(ref hold, 5f);

			note.Excute(Info(1.145f), 0.5f);
			note.Excute(Info(1.30f), 0.5f);

			Assert.That(note.Result, Is.EqualTo(NoteResult.Miss), "no flick is still a miss");
		}
	}
}
