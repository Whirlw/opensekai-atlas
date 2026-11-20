using System.Collections.Generic;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[RequireComponent(typeof(CustomIndexToggleGroup))]
	public sealed class UIPartsDialogTabGroup : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_toggleGroup")]
		[SerializeField]
		private CustomIndexToggleGroup toggleGroup;

		[UnityEngine.Serialization.FormerlySerializedAs("_tabPrefab")]
		[SerializeField]
		private UIPartsDialogTab tabPrefab;

		private readonly List<UIPartsDialogTab> _tabList;

		public CustomIndexToggleGroup ToggleGroup
		{
			get
			{
				return toggleGroup;
			}
		}

		public void Setup()
		{
			if (toggleGroup == null)
			{
				toggleGroup = GetComponent<CustomIndexToggleGroup>();
			}
			SetupTab();
		}

		private void OnSelectedIndexChanged(int index)
		{
			for (int i = 0; i < _tabList.Count; i++)
			{
				UIPartsDialogTab tab = _tabList[i];
				if (tab != null)
				{
					tab.IsOn = index == i;
				}
			}
		}

		private void OnDestroy()
		{
			if (toggleGroup != null)
			{
				toggleGroup.SelectedIndexChanged -= OnSelectedIndexChanged;
			}
		}


		private void SetupTab()
		{
			_tabList.Clear();
			GetComponentsInChildren(true, _tabList);
			toggleGroup.SelectedIndexChanged -= OnSelectedIndexChanged;
			foreach (var tab in _tabList)
			{
				tab.Setup(null);
				var toggle = tab.GetComponent<CustomToggle>();
				if (!toggleGroup.IndexToggles.Contains(toggle))
				{
					toggleGroup.IndexToggles.Add(toggle);
				}
			}

			toggleGroup.CollectToggles(false);
			int index = toggleGroup.SelectedIndex;
			if (toggleGroup.IndexToggles.Count > 0)
			{
				toggleGroup.SetSelectedIndexWithoutNotify(index);
			}
			OnSelectedIndexChanged(index);
			toggleGroup.SelectedIndexChanged += OnSelectedIndexChanged;
		}

		public void Setup(int tabCount)
		{
			if (toggleGroup == null)
			{
				toggleGroup = GetComponent<CustomIndexToggleGroup>();
			}
			tabCount = Mathf.Max(0, tabCount);
			if (tabPrefab != null)
			{
				for (int i = transform.childCount; i < tabCount; i++)
				{
					Instantiate(tabPrefab, transform, false);
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
