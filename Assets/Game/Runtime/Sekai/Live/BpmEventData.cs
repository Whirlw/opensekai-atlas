namespace Sekai.Live
{
	public class BpmEventData : MusicBaseEventData
	{
				private float _barProgress;
		public float barProgress
		{
		    get => _barProgress;
		    set => _barProgress = value;
		}

				private float _bpm;
		public float bpm
		{
		    get => _bpm;
		    set => _bpm = value;
		}

		public BpmEventData(int barIndex, float barProgress, float bpm)
			: base(barIndex)
		{
			this.barProgress = barProgress;
			this.bpm = bpm;
		}
	}
}
