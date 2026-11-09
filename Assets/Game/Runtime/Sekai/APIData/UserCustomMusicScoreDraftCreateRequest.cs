
namespace Sekai.ApiData
{
	public class UserCustomMusicScoreDraftCreateRequest
	{
				private string _baseMusicScoreId;
		public string baseMusicScoreId
		{
		    get => _baseMusicScoreId;
		    set => _baseMusicScoreId = value;
		}

				private int _musicId;
		public int musicId
		{
		    get => _musicId;
		    set => _musicId = value;
		}

				private string _title;
		public string title
		{
		    get => _title;
		    set => _title = value;
		}

				private string _userCustomMusicScoreJsonGzipBase64;
		public string userCustomMusicScoreJsonGzipBase64
		{
		    get => _userCustomMusicScoreJsonGzipBase64;
		    set => _userCustomMusicScoreJsonGzipBase64 = value;
		}

				private string _memo;
		public string memo
		{
		    get => _memo;
		    set => _memo = value;
		}

				private int _baseMusicDifficultyId;
		public int baseMusicDifficultyId
		{
		    get => _baseMusicDifficultyId;
		    set => _baseMusicDifficultyId = value;
		}

		public UserCustomMusicScoreDraftCreateRequest()
		{
			throw null;
		}
	}
}
