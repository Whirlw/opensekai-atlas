namespace OpenSekai.Charts
{
    public enum ChartEventType { BPM = 0, HighSpeed = 1, SeVolume = 2, TimeSignature = 3 }
    public sealed class ChartEvent
    {
                private int _id;
        public int id
        {
            get => _id;
            set => _id = value;
        }
        public ChartEventType eventType;
                private long _ticks;
        public long ticks
        {
            get => _ticks;
            set => _ticks = value;
        }
                private object _changeValue;
        public object changeValue
        {
            get => _changeValue;
            set => _changeValue = value;
        }
        public ChartEvent Copy() => (ChartEvent)MemberwiseClone();
    }
}
