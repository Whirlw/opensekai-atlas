using System.Runtime.CompilerServices;
using MessagePack;
using Sekai;

namespace Sekai.ApiData
{
	public class MasterMysekaiItem : IMessagePackSerializationCallbackReceiver
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

		public string mysekaiItemType;

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

		public MysekaiItemType MysekaiItemType
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

		public MasterMysekaiItem()
		{
		}
	}
}
