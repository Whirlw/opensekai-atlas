using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    // Every note comes from ChartJson.Read -> Convert -> the real OnUpdate / OnInput loop.
    public sealed class LongNoteViewLifetimeTests
    {
        private LiveHarness _harness;
        private GameObject _viewObject;
        private TailLifetimeView _view;
        private LongNote _bar;
        private NoteBase Tail => (NoteBase)_bar.ChildNote;
        private static bool IsJudged(LongNote bar) => bar.ChildNote is NoteBase { HasJudgment: true };
        private float TailTime => Tail.MusicScoreInfo.time;
        private double _clock;

        [SetUp]
        public void SetUp()
        {
            _harness = new LiveHarness();
            _viewObject = new GameObject("tail lifetime observer");
            _view = _viewObject.AddComponent<TailLifetimeView>();
            _clock = 10d;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_viewObject);
            _harness.Dispose();
        }

        private void Arrange(string kind = "Normal", float displayLead = 1f)
        {
            var document = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
            var score = RuntimeChartConverter.Convert(document, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
            _bar = score.NoteArray.OfType<LongNote>().First(n => kind switch
            {
                "Guide" => n is GuideNote,
                // This fixture has no plain friction bar: the friction head comes with hidden markers,
                // which is the case the bug report describes.
                "Friction" => n is FrictionHideLongNote && IsJudged(n),
                "Hidden" => n is FrictionHideLongNote,
                "Swipe" => n.TailBehaviour.AcceptsSwipe && IsJudged(n),
                _ => n.ChildNote is NormalNote
            });
            if (kind == "Hidden") _bar = score.NoteArray.OfType<FrictionHideLongNote>().Last();
            _harness.Arrange(score, new NoteBase[] { _bar }, displayLead);
            _harness.BindViews(_view);
        }

        private void Step(float time, TouchPhase? phase = null, Vector2 delta = default)
        {
            _clock += 0.02d;
            if (phase.HasValue)
                _harness.Step(time, _clock, new LiveTouchSample(1, new Vector2(800f, 600f) + delta, LiveConfig.JudgmentPositions[1], _clock, phase.Value, delta));
            else _harness.Step(time, _clock);
        }

        private void StartHolding()
        {
            Step(_bar.MusicScoreInfo.time - 0.01f);
            Step(_bar.MusicScoreInfo.time, TouchPhase.Began);
            Assert.That(_bar.HasFinger, Is.True, "the real candidate/input path must acquire the hold");
        }

        // A key that can be graded must be on screen, whatever head the bar starts with. The old rule
        // looked at the head instead, which left slide and friction bars answering a key nobody saw.
        [TestCase("Guide", TestName = "GuideBarShowsTheKeyItEndsWith")]
        [TestCase("Friction", TestName = "FrictionBarShowsTheKeyItEndsWith")]
        public void JudgeableTailIsDrawnWhateverTheHead(string kind)
        {
            Arrange(kind, displayLead: 0.25f);
            Assert.That(Tail.HasJudgment, Is.True, "this bar ends in a note the player has to answer");
            Assert.That(_bar.ViewNoteList, Has.Member(Tail));
            Step(TailTime - 0.25f);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.25f), Is.EqualTo(TailKeyPhase.Following));
            Assert.That(_view.Spawns(Tail), Is.EqualTo(1), "a graded key that nobody can see is a trap");
        }

        [Test]
        public void BarEndingInAHiddenMarkerHasNoTailView()
        {
            Arrange("Hidden");
            Assert.That(Tail.HasJudgment, Is.False, "a hidden marker is not something the player answers");
            // The marker is still one of the bar's own notes, so it stays in the draw list and in the
            // geometry; it simply has nothing to show and nothing to grade.
            Assert.That(_bar.ViewNoteList, Has.Member(Tail));
            Assert.That(_bar.LineNoteList, Has.Member(Tail));
            Step(_bar.MusicScoreInfo.time);
            Step(TailTime - 0.01f);
            Step(TailTime);
            Step(TailTime + 2f);
            Assert.That(_bar.GetTailKeyPhase(TailTime), Is.EqualTo(TailKeyPhase.Hidden));
            Assert.That(_view.Spawns(Tail), Is.Zero);
            Assert.That(_view.Removals(Tail), Is.Zero);
        }

        [Test]
        public void BarLaneStopsAtItsTailEvenWhenALaterMarkerExists()
        {
            var headTime = new MusicScoreInfo(0, 0f, 0f, 120, 4, 1, 1);
            var tailTime = new MusicScoreInfo(0, 0.5f, 1f, 120, 4, 1, 1);
            var markerTime = new MusicScoreInfo(0, 1f, 2f, 120, 4, 1, 1);
            var bar = new LongNote(headTime, 1, 2, 3, NoteCategory.Long, NoteType.Default, 1f, 0f);
            var tail = new FrictionNote(tailTime, 2, 4, 6, NoteCategory.Friction, null, NoteType.Default, 1f);
            bar.AddNoteListAndSetupChildNote(tail);
            bar.AddConnectionNote(new FrictionHideNote(markerTime, 3, 8, 9,
                NoteCategory.Hidden, null, NoteType.Default, 1f, 0f));
            var afterTail = new MusicScoreInfo(0, 0.55f, 1.1f, 120, 4, 1, 1);

            bar.Excute(afterTail, 1f);
            Assert.That(bar.LaneStartF, Is.EqualTo(4f));
            Assert.That(bar.LaneEndF, Is.EqualTo(6f));

            tail.SetStateUnnotice(NoteState.Done);
            bar.Excute(afterTail, 1f);
            Assert.That(bar.LaneStartF, Is.EqualTo(4f));
            Assert.That(bar.LaneEndF, Is.EqualTo(6f));
        }

        [Test]
        public void FirstSegmentUsesTheHeadsChartCurveAfterHeadJudgment()
        {
            var headTime = new MusicScoreInfo(0, 0f, 0f, 120, 4, 1, 1);
            var tailTime = new MusicScoreInfo(0, 0.1f, 0.2f, 120, 4, 1, 1);
            var halfway = new MusicScoreInfo(0, 0.05f, 0.1f, 120, 4, 1, 1);
            var bar = new LongNote(headTime, 1, 2, 3, NoteCategory.Long, NoteType.Default, 1f, 0f,
                NoteLineType.EaseIn);
            bar.AddNoteListAndSetupChildNote(new NormalNote(tailTime, 2, 6, 7,
                NoteCategory.Normal, 1f, NoteType.Default, 0f));

            bar.Excute(halfway, 1f);
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.None));
            Assert.That(bar.LaneStartF, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(bar.LaneEndF, Is.EqualTo(4f).Within(1e-5f));

            bar.Head.Settle((NoteResult.JustPerfect, NoteResultDescription.None));
            bar.Excute(halfway, 1f);
            Assert.That(bar.LaneStartF, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(bar.LaneEndF, Is.EqualTo(4f).Within(1e-5f));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void IndependentFrictionKeysAndTailsHaveTheSameWindowAndLifetime(bool flick, bool hit)
        {
            var info = new MusicScoreInfo(0, 0.5f, 1f, 120, 4, 1, 1);
            var headTime = new MusicScoreInfo(0, 0f, 0f, 120, 4, 1, 1);
            NoteBase NewKey(int id, int lane) => flick
                ? new FrictionFlickNote(info, id, lane, lane, NoteCategory.FrictionFlick, 1f,
                    NoteType.Default, NoteDirection.Default, 0f, 0f)
                : new FrictionNote(info, id, lane, lane, NoteCategory.Friction, null, NoteType.Default, 1f);
            var bar = new LongNote(headTime, 1, 8, 8, NoteCategory.Long, NoteType.Critical, 2f, 0f);
            NoteBase tail = NewKey(2, 8);
            NoteBase single = NewKey(3, 2);
            single.speedRatio = 2f;
            bar.AddNoteListAndSetupChildNote(tail);
            Assert.That(tail.Category, Is.EqualTo(single.Category));
            Assert.That(tail.Type, Is.EqualTo(single.Type));
            Assert.That(tail.speedRatio, Is.EqualTo(bar.speedRatio));
            Assert.That(LiveUtility.GetINoteToJudgeFrameType(tail), Is.EqualTo(LiveUtility.GetINoteToJudgeFrameType(single)));

            _harness.Arrange(new MusicScore(new[] { headTime }, System.Array.Empty<EventBase>(), new[] { bar, single }));
            _harness.BindViews(_view);
            _harness.Step(0f, 10d);
            _harness.Step(0.75f, 10.75d);
            Assert.That(tail.Progress, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(tail.Progress, Is.EqualTo(single.Progress).Within(1e-5f));
            Assert.That(_view.Spawns(tail), Is.GreaterThan(0));
            Assert.That(_view.Spawns(tail), Is.EqualTo(_view.Spawns(single)));
            _harness.Step(1.01f, 11.01d);
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(_view.Spawns(tail), Is.EqualTo(_view.Spawns(single)));
            Assert.That(_view.Removals(tail), Is.Zero);
            Assert.That(_view.Removals(single), Is.Zero);
            Assert.That(tail.Progress, Is.EqualTo(single.Progress).Within(1e-5f));

            if (hit)
            {
                _harness.Step(1.10f, 11.10d,
                    LiveHarness.Touch(1, LiveConfig.JudgmentPositions[8], 11.10d),
                    LiveHarness.Touch(2, LiveConfig.JudgmentPositions[2], 11.10d));
                _harness.Step(1.12f, 11.12d,
                    new LiveTouchSample(1, new Vector2(800f, 700f), LiveConfig.JudgmentPositions[8], 11.12d, TouchPhase.Moved, Vector2.zero),
                    new LiveTouchSample(2, new Vector2(800f, 700f), LiveConfig.JudgmentPositions[2], 11.12d, TouchPhase.Moved, Vector2.zero));
            }
            _harness.Step(1.16f, 11.16d);
            _harness.Step(1.18f, 11.18d);
            Assert.That(tail.Result, Is.EqualTo(single.Result));
            if (hit)
                Assert.That(tail.Result, Is.GreaterThan(NoteResult.Miss));
            else
                Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(_view.Removals(tail), Is.EqualTo(1));
            Assert.That(_view.Removals(single), Is.EqualTo(1));
        }

        [Test]
        public void TailIsHiddenBeforeVisibleFrom()
        {
            Arrange(displayLead: 0.25f);
            Step(TailTime - 0.251f);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.251f), Is.EqualTo(TailKeyPhase.Hidden));
            Assert.That(_view.Spawns(Tail), Is.Zero);
        }

        [Test]
        public void TailStartsFollowingAtVisibleFrom()
        {
            Arrange(displayLead: 0.25f);
            Step(TailTime - 0.25f);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.25f), Is.EqualTo(TailKeyPhase.Following));
            Assert.That(_view.Spawns(Tail), Is.EqualTo(1));
        }

        [Test]
        public void OrdinaryTailFollowsUntilItsOwnTime()
        {
            Arrange();
            StartHolding();
            Step(TailTime - 0.001f, TouchPhase.Stationary);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.001f), Is.EqualTo(TailKeyPhase.Following));
            Assert.That(_view.Spawns(Tail), Is.EqualTo(1));
            Assert.That(_view.Removals(Tail), Is.Zero);
        }

        [Test]
        public void AnUnjudgedTailIsTakenOffWhenItMisses()
        {
            Arrange();
            StartHolding();
            Step(TailTime, TouchPhase.Stationary);
            Assert.That(_bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
            Assert.That(_view.Removals(Tail), Is.Zero);
            Step(TailTime + 0.2f);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
            Step(TailTime + 0.6f);
            Step(TailTime + 1f);
            Assert.That(_view.Removals(Tail), Is.EqualTo(1), "and it is recycled exactly once");
        }

        [Test]
        public void AReleaseTailStaysVisibleAndAcceptsALateLiftAfterTheBodyEnds()
        {
            Arrange();
            StartHolding();
            Step(TailTime - 0.01f);
            Assert.That(_view.Removals(Tail), Is.Zero, "the key is on screen just before its own time");

            Step(TailTime + 0.01f, TouchPhase.Stationary);
            Assert.That(_bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(_view.Removals(Tail), Is.Zero);
            Step(TailTime + 0.02f, TouchPhase.Ended);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }

        [Test]
        public void SwipeTailLeavesTheLineOnTimeButStillAcceptsASwipe()
        {
            Arrange("Swipe");
            StartHolding();
            Step(TailTime, TouchPhase.Stationary);
            Assert.That(_bar.GetTailKeyPhase(TailTime), Is.EqualTo(TailKeyPhase.SlidingOut));
            Assert.That(_bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(_view.Removals(Tail), Is.Zero);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.None), "view time is not the swipe window");
            Step(TailTime + 0.01f, TouchPhase.Stationary);
            Assert.That(Tail.Progress, Is.GreaterThan(1f), "the key must move off the judgment line while awaiting input");
            Step(TailTime + 0.02f, TouchPhase.Moved, new Vector2(0f, 200f));
            Assert.That(Tail.Result, Is.Not.EqualTo(NoteResult.None), "the real swipe candidate remains playable");
            Assert.That(_bar.GetTailKeyPhase(TailTime + 0.02f), Is.EqualTo(TailKeyPhase.Retired));
            Step(TailTime + 0.4f);
            Step(TailTime + 0.5f);
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }

        [Test]
        public void AnEarlySwipeKeepsItsCountdownAfterTheBodyEnds()
        {
            Arrange("Swipe");
            StartHolding();
            Step(TailTime - 0.03f, TouchPhase.Stationary);
            Step(TailTime - 0.02f, TouchPhase.Moved, new Vector2(0f, 200f));
            Assert.That(_bar.HasDeferredSwipe, Is.True);
            Step(TailTime, TouchPhase.Stationary, new Vector2(0f, 200f));
            Assert.That(_bar.State, Is.EqualTo(NoteState.Done));
            Step(TailTime + 0.01f);
            Step(TailTime + 0.02f);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
            Assert.That(_view.Removals(Tail), Is.Zero);
            Step(TailTime + 0.03f);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }

        [TestCase("Normal")]
        [TestCase("Swipe")]
        public void EndedTailNeverReturnsToFollowing(string kind)
        {
            Arrange(kind);
            StartHolding();
            Step(TailTime + 0.4f);
            Step(TailTime - 0.001f);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.001f), Is.EqualTo(TailKeyPhase.Retired));
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
            Assert.That(_view.Spawns(Tail), Is.EqualTo(1));
        }

        [Test]
        public void BrokenHoldDoesNotHideAFollowingTail()
        {
            Arrange("Swipe");
            StartHolding();
            Step(_bar.MusicScoreInfo.time + 0.05f, TouchPhase.Ended);
            Step(TailTime - 0.2f);
            Assert.That(_bar.GetTailKeyPhase(TailTime - 0.2f), Is.EqualTo(TailKeyPhase.Following));
            Assert.That(_view.Removals(Tail), Is.Zero);
        }

        [TestCase("Normal", false)]
        [TestCase("Normal", true)]
        [TestCase("Swipe", false)]
        [TestCase("Swipe", true)]
        public void ResultCallbacksArePublishedOnceRegardlessOfViewRetirement(string kind, bool hit)
        {
            Arrange(kind);
            var counts = new int[4];
            Tail.OnJudgment = _ =>
            {
                counts[0]++;
                Tail.FinishWith(Tail.Result); // Reentrant completion must not publish again.
            };
            Tail.OnUpdateCombo = _ => counts[1]++;
            Tail.OnUpdateScore = _ => counts[2]++;
            Tail.OnDamage = _ => counts[3]++;
            StartHolding();
            if (hit)
                Step(TailTime, _bar.TailBehaviour.AcceptsSwipe ? TouchPhase.Moved : TouchPhase.Ended, new Vector2(0f, 200f));
            else Step(TailTime + 0.25f);
            Assert.That(Tail.Result, Is.Not.EqualTo(NoteResult.None));
            Assert.That(counts, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Step(TailTime + 0.4f);
            Step(TailTime + 1f);
            Assert.That(counts, Is.EqualTo(new[] { 1, 1, 1, 1 }), "removing the view must never publish a result");
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }

        [Test]
        public void RetiringViewKeepsAdvancingAfterTheLastNoteFinishes()
        {
            Arrange();
            StartHolding();
            Step(TailTime, TouchPhase.Ended);
            Step(TailTime + 0.1f);
            Step(TailTime + 0.4f);
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }

        [Test]
        public void TheTailKeepsItsViewUntilTheMissIsPublished()
        {
            Arrange("Swipe", 0.1f);
            int judgments = 0;
            Tail.OnJudgment = _ => judgments++;
            StartHolding();
            Step(TailTime - 0.05f, TouchPhase.Stationary);
            Step(TailTime, TouchPhase.Stationary);
            Step(TailTime + 0.04f, TouchPhase.Stationary);
            Assert.That(_view.Removals(Tail), Is.Zero);
            Assert.That(Tail.Result, Is.EqualTo(NoteResult.None));
            Assert.That(judgments, Is.Zero);
            Step(TailTime + 0.25f);
            Assert.That(judgments, Is.EqualTo(1));
            Assert.That(_view.Removals(Tail), Is.EqualTo(1));
        }
    }

    public sealed class TailLifetimeView : LiveViewBase
    {
        private readonly Dictionary<NoteBase, int> _spawns = new Dictionary<NoteBase, int>();
        private readonly Dictionary<NoteBase, int> _removals = new Dictionary<NoteBase, int>();
        public int Spawns(NoteBase note) => _spawns.TryGetValue(note, out int count) ? count : 0;
        public int Removals(NoteBase note) => _removals.TryGetValue(note, out int count) ? count : 0;
        public override void SpawnNote(NoteBase note) => _spawns[note] = Spawns(note) + 1;
        public override void UnspawnNote(NoteBase note) => _removals[note] = Removals(note) + 1;
    }
}
