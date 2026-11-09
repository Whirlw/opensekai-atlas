
namespace Sekai
{
	public class UserShopItem
	{
		public const string STATUS_SALE = "sale";

		public const string STATUS_FORBIDDEN = "forbidden";

		public const string STATUS_SOLD_OUT = "sold_out";

				private int _shopItemId;
		public int shopItemId
		{
		    get => _shopItemId;
		    set => _shopItemId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private string _status;
		public string status
		{
		    get => _status;
		    set => _status = value;
		}

		public bool IsSale
		{
			get
			{
				throw null;
			}
		}

		public UserShopItem()
		{
			throw null;
		}
	}
}
