using System.IO;
using NUnit.Framework;
using OpenSekai.Shared.Conversion;
using Sekai.Live;

namespace OpenSekai.Tests.Core
{
    /// <summary>
    /// The timing stage turns timing rows into score frames. It is pure, so the exact frame list — bar,
    /// bar progress and accumulated time — can be asserted without a scene, which is what makes the
    /// split from the SUS importer verifiable on its own.
    /// </summary>
    public class SusTimingConverterTests
    {
        private const string FixtureDirectory = "Tools/fixtures/sus-p2-6";

        private static MusicScoreInfo[] ConvertFixture(string name)
        {
            string path = Path.Combine(FixtureDirectory, name + ".sus");
            Assert.That(File.Exists(path), Is.True, "fixture not found: " + path);
            SusChartData chart = new SusParser().Parse(File.ReadAllText(path));
            return SusTimingConverter.Convert(chart);
        }

        [Test]
        public void TheFirstFrameIsTheOrigin()
        {
            MusicScoreInfo[] frames = ConvertFixture("timing");

            Assert.That(frames, Is.Not.Empty);
            Assert.That(frames[0].bar, Is.EqualTo(0));
            Assert.That(frames[0].barProgress, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frames[0].time, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frames[0].bpm, Is.EqualTo(120f).Within(0.001f));
            Assert.That(frames[0].timeSignature, Is.EqualTo(4f).Within(0.001f));
        }

        /// <summary>
        /// Tick offsets are read against the time signature in force at their bar: 120 ticks of a 4/4 bar
        /// at 240 ticks per beat is one eighth of the bar, 240 ticks is a quarter.
        /// </summary>
        [Test]
        public void HighSpeedAndVolumeTicksBecomeBarProgress()
        {
            MusicScoreInfo[] frames = ConvertFixture("timing");

            Assert.That(FindFrame(frames, 0, 0.125f), Is.Not.Null, "the #TIL00 row at 0'120");
            Assert.That(FindFrame(frames, 0, 0.25f), Is.Not.Null, "the #VOLUME row at 0'240");
        }

        /// <summary>
        /// Frames carry the values in force at their own position and their time is the running total.
        /// The numbers are the ones the pre-split importer produced for this fixture.
        /// </summary>
        [Test]
        public void FrameTimesAccumulateAcrossBpmAndTimeSignatureChanges()
        {
            MusicScoreInfo[] frames = ConvertFixture("timing");

            AssertFrame(frames, 0, 0f, 0f, 120f, 4f, 1f, 1f);
            AssertFrame(frames, 0, 0.125f, 0.25f, 120f, 4f, 1.5f, 1f);
            AssertFrame(frames, 0, 0.25f, 0.5f, 120f, 4f, 1.5f, 0.8f);
            AssertFrame(frames, 1, 0f, 2f, 120f, 3f, 1.5f, 0.5f);
            AssertFrame(frames, 1, 0.5f, 2.75f, 180f, 3f, -0.5f, 0.5f);
            Assert.That(frames, Has.Length.EqualTo(5));
        }

        [Test]
        public void AnEmptyChartStillHasAnOriginFrame()
        {
            MusicScoreInfo[] frames = SusTimingConverter.Convert(new SusChartData());

            Assert.That(frames, Has.Length.EqualTo(1));
            Assert.That(frames[0].bar, Is.EqualTo(0));
            Assert.That(frames[0].barProgress, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frames[0].bpm, Is.EqualTo(SusChartData.DefaultBpm).Within(0.001f));
            Assert.That(frames[0].timeSignature, Is.EqualTo(SusChartData.DefaultTimeSignature).Within(0.001f));
        }

        private static MusicScoreInfo? FindFrame(MusicScoreInfo[] frames, int bar, float barProgress)
        {
            foreach (MusicScoreInfo frame in frames)
            {
                if (frame.bar == bar && System.Math.Abs(frame.barProgress - barProgress) < 0.0001f)
                {
                    return frame;
                }
            }
            return null;
        }

        private static void AssertFrame(MusicScoreInfo[] frames, int bar, float barProgress, float time,
            float bpm, float timeSignature, float speedRatio, float seVolume)
        {
            MusicScoreInfo? found = FindFrame(frames, bar, barProgress);
            Assert.That(found, Is.Not.Null, $"no frame at bar {bar} progress {barProgress}");
            MusicScoreInfo frame = found.Value;
            Assert.That(frame.time, Is.EqualTo(time).Within(0.0001f), $"time at bar {bar} progress {barProgress}");
            Assert.That(frame.bpm, Is.EqualTo(bpm).Within(0.001f), $"bpm at bar {bar} progress {barProgress}");
            Assert.That(frame.timeSignature, Is.EqualTo(timeSignature).Within(0.001f), $"time signature at bar {bar} progress {barProgress}");
            Assert.That(frame.speedRatio, Is.EqualTo(speedRatio).Within(0.001f), $"speed ratio at bar {bar} progress {barProgress}");
            Assert.That(frame.seVolume, Is.EqualTo(seVolume).Within(0.001f), $"SE volume at bar {bar} progress {barProgress}");
        }
    }
}
