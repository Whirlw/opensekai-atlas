using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sekai;

namespace Sekai.UI
{
	public abstract class UIPartsLeftTabListBase : MonoBehaviour, IUIPartsLeftTabList
	{
		[SerializeField]
		private FixedSizeReuseScroller _scroller;

		[SerializeField]
		private CustomImage _topScrollableIcon;

		[SerializeField]
		private CustomImage _bottomScrollableIcon;

		protected UIPartsLeftTabListCellBase.ViewData[] cellDataList;

		private Action<UIPartsLeftTabListCellBase, int> OnSetupCell;

		[SerializeField]
		private CustomScrollRect _scrollRect;

		private CancellationTokenSource _updateScrollIconCts;

		public Action<UIPartsLeftTabListCellBase.ViewData> OnSelectCell
		{
			[CompilerGenerated]
			get
			{
				return _OnSelectCell;
			}
			[CompilerGenerated]
			set
			{
				_OnSelectCell = value;
			}
		}

		public Action OnSelectedCell
		{
			[CompilerGenerated]
			get
			{
				return _OnSelectedCell;
			}
			[CompilerGenerated]
			set
			{
				_OnSelectedCell = value;
			}
		}

		[CompilerGenerated]
		private Action<UIPartsLeftTabListCellBase.ViewData> _OnSelectCell;

		[CompilerGenerated]
		private Action _OnSelectedCell;

		private void Awake()
		{
			if (_scrollRect == null && _scroller != null)
			{
				_scrollRect = _scroller.GetComponent<CustomScrollRect>();
			}
		}

		public virtual void Initialize(UIPartsLeftTabListCellBase.ViewData[] datas, UIPartsLeftTabListCellBase.ViewData selectedViewData)
		{
			SetCellData(datas, selectedViewData);
			SetScroller();
			SetScrollRect();
		}

		public virtual void Initialize(UIPartsLeftTabListCellBase.ViewData[] dataList, int selectedId)
		{
			UIPartsLeftTabListCellBase.ViewData selectedViewData = null;
			if (dataList != null)
			{
				foreach (var data in dataList)
				{
					if (data != null && data.Id == selectedId)
					{
						selectedViewData = data;
						break;
					}
				}
			}

			Initialize(dataList, selectedViewData);
		}

		public void SetSelected(UIPartsLeftTabListCellBase.ViewData viewData)
		{
			if (cellDataList == null)
			{
				return;
			}

			foreach (var data in cellDataList)
			{
				if (data == null)
				{
					continue;
				}

				data.IsSelected = false;
				if (data.HasSubData)
				{
					foreach (var subData in data.SubTabListViewData)
					{
						if (subData != null)
						{
							subData.IsSelected = false;
						}
					}
				}
			}

			var selectTabViewData = GetSelectTabViewData(viewData);
			if (selectTabViewData != null)
			{
				selectTabViewData.IsSelected = true;
			}
		}

		private void SetCellData(UIPartsLeftTabListCellBase.ViewData[] datas, UIPartsLeftTabListCellBase.ViewData selectedViewData)
		{
			cellDataList = datas ?? Array.Empty<UIPartsLeftTabListCellBase.ViewData>();
			SetSelected(selectedViewData);
			if (cellDataList.Length > 0 && cellDataList[cellDataList.Length - 1] != null)
			{
				cellDataList[cellDataList.Length - 1].IsLast = true;
			}
		}

		private void SetScroller()
		{
			if (_scroller == null)
			{
				return;
			}

			if (_scroller.onUpdateItem != null)
			{
				_scroller.onUpdateItem.RemoveAllListeners();
				_scroller.onUpdateItem.AddListener(OnCreateCell);
			}

			_scroller.Refresh(cellDataList != null ? cellDataList.Length : 0);
		}

		public void SetScrollRect()
		{
			if (_scrollRect == null)
			{
				return;
			}

			_scrollRect.OnEndDragEvent = SetScrollableIcon;
			_scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
			_scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

			_updateScrollIconCts?.Cancel();
			_updateScrollIconCts?.Dispose();
			_updateScrollIconCts = new CancellationTokenSource();
			UpdateScrollIconAfterLayout(_updateScrollIconCts.Token).Forget();
		}

		private void OnDestroy()
		{
			if (_scrollRect != null)
			{
				_scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
			}

			_updateScrollIconCts?.Cancel();
			_updateScrollIconCts?.Dispose();
			_updateScrollIconCts = null;
		}

		private async UniTaskVoid UpdateScrollIconAfterLayout(CancellationToken cancellationToken)
		{
			if (!gameObject.activeInHierarchy)
			{
				await UniTask.WaitUntil(() => gameObject.activeInHierarchy, cancellationToken: cancellationToken);
			}

			if (!cancellationToken.IsCancellationRequested)
			{
				UpdateScrollableState();
			}
		}

		private void OnScrollValueChanged(Vector2 _)
		{
			SetScrollableIcon();
		}

		private void UpdateScrollableState()
		{
			if (_scrollRect == null)
			{
				_topScrollableIcon?.SetActive(false);
				_bottomScrollableIcon?.SetActive(false);
				return;
			}

			var scrollable = _scrollRect.Scrollable;
			_topScrollableIcon?.SetActive(scrollable);
			_bottomScrollableIcon?.SetActive(scrollable);
			if (scrollable)
			{
				SetScrollableIcon();
			}
		}

		private void SetScrollableIcon()
		{
			if (_scrollRect == null)
			{
				return;
			}

			var reachedEdge = _scrollRect.GetReachedEdge();
			if (_topScrollableIcon != null)
			{
				_topScrollableIcon.Alpha = reachedEdge.Top ? 0.2f : 1f;
			}

			if (_bottomScrollableIcon != null)
			{
				_bottomScrollableIcon.Alpha = reachedEdge.Bottom ? 0.2f : 1f;
			}
		}

		private void OnCreateCell(int dataIndex, int itemIndex, GameObject instance)
		{
			if (instance == null || cellDataList == null || dataIndex < 0 || dataIndex >= cellDataList.Length)
			{
				return;
			}

			var cell = instance.GetComponent<UIPartsLeftTabListCellBase>();
			var data = cellDataList[dataIndex];
			if (cell == null || data == null)
			{
				return;
			}

			data.OnSelectEvent = OnSelect;
			cell.Setup(data);
			OnSetupCell?.Invoke(cell, dataIndex);
		}

		private void OnSelect(UIPartsLeftTabListCellBase.ViewData viewData)
		{
			if (viewData == null)
			{
				return;
			}

			UIPartsLeftTabListCellBase.ViewData beforeSelectedData = null;
			if (cellDataList != null)
			{
				foreach (var data in cellDataList)
				{
					if (data != null && data.IsSelected)
					{
						beforeSelectedData = data;
						break;
					}
				}
			}

			if (AllowsPublishSelectEvent(beforeSelectedData, viewData))
			{
				SetSelected(viewData);
				_scroller?.Refresh(false);
				OnSelectCell?.Invoke(viewData);
			}
			else
			{
				OnSelectedCell?.Invoke();
			}
		}

		private UIPartsLeftTabListCellBase.ViewData GetSelectTabViewData(UIPartsLeftTabListCellBase.ViewData viewData)
		{
			if (viewData == null || cellDataList == null)
			{
				return null;
			}

			if (viewData.IsSub)
			{
				foreach (var data in cellDataList)
				{
					if (data != null && data.ContainsSubData(viewData))
					{
						return viewData;
					}
				}
			}

			foreach (var data in cellDataList)
			{
				if (data == viewData)
				{
					return data;
				}
			}

			return null;
		}

		public void Refresh(bool resetContentPosition = false)
		{
			_scroller?.Refresh(resetContentPosition);
		}

		protected abstract bool AllowsPublishSelectEvent(UIPartsLeftTabListCellBase.ViewData beforeSelectedData, UIPartsLeftTabListCellBase.ViewData viewData);

		protected UIPartsLeftTabListBase()
		{
		}
	}
}
