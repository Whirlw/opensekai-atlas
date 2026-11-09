using System.Collections.Generic;
using Sekai.Core.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    internal sealed class FakeTouchSource : ILiveTouchSource
    {
        private readonly List<LiveTouchSample> samples = new List<LiveTouchSample>();

        public int Count => samples.Count;
        public LiveTouchSample this[int index] => samples[index];

        internal void Add(in LiveTouchSample sample) => samples.Add(sample);
        internal void Clear() => samples.Clear();
    }
}
