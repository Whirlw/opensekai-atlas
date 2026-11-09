
namespace Sekai
{
	public class MasterSkillEffectDetail
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _skillEffectId;
		public int skillEffectId
		{
		    get => _skillEffectId;
		    set => _skillEffectId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private float _activateEffectDuration;
		public float activateEffectDuration
		{
		    get => _activateEffectDuration;
		    set => _activateEffectDuration = value;
		}

		public string activateEffectValueType;

				private int _activateEffectValue;
		public int activateEffectValue
		{
		    get => _activateEffectValue;
		    set => _activateEffectValue = value;
		}

				private int _activateEffectValue2;
		public int activateEffectValue2
		{
		    get => _activateEffectValue2;
		    set => _activateEffectValue2 = value;
		}

		public ActivateEffectValueType ActivateEffectValueType
		{
			get
			{
				throw null;
			}
		}

		public MasterSkillEffectDetail()
		{
		}
	}
}
