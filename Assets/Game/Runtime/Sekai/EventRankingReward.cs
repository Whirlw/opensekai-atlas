using MessagePack;

namespace Sekai
{
	public class EventRankingReward : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _eventRankingRewardRangeId;
		public int eventRankingRewardRangeId
		{
		    get => _eventRankingRewardRangeId;
		    set => _eventRankingRewardRangeId = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
		}

				private string _rewardConditionType;
		public string rewardConditionType
		{
		    get => _rewardConditionType;
		    set => _rewardConditionType = value;
		}

				private int _conditionValue;
		public int conditionValue
		{
		    get => _conditionValue;
		    set => _conditionValue = value;
		}

				private EventRankingRewardConditionType _RewardConditionType;
		public EventRankingRewardConditionType RewardConditionType
		{
		    get => _RewardConditionType;
		    set => _RewardConditionType = value;
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public EventRankingReward()
		{
		}
	}
}
