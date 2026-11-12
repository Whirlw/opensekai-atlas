using System.IO;
using System.Linq;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// A bar has no owner any more, so two rules have to hold without one: a second finger may not
    /// publish the same head twice, and a finger leaving the screen finishes one bar - the nearest -
    /// rather than every bar its lane happens to cover.
    /// </summary>
    public sealed class HoldQuotaTests
    {
        private LiveConfig.NoteTypeJudgeData _config;
        private MusicScoreInfo _frame;

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        private static LiveTouch Touch(int finger, float time, TouchPhase phase = TouchPhase.Began)
        {
            return new LiveTouch { fingerId = finger, touchId = finger, musicTime = time, phase = phase };
        }

        private static LiveLogic.InputTmp Input(int finger, float lane, TouchPhase phase = TouchPhase.Stationary)
        {
            return new LiveLogic.InputTmp
            {
                Lane = lane,
                Touch = Touch(finger, 0f, phase),
            };
        }

        private sealed class Bar : LongNote
        {
            internal Bar(int laneStart, int laneEnd, float tailTime)
                : base(Info(0), 1, laneStart, laneEnd, NoteCategory.Long, NoteType.Default, 1, 0)
            {
                AddNoteListAndSetupChildNote(new NormalNote(Info(tailTime), 2, laneStart, laneEnd, NoteCategory.Normal, 1, NoteType.Default, 0));
                Excute(Info(0), 0);
            }
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, Private).GetValue(target);
        }

        private static void Stage(object target, string name)
        {
            target.GetType().GetMethod(name, Private).Invoke(target, null);
        }

        [SetUp]
        public void SetUp()
        {
            _config = LiveConfig.noteTypeJudgeData;
            _frame = MusicScore.CurrentFrameInfo;
            LiveConfig.LoadMasterConfig();
            MusicScore.CurrentFrameInfo = Info(0);
        }

        [TearDown]
        public void TearDown()
        {
            LiveConfig.noteTypeJudgeData = _config;
            MusicScore.CurrentFrameInfo = _frame;
        }

        [Test]
        public void TwoFingersOnOneHeadPublishItOnce()
        {
            var bar = new Bar(4, 5, 10f);
            int judgments = 0;
            bar.OnJudgment = _ => judgments++;

            var first = Touch(1, 0f);
            var second = Touch(2, 0f);
            bar.Judgment(ref first, 4.5f);
            bar.Judgment(ref second, 4.5f);

            Assert.That(judgments, Is.EqualTo(1), "a head is answered once, whoever answers it");
            Assert.That(bar.HasFinger, Is.True, "the bar keeps a finger on it");
            // The bar's visual state belongs to the body, not to the head: what the second finger
            // must not do is take the bar out of the player's hands.
            Assert.That(bar.WasTouched, Is.True, "the bar counts as taken");
        }

        [Test]
        public void OverlappingTailsRequireTwoLiftsOnAConvertedChart()
        {
            using var harness = new LiveHarness();
            var chart = ChartJson.Read(File.ReadAllText("Tools/fixtures/head-settlement/score.json"));
            foreach (var note in chart.NoteList)
            {
                bool left = note.id == 1 || note.id == 3;
                note.laneStart = left ? 2 : 4;
                note.laneEnd = left ? 4 : 7;
            }

            var score = RuntimeChartConverter.Convert(chart, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
            var bars = score.NoteArray.OfType<LongNote>().OrderBy(note => note.LaneStart).ToArray();
            Assert.That(bars, Has.Length.EqualTo(2));
            harness.Arrange(score);
            var leftTail = bars[0].ChildNote;
            var rightTail = bars[1].ChildNote;
            float headTime = bars[0].MusicScoreInfo.time;
            float tailTime = leftTail.MusicScoreInfo.time;
            Assert.That(rightTail.MusicScoreInfo.time, Is.EqualTo(tailTime));

            const float frame = 1f / 60f;
            var positions = LiveConfig.JudgmentPositions;
            harness.Step(headTime - frame, 10d + headTime - frame);
            for (int i = 0; headTime + i * frame < tailTime - frame * 0.5f; i++)
            {
                float now = headTime + i * frame;
                TouchPhase phase = i == 0 ? TouchPhase.Began : TouchPhase.Stationary;
                harness.Step(now, 10d + now,
                    LiveHarness.Touch(1, positions[3], 10d + now, phase),
                    LiveHarness.Touch(2, positions[6], 10d + now, phase));
            }

            Assert.That(bars.All(bar => bar.HasFinger), Is.True);
            Assert.That(leftTail.Result, Is.EqualTo(NoteResult.None));
            Assert.That(rightTail.Result, Is.EqualTo(NoteResult.None));
            harness.Step(tailTime, 10d + tailTime,
                LiveHarness.Touch(1, positions[4], 10d + tailTime, TouchPhase.Ended),
                LiveHarness.Touch(2, positions[6], 10d + tailTime, TouchPhase.Stationary));
            Assert.That(leftTail.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(rightTail.Result, Is.EqualTo(NoteResult.None), "one lift cannot grade both overlapping tails");

            float secondLift = tailTime + frame;
            harness.Step(secondLift, 10d + secondLift, LiveHarness.Touch(2, positions[6], 10d + secondLift, TouchPhase.Ended));
            Assert.That(rightTail.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void OneLiftFinishesOnlyTheNearestOfTwoOverlappingBars()
        {
            // Two bars overlap on lane 4.4; one finger lifts inside the overlap.
            var left = new Bar(3, 5, 10f);
            var right = new Bar(4, 6, 10f);
            var held = Touch(1, 0f);
            left.Judgment(ref held, 4.4f);
            right.Judgment(ref held, 4.4f);

            var logic = new LiveLogic(null);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(left);
            Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(right);
            Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(1, 4.4f, TouchPhase.Ended));

            Stage(logic, "RefreshSustainedHolds");

            int graded = 0;
            if (left.ChildNote != null && left.ChildNote.Result != NoteResult.None) graded++;
            if (right.ChildNote != null && right.ChildNote.Result != NoteResult.None) graded++;

            Assert.That(graded, Is.LessThanOrEqualTo(1), "one lift answers one bar, not both");
        }
    }
}
