using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterCostume3DModel : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _costume3dId;
		public int costume3dId
		{
		    get => _costume3dId;
		    set => _costume3dId = value;
		}

		public string unit;

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

		public string headCostume3dAssetbundleType;

				private string _colorAssetbundleName;
		public string colorAssetbundleName
		{
		    get => _colorAssetbundleName;
		    set => _colorAssetbundleName = value;
		}

				private string _thumbnailAssetbundleName;
		public string thumbnailAssetbundleName
		{
		    get => _thumbnailAssetbundleName;
		    set => _thumbnailAssetbundleName = value;
		}

				private string _part;
		public string part
		{
		    get => _part;
		    set => _part = value;
		}

		public UnitType Unit
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

		public HeadCostume3dAssetbundleType HeadCostume3dAssetbundleType
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

		public MasterCostume3DModel()
		{
		}
	}
}
