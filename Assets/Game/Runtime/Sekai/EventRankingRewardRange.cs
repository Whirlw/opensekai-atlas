
namespace Sekai
{
	public class EventRankingRewardRange
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _eventId;
		public int eventId
		{
		    get => _eventId;
		    set => _eventId = value;
		}

				private int _fromRank;
		public int fromRank
		{
		    get => _fromRank;
		    set => _fromRank = value;
		}

				private int _toRank;
		public int toRank
		{
		    get => _toRank;
		    set => _toRank = value;
		}

				private EventRankingReward[] _eventRankingRewards;
		public EventRankingReward[] eventRankingRewards
		{
		    get => _eventRankingRewards;
		    set => _eventRankingRewards = value;
		}

		public EventRankingRewardRange()
		{
		}
	}
}
