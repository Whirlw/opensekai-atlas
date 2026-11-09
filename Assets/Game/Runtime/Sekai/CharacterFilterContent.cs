using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Sekai
{
	public class CharacterFilterContent : MonoBehaviour
	{
		[Serializable]
		private class CharacterIconCheckboxes
		{
						private UnitType _unitType;
			public UnitType unitType
			{
			    get => _unitType;
			    set => _unitType = value;
			}

						private UIPartsCharacterIconAndCheckBox[] _characterIconCheckboxes;
			public UIPartsCharacterIconAndCheckBox[] characterIconCheckboxes
			{
			    get => _characterIconCheckboxes;
			    set => _characterIconCheckboxes = value;
			}

			public CharacterIconCheckboxes()
			{
				throw null;
			}
		}

		private const float CHARA_BOX_ONLY_LINE_POS_X = 4f;

		private const float CHARA_BOX_ONLY_BOX_ROOT_POS_X = 12f;

		[SerializeField]
		private bool _isGameCharacterUnit;

		[SerializeField]
		private RectTransform[] _buttonsRoots;

		private Dictionary<int, UIPartsCharacterIconAndCheckBox> _characterCheckBoxDict;

		private Dictionary<CardList.FilterUnit, UIPartsCheckBox> _unitCheckBoxDict;

		private int[] _fixedCharacterIds;

		[SerializeField]
		private UIPartsCheckBox _allCheckBox;

		[SerializeField]
		private UIPartsCheckBox _otherCheckBox;

		[SerializeField]
		private UIPartsCheckBox[] _unitCheckBoxes;

		[SerializeField]
		private RectTransform[] _lines;

		[SerializeField]
		private GameObject[] _logos;

		[SerializeField]
		private CharacterIconCheckboxes[] _characterIconCheckboxes;

		private bool _isExistOther
		{
			get
			{
				throw null;
			}
		}

		public void ApplySerializeFieldSettings()
		{
			throw null;
		}

		public void Setup(int[] enableCharacterIds, int[] fixedCharacterIds = null, bool isOtherChecked = true)
		{
			throw null;
		}

		public void UpdateByFilterUnit(CardList.FilterUnit filterUnit)
		{
			throw null;
		}

		public void UpdateCharacterDisplayByFilterUnit(CardList.FilterUnit filterUnit)
		{
			throw null;
		}

		private void UpdateCharacterDisplayByAllUnit()
		{
			throw null;
		}

		private void UpdateCharacterDisplayBySpecificUnit(CardList.FilterUnit filterUnit)
		{
			throw null;
		}

		private void UpdateCharacterDisplay(HashSet<UnitType> visibleUnitTypes)
		{
			throw null;
		}

		private void UpdateUnitCheckBoxesDisplay([NotNull] HashSet<UnitType> visibleUnitTypes)
		{
			throw null;
		}

		private void UpdateCharacterGroupsDisplay([NotNull] HashSet<UnitType> visibleUnitTypes)
		{
			throw null;
		}

		private void EnableChecked(UIPartsCharacterIconAndCheckBox[] children)
		{
			throw null;
		}

		public void DisableCharacterCheckboxes(int[] exclusionCharacterIds)
		{
			throw null;
		}

		private void SetupCharacterCheckBox(int[] enableCharacterIds)
		{
			throw null;
		}

		private bool IsFixedCharacter(int characterId)
		{
			throw null;
		}

		private void ChangeCheckSelectedAll(bool check)
		{
			throw null;
		}

		private void ChangeCheckSelectedUnit(bool check, UnitType unit)
		{
			throw null;
		}

		private void ChangeCheckSelectedCharacter(KeyValuePair<int, UIPartsCharacterIconAndCheckBox> characterIconPair, bool check)
		{
			throw null;
		}

		private void ChangeCheckSelectedCharacter(CardList.FilterUnit filterUnit)
		{
			throw null;
		}

		public void HideUnit()
		{
			throw null;
		}

		public int[] GetCheckedIds()
		{
			throw null;
		}

		public int[] GetUnCheckedIds()
		{
			throw null;
		}

		public bool GetOtherChecked()
		{
			throw null;
		}

		private void SetOtherCheckBox(bool check)
		{
			throw null;
		}

		private bool IsAllCheck()
		{
			throw null;
		}

		private UnitType GetUnitType(int characterId)
		{
			throw null;
		}

		public void SetCheckValueByToggleEnabled()
		{
			throw null;
		}

		public CharacterFilterContent()
		{
			throw null;
		}
	}
}
