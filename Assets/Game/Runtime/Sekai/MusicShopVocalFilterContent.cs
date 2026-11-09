using System.Runtime.CompilerServices;
using Sekai.MusicShop;
using UnityEngine;

namespace Sekai
{
	public class MusicShopVocalFilterContent : MonoBehaviour
	{
		[SerializeField]
		private UIPartsCheckBox _allCheckBox;

		[SerializeField]
		private UIPartsCharacterIconAndCheckBox[] _characterCheckBoxes;

		[SerializeField]
		private UIPartsCheckBox _outSideCheckBox;

		public VocalTypeFilteredData FilteredData
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public void Setup(VocalTypeFilteredData filteredData)
		{
			throw null;
		}

		private void SetCheckAllCheckBox()
		{
			throw null;
		}

		private void OnSelectedVirtualSingerAll(bool isOn)
		{
			throw null;
		}

		private void SetCheckCharacterCheckBoxes()
		{
			throw null;
		}

		private void OnSelectedVirtualSingerGameCharacter(bool isOn, int characterId)
		{
			throw null;
		}

		private void SetCheckOutSideCheckBox()
		{
			throw null;
		}

		private void OnSelectedVirtualSingerOutSide(bool isOn)
		{
			throw null;
		}

		public MusicShopVocalFilterContent()
		{
			throw null;
		}
	}
}
