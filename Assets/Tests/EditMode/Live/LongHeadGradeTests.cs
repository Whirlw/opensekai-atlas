using System;
using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// What a long note's head is actually graded when it is pressed in each band, through the real frame
	/// loop. The head uses the <c>Long</c> table (frames at 60 fps, symmetric): Perfect 2.5 (41.7 ms),
	/// Great 5 (83.3 ms), Good 6.5 (108.3 ms), Bad 7.5 (125 ms). Outside 125 ms the press is not a
	/// candidate at all and the head ends as a Miss when its window closes.
	/// </summary>
	public sealed class LongHeadGradeTests
	{
		private const float Frame = 1f / 60f;
		private const double InputBase = 10d;
		private const float HeadTime = 1f;

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}

		/// <summary>A plain bar in lanes 0..3 from 1 s to 2 s, ending on an ordinary note.</summary>
		private static LongNote Bar()
		{
			var bar = new LongNote(Info(HeadTime), 1, 0, 3, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 2, 0, 3, NoteCategory.Normal, 1f, NoteType.Default, 0f));
			return bar;
		}

		/// <summary>Runs the loop up to the press, presses once at <paramref name="offset"/> from the head, then
		/// keeps the finger down for a few more frames and returns the head's result.</summary>
		private static NoteResult PressAt(float offset, float runUntil = HeadTime + 0.2f)
		{
			using var harness = new LiveHarness();
			LongNote bar = Bar();
			harness.Arrange(new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), new NoteBase[] { bar }));
			Vector3 position = LanePosition(1.5f);
			float press = HeadTime + offset;

			for (float now = 0.5f; now < press - Frame * 0.5f; now += Frame)
			{
				harness.Step(now, InputBase + now);
			}

			harness.Step(press, InputBase + press, LiveHarness.Touch(1, position, InputBase + press, InputTouchPhase.Began));
			for (float now = press + Frame; now <= runUntil; now += Frame)
			{
				harness.Step(now, InputBase + now, LiveHarness.Touch(1, position, InputBase + now, InputTouchPhase.Stationary));
			}

			return bar.Head.Result;
		}

		[TestCase(0f)]
		[TestCase(0.004f)]
		[TestCase(-0.004f)]
		public void APressOnTheBeatIsJustPerfect(float offset)
		{
			Assert.That(PressAt(offset), Is.EqualTo(NoteResult.JustPerfect));
		}

		[TestCase(-0.03f)]
		[TestCase(0.03f)]
		public void APressInsideThePerfectBandIsPerfect(float offset)
		{
			Assert.That(PressAt(offset), Is.EqualTo(NoteResult.Perfect));
		}

		[TestCase(-0.06f)]
		[TestCase(0.06f)]
		[TestCase(-0.08f)]
		[TestCase(0.08f)]
		public void APressInsideTheGreatBandIsGreat(float offset)
		{
			Assert.That(PressAt(offset), Is.EqualTo(NoteResult.Great));
		}

		[TestCase(-0.095f)]
		[TestCase(0.095f)]
		public void APressInsideTheGoodBandIsGood(float offset)
		{
			Assert.That(PressAt(offset), Is.EqualTo(NoteResult.Good));
		}

		[TestCase(-0.118f)]
		[TestCase(0.118f)]
		public void APressInsideTheBadBandIsBad(float offset)
		{
			Assert.That(PressAt(offset), Is.EqualTo(NoteResult.Bad));
		}

		[Test]
		public void APressTooLateIsNotTakenAndTheHeadMisses()
		{
			// 150 ms late: outside the 125 ms press window, so the press is no candidate; the head's window
			// has closed by then and it is a Miss, not a late Bad.
			Assert.That(PressAt(0.15f), Is.EqualTo(NoteResult.Miss));
		}

		[Test]
		public void APressTooEarlyIsNotTakenAndTheHeadMissesLater()
		{
			// 150 ms early: the press is no candidate, the head stays open, and with the finger only resting
			// (no second press) it ends as a Miss once its window closes.
			Assert.That(PressAt(-0.15f, runUntil: HeadTime - 0.15f + Frame), Is.EqualTo(NoteResult.None),
				"an early press outside the window leaves the head unjudged");
			Assert.That(PressAt(-0.15f), Is.EqualTo(NoteResult.Miss), "and the head misses when its window closes");
		}
	}
}
