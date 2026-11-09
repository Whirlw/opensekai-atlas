using System.Collections.Generic;

namespace Sekai.ApiData
{
	public class CustomMusicScorePublishedSearchListResponse
	{
				private List<UserCustomMusicScorePublishedResponse> _userCustomMusicScorePublishedList;
		public List<UserCustomMusicScorePublishedResponse> userCustomMusicScorePublishedList
		{
		    get => _userCustomMusicScorePublishedList;
		    set => _userCustomMusicScorePublishedList = value;
		}

				private List<CustomMusicScoreOfficialCreatorPublishedResponse> _customMusicScoreOfficialCreatorPublishedList;
		public List<CustomMusicScoreOfficialCreatorPublishedResponse> customMusicScoreOfficialCreatorPublishedList
		{
		    get => _customMusicScoreOfficialCreatorPublishedList;
		    set => _customMusicScoreOfficialCreatorPublishedList = value;
		}

		public CustomMusicScorePublishedSearchListResponse()
		{
			throw null;
		}
	}
}
