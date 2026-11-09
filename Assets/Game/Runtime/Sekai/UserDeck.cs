
namespace Sekai
{
	public class UserDeck
	{
				private long _userId;
		public long userId
		{
		    get => _userId;
		    set => _userId = value;
		}

				private int _deckId;
		public int deckId
		{
		    get => _deckId;
		    set => _deckId = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private int _leader;
		public int leader
		{
		    get => _leader;
		    set => _leader = value;
		}

				private int _subLeader;
		public int subLeader
		{
		    get => _subLeader;
		    set => _subLeader = value;
		}

				private int _member1;
		public int member1
		{
		    get => _member1;
		    set => _member1 = value;
		}

				private int _member2;
		public int member2
		{
		    get => _member2;
		    set => _member2 = value;
		}

				private int _member3;
		public int member3
		{
		    get => _member3;
		    set => _member3 = value;
		}

				private int _member4;
		public int member4
		{
		    get => _member4;
		    set => _member4 = value;
		}

				private int _member5;
		public int member5
		{
		    get => _member5;
		    set => _member5 = value;
		}

		public UserDeck()
		{
		}

		public UserDeck(long userId, int deckId, string deckName, int[] cardIds)
		{
			throw null;
		}
	}
}
