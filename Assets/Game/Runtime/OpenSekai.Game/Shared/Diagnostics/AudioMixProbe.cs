using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OpenSekai.Shared.Diagnostics
{
    // Only the isolated, single-source playback objects use this filter.
    internal sealed class AudioMixProbe : MonoBehaviour
    {
        private Ticket _ticket;

        internal void Arm(Ticket ticket)
        {
            ticket.PlayTicks = Stopwatch.GetTimestamp();
            Volatile.Write(ref _ticket, ticket);
        }

        internal void Disarm() => Volatile.Write(ref _ticket, null);

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Ticket ticket = Volatile.Read(ref _ticket);
            if (ticket != null && !ticket.HasAudio)
                ticket.ObserveBlock(data, Stopwatch.GetTimestamp());
        }

        internal sealed class Ticket
        {
            private const float SignalThreshold = 0.00001f;
            private long _firstSignalTicks;

            internal int RequestSequence;
            internal long PlayTicks;

            internal bool HasAudio => Interlocked.Read(ref _firstSignalTicks) != 0;

            internal void ObserveBlock(float[] data, long callbackTicks)
            {
                // Scan the interleaved buffer without modifying the samples.
                foreach (float sample in data)
                {
                    if (sample > SignalThreshold || sample < -SignalThreshold)
                    {
                        Interlocked.CompareExchange(ref _firstSignalTicks, callbackTicks, 0);
                        return;
                    }
                }
            }

            internal double DelayMs
            {
                get
                {
                    long firstSignal = Interlocked.Read(ref _firstSignalTicks);
                    return firstSignal == 0 ? double.NaN : (firstSignal - PlayTicks) * (1000d / Stopwatch.Frequency);
                }
            }
        }
    }
}
