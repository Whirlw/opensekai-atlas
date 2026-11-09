
namespace Sekai
{
	public class MasterCardRarity
	{
				private string _cardRarityType;
		public string cardRarityType
		{
		    get => _cardRarityType;
		    set => _cardRarityType = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _maxLevel;
		public int maxLevel
		{
		    get => _maxLevel;
		    set => _maxLevel = value;
		}

				private int _trainingMaxLevel;
		public int trainingMaxLevel
		{
		    get => _trainingMaxLevel;
		    set => _trainingMaxLevel = value;
		}

				private int _maxSkillLevel;
		public int maxSkillLevel
		{
		    get => _maxSkillLevel;
		    set => _maxSkillLevel = value;
		}

		public CardRarityType Type
		{
			get
			{
				throw null;
			}
		}

		public bool ValidSpecialTraining
		{
			get
			{
				throw null;
			}
		}

		public MasterCardRarity()
		{
		}
	}
}
