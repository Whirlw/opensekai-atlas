using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
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
		public void ARubBarsHeadStaysOnTheLine()
		{
			// "长条的 friction 头会流下判定线" - the pin at the line used to be tied to IsOrdinaryHead,
			// which also answers "who may be judged", so a rub could not be pinned without changing judging.
			var rub = new FrictionLongNote(Info(0f), 1, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1f);
			var guide = new GuideNote(Info(0f), 2, 4, 5, NoteCategory.Long, NoteType.Default, null, 1f);

			Assert.That(rub.KeepsHeadOnTheLine, Is.True, "a rub bar is held like any other, so its head parks");
			Assert.That(guide.KeepsHeadOnTheLine, Is.False, "a guide bar sweeps across the lane, it does not park");
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
