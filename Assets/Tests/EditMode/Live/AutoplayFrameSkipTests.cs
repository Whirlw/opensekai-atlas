using System;
using System.IO;
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
	/// Autoplay used to answer notes AFTER the frame walk, and the walk is what turns anything past
	/// its window into a Miss. So any frame that skipped time - a hitch, the first frame after the
	/// chart loaded - missed notes autoplay never got to answer. On DEINOS that showed up as a score
	/// below 1,000,000 and bars losing life in the middle of their own body.
	/// </summary>
	public sealed class AutoplayFrameSkipTests
	{
		private const float MissWindowSeconds = 0.25f;

		[Test]
		public void AutoplayOnDeinosKeepsAFullScoreWhenFramesAreDropped()
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
			Assert.That(bundle, Is.Not.Null);

			using var harness = new LiveHarness();
			var scoreLogic = new ScoreLogic(bundle);
			harness.Arrange(score, auto: true);
			harness.Logic.scoreLogic = scoreLogic;
			scoreLogic.Setup(null, score);
			harness.BindViews();

			float end = score.NoteArray[score.NoteArray.Length - 1].MusicScoreInfo.time + 3f;
			for (float now = 0f; now < end;)
			{
				harness.OnUpdate(now, 10d + now);
				harness.Logic.OnAutoInput();
				// Every fifth second one frame is late by 150 ms, as a stutter on the device is.
				now += Mathf.Abs(Mathf.Round(now * 60f) % 300f) < 0.5f ? 0.15f : 1f / 60f;
			}

			Assert.That(scoreLogic.Score.missCount, Is.EqualTo(0),
				$"{scoreLogic.Score.missCount} notes were missed in autoplay");
			Assert.That(scoreLogic.Score.life, Is.GreaterThan(0f), "autoplay must not die");
			Assert.That(scoreLogic.Score.autoCount, Is.EqualTo(scoreLogic.Score.totalComboCount));
			Assert.That(scoreLogic.Score.totalScore, Is.EqualTo(1000000));
		}

		[Test]
		public void ANoteWhoseWindowClosedBetweenFramesIsStillAutoPlayed()
		{
			// The frame jumps from 0.9 s to 1.3 s: the note at 1.0 s is already 300 ms late, past
			// every window. Autoplay still owns it, because nobody was asked to press anything.
			var later = new MusicScoreInfo(0, 0f, 1f, 120f, 4f, 1f, 1f);
			var note = new NormalNote(later, 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			var score = new MusicScore(new[] { later }, Array.Empty<EventBase>(), new NoteBase[] { note });

			using var harness = new LiveHarness();
			harness.Arrange(score, auto: true);
			harness.OnUpdate(0.9f, 10.9d);
			harness.OnUpdate(0.9f + MissWindowSeconds + 0.05f, 11.2d);

			Assert.That(note.Result, Is.EqualTo(NoteResult.Auto),
				"a late frame must not decide a note autoplay was going to answer");
		}

		[Test]
		public void ABarWhoseHeadWindowClosedBetweenFramesKeepsItsHead()
		{
			// The same frame jump over a bar: its head and its interior point both have to be answered
			// by autoplay, not by the walk that runs out the window.
			var start = new MusicScoreInfo(0, 0f, 1f, 120f, 4f, 1f, 1f);
			var end = new MusicScoreInfo(0, 0f, 3f, 120f, 4f, 1f, 1f);
			var bar = new LongNote(start, 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(end, 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var score = new MusicScore(new[] { start, end }, Array.Empty<EventBase>(), new NoteBase[] { bar });

			using var harness = new LiveHarness();
			harness.Arrange(score, auto: true);
			harness.OnUpdate(0.9f, 10.9d);
			harness.OnUpdate(0.9f + MissWindowSeconds + 0.05f, 11.2d);

			Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Auto),
				"autoplay owns the head even when the frame skipped its window");
			Assert.That(bar.Result, Is.Not.EqualTo(NoteResult.Miss));
		}
	}
}
