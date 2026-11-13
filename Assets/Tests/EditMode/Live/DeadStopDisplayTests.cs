using System.Linq;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The chart's dead stop. deinos-phainein ramps its scroll speed through zero at tick 71505..71520 and
	/// then holds speed 0.0 until tick 72000 - a quarter bar, t = 47.07..47.37 s. window / 0 is an offset of
	/// zero, and an offset of zero is not a slow note: NoteBase.CalcProgress answers 1 (the line),
	/// NoteLineProgress.Body clamps every sample to 1, and NoteLineView draws no mesh at all for a segment
	/// whose end offset is ~0. Measured before the fix: 19 frames in which every bar in play drew nothing,
	/// then the bars came back - the "到了判定线后整个消失几帧再出现" report.
	///
	/// The fix is in NoteDisplayOffset: a stop keeps the offset the note already had. These cases pin that
	/// the fixture still contains the stop (so they cannot quietly stop covering it) and that a bar in play
	/// keeps a drawable offset through it.
	/// </summary>
	public sealed class DeadStopDisplayTests
	{
		private const float Window = 0.5952f;
		private const float Frame = 1f / 60f;
		private const float StopFrom = 47.0f;
		private const float StopTo = 47.5f;

		[Test]
		public void TheChartReallyDoesStop()
		{
			int stopped = 0;
			using var harness = new LiveHarness();
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			harness.Arrange(score, null, Window);
			harness.BindViews();
			for (float now = 0f; now < StopTo; now += Frame)
			{
				harness.Step(now, 10d + now);
				if (now >= StopFrom && Mathf.Approximately(MusicScore.CurrentFrameInfo.speedRatio, 0f))
				{
					stopped++;
				}
			}

			Assert.That(stopped, Is.GreaterThanOrEqualTo(10),
				"the fixture is what these cases are about; if the stop is gone they cover nothing");
		}

		[Test]
		public void ABarInPlayKeepsADrawableOffsetThroughTheStop()
		{
			using var harness = new LiveHarness();
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			harness.Arrange(score, null, Window);
			harness.BindViews();
			LongNote[] bars = score.NoteArray.OfType<LongNote>().ToArray();
			int framesWithABarInPlay = 0;
			var offenders = new System.Collections.Generic.List<string>();
			for (float now = 0f; now < StopTo; now += Frame)
			{
				harness.Step(now, 10d + now);
				if (now < StopFrom)
				{
					continue;
				}

				foreach (LongNote bar in bars)
				{
					// Only a bar the player could be looking at: it has reached the screen and it has not
					// been finished.
					if (bar.State == NoteState.Done || bar.Progress <= 0f)
					{
						continue;
					}

					framesWithABarInPlay++;
					foreach (NoteBase part in PartsOf(bar))
					{
						float offset = part.CalcTimeOffset(part);
						if (Mathf.Approximately(offset, 0f) || offset < 0f)
						{
							offenders.Add($"{now:F3}s {part.GetType().Name} id={part.Id} bar@{bar.MusicScoreInfo.time:F3}"
								+ $" offset={offset:F5} speed={MusicScore.CurrentFrameInfo.speedRatio:F3} state={part.State}");
						}
					}
				}
			}

			Assert.That(framesWithABarInPlay, Is.GreaterThan(0),
				"nothing was in play during the stop, so the case proves nothing");
			Assert.That(offenders, Is.Empty, "a zero offset is what makes the mesh disappear:\n  "
				+ string.Join("\n  ", offenders.Take(10)));
		}

		/// <summary>The bar and the notes that give it its body, which is what the mesh is built from.</summary>
		private static System.Collections.Generic.IEnumerable<NoteBase> PartsOf(LongNote bar)
		{
			yield return bar;
			foreach (INote part in bar.LineNoteList)
			{
				if (part is NoteBase note && !note.IsSkip)
				{
					yield return note;
				}
			}
		}
	}
}
