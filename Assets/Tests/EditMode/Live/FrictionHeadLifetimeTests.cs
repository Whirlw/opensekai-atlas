using System.Collections.Generic;
using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A rub-headed bar's key lives like a standalone friction note: it travels to the line, and the
	/// moment the head is judged - rubbed, auto-played or missed - the key is gone and the judgement
	/// effect is played on the head's own friction identity. The body goes on without it. An ordinary
	/// head keeps its key on the line, as before.
	/// </summary>
	public sealed class FrictionHeadLifetimeTests
	{
		private const float Frame = 1f / 60f;
		private const double InputBase = 10d;

		private LiveHarness _harness;
		private GameObject _viewObject;
		private RecordingView _view;

		private sealed class RecordingView : LiveViewBase
		{
			internal readonly List<(NoteCategory category, NoteResult result, float at)> Judged = new();
			internal float Now;

			public override void JudgmentNote(NoteBase note) => Judged.Add((note.Category, note.Result, Now));
		}

		[SetUp]
		public void SetUp()
		{
			_harness = new LiveHarness();
			_viewObject = new GameObject("recording-view");
			_view = _viewObject.AddComponent<RecordingView>();
		}

		[TearDown]
		public void TearDown()
		{
			_harness.Dispose();
			Object.DestroyImmediate(_viewObject);
		}

		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static Vector3 Position =>
			Vector3.Lerp(LiveConfig.JudgmentPositions[4], LiveConfig.JudgmentPositions[5], 0.5f);

		private LongNote Arrange(LongNote bar, bool auto = false)
		{
			bar.AddNoteListAndSetupChildNote(new FrictionNote(Info(2f), bar.Id + 1, 4, 5,
				NoteCategory.Friction, null, NoteType.Default, 1f));
			_harness.Arrange(new MusicScore(new[] { bar.MusicScoreInfo }, System.Array.Empty<EventBase>(),
				new NoteBase[] { bar }), auto: auto);
			_harness.BindViews(_view);
			return bar;
		}

		private static FrictionLongNote RubBar() =>
			new FrictionLongNote(Info(1f), 1, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1f);

		private void Step(float now, bool auto, params LiveTouchSample[] touches)
		{
			_view.Now = now;
			if (auto)
			{
				_harness.OnUpdate(now, InputBase + now);
				_harness.Logic.OnAutoInput();
				_harness.OnView();
				return;
			}

			_harness.Step(now, InputBase + now, touches);
		}

		private static LiveTouchSample Rest(float now, TouchPhase phase = TouchPhase.Stationary) =>
			new LiveTouchSample(1, new Vector2(800f, 600f), Position, InputBase + now, phase, Vector2.zero);

		/// <summary>Steps until the head settles; returns the frame it did, or NaN.</summary>
		private float RunUntilHeadSettles(LongNote bar, bool auto, bool touch, float until)
		{
			bool began = false;
			for (float now = 0f; now < until; now += Frame)
			{
				bool keyGoneBefore = bar.HeadKeyLeftWithItsVerdict;
				Assert.That(keyGoneBefore, Is.False, $"the key is still there before the verdict (t={now:F3})");
				if (touch && now >= 0.9f)
				{
					Step(now, auto, Rest(now, began ? TouchPhase.Stationary : TouchPhase.Began));
					began = true;
				}
				else
				{
					Step(now, auto);
				}

				if (bar.Head.IsSettled)
				{
					return now;
				}
			}

			return float.NaN;
		}

		private void AssertKeyLeftWithTheEffect(LongNote bar, float settledAt, NoteResult expected, bool effect)
		{
			Assert.That(float.IsNaN(settledAt), Is.False, "the head has to be judged");
			Assert.That(bar.Head.Result, Is.EqualTo(expected));
			Assert.That(bar.HeadKeyLeftWithItsVerdict, Is.True, "the key leaves on the frame of the verdict");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the body goes on without its key");

			var onFrame = _view.Judged.FindAll(j => j.at == settledAt && j.category == NoteCategory.FrictionLong);
			Assert.That(onFrame.Count, Is.EqualTo(effect ? 1 : 0),
				"the head's verdict reaches the view once, as a friction note, on the same frame");
			if (effect)
			{
				Assert.That(onFrame[0].result, Is.EqualTo(expected));
			}
		}

		[Test]
		public void ARubbedHeadLeavesTheMomentItIsJudged()
		{
			LongNote bar = Arrange(RubBar());
			float settledAt = RunUntilHeadSettles(bar, auto: false, touch: true, until: 1.3f);
			AssertKeyLeftWithTheEffect(bar, settledAt, NoteResult.JustPerfect, effect: true);
		}

		[Test]
		public void AnAutoPlayedRubHeadLeavesTheMomentItIsJudged()
		{
			LongNote bar = Arrange(RubBar(), auto: true);
			float settledAt = RunUntilHeadSettles(bar, auto: true, touch: false, until: 1.3f);
			AssertKeyLeftWithTheEffect(bar, settledAt, NoteResult.Auto, effect: true);
		}

		[Test]
		public void AMissedRubHeadLeavesWithItsMiss()
		{
			LongNote bar = Arrange(RubBar());
			float settledAt = RunUntilHeadSettles(bar, auto: false, touch: false, until: 1.8f);
			Assert.That(settledAt, Is.GreaterThan(1f), "an untouched rub head travels past the line first");
			Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Miss));
			Assert.That(bar.HeadKeyLeftWithItsVerdict, Is.True, "a missed key leaves too, like a missed friction note");
		}

		[Test]
		public void AnOrdinaryHeadKeepsItsKeyOnTheLine()
		{
			var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			Arrange(bar);
			for (float now = 0f; now < 1f - Frame * 0.5f; now += Frame)
			{
				Step(now, false);
			}

			Step(1f, false, Rest(1f, TouchPhase.Began));
			Step(1f + Frame, false, Rest(1f + Frame));
			Assert.That(bar.Head.IsSettled, Is.True);
			Assert.That(bar.HeadKeyLeftWithItsVerdict, Is.False, "an ordinary head parks on the line");
		}
	}
}
