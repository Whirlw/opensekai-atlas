using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The stretch of DEINOS where the chart plays with a negative scroll speed and then zero: bar 37
	/// holds -325, falls to -0.02 by bar 37.242, hits exactly 0 at bar 37.25 and climbs back to 1.0 by
	/// bar 37.797. The display lead is the window divided by the span average, and that average crosses
	/// zero there - measured for the note at t=48.000: the lead swings 3.49 s, 5.35 s, 6.97 s, 14.1 s,
	/// 2.34 s, 0.88 s, 0.44 s and then 6.45 s in the space of four seconds, and the drawn progress
	/// swings with it from +0.72 to -1.98 and back to +0.81.
	///
	/// What kept that from being visible was the walk's own fence: it stopped at the first note still
	/// outside its own lead, so the notes of bar 38 could not become due before bar 37's. The appearance
	/// table dropped the fence, and those notes came due at 42.733 - 5.27 s early - then jumped from
	/// progress -2.016 to +0.807 in the frame the playhead crossed the negative event (46.749). That is
	/// the burst of keys that are not in the chart, and the bars travelling up from under the line.
	/// </summary>
	public sealed class NegativeSpeedSectionTests
	{
		private const float FirstBar = 37f;
		private const float LastBar = 39f;

		[Test]
		public void NotesInTheNegativeSpeedSectionDoNotTeleport()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			NoteBase[] notes = score.NoteArray.Where(note => note != null).ToArray();
			var worstJump = new Dictionary<NoteBase, float>();
			var detail = new Dictionary<NoteBase, string>();
			var lastProgress = new Dictionary<NoteBase, float>();

			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				float end = notes.Max(note => note.MusicScoreInfo.time) + 3f;
				const float frame = 1f / 60f;
				for (float now = 0f; now < end; now += frame)
				{
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
					foreach (NoteBase note in notes)
					{
						if (note.State == NoteState.Wait)
						{
							continue;
						}

						if (lastProgress.TryGetValue(note, out float previous))
						{
							float jump = System.Math.Abs(note.Progress - previous);
							if (jump > worstJump.GetValueOrDefault(note))
							{
								worstJump[note] = jump;
								detail[note] = $"jumped {previous:F3} -> {note.Progress:F3} at {now:F3}s (lead {note.offsetTime:F4}s)";
							}
						}

						lastProgress[note] = note.Progress;
					}
				}
			}

			// A frame of drawing moves a note by a few hundredths of its travel. Anything past a fifth of
			// the screen is a teleport, not motion.
			var offenders = notes
				.Where(note => InSection(note) && worstJump.GetValueOrDefault(note) > 0.2f)
				.Select(note => $"{note.GetType().Name} id={note.Id} t={note.MusicScoreInfo.time:F3}: "
					+ $"{worstJump[note]:F3} ({detail[note]})")
				.ToArray();

			Assert.That(offenders.Take(8), Is.Empty,
				$"{offenders.Length} notes teleported inside bars {FirstBar}..{LastBar}:\n  "
				+ string.Join("\n  ", offenders.Take(8)));
		}

		[Test]
		public void NotesInTheNegativeSpeedSectionAreNotLookedAtSecondsEarly()
		{
			// The same region, seen as "when did the frame loop first touch this note". With the fence the
			// notes of bar 38 were first looked at between 1.25 s and 1.88 s before their own time; without
			// it they came due 5.27 s early, while the notes in front of them were still untouched.
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			NoteBase[] notes = score.NoteArray.Where(note => note != null).ToArray();
			var firstSeen = new Dictionary<NoteBase, float>();

			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				float end = notes.Max(note => note.MusicScoreInfo.time) + 3f;
				const float frame = 1f / 60f;
				for (float now = 0f; now < end; now += frame)
				{
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
					foreach (NoteBase note in notes)
					{
						if (note.State != NoteState.Wait && !firstSeen.ContainsKey(note))
						{
							firstSeen[note] = now;
						}
					}
				}
			}

			var early = notes
				.Where(note => InSection(note) && firstSeen.ContainsKey(note))
				.Where(note => note.MusicScoreInfo.time - firstSeen[note] > 2.5f)
				.Select(note => $"{note.GetType().Name} id={note.Id} t={note.MusicScoreInfo.time:F3}"
					+ $" first looked at {firstSeen[note]:F3}s ({note.MusicScoreInfo.time - firstSeen[note]:F2}s early)")
				.ToArray();

			Assert.That(early.Take(8), Is.Empty,
				$"{early.Length} notes were looked at seconds before the chart reached them:\n  "
				+ string.Join("\n  ", early.Take(8)));
		}

		private static bool InSection(NoteBase note) =>
			note.MusicScoreInfo.bar >= FirstBar && note.MusicScoreInfo.bar < LastBar;
	}
}
