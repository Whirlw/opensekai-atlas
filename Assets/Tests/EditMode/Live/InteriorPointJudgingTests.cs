using NUnit.Framework;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A long note has four kinds of interior point, and only three of them are judged:
	/// the generated ones, the preset ones the chart draws, and the ones added without changing the
	/// shape. A hidden point changes the shape and nothing else. Two points on one position are one
	/// judgement for that bar; two bars on one position judge twice, each for itself.
	/// </summary>
	public sealed class InteriorPointJudgingTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static LongNote Bar()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(2f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			return bar;
		}

		private static ConnectionNote Preset(float time) =>
			new ConnectionNote(Info(time), 10, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);

		private static HiddenConnectionNote Hidden(float time) =>
			new HiddenConnectionNote(Info(time), 11, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f);

		private static LongHoldCombo Auto(float time, bool skip)
		{
			var point = new LongHoldCombo(Info(time), NoteType.Default, 1f, 0f);
			point.SetSkip(skip);
			return point;
		}

		[Test]
		public void TheThreeDrawnOrGeneratedKindsAreJudged()
		{
			LongNote bar = Bar();
			LongHoldCombo auto = Auto(0.5f, skip: true);
			ConnectionNote preset = Preset(1f);
			LongHoldCombo added = Auto(1.5f, skip: false);
			bar.AddHoldCombo(auto);
			bar.AddConnectionNote(preset);
			bar.AddHoldCombo(added);
			bar.SortNoteList();

			Assert.That(bar.JudgedPoints, Does.Contain(auto), "a generated point is judged");
			Assert.That(bar.JudgedPoints, Does.Contain(preset), "a drawn preset point is judged");
			Assert.That(bar.JudgedPoints, Does.Contain(added), "a point added without reshaping is judged");
		}

		[Test]
		public void AHiddenPointIsNotJudged()
		{
			LongNote bar = Bar();
			HiddenConnectionNote hidden = Hidden(1f);
			bar.AddConnectionNote(hidden);
			bar.SortNoteList();

			Assert.That(bar.JudgedPoints, Has.No.Member(hidden),
				"a hidden point changes the shape only");
		}

		[Test]
		public void TwoPointsOnOnePositionAreOneJudgementForTheBar()
		{
			LongNote bar = Bar();
			ConnectionNote preset = Preset(1f);
			LongHoldCombo added = Auto(1f, skip: false);
			bar.AddConnectionNote(preset);
			bar.AddHoldCombo(added);
			bar.SortNoteList();

			Assert.That(bar.JudgedPoints, Has.Count.EqualTo(1),
				"one position, one judgement for this bar");
		}

		[Test]
		public void TwoBarsOnOnePositionJudgeTwice()
		{
			LongNote first = Bar();
			first.AddConnectionNote(Preset(1f));
			first.SortNoteList();

			LongNote second = Bar();
			second.AddHoldCombo(Auto(1f, skip: true));
			second.SortNoteList();

			Assert.That(first.JudgedPoints, Has.Count.EqualTo(1));
			Assert.That(second.JudgedPoints, Has.Count.EqualTo(1),
				"each bar judges its own point at that position");
		}

		[Test]
		public void AutoplayAnswersOnlyTheJudgedPoints()
		{
			LongNote bar = Bar();
			LongHoldCombo auto = Auto(0.5f, skip: true);
			HiddenConnectionNote hidden = Hidden(1f);
			bar.AddHoldCombo(auto);
			bar.AddConnectionNote(hidden);
			bar.SortNoteList();

			bar.AutoJudgment(Info(1.2f));

			Assert.That(auto.Result, Is.EqualTo(NoteResult.Auto));
			Assert.That(hidden.Result, Is.EqualTo(NoteResult.None), "the hidden point is never answered");
		}
	}
}
