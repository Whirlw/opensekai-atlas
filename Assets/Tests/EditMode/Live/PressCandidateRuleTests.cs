using System;
using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The hard rule for who a press or a lift may be offered to. A press goes only to a tap, an unarmed
	/// flick or an ordinary bar head, in the finger's lane, 125 ms either side (a flick: 125 ms early to
	/// 141.7 ms late). A lift goes only to a bar's ordinary end note. A press that reaches a flick arms it
	/// and is spent there; from then on a swipe of any finger in the flick's lanes, inside its judge window
	/// and over its flick distance (5 cm/s), completes it.
	/// </summary>
	public sealed class PressCandidateRuleTests
	{
		private const float NoteTime = 1f;

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static NormalNote Tap(float time = NoteTime, int id = 1) =>
			new NormalNote(Info(time), id, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);

		private static FlickNote Flick(float time = NoteTime, int id = 2) =>
			new FlickNote(Info(time), id, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0f, 0f);

		private static FlickNote ScrollingFlick()
		{
			FlickNote flick = Flick();
			flick.Excute(Info(NoteTime - 0.2f), 1f);
			Assume.That(flick.State, Is.EqualTo(NoteState.Scrolling));
			return flick;
		}

		private static LongNote Bar(NoteBase tail)
		{
			var bar = new LongNote(Info(NoteTime), 3, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			return bar;
		}

		private static float Middle(NoteBase note) => (note.JudgeLaneStart + note.JudgeLaneEnd) * 0.5f;

		// ---- kinds -------------------------------------------------------------------------------

		[Test]
		public void OnlyATapAnUnarmedFlickAndAnOrdinaryHeadArePressKeys()
		{
			Assert.That(LiveCandidatePipeline.IsPressStep(Tap()), Is.True, "a tap");
			Assert.That(LiveCandidatePipeline.IsPressStep(ScrollingFlick()), Is.True, "a flick on no bar");
			LongNote bar = Bar(Tap(2f, 9));
			Assert.That(LiveCandidatePipeline.IsPressStep(bar.Head.Candidate), Is.True, "an ordinary head");

			Assert.That(LiveCandidatePipeline.IsPressStep(bar), Is.False, "a bar's body");
			Assert.That(LiveCandidatePipeline.IsPressStep((NoteBase)bar.ChildNote), Is.False, "a bar's end");
			Assert.That(LiveCandidatePipeline.IsPressStep(new LongHoldCombo(Info(NoteTime), NoteType.Default, 1f, 0f)), Is.False, "an interior point");
			Assert.That(LiveCandidatePipeline.IsPressStep(
				new FrictionNote(Info(NoteTime), 7, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1)), Is.False, "a rub");

			FlickNote flickTail = Flick(2f, 8);
			Bar(flickTail);
			Assert.That(LiveCandidatePipeline.IsPressStep(flickTail), Is.False, "a flick that ends a bar is a tail");
		}

		[Test]
		public void AKeyLeavesThePressKeysOnceItIsJudged()
		{
			NormalNote tap = Tap();
			var press = new LiveTouch(1, 1, Vector2.zero, Vector3.zero, TouchPhase.Began, NoteTime);
			tap.Judgment(ref press, Middle(tap));
			Assert.That(LiveCandidatePipeline.IsPressStep(tap), Is.False, "a judged tap");

			FlickNote flick = ScrollingFlick();
			flick.Judgment(ref press, Middle(flick));
			Assert.That(flick.IsArmedBy(1), Is.True);
			Assert.That(LiveCandidatePipeline.IsPressStep(flick), Is.False, "an armed flick only reads its swipe");
		}

		// ---- lanes -------------------------------------------------------------------------------

		[Test]
		public void APressOutsideTheKeysLanesIsNotOffered()
		{
			NormalNote tap = Tap();
			Assert.That(LiveCandidatePipeline.AdmitsPress(tap, NoteTime, Middle(tap)), Is.True);
			Assert.That(LiveCandidatePipeline.AdmitsPress(tap, NoteTime, tap.JudgeLaneStart - 0.05f), Is.False);
			Assert.That(LiveCandidatePipeline.AdmitsPress(tap, NoteTime, tap.JudgeLaneEnd + 0.05f), Is.False);
			Assert.That(LiveCandidatePipeline.AdmitsPress(tap, NoteTime, -1f), Is.False, "off the playfield");
		}

		[Test]
		public void AHeadTakesPressesOnItsBarsLanes()
		{
			LongNote bar = Bar(Tap(2f, 9));
			NoteBase head = bar.Head.Candidate;
			Assert.That(LiveCandidatePipeline.AdmitsPress(head, NoteTime, bar.JudgeLaneStart), Is.True);
			Assert.That(LiveCandidatePipeline.AdmitsPress(head, NoteTime, bar.JudgeLaneEnd), Is.True);
			Assert.That(LiveCandidatePipeline.AdmitsPress(head, NoteTime, bar.JudgeLaneEnd + 0.05f), Is.False);
		}

		// ---- windows -----------------------------------------------------------------------------

		[TestCase(-0.125f, true)]
		[TestCase(-0.130f, false)]
		[TestCase(0.125f, true)]
		[TestCase(0.130f, false)]
		public void ATapIsOfferedA125MsPressEitherSide(float touchMinusNote, bool offered)
		{
			NormalNote tap = Tap();
			Assert.That(LiveCandidatePipeline.AdmitsPress(tap, NoteTime + touchMinusNote, Middle(tap)), Is.EqualTo(offered));
		}

		[TestCase(-0.125f, true)]
		[TestCase(0.125f, true)]
		[TestCase(0.130f, false)]
		public void AHeadIsOfferedA125MsPressEitherSide(float touchMinusNote, bool offered)
		{
			LongNote bar = Bar(Tap(2f, 9));
			Assert.That(LiveCandidatePipeline.AdmitsPress(bar.Head.Candidate, NoteTime + touchMinusNote, Middle(bar)),
				Is.EqualTo(offered));
		}

		[TestCase(-0.125f, true)]
		[TestCase(-0.130f, false)]
		[TestCase(0.1417f, true)]
		[TestCase(0.145f, false)]
		public void AFlickIsOfferedAPressFrom125MsEarlyTo141MsLate(float touchMinusNote, bool offered)
		{
			FlickNote flick = ScrollingFlick();
			Assert.That(LiveCandidatePipeline.AdmitsPress(flick, NoteTime + touchMinusNote, Middle(flick)), Is.EqualTo(offered));
		}

		// ---- lifts -------------------------------------------------------------------------------

		[Test]
		public void ALiftIsOfferedOnlyToAnOrdinaryEnd()
		{
			NormalNote end = Tap(2f, 9);
			Assert.That(LiveHoldCoordinator.IsReleaseKey(Bar(end), end), Is.True, "an ordinary end");

			FlickNote flickEnd = Flick(2f, 8);
			Assert.That(LiveHoldCoordinator.IsReleaseKey(Bar(flickEnd), flickEnd), Is.False, "a flick end waits for its swipe");

			var rubEnd = new FrictionNote(Info(2f), 7, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);
			Assert.That(LiveHoldCoordinator.IsReleaseKey(Bar(rubEnd), rubEnd), Is.False, "a rub end grades itself");

			Assert.That(LiveHoldCoordinator.IsReleaseKey(Bar(Tap(2f, 6)), null), Is.False, "no end at all");
		}

		// ---- the real loop -----------------------------------------------------------------------

		private sealed class Loop : IDisposable
		{
			private readonly LiveHarness _harness = new LiveHarness();

			internal Loop(params NoteBase[] notes)
			{
				var info = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);
				var score = new MusicScore(new[] { info }, Array.Empty<EventBase>(), notes);
				_harness.Arrange(score);
			}

			internal void Step(float now, params LiveTouchSample[] touches) => _harness.Step(now, 10d + now, touches);

			internal static LiveTouchSample Finger(int id, float now, TouchPhase phase, Vector2 delta = default, int lane = 4) =>
				// The collector reads a swipe from the screen position, so the sample is placed where it moved to.
				new LiveTouchSample(id, new Vector2(800f, 600f) + delta, LiveConfig.JudgmentPositions[lane], 10d + now, phase, delta);

			public void Dispose() => _harness.Dispose();
		}

		private const float Frame = 1f / 60f;

		[Test]
		public void APressArmsAFlickAndIsSpentThere()
		{
			// A tap right behind the flick in the same lane: the one press goes to the flick only.
			FlickNote flick = Flick();
			NormalNote tap = Tap(NoteTime + 0.05f, 5);
			using var loop = new Loop(flick, tap);
			for (float now = 0.5f; now < NoteTime - Frame * 0.5f; now += Frame) loop.Step(now);

			loop.Step(NoteTime, Loop.Finger(1, NoteTime, TouchPhase.Began));
			Assert.That(flick.IsArmedBy(1), Is.True, $"the press arms the flick (state={flick.State})");
			Assert.That(flick.Result, Is.EqualTo(NoteResult.None), "arming is not grading: the swipe grades it");
			Assert.That(tap.Result, Is.EqualTo(NoteResult.None), "one press judges one key");

			float swipe = NoteTime + Frame;
			loop.Step(swipe, Loop.Finger(1, swipe, TouchPhase.Moved, new Vector2(0f, 200f)));
			Assert.That(flick.Result, Is.Not.EqualTo(NoteResult.None), $"the arming finger's swipe grades the flick (state={flick.State} armed={flick.IsArmedBy(1)})");
		}

		private FlickNote ArmedFlick(Loop loop, FlickNote flick)
		{
			for (float now = 0.5f; now < NoteTime - Frame * 0.5f; now += Frame) loop.Step(now);
			loop.Step(NoteTime, Loop.Finger(1, NoteTime, TouchPhase.Began));
			Assert.That(flick.IsArmedBy(1), Is.True);
			return flick;
		}

		[Test]
		public void AnotherFingersSwipeCompletesAnArmedFlick()
		{
			FlickNote flick = Flick();
			using var loop = new Loop(flick);
			ArmedFlick(loop, flick);

			// The resting arming finger comes first in the frame; the swipe of a finger that never pressed
			// the flick still reaches it.
			float swipe = NoteTime + Frame;
			loop.Step(swipe, Loop.Finger(1, swipe, TouchPhase.Stationary),
				Loop.Finger(2, swipe, TouchPhase.Moved, new Vector2(0f, 200f)));
			Assert.That(flick.Result, Is.Not.EqualTo(NoteResult.None), "any finger's swipe completes it");
		}

		[Test]
		public void ASwipeOutsideTheFlicksLanesDoesNotCompleteIt()
		{
			FlickNote flick = Flick();
			using var loop = new Loop(flick);
			ArmedFlick(loop, flick);

			float swipe = NoteTime + Frame;
			loop.Step(swipe, Loop.Finger(1, swipe, TouchPhase.Stationary),
				Loop.Finger(2, swipe, TouchPhase.Moved, new Vector2(0f, 200f), lane: 10));
			Assert.That(flick.Result, Is.EqualTo(NoteResult.None), "a swipe in another lane is not this flick's");
		}

		[Test]
		public void APressTooLateForTheFlickDoesNotArmIt()
		{
			FlickNote flick = Flick();
			using var loop = new Loop(flick);
			for (float now = 0.5f; now < NoteTime + 0.15f - Frame * 0.5f; now += Frame) loop.Step(now);

			float late = NoteTime + 0.15f;
			loop.Step(late, Loop.Finger(1, late, TouchPhase.Began));
			Assert.That(flick.IsArmedBy(1), Is.False, $"150 ms late is outside the flick's press window (state={flick.State})");
		}
	}
}
