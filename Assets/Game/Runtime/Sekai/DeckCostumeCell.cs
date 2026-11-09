using System;
using CP;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class DeckCostumeCell : MonoBehaviour
	{
		public class CellData
		{
						private MasterGameCharacter _MasterGameCharacter;
			public MasterGameCharacter MasterGameCharacter
			{
			    get => _MasterGameCharacter;
			    set => _MasterGameCharacter = value;
			}

						private MasterCard _MasterCard;
			public MasterCard MasterCard
			{
			    get => _MasterCard;
			    set => _MasterCard = value;
			}

						private Action<MasterGameCharacter, CostumePartType> _OnClick;
			public Action<MasterGameCharacter, CostumePartType> OnClick
			{
			    get => _OnClick;
			    set => _OnClick = value;
			}

						private MVOnlyDeckCostumeLocalData _mvOnlyDeckCostumeLocalData;
			public MVOnlyDeckCostumeLocalData mvOnlyDeckCostumeLocalData
			{
			    get => _mvOnlyDeckCostumeLocalData;
			    set => _mvOnlyDeckCostumeLocalData = value;
			}

			public void InvokeOnClick(CostumePartType costumePartType)
			{
				throw null;
			}

			public bool IsCostumeLock()
			{
				throw null;
			}

			public bool IsCostumeAndCharacterValid()
			{
				throw null;
			}

			public bool ExistsCharacter()
			{
				throw null;
			}

			public bool ExistsMvOnlyDeckCostumeLocalData()
			{
				throw null;
			}

			public CellData()
			{
				throw null;
			}
		}

		[Serializable]
		public class CostumeCell
		{
						private CostumePartType _Type;
			public CostumePartType Type
			{
			    get => _Type;
			    set => _Type = value;
			}

						private UIPartsItemThumbnail _Thumbnail;
			public UIPartsItemThumbnail Thumbnail
			{
			    get => _Thumbnail;
			    set => _Thumbnail = value;
			}

						private CustomButton _Button;
			public CustomButton Button
			{
			    get => _Button;
			    set => _Button = value;
			}

						private ClickDetector _ClickDetector;
			public ClickDetector ClickDetector
			{
			    get => _ClickDetector;
			    set => _ClickDetector = value;
			}

						private GameObject _LockObject;
			public GameObject LockObject
			{
			    get => _LockObject;
			    set => _LockObject = value;
			}

			public CostumeCell()
			{
				throw null;
			}
		}

		[SerializeField]
		private CostumeCell[] _costumeCells;

		private CellData _cellData;

		private bool _isCostumeLock;

		public void Setup(CellData _cellData)
		{
			throw null;
		}

		private UserCharacterCostume3D GetUserCharacterLiveCostume()
		{
			throw null;
		}

		private void SetupThumbnail(UserCharacterCostume3D userCharacterLiveCostume)
		{
			throw null;
		}

		private void SetupThumbnail(UIPartsItemThumbnail uiPartsItemThumbnail, int costume3dId, UnitType unitType)
		{
			throw null;
		}

		private void SetupLockObject()
		{
			throw null;
		}

		private void SetupCostumeButton()
		{
			throw null;
		}

		public DeckCostumeCell()
		{
			throw null;
		}
	}
}
