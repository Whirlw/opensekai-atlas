using System.Collections.Generic;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[RequireComponent(typeof(CustomIndexToggleGroup))]
	public sealed class UIPartsDialogTabGroup : MonoBehaviour
	{
		[SerializeField]
		private CustomIndexToggleGroup _toggleGroup;

		[SerializeField]
		private UIPartsDialogTab _tabPrefab;

		private readonly List<UIPartsDialogTab> _tabList;

		public CustomIndexToggleGroup ToggleGroup
		{
			get
			{
				return _toggleGroup;
			}
		}

		public void Setup()
		{
			if (_toggleGroup == null)
			{
				_toggleGroup = GetComponent<CustomIndexToggleGroup>();
			}
			SetupTab();
		}

		private void OnTabSelectedChanged()
		{
			for (int i = 0; i < _tabList.Count; i++)
			{
				UIPartsDialogTab tab = _tabList[i];
				if (tab != null)
				{
					tab.IsOn = _toggleGroup != null && _toggleGroup.SelectedIndex == i;
				}
			}
		}

		private void SetupTab()
		{
			_tabList.Clear();
			GetComponentsInChildren(true, _tabList);
			for (int i = 0; i < _tabList.Count; i++)
			{
				_tabList[i]?.Setup(OnTabSelectedChanged);
			}
			if (_toggleGroup != null)
			{
				_toggleGroup.CollectToggles(false);
				_toggleGroup.OnSelectedIndexChanged -= _ => OnTabSelectedChanged();
				_toggleGroup.OnSelectedIndexChanged += _ => OnTabSelectedChanged();
			}
			OnTabSelectedChanged();
		}

		public void Setup(int tabCount)
		{
			if (_toggleGroup == null)
			{
				_toggleGroup = GetComponent<CustomIndexToggleGroup>();
			}
			tabCount = Mathf.Max(0, tabCount);
			if (_tabPrefab != null)
			{
				for (int i = transform.childCount; i < tabCount; i++)
				{
					Instantiate(_tabPrefab, transform, false);
				}
			}
			for (int i = 0; i < transform.childCount; i++)
			{
				transform.GetChild(i).gameObject.SetActive(i < tabCount);
			}
			SetupTab();
		}

		public void ShowBadge(int index, bool isShow)
		{
			if (index >= 0 && index < _tabList.Count && _tabList[index] != null)
			{
				_tabList[index].IsShowBadge = isShow;
			}
		}

		public UIPartsDialogTabGroup()
		{
			_tabList = new List<UIPartsDialogTab>();
		}
	}
}
