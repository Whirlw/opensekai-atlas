using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// When a note's view is created relative to the moment it becomes visible. The lead is a span average,
	/// so the moment a note first shows is whatever the formula says - but creation must not lag behind it:
	/// that lag is what used to create a dozen notes in a single frame, part way down the screen.
	/// </summary>
	public sealed class NoteSpawnTopEdgeTests
	{
		/// <summary>The player's own setting (NoteSpeed 10.6) maps to this display window.</summary>
		private const float Window = 0.5952f;
		private const float Frame = 1f / 60f;

		[Test]
		public void AViewIsNeverCreatedLaterThanTheFrameTheNoteBecomesVisible()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			harness.Arrange(score, null, Window);

			NoteBase[] notes = score.NoteArray.Where(note => note != null).ToArray();
			var created = new Dictionary<NoteBase, float>();
			var visibleFrom = new Dictionary<NoteBase, float>();
			float end = notes.Max(note => note.MusicScoreInfo.time) + 1f;
			for (float now = 0f; now < end; now += Frame)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				foreach (NoteBase note in notes)
				{
					if (note.offsetTime <= 0f)
					{
						continue;
					}

					float progress = 1f - ((note.MusicScoreInfo.time - now) / note.offsetTime);
					if (progress >= 0f)
					{
						visibleFrom.TryAdd(note, now);
					}

					if (note.State == NoteState.Scrolling)
					{
						created.TryAdd(note, now);
					}
				}
			}

			var late = new List<(string note, float by, float progress)>();
			foreach (var pair in created)
			{
				if (!visibleFrom.TryGetValue(pair.Key, out float visible))
				{
					continue;
				}

				float lag = pair.Value - visible;
				if (lag > Frame * 1.5f)
				{
					float progress = 1f - ((pair.Key.MusicScoreInfo.time - visible) / pair.Key.offsetTime);
					NoteBase note = pair.Key;
					late.Add(($"{note.GetType().Name} id={note.Id} bar"
						+ $" {note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress:F4}", lag, progress));
				}
			}

			var text = new StringBuilder($"{late.Count} of {created.Count} notes were created late:");
			foreach (var entry in late.OrderByDescending(entry => entry.by).Take(8))
			{
				text.Append($"\n  {entry.note} by {entry.by:F3}s at p={entry.progress:F3}");
			}

			Assert.That(late, Is.Empty, text.ToString());
		}
	}
}
