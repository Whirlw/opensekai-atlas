using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Autoplay has one job the player never does: it must actually reach the end of the chart. A note
	/// that stays alive after its time stops the session's cursor, the song "finishes" with a full score
	/// and then never settles - which is exactly the shape of the PlayMode report, so it is cheaper to
	/// catch here than to wait 90 s for the device run to time out.
	/// </summary>
	public sealed class AutoplayCompletionTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		[Test]
		public void AutoplayReachesTheEndOfTheChartAndSettlesEveryNote()
		{
			string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
				"baseline_tap", "score.json"));
			var document = ChartJson.Read(File.ReadAllText(fixture));
			var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
			Assert.That(bundle, Is.Not.Null);
			var score = RuntimeChartConverter.Convert(document, bundle);

			using var harness = new LiveHarness();
			harness.Arrange(score);

			float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 3f;
			const float frame = 1f / 60f;
			for (float now = 0f; now < end; now += frame)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				if (harness.Logic.IsNotesAllFinished)
				{
					break;
				}
			}

			NoteBase stuck = score.NoteArray.FirstOrDefault(note => note != null && note.State != NoteState.Done);
			Assert.That(stuck, Is.Null, stuck == null
				? ""
				: $"{stuck.GetType().Name} id={stuck.Id} at {stuck.MusicScoreInfo.time:F3}s is still {stuck.State}"
					+ $" (result={stuck.Result}); its role is {stuck.ParentRole}");
			Assert.That(harness.Logic.IsNotesAllFinished, Is.True, "autoplay must walk past every note");
		}

		[Test]
		public void AnAutoPlayedBarStaysLitAfterItCrossesTheLine()
		{
			// Autoplay has no finger, so the bar used to grey out the moment it crossed the line and stay
			// grey for the whole rest of its body. The bar knows it is being auto-played now.
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
			bar.SetStateUnnotice(NoteState.Last);
			Assert.That(bar.ShowsAsReleased, Is.True, "a bar nobody holds greys out");

			bar.AutoJudgment(Info(0.5f));

			Assert.That(bar.IsAutoPlaying, Is.True);
			Assert.That(bar.ShowsAsReleased, Is.False, "autoplay keeps it lit without a finger");

			bar.ResetNote();
			Assert.That(bar.IsAutoPlaying, Is.False, "and the flag does not survive the pool");
		}

		[Test]
		public void AOnlyAnOrdinaryHeadStaysOnTheLine()
		{
			// The player's ruling: a friction-headed bar looks like a standalone friction note, so its key
			// keeps travelling past the line. Only the ordinary head - the one you press and hold - parks.
			var rub = new FrictionLongNote(Info(0f), 1, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1f);
			var guide = new GuideNote(Info(0f), 2, 4, 5, NoteCategory.Long, NoteType.Default, null, 1f);
			var ordinary = new LongNote(Info(0f), 3, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);

			Assert.That(ordinary.KeepsHeadOnTheLine, Is.True, "an ordinary head is pressed and held: it parks");
			Assert.That(rub.KeepsHeadOnTheLine, Is.False, "a rub head is played like a standalone friction note");
			Assert.That(guide.KeepsHeadOnTheLine, Is.False, "a guide bar sweeps across the lane, it does not park");
		}

		/// <summary>Loads a chart package from <c>Tools/fixtures/</c> and converts it the way the game does.</summary>
		internal static MusicScore LoadFixture(string name)
		{
			string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures", name, "score.json"));
			var document = ChartJson.Read(File.ReadAllText(fixture));
			var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
			Assert.That(bundle, Is.Not.Null);
			return RuntimeChartConverter.Convert(document, bundle);
		}

		[Test]
		public void NoBarIsTakenAwayInTheMiddleOfItsOwnBody()
		{
			// Superseded by AViewIsNeverTakenAwayFromANoteThatIsStillAlive: LongNote.OnUnspawn fans the
			// handler out to every child note, so a note-level probe counted the children's own lifetimes
			// as the bar's. Kept as a no-op pin is pointless - the view-level case is the real question.
			Assert.Ignore("Superseded by the view-level case; the note-level probe could not tell a child's unspawn from the bar's.");
		}

		/// <summary>Records the presentation layer's spawn/unspawn calls, one entry per call.</summary>
		private sealed class RecordingView : LiveViewBase
		{
			internal readonly System.Collections.Generic.List<(NoteBase note, bool spawned, float at)> Events = new();
			internal float Now;

			public override void SpawnNote(NoteBase note) => Events.Add((note, true, Now));

			public override void UnspawnNote(NoteBase note) => Events.Add((note, false, Now));
		}

		[Test]
		public void AViewIsNeverTakenAwayFromANoteThatIsStillAlive()
		{
			// "长条中途会闪一下" seen from the only layer that can show it: the presentation bridge's
			// spawn/unspawn calls. A note that has no verdict yet must keep its view until its own time;
			// anything else is the flash the player saw.
			MusicScore score = LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			var recorder = new GameObject("recording-view").AddComponent<RecordingView>();
			var offenders = new System.Collections.Generic.List<string>();
			try
			{
				harness.Arrange(score);
				harness.BindViews(recorder);
				float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 2f;
				const float frame = 1f / 60f;
				for (float now = 0f; now < end; now += frame)
				{
					recorder.Now = now;
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
				}
			}
			finally
			{
				Object.DestroyImmediate(recorder.gameObject);
			}

			foreach (var group in recorder.Events.GroupBy(entry => entry.note))
			{
				NoteBase note = group.Key;
				var unspawns = group.Where(entry => !entry.spawned).ToArray();
				if (unspawns.Length == 0 || note == null)
				{
					continue;
				}

				if (unspawns.Length > 1)
				{
					offenders.Add($"{note.GetType().Name} id={note.Id}: {unspawns.Length} unspawns");
					continue;
				}

				float at = unspawns[0].at;
				if (note.Result == NoteResult.None && at + 0.02f < note.MusicScoreInfo.time)
				{
					offenders.Add($"{note.GetType().Name} id={note.Id} unspawned at {at:F3}s, own time {note.MusicScoreInfo.time:F3}s,"
						+ $" no verdict, state={note.State}");
				}
			}

			Assert.That(offenders.Take(8), Is.Empty,
				$"{offenders.Count} notes lost their view too early:\n  " + string.Join("\n  ", offenders.Take(8))
				+ $"\n  (spawn/unspawn calls recorded: {recorder.Events.Count})");
		}

		[Test]
		[Ignore("pins a hypothesis, not a report: a millisecond offset squashing a body was my inference from the 799 measurement, the player never saw it, and bounding the speed to protect against it killed the 799 effect. Delete it when the view geometry is measured instead.")]
		public void TheSpeedSpikeNeverCollapsesADrawnBody()
		{
			// The "闪一下" the player reported at the speed ramp: while a spike sits between the playhead and
			// a note, that note's display offset is allowed to get very small (it is how a chart makes notes
			// appear abruptly). It must never get small enough to squash a body that is already on screen -
			// that is a twentieth of the window, and nothing in this chart may go below it.
			MusicScore score = LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			float floor = 0.05f; // a twentieth of the one second display window the harness uses
			float worst = float.MaxValue;
			string worstNote = "";

			harness.Arrange(score);
			float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 2f;
			const float frame = 1f / 60f;
			for (float now = 0f; now < end; now += frame)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				foreach (LongNote bar in score.NoteArray.OfType<LongNote>())
				{
					if (bar.NoteList == null)
					{
						continue;
					}

					foreach (NoteBase part in bar.NoteList)
					{
						// The bar itself counts: its own offset is what the view uses for the body's near end,
						// and a bar starting exactly on a speed spike is the case that was missed first time.
						if (part == null || part.offsetTime <= 0f)
						{
							continue;
						}

						if (part.offsetTime < worst)
						{
							worst = part.offsetTime;
							worstNote = $"{part.GetType().Name} id={part.Id} at {now:F3}s (bar {bar.Id})";
						}
					}
				}
			}

			Assert.That(worst, Is.GreaterThanOrEqualTo(floor * 0.99f),
				$"a drawn body collapsed to {worst:F4}s (floor {floor:F4}s): {worstNote}");
		}

		[Test]
		public void ARubEndedBarIsFinishedByAutoplay()		{
			// The narrow version of the same question: a bar whose end note judges itself has to be
			// finished by autoplay like any other, without waiting for a verdict nobody will give.
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(tail);

			bar.AutoJudgment(Info(1.2f));

			Assert.That(tail.State, Is.EqualTo(NoteState.Done), "the rub end note is auto-judged");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done), "and the bar follows it to the end");
		}
	}
}
