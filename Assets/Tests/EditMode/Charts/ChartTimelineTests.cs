using System.Collections.Generic;
using NUnit.Framework;
using OpenSekai.Charts;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// Tick/time conversion drives note spawn times and the judge window, so it is pinned here with
    /// hand-computed values (120 BPM, 4/4: one beat is 480 ticks and half a second).
    /// </summary>
    public class ChartTimelineTests
    {
        private const float Bpm = 120f;
        private const float TimeSignature = 4f;

        private static MusicScoreInfo[] ConstantBpmTimeline() => new[]
        {
            new MusicScoreInfo(0, 0f, 0f, Bpm, TimeSignature, 1f, 1f)
        };

        private static MusicScoreInfo[] ConstantBpmTimelineWithIntroAt(float introTime)
        {
            return new[]
            {
                new MusicScoreInfo(0, 0f, 0f, Bpm, TimeSignature, 1f, 1f),
                new MusicScoreInfo(1, 0f, introTime, Bpm, TimeSignature, 1f, 1f)
            };
        }

        [Test]
        public void TickConstantsMatchOneBarOfFourFour()
        {
            Assert.That(ChartTimeline.TICKS_PER_BEAT, Is.EqualTo(480L));
            Assert.That(ChartTimeline.TICKS_PER_BAR, Is.EqualTo(1920L));
        }

        [Test]
        public void ZeroTicksIsZeroSeconds()
        {
            Assert.That(ChartTimeline.GetTimeFromTicks(0, ConstantBpmTimeline()), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void OneBeatAt120BpmIsHalfASecond()
        {
            Assert.That(ChartTimeline.GetTimeFromTicks(480, ConstantBpmTimeline()), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void OneBarAt120BpmIsTwoSeconds()
        {
            Assert.That(ChartTimeline.GetTimeFromTicks(1920, ConstantBpmTimeline()), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void TimeConvertsBackToTheSameTick()
        {
            MusicScoreInfo[] timeline = ConstantBpmTimeline();

            long ticks = ChartTimeline.GetTicksFromTime(2f, timeline);
            float time = ChartTimeline.GetTimeFromTicks(ticks, timeline);

            Assert.That(ticks, Is.EqualTo(1920L));
            Assert.That(time, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void TickAndTimeRoundTripAcrossTheFirstEightBars()
        {
            MusicScoreInfo[] timeline = ConstantBpmTimeline();

            for (long ticks = 0; ticks <= ChartTimeline.TICKS_PER_BAR * 8; ticks += ChartTimeline.TICKS_PER_BEAT)
            {
                float time = ChartTimeline.GetTimeFromTicks(ticks, timeline);
                long back = ChartTimeline.GetTicksFromTime(time, timeline);
                Assert.That(back, Is.EqualTo(ticks), $"ticks={ticks} time={time}");
            }
        }

        [Test]
        public void LaterTimelineEventsDoNotShiftEarlierTicks()
        {
            MusicScoreInfo[] timeline = ConstantBpmTimelineWithIntroAt(3f);

            // The second event only marks a musical change at 3 s; ticks stay absolute from the
            // chart origin, so 120 BPM still maps one bar (1920 ticks) to two seconds.
            Assert.That(ChartTimeline.GetTimeFromTicks(1920, timeline), Is.EqualTo(2f).Within(0.001f));
            Assert.That(ChartTimeline.GetTimeFromTicks(3840, timeline), Is.EqualTo(4f).Within(0.001f));
            Assert.That(ChartTimeline.GetTicksFromTime(3f, timeline), Is.EqualTo(2880L));
            Assert.That(ChartTimeline.GetTicksFromTime(2f, timeline), Is.EqualTo(1920L));
        }

        [Test]
        public void BarAndProgressConvertToTicks()
        {
            List<MusicScoreInfo> sorted = ChartTimeline.CreateSortedMusicScoreInfoList(ConstantBpmTimeline());

            long fromBar = ChartTimeline.CalculateTicksFromBarAndProgressSorted(2, 0.5f, sorted);

            Assert.That(fromBar, Is.EqualTo(ChartTimeline.TICKS_PER_BAR * 2 + ChartTimeline.TICKS_PER_BAR / 2));
        }

        [Test]
        public void ConvertMusicScoreInfoSortsByTime()
        {
            var events = new List<ChartEvent>
            {
                new ChartEvent { id = 2, ticks = 3840, eventType = ChartEventType.BPM, changeValue = 180f },
                new ChartEvent { id = 1, ticks = 0, eventType = ChartEventType.BPM, changeValue = Bpm }
            };

            MusicScoreInfo[] infos = ChartTimeline.ConvertMusicScoreInfo(events);

            Assert.That(infos, Is.Not.Null.And.Length.GreaterThanOrEqualTo(2));
            Assert.That(infos[0].bpm, Is.EqualTo(Bpm).Within(0.001f));
            for (int i = 1; i < infos.Length; i++)
            {
                Assert.That(infos[i].time, Is.GreaterThanOrEqualTo(infos[i - 1].time));
            }
        }
    }
}
