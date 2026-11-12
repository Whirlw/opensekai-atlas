
namespace Sekai
{
	public class IngameLotterySkill
	{
				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int? _relationCardId;
		public int? relationCardId
		{
		    get => _relationCardId;
		    set => _relationCardId = value;
		}

				private int _ingameCutinCharacterId;
		public int ingameCutinCharacterId
		{
		    get => _ingameCutinCharacterId;
		    set => _ingameCutinCharacterId = value;
		}

		public IngameLotterySkill()
		{
		}
	}
}
