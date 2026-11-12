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
	/// Hold recovery, grace expiry, and finger presence.
	/// </summary>
	public class LongNoteGraceTests
	{
		internal static void AssertHeldBody(LongNote bar, string message = null)
		{
			Assert.That(bar.HasFinger, Is.True, message);
			Assert.That(bar.ShowsAsReleased, Is.False, message);
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.InputBegan).And.Not.EqualTo(NoteState.Input), message);
		}

		private static MusicScoreInfo Info(float time)
		{
			return new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);
		}

		private static LiveLogic.InputTmp Input(int finger, float lane)
		{
			return new LiveLogic.InputTmp
			{
				Lane = lane,
				Touch = new LiveTouch
				{
					fingerId = finger,
					touchId = finger,
					musicTime = 0f,
					phase = TouchPhase.Stationary
				}
			};
		}

		/// <summary>A bar of lanes 4..5 that is already being held by <paramref name="finger"/>.</summary>
		private static LongNote HeldBar(int finger)
		{
			var bar = new LongNote(Info(0f), finger, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.SetStateUnnotice(NoteState.Scrolling);
			bar.RecordHoldingInput(finger, 0f);
			return bar;
		}

		[Test]
		public void AFingerReturningInsideTheGraceTakesTheBarBack()
		{
			using var harness = new LiveHarness();
			var chart = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
			var score = RuntimeChartConverter.Convert(chart, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
			var bar = score.NoteArray.OfType<LongNote>().First(note => note.ChildNote is NormalNote && note.HasJudgment);
			var tail = (NoteBase)bar.ChildNote;
			harness.Arrange(score, new NoteBase[] { bar });

			float headTime = bar.MusicScoreInfo.time;
			float tailTime = tail.MusicScoreInfo.time;
			int lane = Mathf.Clamp(Mathf.RoundToInt(bar.JudgeLaneStart), 0, LiveConfig.JudgmentPositions.Length - 1);
			Vector3 position = LiveConfig.JudgmentPositions[lane];
			const float frame = 1f / 60f;

			void Step(float time, int finger, TouchPhase phase)
			{
				harness.Step(time, 10d + time, LiveHarness.Touch(finger, position, 10d + time, phase));
			}

			harness.Step(headTime - frame, 10d + headTime - frame);
			Step(headTime, 1, TouchPhase.Began);
			AssertHeldBody(bar);

			float lossTime = headTime + Mathf.Max(0.25f, LiveConfig.JudgeTime + frame);
			Assert.That(tailTime - lossTime, Is.GreaterThan(0.15f), "the fixture must leave time to resume before the tail");
			for (float now = headTime + frame; now < lossTime; now += frame)
				Step(now, 1, TouchPhase.Stationary);

			Step(lossTime, 1, TouchPhase.Ended);
			float greyTime = lossTime + frame;
			harness.Step(greyTime, 10d + greyTime);
			Assert.That(bar.State, Is.EqualTo(NoteState.Last), "the uncovered body entered its grace period");
			Assert.That(bar.ShowsAsReleased, Is.True);

			float returnTime = greyTime + frame;
			Step(returnTime, 3, TouchPhase.Began);
			AssertHeldBody(bar, "a new finger lights the body while the grace is still open");
			for (float now = returnTime + frame; now < tailTime - frame * 0.5f; now += frame)
			{
				Step(now, 3, TouchPhase.Stationary);
				AssertHeldBody(bar);
				Assert.That(bar.State, Is.EqualTo(NoteState.Last).Or.EqualTo(NoteState.Release),
					"the recovered body keeps its post-head state");
			}

			Assert.That(tail.Result, Is.EqualTo(NoteResult.None));
			Step(tailTime, 3, TouchPhase.Ended);
			Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect), "the returning finger can finish the tail on time");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
		}

		[TestCase(60)]
		[TestCase(165)]
		public void AnUncoveredBarExpiresOnTheSameClockAtEitherRefreshRate(int refreshRate)
		{
			using var harness = new LiveHarness();
			var chart = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
			var score = RuntimeChartConverter.Convert(chart, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
			var bar = score.NoteArray.OfType<LongNote>().First(note => note.ChildNote is NormalNote && note.HasJudgment);
			harness.Arrange(score, new NoteBase[] { bar });

			float step = 1f / refreshRate;
			float headTime = bar.MusicScoreInfo.time;
			int lane = Mathf.Clamp(Mathf.RoundToInt(bar.JudgeLaneStart), 0, LiveConfig.JudgmentPositions.Length - 1);
			Vector3 position = LiveConfig.JudgmentPositions[lane];
			harness.Step(headTime - step, 10d + headTime - step);
			harness.Step(headTime, 10d + headTime, LiveHarness.Touch(1, position, 10d + headTime));
			AssertHeldBody(bar);
			float releaseTime = headTime + Mathf.Max(0.25f, LiveConfig.JudgeTime + step);
			for (float now = headTime + step; now < releaseTime; now += step)
			{
				harness.Step(now, 10d + now, LiveHarness.Touch(1, position, 10d + now, TouchPhase.Stationary));
			}

			harness.Step(releaseTime, 10d + releaseTime, LiveHarness.Touch(1, position, 10d + releaseTime, TouchPhase.Ended));
			float greyTime = releaseTime + step;
			harness.Step(greyTime, 10d + greyTime);
			Assert.That(bar.State, Is.EqualTo(NoteState.Last));
			Assert.That(bar.ShowsAsReleased, Is.True);

			float beforeExpiry = greyTime + 0.05f - 0.001f;
			float afterExpiry = greyTime + 0.05f + 0.001f;
			harness.Step(beforeExpiry, 10d + beforeExpiry);
			Assert.That(bar.State, Is.EqualTo(NoteState.Last));
			harness.Step(afterExpiry, 10d + afterExpiry);
			Assert.That(bar.State, Is.EqualTo(NoteState.Release));
			Assert.That(bar.ChildNote.Result, Is.EqualTo(NoteResult.None), "grace expiry does not judge a tail that is still ahead");
		}

		[Test]
		public void HasFingerFollowsTheTouchIdOnly()
		{
			var bar = HeldBar(1);

			Assert.That(bar.HasFinger, Is.True, "a held bar has a finger by definition");
			Assert.That(bar.IsHeld(0f), Is.True, "and IsHeld is just HasFinger now");

			bar.ClearFinger();

			Assert.That(bar.HasFinger, Is.False, "no touch id means no finger, whatever the state says");
			Assert.That(bar.IsHeld(0f), Is.False, "the time argument no longer decides anything");
		}

		[Test]
		public void HeldBodyNeverEntersAnInputPhase()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.SetStateUnnotice(NoteState.Scrolling);
			var touch = Input(1, 4.5f).Touch;
			for (int frame = 0; frame < 4; frame++)
			{
				bar.Judgment(ref touch, 4.5f);
				AssertHeldBody(bar);
			}
		}

		[Test]
		public void FingerPresenceTracksAssignmentAndReset()
		{
			var bar = HeldBar(1);
			foreach (int finger in new[] { 2, -1, 0 })
			{
				bar.RecordHoldingInput(finger, 0f);
				Assert.That(bar.HasFinger, Is.True);
				Assert.That(bar.HasFinger, Is.True);
			}
			bar.ResetNote();
			Assert.That(bar.HasFinger, Is.False);
			Assert.That(bar.HasFinger, Is.False);
		}
	}
}