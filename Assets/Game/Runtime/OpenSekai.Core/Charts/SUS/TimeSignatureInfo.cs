namespace Sekai.SUS
{
	public class TimeSignatureInfo
	{
				private float _TimeSignature;
		public float TimeSignature
		{
		    get => _TimeSignature;
		    set => _TimeSignature = value;
		}

				private int _StartBarIndex;
		public int StartBarIndex
		{
		    get => _StartBarIndex;
		    set => _StartBarIndex = value;
		}

		public TimeSignatureInfo(float timeSignature, int startBarIndex)
		{
			TimeSignature = timeSignature;
			StartBarIndex = startBarIndex;
		}
	}
}
