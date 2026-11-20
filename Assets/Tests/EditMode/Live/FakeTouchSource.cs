using System.Collections.Generic;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    internal sealed class FakeTouchSource : ILiveTouchSource
    {
        private readonly List<LiveTouchSample> _samples = new List<LiveTouchSample>();

        public int Count => _samples.Count;
        public LiveTouchSample this[int index] => _samples[index];

        internal void Add(in LiveTouchSample sample) => _samples.Add(sample);
        internal void Clear() => _samples.Clear();
    }
}
