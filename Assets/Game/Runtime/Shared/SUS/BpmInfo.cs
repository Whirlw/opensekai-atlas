namespace Sekai.SUS
{
	public class BpmInfo
	{
				private float _bpm;
		public float bpm
		{
		    get => _bpm;
		    set => _bpm = value;
		}

				private int _bar;
		public int bar
		{
		    get => _bar;
		    set => _bar = value;
		}

				private float _barProgress;
		public float barProgress
		{
		    get => _barProgress;
		    set => _barProgress = value;
		}

		public BpmInfo(float bpm, int bar, float barProgress)
		{
			this.bpm = bpm;
			this.bar = bar;
			this.barProgress = barProgress;
		}
	}
}
