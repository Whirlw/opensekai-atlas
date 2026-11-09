using System;
using Sekai.Constants;

namespace Sekai.ApiData
{
	public class MasterMaterialExchangeCost : IEquatable<MasterMaterialExchangeCost>
	{
				private int _materialExchangeId;
		public int materialExchangeId
		{
		    get => _materialExchangeId;
		    set => _materialExchangeId = value;
		}

				private int _costGroupId;
		public int costGroupId
		{
		    get => _costGroupId;
		    set => _costGroupId = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

		public string resourceType;

				private int _resourceId;
		public int resourceId
		{
		    get => _resourceId;
		    set => _resourceId = value;
		}

				private int _quantity;
		public int quantity
		{
		    get => _quantity;
		    set => _quantity = value;
		}

		public ResourceType ResourceType
		{
			get
			{
				throw null;
			}
		}

		public bool Equals(MasterMaterialExchangeCost other)
		{
			throw null;
		}

		public MasterMaterialExchangeCost()
		{
		}
	}
}
