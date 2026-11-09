
namespace Sekai.ApiData
{
	public class MasterMaterialExchangeRelationChild
	{
				private int _materialExchangeId;
		public int materialExchangeId
		{
		    get => _materialExchangeId;
		    set => _materialExchangeId = value;
		}

				private int _materialExchangeCostGroupId;
		public int materialExchangeCostGroupId
		{
		    get => _materialExchangeCostGroupId;
		    set => _materialExchangeCostGroupId = value;
		}

		public MasterMaterialExchangeRelationChild()
		{
		}
	}
}
