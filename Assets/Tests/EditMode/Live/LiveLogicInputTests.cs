using System;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class LiveLogicInputTests
    {
        private const double InputTime = 10d;
        private const int Lane = 4;
        private FakeTouchSource source;
        private LiveLogic logic;
        private Mouse isolatedMouse;
        private Mouse previousMouse;
        private bool previousLegacyTiming;
        private MusicScoreInfo previousFrame;
        private LiveConfig.NoteTypeJudgeData previousJudgeData;

        private sealed class CountingNormalNote : NormalNote
        {
            internal int JudgmentCalls;

            internal CountingNormalNote(int id, float time, int lane)
                : base(Info(time), id, lane, lane, NoteCategory.Normal, 1f, NoteType.Default, 0f) { }

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
            // A fresh, idle current mouse keeps the editor fallback independent of physical input.
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

        private static Vector3 Position(int lane = Lane) => LiveConfig.JudgmentPositions[lane];

        [TestCase(60, false, 5760, 9600)]
        [TestCase(165, false, 5760, 9600)]
        [TestCase(60, true, 5760, 9600)]
        [TestCase(165, true, 5760, 9600)]
        [TestCase(60, false, 9600, 13440)]
        [TestCase(165, false, 9600, 13440)]
        [TestCase(60, true, 9600, 13440)]
        [TestCase(165, true, 9600, 13440)]
        [TestCase(165, true, 9600, 21120)]
        [TestCase(165, true, 9600, 24960)]
        [TestCase(165, true, 26400, 28800)]
        public void AuthoredCrossingAllowsFollowingOrExchangingFingers(int fps, bool keepSides, int startTicks, int otherTicks)
        {
            string path = "Tools/fixtures/hold-overlap-regression/score.json";
            var config = UnityEngine.Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            var document = OpenSekai.Charts.Serialization.ChartJson.Read(System.IO.File.ReadAllText(path));
            var score = OpenSekai.Live.Conversion.RuntimeChartConverter.Convert(document, config);
            float firstTime = OpenSekai.Charts.ChartTimeline.GetTimeFromTicks(startTicks, score.musicScoreInfoArray);
            float secondTime = OpenSekai.Charts.ChartTimeline.GetTimeFromTicks(otherTicks, score.musicScoreInfoArray);
            var notes = score.NoteArray.OfType<LongNote>().Where(n => Mathf.Approximately(n.MusicScoreInfo.time, firstTime)
                || Mathf.Approximately(n.MusicScoreInfo.time, secondTime)).OrderBy(n => n.MusicScoreInfo.time).ToArray();
            Assert.That(notes.Length, Is.EqualTo(2));
            notes[0].Id = startTicks;
            notes[1].Id = otherTicks;
            SetField("_musicScore", score);
            SetField("_noteArray", notes);
            SetField("_noteDisplayTimeOffset", 1f);
            SetField("_liveViews", Array.Empty<LiveViewBase>());
            bool[] begun = new bool[2];
            float start = notes[0].MusicScoreInfo.time;
            float end = notes.Min(n => n.ChildNote.MusicScoreInfo.time);
            for (int frame = 0; start + frame / (float)fps < end; frame++)
            {
                float time = start + frame / (float)fps;
                foreach (var n in notes)
                    typeof(LongNote).GetField("lastInputTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(n, time - 1f);
                logic.OnUpdate(time, InputTime + time);
                source.Clear();
                for (int i = 0; i < notes.Length; i++)
                {
                    var n = notes[i];
                    if (time < n.MusicScoreInfo.time) continue;
                    INote from = n;
                    INote to = n.ChildNote;
                    foreach (var v in n.ViewNoteList.Skip(1))
                    {
                        if (v.IsSkip) continue;
                        if (v.MusicScoreInfo.time >= time) { to = v; break; }
                        from = v;
                    }
                    float rate = LiveConfig.GetNoteLineParentProgress(time, from, to);
                    float lane = Mathf.Lerp((from.DefaultLeftLane + from.DefaultRightLane) * 0.5f,
                        (to.DefaultLeftLane + to.DefaultRightLane) * 0.5f, rate);
                    int laneIndex = Mathf.Min(10, Mathf.FloorToInt(lane));
                    Vector3 world = Vector3.Lerp(Position(laneIndex), Position(laneIndex + 1), lane - laneIndex);
                    int finger = i + 1;
                    if (keepSides && time < notes[1].MusicScoreInfo.time) finger = lane < 5.5f ? 1 : 2;
                    if (keepSides && time >= notes[1].MusicScoreInfo.time)
                    {
                        var other = notes[1 - i];
                        INote otherFrom = other;
                        INote otherTo = other.ChildNote;
                        foreach (var v in other.ViewNoteList.Skip(1))
                        {
                            if (v.IsSkip) continue;
                            if (v.MusicScoreInfo.time >= time) { otherTo = v; break; }
                            otherFrom = v;
                        }
                        float otherRate = LiveConfig.GetNoteLineParentProgress(time, otherFrom, otherTo);
                        float otherLane = Mathf.Lerp((otherFrom.DefaultLeftLane + otherFrom.DefaultRightLane) * 0.5f,
                            (otherTo.DefaultLeftLane + otherTo.DefaultRightLane) * 0.5f, otherRate);
                        finger = lane < otherLane || (lane == otherLane && i == 0) ? 1 : 2;
                    }
                    source.Add(Touch(finger, world, InputTime + time, begun[i] ? InputTouchPhase.Moved : InputTouchPhase.Began));
                    begun[i] = true;
                }
                logic.OnInput();
                for (int i = 0; i < notes.Length; i++)
                    if (begun[i])
                    {
                        Assert.That(notes[i].State, Is.EqualTo(NoteState.InputBegan).Or.EqualTo(NoteState.Input),
                            $"frame={frame} time={time} note={notes[i].Id} lane={notes[i].LaneStartF}..{notes[i].LaneEndF} holder={notes[i].HoldingTouchId}");
                        foreach (var child in notes[i].NoteList)
                            if (child is LongHoldCombo && child.MusicScoreInfo.time < time)
                                Assert.That(child.Result, Is.EqualTo(NoteResult.JustPerfect), $"frame={frame} time={time} tick={child.MusicScoreInfo.time}");
                    }
            }
        }

        private static LiveTouchSample Touch(int id, Vector3 position, double time = InputTime,
            InputTouchPhase phase = InputTouchPhase.Began)
        {
            // Deliberately different: judging must use supplied world coordinates, not screen coordinates.
            return new LiveTouchSample(id, new Vector2(800f, 600f), position, time, phase, new Vector2(3f, 4f));
        }

        private void SetField(string name, object value)
        {
            typeof(LiveLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(logic, value);
        }

        private CountingNormalNote Arrange(float frameTime = 0f, params CountingNormalNote[] additionalNotes)
        {
            var note = new CountingNormalNote(1, 0f, Lane);
            var notes = new NoteBase[additionalNotes.Length + 1];
            notes[0] = note;
            Array.Copy(additionalNotes, 0, notes, 1, additionalNotes.Length);
            // Supply only the chart and empty views, avoiding Setup's storage, camera and asset loading.
            // Candidate construction and all judgment stages still run through real OnUpdate / OnInput.
            SetField("_musicScore", new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes));
            SetField("_noteArray", notes);
            SetField("_noteDisplayTimeOffset", 1f);
            SetField("_liveViews", Array.Empty<LiveViewBase>());
            logic.OnUpdate(frameTime, InputTime);
            return note;
        }

        private void AssertRejectedThenAcceptFreshTouch(CountingNormalNote note)
        {
            NoteState before = note.State;
            logic.OnInput();
            Assert.That(note.State, Is.EqualTo(before));
            Assert.That(note.Result, Is.EqualTo(NoteResult.None));
            Assert.That(note.JudgmentCalls, Is.Zero);
            source.Clear();
            source.Add(Touch(2, Position()));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect), "In-lane control must traverse the judge table.");
            Assert.That(note.JudgmentCalls, Is.EqualTo(1));
        }

        [Test]
        public void OnTimeTouchJudgesJustPerfect()
        {
            var note = Arrange();
            source.Add(Touch(1, Position()));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(note.State, Is.EqualTo(NoteState.Done));
            Assert.That(note.JudgmentCalls, Is.EqualTo(1));
        }

        [TestCase(NoteCategory.Long, false)]
        [TestCase(NoteCategory.FrictionLong, false)]
        [TestCase(NoteCategory.FrictionHideLong, false)]
        [TestCase(NoteCategory.Long, true)]
        [TestCase(NoteCategory.FrictionLong, true)]
        [TestCase(NoteCategory.FrictionHideLong, true)]
        public void ContinuousInputKeepsBothMovingHoldsThroughOverlap(NoteCategory category, bool nested)
        {
            var notes = new LongNote[2];
            for (int i = 0; i < 2; i++)
            {
                int left = i == 0 ? 2 : 6;
                int right = left + (nested ? 1 : 2);
                int endLeft = nested ? (i == 0 ? 2 : 4) : (i == 0 ? 6 : 2);
                int endRight = nested ? (i == 0 ? 8 : 5) : endLeft + 2;
                notes[i] = category == NoteCategory.Long
                    ? new LongNote(Info(0), i, left, right, category, NoteType.Default, 1, 0)
                    : category == NoteCategory.FrictionLong
                        ? new FrictionLongNote(Info(0), i, left, right, category, NoteType.Default, null, 1)
                        : new FrictionHideLongNote(Info(0), i, left, right, category, NoteType.Default, null, 1);
                for (int tick = 1; tick < 24; tick++)
                    notes[i].AddHoldCombo(new LongHoldCombo(Info(tick * 0.125f), NoteType.Default, 1, 0));
                notes[i].AddNoteListAndSetupChildNote(new NormalNote(Info(3), 100 + i, endLeft, endRight, NoteCategory.Normal, 1, NoteType.Default, 0));
                notes[i].LineType = i == 0 ? NoteLineType.EaseIn : NoteLineType.EaseOut;
            }
            SetField("_musicScore", new MusicScore(new[] { Info(0) }, Array.Empty<EventBase>(), notes));
            SetField("_noteArray", notes);
            SetField("_noteDisplayTimeOffset", 1f);
            SetField("_liveViews", Array.Empty<LiveViewBase>());
            for (int frame = 0; frame < 180; frame++)
            {
                // EditMode executes these simulated frames in one engine frame. Age the last input
                // explicitly so the real Excute path sees the previous-frame MissMesh boundary.
                if (frame > 0)
                    foreach (var note in notes)
                        typeof(LongNote).GetField("lastInputTime", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(note, frame / 60f - 1f);
                float time = frame / 60f;
                logic.OnUpdate(time, InputTime + time);
                source.Clear();
                for (int i = 0; i < 2; i++)
                {
                    // Follow the rendered center, calculated independently from the mutable judge lanes.
                    float rate = LiveConfig.GetNoteLineCenterProgress(time / 3f, notes[i]);
                    float startCenter = (notes[i].DefaultLeftLane + notes[i].DefaultRightLane) * 0.5f;
                    float endCenter = (notes[i].ChildNote.DefaultLeftLane + notes[i].ChildNote.DefaultRightLane) * 0.5f;
                    float lane = Mathf.Lerp(startCenter, endCenter, rate);
                    int laneIndex = Mathf.FloorToInt(lane);
                    Vector3 world = Vector3.Lerp(Position(laneIndex), Position(laneIndex + 1), lane - laneIndex);
                    source.Add(Touch(i + 1, world, InputTime + time,
                        frame == 0 ? InputTouchPhase.Began : InputTouchPhase.Moved));
                }
                logic.OnInput();
                foreach (var note in notes)
                {
                    // Trace heads finalize their first touch in the following Excute call.
                    Assert.That(note.HoldingTouchId, Is.EqualTo(note.Id + 1), $"frame={frame} note={note.Id}");
                    if (frame > 0)
                        Assert.That(note.State, Is.EqualTo(NoteState.InputBegan).Or.EqualTo(NoteState.Input), $"frame={frame} note={note.Id}");
                    foreach (var child in note.NoteList)
                        if (child is LongHoldCombo && child.MusicScoreInfo.time < time)
                            Assert.That(child.Result, Is.EqualTo(NoteResult.JustPerfect), $"frame={frame} note={note.Id} tick={child.MusicScoreInfo.time}");
                }
            }
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void JustPerfectHalfBoundaryIsInclusive(int side)
        {
            var note = Arrange(side * LiveConfig.JustPerfectJudgeTimeHalf);
            source.Add(Touch(1, Position()));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(note.Description, Is.EqualTo(NoteResultDescription.Just));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void JustOutsideJustPerfectHalfJudgesPerfect(int side)
        {
            var note = Arrange(side * (LiveConfig.JustPerfectJudgeTimeHalf + 0.0001f));
            source.Add(Touch(1, Position()));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(note.Description, Is.EqualTo(side < 0 ? NoteResultDescription.Fast : NoteResultDescription.Late));
        }

        [Test]
        public void TouchThreeTenthsLateFallsOutsideWindowAndMisses()
        {
            var note = Arrange(0.3f);
            source.Add(Touch(1, Position()));
            logic.OnInput();
            Assert.That(note.CalcNoteResult(0.3f), Is.EqualTo(NoteResult.Miss));
            Assert.That(note.Result, Is.EqualTo(NoteResult.None), "Out-of-window touch is not accepted.");
            Assert.That(note.JudgmentCalls, Is.Zero);
            logic.OnUpdate(0.3f, InputTime);
            Assert.That(note.Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(note.State, Is.EqualTo(NoteState.Done));
        }

        [Test]
        public void TouchAboveJudgmentToleranceLeavesNoteUnchanged()
        {
            var note = Arrange();
            source.Add(Touch(1, Position() + Vector3.up));
            AssertRejectedThenAcceptFreshTouch(note);
        }

        [TestCase(0, -1)]
        [TestCase(11, 1)]
        public void TouchOutsideOuterLaneLeavesNoteUnchanged(int edgeLane, int direction)
        {
            var note = Arrange();
            // Make the target the edge lane as well: rejection cannot be explained by a different lane.
            note.LaneStart = note.LaneEnd = edgeLane;
            note.LaneStartF = note.LaneEndF = edgeLane;
            note.JudgeLaneStart = note.JudgeLaneEnd = edgeLane;
            source.Add(Touch(1, Position(edgeLane) + Vector3.right * direction));
            var before = note.State;
            logic.OnInput();
            Assert.That(note.State, Is.EqualTo(before));
            Assert.That(note.Result, Is.EqualTo(NoteResult.None));
            Assert.That(note.JudgmentCalls, Is.Zero);
            source.Clear();
            source.Add(Touch(2, Position(edgeLane)));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        [Test]
        public void HeldTouchDoesNotPressASecondNote()
        {
            var next = new CountingNormalNote(2, 0.02f, Lane);
            var first = Arrange(0f, next);
            source.Add(Touch(1, Position()));
            logic.OnInput();
            Assert.That(first.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(next.Result, Is.EqualTo(NoteResult.None));

            logic.OnUpdate(0.02f, InputTime);
            logic.OnInput(); // same Began sample/time must become Moved
            source.Clear();
            source.Add(Touch(1, Position(), InputTime + 0.02d, InputTouchPhase.Stationary));
            logic.OnInput();
            Assert.That(logic.TapCount, Is.EqualTo(1));
            Assert.That(next.Result, Is.EqualTo(NoteResult.None));
            Assert.That(next.JudgmentCalls, Is.Zero);

            source.Clear();
            source.Add(Touch(2, Position(), InputTime + 0.02d));
            logic.OnInput();
            Assert.That(next.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(logic.TapCount, Is.EqualTo(2));
        }

        [Test]
        public void OlderSampleForSameTouchIdIsIgnored()
        {
            var note = Arrange();
            source.Add(Touch(1, Position(), InputTime + 1d, InputTouchPhase.Moved));
            logic.OnInput();
            source.Clear();
            source.Add(Touch(1, Position()));
            AssertRejectedThenAcceptFreshTouch(note);
            Assert.That(logic.TapCount, Is.EqualTo(1));
        }

        [Test]
        public void EndedTouchIdCannotPressAgainUntilInputIsRefreshed()
        {
            var note = Arrange();
            source.Add(Touch(1, Position(), InputTime, InputTouchPhase.Ended));
            logic.OnInput();
            source.Clear();
            source.Add(Touch(1, Position(), InputTime + 0.01d));
            AssertRejectedThenAcceptFreshTouch(note);
            Assert.That(logic.TapCount, Is.EqualTo(1));
        }

        [Test]
        public void TwoTouchesOnOneNoteInvokeJudgmentOnlyOnce()
        {
            var note = Arrange();
            source.Add(Touch(1, Position()));
            source.Add(Touch(2, Position()));
            logic.OnInput();
            Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(note.State, Is.EqualTo(NoteState.Done));
            Assert.That(note.JudgmentCalls, Is.EqualTo(1));
            Assert.That(logic.TapCount, Is.EqualTo(2));
        }
    }
}
