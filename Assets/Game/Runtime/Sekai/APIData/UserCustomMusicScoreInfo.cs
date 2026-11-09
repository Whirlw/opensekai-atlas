
namespace Sekai.ApiData
{
	public class UserCustomMusicScoreInfo
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

				private string _userCustomMusicScorePath;
		public string userCustomMusicScorePath
		{
		    get => _userCustomMusicScorePath;
		    set => _userCustomMusicScorePath = value;
		}

		public UserCustomMusicScoreInfo()
		{
		}
	}
}
