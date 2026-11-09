using System.Collections.Generic;

namespace Sekai.ApiData
{
	public class MasterMaterialExchangeRelationParent
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private List<MasterMaterialExchangeRelationChild> _materialExchangeRelationChildren;
		public List<MasterMaterialExchangeRelationChild> materialExchangeRelationChildren
		{
		    get => _materialExchangeRelationChildren;
		    set => _materialExchangeRelationChildren = value;
		}

		public MasterMaterialExchangeRelationParent()
		{
		}
	}
}
