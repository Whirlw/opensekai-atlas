using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar's tail is graded by a release. The release that belongs to the bar is the one from the
	/// finger holding it, or from a finger that is inside its lane; a lift anywhere else used to count
	/// as "released inside" and finished the bar from across the playfield.
	/// </summary>
	public sealed class TailReleaseLaneTests
	{
		private LiveHarness _harness;
		private LongNote _bar;
		private NoteBase Tail => (NoteBase)_bar.ChildNote;
		private float TailTime => Tail.MusicScoreInfo.time;

		[SetUp]
		public void SetUp()
		{
			_harness = new LiveHarness();
			var document = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
			var score = RuntimeChartConverter.Convert(document, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
			_bar = score.NoteArray.OfType<LongNote>().First(n => n.ChildNote is NormalNote && n.HasJudgment);
			_harness.Arrange(score, new NoteBase[] { _bar });
		}

		[TearDown]
		public void TearDown()
		{
			_harness.Dispose();
		}

		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}

		private void StepTo(float time, params LiveTouchSample[] touches)
		{
			_harness.Step(time, 10d + time, touches);
		}

		private void HoldUntilTailTime()
		{
			Vector3 mine = LanePosition(_bar.JudgeLaneStart);
			StepTo(_bar.MusicScoreInfo.time - 0.02f);
			StepTo(_bar.MusicScoreInfo.time, LiveHarness.Touch(1, mine, 10d + _bar.MusicScoreInfo.time, TouchPhase.Began));
			Assert.That(_bar.HasFinger, Is.True, "the real candidate path must acquire the hold");

			// Stop just short of the tail: the next step must be the release itself, and the clock never
			// goes backwards, which would leave the hold's freshness in the past.
			const float FRAME = 1f / 60f;
			for (float now = _bar.MusicScoreInfo.time + FRAME; now < TailTime - FRAME * 0.5f; now += FRAME)
			{
				StepTo(now, LiveHarness.Touch(1, mine, 10d + now, TouchPhase.Stationary));
			}

			LongNoteGraceTests.AssertHeldBody(_bar, "the bar is still held when the release lands");
		}

		[Test]
		public void ALiftInAnotherLaneDoesNotGradeTheTail()
		{
			HoldUntilTailTime();
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), "nothing has lifted the bar yet");

			// A second finger far away from the bar goes down and comes straight back up on the tail frame.
			float far = _bar.JudgeLaneEnd >= 5.5f ? 0.5f : 10.5f;
			StepTo(TailTime, LiveHarness.Touch(2, LanePosition(far), 10d + TailTime, TouchPhase.Ended));

			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), "a lift outside the bar is not its release");
			Assert.That(_bar.State, Is.EqualTo(NoteState.Done), "the body has reached its endpoint");
		}

		[Test]
		public void ABufferedReleaseIsGradedAtItsTouchTime()
		{
			HoldUntilTailTime();
			var position = LanePosition(_bar.JudgeLaneStart);

			StepTo(TailTime + 0.06f, LiveHarness.Touch(1, position, 10d + TailTime, TouchPhase.Ended));

			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Perfect));
		}

		[TestCase(TouchPhase.Ended, NoteResult.Perfect)]
		[TestCase(TouchPhase.Canceled, NoteResult.None)]
		public void AContactCanEndAtTheSameTimestampAsItsLastSample(TouchPhase phase, NoteResult expected)
		{
			HoldUntilTailTime();
			var position = LanePosition(_bar.JudgeLaneStart);
			double timestamp = 10d + TailTime;

			StepTo(TailTime, LiveHarness.Touch(1, position, timestamp, TouchPhase.Stationary),
				LiveHarness.Touch(1, position, timestamp, phase));

			Assert.That(Tail.Result, Is.EqualTo(expected));
		}

		[Test]
		public void ASystemCancelLeavesAnOrdinaryTailToExpire()
		{
			HoldUntilTailTime();
			var position = LanePosition(_bar.JudgeLaneStart);

			StepTo(TailTime, LiveHarness.Touch(1, position, 10d + TailTime, TouchPhase.Canceled));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));

			StepTo(TailTime + 0.25f);
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
		}

		[Test]
		public void AReleaseAfterTheWindowKeepsTheExpiredTailAtMiss()
		{
			HoldUntilTailTime();
			var position = LanePosition(_bar.JudgeLaneStart);
			StepTo(TailTime, LiveHarness.Touch(1, position, 10d + TailTime, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));

			float expired = TailTime + 0.25f;
			StepTo(expired, LiveHarness.Touch(1, position, 10d + expired, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
			Assert.That(_bar.State, Is.EqualTo(NoteState.Done));

			float lift = expired + 1f / 60f;
			StepTo(lift, LiveHarness.Touch(1, position, 10d + lift, TouchPhase.Ended));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
		}

		[Test]
		public void TheHoldersOwnReleaseGradesTheTail()
		{
			HoldUntilTailTime();

			// The holder lifts inside the bar: this is the release that grades the tail.
			StepTo(TailTime, LiveHarness.Touch(1, LanePosition(_bar.JudgeLaneStart), 10d + TailTime, TouchPhase.Ended));

			Assert.That(Tail.Result, Is.Not.EqualTo(NoteResult.None),
				$"the holder release grades the tail (state={_bar.State} finger={_bar.HasFinger}"
				+ $" judgeable={_bar.IsTailJudgeableAt(TailTime)} tail={TailTime:F3} bar={_bar.MusicScoreInfo.time:F3})");
			Assert.That(_bar.State, Is.EqualTo(NoteState.Done));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void AFinalSwipeOnlyReachesTheBarItStartedOn(bool endOutside)
		{
			LongNote left = FlickBar(101, 2);
			LongNote right = FlickBar(103, 8);
			var score = new MusicScore(new[] { left.MusicScoreInfo },
				System.Array.Empty<EventBase>(), new NoteBase[] { left, right });
			_harness.Arrange(score);
			Vector3 leftPosition = LanePosition(2);
			Vector3 rightPosition = LanePosition(8);
			StepTo(0f, Sample(1, leftPosition, 0f, TouchPhase.Began),
				Sample(2, rightPosition, 0f, TouchPhase.Began));
			StepTo(0.98f, Sample(1, leftPosition, 0.98f, TouchPhase.Stationary),
				Sample(2, rightPosition, 0.98f, TouchPhase.Stationary));
			Assert.That(left.HasFinger && right.HasFinger, Is.True);

			Vector3 endPosition = endOutside ? leftPosition + Vector3.left * 100f : rightPosition;
			StepTo(1f, Sample(1, endPosition, 1f, TouchPhase.Ended, 700f),
				Sample(2, rightPosition, 1f, TouchPhase.Stationary));

			Assert.That(((NoteBase)left.ChildNote).Result, Is.EqualTo(NoteResult.Perfect));
			Assert.That(((NoteBase)right.ChildNote).Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void AnEarlySwipeFollowsItsFingerWhenAnotherFingerKeepsHolding()
		{
			LongNote bar = FlickBar(101, 4);
			var score = new MusicScore(new[] { bar.MusicScoreInfo },
				System.Array.Empty<EventBase>(), new NoteBase[] { bar });
			_harness.Arrange(score);
			Vector3 position = LanePosition(4);
			StepTo(0f, Sample(1, position, 0f, TouchPhase.Began));
			StepTo(0.88f, Sample(1, position, 0.88f, TouchPhase.Stationary),
				Sample(2, position, 0.88f, TouchPhase.Began));
			StepTo(0.90f, Sample(1, position, 0.90f, TouchPhase.Moved, 700f),
				Sample(2, position, 0.90f, TouchPhase.Stationary));
			Assert.That(((FlickNote)bar.ChildNote).TailJudge.HasArmedFinger, Is.True);

			StepTo(0.92f, Sample(1, position, 0.92f, TouchPhase.Ended, 700f),
				Sample(2, position, 0.92f, TouchPhase.Stationary));
			StepTo(0.94f, Sample(2, position, 0.94f, TouchPhase.Stationary));
			StepTo(0.96f, Sample(2, position, 0.96f, TouchPhase.Stationary));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));
			StepTo(0.98f, Sample(2, position, 0.98f, TouchPhase.Stationary));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.Perfect));
		}



		private LongNote ArrangeHeldNormalBar(int lane, float until, params NoteBase[] extraNotes)
		{
			LongNote bar = NormalBar(201, lane);
			NoteBase[] notes = new NoteBase[] { bar }.Concat(extraNotes).ToArray();
			var score = new MusicScore(new[] { bar.MusicScoreInfo },
				System.Array.Empty<EventBase>(), notes);
			_harness.Arrange(score);
			Vector3 position = LanePosition(lane);
			StepTo(0f, Sample(1, position, 0f, TouchPhase.Began));
			const float FRAME = 1f / 60f;
			for (float now = FRAME; now < until - FRAME * 0.5f; now += FRAME)
			{
				StepTo(now, Sample(1, position, now, TouchPhase.Stationary));
			}

			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));
			return bar;
		}

		// The tail is at 1.0. A lift within one sixth of a second of the key press is the end of that tap and may only
		// release the tail inside its Perfect band; a later lift is an ordinary release.
		[TestCase(false, 0.95f, 1.03f, NoteResult.Perfect)]
		[TestCase(true, 0.95f, 1.03f, NoteResult.Perfect)]
		[TestCase(false, 1.00f, 1.09f, NoteResult.None)]
		[TestCase(true, 1.00f, 1.09f, NoteResult.None)]
		// Pressed before the tail's window opened, lifted 35 ms later inside it (device log, tail 3813):
		// still a quick tap, so it does not take the tail at Good.
		[TestCase(false, 0.85f, 0.885f, NoteResult.None)]
		// At 150 and 166 ms the protection applies. At 167 ms a Great can be earned.
		[TestCase(false, 0.95f, 1.10f, NoteResult.None)]
		[TestCase(false, 0.934f, 1.10f, NoteResult.None)]
		[TestCase(false, 0.933f, 1.10f, NoteResult.Great)]
		[TestCase(false, 0.90f, 1.10f, NoteResult.Great)]
		public void AQuickTapRestrictsItsLiftToPerfect(bool flick, float pressTime,
			float liftTime, NoteResult expected)
		{
			const int KEY_LANE = 10;
			var info = new MusicScoreInfo(0, pressTime / 2f, pressTime, 120, 4, 1, 1);
			NoteBase key = flick
				? new FlickNote(info, 301, KEY_LANE, KEY_LANE, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0f, 0f)
				: new NormalNote(info, 301, KEY_LANE, KEY_LANE, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			LongNote bar = ArrangeHeldNormalBar(4, pressTime, key);
			Vector3 position = LanePosition(4);
			StepTo(pressTime, Sample(1, position, pressTime, TouchPhase.Stationary),
				Sample(2, LanePosition(KEY_LANE), pressTime, TouchPhase.Began));
			if (key is FlickNote flickKey)
				Assert.That(flickKey.IsArmedBy(2), Is.True);
			else
				Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));

			// The key was in another lane, and the lifting contact is now alone in the tail's lane.
			StepTo(liftTime, Sample(1, LanePosition(KEY_LANE), liftTime, TouchPhase.Stationary),
				Sample(2, position, liftTime, TouchPhase.Ended));
			Assert.That(bar.TailNote.Result, Is.EqualTo(expected));
		}

		[Test]
		public void AKeyPressDoesNotAffectAnotherFingersLift()
		{
			var info = new MusicScoreInfo(0, 0.45f, 0.9f, 120, 4, 1, 1);
			var tap = new NormalNote(info, 301, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			LongNote bar = ArrangeHeldNormalBar(4, 0.9f, tap);
			Vector3 position = LanePosition(4);
			StepTo(0.9f, Sample(1, position, 0.9f, TouchPhase.Stationary), Sample(2, position, 0.9f, TouchPhase.Began));
			Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));

			StepTo(1.1f, Sample(1, position, 1.1f, TouchPhase.Ended), Sample(2, position, 1.1f, TouchPhase.Stationary));
			Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void ASecondContactByTheSameFingerDoesNotInheritTheFirstPress()
		{
			var info = new MusicScoreInfo(0, 0.45f, 0.9f, 120, 4, 1, 1);
			var tap = new NormalNote(info, 301, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			LongNote bar = ArrangeHeldNormalBar(4, 0.9f, tap);
			Vector3 position = LanePosition(4);
			StepTo(0.9f, Sample(1, position, 0.9f, TouchPhase.Stationary), Sample(2, position, 0.9f, TouchPhase.Began));
			Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));

			StepTo(0.91f, Sample(1, position, 0.91f, TouchPhase.Stationary), Sample(2, position, 0.91f, TouchPhase.Ended));
			Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.None));

			StepTo(0.92f, Sample(1, position, 0.92f, TouchPhase.Stationary),
				Sample(3, position, 0.92f, TouchPhase.Began, fingerId: 2));
			StepTo(1.10f, Sample(1, position, 1.10f, TouchPhase.Stationary),
				Sample(3, position, 1.10f, TouchPhase.Ended, fingerId: 2));
			Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void TwoLiftsWithTheSameTwoCandidatesShareTheTails()
		{
			LongNote earlier = NormalBar(201, 4);
			LongNote later = NormalBar(203, 4, 1.05f);
			var score = new MusicScore(new[] { earlier.MusicScoreInfo }, System.Array.Empty<EventBase>(),
				new NoteBase[] { earlier, later });
			_harness.Arrange(score);
			Vector3 position = LanePosition(4);
			StepTo(0f, Sample(1, position, 0f, TouchPhase.Began), Sample(2, position, 0f, TouchPhase.Began));
			StepTo(0.98f, Sample(1, position, 0.98f, TouchPhase.Stationary), Sample(2, position, 0.98f, TouchPhase.Stationary));
			var resultOrder = new System.Collections.Generic.List<NoteBase>();
			earlier.TailNote.OnJudgment = resultOrder.Add;
			later.TailNote.OnJudgment = resultOrder.Add;
			StepTo(1f, Sample(2, position, 1f, TouchPhase.Ended), Sample(1, position, 1f, TouchPhase.Ended));

			Assert.That(earlier.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
			Assert.That(later.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
			Assert.That(resultOrder, Is.EqualTo(new[] { later.TailNote, earlier.TailNote }),
				"the smaller finger id gets the earlier tail at equal distance, even when reported second");
		}

		[Test]
		public void TheSameLiftCanSkipARestrictedTailAndSettleAnother()
		{
			// A quick tap (lift 90 ms after the press): 80 ms late is past the earlier tail's Perfect
			// band, 40 ms late is inside the later one's.
			const float PRESS_TIME = 0.99f;
			const float LIFT_TIME = 1.08f;
			LongNote later = NormalBar(203, 4, 1.04f);
			var info = new MusicScoreInfo(0, PRESS_TIME / 2f, PRESS_TIME, 120, 4, 1, 1);
			var tap = new NormalNote(info, 301, 10, 10, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			LongNote earlier = ArrangeHeldNormalBar(4, PRESS_TIME, later, tap);
			Vector3 position = LanePosition(4);
			StepTo(PRESS_TIME, Sample(1, position, PRESS_TIME, TouchPhase.Stationary),
				Sample(2, LanePosition(10), PRESS_TIME, TouchPhase.Began));
			Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));

			StepTo(LIFT_TIME, Sample(1, position, LIFT_TIME, TouchPhase.Stationary),
				Sample(2, position, LIFT_TIME, TouchPhase.Ended));
			Assert.That(earlier.TailNote.Result, Is.EqualTo(NoteResult.None));
			Assert.That(later.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
		}

		[Test]
		public void ALoneLiftSettlesAnyGrade()
		{
			LongNote bar = ArrangeHeldNormalBar(4, 1.1f);
			StepTo(1.1f, Sample(1, LanePosition(4), 1.1f, TouchPhase.Ended));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void AnotherTouchOutsideTheTailRangeDoesNotAffectTheLift()
		{
			LongNote bar = ArrangeHeldNormalBar(2, 0.9f);
			Vector3 position = LanePosition(2);
			Vector3 far = LanePosition(10);
			StepTo(0.9f, Sample(1, position, 0.9f, TouchPhase.Stationary), Sample(2, far, 0.9f, TouchPhase.Began));

			StepTo(1.1f, Sample(1, position, 1.1f, TouchPhase.Ended), Sample(2, far, 1.1f, TouchPhase.Stationary));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void TwoLiftsSharingOnlyOneCandidateSettleIt()
		{
			LongNote bar = ArrangeHeldNormalBar(4, 0.9f);
			Vector3 position = LanePosition(4);
			StepTo(0.9f, Sample(1, position, 0.9f, TouchPhase.Stationary), Sample(2, position, 0.9f, TouchPhase.Began));

			StepTo(1.1f, Sample(1, position, 1.1f, TouchPhase.Ended), Sample(2, position, 1.1f, TouchPhase.Ended));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void SeveralSamplesOfTheLiftingTouchAreNotAnotherTouch()
		{
			LongNote bar = ArrangeHeldNormalBar(4, 1.1f);
			Vector3 position = LanePosition(4);
			StepTo(1.1f, Sample(1, position, 1.09f, TouchPhase.Moved), Sample(1, position, 1.1f, TouchPhase.Ended));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.Great));
		}

		private static LongNote NormalBar(int id, int lane, float tailTime = 1f)
		{
			var head = new MusicScoreInfo(0, 0f, 0f, 120, 4, 1, 1);
			var end = new MusicScoreInfo(0, tailTime / 2f, tailTime, 120, 4, 1, 1);
			var bar = new LongNote(head, id, lane, lane, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new NormalNote(end, id + 1, lane, lane,
				NoteCategory.Normal, 1f, NoteType.Default, 0f));
			return bar;
		}

		private static LongNote FlickBar(int id, int lane, float laneOffset = 0f)
		{
			var head = new MusicScoreInfo(0, 0f, 0f, 120, 4, 1, 1);
			var end = new MusicScoreInfo(0, 0.5f, 1f, 120, 4, 1, 1);
			var bar = new LongNote(head, id, lane, lane, NoteCategory.Long, NoteType.Default, 1f, laneOffset);
			bar.AddNoteListAndSetupChildNote(new FlickNote(end, id + 1, lane, lane,
				NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 1f, 0f));
			return bar;
		}

		private static LiveTouchSample Sample(int id, Vector3 position, float time, TouchPhase phase,
			float screenX = 800f, int? fingerId = null) =>
			new LiveTouchSample(id, new Vector2(screenX, 600f), position, 10d + time, phase, Vector2.zero, fingerId);
	}
}
