using System.Collections.Generic;

namespace Sekai.ApiData
{
	public class UserCustomMusicScoreDraftListResponse
	{
				private List<UserCustomMusicScoreDraft> _userCustomMusicScoreDrafts;
		public List<UserCustomMusicScoreDraft> userCustomMusicScoreDrafts
		{
		    get => _userCustomMusicScoreDrafts;
		    set => _userCustomMusicScoreDrafts = value;
		}

		public UserCustomMusicScoreDraftListResponse()
		{
		}
	}
}
