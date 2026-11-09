
namespace Sekai
{
	public class MasterMasterLessonAchieveResource
	{
				private int _releaseConditionId;
		public int releaseConditionId
		{
		    get => _releaseConditionId;
		    set => _releaseConditionId = value;
		}

				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int _masterRank;
		public int masterRank
		{
		    get => _masterRank;
		    set => _masterRank = value;
		}

				private UserResource[] _resources;
		public UserResource[] resources
		{
		    get => _resources;
		    set => _resources = value;
		}

		public MasterMasterLessonAchieveResource()
		{
		}
	}
}
