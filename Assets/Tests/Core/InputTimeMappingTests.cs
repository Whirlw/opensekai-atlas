using NUnit.Framework;
using OpenSekai.Shared.Live.Simulation;

namespace OpenSekai.Tests.Core
{
    public sealed class InputTimeMappingTests
    {
        private const double FRAME_GAME_TIME = 100d;
        private const float FRAME_MUSIC_TIME = 10f;

        [Test]
        public void TheLegacyMappingShrinksLatencyToSixtieths()
        {
            // 旧的公式把墙钟位移乘 1/60 秒：120 ms 的真实延迟只算 2 ms。
            float mapped = InputTimeMapping.FromLegacyClock(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME + 0.12);

            Assert.That(mapped, Is.EqualTo(FRAME_MUSIC_TIME + 0.12f / 60f).Within(1e-5f));
            Assert.That(mapped, Is.LessThan(FRAME_MUSIC_TIME + 0.005f), "latency is shrunk sixty times, not passed through");
        }

        [Test]
        public void TheCurrentMappingPassesLatencyAndClampsItToAQuarterSecond()
        {
            // 早到：原样通过。
            Assert.That(
                InputTimeMapping.FromClock(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME - 0.1),
                Is.EqualTo(FRAME_MUSIC_TIME - 0.1f).Within(1e-5f));

            // 迟到（inputTime 在帧之后）：夹到 0，不把未来的时间算进来。
            Assert.That(
                InputTimeMapping.FromClock(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME + 0.1),
                Is.EqualTo(FRAME_MUSIC_TIME).Within(1e-5f));

            // 过期太久（暂停/失焦后残留的触摸）：夹到 -250 ms。
            Assert.That(
                InputTimeMapping.FromClock(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME - 5d),
                Is.EqualTo(FRAME_MUSIC_TIME - 0.25f).Within(1e-5f));
        }

        // The two judge-timing modes must stay distinguishable, otherwise the setting is a lie.
        [Test]
        public void LegacyTimingShrinksLatencyByOneFrame()
        {
            const double frameMusicTime = 12.5d;
            const double frameGameTime = 100d;
            const double inputTime = 99.9d; // a touch sampled 100 ms before this frame

            float legacy = InputTimeMapping.FromLegacyClock(
                frameMusicTime, frameGameTime, inputTime);
            float sampled = InputTimeMapping.FromClock(
                frameMusicTime, frameGameTime, inputTime);

            // Legacy: 100 ms of real latency becomes ~1.7 ms of music time (1/60 of the delta).
            Assert.That((frameMusicTime - legacy) * 1000d, Is.EqualTo(100d / 60d).Within(0.05d));
            // Sampled: the touch lands 100 ms earlier on the timeline.
            Assert.That(sampled, Is.EqualTo(12.4f).Within(1e-4f));
            Assert.That(sampled, Is.LessThan(legacy));
        }

        [Test]
        public void LegacyTimingDoesNotClampButSampledTimingDoes()
        {
            float legacy = InputTimeMapping.FromLegacyClock(5d, 10d, 9d);
            float sampled = InputTimeMapping.FromClock(5d, 10d, 9d);

            // One second of stale latency: legacy turns it into 1/60 s, sampled clamps to 250 ms.
            Assert.That((5d - legacy) * 1000d, Is.EqualTo(1000d / 60d).Within(0.1d));
            Assert.That(sampled, Is.EqualTo(4.75f).Within(1e-4f));
        }
    }
}
