
namespace Sekai
{
	public class UserCard
	{
		public const string SPECIAL_TRAINING_DONE = "done";

		public const string SPECIAL_TRAINING_DO_NOTHING = "not_doing";

		public const string DEFAULT_IMAGE_ORIGINAL = "original";

		public const string DEFAULT_IMAGE_SPECIAL_TRAINING = "special_training";

		public const string DEFAULT_IMAGE_NORMAL = "normal";

				private long _userId;
		public long userId
		{
		    get => _userId;
		    set => _userId = value;
		}

				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private int _exp;
		public int exp
		{
		    get => _exp;
		    set => _exp = value;
		}

				private int _totalExp;
		public int totalExp
		{
		    get => _totalExp;
		    set => _totalExp = value;
		}

				private int _skillLevel;
		public int skillLevel
		{
		    get => _skillLevel;
		    set => _skillLevel = value;
		}

				private int _skillExp;
		public int skillExp
		{
		    get => _skillExp;
		    set => _skillExp = value;
		}

				private int _totalSkillExp;
		public int totalSkillExp
		{
		    get => _totalSkillExp;
		    set => _totalSkillExp = value;
		}

				private int _masterRank;
		public int masterRank
		{
		    get => _masterRank;
		    set => _masterRank = value;
		}

				private string _specialTrainingStatus;
		public string specialTrainingStatus
		{
		    get => _specialTrainingStatus;
		    set => _specialTrainingStatus = value;
		}

				private string _defaultImage;
		public string defaultImage
		{
		    get => _defaultImage;
		    set => _defaultImage = value;
		}

				private int _duplicateCount;
		public int duplicateCount
		{
		    get => _duplicateCount;
		    set => _duplicateCount = value;
		}

				private long _createdAt;
		public long createdAt
		{
		    get => _createdAt;
		    set => _createdAt = value;
		}

				private UserCardEpisode[] _episodes;
		public UserCardEpisode[] episodes
		{
		    get => _episodes;
		    set => _episodes = value;
		}

		public bool DoneSpecialTraining
		{
			get
			{
				throw null;
			}
		}

		public TrainingStatus TrainingStatus
		{
			get
			{
				throw null;
			}
		}

		public UserCard(UserCard source)
		{
			throw null;
		}

		public UserCard(AnotherUserCard source)
		{
			throw null;
		}

		public UserCard()
		{
		}
	}
}
