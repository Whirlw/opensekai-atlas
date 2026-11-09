using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class UserCardEpisode : IMessagePackSerializationCallbackReceiver
	{
				private int _cardEpisodeId;
		public int cardEpisodeId
		{
		    get => _cardEpisodeId;
		    set => _cardEpisodeId = value;
		}

		public string scenarioStatus;

		public string[] scenarioStatusReasons;

				private bool _isNotSkipped;
		public bool isNotSkipped
		{
		    get => _isNotSkipped;
		    set => _isNotSkipped = value;
		}

		public EpisodeStatus ScenarioStatus
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

		public bool IsReleased
		{
			get
			{
				throw null;
			}
		}

		public bool IsAlreadyRead
		{
			get
			{
				throw null;
			}
		}

		public EpisodeStatusReason[] ScenarioStatusReasons
		{
			get
			{
				throw null;
			}
		}

		public UserCardEpisode(UserCardEpisode source)
		{
			throw null;
		}

		public UserCardEpisode()
		{
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}
	}
}
