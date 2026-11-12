using System;
using NUnit.Framework;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The six acceptance cases the judging handoff asks for (docs/operations/handoff-2026-11-11.md §10
	/// step 1): two for the head, two for which lift finishes a tail, two for the flick tail's own time
	/// window and its single publication.
	///
	/// They are written against the contract the documents describe, not against what the code happens
	/// to do today: each one names the fact it pins, so a later change that removes the fact fails here
	/// with the reason instead of silently passing some other way.
	/// </summary>
	[TestFixture]
	public sealed class DecouplingAcceptanceTests
	{
		private const float Frame = 1f / 60f;
		private const double InputBase = 10d;

		private MusicScoreInfo _previousFrame;
		private LiveConfig.NoteTypeJudgeData _previousJudgeData;

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		/// <summary>The screen point that resolves to <paramref name="lane"/>, the way a finger would be placed.</summary>
		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}

		private static MusicScore ScoreOf(params NoteBase[] notes) =>
			new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes);

		/// <summary>A bar taking its head at 0 s, with an ordinary end note at <paramref name="tailTime"/>.</summary>
		private static LongNote OrdinaryBar(float tailTime, int bodyStart, int bodyEnd, float laneOffset = 0f)
		{
			var bar = new LongNote(Info(0f), 1, bodyStart, bodyEnd, NoteCategory.Long, NoteType.Default, 1f, laneOffset);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(tailTime), 2, bodyStart, bodyEnd,
				NoteCategory.Normal, 1f, NoteType.Default, 0f));
			return bar;
		}

		[SetUp]
		public void SetUp()
		{
			_previousFrame = MusicScore.CurrentFrameInfo;
			_previousJudgeData = LiveConfig.noteTypeJudgeData;
			LiveConfig.LoadMasterConfig();
			MusicScore.CurrentFrameInfo = Info(0f);
		}

		[TearDown]
		public void TearDown()
		{
			MusicScore.CurrentFrameInfo = _previousFrame;
			LiveConfig.noteTypeJudgeData = _previousJudgeData;
		}

		// ------------------------------------------------------------------------------------------
		// The head: leaving the play state publishes it, whoever ends the bar.
		// ------------------------------------------------------------------------------------------

		[Test]
		public void ABarThatCrossesTheLineUnpressedStillPublishesItsHeadMiss()
		{
			using var harness = new LiveHarness();
			LongNote bar = OrdinaryBar(tailTime: 2f, bodyStart: 4, bodyEnd: 5);
			int published = 0;
			bar.OnJudgment = note => { if (ReferenceEquals(note, bar)) published++; };
			harness.Arrange(ScoreOf(bar));

			// Nothing ever lands on the lane: the bar crosses its own line on the clock alone.
			for (int frame = 0; frame <= 20; frame++)
			{
				float now = frame * Frame;
				harness.Step(now, InputBase + now);
			}

			Assert.That(bar.Head.IsSettled, Is.True, "the head has to settle once its window is gone");
			Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Miss), "crossing the line unpressed is a Miss");
			Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "and the bar carries that head result");
			Assert.That(published, Is.EqualTo(1), "the head result is published exactly once");
		}

		[Test]
		public void ABarFinishedByItsTailStillPublishesTheHead()
		{
			using var harness = new LiveHarness();
			LongNote bar = OrdinaryBar(tailTime: 1f, bodyStart: 4, bodyEnd: 5);
			NoteBase tail = (NoteBase)bar.ChildNote;
			int published = 0;
			bar.OnJudgment = note => { if (ReferenceEquals(note, bar)) published++; };
			harness.Arrange(ScoreOf(bar));

			Vector3 position = LanePosition(bar.JudgeLaneStart);
			harness.Step(0f, InputBase, LiveHarness.Touch(1, position, InputBase, TouchPhase.Began));
			Assert.That(bar.Head.IsSettled, Is.True, "the press settles the head");
			NoteResult headResult = bar.Head.Result;
			Assert.That(headResult, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"a press on the head is a hit");

			for (float now = Frame; now < 1f - Frame * 0.5f; now += Frame)
			{
				harness.Step(now, InputBase + now, LiveHarness.Touch(1, position, InputBase + now, TouchPhase.Stationary));
			}

			// The lift at the tail is what ends the bar; the head was published long before it.
			harness.Step(1f, InputBase + 1f, LiveHarness.Touch(1, position, InputBase + 1f, TouchPhase.Ended));

			Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None), "the lift grades the tail");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "and finishes the bar");
			Assert.That(bar.Head.Result, Is.EqualTo(headResult), "the tail's result must not replace the head's");
			Assert.That(published, Is.EqualTo(1), "the head is published once, whatever ends the bar");
		}

		// ------------------------------------------------------------------------------------------
		// Which lift finishes a tail: the tail's own lane, never the body's widened range.
		// ------------------------------------------------------------------------------------------

		[Test]
		public void ATailShiftedToAnotherLaneIsFinishedByLiftingThere()
		{
			(NoteResult bodyLift, NoteState bodyState) = ShiftedTailLift(liftLane: 2f);
			(NoteResult tailLift, NoteState tailState) = ShiftedTailLift(liftLane: 9f);

			Assert.That(bodyLift, Is.EqualTo(NoteResult.None), "lifting where the body is leaves the tail alone");
			Assert.That(bodyState, Is.Not.EqualTo(NoteState.Done), "and the bar keeps running");
			Assert.That(tailLift, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"lifting where the tail moved to grades it");
			Assert.That(tailState, Is.EqualTo(NoteState.Done), "and finishes the bar");
		}

		/// <summary>A bar whose body sits on lanes 2..3 and whose end note sits on 8..9.</summary>
		private static (NoteResult tail, NoteState state) ShiftedTailLift(float liftLane)
		{
			using var harness = new LiveHarness();
			var bar = new LongNote(Info(0f), 1, 2, 3, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(1f), 2, 8, 9,
				NoteCategory.Normal, 1f, NoteType.Default, 0f));
			var tail = (NoteBase)bar.ChildNote;
			harness.Arrange(ScoreOf(bar));

			Vector3 body = LanePosition(bar.JudgeLaneStart);
			harness.Step(0f, InputBase, LiveHarness.Touch(1, body, InputBase, TouchPhase.Began));
			Assert.That(bar.HasFinger, Is.True, "the real candidate path has to take the bar");
			for (float now = Frame; now < 1f - Frame * 0.5f; now += Frame)
			{
				harness.Step(now, InputBase + now, LiveHarness.Touch(1, body, InputBase + now, TouchPhase.Stationary));
			}

			harness.Step(1f, InputBase + 1f,
				LiveHarness.Touch(1, LanePosition(liftLane), InputBase + 1f, TouchPhase.Ended));
			return (tail.Result, bar.State);
		}

		[Test]
		public void ALiftInsideADecorationLaneDoesNotFinishTheNeighbour()
		{
			(NoteResult decorationLane, NoteState neighbourState) = WidenedBodyLift(liftLane: 3f);
			(NoteResult ownLane, NoteState ownState) = WidenedBodyLift(liftLane: 4f);

			Assert.That(decorationLane, Is.EqualTo(NoteResult.None),
				"lane 3 is inside the body's widened range but outside the tail's own range");
			Assert.That(neighbourState, Is.Not.EqualTo(NoteState.Done), "so the neighbour keeps running");
			Assert.That(ownLane, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss),
				"its own lane still grades the tail");
			Assert.That(ownState, Is.EqualTo(NoteState.Done), "and finishes the bar");
		}

		/// <summary>
		/// The target bar is on 4..5 with a body widened by one lane (3..6) and a tail that stays on 4..5.
		/// A decoration bar sits on lane 3, so a lift there is a real place a player's finger can be; it
		/// is inside the target's widened body range and outside the range the tail itself occupies.
		/// </summary>
		private static (NoteResult tail, NoteState state) WidenedBodyLift(float liftLane)
		{
			using var harness = new LiveHarness();
			var target = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 1f);
			target.AddNoteListAndSetupChildNote(new NormalNote(Info(1f), 2, 4, 5,
				NoteCategory.Normal, 1f, NoteType.Default, 0f));
			var tail = (NoteBase)target.ChildNote;

			var decoration = new LongNote(Info(0f), 3, 3, 3, NoteCategory.Long, NoteType.Default, 1f, 0f);
			decoration.SetDecoration(true);
			decoration.SkipTailJudgement();
			decoration.AddNoteListAndSetupChildNote(new NormalNote(Info(2.5f), 4, 3, 3,
				NoteCategory.Normal, 1f, NoteType.Default, 0f));

			harness.Arrange(ScoreOf(target, decoration));

			Vector3 own = LanePosition(4f);
			harness.Step(0f, InputBase, LiveHarness.Touch(1, own, InputBase, TouchPhase.Began));
			Assert.That(target.HasFinger, Is.True, "the press has to take the target bar");
			for (float now = Frame; now < 1f - Frame * 0.5f; now += Frame)
			{
				harness.Step(now, InputBase + now, LiveHarness.Touch(1, own, InputBase + now, TouchPhase.Stationary));
			}

			harness.Step(1f, InputBase + 1f,
				LiveHarness.Touch(1, LanePosition(liftLane), InputBase + 1f, TouchPhase.Ended));
			return (tail.Result, target.State);
		}

		// ------------------------------------------------------------------------------------------
		// The flick tail: its own time window, and one publication only.
		// ------------------------------------------------------------------------------------------

		[Test]
		public void AFlickTailIgnoresSamplesFromBeforeItsOwnTime()
		{
			LongNote bar = FlickBar(out NoteBase tail);

			// 400 ms early: far outside the -125 ms band the tail's early window allows, so it is not
			// even worth remembering - remembering it is what used to grade a wobble during the hold.
			Assert.That(bar.NotifyLaneSwipe(60f, 0.6f), Is.False, "a swipe before the tail's own time is not judged yet");
			Assert.That(bar.HasDeferredSwipe, Is.False, "and it is not remembered either");

			// Lifting and letting the deferred-swipe countdown run out must not turn it into a grade.
			bar.NotifyLaneRelease();
			for (int frame = 0; frame < 5; frame++)
			{
				float now = 0.62f + frame * Frame;
				Assert.That(bar.TickSwipeTail(now), Is.False, $"frame {frame} after the release");
			}

			Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "the early sample must not grade the tail");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "and it must not finish the bar");

			Assert.That(bar.NotifyLaneSwipe(60f, 1f), Is.True, "a swipe at the tail's own time still settles it");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
			Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.Miss), "and it is a swipe, not a miss");
		}

		[Test]
		public void AFlickTailPublishesOnceEvenIfTheBarKeepsTicking()
		{
			int headPublished = 0;
			LongNote bar = FlickBar(out NoteBase tail, onHeadJudged: _ => headPublished++);
			int tailPublished = 0;
			int tailCombos = 0;
			tail.OnJudgment = _ => tailPublished++;
			tail.OnUpdateCombo = _ => tailCombos++;

			Assert.That(bar.NotifyLaneSwipe(60f, 1.02f), Is.True, "a swipe 20 ms after the tail time settles it");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
			NoteResult settled = bar.Result;
			Assert.That(settled, Is.Not.EqualTo(NoteResult.None));

			// The bar keeps being driven while the rest of the chart plays out.
			for (int frame = 1; frame <= 30; frame++)
			{
				float now = 1.02f + frame * Frame;
				Assert.That(bar.TickSwipeTail(now), Is.False, $"frame {frame}: a finished tail cannot be graded again");
				bar.Excute(Info(now), 0f);
				Assert.That(bar.NotifyLaneSwipe(60f, now), Is.False, $"frame {frame}: a second swipe is not a second judgement");
			}

			Assert.That(tailPublished, Is.EqualTo(1), "the tail publishes one result, however many frames follow");
			Assert.That(tailCombos, Is.EqualTo(1), "and the combo counts it once");
			Assert.That(headPublished, Is.EqualTo(1), "and the head keeps its own single publication");
			Assert.That(bar.Result, Is.EqualTo(settled), "the settled result is not rewritten by later ticks");
			Assert.That(tail.Result, Is.EqualTo(settled), "and the tail keeps the same result");
		}

		/// <summary>
		/// A held bar ending on a leftward flick at 1 s. The head is pressed first so the end point exists
		/// at all: a bar nobody holds has no tail for a swipe to finish.
		/// </summary>
		private static LongNote FlickBar(out NoteBase tail, Action<NoteBase> onHeadJudged = null)
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			tail = new FlickNote(Info(1f), 2, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Left, 0, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			if (onHeadJudged != null) bar.OnJudgment = onHeadJudged;
			bar.SetStateUnnotice(NoteState.Scrolling);

			var head = new LiveTouch(1, 1, Vector2.zero, Vector3.zero, TouchPhase.Began, 0f);
			bar.Judgment(ref head, 4.5f);
			bar.RecordHoldingInput(1, 0f);
			return bar;
		}
	}
}
