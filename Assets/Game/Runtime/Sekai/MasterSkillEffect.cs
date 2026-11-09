
namespace Sekai
{
	public class MasterSkillEffect
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

		public string skillEffectType;

				private string _shortDescription;
		public string shortDescription
		{
		    get => _shortDescription;
		    set => _shortDescription = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

		public string activateNotesJudgmentType;

				private int _activateLife;
		public int activateLife
		{
		    get => _activateLife;
		    set => _activateLife = value;
		}

		public string conditionType;

				private MasterSkillEffectDetail[] _skillEffectDetails;
		public MasterSkillEffectDetail[] skillEffectDetails
		{
		    get => _skillEffectDetails;
		    set => _skillEffectDetails = value;
		}

				private MasterSkillEnhance _skillEnhance;
		public MasterSkillEnhance skillEnhance
		{
		    get => _skillEnhance;
		    set => _skillEnhance = value;
		}

				private int _activateCharacterRank;
		public int activateCharacterRank
		{
		    get => _activateCharacterRank;
		    set => _activateCharacterRank = value;
		}

				private int _activateUnitCount;
		public int activateUnitCount
		{
		    get => _activateUnitCount;
		    set => _activateUnitCount = value;
		}

		public SkillEffectType SkillEffectType
		{
			get
			{
				throw null;
			}
		}

		public ActivateNotesJudgmentType ActivateNotesJudgmentType
		{
			get
			{
				throw null;
			}
		}

		public ConditionType ConditionType
		{
			get
			{
				throw null;
			}
		}

		public MasterSkillEffect()
		{
		}
	}
}
