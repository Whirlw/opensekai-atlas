
namespace Sekai
{
	public class MasterSkill
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _skillFilterId;
		public int skillFilterId
		{
		    get => _skillFilterId;
		    set => _skillFilterId = value;
		}

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

				private string _descriptionSpriteName;
		public string descriptionSpriteName
		{
		    get => _descriptionSpriteName;
		    set => _descriptionSpriteName = value;
		}

				private MasterSkillEffect[] _skillEffects;
		public MasterSkillEffect[] skillEffects
		{
		    get => _skillEffects;
		    set => _skillEffects = value;
		}

		public SkillSpriteType SkillSpriteType
		{
			get
			{
				throw null;
			}
		}

		public SkillFilterType SkillFilterType
		{
			get
			{
				throw null;
			}
		}

		public MasterSkill()
		{
		}
	}
}
