using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar is played by a finger in its own lane, and by nothing else. The candidate pipes gate on
	/// LongNote.IsJudgment, but the head's candidate and the hold pass reach LongNote.DriveJudgmentFrame
	/// without that gate, and the device traces carry the receipt: a finger at lane 8.85 driving a bar whose
	/// range is -1.50..4.50 (146 of 170 grabs in one run), which gives an untouched bar a finger, keeps it
	/// refreshing and lets a release in another lane grade its end.
	/// </summary>
	public sealed class OutOfLaneFingerTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static LiveTouch Touch(float lane, TouchPhase phase) => new LiveTouch
		{
			fingerId = 1,
			touchId = 1,
			musicTime = 0.02f,
			phase = phase,
			worldPosition = LiveConfig.JudgmentPositions[Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1)],
			delta = Vector2.zero
		};

		private static LongNote Bar()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			bar.SetStateUnnotice(NoteState.Scrolling);
			return bar;
		}

		[Test]
		public void AFingerOutsideTheBarNeverDrivesIt()
		{
			LongNote bar = Bar();

			LiveTouch far = Touch(8.85f, TouchPhase.Moved);
			bool taken = bar.DriveJudgmentFrame(ref far, 8.85f);

			Assert.That(taken, Is.False, "a bar does not answer a finger in another lane");
			Assert.That(bar.HasFinger, Is.False, "and it is not held by it");
		}

		[Test]
		public void AFingerOutsideTheBarNeverTakesItsHead()
		{
			LongNote bar = Bar();

			LiveTouch press = Touch(8.85f, TouchPhase.Began);
			bool judged = bar.Head.Candidate.Judgment(ref press, 8.85f);

			Assert.That(judged, Is.False);
			Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.None), "the head keeps its window for a real press");
		}

		[Test]
		public void AFingerInsideTheBarStillPlaysIt()
		{
			LongNote bar = Bar();

			LiveTouch press = Touch(5f, TouchPhase.Began);
			bool judged = bar.Head.Candidate.Judgment(ref press, 5f);

			Assert.That(judged, Is.True, "the lane's own press still takes the head");
			Assert.That(bar.HasFinger, Is.True);
		}
	}
}
