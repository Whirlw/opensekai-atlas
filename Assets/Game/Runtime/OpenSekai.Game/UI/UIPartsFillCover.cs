using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsFillCover : MonoBehaviour
	{
		[SerializeField]
		private ColorFader _fader;

		[SerializeField]
		private CustomImage _fillImage;

		[SerializeField]
		private CustomButton _closeButton;

		public Color FillColor
		{
			get => _fillImage != null ? _fillImage.color : Color.clear;
			set
			{
				if (_fillImage != null)
				{
					_fillImage.color = value;
				}
				if (_fader != null)
				{
					_fader.Set(value);
				}
			}
		}

		public ColorFader Fader => _fader;
		public CustomButton CloseButton => _closeButton;

		public bool EnableClick
		{
			get => _closeButton != null && _closeButton.interactable && _fillImage != null && _fillImage.raycastTarget;
			set
			{
				if (_closeButton != null)
				{
					_closeButton.interactable = value;
				}
				if (_fillImage != null)
				{
					_fillImage.raycastTarget = value;
				}
			}
		}

		private void Awake()
		{
			if (_fillImage == null)
			{
				_fillImage = GetComponent<CustomImage>();
			}
			if (_closeButton == null)
			{
				_closeButton = GetComponent<CustomButton>();
			}
			if (_fader == null)
			{
				_fader = GetComponent<ColorFader>();
			}

			if (_fader != null && _fillImage != null)
			{
				_fader.Set(_fillImage.color);
			}
		}
	}
}
