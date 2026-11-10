using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A second finger landing on a lane that overlaps a held long note must not take the note away.
	/// These cases pin the decision LongNote uses to ignore a foreign touch. Liveness is expressed in
	/// seconds since the reference-frame change, so the numbers here are seconds, not frame counts.
	/// </summary>
	public sealed class LongHoldOwnershipTests
	{
		/// <summary>Five ticks of 100 ms: the tolerance these cases hand to the predicate.</summary>
		private const float Tolerance = 0.5f;

		private static float At(float tick)
		{
			return tick * 0.1f;
		}

		[Test]
		public void TheHoldingFingerAlwaysKeepsItsOwnNote()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 3, NoteState.Input, At(100), At(101), Tolerance),
				Is.False);
		}

		[Test]
		public void AForeignTouchIsIgnoredWhileTheHoldIsFresh()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 7, NoteState.Input, At(100), At(103), Tolerance),
				Is.True);
		}

		[Test]
		public void AForeignTouchTakesOverOnceThePreviousHoldWentStale()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 7, NoteState.Input, At(100), At(106), Tolerance),
				Is.False);
		}

		[Test]
		public void TheBoundaryMomentStillBelongsToTheHolder()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 7, NoteState.Input, At(100), At(105), Tolerance),
				Is.True);
		}

		[Test]
		public void TheMomentTheHoldBeginsBelongsToTheHolder()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 7, NoteState.InputBegan, At(100), At(100), Tolerance),
				Is.True);
		}

		[Test]
		public void AnUnheldNoteAcceptsAnyFinger()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(-1, 7, NoteState.Scrolling, 0f, At(100), Tolerance),
				Is.False);
		}

		[Test]
		public void AReleasedNoteAcceptsAnyFinger()
		{
			Assert.That(
				LongNote.ShouldIgnoreTouch(3, 7, NoteState.Release, At(100), At(101), Tolerance),
				Is.False);
		}
	}
}
