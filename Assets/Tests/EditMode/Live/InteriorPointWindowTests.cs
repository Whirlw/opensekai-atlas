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
            // A single update puts the accumulated points in Last without resolving them.
            bar.Excute(Info(1f), 0);
            foreach (var point in expired) Assert.That(point.State, Is.EqualTo(NoteState.Last));
            int scored = 0;
            bar.OnJudgment = note => { if (note is LongHoldCombo) scored++; };

            var logic = Drive(bar, 1f, 1f);
            Refresh(logic);

            foreach (var point in expired)
            {
                Assert.That(point.Result, Is.EqualTo(NoteResult.None));
                Assert.That(point.State, Is.Not.EqualTo(NoteState.Done));
            }
            Assert.That(due.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(scored, Is.EqualTo(1), "three expired points must not join the one live point, even on a repeated refresh");
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
                bar.Excute(Info(0.125f), 0);
                int scored = 0;
                bar.OnJudgment = note => { if (ReferenceEquals(note, point)) scored++; };

                var logic = Drive(bar, 0.125f, stamp);
                Refresh(logic);

                Assert.That(point.Result, Is.EqualTo(NoteResult.JustPerfect), $"touch stamp {stamp} must not replace the frame clock");
                Assert.That(point.State, Is.EqualTo(NoteState.Done));
                Assert.That(scored, Is.EqualTo(1));
            }
        }
    }
}
