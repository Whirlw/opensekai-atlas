namespace Sekai.Live
{
	public class MusicBaseEventData
	{
				private int _barIndex;
		public int barIndex
		{
		    get => _barIndex;
		    set => _barIndex = value;
		}

		public MusicBaseEventData(int barIndex)
		{
			this.barIndex = barIndex;
		}
	}
}
