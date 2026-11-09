using UnityEngine;

namespace Sekai.UI
{
	public class UIPartsLeftTabList : UIPartsLeftTabListBase
	{
		[SerializeField]
		private RectTransform _backPanelTransform;

		public float BackPanelSize
		{
			get
			{
				return _backPanelTransform != null ? _backPanelTransform.rect.width : 0f;
			}
		}

		public void SetDisplayingSubTab(bool isDisplaying)
		{
			if (cellDataList == null)
			{
				return;
			}

			foreach (var data in cellDataList)
			{
				if (data != null)
				{
					data.IsDisplayingSubTab = isDisplaying;
				}
			}
		}

		protected override bool AllowsPublishSelectEvent(UIPartsLeftTabListCellBase.ViewData beforeSelectedData, UIPartsLeftTabListCellBase.ViewData viewData)
		{
			return viewData != null && (viewData.HasSubData || !Equals(viewData, beforeSelectedData));
		}

		public UIPartsLeftTabList()
		{
		}
	}
}
