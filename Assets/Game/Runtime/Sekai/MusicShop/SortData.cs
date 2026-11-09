
namespace Sekai.MusicShop
{
	public class SortData
	{
				private SortOrderBy _SortOrder;
		public SortOrderBy SortOrder
		{
		    get => _SortOrder;
		    set => _SortOrder = value;
		}

				private SortType _SortType;
		public SortType SortType
		{
		    get => _SortType;
		    set => _SortType = value;
		}

				private MusicDifficulty _MusicDifficulty;
		public MusicDifficulty MusicDifficulty
		{
		    get => _MusicDifficulty;
		    set => _MusicDifficulty = value;
		}

		public bool IsMusicDifficulty
		{
			get
			{
				throw null;
			}
		}

		public SortData()
		{
			throw null;
		}
	}
}
