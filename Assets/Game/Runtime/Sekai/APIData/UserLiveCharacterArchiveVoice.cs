using System.Collections.Generic;

namespace Sekai.ApiData
{
	public class UserLiveCharacterArchiveVoice
	{
				private List<int> _characterArchiveVoiceGroupIds;
		public List<int> characterArchiveVoiceGroupIds
		{
		    get => _characterArchiveVoiceGroupIds;
		    set => _characterArchiveVoiceGroupIds = value;
		}

		public UserLiveCharacterArchiveVoice()
		{
		}
	}
}
