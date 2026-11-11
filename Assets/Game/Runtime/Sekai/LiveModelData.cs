namespace Sekai
{
	public class LiveModelData
	{
		public bool OriginalMember { get; set; }

		public LiveModelData(LiveDeckData deckData)
			: this(false)
		{
		}

		public LiveModelData(bool isOriginalMember)
		{
			OriginalMember = isOriginalMember;
		}
	}
}
