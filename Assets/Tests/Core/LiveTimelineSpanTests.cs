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
            Assert.That(span.StartSeconds, Is.EqualTo(-3f), "the music comes three seconds after the info page");
            Assert.That(span.EndSeconds, Is.EqualTo(120f));
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(123f), "the result effect three seconds after the music");
        }

        [Test]
        public void AFirstNoteBeforeTheMusicComesThreeSecondsAfterTheInfoPage()
        {
            var span = new LiveTimelineSpan(5f, 100f, 4f, 90f, 1f);
            Assert.That(span.StartSeconds, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ChartPastTheEndOfTheMusicKeepsTheClockGoing()
        {
            var span = new LiveTimelineSpan(0f, 100f, 5f, 101.5f, 1f);
            Assert.That(span.EndSeconds, Is.EqualTo(101.5f), "the last note decides, not the audio");
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(104.5f).Within(1e-5f), "three seconds after the last note");
            Assert.That(span.HasOutroSkip, Is.False);
        }

        [Test]
        public void ChartBeforeTheMusicStartsEarly()
        {
            var span = new LiveTimelineSpan(0f, 100f, 0.5f, 90f, 1.5f);
            Assert.That(span.StartSeconds, Is.EqualTo(-3f).Within(1e-5f), "the music is first and gets its three seconds");
            Assert.That(span.HasIntroSkip, Is.False);
        }

        [Test]
        public void ALeadInLongerThanTheRunInStartsEarlierStill()
        {
            var span = new LiveTimelineSpan(0f, 100f, 0.5f, 90f, 5f);
            Assert.That(span.StartSeconds, Is.EqualTo(-4.5f).Within(1e-5f), "the first note scrolls in whole");
        }

        [Test]
        public void IntroSkipNeedsThreeSecondsAndLandsThreeBeforeTheFirstNote()
        {
            Assert.That(new LiveTimelineSpan(0f, 100f, 2.9f, 90f, 1f).HasIntroSkip, Is.False);

            var span = new LiveTimelineSpan(0f, 100f, 12f, 90f, 1f);
            Assert.That(span.HasIntroSkip, Is.True);
            Assert.That(span.IntroSkipTargetSeconds, Is.EqualTo(9f).Within(1e-5f));
        }

        [Test]
        public void IntroSkipKeepsALongLeadInOnScreen()
        {
            var span = new LiveTimelineSpan(0f, 100f, 12f, 90f, 5f);
            Assert.That(span.IntroSkipTargetSeconds, Is.EqualTo(7f).Within(1e-5f));

            // A lead-in that eats the whole gap leaves nothing to skip.
            Assert.That(new LiveTimelineSpan(0f, 100f, 3.1f, 90f, 3.05f).HasIntroSkip, Is.False);
        }

        [Test]
        public void OutroSkipNeedsThreeSecondsOfMusicAfterTheLastNote()
        {
            Assert.That(new LiveTimelineSpan(0f, 100f, 5f, 97.1f, 1f).HasOutroSkip, Is.False);
            Assert.That(new LiveTimelineSpan(0f, 100f, 5f, 97f, 1f).HasOutroSkip, Is.True);
        }

        [Test]
        public void TheAudibleMusicDecidesTheStartAndEndPoints()
        {
            // The file is 0..100 s, sound only from 4 s to 95 s; notes 6..91 s.
            var span = new LiveTimelineSpan(4f, 95f, 6f, 91f, 1f, 0f, 100f);
            Assert.That(span.StartSeconds, Is.EqualTo(1f), "three seconds before the sound, not the file");
            Assert.That(span.EndSeconds, Is.EqualTo(95f), "the trailing silence is not the end");
            Assert.That(span.ResultEffectSeconds, Is.EqualTo(98f));
            Assert.That(span.HasIntroSkip, Is.False, "only 2 s of sound before the chart");
            Assert.That(span.HasOutroSkip, Is.True, "4 s of sound after the chart");

            var silentHead = new LiveTimelineSpan(4f, 95f, 8f, 91f, 1f, 0f, 100f);
            Assert.That(silentHead.HasIntroSkip, Is.True, "4 s of sound before the chart");
            Assert.That(silentHead.IntroSkipTargetSeconds, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void TheSilenceIsPlayedUnderTheInfoPageAndCutOnlyWhereItWouldPrecedeLoading()
        {
            // 4 s of silence, a 6 s info page and 3 s after it: the file starts 5 s into the page.
            var shortSilence = new LiveTimelineSpan(4f, 95f, 10f, 91f, 1f, 0f, 100f);
            Assert.That(shortSilence.ClockAtIntroStart(6f), Is.EqualTo(-5f).Within(1e-5f));

            // 12 s of silence: the first 3 s would have to play before the page, so they are cut.
            var longSilence = new LiveTimelineSpan(12f, 95f, 18f, 91f, 1f, 0f, 100f);
            Assert.That(longSilence.ClockAtIntroStart(6f), Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void NoNotesRunsOverTheMusicWithoutSkips()
        {
            var span = new LiveTimelineSpan(0f, 60f, float.NaN, float.NaN, 1f);
            Assert.That(span.StartSeconds, Is.EqualTo(-3f));
            Assert.That(span.EndSeconds, Is.EqualTo(60f));
            Assert.That(span.HasIntroSkip, Is.False);
            Assert.That(span.HasOutroSkip, Is.False);
        }
    }
}
