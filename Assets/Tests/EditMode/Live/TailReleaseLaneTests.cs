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
			Assert.That(_bar.State, Is.Not.EqualTo(NoteState.Done), "and it does not finish the bar either");
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
	}
}