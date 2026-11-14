using System.IO;
using System.Linq;
using NUnit.Framework;
using Sekai.Live;
using Sekai.Live.Tails;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The contract a tail answers through, not the mapping table: a bar hands every held frame to
	/// <see cref="ITailBehaviour.TryFinish"/> and the tail decides. These cases pin that the decision
	/// really lives behind the interface, and that the three adapter booleans the caller used to
	/// branch on do not come back.
	/// </summary>
	[TestFixture]
	public class TailBehaviourContractTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static LongNote Bar() => new LongNote(Info(0f), 90, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);

		private static void Bind(LongNote bar, NoteBase tail) => bar.AddNoteListAndSetupChildNote(tail);

		[Test]
		public void ASelfJudgedTailKeepsItsOwnClockAfterTheBarHasGone()
		{
			// "条尾的 friction flick 没有解耦干净，没判定上也不 miss": the bar detaches the end note on its own time and
			// writes nothing onto it. That no longer orphans the note - a judged end note is ticked by the
			// session (LiveNoteSession.TickTails) - so it still finishes its own window and publishes its
			// own Miss. AFrictionFlickTailThatNobodyPlaysMissesInTheRealLoop is the proof through the real
			// loop; this pins the bar's half.
			LongNote bar = Bar();
			var tail = new FrictionFlickNote(Info(1f), 96, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Default, 0.0833f, 0f);
			Bind(bar, tail);

			bool settled = bar.TailBehaviour.TryFinish(bar, new TailFrame(2f, 1f / 60f, default, false, false));

			Assert.That(settled, Is.True, "the bar leaves on its own time");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "and it wrote no verdict onto the note");
			Assert.That(tail.State, Is.Not.EqualTo(NoteState.Done), "the note is still open, on its own clock");
		}

		[Test]
		public void AReleaseTailIgnoresAFrameWithoutALift()
		{
			// A frame where the finger stayed down cannot end the bar, even though the caller hands
			// every frame over unconditionally: the tail, not the caller, knows what ends it.
			LongNote bar = Bar();
			Bind(bar, new NormalNote(Info(1f), 95, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));

			bool settled = bar.TailBehaviour.TryFinish(bar, new TailFrame(1f, 1f / 60f, default, false, true));

			Assert.That(settled, Is.False);
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void ASelfJudgedTailDoesNotOverwriteTheBarsResult()
		{
			LongNote bar = Bar();
			bar.Head.Settle((NoteResult.Great, NoteResultDescription.None));
			Bind(bar, new FrictionFlickNote(Info(1f), 96, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f));
			((NoteBase)bar.ChildNote).FinishWith(NoteResult.JustPerfect);

			bool settled = bar.TailBehaviour.TryFinish(bar, new TailFrame(2f, 1f / 60f, default, false, false));

			Assert.That(settled, Is.True);
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
			Assert.That(bar.Result, Is.EqualTo(NoteResult.Great));
		}

		[Test]
		public void NoBarAdapterBooleansComeBack()
		{
			// The three booleans are how every new kind of end note used to leak a new switch into the
			// caller. The behaviour object is the only answer now, so a source scan is the cheapest
			// guard against the old shape creeping back in a later refactor.
			string[] offenders = Directory
				.EnumerateFiles("Assets/Game/Runtime", "*.cs", SearchOption.AllDirectories)
				.Where(path => File.ReadAllText(path).Contains("UsesReleaseTail")
					|| File.ReadAllText(path).Contains("UsesSwipeTail")
					|| File.ReadAllText(path).Contains("UsesStandaloneTail"))
				.ToArray();

			Assert.That(offenders, Is.Empty, "a bar answers with TailBehaviour.TryFinish, not with three booleans");
		}

		[Test]
		public void ASecondVerdictOnTheSameTailChangesNothing()
		{
			// tail-classes.md §13.2: one tail, one publication. The guarantee has to live in the tail
			// itself, not in "State == Done" or the child's own result - those are the indirect guards
			// the document warns about. So the bar is deliberately put back into the play state here:
			// every route must still find the tail settled and leave both verdicts alone.
			(LongNote bar, NoteBase tail) = BarAtItsTail();
			int published = 0;
			tail.OnJudgment = _ => published++;

			Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the lift at the tail grades it");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect));
			Assert.That(published, Is.EqualTo(1));
			NoteResult barResult = bar.Result;

			bar.SetStateUnnotice(NoteState.Scrolling);

			Assert.That(bar.FailTail(), Is.False, "the window closing again must not write a second verdict");
			Assert.That(bar.JudgeTailAsRelease(1f), Is.False, "nor may the same lift be graded twice");
			Assert.That(bar.ExpireTail(2f), Is.False, "nor may the timeout");
			Assert.That(bar.DetachStandaloneTail(2f), Is.False, "nor may the standalone detach");
			Assert.That(bar.TickSwipeTail(2f), Is.False, "and a release tail is never graded by the swipe path");

			Assert.That(published, Is.EqualTo(1), "the tail is published exactly once");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect), "and keeps its verdict");
			Assert.That(bar.Result, Is.EqualTo(barResult), "the bar keeps it too");
		}

		[Test]
		public void AReusedBarCanPublishItsTailAgain()
		{
			// The "published once" flag is per run: the pool hands the same objects back, and a bar that
			// could never publish again would swallow every tail of the second play.
			(LongNote bar, NoteBase tail) = BarAtItsTail();
			int published = 0;
			tail.OnJudgment = _ => published++;
			Assert.That(bar.JudgeTailAsRelease(1f), Is.True);

			bar.ResetNote();
			tail.ResetNote();
			bar.SetStateUnnotice(NoteState.Scrolling);

			Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "a reused bar has to grade its tail again");
			Assert.That(published, Is.EqualTo(2), "one publication per run");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect));
		}

		[Test]
		public void AVerdictThatArrivesAfterTheBarIsDoneStillPublishesOnlyOnce()
		{
			(LongNote bar, NoteBase tail) = BarAtItsTail();
			int published = 0;
			tail.OnJudgment = _ => published++;
			bar.ForceTerminate(tail);
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the bar is done for some other reason");

			Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the unjudged tail keeps its window");
			Assert.That(bar.JudgeTailAsRelease(1.01f), Is.False);
			Assert.That(bar.ExpireTail(1f), Is.False);
			Assert.That(bar.FailTail(), Is.False);
			Assert.That(bar.DetachStandaloneTail(2f), Is.False);

			Assert.That(published, Is.EqualTo(1));
			Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect));
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "nor is the bar brought back");
		}

		[Test]
		public void OnlyAFlickOrAnOrdinaryEndNoteIsGradedByItsBar()
		{
			// R2 is a mapping, not a switch in the caller: a flick is finished by a swipe, an ordinary
			// end note by a lift, and everything else judges itself - including the hidden carriers,
			// which carry no judgement at all.
			Assert.That(TailBehaviours.For(new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f)).AcceptsRelease,
				Is.True, "an ordinary end note is finished by the bar on a lift");
			Assert.That(TailBehaviours.For(new FlickNote(Info(1f), 2, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Default, 0, 0f)).AcceptsSwipe,
				Is.True, "a flick end note is finished by the bar on a swipe");

			NoteBase[] others =
			{
				new FrictionNote(Info(1f), 3, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f),
				new FrictionFlickNote(Info(1f), 4, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f),
				new HiddenConnectionNote(Info(1f), 5, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f),
			};
			foreach (NoteBase other in others)
			{
				ITailBehaviour behaviour = TailBehaviours.For(other);
				Assert.That(behaviour.JudgesItself, Is.True, other.GetType().Name + " judges itself");
				Assert.That(behaviour.AcceptsRelease, Is.False, other.GetType().Name + " is not finished by a lift");
				Assert.That(behaviour.AcceptsSwipe, Is.False, other.GetType().Name + " is not finished by the bar's swipe");
			}
		}

		[Test]
		public void AReleaseNeverGradesAnOtherEndNote()
		{
			// The bar must not grade it in any direction: not on a lift, and not by its own timeout.
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(tail);
			bar.SetStateUnnotice(NoteState.Scrolling);
			bar.RecordHoldingInput(1f);

			Assert.That(bar.JudgeTailAsRelease(1f), Is.False, "a lift is not this note's judgement");
			Assert.That(bar.FailTail(), Is.False, "and the bar's timeout does not grade it either");
			Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "nothing was written onto it");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
		}

		[Test]
		public void AFrictionFlickTailThatNobodyPlaysMissesInTheRealLoop()
		{
			// The acceptance of the report, through the real frame loop: a bar whose end is a friction flick
			// that nobody plays. The note judges itself, so the bar must keep ticking it until it decides -
			// detaching it any earlier left it with no verdict at all.
			using var harness = new LiveHarness();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionFlickNote(Info(1f), 2, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Default, 0.0833f, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(),
				new NoteBase[] { bar }));

			for (float now = 0f; now < 3f; now += 1f / 60f)
			{
				harness.Step(now, 10d + now);
			}

			Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the friction-flick end misses on its own");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "and the bar leaves on its own time");
			Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "with the verdict the note published");
		}

		[Test]
		public void AnIndependentTailGetsItsOwnCandidateSlot()
		{
			using var harness = new LiveHarness();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(tail);
			harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }));

			harness.Step(1f, 11d);

			Assert.That(harness.Logic.Input.JudgmentNotes, Does.Contain(tail),
				"a tail that judges itself is played like a note on no bar at all");
		}

		[Test]
		public void AHiddenTailIsNeverACandidate()
		{
			using var harness = new LiveHarness();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new HiddenConnectionNote(Info(1f), 2, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }));

			harness.Step(1f, 11d);

			Assert.That(harness.Logic.Input.JudgmentNotes, Has.No.Member(tail),
				"a hidden carrier carries no judgement, so it never reaches the list");
		}

		/// <summary>A bar held at its own tail time, with an ordinary end note at 1 s.</summary>
		private static (LongNote bar, NoteBase tail) BarAtItsTail()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			bar.SetStateUnnotice(NoteState.Scrolling);
			bar.RecordHoldingInput(1f);
			return (bar, tail);
		}
	}
}
