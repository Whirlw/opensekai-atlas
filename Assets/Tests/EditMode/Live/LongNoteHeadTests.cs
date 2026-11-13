using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Live;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class LongNoteHeadTests
    {
        private LiveConfig.NoteTypeJudgeData _previous;
        private MusicScoreInfo _previousFrame;
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);
        private static LiveTouch Touch(float time = 0f) => new LiveTouch { fingerId = 1, touchId = 1, musicTime = time, phase = TouchPhase.Began };

        private static LongNote Bar()
        {
            var bar = new LongNote(Info(0), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            bar.SetStateUnnotice(NoteState.Scrolling);
            return bar;
        }

        [SetUp]
        public void SetUp() { _previous = LiveConfig.noteTypeJudgeData; _previousFrame = MusicScore.CurrentFrameInfo; MusicScore.CurrentFrameInfo = Info(0); LiveConfig.LoadMasterConfig(); }

        [TearDown]
        public void TearDown() { LiveConfig.noteTypeJudgeData = _previous; MusicScore.CurrentFrameInfo = _previousFrame; }

        [Test]
        public void AFingerHeldInTheLaneNeverJudgesTheHead()
        {
            // The report: holding the lane graded an ordinary bar's head (Miss, -80 life) up to a third of
            // a second BEFORE it reached the line, because the resident pass hands over a held touch whose
            // timestamp is when the finger first landed. A hold is not a press: the head is judged by a
            // press through the press channel, and otherwise keeps its own clock.
            var bar = Bar();
            var held = Touch(-0.25f);
            held.phase = TouchPhase.Stationary;

            Assert.That(bar.Head.Judgment(ref held, 4.5f), Is.False, "a hold does not decide the head");
            Assert.That(bar.Head.IsSettled, Is.False, "and nothing was settled: no phantom Miss");
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "the bar keeps no verdict either");

            var heldAtTheHead = Touch(-0.01f);
            heldAtTheHead.phase = TouchPhase.Stationary;
            Assert.That(bar.Head.Judgment(ref heldAtTheHead, 4.5f), Is.False,
                "not even at the head's own moment: a hold is still not a press");
            Assert.That(bar.Head.IsSettled, Is.False);
        }

        [Test]
        public void APressOutsideTheWindowDoesNotSettleTheHead()
        {
            // Outside the head's own Bad window the ladder can only answer Miss; grading there is the
            // damage the player sees in empty space, before the head arrives.
            var bar = Bar();
            var press = Touch(-0.25f);

            Assert.That(bar.Head.Judgment(ref press, 4.5f), Is.False);
            Assert.That(bar.Head.IsSettled, Is.False);
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
        }

        [Test]
        public void HeadResultAndCallbacksAreVisibleInThePressFrameOnlyOnce()
        {
            var bar = Bar();
            var events = new List<string>();
            bar.OffsetJudgeTime = 0.1f;
            bar.OnUpdateResult = result => { events.Add("result"); return (NoteResult.Great, result.description); };
            bar.OnJudgment = n => events.Add($"judge:{n.State}:{n.Result}:{n.OffsetJudgeTime:F1}");
            bar.OnUpdateCombo = n => events.Add("combo");
            bar.OnUpdateScore = n => events.Add("score");
            bar.OnDamage = n => events.Add("damage");
            var touch = Touch();

            Assert.That(bar.Judgment(ref touch, 4.5f), Is.True);

            Assert.That(events, Is.EqualTo(new[] { "result", "damage", "combo", "judge:Scrolling:Great:0.1", "score" }));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.Great));
            LongNoteGraceTests.AssertHeldBody(bar);
            Assert.That(bar.HasFinger, Is.True);
            Assert.That(bar.OffsetJudgeTime, Is.Zero);
            bar.Judgment(ref touch, 4.5f);
            Assert.That(events.Count, Is.EqualTo(5), "continuing the hold must not publish the head again");
        }

        [Test]
        public void ResetAllowsTheSameHeadToSettleAndBeHitAgain()
        {
            var bar = Bar();
            var tail = bar.ChildNote;
            var touch = Touch();
            bar.Judgment(ref touch, 4.5f);
            bar.Excute(Info(1), 0); // Closes the head window and sets its private settlement flag.
            var head = bar.Head;

            bar.ResetNote();

            Assert.That(bar.Head, Is.SameAs(head));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
            Assert.That(bar.HasFinger, Is.False);
            Assert.That(bar.ChildNote, Is.SameAs(tail), "reset after chart binding must preserve the tail");
            bar.Excute(Info(1), 0);
            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "the second use must still close an untouched head window");

            bar.ResetNote();
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "the previous Miss must not leak into another use");
            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.Judgment(ref touch, 4.5f);
            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect));
            LongNoteGraceTests.AssertHeldBody(bar);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HeadMissKeepsItsNotificationOrder(bool fromLast)
        {
            var bar = Bar();
            var events = new List<string>();
            bar.OnUpdateResult = result => { events.Add("result"); return result; };
            bar.OnDamage = n => events.Add($"damage:{(ReferenceEquals(n, bar) ? "bar" : "head")}:{n.Result}");
            bar.OnUpdateCombo = n => events.Add("combo");
            bar.OnJudgment = n => events.Add("judge");
            bar.OnUpdateScore = n => events.Add("score");
            if (fromLast) bar.SetStateUnnotice(NoteState.Last);

            // The head settles on its own window, not on a lifecycle branch: give it a frame past the
            // window and the Miss comes straight from the head, whether or not the bar was left in Last.
            bar.Excute(Info(1f), 0);

            Assert.That(events, Is.EqualTo(new[] { "damage:head:Miss", "combo", "judge" }));
            bar.Excute(Info(1.1f), 0);
            Assert.That(events.Count, Is.EqualTo(3));
        }

        [Test]
        public void SustainedRefreshKeepsOffsetAndInteriorParentWithoutRepublishingHead()
        {
            var bar = Bar();
            var combo = new LongHoldCombo(Info(1), NoteType.Default, 1, 0);
            bar.AddHoldCombo(combo);
            bar.SortNoteList();
            var boundRole = combo.ParentRole;
            var touch = Touch();
            bar.Judgment(ref touch, 4.5f);
            combo.SetStateUnnotice(NoteState.Last);
            bar.OffsetJudgeTime = 0.123f;
            int heads = 0;
            int points = 0;
            bar.OnJudgment = n => { if (ReferenceEquals(n, bar)) heads++; else if (ReferenceEquals(n, combo)) points++; };

            MusicScore.CurrentFrameInfo = Info(1f);
            bar.RefreshHeldInput(ref touch, 4.5f);
            bar.RefreshHeldInput(ref touch, 4.5f);

            Assert.That(bar.OffsetJudgeTime, Is.EqualTo(0.123f));
            Assert.That(combo.ParentNote, Is.SameAs(bar));
            Assert.That(combo.ParentRole, Is.EqualTo(boundRole));
            Assert.That(combo.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(points, Is.EqualTo(1));
            Assert.That(heads, Is.Zero);
        }

        [Test]
        public void TheHeadEntryAnswersThePress()
        {
            // The head is the door a press goes through; the bar's Judgment only forwards to it. This
            // pins the entry so the candidate list can be pointed at the head without moving the work.
            var bar = Bar();
            var touch = Touch();

            Assert.That(bar.Head.Judgment(ref touch, 4.5f), Is.True, "the head's own entry takes the press");
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect), "the picture mirror follows");
            Assert.That(bar.HasFinger, Is.True, "and the bar is held by that press, exactly as before");

            bar.Excute(Info(0.5f), 0);
            bar.Head.Judgment(ref touch, 4.5f);
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect), "a second frame does not re-settle it");
        }

        [Test]
        public void TheHeadScoresThroughItsOwnIdentity()
        {
            // The head's verdict is the head's. Score, combo and damage answer about the head's own
            // result object, not about the bar's JudgeInfo; the bar keeps the same number only because
            // the judgement effect and the views read the bar, and that mirror is the last shared edge.
            var bar = Bar();
            var scored = new List<NoteBase>();
            var visual = new List<NoteBase>();
            bar.OnDamage = n => scored.Add(n);
            bar.OnUpdateCombo = n => scored.Add(n);
            bar.OnUpdateScore = n => scored.Add(n);
            bar.OnJudgment = n => visual.Add(n);
            var touch = Touch();

            Assert.That(bar.Judgment(ref touch, 4.5f), Is.True);

            Assert.That(scored, Has.Count.EqualTo(3), "damage, combo and score each fire once");
            Assert.That(scored, Has.None.SameAs(bar), "and none of them answers about the bar");
            Assert.That(scored.Select(n => n.Result), Is.All.EqualTo(NoteResult.JustPerfect));
            Assert.That(scored, Has.All.SameAs(bar.Head.Identity), "all three read the head's own result");
            Assert.That(visual, Is.EqualTo(new[] { bar }), "the judgement effect still plays on the bar");
            Assert.That(bar.Head.Identity.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect), "the bar's mirror is unchanged");
        }

        [Test]
        public void TheHeadIsItsOwnPressCandidate()
        {
            // The judgement list holds the head's candidate, not the bar: a press that reaches this
            // object is answered by the head's entry, and the bar's Judgement is never on that path.
            var bar = Bar();
            var touch = Touch();
            NoteBase candidate = bar.Head.Candidate;

            Assert.That(candidate, Is.Not.SameAs(bar));
            Assert.That(Sekai.Core.Live.LiveCandidatePipeline.IsPressStep(candidate), Is.True);
            Assert.That(candidate.IsJudgment(ref touch, 4.5f), Is.True);
            Assert.That(candidate.Judgment(ref touch, 4.5f), Is.True);

            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(bar.HasFinger, Is.True, "the same frame still takes the hold");
            Assert.That(candidate.IsJudgment(ref touch, 4.5f), Is.False, "a settled head leaves the candidate list");
        }

        [Test]
        public void OnlyAnOrdinaryHeadBorrowsTheBarsVisualIdentity()
        {
            // The "judgement effect plays on the bar" bridge is for the ordinary head only. Every other
            // head is expected to be visually unrelated to the bar, so it hands its own object to the
            // judgement callback instead.
            var ordinary = Bar();
            var ordinaryVisual = new List<NoteBase>();
            ordinary.OnJudgment = n => ordinaryVisual.Add(n);
            var touch = Touch();
            ordinary.Judgment(ref touch, 4.5f);
            Assert.That(ordinaryVisual, Is.EqualTo(new[] { ordinary }), "an ordinary head still plays on the bar");

            var rub = new FrictionLongNote(Info(0), 3, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1);
            rub.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 4, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            var rubVisual = new List<NoteBase>();
            rub.OnJudgment = n => rubVisual.Add(n);
            rub.Excute(Info(0), 0);
            rub.Head.Settle((NoteResult.JustPerfect, NoteResultDescription.None));

            Assert.That(rubVisual.Count, Is.EqualTo(1));
            Assert.That(rubVisual[0], Is.SameAs(rub.Head.Identity), "a rub head speaks for itself");
            Assert.That(rubVisual[0], Is.Not.SameAs(rub));
        }

        private sealed class TailProbe : NoteBase
        {
            internal readonly List<string> Calls = new List<string>();
            internal bool Accept = true;
            public override bool IsJudgment(ref LiveTouch touch, float lane) { Calls.Add("eligible"); return Accept; }
            public override bool Judgment(ref LiveTouch touch, float lane) { Calls.Add("judge"); return true; }
        }

        [Test]
        public void TailEntryRetainsTheTimeGateAndShortCircuitOrder()
        {
            var bar = Bar();
            var tail = new TailProbe { MusicScoreInfo = Info(10) };
            bar.SetChildNote(tail);
            var touch = Touch();
            bar.Judgment(ref touch, 4.5f);
            Assert.That(tail.Calls, Is.Empty, "an unexecuted tail's cached zero offset must not open its window");
            touch.musicTime = 10;
            tail.Accept = false;
            bar.Judgment(ref touch, 4.5f);
            Assert.That(tail.Calls, Is.EqualTo(new[] { "eligible" }));
            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
            tail.Accept = true;
            bar.Judgment(ref touch, 4.5f);
            Assert.That(tail.Calls, Is.EqualTo(new[] { "eligible", "eligible", "judge" }));
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(bar.Judgment(ref touch, 4.5f), Is.False);
            Assert.That(tail.Calls.Count, Is.EqualTo(3));
        }
    }
}
