using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// How early the per-frame scan looks at a note. The scan walks the chart in note order and stops at
	/// the first note still outside its display lead, so a fast note with a lead of a few milliseconds
	/// used to hold the cursor and keep it from even looking at the notes behind it. This pins the
	/// weaker thing the stop test can promise for every note: it is looked at a whole display window
	/// before its time.
	/// </summary>
	public sealed class NoteSpawnTimingTests
	{
		private const float Window = 2f;
		private const float Frame = 1f / 60f;

		[Test]
		public void TheScanLooksAtEveryNoteAtLeastADisplayWindowAhead()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			harness.Arrange(score, null, Window);

			var late = new List<string>();
			var firstLookedAt = new Dictionary<NoteBase, float>();
			float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 1f;
			for (float now = 0f; now < end; now += Frame)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				foreach (NoteBase note in score.NoteArray)
				{
					// offsetTime is written by Excute, i.e. it stays zero until the scan has looked at
					// the note - the only honest way to ask "when did the frame loop first see it".
					if (note != null && note.offsetTime != 0f)
					{
						firstLookedAt.TryAdd(note, now);
					}
				}
			}

			foreach (var pair in firstLookedAt)
			{
				float lead = pair.Key.MusicScoreInfo.time - pair.Value;
				if (lead + Frame < Window)
				{
					late.Add($"{pair.Key.GetType().Name} id={pair.Key.Id} first looked at {lead:F3}s before its time");
				}
			}

			Assert.That(late.Take(8), Is.Empty,
				$"{late.Count} of {firstLookedAt.Count} notes were first looked at less than one {Window}s window ahead:\n  "
				+ string.Join("\n  ", late.Take(8)));
		}
	}
}
