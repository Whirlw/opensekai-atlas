
namespace Sekai
{
	public class UserProfile
	{
				private string _word;
		public string word
		{
		    get => _word;
		    set => _word = value;
		}

				private string _twitterId;
		public string twitterId
		{
		    get => _twitterId;
		    set => _twitterId = value;
		}

		public string profileImageType;

				private int _profileImageId;
		public int profileImageId
		{
		    get => _profileImageId;
		    set => _profileImageId = value;
		}

		public ProfileImageType ProfileImageType
		{
			get
			{
				throw null;
			}
		}

		public UserProfile()
		{
			throw null;
		}
	}
}
