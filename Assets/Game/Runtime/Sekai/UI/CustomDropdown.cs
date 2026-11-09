using System;
using System.Collections.Generic;
using Sekai.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sekai;
using Sekai.Live;

namespace Sekai.UI
{
	[AddComponentMenu("Custom UI/Custom Dropdown")]
	public class CustomDropdown : TMP_Dropdown
	{
		[SerializeField]
		private GameObject _baseObject;

		[SerializeField]
		private GameObject _arrowObject;

		[SerializeField]
		private bool _showsAllItems;

		[SerializeField]
		private bool _enableItemClickEvent;

		[SerializeField]
		private bool _centerScrollOnOpen;

		private ScrollRect _scrollRect;

		private DropdownItem _templateItem;

		[SerializeField]
		protected SeType se;

		private Action _onSelect;

		private Action _onPointerClick;

		private Dictionary<int, Action> _onItemClickCallbacks;

		public void Setup(List<string> keys, int index, Action<int> _onSelect)
		{
			ClearOptions();
			List<OptionData> optionData = new List<OptionData>();
			if (keys != null)
			{
				for (int i = 0; i < keys.Count; i++)
				{
					optionData.Add(new OptionData(keys[i]));
				}
			}
			AddOptions(optionData);
			SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1)));
			onValueChanged.RemoveAllListeners();
			if (_onSelect != null)
			{
				onValueChanged.AddListener(value => _onSelect(value));
			}
		}

		public void RegisterOnValueChanged(Action<int> callback)
		{
			if (callback != null)
			{
				onValueChanged.AddListener(value => callback(value));
			}
		}

		public void Refresh(List<string> keys, int index)
		{
			ClearOptions();
			List<OptionData> optionData = new List<OptionData>();
			if (keys != null)
			{
				for (int i = 0; i < keys.Count; i++)
				{
					optionData.Add(new OptionData(keys[i]));
				}
			}
			AddOptions(optionData);
			SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1)));
		}

		public override void OnPointerClick(PointerEventData eventData)
		{
			if (IsExpanded)
			{
				return;
			}

			base.OnPointerClick(eventData);
			CustomSelectableDefine.PlaySE(se, string.Empty);
			SetLayout();
			SetArrowObject(true);
			AdjustHeight();
			if (_centerScrollOnOpen && _scrollRect != null && _scrollRect.content != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
				TargetScrollPosition();
			}
			UpdateAllItemCallbacks();
			_onPointerClick?.Invoke();
		}

		private void UpdateAllItemCallbacks()
		{
			if (!_enableItemClickEvent || _scrollRect == null || _onItemClickCallbacks == null || _onItemClickCallbacks.Count == 0)
			{
				return;
			}
			DropdownItem[] items = _scrollRect.GetComponentsInChildren<DropdownItem>();
			foreach (KeyValuePair<int, Action> pair in _onItemClickCallbacks)
			{
				int index = pair.Key;
				if (index < 0 || index >= items.Length || items[index] == null)
				{
					continue;
				}
				var handler = items[index].GetComponent<DropdownItemClickHandler>();
				if (handler == null)
				{
					handler = items[index].gameObject.AddComponent<DropdownItemClickHandler>();
				}
				handler.SetCallback(pair.Value);
			}
		}

		public override void OnSelect(BaseEventData eventData)
		{
			_onSelect?.Invoke();
			base.OnSelect(eventData);
		}

		public void SetOnSelectCallback(Action _onSelect)
		{
			this._onSelect = _onSelect;
		}

		public void SetOnPointerClick(Action _onPointerClick)
		{
			this._onPointerClick = _onPointerClick;
		}

		public void SetOnItemClick(int index, Action callback)
		{
			_onItemClickCallbacks ??= new Dictionary<int, Action>();
			_onItemClickCallbacks[index] = callback;
		}

		public void ClearItemClickCallbacks()
		{
			_onItemClickCallbacks?.Clear();
		}

		public void SetDropDownLayout(Vector2 anchorMin, Vector2 anchorMax, int anchoredPositionY, Vector2 pivot)
		{
			if (template == null)
			{
				return;
			}
			RectTransform rectTransform = template;
			rectTransform.anchorMin = anchorMin;
			rectTransform.anchorMax = anchorMax;
			rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, anchoredPositionY);
			rectTransform.pivot = pivot;
		}

		public bool Contains(int index)
		{
			return index >= 0 && index < options.Count;
		}

		private void SetLayout()
		{
			if (_scrollRect == null)
			{
				return;
			}

			var listRect = _scrollRect.GetComponent<RectTransform>();
			var selfRect = transform as RectTransform;
			if (listRect == null || selfRect == null)
			{
				return;
			}

			float dropdownHeight;
			if (_showsAllItems && _scrollRect.content != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
				dropdownHeight = _scrollRect.content.rect.height;
			}
			else
			{
				dropdownHeight = listRect.sizeDelta.y;
			}

			float halfBaseHeight = selfRect.sizeDelta.y * 0.5f;
			listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, dropdownHeight + halfBaseHeight);
			listRect.anchoredPosition = new Vector2(listRect.anchoredPosition.x, -halfBaseHeight);
			if (_scrollRect.viewport != null)
			{
				_scrollRect.viewport.offsetMax = new Vector2(0f, -halfBaseHeight);
			}

			if (_baseObject == null || _baseObject.GetComponent<Canvas>() != null)
			{
				return;
			}

			var sourceCanvas = _scrollRect.GetComponent<Canvas>();
			if (sourceCanvas == null)
			{
				return;
			}

			var canvas = _baseObject.AddComponent<Canvas>();
			canvas.renderMode = sourceCanvas.renderMode;
			canvas.worldCamera = sourceCanvas.worldCamera;
			canvas.planeDistance = sourceCanvas.planeDistance;
			canvas.overrideSorting = sourceCanvas.overrideSorting;
			canvas.sortingLayerID = sourceCanvas.sortingLayerID;
			canvas.sortingOrder = sourceCanvas.sortingOrder + 1;
		}

		protected override GameObject CreateDropdownList(GameObject template)
		{
			GameObject dropdownList = base.CreateDropdownList(template);
			_scrollRect = dropdownList != null ? dropdownList.GetComponent<CustomScrollRect>() : null;
			_scrollRect ??= dropdownList != null ? dropdownList.GetComponentInChildren<CustomScrollRect>(true) : null;
			_scrollRect ??= dropdownList != null ? dropdownList.GetComponentInChildren<ScrollRect>(true) : null;
			DropdownItem[] items = dropdownList != null ? dropdownList.GetComponentsInChildren<DropdownItem>() : null;
			_templateItem = items != null && items.Length > 0 ? items[0] : null;
			if (_enableItemClickEvent)
			{
				SetupItemClickEvents(items);
			}
			return dropdownList;
		}

		private void SetupItemClickEvents(DropdownItem[] items)
		{
			if (items == null)
			{
				return;
			}
			int count = Mathf.Min(items.Length, options.Count);
			for (int i = 0; i < count; i++)
			{
				if (items[i] == null || items[i].GetComponent<DropdownItemClickHandler>() != null)
				{
					continue;
				}
				items[i].gameObject.AddComponent<DropdownItemClickHandler>();
			}
		}

		protected override void DestroyDropdownList(GameObject dropdownList)
		{
			SetArrowObject(false);
			_baseObject.RemoveComponentIfExists<Canvas>();
			base.DestroyDropdownList(dropdownList);
		}

		protected override GameObject CreateBlocker(Canvas rootCanvas)
		{
			GameObject blocker = base.CreateBlocker(rootCanvas);
			if (!_centerScrollOnOpen)
			{
				TargetScrollPosition();
			}
			return blocker;
		}

		private void TargetScrollPosition()
		{
			if (_scrollRect == null || _scrollRect.viewport == null || _scrollRect.content == null || _templateItem == null || options.Count <= 0)
			{
				return;
			}

			float viewportHeight = _scrollRect.viewport.rect.height;
			float contentHeight = _scrollRect.content.rect.height;
			float itemHeight = _templateItem.rectTransform != null ? _templateItem.rectTransform.rect.height : 0f;
			float targetY = contentHeight * ((float)value / options.Count) + itemHeight * 0.5f - viewportHeight * 0.5f;
			float maxY = Mathf.Max(0f, contentHeight - viewportHeight);
			Vector2 position = _scrollRect.content.anchoredPosition;
			position.y = Mathf.Clamp(targetY, 0f, maxY);
			_scrollRect.content.anchoredPosition = position;
		}

		private void SetArrowObject(bool isSelected)
		{
			if (_arrowObject != null)
			{
				_arrowObject.SetActive(isSelected);
			}
		}

		private void AdjustHeight()
		{
			if (_scrollRect == null)
			{
				return;
			}
			var rectTransform = _scrollRect.GetComponent<RectTransform>();
			if (rectTransform == null)
			{
				return;
			}

			Vector2 screenPoint;
			if (ScreenManager.Instance != null)
			{
				screenPoint = ScreenManager.Instance.WorldToScreenPoint(transform.position, Vector2.zero);
			}
			else
			{
				screenPoint = RectTransformUtility.WorldToScreenPoint(null, transform.position);
			}

			float overflowBelow = rectTransform.rect.height - screenPoint.y;
			if (overflowBelow < 0f)
			{
				return;
			}

			float availableHeight = rectTransform.rect.height - overflowBelow - Screen.safeArea.yMin - 10f;
			float height = Mathf.Min(rectTransform.rect.height, Mathf.Max(0f, availableHeight));
			rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);
		}

		public CustomDropdown()
		{
			_onItemClickCallbacks = new Dictionary<int, Action>();
		}
	}
}
