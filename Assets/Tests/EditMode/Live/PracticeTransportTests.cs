using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using OpenSekai.Shared.Live.Simulation;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class PracticeTransportTests
    {
        private static NormalNote Tap(float seconds, int id) =>
            new NormalNote(new MusicScoreInfo(0, 0f, seconds, 120f, 4f, 1f, 1f), id, 4, 4,
                NoteCategory.Normal, 1f, NoteType.Default, 0f);

        [Test]
        public void ConstantScrollLeavesLegacyChartTimesAndAuthoredSpeedsIntact()
        {
            var document = new OpenSekai.Charts.ChartDocument();
            document.MusicScoreEventDataList.Add(new OpenSekai.Charts.ChartEvent
                { eventType = OpenSekai.Charts.ChartEventType.HighSpeed, ticks = 0, changeValue = "2" });
            document.NoteList.Add(new OpenSekai.Charts.ChartNote(1, 480, 4, 4, NoteCategory.Normal, speedRatio: 3f));
            var standard = RuntimeChartConverter.Convert(document, new ChartConversionSettings(null), false, false);
            var constant = RuntimeChartConverter.Convert(document, new ChartConversionSettings(null, true), false, false);
            Assert.That(standard.NoteArray.Single().speedRatio, Is.EqualTo(3f));
            Assert.That(constant.NoteArray.Single().speedRatio, Is.EqualTo(1f));
            Assert.That(constant.NoteArray.Single().MusicScoreInfo.time, Is.EqualTo(standard.NoteArray.Single().MusicScoreInfo.time));
            Assert.That(constant.musicScoreInfoArray.All(frame => frame.speedRatio == 1f), Is.True);
            Assert.That(document.NoteList.Single().speedRatio, Is.EqualTo(3f));
        }

        [TestCase(0.01d)]
        [TestCase(0.5d)]
        [TestCase(0.7d)]
        [TestCase(1d)]
        [TestCase(2d)]
        [TestCase(3d)]
        public void SampledTouchAndLegacyOffsetsUseSongTime(double rate)
        {
            Assert.That(InputTimeMapping.FromClock(20, 100, 99.9, rate), Is.EqualTo(20 - .1 * rate).Within(.00001));
            Assert.That(InputTimeMapping.FromClock(20, 100, 99, rate), Is.EqualTo(20 - .25 * rate).Within(.00001));
            Assert.That(InputTimeMapping.FromClock(20, 100, 101, rate), Is.EqualTo(20));
            Assert.That(InputTimeMapping.FromLegacyClock(20, 100, 99.9, rate),
                Is.EqualTo(20 - .1 * rate / 60).Within(.00001));
        }

        [Test]
        public void SeekingAtAnExactNoteKeepsItPlayableAndRewindRearmsIt()
        {
            using var h = new LiveHarness();
            var first = Tap(1, 1);
            var second = Tap(20, 2);
            var score = new MusicScore(null, null, new NoteBase[] { first, second });
            h.Arrange(score, auto: true);
            h.Logic.SeekPractice(20);
            h.Logic.PreviewPractice(20, 120);
            Assert.That(first.State, Is.EqualTo(NoteState.Done));
            Assert.That(first.Result, Is.EqualTo(NoteResult.None));
            Assert.That(second.Result, Is.EqualTo(NoteResult.None), "paused scrub cannot judge the target");
            h.OnUpdate(20, 120);
            Assert.That(second.Result, Is.EqualTo(NoteResult.Auto));
            Assert.That(h.Logic.Result, Is.Zero, "the last note must not finish a practice segment");
            h.Logic.SeekPractice(1);
            Assert.That(first.State, Is.EqualTo(NoteState.Wait));
            Assert.That(second.State, Is.EqualTo(NoteState.Wait));
            h.OnUpdate(1, 125);
            Assert.That(first.Result, Is.EqualTo(NoteResult.Auto));
            Assert.That(second.Result, Is.EqualTo(NoteResult.None));
        }

        [Test]
        public void RewindingClearsThePreviousTakesScoreComboAndPublishedJudgments()
        {
            using var h = new LiveHarness();
            var note = Tap(2, 1);
            var score = new MusicScore(null, null, new NoteBase[] { note });
            h.Arrange(score);
            h.BindViews();
            h.Logic.SetScoreLogic(new ScoreLogic(null));
            foreach (double wall in new[] { 100d, 200d })
            {
                h.Logic.SeekPractice(1);
                Assert.That(h.Logic.Score.combo, Is.Zero);
                Assert.That(h.Logic.Score.totalScore, Is.Zero);
                Assert.That(h.Logic.Score.perfectCount, Is.Zero);
                h.Step(2, wall, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[4], wall));
                Assert.That(h.Logic.Score.combo, Is.EqualTo(1));
                Assert.That(h.Logic.Score.perfectCount, Is.EqualTo(1));
                Assert.That(h.Logic.Score.totalScore, Is.GreaterThan(0));
            }
        }

        [Test]
        public void EmptyPracticeCanContinueThroughSilenceWithoutFinishing()
        {
            using var h = new LiveHarness();
            h.Arrange(new MusicScore(null, null, Array.Empty<NoteBase>()), auto: true);
            h.Logic.SeekPractice(10);
            h.OnUpdate(12, 20);
            Assert.That(h.Logic.Result, Is.Zero);
        }

        [Test]
        public void SeekingInsideAHoldSkipsItsHeadWithoutScoringOrDroppingItsTail()
        {
            string fixture = Path.Combine(Application.dataPath, "..", "Tools", "fixtures", "head-settlement", "score.json");
            var score = RuntimeChartConverter.Convert(ChartJson.Read(File.ReadAllText(fixture)),
                Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
            using var h = new LiveHarness();
            h.Arrange(score);
            var hold = score.NoteArray.OfType<LongNote>().First();
            float start = hold.MusicScoreInfo.time, end = hold.TailNote.MusicScoreInfo.time;
            Assert.That(end, Is.GreaterThan(start));
            float cut = (start + end) / 2f;
            int scored = 0;
            hold.OnUpdateScore = n => scored++;
            h.Logic.SeekPractice(cut);
            h.OnUpdate(cut, 50);
            h.OnInput();
            Assert.That(hold.Head.IsSettled, Is.True);
            Assert.That(hold.TailNote.State, Is.Not.EqualTo(NoteState.Done));
            Assert.That(scored, Is.Zero);
            h.Logic.SeekPractice(start);
            Assert.That(hold.Head.IsSettled, Is.False);
        }

        [Test]
        public void EventsBeforeCutAreSilentAndEventsOnCutFireAgainAfterRewind()
        {
            var oldEvent = new EventBase(Tap(1, 1).MusicScoreInfo);
            var atCut = new EventBase(Tap(2, 2).MusicScoreInfo);
            var timeline = new LiveTimeline();
            var events = new[] { oldEvent, atCut };
            int count = 0;
            timeline.SkipEventsBefore(events, 2);
            timeline.AdvanceEvents(atCut.MusicScoreInfo, events, e => count++);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(oldEvent.State, Is.EqualTo(EventState.Wait));
            atCut.ResetEvent();
            timeline.Reset();
            timeline.AdvanceEvents(atCut.MusicScoreInfo, events, e => count++);
            Assert.That(count, Is.EqualTo(3));
        }
    }
}
