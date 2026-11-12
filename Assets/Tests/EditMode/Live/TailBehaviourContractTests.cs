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
		public void AReleaseTailIgnoresAFrameWithoutALift()
		{
			// A frame where the finger stayed down cannot end the bar, even though the caller hands
			// every frame over unconditionally: the tail, not the caller, knows what ends it.
			LongNote bar = Bar();
			Bind(bar, new NormalNote(Info(1f), 95, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));

			bool settled = bar.TailBehaviour.TryFinish(bar, new TailFrame(1f, 1f / 60f, default, false, true, true));

			Assert.That(settled, Is.False);
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
			Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));
		}

		[Test]
		public void ASelfJudgedTailDetachesOnceItsNoteHasAResult()
		{
			// The friction flick end grades itself; the bar only follows it and takes its result.
			LongNote bar = Bar();
			Bind(bar, new FrictionFlickNote(Info(1f), 96, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Default, 0.0833f, 0f));
			((NoteBase)bar.ChildNote).FinishWith(NoteResult.JustPerfect);

			bool settled = bar.TailBehaviour.TryFinish(bar, new TailFrame(2f, 1f / 60f, default, false, false, false));

			Assert.That(settled, Is.True);
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
			Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect));
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
		public void AVerdictThatArrivesAfterTheBarIsDoneIsNotAskedAgain()
		{
			// One point, stated plainly by the rules: when the tail's verdict comes back to a bar that is
			// already done, it is not judged a second time. Nothing is resurrected and nothing is
			// written onto the note.
			(LongNote bar, NoteBase tail) = BarAtItsTail();
			bar.ForceTerminate(tail);
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the bar is done for some other reason");

			Assert.That(bar.JudgeTailAsRelease(1f), Is.False, "a done bar is not judged again");
			Assert.That(bar.ExpireTail(1f), Is.False);
			Assert.That(bar.FailTail(), Is.False);
			Assert.That(bar.DetachStandaloneTail(2f), Is.False);

			Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "and nothing is written onto the note");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "nor is the bar brought back");
		}

		/// <summary>A bar held at its own tail time, with an ordinary end note at 1 s.</summary>
		private static (LongNote bar, NoteBase tail) BarAtItsTail()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			bar.SetStateUnnotice(NoteState.Scrolling);
			bar.RecordHoldingInput(1, 1f);
			return (bar, tail);
		}
	}
}
