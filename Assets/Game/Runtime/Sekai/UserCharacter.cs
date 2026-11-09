
namespace Sekai
{
	public class UserCharacter
	{
				private long _userId;
		public long userId
		{
		    get => _userId;
		    set => _userId = value;
		}

				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private int _characterRank;
		public int characterRank
		{
		    get => _characterRank;
		    set => _characterRank = value;
		}

				private int _exp;
		public int exp
		{
		    get => _exp;
		    set => _exp = value;
		}

				private int _totalExp;
		public int totalExp
		{
		    get => _totalExp;
		    set => _totalExp = value;
		}

		public UserCharacter()
		{
		}
	}
}
