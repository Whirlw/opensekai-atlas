using System.Collections.Generic;
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
            bar.OnDamage = n => events.Add($"damage:{n.State}:{n.Result}");
            bar.OnUpdateCombo = n => events.Add("combo");
            bar.OnJudgment = n => events.Add("judge");
            bar.OnUpdateScore = n => events.Add("score");
            if (fromLast) bar.SetStateUnnotice(NoteState.Last);

            // The head settles on its own window, not on a lifecycle branch: give it a frame past the
            // window and the Miss comes straight from the head, whether or not the bar was left in Last.
            bar.Excute(Info(1f), 0);

            var state = fromLast ? "Release" : "Scrolling";
            Assert.That(events, Is.EqualTo(new[] { $"damage:{state}:Miss", "combo", "judge" }));
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
