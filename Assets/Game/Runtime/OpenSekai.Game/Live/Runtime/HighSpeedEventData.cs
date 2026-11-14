namespace Sekai.Live
{
	public class HighSpeedEventData : MusicBaseEventData
	{
				private int _tickOffset;
		public int tickOffset
		{
		    get => _tickOffset;
		    set => _tickOffset = value;
		}

				private float _speedRatio;
		public float speedRatio
		{
		    get => _speedRatio;
		    set => _speedRatio = value;
		}

		public HighSpeedEventData(int barIndex, int tickOffset, float speedRatio)
			: base(barIndex)
		{
			this.tickOffset = tickOffset;
			this.speedRatio = speedRatio;
		}
	}
}
