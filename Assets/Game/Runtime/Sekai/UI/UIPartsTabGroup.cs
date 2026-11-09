using System;
using UnityEngine;
using Sekai;

namespace Sekai.UI
{
	public class UIPartsTabGroup : MonoBehaviour
	{
		[SerializeField]
		private UIPartsSelectorCell[] _selectorCells;

		[SerializeField]
		private UIPartsSelectorCell _cellPrefab;

		[SerializeField]
		private RectTransform _cellRoot;

		[SerializeField]
		private GameObject _coverObj;

		private UIPartsSelectorCell.ViewData[] _tabDataList;

		public Action<int> OnSelectCell { get; set; }

		public int CurrentSelectedIndex { get; private set; }

		public UIPartsSelectorCell CurrentCell
		{
			get
			{
				return _selectorCells != null && CurrentSelectedIndex >= 0 && CurrentSelectedIndex < _selectorCells.Length ? _selectorCells[CurrentSelectedIndex] : null;
			}
		}

		public UIPartsSelectorCell[] SelectorCells
		{
			get
			{
				return _selectorCells;
			}
		}

		public void ShowCover(bool isShow)
		{
			if (_coverObj != null)
			{
				_coverObj.SetActive(isShow);
			}
		}

		public void Setup(int defaultIndex)
		{
			if (_selectorCells == null)
			{
				_selectorCells = Array.Empty<UIPartsSelectorCell>();
			}

			_tabDataList = new UIPartsSelectorCell.ViewData[_selectorCells.Length];
			for (int i = 0; i < _tabDataList.Length; i++)
			{
				_tabDataList[i] = new UIPartsSelectorCell.ViewData(i)
				{
					Number = i + 1,
					IsSelected = i == defaultIndex,
					IsEnabled = true
				};
			}

			SetupCell(defaultIndex);
		}

		public void Setup(int defaultIndex, UIPartsSelectorCell.ViewData[] _tabDataList)
		{
			this._tabDataList = _tabDataList ?? Array.Empty<UIPartsSelectorCell.ViewData>();
			SetupCell(defaultIndex);
		}

		public void Setup(UIPartsSelectorCell.ViewData[] _tabDataList, UIPartsSelectorCell[] _selectorCells)
		{
			this._tabDataList = _tabDataList ?? Array.Empty<UIPartsSelectorCell.ViewData>();
			this._selectorCells = _selectorCells ?? Array.Empty<UIPartsSelectorCell>();
			SetupCell(0);
		}

		public void SetupReuseOrCreate(int defaultIndex, UIPartsSelectorCell.ViewData[] tabDataArray)
		{
			_tabDataList = tabDataArray ?? Array.Empty<UIPartsSelectorCell.ViewData>();
			if (_selectorCells == null || _selectorCells.Length < _tabDataList.Length)
			{
				Array.Resize(ref _selectorCells, _tabDataList.Length);
			}

			for (int i = 0; i < _tabDataList.Length; i++)
			{
				if (_selectorCells[i] == null && _cellPrefab != null)
				{
					_selectorCells[i] = Instantiate(_cellPrefab, _cellRoot != null ? _cellRoot : transform);
				}

				if (_selectorCells[i] != null)
				{
					_selectorCells[i].gameObject.SetActive(true);
				}
			}

			for (int i = _tabDataList.Length; _selectorCells != null && i < _selectorCells.Length; i++)
			{
				if (_selectorCells[i] != null)
				{
					_selectorCells[i].gameObject.SetActive(false);
				}
			}

			SetupCell(defaultIndex);
		}

		private void SetCellViewData()
		{
			if (_selectorCells == null || _tabDataList == null)
			{
				return;
			}

			for (int i = 0; i < _selectorCells.Length && i < _tabDataList.Length; i++)
			{
				if (_selectorCells[i] != null)
				{
					_selectorCells[i].Setup(_tabDataList[i]);
				}
			}
		}

		private void SetCellCallback()
		{
			if (_selectorCells == null)
			{
				return;
			}

			for (int i = 0; i < _selectorCells.Length; i++)
			{
				if (_selectorCells[i] != null)
				{
					_selectorCells[i].SetCallback(OnClickCell);
				}
			}
		}

		public void SetClickSe(string seName)
		{
			// TODO(original): restore CustomButton.ChangeOtherSE after CustomButton is fully restored.
		}

		public void SetSelectedIndex(int selectIndex)
		{
			SetupCell(selectIndex);
			OnSelectCell?.Invoke(CurrentSelectedIndex);
		}

		public void SetSelectedIndexWithoutCallback(int selectIndex)
		{
			SetupCell(selectIndex);
		}

		private void SetupCell(int selectIndex)
		{
			if (_selectorCells == null)
			{
				_selectorCells = Array.Empty<UIPartsSelectorCell>();
			}

			if (_tabDataList == null || _tabDataList.Length == 0)
			{
				_tabDataList = new UIPartsSelectorCell.ViewData[_selectorCells.Length];
				for (int i = 0; i < _tabDataList.Length; i++)
				{
					_tabDataList[i] = new UIPartsSelectorCell.ViewData(i)
					{
						Number = i + 1,
						IsEnabled = true
					};
				}
			}

			CurrentSelectedIndex = Mathf.Clamp(selectIndex, 0, Mathf.Max(0, _tabDataList.Length - 1));
			for (int i = 0; i < _tabDataList.Length; i++)
			{
				if (_tabDataList[i] != null)
				{
					_tabDataList[i].IsSelected = i == CurrentSelectedIndex;
				}
			}

			SetCellViewData();
			SetCellCallback();
		}

		private void OnClickCell(int selectIndex)
		{
			SetSelectedIndex(selectIndex);
		}

		public void Refresh()
		{
			SetCellViewData();
		}

		public void Show()
		{
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}

		public UIPartsTabGroup()
		{
			CurrentSelectedIndex = 0;
		}
	}
}
