using System.Collections.Generic;
using Sekai.UI;
using Sekai;

namespace Sekai.MusicShop
{
	public class FilteredData
	{
				private MusicTag _MusicTag;
		public MusicTag MusicTag
		{
		    get => _MusicTag;
		    set => _MusicTag = value;
		}

				private string _FreeWord;
		public string FreeWord
		{
		    get => _FreeWord;
		    set => _FreeWord = value;
		}

				private MusicVideoFilterKey _MusicVideoFilterKey;
		public MusicVideoFilterKey MusicVideoFilterKey
		{
		    get => _MusicVideoFilterKey;
		    set => _MusicVideoFilterKey = value;
		}

				private PublishedFilteredData _PublishedData;
		public PublishedFilteredData PublishedData
		{
		    get => _PublishedData;
		    set => _PublishedData = value;
		}

				private VocalTypeFilteredData _VocalTypeData;
		public VocalTypeFilteredData VocalTypeData
		{
		    get => _VocalTypeData;
		    set => _VocalTypeData = value;
		}

		public bool FilteredMusic
		{
			get
			{
				throw null;
			}
		}

		public void SetData<T>(IReadOnlyList<T> viewDataList) where T : IMusicShopCellData
		{
			throw null;
		}

		public void Setup()
		{
			throw null;
		}

		public FilteredData()
		{
			throw null;
		}
	}
}
