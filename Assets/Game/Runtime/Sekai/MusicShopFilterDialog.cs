using Sekai.MusicShop;
using UnityEngine;

namespace Sekai
{
	public class MusicShopFilterDialog : CharacterFilterDialog
	{
		[SerializeField]
		private MusicShopMVFilterContent _mvFilterContent;

		[SerializeField]
		private PublishFilterContent _publishFilterContent;

		[SerializeField]
		private MusicShopVocalFilterContent _vocalFilterContent;

		[SerializeField]
		private GameObject[] _vocalFilterRoots;

		private FilteredData _filteredData;

		public FilteredData GetFilteredData()
		{
			throw null;
		}

		public void Setup(MusicShopSortFilterData.FilterType filterType, FilteredData _filteredData)
		{
			throw null;
		}

		private void SetupMV()
		{
			throw null;
		}

		private void SetupPublished()
		{
			throw null;
		}

		private void SetVocal(MusicShopSortFilterData.FilterType filterType)
		{
			throw null;
		}

		public MusicShopFilterDialog()
		{
			throw null;
		}
	}
}
