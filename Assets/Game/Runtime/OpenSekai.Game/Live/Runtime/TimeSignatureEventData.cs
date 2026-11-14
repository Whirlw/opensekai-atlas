namespace Sekai.Live
{
	public class TimeSignatureEventData : MusicBaseEventData
	{
				private float _timeSignature;
		public float timeSignature
		{
		    get => _timeSignature;
		    set => _timeSignature = value;
		}

		public TimeSignatureEventData(int barIndex, float timeSignature)
			: base(barIndex)
		{
			this.timeSignature = timeSignature;
		}
	}
}
