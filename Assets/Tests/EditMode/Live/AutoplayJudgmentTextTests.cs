using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// In an autoplay session the friction stretch of DEINOS grades nothing but silent markers: the
	/// hidden carriers publish no result, and the generated interior points are skipped by the
	/// judgement presentation. That left the AUTO popup frozen for seconds, because no text was
	/// refreshed at all. The text refreshes now; the effect stays off, as before.
	/// </summary>
	public sealed class AutoplayJudgmentTextTests
	{
		// The friction stretch the report names: bars 78-79, where only markers are graded.
		private const float WindowStart = 98.5f;
		private const float WindowEnd = 101.5f;

		[Test]
		public void TheFrictionStretchRefreshesTheAutoPopup()
		{
			var recorder = Run(out _);
			int refreshes = recorder.TextEvents.Count(entry => entry >= WindowStart && entry <= WindowEnd);

			Assert.That(refreshes, Is.GreaterThanOrEqualTo(5),
				$"only {refreshes} judgement texts were refreshed between {WindowStart} s and {WindowEnd} s");
		}

		[Test]
		public void ASilentMarkerStillNeverPlaysItsEffect()
		{
			Run(out List<NoteBase> effectNotes);

			NoteBase skipInEffect = effectNotes.FirstOrDefault(note => note.IsSkip);
			Assert.That(skipInEffect, Is.Null,
				skipInEffect == null ? "" : $"a skipped marker reached the effect path: id={skipInEffect.Id}");
		}

		private static RecordingView Run(out List<NoteBase> effectNotes)
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture("deinos-phainein");
			using var harness = new LiveHarness();
			harness.Arrange(score, auto: true);
			var recorder = new GameObject("judgement-text-recorder").AddComponent<RecordingView>();
			try
			{
				harness.BindViews(recorder);
				float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 2f;
				for (float now = 0f; now < end; now += 1f / 60f)
				{
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
				}
			}
			finally
			{
				Object.DestroyImmediate(recorder.gameObject);
			}

			effectNotes = recorder.EffectNotes;
			return recorder;
		}

		private sealed class RecordingView : LiveViewBase
		{
			internal readonly List<float> TextEvents = new();
			internal readonly List<NoteBase> EffectNotes = new();

			public override void JudgmentNote(NoteBase note)
			{
				if (note != null)
				{
					EffectNotes.Add(note);
				}
			}

			public override void JudgmentText(NoteBase note)
			{
				if (note != null)
				{
					TextEvents.Add(note.MusicScoreInfo.time);
				}
			}
		}
	}
}
