using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[ExecuteInEditMode]
	public class UIPartsImageOutline : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_targetImage")]
		[SerializeField]
		private CustomImage targetImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_outline")]
		[SerializeField]
		private float outline;

		private RectTransform _targetTransform;

		private RectTransform _myTransform;

		private void Awake()
		{
			SetupTransforms();
		}

		private void SetupTransforms()
		{
			_targetTransform = targetImage != null ? targetImage.transform as RectTransform : null;
			_myTransform = transform as RectTransform;
		}

		private void Update()
		{
			if (_targetTransform == null || _myTransform == null)
			{
				SetupTransforms();
				if (_targetTransform == null || _myTransform == null)
				{
					return;
				}
			}

			_myTransform.anchoredPosition = _targetTransform.anchoredPosition;
			Vector2 targetSize = _targetTransform.sizeDelta;
			Vector3 targetScale = _targetTransform.localScale;
			_myTransform.sizeDelta = new Vector2(
				targetSize.x * targetScale.x + outline,
				targetSize.y * targetScale.y + outline);
		}

		public UIPartsImageOutline()
		{
		}
	}
}
