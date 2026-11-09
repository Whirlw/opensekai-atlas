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
	}
}
