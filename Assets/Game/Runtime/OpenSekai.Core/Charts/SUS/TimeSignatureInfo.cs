namespace Sekai.SUS
{
	public class TimeSignatureInfo
	{
				private float _timeSignature;
		public float TimeSignature
		{
		    get => _timeSignature;
		    set => _timeSignature = value;
		}

				private int _startBarIndex;
		public int StartBarIndex
		{
		    get => _startBarIndex;
		    set => _startBarIndex = value;
		}

		public TimeSignatureInfo(float timeSignature, int startBarIndex)
		{
			TimeSignature = timeSignature;
			StartBarIndex = startBarIndex;
		}
	}
}
