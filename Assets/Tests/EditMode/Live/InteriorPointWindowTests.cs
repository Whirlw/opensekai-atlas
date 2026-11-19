using System.Collections.Generic;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class InteriorPointWindowTests
    {
        private MusicScoreInfo _previousFrame;
        private LiveConfig.NoteTypeJudgeData _previousConfig;
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        [SetUp]
        public void SetUp()
        {
            _previousFrame = MusicScore.CurrentFrameInfo;
            _previousConfig = LiveConfig.noteTypeJudgeData;
            MusicScore.CurrentFrameInfo = Info(0);
            LiveConfig.LoadMasterConfig();
        }

        [TearDown]
        public void TearDown()
        {
            MusicScore.CurrentFrameInfo = _previousFrame;
            LiveConfig.noteTypeJudgeData = _previousConfig;
        }


        private static void Refresh(LiveLogic logic) => logic.Scheduler.RefreshSustainedHolds();

        private static LongNote Bar()
        {
            var bar = new FrictionLongNote(Info(0), 1, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            return bar;
        }

        private static LiveLogic Drive(LongNote bar, float now, float touchTime)
        {
            var logic = new LiveLogic(null);
            logic.currentFrameInfo = MusicScore.CurrentFrameInfo = Info(now);
            var input = new LiveLogic.InputTmp
            {
                Lane = 4.5f,
                Touch = new LiveTouch { fingerId = 1, touchId = 1, musicTime = touchTime, phase = TouchPhase.Began }
            };
            // Exercise both the initial friction press and the sustained refresh entry.
            bar.Judgment(ref input.Touch, input.Lane);
            input.Touch.phase = TouchPhase.Stationary;
            logic.Input.JudgmentNotes.Add(bar);
            logic.Input.ActiveInputs.Add(input);
            Refresh(logic);
            return logic;
        }

        [Test]
        public void AnExpiredInteriorPointIsNotJudgedByALatePress()
        {
            var bar = Bar();
            var expired = new List<LongHoldCombo>();
            foreach (float time in new[] { 0.125f, 0.25f, 0.375f })
            {
                var point = new LongHoldCombo(Info(time), NoteType.Default, 1, 0);
                bar.AddHoldCombo(point);
                expired.Add(point);
            }
            var due = new LongHoldCombo(Info(1f), NoteType.Default, 1, 0);
            bar.AddHoldCombo(due);
            bar.SortNoteList();
            // A dropped frame may skip their entire contact window.
            bar.Excute(Info(1f), 0);
            int scored = 0;
            bar.OnJudgment = note => { if (note is LongHoldCombo) scored++; };

            var logic = Drive(bar, 1f, 1f);
            Refresh(logic);

            foreach (var point in expired)
            {
                Assert.That(point.Result, Is.EqualTo(NoteResult.Miss));
                Assert.That(point.State, Is.EqualTo(NoteState.Done));
            }
            Assert.That(due.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(scored, Is.EqualTo(1), "three expired points must not join the one live point, even on a repeated refresh");
        }

        [TestCase(NoteState.InputBegan)]
        [TestCase(NoteState.Input)]
        [TestCase(NoteState.Last)]
        public void ANearlyHeldOrGraceStateCountsWithoutAnotherTouch(NoteState state)
        {
            var bar = Bar();
            var point = new LongHoldCombo(Info(1f), NoteType.Default, 1f, 0f);
            bar.AddHoldCombo(point);

            bar.SetStateUnnotice(state);
            point.Excute(Info(0.95f), 0f);
            Assert.That(point.Result, Is.EqualTo(NoteResult.None));

            bar.SetStateUnnotice(NoteState.Release);
            point.Excute(Info(1f), 0f);
            Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AReleasedBarNeedsActualFingerCoverage(bool covered)
        {
            var bar = Bar();
            var point = new LongHoldCombo(Info(1f), NoteType.Default, 1f, 0f);
            bar.AddHoldCombo(point);
            bar.SetStateUnnotice(NoteState.Release);
            if (covered) bar.RecordHoldingInput(1f);

            point.Excute(Info(1f), 0f);
            point.Excute(Info(1.084f), 0f);

            Assert.That(point.Result, Is.EqualTo(covered ? NoteResult.JustPerfect : NoteResult.Miss));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EndingAHoldUsesTailInputForPendingComboPoints(bool hiddenHead, bool tailHit)
        {
            LongNote bar = hiddenHead
                ? new FrictionHideLongNote(Info(0), 1, 4, 5, NoteCategory.FrictionHideLong, NoteType.Default, null, 1)
                : new LongNote(Info(0), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            var expired = new LongHoldCombo(Info(1f), NoteType.Default, 1, 0);
            var due = new LongHoldCombo(Info(1.05f), NoteType.Default, 1, 0);
            var upcoming = new LongHoldCombo(Info(1.13f), NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(1.2f), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            var judged = new List<NoteBase>();
            bar.SetStateUnnotice(NoteState.Release);
            MusicScore.CurrentFrameInfo = Info(1.084f);
            foreach (var point in new[] { expired, due, upcoming })
            {
                bar.AddHoldCombo(point);
                point.OnJudgment = judged.Add;
                point.Excute(MusicScore.CurrentFrameInfo, 0);
            }

            Assert.That(expired.Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(due.Result, Is.EqualTo(NoteResult.None));
            Assert.That(upcoming.Result, Is.EqualTo(NoteResult.None));

            Assert.That(tailHit ? bar.JudgeTailAsRelease(1.084f) : bar.FailTail(), Is.True);
            bar.CompleteTail();

            var expected = tailHit ? NoteResult.Perfect : NoteResult.Miss;
            Assert.That(expired.Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(due.Result, Is.EqualTo(expected));
            Assert.That(upcoming.Result, Is.EqualTo(expected));
            Assert.That(judged, Is.EqualTo(new[] { expired, due, upcoming }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AComboPointCanWaitForALiftAfterTheBodyEnds(bool lift)
        {
            var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            var point = new LongHoldCombo(Info(1.28f), NoteType.Default, 1, 0);
            var tail = new NormalNote(Info(1.3f), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            bar.AddHoldCombo(point);
            bar.AddNoteListAndSetupChildNote(tail);
            using var harness = new LiveHarness();
            harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }));
            harness.Step(1f, 11f);
            harness.Step(1.15f, 11.15);
            harness.Step(1.3f, 11.3);

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(point.Result, Is.EqualTo(NoteResult.None));
            Assert.That(tail.Result, Is.EqualTo(NoteResult.None));

            if (lift)
                harness.Step(1.335f, 11.335, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[4], 11.335, TouchPhase.Ended));
            else
                harness.Step(1.364f, 11.364);

            Assert.That(point.Result, Is.EqualTo(lift ? NoteResult.Perfect : NoteResult.Miss));
            Assert.That(tail.Result, Is.EqualTo(lift ? NoteResult.JustPerfect : NoteResult.None));
        }

        [Test]
        public void ARegisteredFlickTailContactCanSaveAComboBeforeTheFlickGrades()
        {
            var bar = new LongNote(Info(0), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            var point = new LongHoldCombo(Info(1f), NoteType.Default, 1, 0);
            var tail = new FlickNote(Info(1.1f), 2, 7, 8, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 1);
            bar.AddHoldCombo(point);
            bar.AddNoteListAndSetupChildNote(tail);
            var touch = new LiveTouch { touchId = 1, musicTime = 0.99f, phase = TouchPhase.Stationary };
            tail.TailJudge.Offer(ref touch, 7.5f, 7.5f, 1f / 60f);

            bar.AdvanceTailLinkedComboPoints(Info(0.99f));
            Assert.That(point.Result, Is.EqualTo(NoteResult.None));

            bar.AdvanceTailLinkedComboPoints(Info(1f));
            Assert.That(point.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(tail.Result, Is.EqualTo(NoteResult.None));

            tail.ResetNote();
            Assert.That(tail.HasSlideEndInputAt(1f), Is.False);
        }

        [Test]
        public void AHeadPressedAfterItsFirstPointCanStillSaveThePoint()
        {
            var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
            var point = new LongHoldCombo(Info(1.019f), NoteType.Default, 3, 1f, 0f);
            bar.AddHoldCombo(point);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            bar.SortNoteList();
            using var harness = new LiveHarness();
            harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }));
            harness.Step(1.021f, 11.021);
            harness.Step(1.027f, 11.027);
            Assert.That(point.Result, Is.EqualTo(NoteResult.None));

            harness.Step(1.039f, 11.039, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[4], 11.032));

            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        [TestCase(-0.084f, false)]
        [TestCase(-0.082f, true)]
        [TestCase(0.082f, true)]
        [TestCase(0.084f, false)]
        public void FrictionAndStandaloneTickUseTheSameContactWindow(float offset, bool expected)
        {
            var friction = new FrictionNote(Info(1f), 1, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
            var tick = new ConnectionNote(Info(1f), 2, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);
            var touch = new LiveTouch { phase = TouchPhase.Stationary };

            foreach (var key in new ConnectionNote[] { friction, tick })
            {
                key.Excute(Info(1f + offset), 0.5f);
                Assert.That(key.IsJudgment(ref touch, 4.5f), Is.EqualTo(expected));
                if (expected) key.Judgment(ref touch, 4.5f);
                if (offset < 0f) Assert.That(key.Result, Is.EqualTo(NoteResult.None));
                key.Excute(Info(1.1f), 0.5f);
                Assert.That(key.Result, Is.EqualTo(expected ? NoteResult.JustPerfect : NoteResult.Miss));
            }
        }

        [TestCase(-0.109f, false)]
        [TestCase(-0.108f, true)]
        [TestCase(0.133f, true)]
        [TestCase(0.134f, false)]
        public void BoundTraceTailHasTheWiderContactWindow(float offset, bool accepts)
        {
            var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
            new LongNote().AddNoteListAndSetupChildNote(tail);
            var touch = new LiveTouch { phase = TouchPhase.Stationary };
            tail.Excute(Info(1f + offset), 0.5f);
            Assert.That(tail.IsJudgment(ref touch, 4.5f), Is.EqualTo(accepts));
        }

        [Test]
        public void CompletedTailSettlesOnlyUnansweredVisibleTicks()
        {
            var bar = Bar();
            var answered = new ConnectionNote(Info(1f), 2, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);
            var pending = new ConnectionNote(Info(2f), 3, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);
            bar.AddConnectionNote(answered);
            bar.AddConnectionNote(pending);
            answered.Excute(Info(1f), 0.5f);
            answered.Excute(Info(1.01f), 0.5f);
            bar.SetStateUnnotice(NoteState.Release);
            Assert.That(bar.FailTail(), Is.True);
            bar.AdvanceTailLinkedComboPoints(Info(1.5f));
            Assert.That(pending.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(answered.Result, Is.EqualTo(NoteResult.Miss));
        }

        [TestCase(0.004f)]
        [TestCase(0.033f)]
        public void ConnectedVisibleTickOnlyAcceptsTheCrossingFrame(float frameDuration)
        {
            var bar = Bar();
            var point = new ConnectionNote(Info(1f), 2, 4, 5, NoteCategory.Connection, NoteType.Default, 1f, 0f);
            bar.AddConnectionNote(point);
            var touch = new LiveTouch { phase = TouchPhase.Stationary };
            point.Excute(Info(0.99f), 0.5f);
            Assert.That(point.IsJudgment(ref touch, 4.5f), Is.False);
            point.Excute(Info(1.002f), 0.5f);
            Assert.That(point.IsJudgment(ref touch, 4.5f), Is.True);
            point.Excute(Info(1.002f + frameDuration), 0.5f);
            Assert.That(point.Result, Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void ATouchStampedEarlyStillJudgesAPointThatHasArrived()
        {
            // Zero is exactly on the 125 ms early boundary; -1 also proves a touch-clock gate fails.
            foreach (float stamp in new[] { 0f, -1f })
            {
                var bar = Bar();
                // Start the hold before advancing to the point; friction heads settle on Excute.
                var began = new LiveTouch { fingerId = 1, touchId = 1, musicTime = 0, phase = TouchPhase.Began };
                bar.Judgment(ref began, 4.5f);
                var point = new LongHoldCombo(Info(0.125f), NoteType.Default, 1, 0);
                bar.AddHoldCombo(point);
                bar.SortNoteList();
                int scored = 0;
                bar.OnJudgment = note => { if (ReferenceEquals(note, point)) scored++; };
                bar.Excute(Info(0.125f), 0);

                var logic = Drive(bar, 0.125f, stamp);
                Refresh(logic);

                Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect), $"touch stamp {stamp} must not replace the frame clock");
                Assert.That(point.State, Is.EqualTo(NoteState.Done));
                Assert.That(scored, Is.EqualTo(1));
            }
        }
    }
}
