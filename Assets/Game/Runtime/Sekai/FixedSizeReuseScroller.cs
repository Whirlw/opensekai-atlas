using System;
using System.Collections.Generic;
using Beebyte.Obfuscator;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Sekai
{
	[RequireComponent(typeof(ScrollRect))]
	public class FixedSizeReuseScroller : MonoBehaviour
	{
		[Serializable]
		public class UpdateItemEvent : UnityEvent<int, int, GameObject>
		{
			public UpdateItemEvent()
			{
			}
		}

		[Serializable]
		public class DisableItemEvent : UnityEvent<int, GameObject>
		{
			public DisableItemEvent()
			{
			}
		}

		public enum DirectionType
		{
			Vertical = 0,
			Horizontal = 1
		}

		[Serializable]
		public class LayoutInfo
		{
						private int _splitCount;
			public int splitCount
			{
			    get => _splitCount;
			    set => _splitCount = value;
			}

						private Vector2 _itemSpacing;
			public Vector2 itemSpacing
			{
			    get => _itemSpacing;
			    set => _itemSpacing = value;
			}

			public LayoutInfo()
			{
			}
		}

		[Serializable]
		public struct UpdateItemInfo
		{
			public int dataIndex;

			public int itemIndex;

			public GameObject instance;
		}

		[Serializable]
		public struct DisableItemInfo
		{
			public int itemIndex;

			public GameObject instance;
		}

		[SerializeField]
		private ScrollRect _scrollRect;

		[SerializeField]
		private RectTransform _prefab;

		[SerializeField]
		private DirectionType _baseDirectionType;

		[SerializeField]
		[FormerlySerializedAs("updateItemTimingRowCount")]
		private int _updateItemTimingOffsetCount;

		[SerializeField]
		private LayoutInfo _layoutInfo;

		[SerializeField]
		private RectOffset _padding;

		private int _poolCount;

		private int _allDataCount;

		private RectTransform[] _poolItems;

		private int _currentDataIndex;

		private float _decrementPerFrame;

				[SerializeField] private UnityEvent _onScrollItem;
		public UnityEvent onScrollItem
		{
		    get => _onScrollItem;
		    set => _onScrollItem = value;
		}

				[SerializeField] private UpdateItemEvent _onUpdateItem;
		public UpdateItemEvent onUpdateItem
		{
		    get => _onUpdateItem;
		    set => _onUpdateItem = value;
		}

				[SerializeField] private DisableItemEvent _onDisableItem;
		public DisableItemEvent onDisableItem
		{
		    get => _onDisableItem;
		    set => _onDisableItem = value;
		}

		private float _prevNormalizedContentPosition;

		public int POOL_COUNT
		{
			get
			{
				return _poolCount;
			}
		}

		public Vector2 NormalzeScrollPosition
		{
			get
			{
				return _scrollRect != null ? _scrollRect.normalizedPosition : Vector2.zero;
			}
			set
			{
				if (_scrollRect != null)
				{
					_scrollRect.normalizedPosition = value;
				}
			}
		}

		public Vector2 Velocity
		{
			get
			{
				return _scrollRect != null ? _scrollRect.velocity : Vector2.zero;
			}
			set
			{
				if (_scrollRect != null)
				{
					_scrollRect.velocity = value;
				}
			}
		}

		public bool Vertical
		{
			get
			{
				return _scrollRect != null && _scrollRect.vertical;
			}
			set
			{
				if (_scrollRect != null)
				{
					_scrollRect.vertical = value;
				}
			}
		}

		public bool Horizontal
		{
			get
			{
				return _scrollRect != null && _scrollRect.horizontal;
			}
			set
			{
				if (_scrollRect != null)
				{
					_scrollRect.horizontal = value;
				}
			}
		}

		public bool TryGetPoolItemAt(int index, out GameObject obj)
		{
			obj = null;
			if (_poolItems == null || index < 0 || index >= _poolItems.Length || _poolItems[index] == null)
			{
				return false;
			}

			obj = _poolItems[index].gameObject;
			return obj != null;
		}

		public void Refresh(bool resetContentPosition = true)
		{
			Refresh(_allDataCount, resetContentPosition);
		}

		public IReadOnlyList<RectTransform> GetPoolItemAll()
		{
			return _poolItems;
		}

		[Skip]
		public void Refresh(int _allDataCount, bool resetContentPosition = true)
		{
			EnsureReferences();
			if (_layoutInfo.splitCount <= 0)
			{
				_layoutInfo.splitCount = 1;
			}

			this._allDataCount = Mathf.Max(0, _allDataCount);
			if (resetContentPosition && _scrollRect != null && _scrollRect.content != null)
			{
				_scrollRect.content.anchoredPosition = Vector2.zero;
				_scrollRect.velocity = Vector2.zero;
				_currentDataIndex = 0;
				_decrementPerFrame = 0f;
			}

			if (_poolItems == null || _poolItems.Length == 0)
			{
				SetupPoolItems();
			}

			if (_scrollRect != null)
			{
				_prevNormalizedContentPosition = _baseDirectionType == DirectionType.Horizontal
					? _scrollRect.normalizedPosition.x
					: 1f - _scrollRect.normalizedPosition.y;
			}

			SetupScrollRange(this._allDataCount);
			ResetupAllItem();
		}

		public void UpdateAllItem()
		{
			if (_poolItems == null)
			{
				return;
			}

			for (var i = 0; i < _poolItems.Length && _currentDataIndex + i < _allDataCount; i++)
			{
				var item = _poolItems[i];
				if (item == null)
				{
					continue;
				}

				BuildUpdateItemData(i, _currentDataIndex + i, item.gameObject, out var data);
				UpdateItemObserver(ref data);
			}
		}

		public void UpdateItemOf(int dataIndex)
		{
			if (_poolItems == null || dataIndex < _currentDataIndex || dataIndex >= _currentDataIndex + _poolItems.Length || dataIndex >= _allDataCount)
			{
				return;
			}

			var itemIndex = dataIndex - _currentDataIndex;
			if (itemIndex < 0 || itemIndex >= _poolItems.Length || _poolItems[itemIndex] == null)
			{
				return;
			}

			BuildUpdateItemData(itemIndex, dataIndex, _poolItems[itemIndex].gameObject, out var data);
			UpdateItemObserver(ref data);
		}

		public Vector2 GetScrollPosition()
		{
			return _scrollRect != null && _scrollRect.content != null ? (Vector2)_scrollRect.content.localPosition : Vector2.zero;
		}

		public void SetScrollPosition(Vector2 position)
		{
			if (_scrollRect != null && _scrollRect.content != null)
			{
				_scrollRect.content.localPosition = new Vector3(position.x, position.y, 0f);
			}
		}

		public void SetScrollRectMovementType(ScrollRect.MovementType movementType)
		{
			if (_scrollRect != null)
			{
				_scrollRect.movementType = movementType;
			}
		}

		public void SetupPoolItems()
		{
			EnsureReferences();
			if (_scrollRect == null || _scrollRect.content == null || _prefab == null)
			{
				_poolItems = Array.Empty<RectTransform>();
				_poolCount = 0;
				return;
			}

			_poolCount = Mathf.Max(1, CalcShouldCreatePoolObjectCount());
			_poolItems = new RectTransform[_poolCount];
			var prefabObject = _prefab.gameObject;
			var wasActive = prefabObject.activeSelf;
			if (!wasActive)
			{
				prefabObject.SetActive(true);
			}

			for (var i = 0; i < _poolItems.Length; i++)
			{
				var instance = Instantiate(_prefab, _scrollRect.content, false);
				instance.name = i.ToString();
				_poolItems[i] = instance;
				UpdateItemPositionOf(i, i);
				SetupItemIndexOf(i);
			}

			prefabObject.SetActive(false);
		}

		private void SetupScrollRange(int allItemCount)
		{
			if (_scrollRect == null || _scrollRect.content == null || _prefab == null || _layoutInfo == null)
			{
				return;
			}

			var splitCount = Mathf.Max(1, _layoutInfo.splitCount);
			var rowCount = Mathf.Max(1, Mathf.CeilToInt((float)Mathf.Max(0, allItemCount) / splitCount));
			var prefabRect = _prefab.rect;
			var size = _scrollRect.content.sizeDelta;
			if (_baseDirectionType == DirectionType.Vertical)
			{
				size.y = _padding.top + _padding.bottom + prefabRect.height * rowCount + _layoutInfo.itemSpacing.y * Mathf.Max(0, rowCount - 1);
			}
			else
			{
				size.x = _padding.left + _padding.right + prefabRect.width * rowCount + _layoutInfo.itemSpacing.x * Mathf.Max(0, rowCount - 1);
			}

			_scrollRect.content.sizeDelta = size;
		}

		private void ResetupAllItem()
		{
			if (_poolItems == null)
			{
				return;
			}

			for (var i = 0; i < _poolItems.Length; i++)
			{
				SetupItemIndexOf(i);
			}
		}

		private void SetupItemIndexOf(int itemIndex)
		{
			if (_poolItems == null || itemIndex < 0 || itemIndex >= _poolItems.Length)
			{
				return;
			}

			var item = _poolItems[itemIndex];
			if (item == null)
			{
				return;
			}

			var dataIndex = _currentDataIndex + itemIndex;
			if (dataIndex < _allDataCount)
			{
				if (!item.gameObject.activeSelf)
				{
					item.gameObject.SetActive(true);
				}

				UpdateItemPositionOf(itemIndex, dataIndex);
				BuildUpdateItemData(itemIndex, dataIndex, item.gameObject, out var data);
				UpdateItemObserver(ref data);
			}
			else
			{
				BuildDisableItemData(itemIndex, item.gameObject, out var data);
				if (item.gameObject.activeSelf)
				{
					item.gameObject.SetActive(false);
				}

				DisableItemObserver(ref data);
			}
		}

		private void OnUpdatePoolItemImpl(int nextIndexinAllItems)
		{
			_currentDataIndex = Mathf.Clamp(nextIndexinAllItems, 0, Mathf.Max(0, _allDataCount - 1));
			ResetupAllItem();
			ScrollItemObserver();
		}

		private Vector2 CalcItemPosition(int row, int column)
		{
			if (_prefab == null || _layoutInfo == null)
			{
				return Vector2.zero;
			}

			var basePosition = _prefab.anchoredPosition;
			var rect = _prefab.rect;
			return new Vector2(
				basePosition.x + _padding.left + (rect.width + _layoutInfo.itemSpacing.x) * row,
				basePosition.y - _padding.top - (rect.height + _layoutInfo.itemSpacing.y) * column);
		}

		private void UpdateItemPositionOf(int itemIndex, int dataIndex)
		{
			if (_poolItems == null || itemIndex < 0 || itemIndex >= _poolItems.Length || _poolItems[itemIndex] == null)
			{
				return;
			}

			var rowAndColumn = CalcRowAndColumn(dataIndex);
			_poolItems[itemIndex].anchoredPosition = CalcItemPosition(rowAndColumn.Item1, rowAndColumn.Item2);
		}

		private (int, int) CalcRowAndColumn(int dataIndex)
		{
			var splitCount = Mathf.Max(1, _layoutInfo != null ? _layoutInfo.splitCount : 1);
			return _baseDirectionType == DirectionType.Horizontal
				? (dataIndex / splitCount, dataIndex % splitCount)
				: (dataIndex % splitCount, dataIndex / splitCount);
		}

		private void UpdateInternal(float directionStartPosition, float directionItemSize, float directionSpacing, float scrollContentPosition)
		{
			if (_poolItems == null || _poolItems.Length == 0 || _layoutInfo == null)
			{
				return;
			}

			var itemStride = Mathf.Max(1f, directionItemSize + directionSpacing);
			var row = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, scrollContentPosition - directionStartPosition) / itemStride));
			var nextDataIndex = row * Mathf.Max(1, _layoutInfo.splitCount);
			nextDataIndex = Mathf.Clamp(nextDataIndex, 0, Mathf.Max(0, _allDataCount - 1));
			if (nextDataIndex != _currentDataIndex)
			{
				_currentDataIndex = nextDataIndex;
				ResetupAllItem();
				ScrollItemObserver();
			}

			if (_scrollRect != null)
			{
				_prevNormalizedContentPosition = _baseDirectionType == DirectionType.Horizontal
					? _scrollRect.normalizedPosition.x
					: 1f - _scrollRect.normalizedPosition.y;
			}
		}

		private void BuildUpdateItemData(int itemIndex, int dataIndex, GameObject obj, out UpdateItemInfo data)
		{
			data = new UpdateItemInfo
			{
				dataIndex = dataIndex,
				itemIndex = itemIndex,
				instance = obj
			};
		}

		private void BuildDisableItemData(int itemIndex, GameObject obj, out DisableItemInfo data)
		{
			data = new DisableItemInfo
			{
				itemIndex = itemIndex,
				instance = obj
			};
		}

		private void UpdateItemObserver(ref UpdateItemInfo info)
		{
			onUpdateItem?.Invoke(info.dataIndex, info.itemIndex, info.instance);
		}

		private void DisableItemObserver(ref DisableItemInfo info)
		{
			onDisableItem?.Invoke(info.itemIndex, info.instance);
		}

		private void ScrollItemObserver()
		{
			onScrollItem?.Invoke();
		}

		private int CalcShouldCreatePoolObjectCount()
		{
			EnsureReferences();
			if (_scrollRect == null || _scrollRect.viewport == null || _prefab == null || _layoutInfo == null)
			{
				return _poolCount;
			}

			_scrollRect.horizontal = _baseDirectionType == DirectionType.Horizontal;
			_scrollRect.vertical = _baseDirectionType == DirectionType.Vertical;

			var viewportSize = _baseDirectionType == DirectionType.Horizontal ? _scrollRect.viewport.rect.width : _scrollRect.viewport.rect.height;
			var itemSize = _baseDirectionType == DirectionType.Horizontal ? _prefab.rect.width : _prefab.rect.height;
			if (itemSize <= 0f)
			{
				return _poolCount;
			}

			var visibleLineCount = Mathf.CeilToInt(viewportSize / itemSize) + Mathf.Max(0, _updateItemTimingOffsetCount);
			var splitCount = Mathf.Max(1, _layoutInfo.splitCount);
			return Mathf.Max(splitCount, visibleLineCount * splitCount);
		}

		private void Awake()
		{
			EnsureReferences();
			SetupPoolItems();
		}

		private void OnDestroy()
		{
			onUpdateItem?.RemoveAllListeners();
			onScrollItem?.RemoveAllListeners();
			onDisableItem?.RemoveAllListeners();
		}

		private void LateUpdate()
		{
			if (_scrollRect == null || _scrollRect.content == null || _prefab == null || _layoutInfo == null)
			{
				return;
			}

			var prefabRect = _prefab.rect;
			if (_scrollRect.vertical)
			{
				UpdateInternal(_prefab.anchoredPosition.y, prefabRect.height, _layoutInfo.itemSpacing.y, -_scrollRect.content.anchoredPosition.y);
			}

			if (_scrollRect.horizontal)
			{
				UpdateInternal(_prefab.anchoredPosition.x, prefabRect.width, _layoutInfo.itemSpacing.x, _scrollRect.content.anchoredPosition.x);
			}
		}

		private void OnEnable()
		{
			EnsureReferences();
			if (_scrollRect != null)
			{
				_scrollRect.horizontal = _baseDirectionType == DirectionType.Horizontal;
				_scrollRect.vertical = _baseDirectionType == DirectionType.Vertical;
			}
		}

		public FixedSizeReuseScroller()
		{
			_updateItemTimingOffsetCount = 1;
			_poolCount = 7;
			onScrollItem = new UnityEvent();
			onUpdateItem = new UpdateItemEvent();
			onDisableItem = new DisableItemEvent();
			_layoutInfo = new LayoutInfo { splitCount = 1 };
			_padding = new RectOffset();
		}

		private void EnsureReferences()
		{
			if (_scrollRect == null)
			{
				_scrollRect = GetComponent<ScrollRect>();
			}

			if (_layoutInfo == null)
			{
				_layoutInfo = new LayoutInfo { splitCount = 1 };
			}

			if (_layoutInfo.splitCount <= 0)
			{
				_layoutInfo.splitCount = 1;
			}

			if (_padding == null)
			{
				_padding = new RectOffset();
			}

			if (onScrollItem == null)
			{
				onScrollItem = new UnityEvent();
			}

			if (onUpdateItem == null)
			{
				onUpdateItem = new UpdateItemEvent();
			}

			if (onDisableItem == null)
			{
				onDisableItem = new DisableItemEvent();
			}
		}
	}
}
