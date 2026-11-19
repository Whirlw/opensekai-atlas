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
            var bar = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<LongNote>(score.NoteArray), n => n.ChildNote is FlickNote && n.ChildNote is NoteBase { HasJudgment: true });
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

            // The report: press and hold still, and the tail flick grades itself perfect. A resting

            // finger is no swipe, so the tail\'s own clock can only ever miss it.

            var bar = new LongNote(Info(0f), 95, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(1f), 96, 4, 5, NoteCategory.Flick, 1,

                NoteType.Default, NoteDirection.Left, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);



            var still = Touch(1f, TouchPhase.Stationary);

            tail.Judgment(ref still, 4.5f);

            tail.Excute(Info(1.2f), 0.5f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss),

                "an unswiped tail is never graded as a hit: past its window it misses");

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



        private static LongNote FlickEndedBar(NoteDirection direction = NoteDirection.Default, float flickDistance = 0f)

        {

            var bar = new LongNote(Info(0f), 45, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(new FlickNote(Info(1f), 46, 4, 5, NoteCategory.Flick, 1,
                NoteType.Default, direction, laneOffset: 0f, screenDpiToInch: flickDistance));



            // Press the head so the bar is held; the end point only exists while the bar is being held.

            var head = Touch(0f, TouchPhase.Began);

            bar.Judgment(ref head, 4.5f);

            bar.RecordHoldingInput(0f);

            return bar;

        }



        [Test]

        public void ARubTailIsNotGradedByTheBarAndEndsWithIt()

        {

            // The bar leaves on its own time and writes nothing onto a self-judged end note. The note's

            // clock is its own now (LiveNoteSession.TickTails), so the bar ending first cannot take its

            // verdict away: it still publishes its own Miss afterwards.

            var bar = new LongNote(Info(0f), 63, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionNote(Info(1f), 64, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(0.5f);



            Assert.That(bar.IsPastTailWindow(2f), Is.True, "way past the tail window");

            Assert.That(bar.ExpireTail(2f), Is.True, "the bar ends on its own time");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "and it graded the note nothing");



            // The note keeps its own clock after the bar is gone.

            tail.Excute(Info(1.2f), 0.5f);

            tail.Excute(Info(1.3f), 0.5f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the rub end misses on its own");

        }

        [Test]

        public void AHiddenTailThatNeverSettlesStillLetsTheBarEnd()

        {

            // The one tail the bar does not wait for: a hidden carrier carries no judgement of its own, so

            // it never settles and the bar's own end is a Miss of its own.

            var bar = new LongNote(Info(0f), 63, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new HiddenConnectionNote(Info(1f), 64, 4, 5, NoteCategory.Hidden, NoteType.Default, 1, 0);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(0.5f);



            Assert.That(bar.IsPastTailWindow(0.9f), Is.False, "the tail has not arrived yet");

            Assert.That(bar.ExpireTail(0.9f), Is.False);



            Assert.That(bar.ExpireTail(2f), Is.True, "the bar must end");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Miss), "an unresolved tail is a miss");

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

            // The note settles and publishes the Miss on its own clock.

            var bar = new LongNote(Info(0f), 311, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 312, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Default, 0.0833f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            int published = 0;

            tail.OnJudgment = _ => published++;

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1f);



            tail.Excute(Info(1.2f), 0f);
            tail.Excute(Info(1.3f), 0f);

            Assert.That(tail.State, Is.EqualTo(NoteState.Done), "the tail is finished");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the untouched tail is graded Miss");

            Assert.That(published, Is.EqualTo(1), "and it publishes that verdict itself, once");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "an independent tail does not end its bar");



            Assert.That(bar.DetachStandaloneTail(1.4f), Is.True, "the bar detaches once its own time has gone");

            Assert.That(published, Is.EqualTo(1), "the bar adds no publication of its own");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "the tail's Miss belongs to the tail");

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
            bar.RecordHoldingInput(1f);



            var reported = new List<NoteBase>();

            tail.OnJudgment = note => reported.Add(note);



            // The flick end grades itself: its window closing is the Miss.
            tail.Excute(Info(1.2f), 0.5f);



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
            bar.RecordHoldingInput(1f);



            var reported = new List<NoteBase>();

            tail.OnJudgment = note => reported.Add(note);



            tail.Excute(Info(1.2f), 0f);
            tail.Excute(Info(1.3f), 0f);



            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss));

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
        public void OnlyTheTailRidesAlongWithTheBar()

        {

            var bar = new LongNote(Info(0f), 211, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FlickNote(Info(2f), 212, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.SetStateUnnotice(NoteState.Scrolling);
            bar.RecordHoldingInput(1f);



            var marker = new FlickNote(Info(1f), 213, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Default, 0, 0f);

            marker.SetParentNote(bar);



            Assert.That(tail.ParentRole, Is.EqualTo(NoteParentRole.Tail), "the tail is the key that follows the bar");

            Assert.That(marker.ParentRole, Is.EqualTo(NoteParentRole.Connection), "a preset marker leaves on its own judgement");

        }













        



        [Test]

        public void AFrictionFlickTailIsJudgedByItselfAndTheBarJustDetaches()

        {

            // A friction flick keeps its own rule; the bar ends invisibly where that note sits and

            // must not grade it through the swipe path.

            var bar = new LongNote(Info(0f), 95, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 96, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1.0f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.TailBehaviour.JudgesItself, Is.True, "a friction flick tail is not settled by the bar");

            Assert.That(bar.TailBehaviour.JudgesItself, Is.True);


            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));



            // The tail grades itself. The bar ends at the chart endpoint.

            tail.FinishWith(NoteResult.JustPerfect);

            Assert.That(tail.State, Is.EqualTo(NoteState.Done), "the tail settles itself");

            Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "an independent tail does not end its bar");



            Assert.That(bar.DetachStandaloneTail(1.4f), Is.True, "the bar leaves on its own time");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(bar.Result, Is.EqualTo(NoteResult.None), "the result belongs to the tail");

        }



        [Test]

        public void AnUnsettledFrictionFlickTailKeepsJudgingAfterTheBarEnds()

        {

            var bar = new LongNote(Info(0f), 97, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new FrictionFlickNote(Info(1f), 98, 4, 5, NoteCategory.FrictionFlick, 1, NoteType.Default, NoteDirection.Left, 0f, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            bar.RecordHoldingInput(1.0f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.DetachStandaloneTail(0.99f), Is.False, "the chart endpoint is ahead");
            Assert.That(bar.DetachStandaloneTail(1f), Is.True);
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(tail.Result, Is.EqualTo(NoteResult.None), "the bar must not grade a standalone tail");



            // The note's own clock closes its window after the body has left.

            tail.Excute(Info(1.2f), 0f);
            tail.Excute(Info(1.3f), 0f);

            Assert.That(tail.Result, Is.EqualTo(NoteResult.Miss), "the tail writes its own Miss");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.None));

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

            bar.RecordHoldingInput(1f);

            bar.SetStateUnnotice(NoteState.Scrolling);



            Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the lift at the tail's own time grades it");

            Assert.That(bar.ExpireTail(1f), Is.False, "and the bar's own ending on the same frame adds nothing");

            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect), "the verdict stands");

            Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect), "for the note too");

        }







        [Test]

        public void APlayedTailGrantsAnUncontactedComboPointThatIsStillPending()

        {

            // A finished point keeps its verdict when the tail supplies input to an open point.

            var bar = new LongNote(Info(0f), 401, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);

            var tail = new NormalNote(Info(1f), 402, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0f);

            bar.AddNoteListAndSetupChildNote(tail);

            var late = new LongHoldCombo(Info(0.9f), NoteType.Default, 1, 0f);

            var earned = new LongHoldCombo(Info(0.5f), NoteType.Default, 1, 0f);

            bar.AddHoldCombo(late);

            bar.AddHoldCombo(earned);

            earned.FinishWith(NoteResult.Great);

            bar.SetStateUnnotice(NoteState.Scrolling);

            bar.RecordHoldingInput(1f);



            Assert.That(bar.JudgeTailAsRelease(1f), Is.True, "the lift at the tail ends the bar");

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            Assert.That(late.Result, Is.EqualTo(NoteResult.Perfect), "the accepted lift settles the pending point");

            Assert.That(earned.Result, Is.EqualTo(NoteResult.Great), "and leaves an earned verdict alone");

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



        



		// Before the tail time a swipe on the bar waits for the release; a swipe off the bar used to be
		// dropped. The opening: off the bar, inside the tail note's lane, and inside the Perfect band.
		/// <summary>One frame of hold coverage, driven through the coordinator the live loop uses.</summary>
		private static void CoverHolds(Sekai.Core.Live.LiveLogic logic)
		{
			var holds = logic.Input.Holds;
			var notes = logic.Input.JudgmentNotes;
			var inputs = logic.Input.ActiveInputs;
			holds.GetType()
				.GetMethod("CoverHoldsWithAnyFinger", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(holds, new object[] { notes, inputs, logic.currentFrameInfo });
		}
    }

}
