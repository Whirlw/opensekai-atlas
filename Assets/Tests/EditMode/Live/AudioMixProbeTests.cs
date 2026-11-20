using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class AudioMixProbeTests
    {
        [Test]
        public void UsesTheFirstBlockWithSignalAndLeavesTheBufferIntact()
        {
            long tick = Stopwatch.Frequency;
            var ticket = new AudioMixProbe.Ticket { PlayTicks = tick };
            ticket.ObserveBlock(new[] { 0f, 0.000001f }, tick + 1);
            Assert.That(ticket.HasAudio, Is.False);
            Assert.That(double.IsNaN(ticket.DelayMs), Is.True);

            var signal = new[] { 0f, -0.2f };
            ticket.ObserveBlock(signal, 2 * tick);
            ticket.ObserveBlock(new[] { 0.2f }, 3 * tick);
            Assert.That(ticket.DelayMs, Is.EqualTo(1000d));
            Assert.That(signal, Is.EqualTo(new[] { 0f, -0.2f }));
        }
    }
}
