using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using Sekai.Live.Tails;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The flick that ends a held bar grades itself (FlickTailJudge); the bar does not judge it any more.
	///
	/// Rule: inside the flick's own lanes, a swipe over 5 cm/s up to 125 ms before the tail time is only
	/// recorded. It is graded 50 ms after that finger lifts or slides out of the lanes; a finger still in
	/// the lanes at the tail time is graded right then. A newer swipe of the same finger overrides the
	/// direction, several fingers coming due on one frame keep the best grade, and from the tail time on a
	/// swipe grades at once. Everything here runs through the real input loop (LiveHarness).
	/// </summary>
	public sealed class FlickTailJudgeTests
	{
		private const int LANE = 4;
		private const int FAR_LANE = 9;
		private const float FRAME = 1f / 60f;

		private LiveHarness _harness;
		private LongNote _bar;
		private float _chartStart;
		private FlickNote Tail => (FlickNote)_bar.ChildNote;

		// Screen x of each finger: the harness turns position changes into deltas, so a finger that
		// swiped must keep reporting its new position or the next sample reads as a swipe back.
		private readonly float[] _screenX = new float[8];

		[SetUp]
		public void SetUp()
		{
			_harness = new LiveHarness();
		}

		[TearDown]
		public void TearDown()
		{
			_harness.Dispose();
		}

		private void Arrange(NoteDirection direction = NoteDirection.Default, float chartStart = 0f)
		{
			_chartStart = chartStart;
			var head = new MusicScoreInfo(0, 0f, chartStart, 120, 4, 1, 1);
			var end = new MusicScoreInfo(0, 0.5f, chartStart + 1f, 120, 4, 1, 1);
			_bar = new LongNote(head, 301, LANE, LANE, NoteCategory.Long, NoteType.Default, 1f, 0f);
			_bar.AddNoteListAndSetupChildNote(new FlickNote(end, 302, LANE, LANE,
				NoteCategory.Flick, 1f, NoteType.Default, direction, 0f, 10f));
			var score = new MusicScore(new[] { _bar.MusicScoreInfo },
				System.Array.Empty<EventBase>(), new NoteBase[] { _bar });
			_harness.Arrange(score);
			for (int i = 0; i < _screenX.Length; i++) _screenX[i] = 800f;
		}

		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}

		private LiveTouchSample At(int id, float time, TouchPhase phase, int lane = LANE, float moveX = 0f)
		{
			_screenX[id] += moveX;
			return new LiveTouchSample(id, new Vector2(_screenX[id], 600f), LanePosition(lane), 10d + time,
				phase, Vector2.zero);
		}

		private void StepTo(float time, params LiveTouchSample[] touches) => _harness.Step(time + _chartStart, 10d + time, touches);

		/// <summary>Presses the bar at 0 and holds it with finger 1 until <paramref name="until"/> (exclusive).</summary>
		private void HoldUntil(float until)
		{
			StepTo(0f, At(1, 0f, TouchPhase.Began));
			for (float now = FRAME; now < until - FRAME * 0.5f; now += FRAME)
			{
				StepTo(now, At(1, now, TouchPhase.Stationary));
			}

			Assert.That(_bar.HasFinger, Is.True, "the bar is held");
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void TheBarNoLongerJudgesItsFlickEnd()
		{
			Arrange();
			Assert.That(_bar.TailBehaviour, Is.SameAs(SelfJudgedTail.Instance));
			Assert.That(_bar.HasIndependentTail, Is.True);
			Assert.That(Tail.JudgesAsBarTail, Is.True);
		}

		[TestCase(0f)]
		[TestCase(-2f)]
		public void AnEarlySwipeIsGradedFiftyMillisecondsAfterTheLift(float chartStart)
		{
			Arrange(chartStart: chartStart);
			HoldUntil(0.88f);
			StepTo(0.88f, At(1, 0.88f, TouchPhase.Moved, moveX: -100f));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), "an early swipe is only recorded");

			StepTo(0.89f, At(1, 0.89f, TouchPhase.Ended));
			StepTo(0.93f);
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), "not yet 50 ms after the lift");

			StepTo(0.95f);
			// Graded at 0.94 (lift + 50 ms), not at the frame that noticed it: 60 ms early is Great on the
			// end table, where 50 ms would already be the Perfect band.
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void CancelDiscardsAnArmedSwipeAndItsFinalDisplacement()
		{
			Arrange();
			HoldUntil(0.93f);
			StepTo(0.93f, At(1, 0.93f, TouchPhase.Moved, moveX: -100f));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));

			StepTo(1f, At(1, 1f, TouchPhase.Canceled, moveX: -100f));
			StepTo(1.06f);

			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void SlidingOutOfTheFlickLanesCountsAsTheLift()
		{
			Arrange();
			HoldUntil(0.88f);
			StepTo(0.88f, At(1, 0.88f, TouchPhase.Moved, moveX: -100f));
			StepTo(0.90f, At(1, 0.90f, TouchPhase.Stationary, FAR_LANE));
			StepTo(0.94f, At(1, 0.94f, TouchPhase.Stationary, FAR_LANE));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));

			StepTo(0.96f, At(1, 0.96f, TouchPhase.Stationary, FAR_LANE));
			// Graded at 0.95: 50 ms early is still the Perfect band.
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void AFingerStillOnTheLanesIsGradedAtTheTailTime()
		{
			Arrange();
			HoldUntil(0.90f);
			StepTo(0.90f, At(1, 0.90f, TouchPhase.Moved, moveX: -100f));
			for (float now = 0.90f + FRAME; now < 1f - 0.001f; now += FRAME)
			{
				StepTo(now, At(1, now, TouchPhase.Stationary));
				Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), $"waiting at {now:F3}");
			}

			StepTo(1f, At(1, 1f, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void ASwipeBeforeTheEarlyWindowIsNotRecorded()
		{
			Arrange();
			HoldUntil(0.80f);
			StepTo(0.80f, At(1, 0.80f, TouchPhase.Moved, moveX: -100f));
			Assert.That(Tail.TailJudge.HasArmedFinger, Is.False, "200 ms early is outside the 125 ms window");

			StepTo(0.82f, At(1, 0.82f, TouchPhase.Ended));
			for (float now = 0.84f; now < 1.3f; now += FRAME)
			{
				StepTo(now);
			}

			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
		}

		[Test]
		public void ASlowMoveIsNotASwipe()
		{
			Arrange();
			HoldUntil(0.90f);
			StepTo(0.90f, At(1, 0.90f, TouchPhase.Moved, moveX: -0.5f));
			Assert.That(Tail.TailJudge.HasArmedFinger, Is.False, "under the flick speed");
		}

		[TestCase(false, NoteResult.JustPerfect)]
		[TestCase(true, NoteResult.Great)]
		public void TheLatestSwipeDecidesTheDirection(bool wrongWayLast, NoteResult expected)
		{
			// Left is asked for: moving left on screen is the right way, moving right the wrong way.
			Arrange(NoteDirection.Left);
			HoldUntil(0.90f);
			StepTo(0.90f, At(1, 0.90f, TouchPhase.Moved, moveX: wrongWayLast ? -100f : 100f));
			StepTo(0.93f, At(1, 0.93f, TouchPhase.Moved, moveX: wrongWayLast ? 100f : -100f));
			StepTo(0.96f, At(1, 0.96f, TouchPhase.Stationary));
			StepTo(1f, At(1, 1f, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(expected));
		}

		[Test]
		public void FingersComingDueOnTheSameFrameKeepTheBestGrade()
		{
			Arrange();
			HoldUntil(0.86f);
			StepTo(0.86f, At(1, 0.86f, TouchPhase.Moved, moveX: -100f), At(2, 0.86f, TouchPhase.Began));
			// Finger 1 lifts at 0.87 (due 0.92, 80 ms early: Great).
			StepTo(0.87f, At(1, 0.87f, TouchPhase.Ended), At(2, 0.87f, TouchPhase.Stationary));
			StepTo(0.89f, At(2, 0.89f, TouchPhase.Moved, moveX: -100f));
			// Finger 2 lifts at 0.90 (due 0.95, 50 ms early: Perfect band).
			StepTo(0.90f, At(2, 0.90f, TouchPhase.Ended));
			StepTo(0.91f);
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));

			// One long frame: both fingers come due together, and the better grade is kept.
			StepTo(0.96f);
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[TestCase(0f)]
		[TestCase(-2f)]
		public void ASwipeAfterTheTailTimeGradesAtOnce(float chartStart)
		{
			Arrange(chartStart: chartStart);
			HoldUntil(1.03f);
			StepTo(1.03f, At(1, 1.03f, TouchPhase.Moved, moveX: -100f));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void AnyFingerInTheLanesMayPlayTheTail()
		{
			// Not only the holder: a second finger in the flick's lanes swipes and lifts.
			Arrange();
			HoldUntil(0.90f);
			StepTo(0.90f, At(1, 0.90f, TouchPhase.Stationary), At(2, 0.90f, TouchPhase.Began));
			StepTo(0.92f, At(1, 0.92f, TouchPhase.Stationary), At(2, 0.92f, TouchPhase.Moved, moveX: -100f));
			StepTo(0.93f, At(1, 0.93f, TouchPhase.Stationary), At(2, 0.93f, TouchPhase.Ended));
			StepTo(0.97f, At(1, 0.97f, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
			StepTo(0.99f, At(1, 0.99f, TouchPhase.Stationary));
			Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void NoSwipeAtAllIsAMiss()
		{
			Arrange();
			HoldUntil(1.0f);
			for (float now = 1.0f; now < 1.3f; now += FRAME)
			{
				StepTo(now, At(1, now, TouchPhase.Stationary));
			}

			Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
		}
	}
}
