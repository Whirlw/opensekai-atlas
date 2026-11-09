
namespace Sekai
{
	public class UserCharacterCostume3D
	{
				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private string _unit;
		public string unit
		{
		    get => _unit;
		    set => _unit = value;
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

		public UnitType UnitType
		{
			get
			{
				throw null;
			}
		}

		public UserCharacterCostume3D()
		{
		}

		public UserCharacterCostume3D(int characterId, string unit, int headCostume3dId, int hairCostume3dId, int bodyCostume3dId)
		{
			throw null;
		}
	}
}
