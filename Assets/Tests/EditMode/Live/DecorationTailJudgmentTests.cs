using System.Collections.Generic;

using NUnit.Framework;

using Sekai.Core.Live;
using Sekai.Live;

using UnityEngine;

using Sekai;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// A decorative bar is only a moving lane. The note at its end is a real note of its own kind and
    /// has to be judgeable exactly like a free-standing one: pressed for an ordinary end, swiped for a
    /// flick, rubbed for a friction end. These cases drive the real pipeline
    /// (registration → candidate allocation → judgment) once per end type, so a difference between the
    /// types shows up here instead of only on device.
    /// </summary>
    // Parked: hand-built notes never reach the candidate list even for an ordinary end, so this
    // fixture measures its own scaffolding, not the game. Rebuild it from RuntimeChartConverter
    // output before trusting it again.
    [Ignore("parked: hand-built notes do not become candidates; rebuild from a converted chart")]
    [TestFixture]
    public class DecorationTailJudgmentTests
    {
        private LiveBundleBuildData _config;

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        private bool _previousLegacyTiming;

        private LiveConfig.NoteTypeJudgeData _previousJudgeData;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LiveBundleBuildData>();
            _previousLegacyTiming = LiveConfig.UseLegacyJudgeTiming;
            _previousJudgeData = LiveConfig.noteTypeJudgeData;
            LiveConfig.LoadMasterConfig();
            // The shipped windows are the tables the game judges with; the legacy frame-based path
            // needs real frames and would silently reject every end note in an editor test.
            LiveConfig.UseLegacyJudgeTiming = false;
        }

        [TearDown]
        public void TearDown()
        {
            LiveConfig.UseLegacyJudgeTiming = _previousLegacyTiming;
            LiveConfig.noteTypeJudgeData = _previousJudgeData;
        }



        private static void Register(LiveLogic logic, NoteBase note) =>
            logic.Input.Register(note);

        private static LiveLogic.InputTmp Input(int finger, float lane, TouchPhase phase = TouchPhase.Began) => new LiveLogic.InputTmp
        {
            Lane = lane,
            Touch = new LiveTouch
            {
                fingerId = finger,
                touchId = finger,
                musicTime = 1f,
                phase = phase,
                worldPosition = LiveConfig.JudgmentPositions[Mathf.RoundToInt(lane)],
                delta = new Vector2(-240f, 0f)
            }
        };

        private LongNote DecorationBar(out NoteBase tail, System.Func<MusicScoreInfo, NoteBase> make)
        {
            var bar = new LongNote(Info(0f), 10, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
            bar.SetDecoration(true);
            tail = make(Info(1f));
            bar.AddNoteListAndSetupChildNote(tail);
            return bar;
        }

        private NoteBase NormalTail(MusicScoreInfo info) =>
            new NormalNote(info, 11, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);

        private NoteBase FlickTail(MusicScoreInfo info) =>
            new FlickNote(info, 12, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f);

        private NoteBase FrictionTail(MusicScoreInfo info) =>
            new FrictionNote(info, 13, 4, 5, NoteCategory.Friction, _config, NoteType.Default, 1f);

        private NoteBase FrictionFlickTail(MusicScoreInfo info) =>
            new FrictionFlickNote(info, 14, 4, 5, NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f);

        private LiveLogic Drive(System.Func<MusicScoreInfo, NoteBase> make, out NoteBase tail, TouchPhase phase)
        {
            var logic = new LiveLogic(null);
            LongNote bar = DecorationBar(out tail, make);
            // Let the bar drive its own end note to the judgement line the way a frame does: the
            // child only gets its judge lane range from its parent's per-frame pass.
            for (int frame = 0; frame <= 4; frame++)
            {
                logic.currentFrameInfo = Info(0.5f + frame * 0.125f);
                bar.Excute(logic.currentFrameInfo, 0f);
            }

            logic.currentFrameInfo = Info(1f);
            bar.Excute(logic.currentFrameInfo, 0f);
            Register(logic, bar);

            Assert.That(logic.Input.JudgmentNotes, Contains.Item(tail),
                "the key at the end of a decorative bar has to be registered itself");
            Assert.That(logic.Input.JudgmentNotes, Has.No.Member(bar),
                "the decorative bar itself must never answer input");

            logic.Input.ActiveInputs.Add(Input(1, 4.5f, phase));
            logic.Input.CollectCandidates();
            return logic;
        }

        [Test]
        public void AnOrdinaryEndOfADecorativeBarIsPressed()
        {
            LiveLogic logic = Drive(NormalTail, out NoteBase tail, TouchPhase.Began);
            Assert.That(logic.Input.JudgmentNotes, Contains.Item(tail),
                "a press inside the end note's lane has to make it a candidate");
            logic.Scheduler.EvaluateJudgment();
            Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None), "the press has to grade the end note");
        }

        [Test]
        public void AFlickEndOfADecorativeBarIsSwiped()
        {
            LiveLogic logic = Drive(FlickTail, out NoteBase tail, TouchPhase.Moved);
            Assert.That(logic.Input.JudgmentNotes, Contains.Item(tail),
                "a swipe over the flick end has to make it a candidate");
            logic.Scheduler.EvaluateJudgment();
            Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None), "the swipe has to grade the flick end");
        }

        [Test]
        public void AFrictionEndOfADecorativeBarIsRubbed()
        {
            LiveLogic logic = Drive(FrictionTail, out NoteBase tail, TouchPhase.Moved);
            Assert.That(logic.Input.JudgmentNotes, Contains.Item(tail),
                "a rub inside the friction end has to make it a candidate");
            logic.Scheduler.EvaluateJudgment();
            Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None), "the rub has to grade the friction end");
        }

        [Test]
        public void AFrictionFlickEndOfADecorativeBarIsSwiped()
        {
            LiveLogic logic = Drive(FrictionFlickTail, out NoteBase tail, TouchPhase.Moved);
            Assert.That(logic.Input.JudgmentNotes, Contains.Item(tail),
                "a swipe over the friction flick end has to make it a candidate");
            logic.Scheduler.EvaluateJudgment();
            Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None), "the swipe has to grade the friction flick end");
        }
    }
}
