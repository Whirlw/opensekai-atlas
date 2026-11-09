using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MVOnlyDeckCostumeLocalData : IMessagePackSerializationCallbackReceiver
	{
				private int _memberIndex;
		public int memberIndex
		{
		    get => _memberIndex;
		    set => _memberIndex = value;
		}

				private int _headCostume3dId;
		public int headCostume3dId
		{
		    get => _headCostume3dId;
		    set => _headCostume3dId = value;
		}

				private int _hairCostume3dId;
		public int hairCostume3dId
		{
		    get => _hairCostume3dId;
		    set => _hairCostume3dId = value;
		}

				private int _bodyCostume3dId;
		public int bodyCostume3dId
		{
		    get => _bodyCostume3dId;
		    set => _bodyCostume3dId = value;
		}

				private string _unit;
		public string unit
		{
		    get => _unit;
		    set => _unit = value;
		}

		public UnitType UnitType
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

		public MVOnlyDeckCostumeLocalData()
		{
			throw null;
		}

		public MVOnlyDeckCostumeLocalData(int memberIndex, UnitType unitType)
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

		public void SetUnitType(UnitType unitType)
		{
			throw null;
		}
	}
}
