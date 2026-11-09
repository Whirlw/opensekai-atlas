using System.Collections.Generic;
using Sekai.ApiData;

namespace Sekai
{
	public class MasterMaterialExchangeSummary
	{
		public enum Category
		{
			None = 0,
			master_crystal = 1,
			master_piece = 2,
			card_ticket = 3,
			vocal_card_ticket = 4,
			gacha_seal = 5,
			common_ticket = 6,
			home_exchange = 7,
			mysekai_material_game_character = 8
		}

				private int _Id;
		public int Id
		{
		    get => _Id;
		    set => _Id = value;
		}

				private long _StartAt;
		public long StartAt
		{
		    get => _StartAt;
		    set => _StartAt = value;
		}

				private long _EndAt;
		public long EndAt
		{
		    get => _EndAt;
		    set => _EndAt = value;
		}

				private List<MasterMaterialExchange> _MaterialExchanges;
		public List<MasterMaterialExchange> MaterialExchanges
		{
		    get => _MaterialExchanges;
		    set => _MaterialExchanges = value;
		}

				private string _Name;
		public string Name
		{
		    get => _Name;
		    set => _Name = value;
		}

				private string _ExchangeCategory;
		public string ExchangeCategory
		{
		    get => _ExchangeCategory;
		    set => _ExchangeCategory = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private string _materialExchangeType;
		public string materialExchangeType
		{
		    get => _materialExchangeType;
		    set => _materialExchangeType = value;
		}

				private int _notificationRemainHour;
		public int notificationRemainHour
		{
		    get => _notificationRemainHour;
		    set => _notificationRemainHour = value;
		}

				private int _materialExchangeDisplayResourceGroupId;
		public int materialExchangeDisplayResourceGroupId
		{
		    get => _materialExchangeDisplayResourceGroupId;
		    set => _materialExchangeDisplayResourceGroupId = value;
		}

				private MaterialExchangeDisplayResourceGroup[] _materialExchangeDisplayResourceGroups;
		public MaterialExchangeDisplayResourceGroup[] materialExchangeDisplayResourceGroups
		{
		    get => _materialExchangeDisplayResourceGroups;
		    set => _materialExchangeDisplayResourceGroups = value;
		}

				private MaterialExchangeFreebieGroupJson _materialExchangeFreebieGroupJson;
		public MaterialExchangeFreebieGroupJson materialExchangeFreebieGroupJson
		{
		    get => _materialExchangeFreebieGroupJson;
		    set => _materialExchangeFreebieGroupJson = value;
		}

				private MaterialExchangeFreebie[] _materialExchangeFreebies;
		public MaterialExchangeFreebie[] materialExchangeFreebies
		{
		    get => _materialExchangeFreebies;
		    set => _materialExchangeFreebies = value;
		}

		public MaterialExchangeType ExchangeType
		{
			get
			{
				throw null;
			}
		}

		public Category ExchangeCategoryType
		{
			get
			{
				throw null;
			}
		}

		public MasterMaterialExchangeSummary()
		{
		}
	}
}
