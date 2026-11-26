using System.Collections.Generic;
using NUnit.Framework;
using OpenSekai.Charts;
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
		private const float FRAME = 1f / 60f;
		private const double INPUT_BASE = 10d;

		private LiveHarness _harness;
		private GameObject _viewObject;
		private RecordingView _view;

		private sealed class RecordingView : LiveViewBase
		{
			internal readonly List<(NoteCategory category, NoteResult result, float at)> Judged = new();
			internal float Now;
			internal int InputFeedbackCount;
			internal override void TraceInputFeedback(NoteBase note) => InputFeedbackCount++;
			internal readonly List<NoteBase> Removed = new();
			public override void UnspawnNote(NoteBase note) => Removed.Add(note);

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
			LiveConfig.HasVisualLatency = false;
			LiveConfig.ViewLeadSeconds = 0;
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
				_harness.OnUpdate(now, INPUT_BASE + now);
				_harness.Logic.OnAutoInput();
				_harness.OnView();
				return;
			}

			_harness.Step(now, INPUT_BASE + now, touches);
		}

		private static LiveTouchSample Rest(float now, TouchPhase phase = TouchPhase.Stationary) =>
			new LiveTouchSample(1, new Vector2(800f, 600f), Position, INPUT_BASE + now, phase, Vector2.zero);

		/// <summary>Steps until the head settles; returns the frame it did, or NaN.</summary>
		private float RunUntilHeadSettles(LongNote bar, bool auto, bool touch, float until)
		{
			bool began = false;
			for (float now = 0f; now < until; now += FRAME)
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
			AssertKeyLeftWithTheEffect(bar, settledAt, NoteResult.Perfect, effect: true);
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

		[TestCase(-0.09f, NoteResult.Miss)]
		[TestCase(-0.08f, NoteResult.Perfect)]
		[TestCase(0.08f, NoteResult.Perfect)]
		[TestCase(0.09f, NoteResult.Miss)]
		public void RubHeadUsesTheTraceContactWindow(float offset, NoteResult expected)
		{
			var bar = Arrange(RubBar());
			Step(0.75f, false);
			float contact = 1f + offset;
			Step(contact, false, Rest(contact, TouchPhase.Began));
			Step(contact + 0.001f, false, Rest(contact + 0.001f, TouchPhase.Ended));
			Step(1.2f, false);
			Assert.That(bar.Head.Result, Is.EqualTo(expected));
		}

		[TestCase(false, false, -0.1f, 0.95f, false)]
		[TestCase(true, false, -0.1f, 0.95f, false)]
		[TestCase(false, true, -0.1f, 0.95f, false)]
		[TestCase(true, true, -0.1f, 0.95f, false)]
		[TestCase(false, false, -0.04f, 1.06f, true)]
		[TestCase(true, true, -0.04f, 1.06f, true)]
		[TestCase(false, false, 0f, 0.95f, false)] // Opposing visual and judge offsets cancel on the chart clock.
		[TestCase(true, false, 0.1f, 0.95f, true)]
		[TestCase(false, true, 0.1f, 0.95f, true)]
		public void TraceSettlesOnInputAndPresentsOnceAtTheVisibleLine(bool flick, bool native, float lead, float hit, bool alreadyAtLine)
		{
			LiveConfig.HasVisualLatency = true;
			LiveConfig.ViewLeadSeconds = lead;
			NoteBase key = flick
				? new FrictionFlickNote(Info(1f), 1, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Left, 0.0833f, 0f)
				: new FrictionNote(Info(1f), 1, 4, 5, NoteCategory.Friction, 0f, NoteType.Default, 1f);
			if (native)
			{
				key.SetNextRushScroll(new NextRushScrollTimeline(System.Array.Empty<NextRushScrollTimeline.Change>(), 0), 1.01);
				key.NativeEffectKind = NextRushEffectKind.Trace;
			}
			_harness.Arrange(new MusicScore(new[] { key.MusicScoreInfo }, System.Array.Empty<EventBase>(), new[] { key }));
			_harness.Logic.Notes.BindNativeVisuals(1);
			_harness.BindViews(_view);
			_harness.Logic.SetScoreLogic(new ScoreLogic(null));
			Step(0.8f, false);
			if (flick) Step(hit - 0.01f, false, Rest(hit - 0.01f, TouchPhase.Began));
			Step(hit, false, new LiveTouchSample(1, new Vector2(flick ? 560f : 800f, 600f), Position, INPUT_BASE + hit,
				flick ? TouchPhase.Moved : TouchPhase.Began, flick ? new Vector2(-240f, 0f) : Vector2.zero));
			Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect), "input settles this frame");
			Assert.That(_harness.Logic.Score.perfectCount, Is.EqualTo(1));
			Assert.That(_view.Judged.Count, Is.EqualTo(alreadyAtLine ? 1 : 0));
			Assert.That(_view.Removed.Contains(key), Is.EqualTo(alreadyAtLine));
			Assert.That(_view.InputFeedbackCount, Is.EqualTo(alreadyAtLine ? 0 : 1));
			if (!alreadyAtLine)
			{
				float crossing = (float)key.VisualTargetTime - lead;
				Step(crossing - 0.002f, false);
				Assert.That(_view.Judged, Is.Empty);
				Assert.That(LiveConfig.ViewProgressOf(key, (float)key.VisualTargetTime - 0.002f), Is.GreaterThan(0f).And.LessThan(1f));
				Assert.That(_harness.Logic.IsNotesAllFinished, Is.False);
				Step(crossing + 0.001f, false);
			}
			Step(1.3f, false);
			Assert.That(_view.Judged.Count, Is.EqualTo(1));
			Assert.That(_view.Removed.FindAll(n => n == key).Count, Is.EqualTo(1));
		}

		[Test]
		public void PracticeSeekDropsPendingTraceFeedback()
		{
			LiveConfig.HasVisualLatency = true;
			LiveConfig.ViewLeadSeconds = -0.1f;
			var key = new FrictionNote(Info(1f), 1, 4, 5, NoteCategory.Friction, 0f, NoteType.Default, 1f);
			_harness.Arrange(new MusicScore(new[] { key.MusicScoreInfo }, System.Array.Empty<EventBase>(), new NoteBase[] { key }));
			_harness.BindViews(_view);
			Step(0.8f, false);
			Step(0.95f, false, Rest(0.95f, TouchPhase.Began));
			Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
			_harness.Logic.SeekPractice(1.5f);
			Step(1.5f, false);
			Assert.That(_view.Judged, Is.Empty);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void SettledBarKeepsItsPinnedHeadUntilTheVisibleTail(bool native)
		{
			LiveConfig.HasVisualLatency = true;
			LiveConfig.ViewLeadSeconds = -0.1f;
			var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(2f), 2, 4, 5, NoteCategory.Friction, 0f, NoteType.Default, 1f);
			if (native)
			{
				var scroll = new NextRushScrollTimeline(System.Array.Empty<NextRushScrollTimeline.Change>(), 0);
				bar.SetNextRushScroll(scroll, 1);
				tail.SetNextRushScroll(scroll, 2.025);
			}
			bar.AddNoteListAndSetupChildNote(tail);
			_harness.Arrange(new MusicScore(new[] { bar.MusicScoreInfo }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }), auto: true);
			_harness.Logic.Notes.BindNativeVisuals(1);
			_harness.BindViews(_view);
			var key = _viewObject.AddComponent<LongNoteView>();
			key.Spawn(bar, 0);
			Step(0.8f, true);
			Step(1.2f, true);
			Step(2.01f, true);
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
			float before = (float)bar.VisualTailTime - 0.01f;
			key.Move(before);
			Assert.That(key.transform.localScale.x, Is.GreaterThan(0));
			Assert.That(_view.Removed, Has.No.Member(bar));
			Step((float)bar.VisualTailTime + 0.101f, true);
			key.Move((float)bar.VisualTailTime + 0.001f);
			Assert.That(key.transform.localScale.x, Is.Zero);
			Assert.That(_view.Removed, Has.Member(bar));
		}

		[Test]
		public void AnOrdinaryHeadKeepsItsKeyOnTheLine()
		{
			var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			Arrange(bar);
			for (float now = 0f; now < 1f - FRAME * 0.5f; now += FRAME)
			{
				Step(now, false);
			}

			Step(1f, false, Rest(1f, TouchPhase.Began));
			Step(1f + FRAME, false, Rest(1f + FRAME));
			Assert.That(bar.Head.IsSettled, Is.True);
			Assert.That(bar.HeadKeyLeftWithItsVerdict, Is.False, "an ordinary head parks on the line");
		}
	}
}
