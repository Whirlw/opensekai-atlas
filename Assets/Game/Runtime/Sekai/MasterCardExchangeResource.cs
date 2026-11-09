
namespace Sekai
{
	public class MasterCardExchangeResource
	{
		public string cardRarityType;

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
		}

		public CardRarityType CardRarityType
		{
			get
			{
				throw null;
			}
		}

		public MasterCardExchangeResource()
		{
		}
	}
}
