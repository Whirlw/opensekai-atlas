
namespace Sekai
{
	public class MasterSkillEnhanceCondition
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

				private string _unit;
		public string unit
		{
		    get => _unit;
		    set => _unit = value;
		}

		public UnitType UnitType
		{
			get
			{
				throw null;
			}
		}

		public MasterSkillEnhanceCondition()
		{
		}
	}
}
