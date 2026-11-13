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

    /// <summary>

    /// Characterisation of what a flick-ended bar does today when the swipe comes early.

    ///

    /// The question is not "should it be perfect" but "which link decides it", because the report

    /// was that a swipe far ahead of the tail is still graded perfect. These cases pin the current

    /// behaviour down so the fix has a baseline to move.

    /// </summary>

    [TestFixture]

    public class FlickEndBehaviourTests

    {

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);



        private static LiveTouch Touch(float musicTime, TouchPhase phase, int fingerId = 1)

        {

            // The third argument is the delta the flick gesture reads; a zero delta never counts as

            // a swipe, so it has to carry a real movement.

            return new LiveTouch(fingerId, fingerId, new UnityEngine.Vector2(3f, 4f), UnityEngine.Vector3.zero, phase, musicTime);

        }



        [Test]

        public void AFlickNoteHoldsAnEarlySwipeUntilItsOwnTime()

        {

            // The flick at 1.0 s is swiped 500 ms early. If the note completes on the spot then the

            // swipe timing is irrelevant, which is the shape of the reported behaviour.

            var flick = new FlickNote(Info(1f), 41, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            var early = Touch(0.5f, TouchPhase.Moved);



            flick.Judgment(ref early, 4.5f);



            Assert.That(flick.JudgeInfo.result, Is.EqualTo(NoteResult.None),

                "an early swipe must not settle the flick on the spot; it is only recorded");

        }



        [Test]

        public void AnEarlySwipeIsGradedTheSameAsAnOnTimeOne()

        {

            // Two notes, identical except for when the swipe arrives. Whatever the grading is, these

            // two results are the answer to "does the swipe time matter at all right now".

            NoteResult earlyResult = SwipeAt(0.5f);

            NoteResult onTimeResult = SwipeAt(1.0f);



            Assert.That(earlyResult, Is.EqualTo(onTimeResult),

                "current behaviour: an early swipe is graded exactly like an on time one (" + earlyResult + " vs " + onTimeResult + ")");

        }



        [Test]

        public void AnUnjudgedTailKeyLeavesTheLineAtItsOwnTime()

        {

            using var harness = new LiveHarness();
            var document = OpenSekai.Charts.Serialization.ChartJson.Read(System.IO.File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
            var score = OpenSekai.Live.Conversion.RuntimeChartConverter.Convert(document, UnityEngine.Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
            var bar = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<LongNote>(score.NoteArray), n => n.TailBehaviour.AcceptsSwipe && n.ChildNote is NoteBase { HasJudgment: true });
            harness.Arrange(score, new NoteBase[] { bar });
            harness.Step(bar.MusicScoreInfo.time, 10d);
            float tailTime = bar.ChildNote.MusicScoreInfo.time;
            harness.Step(tailTime, 11d);
            Assert.That(bar.GetTailKeyPhase(tailTime), Is.EqualTo(Sekai.TailKeyPhase.SlidingOut));
            Assert.That(((NoteBase)bar.ChildNote).Result, Is.EqualTo(NoteResult.None));

        }



        [Test]

        public void TheTailKeepsItsRoleEvenWhenTheConverterAlsoConnectsIt()

        {

            // The real chart pipeline attaches the tail twice: once as the child note, once through the

            // generic connection path. The second call used to demote it to Connection, which is why

            // every tail rule (timeout, key lifetime, ending the bar) silently stopped applying.

            var bar = new LongNote(Info(0f), 91, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 92, 4, 5, NoteCategory.Flick, 1,

                NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.AddConnectionNote(tail);



            Assert.That(tail.ParentRole, Is.EqualTo(NoteParentRole.Tail),

                "a connected tail must stay the tail");

        }



        [Test]

        public void AHeldFingerDoesNotCompleteTheTailFlickByItself()

        {

            // The report: press and hold still, and the tail flick grades itself perfect. The tail

            // belongs to the bar, so its own Excute must never settle it.

            var bar = new LongNote(Info(0f), 95, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 96, 4, 5, NoteCategory.Flick, 1,

                NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);



            var still = Touch(1f, TouchPhase.Stationary);

            tail.Judgment(ref still, 4.5f);

            tail.Excute(Info(1.2f), 0.5f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.None),

                "an unswiped tail must stay unjudged; the bar decides it");

        }



        [Test]

        public void ARetiringKeyKeepsMovingPastTheLine()

        {

            // A key nobody judged slides on instead of parking on the judgement line.

            var tail = new FlickNote(Info(1f), 97, 4, 5, NoteCategory.Flick, 1,

                NoteType.Default, NoteDirection.Left, 0, 0f);

            tail.Excute(Info(1f), 0.5f);

            float atLine = tail.Progress;



            tail.TickRetire(1.2f);



            Assert.That(tail.Progress, Is.GreaterThan(atLine),

                "before the line its progress grows towards 1, after it must keep growing");

            Assert.That(tail.Progress, Is.GreaterThan(1f), "and it must pass the line");

        }



        private static NoteResult SwipeAt(float swipeTime)

        {

            var flick = new FlickNote(Info(1f), 42, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            var touch = Touch(swipeTime, TouchPhase.Moved);

            flick.Judgment(ref touch, 4.5f);



            // Settle the note the way the pipeline does: once the frame is far enough past the swipe

            // it grades whatever was recorded.

            flick.Excute(Info(1.2f), 0f);

            return flick.JudgeInfo.result;

        }



        [Test]

        public void AFlickEndedBarStillRefusesAClearlyEarlyRelease()

        {

            var bar = new LongNote(Info(0f), 43, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 44, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f));



            // A flick-ended bar never takes a release as its end point, however close to the tail:

            // it waits for the swipe. (It used to answer true here because the tail was tagged Long.)

            Assert.That(bar.IsTailJudgeableAt(0.8f), Is.False, "200 ms early is outside the bar's own early window");

            Assert.That(bar.IsTailJudgeableAt(0.95f), Is.False, "even inside the band a flick tail waits for the swipe");

        }



        private static LongNote FlickEndedBar(NoteDirection direction = NoteDirection.Default)

        {

            var bar = new LongNote(Info(0f), 45, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 46, 4, 5, NoteCategory.Flick, 1, NoteType.Default, direction, 0, 0f));



            // Press the head so the bar is held; the end point only exists while the bar is being held.

            var head = Touch(0f, TouchPhase.Began);

            bar.Judgment(ref head, 4.5f);

            bar.RecordHoldingInput(1, 0f);

            return bar;

        }



        [Test]

        public void ARubTailIsNotEndedBeforeItHasDecidedItself()

        {

            // A tail that carries a judgement of its own keeps its own clock: it is executed while it stays

            // in the bar's note list and publishes its own Miss when its window closes. The bar used to

            // detach it at its own end instead, which orphaned it: a friction or friction-flick end was

            // then never judged and never missed.

            var bar = new LongNote(Info(0f), 63, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionNote(Info(1f), 64, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 0.5f);



            Assert.That(bar.IsPastTailWindow(2f), Is.True, "way past the tail window");

            Assert.That(bar.ExpireTail(2f), Is.False, "but the note is still open, so the bar waits");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));



            // The note misses on its own clock; only then does the bar leave, reading that verdict.

            tail.Excute(Info(1.2f), 0.5f);

            tail.Excute(Info(1.3f), 0.5f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the rub end misses on its own");

            Assert.That(bar.ExpireTail(2f), Is.True, "and now the bar ends");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "reading the verdict the note published");

        }

        [Test]

        public void AHiddenTailThatNeverSettlesStillLetsTheBarEnd()

        {

            // The one tail the bar does not wait for: a hidden carrier carries no judgement of its own, so

            // it never settles and the bar's own end is a Miss of its own.

            var bar = new LongNote(Info(0f), 63, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new HiddenConnectionNote(Info(1f), 64, 4, 5, NoteCategory.Hidden, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 0.5f);



            Assert.That(bar.IsPastTailWindow(0.9f), Is.False, "the tail has not arrived yet");

            Assert.That(bar.ExpireTail(0.9f), Is.False);



            Assert.That(bar.ExpireTail(2f), Is.True, "the bar must end");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "an unresolved tail is a miss");

        }

        [Test]

        public void AWrongDirectionSwipeStillEndsTheBarButCapsAtGreat()

        {

            // A deliberate swipe the other way is not ignored: it ends the bar like any other and

            // keeps its timing grade - except that it can never be Perfect.

            var bar = FlickEndedBar(NoteDirection.Left);

            var swipe = new UnityEngine.Vector2(80f, 0f); // right: against a left flick



            Assert.That(bar.TrySwipeTail(swipe, 0.02f, 1.0f), Is.True, "the wrong way still ends the bar");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Great), "a wrong swipe can never be perfect");

            Assert.That(bar.ChildNote.Result, Is.EqualTo(NoteResult.Great));

        }



        [Test]

        public void ABarWithNoSwipeAtAllStillEnds()

        {

            var bar = FlickEndedBar();



            Assert.That(bar.TickSwipeTail(1.05f), Is.False, "still inside the tail window");

            Assert.That(bar.TickSwipeTail(1.3f), Is.True, "the window closed without a swipe");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss));

        }



        [Test]

        public void ADefiniteSwipeFromThePreviousRunDoesNotFinishAReusedBar()

        {

            // A bar comes back from the pool with the deferred swipe of the previous run still set.

            var bar = FlickEndedBar();

            Assert.That(bar.NotifyLaneSwipe(50f, 0.02f), Is.False, "a swipe before the tail time is only recorded");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "nothing is judged yet");



            // The chart restarts and the same note object is handed out again.

            bar.ResetNote();



            Assert.That(bar.TickSwipeTail(0.5f), Is.False, "the reset clears the deferred swipe");

            Assert.That(bar.State, Is.EqualTo(NoteState.Wait), "the reused bar stays unstarted");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "and keeps no result");

        }



        [Test]

        public void AGradingAttemptFarBeforeTheTailIsRefusedEvenWhenItReachesTheGrader()

        {

            // Guards the pool path: whatever route reaches the grader, the window is re-checked there.

            var bar = FlickEndedBar();



            Assert.That(bar.JudgeTailAsRelease(-0.24f), Is.False, "the tail window is not open 340 ms early");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the bar is left alone");

        }

        [Test]

        public void ASwipeAgainstTheTailDirectionIsGradedAsAWrongGesture()

        {

            // The tail asks for a left flick, so a swipe to the right is the wrong gesture: it still

            // ends the bar, capped at Great instead of being thrown away.

            var bar = FlickEndedBar(NoteDirection.Left);



            bool judged = bar.TrySwipeTail(new UnityEngine.Vector2(80f, 0f), 1f / 60f, 1f);



            Assert.That(judged, Is.True, "the wrong gesture still settles the tail");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Great));

        }



        [Test]

        public void ASwipeAlongTheTailDirectionFinishesTheBar()

        {

            var bar = FlickEndedBar(NoteDirection.Left);



            bool judged = bar.TrySwipeTail(new UnityEngine.Vector2(-10f, 10f), 1f / 60f, 1f);



            Assert.That(judged, Is.True, "a left up swipe is what this tail asks for");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the bar is finished");

        }



        [Test]

        public void ASwipeBeforeTheBarIsHeldDoesNotFinishIt()

        {

            // A bar that has only just appeared is not being held yet. Swiping then must do nothing,

            // otherwise a short bar can be finished while its head is still travelling to the line.

            var bar = new LongNote(Info(0f), 47, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(0.1f), 48, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f));



            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Input), "a fresh bar is not held yet");

            Assert.That(bar.IsTailWindowOpenAt(0.05f), Is.True, "the tail window is open this early, so only the state gate can stop it");

            Assert.That(bar.NotifyLaneSwipe(10f, 0.05f), Is.False, "an unheld bar cannot be finished by a swipe");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the bar keeps running");

        }



        [Test]

        public void AFlickEndedTailIgnoresAPlainLift()

        {

            // A lift finishes an ordinary bar; on a flick ended bar it must do nothing, otherwise

            // releasing anywhere inside the bar would end it.

            var bar = FlickEndedBar();



            Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.True, "a bar whose tail is a flick is finished by a swipe");

            Assert.That(bar.JudgeTailAsRelease(1f), Is.False, "lifting is not a swipe, so it cannot finish this bar");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the bar must still be running after the lift");

        }



        [Test]

        public void ASwipeAfterTheTailStandardTimeJudgesImmediately()

        {

            var bar = FlickEndedBar();



            bool judged = bar.NotifyLaneSwipe(10f, 1.02f);



            Assert.That(judged, Is.True, "a swipe at the tail time settles the bar on the spot");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the bar is finished");

            Assert.That(bar.JudgeInfo.result, Is.EqualTo(NoteResult.JustPerfect), "20 ms late is still perfect");

        }



        [Test]

        public void ASwipeBeforeTheTailStandardTimeJudgesOnTheThirdFrameAfterRelease()

        {

            var bar = FlickEndedBar();



            // 100 ms early is inside the tail window but before the tail time, so it is only recorded.

            // Grading it on the spot would give a Great; deferring to the release is what makes it a

            // Perfect, which is the whole point of the rule.

            Assert.That(bar.NotifyLaneSwipe(10f, 0.9f), Is.False, "an early swipe is only remembered");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the bar keeps running");

            Assert.That(LongEndJudgement.Evaluate(0.9f - 1f), Is.EqualTo(NoteResult.Great),

                "on the spot, 100 ms early is only Great");



            bar.NotifyLaneRelease();



            Assert.That(bar.TickSwipeTail(0.95f), Is.False, "first frame after the release");

            Assert.That(bar.TickSwipeTail(1.00f), Is.False, "second frame after the release");

            Assert.That(bar.TickSwipeTail(1.02f), Is.True, "third frame after the release settles the deferred swipe");



            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the bar is finished");

            Assert.That(bar.JudgeInfo.result, Is.EqualTo(NoteResult.JustPerfect),

                "deferring to the release is what turns an early swipe into a good grade");

        }

        [Test]

        public void ADecorativeBarHandsItsFlickTailToThePlayer()

        {

            // A decorative bar scrolls by without input, but its end note is a real flick and must

            // stay playable. The bar is only decoration while nothing on it can be played, so the

            // tail keeps its own decoration flag off and the pipeline keeps feeding the bar.

            var bar = new LongNote(Info(0f), 71, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.SetDecoration(true);

            var tail = new FlickNote(Info(1f), 72, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);



            Assert.That(((NoteBase)bar.ChildNote).IsDecoration, Is.False,

                "the end note of a decorative bar is a real note");



            var logic = new Sekai.Core.Live.LiveLogic(null);

            logic.currentFrameInfo = Info(0.98f);

            logic.Input.Register(bar);



            var noteList = logic.Input.JudgmentNotes;

            Assert.That(noteList, Contains.Item(tail),

                "the key at the end of a decorative bar is the one the player judges");

            Assert.That(noteList, Has.No.Member(bar),

                "the decoration bar itself never answers input");

        }



        [Test]

        public void ARealSwipeOnADecorativeBarTailIsJudged()

        {

            // Drives the same path the game does: the bar sits in the judgement list, a finger drags

            // across it in the direction the tail flick asks for, and the cover pass runs.

            var bar = new LongNote(Info(0f), 73, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.SetDecoration(true);

            var tail = new FlickNote(Info(1f), 74, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);



            var logic = new Sekai.Core.Live.LiveLogic(null);

            logic.currentFrameInfo = Info(1.0f);

            var list = logic.Input.JudgmentNotes;

            list.Add(bar);



            // Put the bar into the held state the way the decoration pass does.

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1, 1f);



            var input = new Sekai.Core.Live.LiveLogic.InputTmp

            {

                Lane = 4.5f,

                Touch = new LiveTouch

                {

                    fingerId = 1,

                    touchId = 1,

                    musicTime = 1.0f,

                    phase = TouchPhase.Moved,

                    delta = new UnityEngine.Vector2(-60f, 0f)

                }

            };

            logic.Input.ActiveInputs.Add(input);



            // A decorative bar is not played, only its tail is, so the player has to click it first.

            input.Touch.phase = TouchPhase.Began;

            input.Touch.delta = UnityEngine.Vector2.zero;

            CoverHolds(logic);



            input.Touch.phase = TouchPhase.Moved;

            input.Touch.delta = new UnityEngine.Vector2(-60f, 0f);

            CoverHolds(logic);



            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the swipe has to settle the decorative bar's tail");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect));

        }



        [Test]

        public void AFingerOverAHeadThatWasNeverPressedDoesNotAdoptTheBar()

        {

            // Holding the lane before the head arrives is not a press: the head has to be graded

            // Miss, and only after that may any finger inside the lane take the bar over.

            var bar = new LongNote(Info(1f), 301, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(3f), 302, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f));



            var logic = new Sekai.Core.Live.LiveLogic(null);

            logic.currentFrameInfo = Info(0.6f);

            var list = logic.Input.JudgmentNotes;

            list.Add(bar);



            Cover(logic, Finger(0.6f, TouchPhase.Moved, 4.5f, Vector2.zero));



            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Input), "an unjudged head is not adopted by a resting finger");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "the head has not been graded yet");



            // Wait -> Playing, then Playing -> Last once the head is past JudgeTime, then Last grades

            // the head Miss on the following frame.

            bar.Excute(Info(1.1f), 0f);

            bar.Excute(Info(1.6f), 1f);

            bar.Excute(Info(1.7f), 1f);



            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "a head nobody pressed is a Miss");

        }



        [Test]

        public void AnUntouchedFrictionFlickTailIsGradedMiss()

        {

            // The friction flick tail judges itself, but nothing used to fail it: an untouched tail

            // has to end up Miss once its window is gone, or it is the one note that cannot be missed.

            // That Miss is the note's own verdict (tail-classes.md §3 类 1): the bar drives the note and

            // reads the result, it never writes it.

            var bar = new LongNote(Info(0f), 311, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 312, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Default, 0.0833f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            int published = 0;

            tail.OnJudgment = _ => published++;

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1, 1f);



            bar.Excute(Info(1.2f), 0f);
            bar.Excute(Info(1.3f), 0f);

            Assert.That(tail.State, Is.EqualTo(NoteState.Done), "the tail is finished");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the untouched tail is graded Miss");

            Assert.That(published, Is.EqualTo(1), "and it publishes that verdict itself, once");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "an independent tail does not end its bar");



            Assert.That(bar.DetachStandaloneTail(1.4f), Is.True, "the bar detaches once its own time has gone");

            Assert.That(published, Is.EqualTo(1), "the bar adds no publication of its own");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "it only reads the verdict the note wrote");

        }



        [Test]

        public void AMissedTailStillReportsItsJudgement()

        {

            // A tail nobody played is graded Miss, and that Miss has to reach the view layer: the

            // popup is what tells the player the note was missed. Only the hit sound is withheld.

            var bar = new LongNote(Info(0f), 321, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 322, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1, 1f);



            var reported = new List<NoteBase>();

            tail.OnJudgment = note => reported.Add(note);



            bar.FailTail();



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the tail is graded Miss");

            Assert.That(reported, Does.Contain(tail), "a missed tail still reports its judgement to the view");

        }



        [Test]

        public void AMissedStandaloneTailShowsItsCharacterEvenThoughTheBarDidNotGradeIt()

        {

            // An independent tail writes its own Miss (tail-classes.md §3 类 1), and that Miss still has

            // to reach the view layer: the popup is what tells the player the note was missed. The bar

            // is not the one failing it any more, so this pins the note's own reporting.

            var bar = new LongNote(Info(0f), 331, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 332, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Default, 0.0833f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1, 1f);



            var reported = new List<NoteBase>();

            tail.OnJudgment = note => reported.Add(note);



            bar.Excute(Info(1.2f), 0f);
            bar.Excute(Info(1.3f), 0f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss));

            Assert.That(tail.SuppressJudgmentEffect, Is.False, "the popup must not be suppressed");

            Assert.That(reported, Does.Contain(tail), "the Miss character has to be shown for the tail");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "and the bar is not the one that failed it");

        }



        [Test]

        public void AGuideBarTailIsAutoJudged()

        {

            // Autoplay has no finger to swipe with: a playable tail on a guide bar has to be judged

            // by the bar itself, otherwise autoplay can never hit that key.

            var bar = new GuideNote(Info(0f), 341, 4, 5, NoteCategory.Long, NoteType.Default, null, 1);

            var tail = new FlickNote(Info(1f), 342, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);



            Assert.That(bar.AutoJudgment(Info(0.5f)), Is.False, "nothing to judge before the tail time");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.None));



            Assert.That(bar.AutoJudgment(Info(1.1f)), Is.True, "autoplay has to hit the tail");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.Auto), "the tail is graded by autoplay");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "and the bar finishes with it");

        }



        // ------------------------------------------------------------------------------------

        // Three reports from the device: a held finger adopting a bar that was never pressed, a

        // preset marker sticking to the line after it was judged, and a swipe made in the middle

        // of the bar being remembered for its tail. These run through the real cover pass.

        // ------------------------------------------------------------------------------------



        private static Sekai.Core.Live.LiveLogic NewLogic(params NoteBase[] notes)

        {

            var logic = new Sekai.Core.Live.LiveLogic(null);

            var list = logic.Input.JudgmentNotes;

            foreach (var note in notes)

            {

                list.Add(note);

            }



            return logic;

        }



        private static void Cover(Sekai.Core.Live.LiveLogic logic, params Sekai.Core.Live.LiveLogic.InputTmp[] inputs)

        {

            var list = logic.Input.ActiveInputs;

            list.Clear();

            foreach (var input in inputs)

            {

                list.Add(input);

            }



            CoverHolds(logic);

            list.Clear();

        }



        private static Sekai.Core.Live.LiveLogic.InputTmp Finger(float musicTime, TouchPhase phase, float lane, Vector2 delta, int fingerId = 1)

        {

            return new Sekai.Core.Live.LiveLogic.InputTmp

            {

                Lane = lane,

                Touch = new LiveTouch

                {

                    fingerId = fingerId,

                    touchId = fingerId,

                    musicTime = musicTime,

                    phase = phase,

                    delta = delta

                }

            };

        }



        [Test]

        public void AWrongWaySwipeSeenEarlyIsStillCappedAtGreat()

        {

            // Same drive as the on-time case, but the swipe goes against the tail direction. The cap

            // has to survive the release delay: clearing it there let a wrong way swipe be Perfect.

            // A left tail, swiped to the right: the gesture is real but goes the wrong way.

            var bar = FlickEndedBar(NoteDirection.Left);



            Assert.That(bar.TrySwipeTail(new Vector2(60f, 0f), 1f / 60f, 0.9f), Is.False,

                "a wrong way swipe early is only remembered");

            bar.NotifyLaneRelease();



            Assert.That(bar.TickSwipeTail(0.95f), Is.False, "first frame after the release");

            Assert.That(bar.TickSwipeTail(1.00f), Is.False, "second frame after the release");

            Assert.That(bar.TickSwipeTail(1.02f), Is.True, "third frame settles it");



            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.JudgeInfo.result, Is.EqualTo(NoteResult.Great),

                "swiping the wrong way may never be graded above Great");

        }



        [Test]

        public void OnlyTheTailRidesAlongWithTheBar()

        {

            var bar = new LongNote(Info(0f), 211, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(2f), 212, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1, 1f);



            var marker = new FlickNote(Info(1f), 213, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            marker.SetParentNote(bar);



            Assert.That(tail.ParentRole, Is.EqualTo(NoteParentRole.Tail), "the tail is the key that follows the bar");

            Assert.That(marker.ParentRole, Is.EqualTo(NoteParentRole.Connection), "a preset marker leaves on its own judgement");

        }



        [Test]

        public void ASwipeInTheMiddleOfTheBarIsNotRememberedForItsTail()

        {

            var bar = new LongNote(Info(0f), 221, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(2f), 222, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f));

            bar.RecordHoldingInput(1, 0.9f);

            bar.SetStateUnnotice(NoteState.Scrolling);

            var logic = NewLogic(bar);



            logic.currentFrameInfo = Info(1.0f);

            Cover(logic, Finger(1.0f, TouchPhase.Moved, 4.5f, new Vector2(-60f, 0f)));



            Assert.That(bar.HasDeferredSwipe, Is.False, "a swipe one second before the tail is not an end point gesture");

        }



        [Test]

        public void ASwipeInsideTheEarlyWindowIsStillRemembered()

        {

            var bar = new LongNote(Info(0f), 231, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(2f), 232, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f));

            bar.RecordHoldingInput(1, 1.9f);

            bar.SetStateUnnotice(NoteState.Scrolling);

            var logic = NewLogic(bar);



            logic.currentFrameInfo = Info(1.95f);

            Cover(logic, Finger(1.95f, TouchPhase.Moved, 4.5f, new Vector2(-60f, 0f)));



            Assert.That(bar.HasDeferredSwipe, Is.True, "a swipe 50 ms before the tail is inside the window");

        }

        [Test]

        public void AnEarlySwipeWaitsForTheReleaseAndThenThreeFrames()

        {

            // An early swipe is only remembered; the countdown starts when the

            // finger leaves, and the deferred swipe is graded on the third frame after that.

            var bar = new LongNote(Info(0f), 90, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 91, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 0.9f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.NotifyLaneSwipe(60f, 0.90f), Is.False, "an early swipe must not settle on the spot");

            Assert.That(bar.HasDeferredSwipe, Is.True);



            // No release yet: the bar has to stay alive even though frames pass.

            Assert.That(bar.TickSwipeTail(0.92f), Is.False);

            Assert.That(bar.TickSwipeTail(0.94f), Is.False);

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));



            bar.NotifyLaneRelease();

            Assert.That(bar.TickSwipeTail(0.96f), Is.False);

            Assert.That(bar.TickSwipeTail(0.98f), Is.False);

            Assert.That(bar.TickSwipeTail(1.00f), Is.True, "graded on the third frame after the release");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.Not.EqualTo(NoteResult.None));

        }



        



        [Test]

        public void AFrictionFlickTailIsJudgedByItselfAndTheBarJustDetaches()

        {

            // A friction flick keeps its own rule; the bar ends invisibly where that note sits and

            // must not grade it through the swipe path.

            var bar = new LongNote(Info(0f), 95, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 96, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 1.0f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.TailBehaviour.AcceptsSwipe, Is.False, "a friction flick tail is not settled by the bar's swipe rule");

            Assert.That(bar.TailBehaviour.JudgesItself, Is.True);

            Assert.That(bar.TrySwipeTail(new UnityEngine.Vector2(-60f, 0f), 0.016f, 1.0f), Is.False);

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));



            // The tail judges itself. Its verdict must NOT end the bar (tail-classes.md §3 类 1): an
            // independent tail does not touch the bar in either direction, so the bar leaves on its own
            // time and only reads the verdict.

            tail.FinishWith(NoteResult.JustPerfect);

            Assert.That(tail.State, Is.EqualTo(NoteState.Done), "the tail settles itself");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "an independent tail does not end its bar");



            Assert.That(bar.DetachStandaloneTail(1.4f), Is.True, "the bar leaves on its own time");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect), "and takes the verdict it read");

        }



        [Test]

        public void AnUnsettledFrictionFlickTailEndsTheBarOnlyWhenItsWindowCloses()

        {

            var bar = new LongNote(Info(0f), 97, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 98, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 1.0f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.DetachStandaloneTail(1.0f), Is.False, "the window is still open");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));

            Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "the bar must not grade a standalone tail");



            // The note times itself out: the bar drives its children, and NoteBase.Excute turns a note

            // past its window into its own Miss. The bar waits for that verdict instead of writing one.

            bar.Excute(Info(1.2f), 0f);
            bar.Excute(Info(1.3f), 0f);

            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the tail writes its own Miss");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "and that does not end the bar");



            Assert.That(bar.DetachStandaloneTail(1.4f), Is.True, "past the window the bar has to end");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

        }



        [Test]

        public void AVerdictIsPublishedBeforeItsBarIsAllowedToLeave()

        {

            // The ordering R2 depends on: the bar hands the frame to its tail, the tail writes and

            // publishes its own verdict, and only then does that verdict end the bar. A bar that left

            // first would recycle its views before the tail could show the judgement.

            var bar = new LongNote(Info(0f), 411, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 412, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 1f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            int published = 0;

            bool aliveWhenPublished = false;

            tail.OnJudgment = _ => { published++; aliveWhenPublished = bar.State != NoteState.Done; };



            Assert.That(bar.NotifyLaneSwipe(10f, 1.02f), Is.True, "the swipe settles the tail");



            Assert.That(published, Is.EqualTo(1), "the tail publishes its verdict");

            Assert.That(aliveWhenPublished, Is.True, "and the bar is still in play at that moment");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "the verdict is what ends the bar");

        }



        [Test]

        public void AJudgementOnTheTailTimeFrameWinsOverTheBarsOwnEnding()

        {

            // The tail-time frame gives the bar two chances on the same frame: the tail's judgement first,

            // then its own ending (R4). Whichever arrives second must add nothing - so the lift at exactly

            // the tail's time is graded before expiry is even consulted.

            var bar = new LongNote(Info(0f), 421, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new NormalNote(Info(1f), 422, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 1f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the lift at the tail's own time grades it");

            Assert.That(bar.ExpireTail(1f), Is.False, "and the bar's own ending on the same frame adds nothing");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect), "the verdict stands");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect), "for the note too");

        }



        [Test]

        public void ADeferredSwipeKeepsTheBarAliveOnTheTailTimeFrame()

        {

            // A flick tail graded by a deferred swipe is not due on its own time frame: that frame only

            // starts the countdown, so the bar must not take it as its own ending.

            var bar = FlickEndedBar(NoteDirection.Left);

            Assert.That(bar.TrySwipeTail(new UnityEngine.Vector2(-60f, 0f), 1f / 60f, 0.9f), Is.False);

            Assert.That(bar.HasDeferredSwipe, Is.True, "the early swipe is remembered");



            Assert.That(bar.TickSwipeTail(1.0f), Is.False, "the tail-time frame starts the countdown");

            Assert.That(bar.ExpireTail(1.0f), Is.False, "and the bar must not end on it");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));

        }



        [Test]

        public void AComboPointTheBarsEndNeverReachedIsSettledPerfect()

        {

            // The bar shares its fate with its interior points and nothing else: whichever way it ends,

            // a point it never reached is settled Perfect right there, and a point that already has a

            // verdict keeps it.

            var bar = new LongNote(Info(0f), 401, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new NormalNote(Info(1f), 402, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            var late = new LongHoldCombo(Info(0.9f), NoteType.Default, 1, 0f);

            var earned = new LongHoldCombo(Info(0.5f), NoteType.Default, 1, 0f);

            bar.AddHoldCombo(late);

            bar.AddHoldCombo(earned);

            earned.FinishWith(NoteResult.Great);

            bar.SetStateUnnotice(NoteState.Scrolling);

            bar.RecordHoldingInput(1, 1f);



            Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the lift at the tail ends the bar");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(late.Result, Is.EqualTo(NoteResult.Perfect), "the bar's end settles what it made unreachable");

            Assert.That(earned.Result, Is.EqualTo(NoteResult.Great), "and leaves an earned verdict alone");

        }



        [Test]

        public void AQuickFlickReportedOnlyAsAnEndedSampleStillFinishesTheBar()

        {

            // The real pipeline: a fast flick can arrive as a single Ended sample that carries the

            // whole movement, and that sample is usually registered above the judgement line, so it

            // must not be filtered out by the lane test before its delta is read.

            var bar = new LongNote(Info(0f), 101, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 102, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1, 0.9f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            var logic = new Sekai.Core.Live.LiveLogic(null);

            var list = logic.Input.JudgmentNotes;

            list.Add(bar);

            var inputs = logic.Input.ActiveInputs;

            



            logic.currentFrameInfo = Info(0.90f);

            inputs.Add(new Sekai.Core.Live.LiveLogic.InputTmp

            {

                Lane = -1f, // above the judgment line, like a real lift

                Touch = new LiveTouch

                {

                    fingerId = 1,

                    touchId = 1,

                    musicTime = 0.90f,

                    phase = TouchPhase.Ended,

                    delta = new UnityEngine.Vector2(-60f, 0f)

                }

            });

            CoverHolds(logic);

            inputs.Clear();



            Assert.That(bar.HasDeferredSwipe, Is.True, "the ended sample has to be remembered as the swipe");



            logic.currentFrameInfo = Info(0.92f);

            CoverHolds(logic);

            logic.currentFrameInfo = Info(0.94f);

            CoverHolds(logic);

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));

            logic.currentFrameInfo = Info(0.96f);

            CoverHolds(logic);



            Assert.That(bar.State, Is.EqualTo(NoteState.Done), "graded on the third frame after the release");

        }



        [Test]

        public void SlidingOutOfTheLaneCountsAsTheReleaseToo()

        {

            LongNote bar = FlickEndedBar(NoteDirection.Left);

            bar.TrySwipeTail(new UnityEngine.Vector2(-60f, 0f), 1f / 60f, 0.90f);

            Assert.That(bar.HasDeferredSwipe, Is.True, "the early swipe is remembered, no countdown yet");



            // No finger is inside the bar any more: that is a release, exactly like lifting.

            for (int frame = 0; frame < 3; frame++)

            {

                bar.TickSwipeTail(0.92f + frame * 0.02f);

                Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "third frame after the release grades it");

                bar.NotifyLaneRelease();

            }



            bar.TickSwipeTail(1.00f);

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

        }



        [Test]

        public void ReachingTheTailTimeCountsAsTheRelease()

        {

            LongNote bar = FlickEndedBar(NoteDirection.Left);

            bool deferred = bar.TrySwipeTail(new UnityEngine.Vector2(-60f, 0f), 1f / 60f, 0.90f);

            Assert.That(bar.HasDeferredSwipe, Is.True, "swiped early, nothing graded yet");

            Assert.That(deferred, Is.False);



            // Still holding at the tail time: that alone must start the countdown.

            Assert.That(bar.TickSwipeTail(1.00f), Is.False, "frame zero of the release");

            Assert.That(bar.TickSwipeTail(1.02f), Is.False, "frame one");

            Assert.That(bar.TickSwipeTail(1.04f), Is.False, "frame two");



            Assert.That(bar.TickSwipeTail(1.06f), Is.True, "frame three grades it");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

        }



        [Test]

        public void AnUnjudgedMarkerDoesNotReportAResult()

        {

            // Combo markers fire score/combo/sound when they leave the pool. A marker the bar never

            // judged must stay silent, otherwise ending a bar early machine-guns the hit sound and

            // spikes the combo counter.

            var marker = new LongHoldCombo(Info(0.5f), NoteType.Default, 1, 0f);

            int judged = 0;

            int combo = 0;

            marker.OnJudgment = _ => judged++;

            marker.OnUpdateCombo = _ => combo++;



            marker.SetStateUnnotice(NoteState.Done);



            Assert.That(judged, Is.EqualTo(0), "an unjudged marker must not play a judgement");

            Assert.That(combo, Is.EqualTo(0), "an unjudged marker must not count towards the combo");

        }



        



		/// <summary>One frame of hold coverage, driven through the coordinator the live loop uses.</summary>
		private static void CoverHolds(Sekai.Core.Live.LiveLogic logic)
		{
			var holds = logic.Input.Holds;
			var notes = logic.Input.JudgmentNotes;
			var inputs = logic.Input.ActiveInputs;
			holds.GetType()
				.GetMethod("CoverHoldsWithAnyFinger", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(holds, new object[] { notes, inputs, logic.currentFrameInfo, UnityEngine.Time.deltaTime });
		}
    }

}