using System;
using MessagePack;
using Sekai.SUS;

namespace Sekai
{
	public class MasterEvent : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _eventType;
		public string eventType
		{
		    get => _eventType;
		    set => _eventType = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private string _bgmAssetbundleName;
		public string bgmAssetbundleName
		{
		    get => _bgmAssetbundleName;
		    set => _bgmAssetbundleName = value;
		}

				private long _eventOnlyComponentDisplayStartAt;
		public long eventOnlyComponentDisplayStartAt
		{
		    get => _eventOnlyComponentDisplayStartAt;
		    set => _eventOnlyComponentDisplayStartAt = value;
		}

				private long _eventOnlyComponentDisplayEndAt;
		public long eventOnlyComponentDisplayEndAt
		{
		    get => _eventOnlyComponentDisplayEndAt;
		    set => _eventOnlyComponentDisplayEndAt = value;
		}

				private string _eventPointAssetbundleName;
		public string eventPointAssetbundleName
		{
		    get => _eventPointAssetbundleName;
		    set => _eventPointAssetbundleName = value;
		}

				private long _startAt;
		public long startAt
		{
		    get => _startAt;
		    set => _startAt = value;
		}

				private long _aggregateAt;
		public long aggregateAt
		{
		    get => _aggregateAt;
		    set => _aggregateAt = value;
		}

				private long _rankingAnnounceAt;
		public long rankingAnnounceAt
		{
		    get => _rankingAnnounceAt;
		    set => _rankingAnnounceAt = value;
		}

				private long _distributionStartAt;
		public long distributionStartAt
		{
		    get => _distributionStartAt;
		    set => _distributionStartAt = value;
		}

				private long _closedAt;
		public long closedAt
		{
		    get => _closedAt;
		    set => _closedAt = value;
		}

				private long _distributionEndAt;
		public long distributionEndAt
		{
		    get => _distributionEndAt;
		    set => _distributionEndAt = value;
		}

				private long _standbyScreenDisplayStartAt;
		public long standbyScreenDisplayStartAt
		{
		    get => _standbyScreenDisplayStartAt;
		    set => _standbyScreenDisplayStartAt = value;
		}

				private EventRankingRewardRange[] _eventRankingRewardRanges;
		public EventRankingRewardRange[] eventRankingRewardRanges
		{
		    get => _eventRankingRewardRanges;
		    set => _eventRankingRewardRanges = value;
		}

				private int? _virtualLiveId;
		public int? virtualLiveId
		{
		    get => _virtualLiveId;
		    set => _virtualLiveId = value;
		}

				private string _unit;
		public string unit
		{
		    get => _unit;
		    set => _unit = value;
		}

				private int? _eventBreakTimeId;
		public int? eventBreakTimeId
		{
		    get => _eventBreakTimeId;
		    set => _eventBreakTimeId = value;
		}

				private GameEventType _EventType;
		public GameEventType EventType
		{
		    get => _EventType;
		    set => _EventType = value;
		}

				private DateTime _DisplayableStartTime;
		public DateTime DisplayableStartTime
		{
		    get => _DisplayableStartTime;
		    set => _DisplayableStartTime = value;
		}

				private DateTime _StartTime;
		public DateTime StartTime
		{
		    get => _StartTime;
		    set => _StartTime = value;
		}

				private DateTime _AggregateTime;
		public DateTime AggregateTime
		{
		    get => _AggregateTime;
		    set => _AggregateTime = value;
		}

				private DateTime _RankingAnnounceAt;
		public DateTime RankingAnnounceAt
		{
		    get => _RankingAnnounceAt;
		    set => _RankingAnnounceAt = value;
		}

				private DateTime _EndTime;
		public DateTime EndTime
		{
		    get => _EndTime;
		    set => _EndTime = value;
		}

				private DateTime _StandbyScreenDisplayStartAt;
		public DateTime StandbyScreenDisplayStartAt
		{
		    get => _StandbyScreenDisplayStartAt;
		    set => _StandbyScreenDisplayStartAt = value;
		}

		public UnitType BonusUnitType
		{
			get
			{
				throw null;
			}
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
			throw null;
		}

		public bool IsPlayableEvent()
		{
			throw null;
		}

		public bool IsWorldBloomEvent()
		{
			throw null;
		}

		public bool HasVirtualLive()
		{
			throw null;
		}

		public MasterEvent()
		{
		}
	}
}
