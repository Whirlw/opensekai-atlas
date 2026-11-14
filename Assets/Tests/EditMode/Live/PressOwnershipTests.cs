using System;
using System.Collections.Generic;
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
        private const double InputTime = 10d;
        private FakeTouchSource source;
        private LiveLogic logic;
        private Mouse isolatedMouse;
        private Mouse previousMouse;
        private bool previousLegacyTiming;
        private MusicScoreInfo previousFrame;
        private LiveConfig.NoteTypeJudgeData previousJudgeData;

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
            previousLegacyTiming = LiveConfig.UseLegacyJudgeTiming;
            previousFrame = MusicScore.CurrentFrameInfo;
            previousJudgeData = LiveConfig.noteTypeJudgeData;
            LiveConfig.LoadMasterConfig();
            LiveConfig.UseLegacyJudgeTiming = false;
            previousMouse = Mouse.current;
            isolatedMouse = InputSystem.AddDevice<Mouse>();
            source = new FakeTouchSource();
            logic = new LiveLogic(null);
            logic.SetTouchSource(source);
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(isolatedMouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            LiveConfig.UseLegacyJudgeTiming = previousLegacyTiming;
            LiveConfig.noteTypeJudgeData = previousJudgeData;
            MusicScore.CurrentFrameInfo = previousFrame;
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
            return new LiveTouchSample(touchId, new Vector2(800f, 600f), At(lane), InputTime,
                InputTouchPhase.Began, Vector2.zero, fingerId);
        }

        private void Arrange(params NoteBase[] notes)
        {
            logic.Session.MusicScore = new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes);
            logic.Notes.Bind(notes);
            logic.Session.NoteDisplayTimeOffset = 1f;
            logic.Session.Views = Array.Empty<LiveViewBase>();
            logic.OnUpdate(0f, InputTime);
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

            source.Add(Press(1, 5f));
            source.Add(Press(2, 3f));
            logic.OnInput();

            Assert.That(left.JudgmentCalls, Is.EqualTo(1));
            Assert.That(right.JudgmentCalls, Is.EqualTo(1));
            Assert.That(left.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(right.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        /// <summary>
        /// Two fingers compete for one note, and the finger that wins it can also reach an earlier note.
        /// The winner spends its press on the earlier note and the loser is not offered the shared one in
        /// its place: this pins the single-slot policy. Re-pairing the loser is a separate decision.
        ///
        /// The shared note grades worse than Great at the press, which is what made the old outcome
        /// depend on the order the frame registered the notes in: when the press was spent at
        /// collection, registering the earlier note first kept the shared one out of the winner's list
        /// and both notes were hit, and registering it second left the shared note unhit. Now the
        /// outcome is the same for both orders.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TheFingerThatWinsASharedNoteSpendsItsPressOnItsEarlierNote(bool sharedNoteFirst)
        {
            var early = new CountingNote(1, 0f, 3, 2.5f, 4.5f);
            var shared = new CountingNote(2, 0.1f, 5, 3.5f, 6.5f);
            if (sharedNoteFirst) Arrange(shared, early);
            else Arrange(early, shared);
            Assert.That(shared.CalcNoteResult(0f), Is.LessThanOrEqualTo(NoteResult.Great).And.Not.EqualTo(NoteResult.None),
                "Precondition: the shared note is in its window and grades Great or worse.");

            source.Add(Press(1, 4.2f)); // reaches both; 0.8 lanes from the shared note
            source.Add(Press(2, 6f));   // reaches only the shared note; 1.0 lane from it
            logic.OnInput();

            Assert.That(early.JudgmentCalls, Is.EqualTo(1));
            Assert.That(early.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(shared.JudgmentCalls, Is.Zero, "The losing finger is not re-paired.");
            Assert.That(shared.Result, Is.EqualTo(NoteResult.None));
        }

        [Test]
        public void SeveralBeganSamplesOfOneFingerShareOnePress()
        {
            var left = new CountingNote(1, 0f, 3, 2.5f, 3.5f);
            var right = new CountingNote(2, 0f, 6, 5.5f, 6.5f);
            Arrange(left, right);

            source.Add(Press(1, 3f, fingerId: 7));
            source.Add(Press(2, 6f, fingerId: 7));
            logic.OnInput();

            Assert.That(left.JudgmentCalls + right.JudgmentCalls, Is.EqualTo(1));
            Assert.That(left.Result, Is.EqualTo(NoteResult.JustPerfect), "The first sample spends the credit.");
            Assert.That(right.Result, Is.EqualTo(NoteResult.None));
            Assert.That(logic.TapCount, Is.EqualTo(2), "Both samples are still taps.");

            source.Clear();
            source.Add(Press(3, 6f, fingerId: 7));
            logic.OnInput();
            Assert.That(right.Result, Is.EqualTo(NoteResult.JustPerfect), "The next frame has a credit of its own.");
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
            logic.Notes.Bind(new NoteBase[] { first, second });
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
            logic.Notes.Bind(new NoteBase[] { bar, tap });
            Assert.That(bar.Head.Candidate.ChartOrder, Is.EqualTo(bar.ChartOrder));
            Assert.That(LiveCandidatePipeline.PrecedesForOneFinger(bar.Head.Candidate, tap), Is.True);
            Assert.That(LiveCandidatePipeline.PrecedesForOneFinger(tap, bar.Head.Candidate), Is.False);
        }
    }
}
