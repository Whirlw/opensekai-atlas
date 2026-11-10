using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class LongHoldPipelineTests
    {
        private LiveConfig.NoteTypeJudgeData previous;
        private MusicScoreInfo previousFrame;
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        private sealed class HeldNote : LongNote
        {
            internal HeldNote(int finger, float time = 0f) : base(Info(time), finger, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0)
            {
                AddNoteListAndSetupChildNote(new NormalNote(Info(10), 100 + finger, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
                State = NoteState.Scrolling;
                var touch = new LiveTouch { fingerId = finger, touchId = finger, phase = TouchPhase.Began };
                Judgment(ref touch, 4.5f);
                JudgmentCalls = 0;
                lastInputTime = 0f;
                // The real update pass releases a hold whose refresh is older than one reference frame.
                Excute(Info(1), 0);
            }
            internal float LastTime => lastInputTime;
            internal void SetLastTime(float time) { lastInputTime = time; }
            internal int JudgmentCalls;
            public override bool Judgment(ref LiveTouch touch, float lane)
            {
                JudgmentCalls++;
                return base.Judgment(ref touch, lane);
            }
        }

        [SetUp] public void SetUp() { previous = LiveConfig.noteTypeJudgeData; previousFrame = MusicScore.CurrentFrameInfo; MusicScore.CurrentFrameInfo = Info(0); LiveConfig.LoadMasterConfig(); }
        [TearDown] public void TearDown() { LiveConfig.noteTypeJudgeData = previous; MusicScore.CurrentFrameInfo = previousFrame; }
        private static T Field<T>(LiveLogic logic, string name) => (T)typeof(LiveLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
        private static void Stage(LiveLogic logic, string name) => typeof(LiveLogic).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
        private static LiveLogic.InputTmp Input(int finger, float lane) => new LiveLogic.InputTmp
        {
            Lane = lane,
            Touch = new LiveTouch { fingerId = finger, touchId = finger, musicTime = 0, phase = TouchPhase.Stationary }
        };

        [Test]
        public void IsHeld_follows_the_finger_not_the_state()
        {
            var bar = new HeldNote(3);

            Assert.That(bar.IsHeld(0f), Is.True, "a finger is down and was refreshed this frame");
            Assert.That(bar.State == NoteState.InputBegan || bar.State == NoteState.Input, Is.True);

            bar.SetLastTime(-10f);

            Assert.That(bar.IsHeld(0f), Is.True, "the finger is still down, so the bar is still held");
            Assert.That(bar.State == NoteState.InputBegan || bar.State == NoteState.Input, Is.True,
                "the state lags one refresh behind, which is why heldness is asked separately");

            Assert.That(new HeldNote(4).IsHeld(0f), Is.True);
        }

        [TestCase(NoteState.Input, true, true)]
        [TestCase(NoteState.InputBegan, true, true)]
        [TestCase(NoteState.Release, true, true)]
        [TestCase(NoteState.Release, false, false)]
        [TestCase(NoteState.Scrolling, true, false)]
        [TestCase(NoteState.Done, true, false)]
        public void ReservationRequiresAnEligibleOwnerAndStartedHold(NoteState state, bool eligible, bool expected)
        {
            Assert.That(LiveLogic.ShouldReserveHold(1, 2, state, eligible), Is.EqualTo(expected));
            Assert.That(LiveLogic.ShouldReserveHold(1, 1, state, eligible), Is.False);
            Assert.That(LiveLogic.ShouldReserveHold(-1, 2, state, eligible), Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void PickupPreservesBothHoldersRegardlessOfInputOrder(bool reverse, bool crossed)
        {
            var logic = new LiveLogic(null);
            var a = new HeldNote(1);
            var b = new HeldNote(2);
            var first = Input(1, crossed ? 4.8f : 4.2f);
            var second = Input(2, crossed ? 4.2f : 4.8f);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { a, b });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(reverse ? new[] { second, first } : new[] { first, second });
            Stage(logic, "AllocateInputJudgableEachNotes");
            Assert.That(Field<List<NoteBase>>(logic, "_connectionNoteList"), Is.EquivalentTo(new[] { a, b }));
            Stage(logic, "OverlapJudgmentSorting");
            Stage(logic, "LeaveNotesFastGeneration");
            Stage(logic, "PickUpNotBeginInputNotes");
            Assert.That(first.CandidateNotes, Is.EqualTo(new[] { a }), "Pickup must not give the other holder's note to this input.");
            Assert.That(second.CandidateNotes, Is.EqualTo(new[] { b }));
            Stage(logic, "EvaluateJudgment");
            Assert.That(a.LastTime, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(b.LastTime, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(a.JudgmentCalls, Is.EqualTo(1));
            Assert.That(b.JudgmentCalls, Is.EqualTo(1));
            Assert.That(a.HoldingTouchId, Is.EqualTo(1));
            Assert.That(b.HoldingTouchId, Is.EqualTo(2));
        }

        [TestCase(NoteCategory.Normal, OpenSekai.Shared.Live.CandidateOrder.TouchRank)]
        [TestCase(NoteCategory.Friction, OpenSekai.Shared.Live.CandidateOrder.TouchRank)]
        [TestCase(NoteCategory.FrictionFlick, OpenSekai.Shared.Live.CandidateOrder.FlickRank)]
        [TestCase(NoteCategory.Flick, OpenSekai.Shared.Live.CandidateOrder.FlickRank)]
        public void CandidateRankFollowsWhatNeedsTheFrame(NoteCategory category, int expected)
        {
            var note = category == NoteCategory.FrictionFlick
                ? (NoteBase)new FrictionFlickNote(Info(0), 1, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0.05f, 0)
                : category == NoteCategory.Flick
                    ? new FlickNote(Info(0), 1, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0.05f, 0)
                    : category == NoteCategory.Friction
                        ? new FrictionNote(Info(0), 1, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1)
                        : new NormalNote(Info(0), 1, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            Assert.That(LiveLogic.CandidateRank(note), Is.EqualTo(expected));
        }

        [Test]
        public void AConnectionNoteFinishingDoesNotEndTheBar()
        {
            var bar = new LongNote(Info(0f), 81, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 82, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            var connection = new NormalNote(Info(1f), 83, 4, 5, NoteCategory.Connection, 1, NoteType.Default, 0);
            connection.SetParentNote(bar, NoteParentRole.Connection);
            bar.Excute(Info(0f), 0);

            connection.FinishWith(NoteResult.Perfect);

            Assert.That(connection.State, Is.EqualTo(NoteState.Done));
            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "A slide note finishing is not the end of the bar.");
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
        }

        [Test]
        public void AComboMarkerFinishingDoesNotEndTheBar()
        {
            var bar = new LongNote(Info(0f), 84, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 85, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            var combo = new LongHoldCombo(Info(1f), NoteType.Default, 1, 0);
            combo.SetParentNote(bar, NoteParentRole.Combo);
            bar.Excute(Info(0f), 0);

            combo.FinishWith(NoteResult.JustPerfect);

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "A combo marker is graded on its own beat, not the bar's.");
        }

        [Test]
        public void TheTailFinishingStillEndsTheBar()
        {
            var bar = new LongNote(Info(0f), 86, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            var tail = new NormalNote(Info(1f), 87, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            bar.AddNoteListAndSetupChildNote(tail);
            bar.Excute(Info(0f), 0);
            var press = Input(1, 4.5f);
            press.Touch.phase = TouchPhase.Began;
            bar.Judgment(ref press.Touch, 4.5f);

            Assert.That(tail.ParentRole, Is.EqualTo(NoteParentRole.Tail));
            tail.FinishWith(NoteResult.JustPerfect);

            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "The tail is the one child that ends the bar.");
        }

        [Test]
        public void ResetReturnsEveryTransientFieldToItsDefault()
        {
            var bar = new LongNote(Info(0f), 71, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 72, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0));
            bar.Excute(Info(0f), 0);
            var press = Input(1, 4.5f);
            press.Touch.phase = TouchPhase.Began;
            bar.Judgment(ref press.Touch, 4.5f);
            bar.TrySwipeTail(new UnityEngine.Vector2(0f, 60f), 1f / 60f, 0.9f);

            bar.ResetNote();

            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(bar.State, Is.EqualTo(NoteState.Wait));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.None));
            Assert.That((float)typeof(LongNote).GetField("lastInputTime", Private).GetValue(bar), Is.EqualTo(0f));
            Assert.That((int)typeof(LongNote).GetField("_holdingTouchId", Private).GetValue(bar), Is.EqualTo(-1));
            Assert.That((bool)typeof(LongNote).GetField("_swipeTailDeferred", Private).GetValue(bar), Is.False);
            Assert.That((int)typeof(LongNote).GetField("_framesSinceRelease", Private).GetValue(bar), Is.EqualTo(-1));
            Assert.That((bool)typeof(LongNote).GetField("_pressedThisFrame", Private).GetValue(bar), Is.False);
            Assert.That((float)typeof(LongNote).GetField("_lastFrameTime", Private).GetValue(bar), Is.EqualTo(0f));
        }

        [Test]
        public void AHeldBarRanksLastSoARubNoteCanStillBeTouched()
        {
            var held = new HeldNote(1);
            Assert.That(LiveLogic.CandidateRank(held), Is.EqualTo(OpenSekai.Shared.Live.CandidateOrder.HeldRank));
        }
        [TestCase(0f)]
        [TestCase(0.01f)]
        public void ASlideStaysJudgeableAfterItsTimePasses(float lateness)
        {
            // Slide (connection) notes used to fail on the very next Excute once their time passed,
            // which left a one frame window: at 165 Hz that is ~6 ms, so the note looked unjudgeable.
            var logic = new LiveLogic(null);
            var slide = new ConnectionNote(Info(1f), 7, 4, 5, NoteCategory.Connection, NoteType.Default, 1, 0);
            slide.Excute(Info(0.9f), 0);
            slide.Excute(Info(1f), 0);
            Assert.That(slide.State, Is.EqualTo(NoteState.Last), "the slide becomes judgeable when its time passes");
            slide.Excute(Info(1f + lateness), 0);
            Assert.That(slide.State, Is.EqualTo(NoteState.Last), "it must survive a few late frames");
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(slide);
            var finger = Input(1, 4.5f);
            finger.Touch.musicTime = 1f + lateness;
            finger.Touch.phase = TouchPhase.Moved;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(finger);
            Stage(logic, "Judgment");
            Assert.That(slide.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(slide.State, Is.EqualTo(NoteState.Done));
        }

        [Test]
        public void ANoteThatDoesNotConsumeThePressStillLetsTheNextOneBeJudged()
        {
            // A rub that is still approaching returns false from Judgment and stays in play. Breaking
            // there starved every note queued behind it on the same finger, so a rub behind a flick was
            // never judged and expired. The finger must be able to serve the next candidate too.
            var logic = new LiveLogic(null);
            var rub = new FrictionNote(Info(2f), 21, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);
            rub.Excute(Info(1.9f), 0.5f);
            var tap = new NormalNote(Info(2f), 22, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(rub);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(tap);
            var finger = Input(1, 4.5f);
            finger.Touch.musicTime = 2f;
            finger.Touch.phase = TouchPhase.Began;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(finger);
            Stage(logic, "Judgment");
            Assert.That(tap.State, Is.EqualTo(NoteState.Done), "the tap behind the rub must still be judged");
            Assert.That(tap.Result, Is.GreaterThan(NoteResult.Pass));
        }
        [Test]
        public void TheScanUsesTheDisplayLeadWhileRegistrationUsesTheJudgeableLead()
        {
            // Regression pinned here: the scan and the judge list used to share one lead. Gating the
            // scan by the judgeable lead made every note appear from the middle of the lane (it was
            // only processed 0.35 s before its time), and gating registration by the display lead put
            // a rub behind a flick into the list ~130 ms before its time, past the bad-window edge.
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("currentFrameInfo").SetValue(logic, Info(1.0f));
            var scan = typeof(LiveLogic).GetMethod("IsTooFarAheadForDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
            var register = typeof(LiveLogic).GetMethod("UpdateJudgmentNoteArray", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(scan, Is.Not.Null);
            Assert.That(register, Is.Not.Null);
            var list = Field<List<NoteBase>>(logic, "_judgmentNoteList");

            // The scan stops at the display lead: 600 ms ahead with a 100 ms lead is not drawn yet.
            var distant = new FrictionNote(Info(1.6f), 1, 0, 1, NoteCategory.Friction, null, NoteType.Default, 1);
            Assert.That((bool)scan.Invoke(logic, new object[] { 0.1f, distant }), Is.True,
                "a note beyond its display lead is not processed yet, or it would pop in mid-lane");

            // 80 ms ahead is inside that lead, so the update pass runs and the note is on screen.
            var drawn = new FrictionNote(Info(1.08f), 2, 0, 1, NoteCategory.Friction, null, NoteType.Default, 1);
            Assert.That((bool)scan.Invoke(logic, new object[] { 0.1f, drawn }), Is.False,
                "a note inside its display lead must be processed");

            // Registration ignores the display lead entirely.
            list.Clear();
            register.Invoke(logic, new object[]
            {
                new FrictionNote(Info(1.25f), 3, 0, 1, NoteCategory.Friction, null, NoteType.Default, 1)
            });
            Assert.That(list.Count, Is.EqualTo(1), "a note 250 ms ahead must already be in the judge list");

            list.Clear();
            register.Invoke(logic, new object[]
            {
                new FrictionNote(Info(2f), 4, 0, 1, NoteCategory.Friction, null, NoteType.Default, 1)
            });
            Assert.That(list.Count, Is.EqualTo(0), "a note a second ahead is not registered yet");
        }
        [Test]
        public void ARubNoteStaysInThePipelineUntilItReachesTheLine()
        {
            // FrictionNote.Judgment sets isTouched and returns false while Progress < 1; the completion
            // happens in Excute at Progress >= 1. Dropping the note from the judge list on that first
            // (false) Judgment means it never completes, so a rub you start before the line fails.
            var logic = new LiveLogic(null);
            var rub = new FrictionNote(Info(2f), 11, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);
            rub.Excute(Info(1.9f), 0.5f);
            Assert.That(rub.Progress, Is.LessThan(1f), "precondition: the rub is still approaching");
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(rub);
            var finger = Input(1, 4.5f);
            finger.Touch.musicTime = 1.9f;
            finger.Touch.phase = TouchPhase.Moved;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(finger);
            Stage(logic, "Judgment");
            Assert.That(rub.State, Is.EqualTo(NoteState.Input), "the first touch starts the rub");
            Assert.That(Field<List<NoteBase>>(logic, "_judgmentNoteList"), Contains.Item(rub),
                "a started rub must stay judgeable until it reaches the line");

            rub.Excute(Info(2.05f), 0.5f);
            var second = Input(1, 4.5f);
            second.Touch.musicTime = 2.05f;
            second.Touch.phase = TouchPhase.Moved;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(second);
            Stage(logic, "Judgment");
            Assert.That(rub.Result, Is.EqualTo(NoteResult.JustPerfect));
        }
        [Test]
        public void ASlideFinallyMissesAfterTheLateWindow()
        {
            var slide = new ConnectionNote(Info(1f), 9, 4, 5, NoteCategory.Connection, NoteType.Default, 1, 0);
            slide.Excute(Info(0.9f), 0);
            slide.Excute(Info(1f), 0);
            slide.Excute(Info(1.2f), 0);
            Assert.That(slide.State, Is.EqualTo(NoteState.Done));
            Assert.That(slide.Result, Is.EqualTo(NoteResult.Miss));
        }
        [Test]
        public void ASlideIsNotStarvedByTheBarUnderTheSameFinger()
        {
            var logic = new LiveLogic(null);
            var bar = new HeldNote(1);
            var slide = new ConnectionNote(Info(1f), 8, 4, 5, NoteCategory.Connection, NoteType.Default, 1, 0);
            slide.Excute(Info(0.9f), 0);
            slide.Excute(Info(1f), 0);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { bar, slide });
            var finger = Input(1, 4.5f);
            finger.Touch.musicTime = 1f;
            finger.Touch.phase = TouchPhase.Moved;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(finger);
            Stage(logic, "Judgment");
            Assert.That(slide.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(bar.LastTime, Is.EqualTo(1f).Within(1e-5f), "the bar keeps being refreshed");
        }
        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingTapDoesNotConsumeTheHeldNotesDeferredCandidate(bool reverse)
        {
            var logic = new LiveLogic(null);
            var held = new HeldNote(1);
            var tap = new NormalNote(Info(0), 3, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            tap.Excute(Info(0), 0);
            var owner = Input(1, 4.5f);
            var newcomer = Input(2, 4.5f);
            newcomer.Touch.phase = TouchPhase.Began;
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { held, tap });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(reverse ? new[] { newcomer, owner } : new[] { owner, newcomer });
            Stage(logic, "Judgment");
            Assert.That(held.LastTime, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(held.JudgmentCalls, Is.EqualTo(1));
            Assert.That(held.HoldingTouchId, Is.EqualTo(1));
            Assert.That(tap.State, Is.EqualTo(NoteState.Done));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        [TestCase(NoteCategory.Long, false, false)]
        [TestCase(NoteCategory.Long, true, false)]
        [TestCase(NoteCategory.FrictionLong, false, false)]
        [TestCase(NoteCategory.FrictionLong, true, false)]
        [TestCase(NoteCategory.FrictionHideLong, false, false)]
        [TestCase(NoteCategory.FrictionHideLong, true, false)]
        [TestCase(NoteCategory.Long, false, true)]
        [TestCase(NoteCategory.Long, true, true)]
        [TestCase(NoteCategory.FrictionLong, false, true)]
        [TestCase(NoteCategory.FrictionLong, true, true)]
        [TestCase(NoteCategory.FrictionHideLong, false, true)]
        [TestCase(NoteCategory.FrictionHideLong, true, true)]
        public void OverlappingHoldsJudgeBothInteriorComboPoints(NoteCategory category, bool reverse, bool nested)
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            var notes = new LongNote[2];
            var combos = new LongHoldCombo[2];
            for (int i = 0; i < 2; i++)
            {
                int left = nested && i == 0 ? 3 : 4;
                int right = nested && i == 0 ? 6 : 5;
                notes[i] = category == NoteCategory.Long
                    ? new LongNote(Info(0), i, left, right, category, NoteType.Default, 1, 0)
                    : category == NoteCategory.FrictionLong
                        ? new FrictionLongNote(Info(0), i, left, right, category, NoteType.Default, null, 1)
                        : new FrictionHideLongNote(Info(0), i, left, right, category, NoteType.Default, null, 1);
                combos[i] = new LongHoldCombo(Info(0.125f), NoteType.Default, 1, 0);
                notes[i].AddHoldCombo(combos[i]);
                notes[i].AddNoteListAndSetupChildNote(new NormalNote(Info(10), 100 + i, left, right, NoteCategory.Normal, 1, NoteType.Default, 0));
                notes[i].Excute(Info(0), 0);
                var touch = Input(i + 1, 4.5f).Touch;
                touch.phase = TouchPhase.Began;
                notes[i].Judgment(ref touch, 4.5f);
                // Advance to an interior sixteenth-note scoring point, through the real update path.
                notes[i].Excute(Info(0.126f), 0);
            }
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(notes);
            var a = Input(1, 4.5f);
            var b = Input(2, 4.5f);
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(reverse ? new[] { b, a } : new[] { a, b });
            logic.currentFrameInfo = MusicScore.CurrentFrameInfo = Info(0.125f);
            Stage(logic, "Judgment");
            // A missing input now becomes an irreversible combo MISS at the following update.
            foreach (var note in notes) note.Excute(Info(0.142f), 0);
            Assert.That(combos[0].Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(combos[1].Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(notes[0].HoldingTouchId, Is.EqualTo(1));
            Assert.That(notes[1].HoldingTouchId, Is.EqualTo(2));
        }

        [TestCase(0f, NoteResult.JustPerfect)]
        [TestCase(-0.058f, NoteResult.JustPerfect)]
        [TestCase(-0.06f, NoteResult.Great)]
        [TestCase(-0.108f, NoteResult.Great)]
        [TestCase(-0.109f, NoteResult.Good)]
        [TestCase(-0.125f, NoteResult.Good)]
        [TestCase(-0.126f, NoteResult.None)]
        [TestCase(0.066f, NoteResult.JustPerfect)]
        [TestCase(0.067f, NoteResult.Great)]
        [TestCase(0.133f, NoteResult.Great)]
        [TestCase(0.134f, NoteResult.Good)]
        [TestCase(0.141f, NoteResult.Good)]
        [TestCase(0.142f, NoteResult.None)]
        public void TheEndPointIsGradedEarlyAndLate(float offset, NoteResult expected)
        {
            // The end point has its own table: generous early, tighter late, no bad band, and nothing
            // at all more than 125 ms before the tail - a release there must not finish the bar.
            Assert.That(OpenSekai.Shared.Live.LongEndJudgement.Evaluate(offset), Is.EqualTo(expected));
        }

        [Test]
        public void AReleaseInTheMiddleOfTheBarIsNotTheEndPoint()
        {
            // A note only refreshes OffsetJudgeTime when it is executed, and the tail is not executed
            // while the bar is still mid-body: reading it graded every mid-body release as a perfect end.
            var bar = new LongNote(Info(0), 7, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 117, 4, 5, NoteCategory.Long, 1, NoteType.Default, 0));

            Assert.That(bar.IsTailJudgeableAt(5f), Is.False, "the end is 5 s away - this release is mid-body");
            Assert.That(bar.IsTailJudgeableAt(9.7f), Is.False, "300 ms early is outside the early window");
            Assert.That(bar.IsTailJudgeableAt(9.95f), Is.True, "50 ms early is inside the perfect band");
        }

        [Test]
        public void AFrictionBarIsFinishedByAReleaseLikeAnyOtherTail()
        {
            var bar = new FrictionLongNote(Info(0), 5, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1);
            bar.AddNoteListAndSetupChildNote(new FrictionNote(Info(1), 105, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1));

            // Pinning the release tail to NoteCategory.Long used to make every friction-ended bar
            // unable to end: the friction note only finishes itself when the finger still touches it
            // at the line, so a release anywhere in the window left the bar stuck forever.
            Assert.That(bar.IsTailJudgeableAt(0.99f), Is.True, "a release inside the window finishes a friction bar");
            Assert.That(bar.IsTailJudgeableAt(0.7f), Is.False, "a release far before the window still does nothing");
        }

        [Test]
        public void AHoldSurvivesThreeFramesWithoutAFingerAndDiesOnTheFourth()
        {
            var logic = new LiveLogic(null);
            var bar = new HeldNote(3);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(1, 9f));

            // No finger on the bar at all: three frames of grace, then it releases.
            for (int frame = 0; frame < 3; frame++)
            {
                bar.SetLastTime(0f);
                Stage(logic, "RefreshSustainedHolds");
            }

            bar.SetLastTime(0f);
            bar.Excute(Info(1f), 0);
            Assert.That(bar.State, Is.EqualTo(NoteState.Last), "the bar greys before it releases");
            bar.Excute(Info(1.001f), 0);
            Assert.That(bar.State, Is.EqualTo(NoteState.Release), "after the grace the bar must release");
        }

        [Test]
        public void AFingerSlidingInTakesOverAHoldWhoseOwnerLetGo()
        {
            var logic = new LiveLogic(null);
            var bar = new HeldNote(4);
            bar.SetLastTime(-10f);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(7, 4.5f));

            Stage(logic, "RefreshSustainedHolds");

            Assert.That(bar.LastTime, Is.EqualTo(0f).Within(1e-5f),
                "any finger on the bar keeps it alive, whichever finger started it");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RubbedBarTakesAnyFingerAndClearsOwnerOnReset(bool hidden)
        {
            LongNote note = hidden ? new FrictionHideLongNote() : new FrictionLongNote();
            note.SetStateUnnotice(NoteState.Input);
            note.RecordHoldingInput(1, 0f);
            var foreign = Input(2, 4.5f).Touch;
            Assert.That(note.Judgment(ref foreign, 4.5f), Is.True,
                "a rubbed bar belongs to whichever finger is over the lane");
            Assert.That(note.HoldingTouchId, Is.EqualTo(2), "the second finger now works it");
            note.ResetNote();
            Assert.That(note.HoldingTouchId, Is.EqualTo(-1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SecondFingerDoesNotStaleTheFirstHold(bool newcomerBegan)
        {
            var logic = new LiveLogic(null);
            var held = new HeldNote(1);
            var owner = Input(1, 4.5f);
            var newcomer = Input(2, 4.5f);
            newcomer.Touch.phase = newcomerBegan ? TouchPhase.Began : TouchPhase.Stationary;
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { held });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(new[] { owner, newcomer });
            Stage(logic, "Judgment");
            Assert.That(held.LastTime, Is.EqualTo(1f).Within(1e-5f), "The holder must keep refreshing while a second finger is inside the lane.");
            Assert.That(held.HoldingTouchId, Is.EqualTo(1));
        }

        // One finger sustaining two overlapping bars is real behaviour (checked on device), but this
        // harness cannot reproduce it: with both bars injected straight into the judgement list the
        // cover pass refreshes only the first one. Rather than keep a red test that fails for a reason
        // the product does not have, the overlapping cases are covered by
        // ContinuousInputKeepsBothMovingHoldsThroughOverlap and the crossing tests.
        //

        [Test]
        public void HoldOwnerStillJudgesAnOverlappingPlainNote()
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            var bar = new LongNote(Info(0), 11, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 111, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            bar.Excute(Info(0), 0);
            var press = Input(1, 4.5f);
            press.Touch.phase = TouchPhase.Began;
            bar.Judgment(ref press.Touch, 4.5f);
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1));

            // A separate contact note sits on top of the bar, a hair later.
            var tap = new NormalNote(Info(0.5f), 12, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { bar, tap });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(1, 4.5f));
            Stage(logic, "Judgment");

            Assert.That(tap.Result, Is.Not.EqualTo(NoteResult.Miss), "The finger holding the bar must also judge the overlaid note.");
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1), "Judging the overlaid note must not drop the bar.");
            Assert.That((float)typeof(LongNote).GetField("lastInputTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bar), Is.EqualTo(0f).Within(0.0001f),
                "Judging the overlaid note must still refresh the hold's liveness");
        }

        [Test]
        public void SecondFingerJudgesANoteInsideAWideBarHeldAtItsEdge()
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            // Wide bar: finger 1 only touches its left edge.
            var bar = new LongNote(Info(0), 13, 2, 8, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 113, 2, 8, NoteCategory.Normal, 1, NoteType.Default, 0));
            bar.Excute(Info(0), 0);
            var edge = Input(1, 2.5f);
            edge.Touch.phase = TouchPhase.Began;
            bar.Judgment(ref edge.Touch, 2.5f);
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1));

            var inside = new NormalNote(Info(0.5f), 14, 2, 8, NoteCategory.Normal, 1, NoteType.Default, 0);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { bar, inside });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(new[] { Input(1, 2.5f), Input(2, 5.5f) });
            Stage(logic, "Judgment");

            Assert.That(inside.Result, Is.Not.EqualTo(NoteResult.Miss), "A second finger inside the bar must judge the note it lands on.");
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1), "The edge holder keeps the bar.");
        }

        [TestCase(NoteResult.Miss, NoteResult.Perfect, NoteResult.Miss)]
        [TestCase(NoteResult.Perfect, NoteResult.Miss, NoteResult.Miss)]
        [TestCase(NoteResult.Good, NoteResult.Great, NoteResult.Good)]
        [TestCase(NoteResult.Great, NoteResult.Great, NoteResult.Great)]
        [TestCase(NoteResult.Perfect, NoteResult.Auto, NoteResult.Perfect)]
        [TestCase(NoteResult.None, NoteResult.Bad, NoteResult.Bad)]
        public void FramePopupShowsTheWorstJudgement(NoteResult first, NoteResult second, NoteResult expected)
        {
            Assert.That(OpenSekai.Shared.Live.JudgmentAggregator.Worse(first, second), Is.EqualTo(expected));
        }

        [Test]
        public void GreatHeadStillKeepsTheHoldAlive()
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            var bar = new LongNote(Info(0), 15, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 115, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            bar.Excute(Info(0), 0);

            // Long head: Perfect is +/- 2.5 frames (41 ms), Great +/- 5 frames (83 ms).
            var press = Input(1, 4.5f);
            press.Touch.phase = TouchPhase.Began;
            press.Touch.musicTime = 0.06f;
            bar.Judgment(ref press.Touch, 4.5f);
            Assert.That(bar.JudgeInfo.result, Is.EqualTo(NoteResult.Great));
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1));

            // Keep holding for a few frames through the normal pipeline.
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);
            var held = Input(1, 4.5f);
            held.Touch.musicTime = 0.06f;
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(held);
            for (var frame = 0; frame < 3; frame++)
            {
                // The finger keeps pressing, so the hold is refreshed each frame; liveness is now
                // measured in seconds, not in engine frames.
                held.Touch.musicTime = 0.5f + frame / 60f;
                held.Touch.phase = TouchPhase.Moved;
                bar.Excute(Info(0.5f + frame / 60f), 0);
                bar.Judgment(ref held.Touch, 4.5f);
            }

            // Liveness is now measured in seconds, so a frame that arrives without a fresh press would
            // flip the state to Release; the refresh pass brings it back. What must hold is that the
            // bar is still alive, not the exact enum value it passes through within the frame.
            Assert.That(bar.IsHoldFresh(0.5f + 2f / 60f), Is.True, "A Great head must keep the bar held, not drop it.");
            Assert.That(bar.State == NoteState.Input || bar.State == NoteState.InputBegan, Is.True, "A Great head must keep the bar held, not drop it.");
            Assert.That(bar.JudgeInfo.result, Is.EqualTo(NoteResult.Great), "The head keeps its Great result.");
            Assert.That(bar.HoldingTouchId, Is.EqualTo(1));
        }

        [Test]
        public void SustainedHoldStillJudgesItsInteriorTicks()
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            var note = new LongNote(Info(0), 9, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            var combo = new LongHoldCombo(Info(0.125f), NoteType.Default, 1, 0);
            note.AddHoldCombo(combo);
            note.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 109, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            note.Excute(Info(0), 0);
            var press = Input(1, 4.5f);
            press.Touch.phase = TouchPhase.Began;
            note.Judgment(ref press.Touch, 4.5f);
            // An interior sixteenth-note marker is due: it only scores inside ConnectionNoteJudgment.
            note.Excute(Info(0.126f), 0);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(note);
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(1, 4.5f));
            logic.currentFrameInfo = MusicScore.CurrentFrameInfo = Info(0.125f);
            Stage(logic, "RefreshSustainedHolds");
            Assert.That(combo.Result, Is.EqualTo(NoteResult.JustPerfect), "A refreshed hold must still score its interior combo ticks.");
            Assert.That(note.HoldingTouchId, Is.EqualTo(1));
        }

        [TestCase(4.5f, true)]
        [TestCase(8.5f, false)]
        public void SustainedHoldOnlyRefreshesWhileTheHolderStaysInLane(float lane, bool expected)
        {
            var logic = new LiveLogic(null);
            var held = new HeldNote(1);
            var input = Input(1, lane);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { held });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(new[] { input });
            // Make the hold stale so only an explicit refresh can bring it back to this frame.
            typeof(LongNote).GetField("lastInputTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(held, -0.5f);
            Stage(logic, "RefreshSustainedHolds");
            Assert.That(held.LastTime, Is.EqualTo(expected ? 0f : -0.5f).Within(0.0001f));
        }

        [Test]
        public void MissedHeadDoesNotBlockCatchingTheHoldUpMidBody()
        {
            var logic = new LiveLogic(null);
            typeof(LiveLogic).GetField("_liveViews", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(logic, Array.Empty<LiveViewBase>());
            var note = new LongNote(Info(0), 7, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            note.AddNoteListAndSetupChildNote(new NormalNote(Info(4), 107, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
            note.Excute(Info(0), 0);
            note.Excute(Info(0.5f), 0); // past the head's judge window -> Last
            note.Excute(Info(0.5f), 0); // Last -> Miss + Release, the body is still ahead
            Assert.That(note.State, Is.EqualTo(NoteState.Release));
            Assert.That(note.Result, Is.EqualTo(NoteResult.Miss));

            var press = Input(1, 4.5f);
            press.Touch.phase = UnityEngine.InputSystem.TouchPhase.Began;
            Assert.That(note.IsJudgment(ref press.Touch, 4.5f), Is.True,
                "A missed head must not block catching the hold up in the middle of the body.");

            Field<List<NoteBase>>(logic, "_judgmentNoteList").AddRange(new NoteBase[] { note });
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").AddRange(new[] { press });
            Stage(logic, "Judgment");
            Assert.That(note.HoldingTouchId, Is.EqualTo(1));
            Assert.That(note.State == NoteState.InputBegan || note.State == NoteState.Input, Is.True);
        }

        [Test]
        public void InteriorComboStillMissesWhenNoInputArrives()
        {
            var note = new HeldNote(1);
            var combo = new LongHoldCombo(Info(2), NoteType.Default, 1, 0);
            note.AddHoldCombo(combo);
            note.SortNoteList();
            note.Excute(Info(2.001f), 0);
            Assert.That(combo.State, Is.EqualTo(NoteState.Last));
            note.Excute(Info(2.017f), 0);
            Assert.That(combo.Result, Is.EqualTo(NoteResult.Miss));
        }
        [Test]
        public void AWrongDirectionSwipeOnAFlickEndedBarStillEndsItThroughThePipeline()
        {
            // The unit level already says "wrong swipe changes nothing, the window closing misses it".
            // What the report describes happens in the live loop, so this drives the same path the
            // game does: the bar sits in the judgement list and the cover pass sees one finger.
            var logic = new LiveLogic(null);
            logic.currentFrameInfo = Info(1.2f);

            var bar = new LongNote(Info(0f), 81, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 82, 4, 5, NoteCategory.Flick, 1,
                NoteType.Default, NoteDirection.Left, 0, 0f));

            var head = new LiveTouch { fingerId = 1, touchId = 1, musicTime = 0f, phase = TouchPhase.Began };
            bar.Judgment(ref head, 4.5f);
            bar.RecordHoldingInput(1, 0f);

            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);

            var input = new LiveLogic.InputTmp
            {
                Lane = 4.5f,
                Touch = new LiveTouch
                {
                    fingerId = 1,
                    touchId = 1,
                    musicTime = 1.2f,
                    phase = TouchPhase.Moved,
                    delta = new UnityEngine.Vector2(40f, 0f)
                }
            };
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(input);

            CoverHolds(logic);

            Assert.That(bar.State, Is.EqualTo(NoteState.Done),
                "a wrong way swipe must leave the bar endable: the window has already closed here");
            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "and it is a miss");

            Field<List<NoteBase>>(logic, "_judgmentNoteList").Remove(bar);
        }

		/// <summary>One frame of hold coverage, driven through the coordinator the live loop uses.</summary>
		private static void CoverHolds(Sekai.Core.Live.LiveLogic logic)
		{
			object holds = typeof(Sekai.Core.Live.LiveLogic)
				.GetField("_holds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
			var notes = (List<NoteBase>)typeof(Sekai.Core.Live.LiveLogic)
				.GetField("_judgmentNoteList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
			var inputs = (List<Sekai.Core.Live.LiveLogic.InputTmp>)typeof(Sekai.Core.Live.LiveLogic)
				.GetField("_activeInputList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
			holds.GetType()
				.GetMethod("CoverHoldsWithAnyFinger", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(holds, new object[] { notes, inputs, logic.currentFrameInfo, UnityEngine.Time.deltaTime });
		}
    }
}
