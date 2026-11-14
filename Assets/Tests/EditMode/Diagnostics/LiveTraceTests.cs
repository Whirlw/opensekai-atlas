using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;

namespace EditMode.Diagnostics
{
    /// <summary>
    /// The trace is resident, so its own behaviour has to be pinned down: silent while off
    /// (otherwise it costs a frame budget for nothing), gated per category, and able to hand back
    /// the newest lines in order.
    /// </summary>
    public sealed class LiveTraceTests
    {
        [SetUp]
        public void SetUp()
        {
            LiveTrace.Enabled = false;
            LiveTrace.Categories = LiveTraceCategory.Hold | LiveTraceCategory.Tail;
            LiveTrace.ClearBuffer();
        }

        [TearDown]
        public void TearDown()
        {
            LiveTrace.Enabled = false;
            LiveTrace.Categories = LiveTraceCategory.Hold | LiveTraceCategory.Tail;
            LiveTrace.ClearBuffer();
        }

        [Test]
        public void NothingIsKeptWhileTracingIsOff()
        {
            LiveTrace.Write(LiveTraceCategory.Hold, "ignored");

            Assert.That(LiveTrace.IsOn(LiveTraceCategory.Hold), Is.False);
            Assert.That(LiveTrace.Recent(10), Is.Empty);
        }

        [Test]
        public void TurningTracingOnKeepsTheSliceItWasGiven()
        {
            LiveTrace.Enabled = true;

            LiveTrace.Write(LiveTraceCategory.Hold, "a");
            LiveTrace.Write(LiveTraceCategory.Tail, "b");

            string recent = LiveTrace.Recent(10);
            StringAssert.Contains("a", recent);
            StringAssert.Contains("b", recent);
        }

        [Test]
        public void ACategoryThatIsNotSelectedIsDropped()
        {
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.Hold;

            LiveTrace.Write(LiveTraceCategory.Tail, "not-here");

            Assert.That(LiveTrace.IsOn(LiveTraceCategory.Tail), Is.False);
            Assert.That(LiveTrace.IsOn(LiveTraceCategory.Hold), Is.True);
            Assert.That(LiveTrace.Recent(10), Is.Empty);
        }

        [Test]
        public void RecentReturnsTheNewestLinesOldestFirst()
        {
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.All;

            LiveTrace.Write(LiveTraceCategory.Hold, "one");
            LiveTrace.Write(LiveTraceCategory.Hold, "two");
            LiveTrace.Write(LiveTraceCategory.Hold, "three");

            string[] lines = LiveTrace.Recent(2).TrimEnd().Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            StringAssert.Contains("two", lines[0]);
            StringAssert.Contains("three", lines[1]);
        }

        [Test]
        public void AnInvariantIsKeptEvenWhenItsCategoryIsOff()
        {
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.View;

            LiveTrace.Invariant(false, "time went backwards");

            StringAssert.Contains("INVARIANT", LiveTrace.Recent(10));
            StringAssert.Contains("time went backwards", LiveTrace.Recent(10));
        }

        [Test]
        public void FlushingPutsTheLineOnDiskBeforeTheProcessCanDie()
        {
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.Hold;

            LiveTrace.Write(LiveTraceCategory.Hold, "flush-marker");
            LiveTrace.Flush();

            Assert.That(LiveTrace.LogFilePath, Is.Not.Null.And.Not.Empty);

            // The writer still holds the file, so the reader has to permit it: FileShare.ReadWrite.
            using (var stream = new System.IO.FileStream(LiveTrace.LogFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            using (var reader = new System.IO.StreamReader(stream))
            {
                StringAssert.Contains("flush-marker", reader.ReadToEnd());
            }
        }

        [Test]
        public void FlushAndShutdownAreFreeWhileTracingIsOff()
        {
            Assert.DoesNotThrow(LiveTrace.Flush);

            LiveTrace.Shutdown();

            Assert.DoesNotThrow(LiveTrace.Shutdown);
            Assert.DoesNotThrow(LiveTrace.Flush);
        }

        [Test]
        public void AViolationThatDoesNotHoldIsSilent()
        {
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.All;

            LiveTrace.Invariant(true, "never printed");

            Assert.That(LiveTrace.Recent(10), Is.Empty);
        }
    }
}
