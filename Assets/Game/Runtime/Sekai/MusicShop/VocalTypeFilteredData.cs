using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sekai.MusicShop
{
	public class VocalTypeFilteredData
	{
				private HashSet<int> _VirtualSingerGameCharacterIds;
		public HashSet<int> VirtualSingerGameCharacterIds
		{
		    get => _VirtualSingerGameCharacterIds;
		    set => _VirtualSingerGameCharacterIds = value;
		}

				private bool _VirtualSingerOutSide;
		public bool VirtualSingerOutSide
		{
		    get => _VirtualSingerOutSide;
		    set => _VirtualSingerOutSide = value;
		}

				private List<int> _SekaiCharacterIds;
		public List<int> SekaiCharacterIds
		{
		    get => _SekaiCharacterIds;
		    set => _SekaiCharacterIds = value;
		}

		public bool IsAllVirtualSingerCharacter
		{
			get
			{
				throw null;
			}
		}

		public bool IsAllVirtualSingerGameCharacter
		{
			get
			{
				throw null;
			}
		}

		public bool IsAllSekaiCharacter
		{
			get
			{
				throw null;
			}
		}

		private int[] VirtualSingerGameCharacterAllIds
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

		private int[] SekaiCharacterAllIds
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

		public VocalTypeFilteredData()
		{
			throw null;
		}

		public VocalTypeFilteredData(VocalTypeFilteredData filteredData)
		{
			throw null;
		}

		public void Setup()
		{
			throw null;
		}
	}
}
