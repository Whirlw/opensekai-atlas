namespace Sekai.Live
{
	public class VolumeEventData : MusicBaseEventData
	{
				private int _tickOffset;
		public int tickOffset
		{
		    get => _tickOffset;
		    set => _tickOffset = value;
		}

				private float _seVolume;
		public float seVolume
		{
		    get => _seVolume;
		    set => _seVolume = value;
		}

		public VolumeEventData(int barIndex, int tickOffset, float seVolume)
			: base(barIndex)
		{
			this.tickOffset = tickOffset;
			this.seVolume = seVolume;
		}
	}
}
