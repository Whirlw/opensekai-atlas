using Sekai.Constants;

namespace Sekai
{
	public class MaterialExchangeDisplayResourceGroup
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _groupId;
		public int groupId
		{
		    get => _groupId;
		    set => _groupId = value;
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

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

		public ResourceType ResourceType
		{
			get
			{
				throw null;
			}
		}

		public MaterialExchangeDisplayResourceGroup()
		{
		}
	}
}
