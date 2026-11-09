using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sekai
{
	public class PublishedFilteredData
	{
		public enum FilteredType
		{
			All = 0,
			AD = 1
		}

				private FilteredType _Type;
		public FilteredType Type
		{
		    get => _Type;
		    set => _Type = value;
		}

		public int[] PublishedAdList
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool IsAD
		{
			get
			{
				throw null;
			}
		}

		public int PublishedAd
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public List<string> PublishedAdStringList
		{
			get
			{
				throw null;
			}
		}

		public int GetPublishedAdIndex()
		{
			throw null;
		}

		public void SetData(int[] publishedAdList)
		{
			throw null;
		}

		public void SetPublishedAd(int publishedAd)
		{
			throw null;
		}

		public PublishedFilteredData()
		{
			throw null;
		}
	}
}
