using MessagePack;

namespace Sekai
{
	public class MasterCharacter3D : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _characterType;
		public string characterType
		{
		    get => _characterType;
		    set => _characterType = value;
		}

				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
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

				private float _lookAtLimitX;
		public float lookAtLimitX
		{
		    get => _lookAtLimitX;
		    set => _lookAtLimitX = value;
		}

				private float _lookAtLimitY;
		public float lookAtLimitY
		{
		    get => _lookAtLimitY;
		    set => _lookAtLimitY = value;
		}

		public UnitType GetUnitType()
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

		public MasterCharacter3D()
		{
		}
	}
}
