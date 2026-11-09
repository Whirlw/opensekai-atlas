
namespace Sekai
{
	public class MasterShopItem
	{
		public const string TYPE_NORMAL = "area_item";

		public const string TYPE_MUSIC = "music";

		public const string TYPE_MUSIC_VOCAL = "music_vocal";

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _shopId;
		public int shopId
		{
		    get => _shopId;
		    set => _shopId = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _releaseConditionId;
		public int releaseConditionId
		{
		    get => _releaseConditionId;
		    set => _releaseConditionId = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
		}

				private long _startAt;
		public long startAt
		{
		    get => _startAt;
		    set => _startAt = value;
		}

				private long? _endAt;
		public long? endAt
		{
		    get => _endAt;
		    set => _endAt = value;
		}

				private MasterShopItemCost[] _costs;
		public MasterShopItemCost[] costs
		{
		    get => _costs;
		    set => _costs = value;
		}

		public MasterShopItem()
		{
			throw null;
		}
	}
}
