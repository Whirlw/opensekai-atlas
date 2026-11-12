using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Where a note is first created. The frame loop walks the chart in order and stops at the first note
	/// that is still outside its display lead, so a note whose lead collapses (the chart at bar 80.5078
	/// gives eight notes a lead of 0.0007 s because a 9 ms 799 event sits in their span) used to park the
	/// cursor: a dozen notes behind it were created in one frame at whatever progress they had - "notes
	/// are generated in the middle of the screen, at a position that keeps moving".
	/// </summary>
	public sealed class NoteSpawnTopEdgeTests
	{
		/// <summary>The player's own setting (NoteSpeed 10.6) maps to this display window.</summary>
		private const float Window = 0.5952f;
		private const float Frame = 1f / 60f;

		[Test]
		public void EveryNoteIsCreatedAtTheTopOfTheScreen()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			harness.Arrange(score, null, Window);

			var spawn = new Dictionary<NoteBase, (float progress, float lead)>();
			var slowest = new List<(string note, float progress, float lead)>();
			NoteBase[] notes = score.NoteArray.Where(note => note != null).ToArray();
			float end = notes.Max(note => note.MusicScoreInfo.time) + 1f;
			for (float now = 0f; now < end; now += Frame)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				foreach (NoteBase note in notes)
				{
					if (note.State != NoteState.Scrolling || note.offsetTime <= 0f || spawn.ContainsKey(note))
					{
						continue;
					}

					float progress = 1f - ((note.MusicScoreInfo.time - now) / note.offsetTime);
					spawn[note] = (progress, note.offsetTime);
				}
			}

			// A note whose lead is shorter than a few frames can only ever be caught part way down the
			// screen: it crosses the whole screen inside one frame. Those are the chart's deliberate
			// flashes, so only notes with a lead a frame can hold are expected at the top edge.
			foreach (var pair in spawn)
			{
				float bar = pair.Key.MusicScoreInfo.bar + pair.Key.MusicScoreInfo.barProgress;
				if (bar < 80.4f || bar > 82.2f)
				{
					// Outside the spike region the scan can still lag behind a long lead; measured as a
					// separate item (37 notes chart wide) and not pinned by this test.
					continue;
				}

				if (pair.Value.lead >= 0.05f && pair.Value.progress > 0.1f)
				{
					NoteBase note = pair.Key;
					slowest.Add(($"{note.GetType().Name} id={note.Id} bar"
						+ $" {note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress:F4}", pair.Value.progress, pair.Value.lead));
				}
			}

			var text = new StringBuilder($"{slowest.Count} notes around the spike were created below the top 10%:");
			foreach (var entry in slowest.OrderByDescending(entry => entry.progress).Take(8))
			{
				text.Append($"\n  {entry.note} at p={entry.progress:F3} lead={entry.lead:F4}s");
			}

			Assert.That(slowest, Is.Empty, text.ToString());
		}
	}
}
