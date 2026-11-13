using System;
using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Test16, note for note: two bars start at the same moment, one in lane 0..3 and one in lane 9..11 whose
	/// end note is a hidden one two milliseconds later. The report is order dependent - press the short one
	/// first and everything is fine, press the long one first and the short one always misses - so the two
	/// presses land on the same frame and the order is the variable under test.
	/// </summary>
	public sealed class SimultaneousBarHeadsTests
	{
		private const float Frame = 1f / 60f;
		private const double InputBase = 10d;

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}

		private static MusicScore ScoreOf(params NoteBase[] notes) =>
			new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes);

		/// <summary>The left bar: lane 0..3, end note a second later, exactly like Test16's note 30.</summary>
		private static LongNote LongBar()
		{
			var bar = new LongNote(Info(0f), 30, 0, 3, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(1f), 35, 8, 11, NoteCategory.Normal, 1f, NoteType.Default, 0f));
			return bar;
		}

		/// <summary>The short bar: lane 9..11, end note two milliseconds later, exactly like Test16's note 31.</summary>
		private static LongNote ShortBar()
		{
			var bar = new LongNote(Info(0f), 31, 9, 11, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new FrictionHideNote(Info(0.002f), 34, 9, 11, NoteCategory.Hidden, null, NoteType.Default, 1f, 0));
			return bar;
		}

		[Test]
		public void ALatePressOnAShortBarStillJudgesItsHead()
		{
			// The short bar's end note is two milliseconds after its head, so by the time a press lands 30 ms
			// late the bar would long since have ended. Its head still has its own window, and it must be
			// answerable: the head is an instant key on its own clock, not a part of the bar's life.
			using var harness = new LiveHarness();
			LongNote longBar = LongBar();
			LongNote shortBar = ShortBar();
			harness.Arrange(ScoreOf(longBar, shortBar));

			harness.Step(0f, InputBase);
			harness.Step(0.03f, InputBase + 0.03f,
				LiveHarness.Touch(2, LanePosition(10f), InputBase + 0.03f, InputTouchPhase.Began));

			Assert.That(shortBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"a press inside the head's window judges it, whatever the bar's own end is doing");
		}

		[Test]
		public void ALatePressOnAShortBarIsJudgedEvenAfterTheBarItselfHasEnded()
		{
			// Same, but the press is late enough that the bar has already finished and gone back to the pool
			// path: the head must still answer, because its window is 125 ms.
			using var harness = new LiveHarness();
			LongNote longBar = LongBar();
			LongNote shortBar = ShortBar();
			harness.Arrange(ScoreOf(longBar, shortBar));

			harness.Step(0f, InputBase);
			for (int frame = 1; frame <= 6; frame++)
			{
				harness.Step(frame * Frame, InputBase + frame * Frame);
			}

			harness.Step(0.1f, InputBase + 0.1f,
				LiveHarness.Touch(2, LanePosition(10f), InputBase + 0.1f, InputTouchPhase.Began));

			Assert.That(shortBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"the head answers inside its window even though the bar is over");
		}

		[Test]
		public void PressingTheLongBarFirstStillJudgesTheShortBar()
		{
			using var harness = new LiveHarness();
			LongNote longBar = LongBar();
			LongNote shortBar = ShortBar();
			harness.Arrange(ScoreOf(longBar, shortBar));

			// Both presses on one frame, long bar first - the reported order.
			harness.Step(0f, InputBase,
				LiveHarness.Touch(1, LanePosition(1.5f), InputBase, InputTouchPhase.Began),
				LiveHarness.Touch(2, LanePosition(10f), InputBase, InputTouchPhase.Began));

			Assert.That(longBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"the long bar's head is pressed first and is judged");
			Assert.That(shortBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"the short bar's head is pressed on the same frame and must be judged too");
		}

		[Test]
		public void PressingTheShortBarFirstStillJudgesTheLongBar()
		{
			using var harness = new LiveHarness();
			LongNote longBar = LongBar();
			LongNote shortBar = ShortBar();
			harness.Arrange(ScoreOf(longBar, shortBar));

			harness.Step(0f, InputBase,
				LiveHarness.Touch(1, LanePosition(10f), InputBase, InputTouchPhase.Began),
				LiveHarness.Touch(2, LanePosition(1.5f), InputBase, InputTouchPhase.Began));

			Assert.That(shortBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
			Assert.That(longBar.Head.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"the long bar's head is pressed on the same frame and must be judged too");
		}
	}
}
