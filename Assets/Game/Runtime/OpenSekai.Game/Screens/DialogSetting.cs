using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public sealed class DialogSetting : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_sizeFitter")]
		[SerializeField]
		private DialogSizeFitter sizeFitter;

		[UnityEngine.Serialization.FormerlySerializedAs("_tabGroup")]
		[SerializeField]
		private UIPartsDialogTabGroup tabGroup;

		[UnityEngine.Serialization.FormerlySerializedAs("_buttonGroup")]
		[SerializeField]
		private UIPartsDialogButtonGroup buttonGroup;

		[UnityEngine.Serialization.FormerlySerializedAs("_dialogSize")]
		[SerializeField]
		private DialogSize dialogSize;

		[UnityEngine.Serialization.FormerlySerializedAs("_tabCount")]
		[SerializeField]
		private int tabCount;

		[UnityEngine.Serialization.FormerlySerializedAs("_buttonCount")]
		[SerializeField]
		private int buttonCount;

		public DialogSizeFitter SizeFitter
		{
			get
			{
				return sizeFitter;
			}
		}

		public CustomIndexToggleGroup ToggleGroup
		{
			get
			{
				return tabGroup != null ? tabGroup.ToggleGroup : null;
			}
		}

		public void Execute()
		{
			SetSize();
			SetTab(tabCount);
			SetButton(buttonCount);
		}

		public void SetSize(DialogSize size)
		{
			dialogSize = size;
			SetSize();
		}

		public void SetSize()
		{
			if (sizeFitter != null)
			{
				sizeFitter.Setup(dialogSize, tabGroup != null && tabCount > 0 ? DialogSizeFitter.ViewType.HasTab : DialogSizeFitter.ViewType.Default);
			}
		}

		public void SetTab(int count)
		{
			tabCount = count;
			if (tabGroup != null)
			{
				tabGroup.Setup(tabCount);
			}
			SetSize();
		}

		public void SetButton(int count)
		{
			buttonCount = count;
			if (buttonGroup != null)
			{
				buttonGroup.Setup(buttonCount);
			}
		}

		public void SetButtonSize(UIPartsCommonButton.SizeEnum size)
		{
			buttonGroup?.SetButtonSize(size);
		}

		public DialogSetting()
		{
		}
	}
}
