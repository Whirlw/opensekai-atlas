
namespace Sekai
{
	public class MasterSpecialTrainingCost
	{
				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private UserResource _cost;
		public UserResource cost
		{
		    get => _cost;
		    set => _cost = value;
		}

		public MasterSpecialTrainingCost()
		{
		}
	}
}
