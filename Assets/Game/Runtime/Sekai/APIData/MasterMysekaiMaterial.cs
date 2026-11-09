using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MessagePack;
using Sekai;

namespace Sekai.ApiData
{
	public class MasterMysekaiMaterial : IMessagePackSerializationCallbackReceiver
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

		public string mysekaiMaterialType;

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _pronunciation;
		public string pronunciation
		{
		    get => _pronunciation;
		    set => _pronunciation = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

		public string mysekaiMaterialRarityType;

				private string _iconAssetbundleName;
		public string iconAssetbundleName
		{
		    get => _iconAssetbundleName;
		    set => _iconAssetbundleName = value;
		}

				private string _modelAssetbundleName;
		public string modelAssetbundleName
		{
		    get => _modelAssetbundleName;
		    set => _modelAssetbundleName = value;
		}

				private int _mysekaiPhenomenaGroupId;
		public int mysekaiPhenomenaGroupId
		{
		    get => _mysekaiPhenomenaGroupId;
		    set => _mysekaiPhenomenaGroupId = value;
		}

				private List<int> _mysekaiSiteIds;
		public List<int> mysekaiSiteIds
		{
		    get => _mysekaiSiteIds;
		    set => _mysekaiSiteIds = value;
		}

		public MysekaiMaterialType MysekaiMaterialType
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

		public MysekaiMaterialRarityType MysekaiMaterialRarityType
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

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterMysekaiMaterial()
		{
		}
	}
}
