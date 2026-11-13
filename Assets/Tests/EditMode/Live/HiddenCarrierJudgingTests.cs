using NUnit.Framework;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A hidden carrier is the lane a rub travels along, not a note: it has no judgement
	/// (<see cref="NoteBase.HasJudgment"/> is false), so neither its head nor its end may ever
	/// produce one - not by timing out, not by autoplay. Only the interior points it carries are
	/// judged, and those are the points the 8th note grid puts inside its body.
	/// </summary>
	public sealed class HiddenCarrierJudgingTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		[Test]
		public void ACarrierHeadNeverTimesOut()
		{
			var bar = new CarrierNote(Info(0f), 1, 4, 5);

			bar.Head.SettleExpiredHead(5f);

			Assert.That(bar.Head.IsSettled, Is.False, "a carrier head has no window to miss");
		}

		[Test]
		public void AutoplayOnACarrierAnswersOnlyItsInteriorPoints()
		{
			var bar = new CarrierNote(Info(0f), 1, 4, 5);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var combo = new LongHoldCombo(Info(0.5f), NoteType.Default, 1f, 0f);
			bar.AddHoldCombo(combo);
			bar.SortNoteList();

			bar.AutoJudgment(Info(0.6f));

			Assert.That(combo.Result, Is.EqualTo(NoteResult.Auto), "the point on the grid is judged");
			Assert.That(bar.Head.IsSettled, Is.False, "the head is not judged");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the body is still running");

			bar.AutoJudgment(Info(1.2f));

			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the carrier still leaves when its end passes");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.Auto),
				"the note at its end is a real note and is judged like one");
		}

		/// <summary>A bar with no judgement, which is what a hidden friction carrier is.</summary>
		private sealed class CarrierNote : LongNote
		{
			internal CarrierNote(MusicScoreInfo info, int id, int laneStart, int laneEnd)
				: base(info, id, laneStart, laneEnd, NoteCategory.FrictionHideLong, NoteType.Default, 1f, 0f)
			{
			}

			public override bool HasJudgment => false;
		}
	}
}
