using NUnit.Framework;
using Sekai.Live;
using Sekai.Live.Tails;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar answers "how do I end" with one object instead of three booleans spread over the class.
	/// These cases pin the mapping from end note to behaviour, which the old properties used to encode
	/// inside their getters.
	/// </summary>
	[TestFixture]
	public class TailBehaviourTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static LongNote Bar() => new LongNote(Info(0f), 90, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);

		private static void Bind(LongNote bar, NoteBase tail) => bar.AddNoteListAndSetupChildNote(tail);

		[Test]
		public void AnOrdinaryEndNoteIsFinishedByARelease()
		{
			LongNote bar = Bar();
			Bind(bar, new NormalNote(Info(1f), 91, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));

			Assert.That(bar.TailBehaviour, Is.SameAs(ReleaseTail.Instance));
			Assert.That(bar.TailBehaviour.AcceptsRelease, Is.True);
			Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.False);
			Assert.That(bar.TailBehaviour.JudgesItself, Is.False);
		}

		[Test]
		public void AFlickTailIsFinishedByASwipeNotARelease()
		{
			LongNote bar = Bar();
			Bind(bar, new FlickNote(Info(1f), 92, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f));

			Assert.That(bar.TailBehaviour, Is.SameAs(SwipeTail.Instance));
			Assert.That(bar.TailBehaviour.AcceptsRelease, Is.False);
			Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.True);
			Assert.That(bar.TailBehaviour.JudgesItself, Is.False);
		}

		[Test]
		public void AFrictionFlickTailJudgesItselfAndTheBarOnlyDetaches()
		{
			LongNote bar = Bar();
			Bind(bar, new FrictionFlickNote(Info(1f), 93, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f));

			Assert.That(bar.TailBehaviour, Is.SameAs(SelfJudgedTail.Instance));
			Assert.That(bar.TailBehaviour.AcceptsRelease, Is.False);
			Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.False);
			Assert.That(bar.TailBehaviour.JudgesItself, Is.True);
		}

		[Test]
		public void AResetBarStillKnowsHowItEnds()
		{
			// The pool resets a bar between runs. The binding survives that reset, so resetting the
			// behaviour with it silently turned every flick tail into a release tail - the bar then
			// waited for a lift that a flick tail refuses, and neither graded nor missed it.
			LongNote bar = Bar();
			Bind(bar, new FlickNote(Info(1f), 94, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f));
			bar.ResetNote();

			Assert.That(bar.TailBehaviour, Is.SameAs(SwipeTail.Instance));
			Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.True);
		}

		[Test]
		public void ABarWithNoTailFallsBackToARelease()
		{
			Assert.That(Bar().TailBehaviour, Is.SameAs(ReleaseTail.Instance));
		}
	[Test]
	public void AHiddenEndedBarStopsWithoutAnyResult()
	{
		// A bar that ends on a hidden marker never judges its tail: no result, no combo, and the
		// player letting go early changes nothing.
		LongNote bar = new LongNote(Info(0f), 21, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
		bar.AddNoteListAndSetupChildNote(new NormalNote(Info(0.5f), 22, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
		bar.AddNoteListAndSetupChildNote(new HiddenConnectionNote(Info(1f), 23, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f));

		Assert.That(bar.EndsWithoutJudgement, Is.True);
		Assert.That(bar.CanFinishSilently(0.9f), Is.False);
		Assert.That(bar.JudgeTailAsRelease(1f), Is.False);
		Assert.That(bar.ExpireTail(1.2f), Is.True);
		Assert.That(bar.State, Is.EqualTo(NoteState.Done));
		Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
	}

	[Test]
	public void ADecorativeBarEndsSilentlySoItsPlayableTailIsJudgedOnItsOwn()
	{
	    var bar = new GuideNote(Info(0f), 401, 4, 5, NoteCategory.Long, NoteType.Default, null, 1);
	    var tail = new FrictionFlickNote(Info(1f), 402, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);
	    bar.SetDecoration(true);
	    bar.AddNoteListAndSetupChildNote(tail);
	    bar.SkipTailJudgement();

	    Assert.That(bar.TailBehaviour.AcceptsRelease, Is.False);
	    Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.False);
	    Assert.That(bar.TailBehaviour.JudgesItself, Is.False);
	    Assert.That(bar.EndsWithoutJudgement, Is.True);
	    Assert.That(bar.CanFinishSilently(0.9f), Is.False);

	    Assert.That(bar.ExpireTail(1.2f), Is.True);
	    Assert.That(bar.State, Is.EqualTo(NoteState.Done));
	    Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
	    Assert.That(tail.Result, Is.EqualTo(NoteResult.None));
	}

	[Test]
	public void AHiddenMarkerAfterTheTailDoesNotStealIt()
	{
		LongNote bar = new LongNote(Info(0f), 11, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

		bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 12, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 100, 0f));
		bar.AddNoteListAndSetupChildNote(new HiddenConnectionNote(Info(1.5f), 13, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f));

		Assert.That(bar.ChildNote, Is.TypeOf<FlickNote>());
		Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.True);
		Assert.That(bar.TryGetTail(out NoteBase tail), Is.True);
		Assert.That(tail, Is.TypeOf<FlickNote>());
	}
	}
}
