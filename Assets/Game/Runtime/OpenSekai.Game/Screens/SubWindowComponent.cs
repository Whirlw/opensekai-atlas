using System;
using Sekai.SubWindow;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class SubWindowComponent : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_windowType")]
		[SerializeField]
		private WindowType windowType;

		[UnityEngine.Serialization.FormerlySerializedAs("_contentTransform")]
		[SerializeField]
		private RectTransform contentTransform;

		[UnityEngine.Serialization.FormerlySerializedAs("_backPanelTransform")]
		[SerializeField]
		private RectTransform backPanelTransform;

		[UnityEngine.Serialization.FormerlySerializedAs("_closeButtons")]
		[SerializeField]
		private CustomButton[] closeButtons;

		public WindowType GetWindowType
		{
			get
			{
				return windowType;
			}
		}

		public Vector2 WindowSize
		{
			get
			{
				return backPanelTransform != null ? backPanelTransform.sizeDelta : Vector2.zero;
			}
		}

		public Vector2 ContentSize
		{
			get
			{
				return contentTransform != null ? contentTransform.sizeDelta : Vector2.zero;
			}
		}

		public void Setup(Action onClose)
		{
			SetBackPanel();
			SetCallback(onClose);
		}

		public void SetBackPanel()
		{
			if (backPanelTransform == null || contentTransform == null)
			{
				return;
			}
			backPanelTransform.SetAsFirstSibling();
		}

		public void SetCallback(Action onClose)
		{
			if (closeButtons == null)
			{
				return;
			}
			for (int i = 0; i < closeButtons.Length; i++)
			{
				CustomButton button = closeButtons[i];
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
