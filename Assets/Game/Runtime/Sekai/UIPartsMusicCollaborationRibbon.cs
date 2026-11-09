using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsMusicCollaborationRibbon : MonoBehaviour
	{
		[SerializeField]
		private CustomTextMesh _collaborationText;

		[SerializeField]
		private float _collaborationTextPadding;

		[SerializeField]
		private float _widthMax;

		[SerializeField]
		private CustomImage _coverImage;

		public bool Lock
		{
			get
			{
				return _coverImage != null && _coverImage.gameObject.activeSelf;
			}
			set
			{
				if (_coverImage != null)
				{
					_coverImage.gameObject.SetActive(value);
				}
			}
		}

		public void SetText(string text)
		{
			if (_collaborationText == null)
			{
				return;
			}
			_collaborationText.SetText(text);
			RectTransform rectTransform = transform as RectTransform;
			if (rectTransform == null)
			{
				return;
			}
			Vector2 size = rectTransform.sizeDelta;
			float preferredWidth = _collaborationText.preferredWidth + _collaborationTextPadding;
			size.x = Mathf.Min(preferredWidth, _widthMax);
			rectTransform.sizeDelta = size;
		}

		public UIPartsMusicCollaborationRibbon()
		{
			_collaborationTextPadding = 18f;
			_widthMax = 856f;
		}
	}
}
