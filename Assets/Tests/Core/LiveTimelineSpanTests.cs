using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;

namespace OpenSekai.Tests.Core
{
    public sealed class LiveTimelineSpanTests
    {
        [Test]
        public void ChartInsideTheMusicRunsOverTheMusic()
        {
            var span = new LiveTimelineSpan(0f, 120f, 5f, 110f, 1f);
            Assert.That(span.StartSeconds, Is.EqualTo(-2f), "the music comes two seconds after the info page");
            Assert.That(span.EndSeconds, Is.EqualTo(120f));
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(121f), "the result effect one second after the music");
        }

        [Test]
        public void AFirstNoteBeforeTheMusicComesTwoSecondsAfterTheInfoPage()
        {
            var span = new LiveTimelineSpan(5f, 100f, 4f, 90f, 1f);
            Assert.That(span.StartSeconds, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void ChartPastTheEndOfTheMusicKeepsTheClockGoing()
        {
            var span = new LiveTimelineSpan(0f, 100f, 5f, 101.5f, 1f);
            Assert.That(span.EndSeconds, Is.EqualTo(101.5f), "the last note decides, not the audio");
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(102.5f).Within(1e-5f), "one second after the last note");
            Assert.That(span.HasOutroSkip, Is.False);
        }

        [Test]
        public void ChartBeforeTheMusicStartsEarly()
        {
            var span = new LiveTimelineSpan(0f, 100f, 0.5f, 90f, 1.5f);
            Assert.That(span.StartSeconds, Is.EqualTo(-2f).Within(1e-5f), "the music is first and gets its two seconds");
            Assert.That(span.IntroSkipTargetSeconds, Is.EqualTo(-1.5f));
            Assert.That(span.HasIntroSkip, Is.True);
        }

        [Test]
        public void ALeadInLongerThanTheRunInStartsEarlierStill()
        {
            var span = new LiveTimelineSpan(0f, 100f, 0.5f, 90f, 5f);
            Assert.That(span.StartSeconds, Is.EqualTo(-4.5f).Within(1e-5f), "the first note scrolls in whole");
        }

        [Test]
        public void IntroSkipAppearsAtTheStartAndLandsTwoSecondsBeforeTheFirstNote()
        {
            Assert.That(new LiveTimelineSpan(0f, 100f, 0f, 90f, 1f).HasIntroSkip, Is.False);

            var span = new LiveTimelineSpan(0f, 100f, 12f, 90f, 1f);
            Assert.That(span.HasIntroSkip, Is.True);
            Assert.That(span.IntroSkipTargetSeconds, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(new LiveTimelineSpan(0f, 100f, 0.01f, 90f, 1f).HasIntroSkip, Is.True);
        }

        [Test]
        public void IntroSkipLandsAtTwoSecondsEvenWithALongLeadIn()
        {
            var span = new LiveTimelineSpan(0f, 100f, 12f, 90f, 5f);
            Assert.That(span.IntroSkipTargetSeconds, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(span.HasIntroSkip, Is.True);
        }

        [Test]
        public void OutroSkipAppearsOneSecondAfterTheLastNote()
        {
            Assert.That(new LiveTimelineSpan(0f, 100f, 5f, 100f, 1f).HasOutroSkip, Is.False);
            var span = new LiveTimelineSpan(0f, 100f, 5f, 99.5f, 1f);
            Assert.That(span.HasOutroSkip, Is.True);
            Assert.That(span.OutroSkipAvailableSeconds, Is.EqualTo(100.5f));
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(101f));
        }

        [Test]
        public void TheAudibleMusicDecidesTheStartAndEndPoints()
        {
            // The file is 0..100 s, sound only from 4 s to 95 s; notes 6..91 s.
            var span = new LiveTimelineSpan(4f, 95f, 6f, 91f, 1f, 0f, 100f);
            Assert.That(span.StartSeconds, Is.EqualTo(2f), "two seconds before the sound, not the file");
            Assert.That(span.EndSeconds, Is.EqualTo(95f), "the trailing silence is not the end");
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(96f));
            Assert.That(span.HasIntroSkip, Is.True, "2 s of sound before the chart");
            Assert.That(span.HasOutroSkip, Is.True, "4 s of sound after the chart");

            var silentHead = new LiveTimelineSpan(4f, 95f, 8f, 91f, 1f, 0f, 100f);
            Assert.That(silentHead.HasIntroSkip, Is.True, "4 s of sound before the chart");
            Assert.That(silentHead.IntroSkipTargetSeconds, Is.EqualTo(6f).Within(1e-5f));
        }

        [Test]
        public void TheSilenceIsPlayedUnderTheInfoPageAndCutOnlyWhereItWouldPrecedeLoading()
        {
            // 4 s of silence, a 6 s info page and 2 s after it: the file starts 4 s into the page.
            var shortSilence = new LiveTimelineSpan(4f, 95f, 10f, 91f, 1f, 0f, 100f);
            Assert.That(shortSilence.ClockAtIntroStart(6f), Is.EqualTo(-4f).Within(1e-5f));

            // 12 s of silence: the first 4 s would have to play before the page, so they are cut.
            var longSilence = new LiveTimelineSpan(12f, 95f, 18f, 91f, 1f, 0f, 100f);
            Assert.That(longSilence.ClockAtIntroStart(6f), Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void NoNotesRunsOverTheMusicWithoutSkips()
        {
            var span = new LiveTimelineSpan(0f, 60f, float.NaN, float.NaN, 1f);
            Assert.That(span.StartSeconds, Is.EqualTo(-2f));
            Assert.That(span.EndSeconds, Is.EqualTo(60f));
            Assert.That(span.HasIntroSkip, Is.False);
            Assert.That(span.HasOutroSkip, Is.False);
        }
    }
}
