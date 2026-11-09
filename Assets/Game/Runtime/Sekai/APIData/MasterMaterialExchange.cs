using System.Collections.Generic;
using JetBrains.Annotations;

namespace Sekai.ApiData
{
	public class MasterMaterialExchange
	{
		public const string REFRESH_CYCLE_NONE = "none";

		public const string REFRESH_CYCLE_WEEKLY = "weekly";

		public const string REFRESH_CYCLE_MONTHLY = "monthly";

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _materialExchangeSummaryId;
		public int materialExchangeSummaryId
		{
		    get => _materialExchangeSummaryId;
		    set => _materialExchangeSummaryId = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private string _displayName;
		public string displayName
		{
		    get => _displayName;
		    set => _displayName = value;
		}

				private bool _isDisplayQuantity;
		public bool isDisplayQuantity
		{
		    get => _isDisplayQuantity;
		    set => _isDisplayQuantity = value;
		}

				private string _thumbnailAssetbundleName;
		public string thumbnailAssetbundleName
		{
		    get => _thumbnailAssetbundleName;
		    set => _thumbnailAssetbundleName = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
		}

				private string _refreshCycle;
		public string refreshCycle
		{
		    get => _refreshCycle;
		    set => _refreshCycle = value;
		}

				private int _exchangeLimit;
		public int exchangeLimit
		{
		    get => _exchangeLimit;
		    set => _exchangeLimit = value;
		}

				private long _startAt;
		public long startAt
		{
		    get => _startAt;
		    set => _startAt = value;
		}

				private long _endAt;
		public long endAt
		{
		    get => _endAt;
		    set => _endAt = value;
		}

				private List<MasterMaterialExchangeCost> _costs;
		public List<MasterMaterialExchangeCost> costs
		{
		    get => _costs;
		    set => _costs = value;
		}

				private List<MasterMaterialExchangeRelationParent> _materialExchangeRelationParents;
		public List<MasterMaterialExchangeRelationParent> materialExchangeRelationParents
		{
		    get => _materialExchangeRelationParents;
		    set => _materialExchangeRelationParents = value;
		}

		[CanBeNull]
		public MasterMaterialExchangeCost GetMaterialExchangeCost(int materialExchangeCostGroupId)
		{
			throw null;
		}

		public MasterMaterialExchange()
		{
		}
	}
}
