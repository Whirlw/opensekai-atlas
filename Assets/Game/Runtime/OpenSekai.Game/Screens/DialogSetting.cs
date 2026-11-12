using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public sealed class DialogSetting : MonoBehaviour
	{
		[SerializeField]
		private DialogSizeFitter _sizeFitter;

		[SerializeField]
		private UIPartsDialogTabGroup _tabGroup;

		[SerializeField]
		private UIPartsDialogButtonGroup _buttonGroup;

		[SerializeField]
		private DialogSize _dialogSize;

		[SerializeField]
		private int _tabCount;

		[SerializeField]
		private int _buttonCount;

		public DialogSizeFitter SizeFitter
		{
			get
			{
				return _sizeFitter;
			}
		}

		public CustomIndexToggleGroup ToggleGroup
		{
			get
			{
				return _tabGroup != null ? _tabGroup.ToggleGroup : null;
			}
		}

		public void Execute()
		{
			SetSize();
			SetTab(_tabCount);
			SetButton(_buttonCount);
		}

		public void SetSize(DialogSize size)
		{
			_dialogSize = size;
			SetSize();
		}

		public void SetSize()
		{
			if (_sizeFitter != null)
			{
				_sizeFitter.Setup(_dialogSize, _tabGroup != null && _tabCount > 0 ? DialogSizeFitter.ViewType.HasTab : DialogSizeFitter.ViewType.Default);
			}
		}

		public void SetTab(int count)
		{
			_tabCount = count;
			if (_tabGroup != null)
			{
				_tabGroup.Setup(_tabCount);
			}
			SetSize();
		}

		public void SetButton(int count)
		{
			_buttonCount = count;
			if (_buttonGroup != null)
			{
				_buttonGroup.Setup(_buttonCount);
			}
		}

		public void SetButtonSize(UIPartsCommonButton.SizeEnum size)
		{
			_buttonGroup?.SetButtonSize(size);
		}

		public DialogSetting()
		{
		}
	}
}
