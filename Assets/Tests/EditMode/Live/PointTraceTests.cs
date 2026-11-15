using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// An interior point is judged in the one input pass after it becomes due, so a Miss on a device
	/// is only explained by the trace of that frame: when it became due, who advanced it, and what its
	/// bar was doing. These pin the lines the device logs depend on.
	/// </summary>
	public sealed class PointTraceTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		[SetUp]
		public void SetUp()
		{
			LiveTrace.ClearBuffer();
			LiveTrace.Categories = LiveTraceCategory.Judge;
			LiveTrace.Enabled = true;
		}

		[TearDown]
		public void TearDown()
		{
			LiveTrace.Enabled = false;
			LiveTrace.Categories = LiveTraceCategory.Hold | LiveTraceCategory.Tail;
			LiveTrace.ClearBuffer();
		}

		private static (LongNote bar, LongHoldCombo point) BarWithAPoint()
		{
			var bar = new LongNote(Info(0f), 50, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
			var point = new LongHoldCombo(Info(1f), NoteType.Default, 51, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(point);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 52, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
			return (bar, point);
		}

		[Test]
		public void AMissedPointTellsWhenItWasDueAndWhoAdvancedIt()
		{
			var (bar, point) = BarWithAPoint();

			bar.Excute(Info(1.002f), 0.5f);
			bar.Excute(Info(1.008f), 0.5f);

			Assert.That(point.Result, Is.EqualTo(NoteResult.Miss));
			string trace = LiveTrace.Recent(20);
			StringAssert.Contains("pointDue id=51 t=1.000 now=1.002", trace);
			StringAssert.Contains("by=LongNote.AdvanceBar", trace, "the due line names who advanced the point");
			StringAssert.Contains("pointMiss id=51 t=1.000 now=1.008", trace);
			StringAssert.Contains("dueAt=1.002", trace, "the Miss line says how long ago it became due");
			StringAssert.Contains("bar=50@0.000", trace);
		}

		[Test]
		public void AHitPointIsTraced()
		{
			var (bar, point) = BarWithAPoint();
			bar.Excute(Info(1.002f), 0.5f);

			var touch = new LiveTouch(1, 1, UnityEngine.Vector2.zero, UnityEngine.Vector3.zero,
				UnityEngine.InputSystem.TouchPhase.Moved, 1.002f);
			point.Judgment(ref touch, 4.5f);

			Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect));
			StringAssert.Contains("pointHit id=51", LiveTrace.Recent(20));
		}

		[Test]
		public void NothingIsWrittenWithTheJudgeCategoryOff()
		{
			LiveTrace.Categories = LiveTraceCategory.Hold;
			var (bar, _) = BarWithAPoint();
			bar.Excute(Info(1.002f), 0.5f);
			bar.Excute(Info(1.008f), 0.5f);

			StringAssert.DoesNotContain("point", LiveTrace.Recent(20));
		}
	}
}
