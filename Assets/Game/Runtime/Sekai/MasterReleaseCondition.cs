using System;
using MessagePack;

namespace Sekai
{
	public class MasterReleaseCondition : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _sentence;
		public string sentence
		{
		    get => _sentence;
		    set => _sentence = value;
		}

		public string releaseConditionType;

				private int _releaseConditionTypeId;
		public int releaseConditionTypeId
		{
		    get => _releaseConditionTypeId;
		    set => _releaseConditionTypeId = value;
		}

				private int? _releaseConditionTypeId2;
		public int? releaseConditionTypeId2
		{
		    get => _releaseConditionTypeId2;
		    set => _releaseConditionTypeId2 = value;
		}

				private int _releaseConditionTypeLevel;
		public int releaseConditionTypeLevel
		{
		    get => _releaseConditionTypeLevel;
		    set => _releaseConditionTypeLevel = value;
		}

				private int _releaseConditionTypeQuantity;
		public int releaseConditionTypeQuantity
		{
		    get => _releaseConditionTypeQuantity;
		    set => _releaseConditionTypeQuantity = value;
		}

		public long releaseConditionTypeDate;

		public ReleaseConditionType ReleaseConditionType
		{
			get
			{
				throw null;
			}
		}

		public DateTime ReleaseConditionTypeDate
		{
			get
			{
				throw null;
			}
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterReleaseCondition()
		{
			throw null;
		}
	}
}
