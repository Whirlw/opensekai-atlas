using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	public sealed class DialogSizeFitter : MonoBehaviour
	{
		public enum ViewType
		{
			Default = 0,
			HasTab = 1
		}

		private static readonly Dictionary<DialogSize, float> SIZE_DIC;

		private static readonly float _bottomPadding;

		[SerializeField]
		private RectTransform _windowRect;

		[SerializeField]
		private LayoutElement _paddingLayoutElement;

		[SerializeField]
		private LayoutElement _content;

		public void Setup(DialogSize dialogSize, ViewType viewType = ViewType.Default)
		{
			if (dialogSize != DialogSize.Manual && SIZE_DIC.TryGetValue(dialogSize, out float width))
			{
				if (_windowRect != null)
				{
					Vector2 sizeDelta = _windowRect.sizeDelta;
					sizeDelta.x = width;
					_windowRect.sizeDelta = sizeDelta;
				}
				if (_content != null)
				{
					_content.preferredWidth = width;
				}
			}

			if (_paddingLayoutElement != null)
			{
				_paddingLayoutElement.preferredHeight = viewType == ViewType.HasTab ? 0f : _bottomPadding;
			}
		}

		public DialogSizeFitter()
		{
		}

		static DialogSizeFitter()
		{
			// Restored enough for copied dialog prefabs; exact original size constants
			// should be checked against IDA if another dialog depends on pixel parity.
			SIZE_DIC = new Dictionary<DialogSize, float>
			{
				{ DialogSize.Small, 720f },
				{ DialogSize.Medium, 960f },
				{ DialogSize.Large, 1200f }
			};
			_bottomPadding = 32f;
		}
	}
}
