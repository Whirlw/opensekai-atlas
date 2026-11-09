
namespace Sekai
{
	public class AnotherUserCard
	{
				private int _cardId;
		public int cardId
		{
		    get => _cardId;
		    set => _cardId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private int _masterRank;
		public int masterRank
		{
		    get => _masterRank;
		    set => _masterRank = value;
		}

				private string _specialTrainingStatus;
		public string specialTrainingStatus
		{
		    get => _specialTrainingStatus;
		    set => _specialTrainingStatus = value;
		}

				private string _defaultImage;
		public string defaultImage
		{
		    get => _defaultImage;
		    set => _defaultImage = value;
		}

		public AnotherUserCard()
		{
		}
	}
}
