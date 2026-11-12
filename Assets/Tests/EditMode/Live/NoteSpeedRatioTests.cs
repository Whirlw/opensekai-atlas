using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The display speed of a note is the speed of the chart segment the note sits in. A note is not
	/// averaged with the segments between it and the playhead: that average is what let a 9 ms 799
	/// spike fling the notes behind it ~5 screens up and snap them back onto the screen once the spike
	/// had passed the playhead.
	/// </summary>
	public sealed class NoteSpeedRatioTests
	{
		private static MusicScoreInfo Speed(float barProgress, float speedRatio)
		{
			// bar, barProgress, time, bpm, timeSignature, speedRatio, seVolume
			return new MusicScoreInfo(0, barProgress, barProgress * 2f, 120f, 4f, speedRatio, 1f);
		}

		private static MusicScoreInfo[] ThreeSpeeds() => new[]
		{
			Speed(0f, 1f), Speed(2f, 2f), Speed(4f, 0.5f)
		};

		[Test]
		public void NoteBehindThePlayheadUsesTheFallback()
		{
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 4f) }, 5f, 3f, 1.5f), Is.EqualTo(1.5f));
		}

		[Test]
		public void MissingSpeedEventsUseTheFallback()
		{
			Assert.That(NoteSpeedRatio.Resolve(null, 0f, 4f, 2f), Is.EqualTo(2f));
			Assert.That(NoteSpeedRatio.Resolve(new MusicScoreInfo[0], 0f, 4f, 2f), Is.EqualTo(2f));
		}

		[Test]
		public void ZeroLengthSpanFallsBack()
		{
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 3f) }, 1f, 1f, 4f), Is.EqualTo(4f));
		}

		[Test]
		public void SingleEventAppliesForTheWholeSpan()
		{
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 2f) }, 0f, 1f, 9f), Is.EqualTo(2f));
		}

		[Test]
		public void NotePastTheLastEventKeepsTheLastSpeed()
		{
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 5f) }, 0f, 10f, 1f), Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void TheNoteKeepsItsOwnSegmentsSpeedInsteadOfAnAverage()
		{
			// The old span average answered 2 here: (2 * 1 + 2 * 3) / 4. The note is in the 3 segment.
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 1f), Speed(2f, 3f) }, 0f, 4f, 9f),
				Is.EqualTo(3f).Within(1e-5f));
		}

		[Test]
		public void AnEventDoesNotReachTheNotesBeforeIt()
		{
			// "上游这四个note完全没有变速": a note before the spike is drawn with the speed it was written
			// with, whatever the playhead is doing. 1.5 sits before the 799 event at 2.
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 1f), Speed(2f, 799f) }, 0.5f, 1.5f, 9f),
				Is.EqualTo(1f).Within(1e-5f));
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 1f), Speed(2f, 799f) }, 0.5f, 2.5f, 9f),
				Is.EqualTo(799f).Within(1e-5f));
		}

		[Test]
		public void NoteOnAnEventBoundaryTakesTheNewSegment()
		{
			// An event applies from its own tick on, which is how the chart's own high speed lookup reads.
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 1f), Speed(2f, 3f) }, 0f, 2f, 9f),
				Is.EqualTo(3f).Within(1e-5f));
		}

		[Test]
		public void EachSegmentAnswersWithItsOwnSpeed()
		{
			Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, 1f, 9f), Is.EqualTo(1f).Within(1e-5f));
			Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, 3f, 9f), Is.EqualTo(2f).Within(1e-5f));
			Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, 6f, 9f), Is.EqualTo(0.5f).Within(1e-5f));
		}

		[Test]
		public void ThePlayheadPositionDoesNotChangeTheAnswer()
		{
			// The playhead only says whether the note is still ahead of it; the note's speed is its own.
			Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 0f, 3f, 9f), Is.EqualTo(2f).Within(1e-5f));
			Assert.That(NoteSpeedRatio.Resolve(ThreeSpeeds(), 2.5f, 3f, 9f), Is.EqualTo(2f).Within(1e-5f));
		}

		[Test]
		public void NegativeSpeedIsKeptWithoutBeingFlipped()
		{
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, -2f) }, 0f, 1f, 1f), Is.EqualTo(-2f).Within(1e-5f));
			Assert.That(NoteSpeedRatio.Resolve(new[] { Speed(0f, 1f), Speed(2f, -325f) }, 0f, 3f, 1f),
				Is.EqualTo(-325f).Within(1e-5f));
		}
	}
}
