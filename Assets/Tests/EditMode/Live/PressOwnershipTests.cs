using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Who a press goes to. Collecting candidates spends nothing; a press is spent where a note consumes
    /// it, and one finger has one press credit per frame. The two orders are explicit: a shared note goes
    /// to the finger nearer its lane centre, then the smaller finger id; one finger's own candidates are
    /// ranked by chart time, then chart order.
    /// </summary>
    public sealed class PressOwnershipTests
    {
        private const double INPUT_TIME = 10d;
        private FakeTouchSource _source;
        private LiveLogic _logic;
        private Mouse _isolatedMouse;
        private Mouse _previousMouse;
        private bool _previousLegacyTiming;
        private MusicScoreInfo _previousFrame;
        private LiveConfig.NoteTypeJudgeData _previousJudgeData;

        private sealed class CountingNote : NormalNote
        {
            internal int JudgmentCalls;

            internal CountingNote(int id, float time, int lane, float reachStart, float reachEnd)
                : base(Info(time), id, lane, lane, NoteCategory.Normal, 1f, NoteType.Default, 0f)
            {
                JudgeLaneStart = reachStart;
                JudgeLaneEnd = reachEnd;
            }

            public override bool Judgment(ref LiveTouch touch, float lane)
            {
                JudgmentCalls++;
                return base.Judgment(ref touch, lane);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _previousLegacyTiming = LiveConfig.UseLegacyJudgeTiming;
            _previousFrame = MusicScore.CurrentFrameInfo;
            _previousJudgeData = LiveConfig.noteTypeJudgeData;
            LiveConfig.LoadMasterConfig();
            LiveConfig.UseLegacyJudgeTiming = false;
            _previousMouse = Mouse.current;
            _isolatedMouse = InputSystem.AddDevice<Mouse>();
            _source = new FakeTouchSource();
            _logic = new LiveLogic(null);
            _logic.SetTouchSource(_source);
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(_isolatedMouse);
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            LiveConfig.UseLegacyJudgeTiming = _previousLegacyTiming;
            LiveConfig.noteTypeJudgeData = _previousJudgeData;
            MusicScore.CurrentFrameInfo = _previousFrame;
        }

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120f, 4f, 1f, 1f);

        /// <summary>A world position whose resolved lane is <paramref name="lane"/>, fractions included.</summary>
        private static Vector3 At(float lane)
        {
            int left = Mathf.FloorToInt(lane);
            Vector2 a = LiveConfig.JudgmentPositions[left];
            Vector2 b = LiveConfig.JudgmentPositions[Mathf.Min(left + 1, LiveConfig.JudgmentPositions.Length - 1)];
            return Vector2.Lerp(a, b, lane - left);
        }

        private static LiveTouchSample Press(int touchId, float lane, int? fingerId = null)
        {
            return new LiveTouchSample(touchId, new Vector2(800f, 600f), At(lane), INPUT_TIME,
                InputTouchPhase.Began, Vector2.zero, fingerId);
        }

        private static LiveTouchSample PressAt(int touchId, float lane, double inputTime, int fingerId)
        {
            return new LiveTouchSample(touchId, new Vector2(800f, 600f), At(lane), inputTime,
                InputTouchPhase.Began, Vector2.zero, fingerId);
        }

        private void Arrange(params NoteBase[] notes)
        {
            _logic.Session.MusicScore = new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes);
            _logic.Notes.Bind(notes);
            _logic.Session.NoteDisplayTimeOffset = 1f;
            _logic.Session.Views = Array.Empty<LiveViewBase>();
            _logic.OnUpdate(0f, INPUT_TIME);
        }

        private static LiveLogic.InputTmp Input(int fingerId, float lane, params NoteBase[] candidates)
        {
            var input = new LiveLogic.InputTmp
            {
                Lane = lane,
                Touch = new LiveTouch { fingerId = fingerId, touchId = fingerId, phase = InputTouchPhase.Began },
            };
            input.CandidateNotes.AddRange(candidates);
            return input;
        }

        [Test]
        public void ResolvedLaneOfTheHelperIsTheLaneAskedFor()
        {
            float lane = OpenSekai.Shared.Live.TouchLane.Resolve(LiveConfig.JudgmentPositions, At(4.2f), 0f);
            Assert.That(lane, Is.EqualTo(4.2f).Within(1e-3f), "The scenarios below place fingers by lane.");
        }

        [Test]
        public void SameFramePressesOnTwoNotesBothFingersReachEachTakeTheNearerOne()
        {
            var left = new CountingNote(1, 0f, 3, 2f, 6f);
            var right = new CountingNote(2, 0f, 5, 2f, 6f);
            Arrange(left, right);

            _source.Add(Press(1, 5f));
            _source.Add(Press(2, 3f));
            _logic.OnInput();

            Assert.That(left.JudgmentCalls, Is.EqualTo(1));
            Assert.That(right.JudgmentCalls, Is.EqualTo(1));
            Assert.That(left.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(right.Result, Is.EqualTo(NoteResult.Perfect));
        }

        /// <summary>
        /// Step 1 through the real loop. Finger 1 reaches an earlier key and a shared one and is nearer
        /// the shared one; finger 2 reaches only the shared key. Finger 2 keeps the key it alone can play,
        /// finger 1 plays its own, and both keys are hit whatever order the frame registered them in.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AFingerWhoseOnlyKeyIsSharedKeepsItAndTheOtherPlaysItsOwn(bool sharedNoteFirst)
        {
            var early = new CountingNote(1, 0f, 3, 2.5f, 4.5f);
            var shared = new CountingNote(2, 0.1f, 5, 3.5f, 6.5f);
            if (sharedNoteFirst) Arrange(shared, early);
            else Arrange(early, shared);

            _source.Add(Press(1, 4.2f)); // reaches both; 0.8 lanes from the shared note
            _source.Add(Press(2, 6f));   // reaches only the shared note; 1.0 lane from it
            _logic.OnInput();

            Assert.That(early.JudgmentCalls, Is.EqualTo(1));
            Assert.That(early.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(shared.JudgmentCalls, Is.EqualTo(1), "The finger that can play only this key gets it.");
            Assert.That(shared.Result, Is.Not.EqualTo(NoteResult.None));
        }

        [Test]
        public void SeveralBeganSamplesOfOneFingerShareOnePress()
        {
            var left = new CountingNote(1, 0f, 3, 2.5f, 3.5f);
            var right = new CountingNote(2, 0f, 6, 5.5f, 6.5f);
            Arrange(left, right);

            _source.Add(Press(1, 3f, fingerId: 7));
            _source.Add(Press(2, 6f, fingerId: 7));
            _logic.OnInput();

            Assert.That(left.JudgmentCalls + right.JudgmentCalls, Is.EqualTo(1));
            Assert.That(left.Result, Is.EqualTo(NoteResult.Perfect), "The first sample spends the credit.");
            Assert.That(right.Result, Is.EqualTo(NoteResult.None));
            Assert.That(_logic.TapCount, Is.EqualTo(2), "Both samples are still taps.");

            _source.Clear();
            _source.Add(Press(3, 6f, fingerId: 7));
            _logic.OnInput();
            Assert.That(right.Result, Is.EqualTo(NoteResult.Perfect), "The next frame has a credit of its own.");
        }

        [Test]
        public void AFingerThatPressesAgainInsideALateFrameJudgesBothNotesInOrder([Values(false, true)] bool laterPressNearer)
        {
            // A 100 ms hitch: the finger pressed at 0.00 s, lifted, and pressed again at 0.08 s; both Began
            // samples arrive in the frame at 0.10 s. Each is its own press and takes its own note, the
            // earlier press the earlier note, whichever sample sits nearer the lane centre.
            var first = new CountingNote(1, 0f, 3, 2.5f, 4.5f);
            var second = new CountingNote(2, 0.08f, 3, 2.5f, 4.5f);
            Arrange(first, second);
            _logic.OnUpdate(0.1f, INPUT_TIME + 0.1);

            _source.Add(PressAt(1, laterPressNearer ? 4.2f : 3.5f, INPUT_TIME, fingerId: 7));
            _source.Add(PressAt(2, laterPressNearer ? 3.5f : 4.2f, INPUT_TIME + 0.08, fingerId: 7));
            _logic.OnInput();

            Assert.That(first.Result, Is.EqualTo(NoteResult.Perfect), "The first press takes the first note on time.");
            Assert.That(second.Result, Is.EqualTo(NoteResult.Perfect), "The second press takes the second note on time.");
            Assert.That(first.JudgmentCalls + second.JudgmentCalls, Is.EqualTo(2));
        }

        [Test]
        public void TwoFingersPressingFramesApartInsideALateFrameAreNotAChord()
        {
            // 42 ms apart, as in the DEINOS replay at 160.2 s: a running game judges them seven frames
            // apart. The later finger sits nearer the earlier note's centre; as a chord it would take that
            // note late and leave the earlier finger the later note early.
            var early = new CountingNote(1, 0f, 5, 2.5f, 6.5f);
            var late = new CountingNote(2, 0.042f, 3, 2.5f, 6.5f);
            Arrange(early, late);
            _logic.OnUpdate(0.06f, INPUT_TIME + 0.06);

            _source.Add(PressAt(1, 3.2f, INPUT_TIME, fingerId: 0));
            _source.Add(PressAt(2, 5f, INPUT_TIME + 0.042, fingerId: 1));
            _logic.OnInput();

            Assert.That(early.Result, Is.EqualTo(NoteResult.Perfect), "The earlier press takes the earlier note.");
            Assert.That(late.Result, Is.EqualTo(NoteResult.Perfect), "The later press takes the later note.");
        }

        [Test]
        public void ANearChordIsMatchedOnlyUnderFrameMatching([Values(false, true)] bool frameMatching)
        {
            // Two fingers 3 ms apart on a chord. The first finger reaches both notes, the second only the
            // left one. Frame matching treats them as one moment and gives each finger a note it reaches;
            // serial judging lets the first press choose alone, as a running game does for presses a frame
            // apart, and the chart order hands it the left note.
            var left = new CountingNote(1, 0f, 3, 2.5f, 4.5f);
            var right = new CountingNote(2, 0f, 5, 3.5f, 6.5f);
            Arrange(left, right);
            bool previous = LiveConfig.UseFramePressMatching;
            try
            {
                LiveConfig.UseFramePressMatching = frameMatching;
                _logic.OnUpdate(0.01f, INPUT_TIME + 0.01);
                _source.Add(PressAt(1, 4f, INPUT_TIME, fingerId: 0));
                _source.Add(PressAt(2, 3f, INPUT_TIME + 0.003, fingerId: 1));
                _logic.OnInput();
            }
            finally
            {
                LiveConfig.UseFramePressMatching = previous;
            }

            Assert.That(left.Result, Is.Not.EqualTo(NoteResult.None));
            if (frameMatching)
                Assert.That(right.Result, Is.Not.EqualTo(NoteResult.None), "Matched as a chord, each finger plays a note.");
            else
                Assert.That(right.Result, Is.EqualTo(NoteResult.None), "Judged in order, the first press took the left note.");
        }

        [Test]
        public void PressPassesSplitEveryPressWhenSerialAndOnlyFrameGapsUnderFrameMatching()
        {
            LiveLogic.InputTmp At(int finger, float time, InputTouchPhase phase = InputTouchPhase.Began) => new LiveLogic.InputTmp
            {
                Touch = new LiveTouch { fingerId = finger, touchId = finger * 10 + (int)(time * 1000f), phase = phase, musicTime = time },
            };

            var inputs = new List<LiveLogic.InputTmp>
            {
                At(1, 0.000f),                        // chord with finger 2
                At(2, 0.000f),
                At(3, 0.005f),                        // 5 ms later
                At(1, 0.008f),                        // finger 1 again within 1/60 s: the same contact
                At(4, 0.040f),                        // a late frame's later press
                At(5, 0.020f, InputTouchPhase.Moved), // not a press
            };

            int serialPasses = JudgmentScheduler.AssignPressWaves(inputs, 0f);
            Assert.That(inputs.Select(i => i.PressWave), Is.EqualTo(new[] { 0, 1, 2, 0, 3, 0 }), "serial: one press per pass, ties in input order");
            Assert.That(serialPasses, Is.EqualTo(4));

            int framePasses = JudgmentScheduler.AssignPressWaves(inputs, JudgmentScheduler.PRESS_PASS_SECONDS);
            Assert.That(inputs.Select(i => i.PressWave), Is.EqualTo(new[] { 0, 0, 0, 0, 1, 0 }), "frame matching: one pass per 60 fps frame");
            Assert.That(framePasses, Is.EqualTo(2));
        }

        [Test]
        public void TwoBeganSamplesOfOneFingerInsideOneFrameTimeStillShareOnePress()
        {
            // Closer than a 60 fps frame is one moment: one contact reported twice, not a lift and a press again.
            var left = new CountingNote(1, 0f, 3, 2.5f, 3.5f);
            var right = new CountingNote(2, 0.01f, 6, 5.5f, 6.5f);
            Arrange(left, right);
            _logic.OnUpdate(0.02f, INPUT_TIME + 0.02);

            _source.Add(PressAt(1, 3f, INPUT_TIME, fingerId: 7));
            _source.Add(PressAt(2, 6f, INPUT_TIME + 0.01, fingerId: 7));
            _logic.OnInput();

            Assert.That(left.JudgmentCalls + right.JudgmentCalls, Is.EqualTo(1));
            Assert.That(left.Result, Is.EqualTo(NoteResult.Perfect), "The first sample spends the credit.");
            Assert.That(right.Result, Is.EqualTo(NoteResult.None));
        }

        [Test]
        public void CollectingSpendsNothingAndKeepsEveryReachableCandidate()
        {
            var first = new CountingNote(1, 0f, 4, 3f, 5f);
            var second = new CountingNote(2, 0.1f, 4, 3f, 5f);
            Arrange(first, second);
            Assert.That(second.CalcNoteResult(0f), Is.LessThanOrEqualTo(NoteResult.Great).And.Not.EqualTo(NoteResult.None),
                "Precondition: the old gate would have kept this note out after the first was collected.");

            var notes = new List<NoteBase> { first, second };
            var input = Input(1, 4f);
            input.Touch.musicTime = 0f;
            var pressed = new Dictionary<int, double>();
            new LiveCandidatePipeline().Collect(notes, new List<LiveLogic.InputTmp> { input }, null, pressed);

            Assert.That(input.CandidateNotes, Is.EqualTo(new NoteBase[] { first, second }),
                "A candidate that was not hit cannot keep a later one out.");
            Assert.That(pressed, Is.Empty, "Only a consumed press is recorded.");
        }

        /// <summary>
        /// A touch a note already consumed is offered another note only when that note would grade better
        /// than Great, or a sixth of a second has passed since the consumption.
        /// </summary>
        [TestCase(0.1f, 0d, false)]
        [TestCase(0.1f, -0.2d, true)]
        [TestCase(0f, 0d, true)]
        public void ATouchWhosePressWasConsumedIsOfferedAgainOnlyForABetterThanGreatNote(
            float noteTime, double consumedAt, bool offered)
        {
            var note = new CountingNote(1, noteTime, 4, 3f, 5f);
            Arrange(note);
            var input = Input(1, 4f);
            input.Touch.musicTime = 0f;
            var pressed = new Dictionary<int, double> { [1] = consumedAt };
            new LiveCandidatePipeline().Collect(new List<NoteBase> { note }, new List<LiveLogic.InputTmp> { input }, null, pressed);
            Assert.That(input.CandidateNotes.Count, Is.EqualTo(offered ? 1 : 0), $"grade={note.CalcNoteResult(0f)}");
        }

        [Test]
        public void ASharedNoteGoesToTheNearerFinger()
        {
            var note = new CountingNote(1, 0f, 4, 2f, 6f);
            var far = Input(1, 5.5f, note);
            var near = Input(2, 3.5f, note);
            new LiveCandidatePipeline().SortPressCandidates(new List<LiveLogic.InputTmp> { far, near }, Info(0f));
            Assert.That(near.CandidateNotes, Is.EqualTo(new NoteBase[] { note }));
            Assert.That(far.CandidateNotes, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AtTheSameDistanceASharedNoteGoesToTheSmallerFingerId(bool smallerListedFirst)
        {
            var note = new CountingNote(1, 0f, 4, 2f, 6f);
            var smaller = Input(1, 3f, note);
            var larger = Input(2, 5f, note);
            var inputs = smallerListedFirst
                ? new List<LiveLogic.InputTmp> { smaller, larger }
                : new List<LiveLogic.InputTmp> { larger, smaller };
            new LiveCandidatePipeline().SortPressCandidates(inputs, Info(0f));
            Assert.That(smaller.CandidateNotes, Is.EqualTo(new NoteBase[] { note }));
            Assert.That(larger.CandidateNotes, Is.Empty);
        }

        [Test]
        public void OneFingerTakesTheEarlierChartTime()
        {
            var later = new CountingNote(1, 0.02f, 4, 3f, 5f) { ChartOrder = 0 };
            var earlier = new CountingNote(2, 0f, 4, 3f, 5f) { ChartOrder = 1 };
            var input = Input(1, 4f, later, earlier);
            new LiveCandidatePipeline().SortPressCandidates(new List<LiveLogic.InputTmp> { input }, Info(0f));
            Assert.That(input.CandidateNotes, Is.EqualTo(new NoteBase[] { earlier }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AtTheSameTimeOneFingerTakesTheNoteFirstInTheChart(bool collectedInChartOrder)
        {
            var first = new CountingNote(1, 0f, 4, 3f, 5f);
            var second = new CountingNote(2, 0f, 4, 3f, 5f);
            _logic.Notes.Bind(new NoteBase[] { first, second });
            Assert.That(first.ChartOrder, Is.LessThan(second.ChartOrder), "Bind numbers the chart.");

            var input = collectedInChartOrder ? Input(1, 4f, first, second) : Input(1, 4f, second, first);
            new LiveCandidatePipeline().SortPressCandidates(new List<LiveLogic.InputTmp> { input }, Info(0f));
            Assert.That(input.CandidateNotes, Is.EqualTo(new NoteBase[] { first }));
        }

        [Test]
        public void AHeadIsOrderedWhereItsBarSitsInTheChart()
        {
            var tap = new NormalNote(Info(0f), 1, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f);
            var bar = new LongNote(Info(0f), 2, 4, 4, NoteCategory.Long, NoteType.Default, 1f, 0f);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(1f), 3, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            _logic.Notes.Bind(new NoteBase[] { bar, tap });
            Assert.That(bar.Head.Candidate.ChartOrder, Is.EqualTo(bar.ChartOrder));
            Assert.That(LiveCandidatePipeline.PrecedesForOneFinger(bar.Head.Candidate, tap), Is.True);
            Assert.That(LiveCandidatePipeline.PrecedesForOneFinger(tap, bar.Head.Candidate), Is.False);
        }

        private static void Sort(params LiveLogic.InputTmp[] inputs) =>
            new LiveCandidatePipeline().SortPressCandidates(new List<LiveLogic.InputTmp>(inputs), Info(0f));

        [Test]
        public void StepOneCascadesUntilNothingChanges()
        {
            var k1 = new CountingNote(1, 0f, 3, 0f, 11f);
            var k2 = new CountingNote(2, 0f, 5, 0f, 11f);
            var k3 = new CountingNote(3, 0f, 7, 0f, 11f);
            var a = Input(1, 3f, k1);
            var b = Input(2, 3.1f, k1, k2);
            var c = Input(3, 5.1f, k2, k3);
            LiveCandidatePipeline.KeepSoleOptions(new List<LiveLogic.InputTmp> { a, b, c });
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { k1 }));
            Assert.That(b.CandidateNotes, Is.EqualTo(new NoteBase[] { k2 }), "b loses k1 to a, then keeps k2 alone");
            Assert.That(c.CandidateNotes, Is.EqualTo(new NoteBase[] { k3 }), "so c loses k2 to b");
        }

        [Test]
        public void StepOneDoesNotEmptyAFingerWhenBothHaveOnlyTheSameKey()
        {
            var key = new CountingNote(1, 0f, 4, 0f, 11f);
            var a = Input(1, 3f, key);
            var b = Input(2, 5.5f, key);
            LiveCandidatePipeline.KeepSoleOptions(new List<LiveLogic.InputTmp> { a, b });
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { key }));
            Assert.That(b.CandidateNotes, Is.EqualTo(new NoteBase[] { key }));

            Sort(a, b);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { key }), "step 3 then gives it to the nearer finger");
            Assert.That(b.CandidateNotes, Is.Empty);
        }

        /// <summary>
        /// Step 2. Finger a is nearer both keys, so distance alone would give a both and leave b with
        /// nothing. A pair with the same two keys splits them in chart order instead: the earlier key to
        /// the nearer finger, the later key to the other.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TwoFingersWithTheSameTwoKeysSplitThemInChartOrder(bool listedLaterFirst)
        {
            var earlier = new CountingNote(1, 0f, 3, 0f, 11f);
            var later = new CountingNote(2, 0.02f, 4, 0f, 11f);
            var a = listedLaterFirst ? Input(1, 3.4f, later, earlier) : Input(1, 3.4f, earlier, later);
            var b = Input(2, 5.5f, earlier, later);
            Sort(a, b);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { earlier }));
            Assert.That(b.CandidateNotes, Is.EqualTo(new NoteBase[] { later }));
        }

        [Test]
        public void ThreeFingersWithTheSameThreeKeysAreLeftToDistance()
        {
            var k1 = new CountingNote(1, 0f, 3, 0f, 11f);
            var k2 = new CountingNote(2, 0.02f, 4, 0f, 11f);
            var k3 = new CountingNote(3, 0.04f, 5, 0f, 11f);
            var a = Input(1, 3.9f, k1, k2, k3);
            var b = Input(2, 8f, k1, k2, k3);
            var c = Input(3, 9f, k1, k2, k3);
            Sort(a, b, c);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { k1 }), "a is nearest all three and plays the earliest");
            Assert.That(b.CandidateNotes, Is.Empty);
            Assert.That(c.CandidateNotes, Is.Empty);
        }

        [Test]
        public void TwoFingersWithTheSameThreeKeysAreLeftToDistance()
        {
            var k1 = new CountingNote(1, 0f, 3, 0f, 11f);
            var k2 = new CountingNote(2, 0.02f, 4, 0f, 11f);
            var k3 = new CountingNote(3, 0.04f, 5, 0f, 11f);
            var a = Input(1, 3.9f, k1, k2, k3);
            var b = Input(2, 8f, k1, k2, k3);
            Sort(a, b);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { k1 }));
            Assert.That(b.CandidateNotes, Is.Empty);
        }

        [Test]
        public void AFingerOutsideThePairLosesAKeyThePairSettled()
        {
            var k1 = new CountingNote(1, 0f, 3, 0f, 11f);
            var k2 = new CountingNote(2, 0.02f, 4, 0f, 11f);
            var k3 = new CountingNote(3, 0.04f, 9, 0f, 11f);
            var a = Input(1, 3.4f, k1, k2);
            var b = Input(2, 5.5f, k1, k2);
            var outsider = Input(3, 4f, k2, k3); // nearest k2 of all three
            Sort(a, b, outsider);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { k1 }));
            Assert.That(b.CandidateNotes, Is.EqualTo(new NoteBase[] { k2 }));
            Assert.That(outsider.CandidateNotes, Is.EqualTo(new NoteBase[] { k3 }), "k2 is assigned to the pair");
        }

        [Test]
        public void APairsKeysLeaveEveryOtherFingerEvenWhenThatEmptiesIt()
        {
            var k1 = new CountingNote(1, 0f, 3, 0f, 11f);
            var k2 = new CountingNote(2, 0.02f, 4, 0f, 11f);
            var k3 = new CountingNote(3, 0.04f, 9, 0f, 11f);
            var a = Input(1, 3.4f, k1, k2);
            var b = Input(2, 5.5f, k1, k2);
            var wide = Input(3, 4f, k1, k2, k3);
            var onlyK2 = Input(4, 4f, k2);
            var frame = Info(0f);
            new LiveCandidatePipeline().AssignMatchedPairs(new List<LiveLogic.InputTmp> { a, b, wide, onlyK2 }, ref frame);
            Assert.That(a.CandidateNotes, Is.EqualTo(new NoteBase[] { k1 }));
            Assert.That(b.CandidateNotes, Is.EqualTo(new NoteBase[] { k2 }));
            Assert.That(wide.CandidateNotes, Is.EqualTo(new NoteBase[] { k3 }), "both of the pair's keys go");
            Assert.That(onlyK2.CandidateNotes, Is.Empty, "step 2 does not ask whether a finger has anything left");
        }

        /// <summary>Step 4. Grades are read at the touch time, 0 s.</summary>
        [TestCase(-0.11f, 0.0f, 1, TestName = "AnEarliestGoodYieldsToALaterGreatOrBetter")]
        [TestCase(-0.11f, 0.11f, 0, TestName = "AnEarliestGoodStaysWhenTheLaterIsNoBetterThanGood")]
        [TestCase(-0.05f, 0.0f, 0, TestName = "AnEarliestGreatOrBetterStaysEvenWhenTheLaterIsBetter")]
        public void OneFingerYieldsALowGradeKeyOnlyToAGreatOrBetterOne(float earlyTime, float laterTime, int picked)
        {
            var early = new CountingNote(1, earlyTime, 4, 3f, 5f);
            var later = new CountingNote(2, laterTime, 4, 3f, 5f);
            NoteResult earlyGrade = early.CalcNoteResult(0f);
            NoteResult laterGrade = later.CalcNoteResult(0f);
            string grades = $"early={earlyGrade} later={laterGrade}";
            if (picked == 1 || laterTime > 0.1f)
                Assert.That(earlyGrade, Is.LessThan(NoteResult.Great).And.GreaterThan(NoteResult.None), "Precondition. " + grades);
            else
                Assert.That(earlyGrade, Is.GreaterThanOrEqualTo(NoteResult.Great), "Precondition. " + grades);
            if (laterTime > 0.1f)
                Assert.That(laterGrade, Is.LessThan(NoteResult.Great).And.GreaterThan(NoteResult.None), "Precondition. " + grades);

            var input = Input(1, 4f, later, early);
            Sort(input);
            Assert.That(input.CandidateNotes, Is.EqualTo(new NoteBase[] { picked == 0 ? early : later }), grades);
        }

        [Test]
        public void ThroughTheLoopALatePressOnAnOldKeyPlaysTheNextOneInstead()
        {
            var old = new CountingNote(1, 0f, 4, 3f, 5f);
            var next = new CountingNote(2, 0.12f, 4, 3f, 5f);
            Arrange(old, next);
            _logic.OnUpdate(0.11f, INPUT_TIME + 0.11d);
            Assert.That(old.CalcNoteResult(0.11f), Is.LessThan(NoteResult.Great).And.GreaterThan(NoteResult.None), "Precondition.");

            _source.Add(new LiveTouchSample(1, new Vector2(800f, 600f), At(4f), INPUT_TIME + 0.11d,
                InputTouchPhase.Began, Vector2.zero));
            _logic.OnInput();

            Assert.That(next.JudgmentCalls, Is.EqualTo(1));
            Assert.That(next.Result, Is.GreaterThanOrEqualTo(NoteResult.Great));
            Assert.That(old.JudgmentCalls, Is.Zero);
        }

        [Test]
        public void AHeadCandidateGradesWithItsBarsStartWindow()
        {
            var bar = new LongNote(Info(0f), 2, 4, 4, NoteCategory.Long, NoteType.Default, 1f, 0f);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(1f), 3, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            foreach (float t in new[] { -0.1f, -0.05f, 0f, 0.05f, 0.1f })
            {
                Assert.That(bar.Head.Candidate.CalcNoteResult(t), Is.EqualTo(bar.CalcNoteResult(t)));
            }

            Assert.That(bar.Head.Candidate.CalcNoteResult(0f), Is.EqualTo(NoteResult.Perfect));
        }
    }
}
