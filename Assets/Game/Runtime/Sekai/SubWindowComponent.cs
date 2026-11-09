using System;
using Sekai.SubWindow;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class SubWindowComponent : MonoBehaviour
	{
		[SerializeField]
		private WindowType _windowType;

		[SerializeField]
		private RectTransform _contentTransform;

		[SerializeField]
		private RectTransform _backPanelTransform;

		[SerializeField]
		private CustomButton[] _closeButtons;

		public WindowType GetWindowType
		{
			get
			{
				return _windowType;
			}
		}

		public Vector2 WindowSize
		{
			get
			{
				return _backPanelTransform != null ? _backPanelTransform.sizeDelta : Vector2.zero;
			}
		}

		public Vector2 ContentSize
		{
			get
			{
				return _contentTransform != null ? _contentTransform.sizeDelta : Vector2.zero;
			}
		}

		public void Setup(Action onClose)
		{
			SetBackPanel();
			SetCallback(onClose);
		}

		public void SetBackPanel()
		{
			if (_backPanelTransform == null || _contentTransform == null)
			{
				return;
			}
			_backPanelTransform.SetAsFirstSibling();
		}

		public void SetCallback(Action onClose)
		{
			if (_closeButtons == null)
			{
				return;
			}
			for (int i = 0; i < _closeButtons.Length; i++)
			{
				CustomButton button = _closeButtons[i];
				if (button == null)
				{
					continue;
				}
				button.onClick.RemoveAllListeners();
				if (onClose != null)
				{
					button.onClick.AddListener(() => onClose());
				}
			}
		}

		public SubWindowComponent()
		{
		}
	}
}
