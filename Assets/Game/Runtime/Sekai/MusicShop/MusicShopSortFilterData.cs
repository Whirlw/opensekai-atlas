
namespace Sekai.MusicShop
{
	public class MusicShopSortFilterData
	{
		public enum FilterType
		{
			MusicShop = 0,
			AnotherVocalShop = 1,
			MusicScoreMakerMusicSelectForCreate = 2
		}

				private FilteredData _FilteredData;
		public FilteredData FilteredData
		{
		    get => _FilteredData;
		    set => _FilteredData = value;
		}

				private SortData _SortData;
		public SortData SortData
		{
		    get => _SortData;
		    set => _SortData = value;
		}

		private FilterType _type;

		public void Setup(FilterType type)
		{
			throw null;
		}

		public void Load()
		{
			throw null;
		}

		public void Save()
		{
			throw null;
		}

		public MusicShopSortFilterData()
		{
			throw null;
		}
	}
}
