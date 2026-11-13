using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The two rules that were mixed up: a bar's body always ends on the judgement line, its head key is
	/// allowed to fall past it when nobody took the bar. These assert the numbers the views use, not the
	/// intention behind them - the earlier predicate-only tests went green while the bug stayed.
	/// </summary>
	public sealed class NoteLineProgressTests
	{
		private const float Offset = 1f;
		private const float NoteTime = 10f;

		[Test]
		public void ABodySampleNeverPassesTheLine()
		{
			// 0.2 s past its own time would be 1.2 unclamped. The body must not go there: letting it
			// also inflates the split count until the tail end of the mesh no longer fits.
			float progress = NoteLineProgress.Body(Offset, NoteTime + 0.2f, NoteTime);

			Assert.That(progress, Is.EqualTo(1f).Within(1e-4f), "the body always ends on the line");
		}

		[Test]
		public void ABodySampleStillAdvancesBeforeItsTime()
		{
			float progress = NoteLineProgress.Body(Offset, NoteTime - 0.4f, NoteTime);

			Assert.That(progress, Is.EqualTo(0.6f).Within(1e-4f));
		}

		[Test]
		public void ABarKeyPastItsTimeKeepsFallingWhenNothingStandsOnTheLine()
		{
			// This is the device fix: a rub or guide bar nobody took drops its key below the line.
			float progress = NoteLineProgress.BarKey(1.2f, pinsAtLine: false);

			Assert.That(progress, Is.EqualTo(1.2f).Within(1e-4f), "a missed rub or guide bar must not freeze on the line");
		}

		[Test]
		public void ABarKeyPastItsTimeIsPinnedWhenSomethingStandsOnTheLine()
		{
			float progress = NoteLineProgress.BarKey(1.2f, pinsAtLine: true);

			Assert.That(progress, Is.EqualTo(1f).Within(1e-4f));
		}

		[Test]
		public void ABarKeyBeforeItsTimeIsUnchangedByThePin()
		{
			float unpinned = NoteLineProgress.BarKey(0.6f, pinsAtLine: false);
			float pinned = NoteLineProgress.BarKey(0.6f, pinsAtLine: true);

			Assert.That(unpinned, Is.EqualTo(0.6f).Within(1e-4f));
			Assert.That(pinned, Is.EqualTo(unpinned).Within(1e-6f), "approach is the same for every kind of bar");
		}

		[Test]
		public void TheViewCurveKeepsAValueAboveOneAboveOne()
		{
			// A key at 1.2 has to land below the line, so the curve must not fold it back onto 1.
			Assert.That(NoteLineProgress.View(1.2f), Is.GreaterThan(1f));
		}

		[Test]
		public void AnEndThatWasNeverExecutedIsMeasuredInsteadOfDrawnAtZero()
		{
			// Device report: a friction-hide bar was invisible for 18 frames (0.2 s) right after the press.
			// Its end note reaches Input when the player presses it, before any frame executed it, so its
			// stored offset was still 0 - and the mesh refuses to build a segment at an offset of 0.
			var note = new Sekai.Live.FrictionNote(
				new Sekai.Live.MusicScoreInfo(0, 0f, 5f, 120f, 4f, 1f, 1f), 1, 4, 5,
				Sekai.Live.NoteCategory.Friction, null, Sekai.Live.NoteType.Default, 1f);
			note.SetStateUnnotice(Sekai.Live.NoteState.Input);
			note.CalcTimeOffset = _ => 0.4f;

			Assert.That(note.offsetTime, Is.EqualTo(0f), "the note has never been executed");
			Assert.That(NoteLineProgress.SampleOffset(note), Is.EqualTo(0.4f).Within(1e-5f),
				"a zero offset is not a speed, it is a body nobody would draw");
		}

		[Test]
		public void AnExecutedEndKeepsTheOffsetItWasDrawnWith()
		{
			var note = new Sekai.Live.FrictionNote(
				new Sekai.Live.MusicScoreInfo(0, 0f, 5f, 120f, 4f, 1f, 1f), 1, 4, 5,
				Sekai.Live.NoteCategory.Friction, null, Sekai.Live.NoteType.Default, 1f);
			note.SetStateUnnotice(Sekai.Live.NoteState.Scrolling);
			note.offsetTime = 0.25f;
			note.CalcTimeOffset = _ => 9f;

			Assert.That(NoteLineProgress.SampleOffset(note), Is.EqualTo(0.25f).Within(1e-5f),
				"an executed note is drawn with the offset its frame gave it");
		}
	}
}
