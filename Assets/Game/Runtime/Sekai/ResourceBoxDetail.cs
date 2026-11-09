using System.Runtime.CompilerServices;
using Sekai.Constants;
using MessagePack;

namespace Sekai
{
	public class ResourceBoxDetail : IMessagePackSerializationCallbackReceiver
	{
				private string _resourceBoxPurpose;
		public string resourceBoxPurpose
		{
		    get => _resourceBoxPurpose;
		    set => _resourceBoxPurpose = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
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

				private int _resourceLevel;
		public int resourceLevel
		{
		    get => _resourceLevel;
		    set => _resourceLevel = value;
		}

				private int _resourceQuantity;
		public int resourceQuantity
		{
		    get => _resourceQuantity;
		    set => _resourceQuantity = value;
		}

		public int Costume3dId
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

		public int Costume3dGroupId
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

		public CostumePartType PartType
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

		public FigureType Figure
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

		public int ColorId
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

		public ResourceType ResourceType
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

		public void CostumeSetup()
		{
			throw null;
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public ResourceBoxDetail()
		{
		}
	}
}
