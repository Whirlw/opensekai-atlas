using System.Linq;
using NUnit.Framework;
using Sekai.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Every live start binds the chart (which files the notes with a speed other than 1 into the high
	/// speed walk) and then resets the notes. The reset used to clear that filing: the note stayed in the
	/// high speed list while the normal walk stopped skipping it, so it was advanced twice a frame, and
	/// an interior point went due and Miss in one update before input could judge it.
	/// </summary>
	public sealed class HighSpeedResetTests
	{
		[Test]
		public void AResetKeepsWhichWalkANoteBelongsTo()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				NoteBase[] highSpeed = score.NoteArray.Where(n => n != null && !Mathf.Approximately(n.speedRatio, 1f)).ToArray();
				Assume.That(highSpeed, Is.Not.Empty, "the chart has high speed notes");

				score.ResetNote();

				Assert.That(highSpeed.All(n => n.IsHighSpeedNote), Is.True, "still walked by the high speed pass only");
				Assert.That(score.NoteArray.Where(n => n != null && Mathf.Approximately(n.speedRatio, 1f)).Any(n => n.IsHighSpeedNote),
					Is.False, "and the others still by the normal pass only");
			}
		}

		// Device frames and finger lanes (trace grab lines) of the 2026-11-14 run of DEINOS PHAINEIN, where
		// the speed-3 rub bar at 101.487 s lost its interior point at 101.526 s with a finger on it.
		private static readonly float[] _deviceT = { 101.408f, 101.414f, 101.420f, 101.426f, 101.432f, 101.438f, 101.444f, 101.450f, 101.457f, 101.463f, 101.469f, 101.475f, 101.481f, 101.487f, 101.494f, 101.499f, 101.505f, 101.511f, 101.517f, 101.523f, 101.529f, 101.535f, 101.541f, 101.547f };
		private static readonly float[] _deviceLane = { 3.67f, 3.67f, 4.04f, 4.44f, 4.86f, 5.26f, 5.62f, 5.97f, 6.32f, 6.97f, 6.97f, 7.24f, 7.49f, 7.68f, 7.84f, 8.10f, 8.10f, 8.22f, 8.33f, 8.44f, 8.53f, 8.60f, 8.65f, 8.70f };

		private static Vector3 Pos(float lane)
		{
			int a = Mathf.Clamp(Mathf.FloorToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 2);
			return Vector2.Lerp(LiveConfig.JudgmentPositions[a], LiveConfig.JudgmentPositions[a + 1], lane - a);
		}

		[Test]
		public void ARubbedHighSpeedBarStillGetsItsInteriorPointAfterTheStartReset()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			LongNote rub = score.NoteArray.OfType<FrictionLongNote>().First(b => Mathf.Abs(b.MusicScoreInfo.time - 101.487f) < 0.002f);
			LongNote held = score.NoteArray.OfType<LongNote>().First(b => b.GetType() == typeof(LongNote) && Mathf.Abs(b.MusicScoreInfo.time - 100.421f) < 0.002f);
			NoteBase point = rub.NoteList.First(n => n is LongHoldCombo);
			Assume.That(rub.speedRatio, Is.Not.EqualTo(1f), "the rub bar is a high speed note");

			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				score.ResetNote(); // what LiveLogic.Setup does right after binding

				bool down = false;
				float t = 100.30f;
				int k = 0;
				while (k < _deviceT.Length)
				{
					float now, lane;
					if (t < _deviceT[0]) { now = t; t += 1f / 165f; lane = (held.BodyLaneStart + held.BodyLaneEnd) * 0.5f; }
					else { now = _deviceT[k]; lane = _deviceLane[k]; k++; }

					harness.OnUpdate(now, 10d + now);
					if (now >= 100.40f)
					{
						// One finger holds the bar at 100.421 s and sweeps over the rub chain, as on the device.
						harness.AddTouch(1, Pos(lane), 10d + now, down ? InputTouchPhase.Moved : InputTouchPhase.Began,
							new Vector2(down ? 3f : 0f, 0f));
						down = true;
					}
					harness.OnInput();
					harness.OnView();
				}
			}

			Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect));
		}
	}
}
