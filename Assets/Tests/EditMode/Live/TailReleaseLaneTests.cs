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
			const float frame = 1f / 60f;
			for (float now = _bar.MusicScoreInfo.time + frame; now < TailTime - frame * 0.5f; now += frame)
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

			Assert.That(((NoteBase)left.ChildNote).Result, Is.EqualTo(NoteResult.JustPerfect));
			Assert.That(((NoteBase)right.ChildNote).Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void ADeferredSwipeFollowsItsFingerWhenAnotherFingerKeepsHolding()
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
			Assert.That(bar.HasDeferredSwipe, Is.True);

			StepTo(0.92f, Sample(1, position, 0.92f, TouchPhase.Ended, 700f),
				Sample(2, position, 0.92f, TouchPhase.Stationary));
			StepTo(0.94f, Sample(2, position, 0.94f, TouchPhase.Stationary));
			StepTo(0.96f, Sample(2, position, 0.96f, TouchPhase.Stationary));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));
			StepTo(0.98f, Sample(2, position, 0.98f, TouchPhase.Stationary));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void AFinalSwipeInsideTwoJudgeRangesGoesToTheCloserBar()
		{
			LongNote left = FlickBar(101, 4, 1.5f);
			LongNote right = FlickBar(103, 5, 1.5f);
			var score = new MusicScore(new[] { left.MusicScoreInfo },
				System.Array.Empty<EventBase>(), new NoteBase[] { left, right });
			_harness.Arrange(score);
			Vector3 leftPosition = LanePosition(4);
			Vector3 rightPosition = LanePosition(5);
			StepTo(0f, Sample(1, leftPosition, 0f, TouchPhase.Began),
				Sample(2, rightPosition, 0f, TouchPhase.Began));
			StepTo(0.98f, Sample(1, leftPosition, 0.98f, TouchPhase.Stationary),
				Sample(2, rightPosition, 0.98f, TouchPhase.Stationary));
			Assert.That(left.HasFinger && right.HasFinger, Is.True);

			StepTo(1f, Sample(1, leftPosition, 1f, TouchPhase.Ended, 700f),
				Sample(2, rightPosition, 1f, TouchPhase.Stationary));

			Assert.That(((NoteBase)left.ChildNote).Result, Is.EqualTo(NoteResult.JustPerfect));
			Assert.That(((NoteBase)right.ChildNote).Result, Is.EqualTo(NoteResult.None));
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
			float screenX = 800f) =>
			new LiveTouchSample(id, new Vector2(screenX, 600f), position, 10d + time, phase, Vector2.zero);
	}
}
