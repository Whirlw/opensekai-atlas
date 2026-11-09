using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class UserHonorMission : IMessagePackSerializationCallbackReceiver
	{
				private long _userId;
		public long userId
		{
		    get => _userId;
		    set => _userId = value;
		}

				private string _honorMissionType;
		public string honorMissionType
		{
		    get => _honorMissionType;
		    set => _honorMissionType = value;
		}

				private int _progress;
		public int progress
		{
		    get => _progress;
		    set => _progress = value;
		}

				private int[] _achievedMissionIds;
		public int[] achievedMissionIds
		{
		    get => _achievedMissionIds;
		    set => _achievedMissionIds = value;
		}

		public HonorMissionType Type
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
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

		public UserHonorMission()
		{
		}
	}
}
