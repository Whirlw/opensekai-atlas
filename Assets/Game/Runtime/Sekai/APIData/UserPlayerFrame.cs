using System.Runtime.CompilerServices;
using MessagePack;
using Sekai;

namespace Sekai.ApiData
{
	public class UserPlayerFrame : IMessagePackSerializationCallbackReceiver
	{
				private int _playerFrameId;
		public int playerFrameId
		{
		    get => _playerFrameId;
		    set => _playerFrameId = value;
		}

		public string playerFrameAttachStatus;

		public PlayerFrameAttachStatus PlayerFrameAttachStatus
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

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public void InitPlayerFrameAttachStatus()
		{
			throw null;
		}

		public UserPlayerFrame()
		{
		}
	}
}
