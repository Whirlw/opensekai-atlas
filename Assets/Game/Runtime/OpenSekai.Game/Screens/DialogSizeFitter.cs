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

		private static readonly Dictionary<DialogSize, float> _sizeDIC;

		private static readonly float _bottomPadding;

		[UnityEngine.Serialization.FormerlySerializedAs("_windowRect")]
		[SerializeField]
		private RectTransform windowRect;

		[UnityEngine.Serialization.FormerlySerializedAs("_paddingLayoutElement")]
		[SerializeField]
		private LayoutElement paddingLayoutElement;

		[UnityEngine.Serialization.FormerlySerializedAs("_content")]
		[SerializeField]
		private LayoutElement content;

		public void Setup(DialogSize dialogSize, ViewType viewType = ViewType.Default)
		{
			if (dialogSize != DialogSize.Manual && _sizeDIC.TryGetValue(dialogSize, out float width))
			{
				if (windowRect != null)
				{
					Vector2 sizeDelta = windowRect.sizeDelta;
					sizeDelta.x = width;
					windowRect.sizeDelta = sizeDelta;
				}
				if (content != null)
				{
					content.preferredWidth = width;
				}
			}

			if (paddingLayoutElement != null)
			{
				paddingLayoutElement.preferredHeight = viewType == ViewType.HasTab ? 0f : _bottomPadding;
			}
		}

		public DialogSizeFitter()
		{
		}

		static DialogSizeFitter()
		{
			// Size constants used by the dialog prefabs.
			// Adjust them only with a visual check.
			_sizeDIC = new Dictionary<DialogSize, float>
			{
				{ DialogSize.Small, 720f },
				{ DialogSize.Medium, 960f },
				{ DialogSize.Large, 1200f }
			};
			_bottomPadding = 32f;
		}
	}
}
