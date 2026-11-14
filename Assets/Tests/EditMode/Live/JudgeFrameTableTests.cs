using NUnit.Framework;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Pins the judge frame table and the frame-to-second conversion.
    ///
    /// The numbers come from the shipped judge data, cross-checked against the published
    /// reverse-engineering write-up "音符判定范围详解" (the third article linked from
    /// docs/reference/judging.md). Every value below is quoted in that article in milliseconds,
    /// so these assertions are a parity check, not a preference.
    ///
    /// If a change here is intentional, it is a gameplay feel change: update the article table in
    /// docs/reference/judging.md in the same commit so the next reader knows it was chosen.
    /// </summary>
    public sealed class JudgeFrameTableTests
    {
        private const float FrameMs = 1000f / 60f;

        [Test]
        public void FrameToTimeUsesSixtyFramesPerSecond()
        {
            Assert.That(LiveConfig.CalcFrameToTime(60f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(LiveConfig.CalcFrameToTime(30f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(LiveConfig.CalcFrameToTime(2.5f), Is.EqualTo(2.5f * FrameMs / 1000f).Within(1e-6f));
        }

        [TestCase(2.5f, 41.6667f, TestName = "Normal perfect is plus or minus 41.7 ms")]
        [TestCase(3.3f, 55.0f, TestName = "Critical tap perfect is plus or minus 55 ms")]
        [TestCase(3.5f, 58.3333f, TestName = "Critical flick perfect is plus or minus 58.3 ms")]
        [TestCase(5f, 83.3333f, TestName = "Normal great is plus or minus 83.3 ms")]
        [TestCase(7.5f, 125f, TestName = "Bad edge is 125 ms, the point where an early tap stops counting")]
        [TestCase(8.5f, 141.6667f, TestName = "Late bad edge is 141.7 ms for notes other than taps and long starts")]
        public void FrameWindowsMatchThePublishedTable(float frames, float expectedMs)
        {
            float ms = LiveConfig.CalcFrameToTime(frames) * 1000f;
            Assert.That(ms, Is.EqualTo(expectedMs).Within(0.1f));
        }

        [Test]
        public void PerfectWindowWidthsMatchTheArticle()
        {
            // Normal tap: 2.5 + 2.5 frames = 83.3 ms, the article quotes "about 83.3 ms".
            Assert.That(WidthMs(2.5f, 2.5f), Is.EqualTo(83.3f).Within(0.1f));

            // Critical tap: 3.3 + 3.3 = 110 ms, article quotes 110 ms.
            Assert.That(WidthMs(3.3f, 3.3f), Is.EqualTo(110f).Within(0.1f));

            // Critical flick: 3.5 + 3.5 = 116.7 ms, article quotes 116.7 ms.
            Assert.That(WidthMs(3.5f, 3.5f), Is.EqualTo(116.7f).Within(0.1f));

            // Long end: 3.5 before + 4 after = 125 ms, article says every long end shares 125 ms.
            Assert.That(WidthMs(3.5f, 4f), Is.EqualTo(125f).Within(0.1f));
        }

        private static float WidthMs(float beforeFrames, float afterFrames)
        {
            return (LiveConfig.CalcFrameToTime(beforeFrames) + LiveConfig.CalcFrameToTime(afterFrames)) * 1000f;
        }

        [Test]
        public void JudgeTimingSettingDefaultsToSampledTouch()
        {
            var data = new Sekai.LiveSettingData();
            Assert.That(data.UseLegacyJudgeTiming, Is.False, "new installs judge at the touch timestamp");
        }
    }
}
