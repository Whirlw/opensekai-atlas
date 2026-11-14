using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A hidden interior point shapes the bar and draws nothing. It used to get a view like any other
	/// connection, which put a diamond on screen that nobody was meant to press - and, once it stopped
	/// being judged, one that slid past the line forever.
	/// </summary>
	public sealed class InteriorPointViewTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static (MusicScore score, ConnectionNote point, LongNote bar) Chart(bool skip)
		{
			var start = Info(0f);
			var end = Info(2f);
			var bar = new LongNote(start, 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(end, 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var point = new ConnectionNote(Info(0.5f), 3, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);
			point.SetSkip(skip);
			bar.AddConnectionNote(point);
			bar.SortNoteList();
			var score = new MusicScore(new[] { start, end }, System.Array.Empty<EventBase>(), new NoteBase[] { bar });
			return (score, point, bar);
		}

		[Test]
		public void AHiddenPointNeverSpawnsAView()
		{
			var (score, point, _) = Chart(skip: true);
			Running recorder = Run(score);

			Assert.That(recorder.Spawned.Where(n => ReferenceEquals(n, point)), Is.Empty,
				"a hidden point draws nothing");
		}

		[Test]
		public void AVIsiblePointStillSpawnsAView()
		{
			var (score, point, _) = Chart(skip: false);
			Running recorder = Run(score);

			Assert.That(recorder.Spawned.Where(n => ReferenceEquals(n, point)), Is.Not.Empty,
				"a drawn point still gets its diamond");
		}

		[Test]
		public void AHiddenPointEndsAsPassInsteadOfSlidingForever()
		{
			var (score, point, _) = Chart(skip: true);
			Run(score);

			Assert.That(point.State, Is.EqualTo(NoteState.Done),
				"the point leaves the play state instead of sliding past the line");
			Assert.That(point.Result, Is.EqualTo(NoteResult.Pass), "and it is not a miss either");
		}

		private static Running Run(MusicScore score)
		{
			using var harness = new LiveHarness();
			var recorder = new GameObject("interior-point-recorder").AddComponent<Running>();
			try
			{
				harness.Arrange(score, auto: true);
				harness.BindViews(recorder);
				for (float now = 0f; now < 3f; now += 1f / 60f)
				{
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
				}
			}
			finally
			{
				Object.DestroyImmediate(recorder.gameObject);
			}

			return recorder;
		}

		private sealed class Running : LiveViewBase
		{
			internal readonly List<NoteBase> Spawned = new();

			public override void SpawnNote(NoteBase note)
			{
				if (note != null)
				{
					Spawned.Add(note);
				}
			}
		}
	}
}
