
namespace Sekai
{
	public class MasterShopItemCost
	{
				private int _shopItemId;
		public int shopItemId
		{
		    get => _shopItemId;
		    set => _shopItemId = value;
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

		public MasterShopItemCost()
		{
			throw null;
		}
	}
}
