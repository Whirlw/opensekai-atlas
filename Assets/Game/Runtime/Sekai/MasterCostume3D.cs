using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterCostume3D : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _costume3dGroupId;
		public int costume3dGroupId
		{
		    get => _costume3dGroupId;
		    set => _costume3dGroupId = value;
		}

				private string _costume3dType;
		public string costume3dType
		{
		    get => _costume3dType;
		    set => _costume3dType = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _partType;
		public string partType
		{
		    get => _partType;
		    set => _partType = value;
		}

				private int _colorId;
		public int colorId
		{
		    get => _colorId;
		    set => _colorId = value;
		}

				private string _colorName;
		public string colorName
		{
		    get => _colorName;
		    set => _colorName = value;
		}

				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private string _costume3dRarity;
		public string costume3dRarity
		{
		    get => _costume3dRarity;
		    set => _costume3dRarity = value;
		}

				private string _howToObtain;
		public string howToObtain
		{
		    get => _howToObtain;
		    set => _howToObtain = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private string _designer;
		public string designer
		{
		    get => _designer;
		    set => _designer = value;
		}

				private long _publishedAt;
		public long publishedAt
		{
		    get => _publishedAt;
		    set => _publishedAt = value;
		}

				private long _archivePublishedAt;
		public long archivePublishedAt
		{
		    get => _archivePublishedAt;
		    set => _archivePublishedAt = value;
		}

				private string _archiveDisplayType;
		public string archiveDisplayType
		{
		    get => _archiveDisplayType;
		    set => _archiveDisplayType = value;
		}

		public CostumePartType CostumePartType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
			throw null;
		}

		public MasterCostume3D()
		{
		}
	}
}
