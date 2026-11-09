using UnityEngine;
using UnityEngine.UI;

namespace Sekai.UI
{
	public class ScrollMask : MonoBehaviour
	{
		[SerializeField]
		private RectMask2D _mask;

		[SerializeField]
		private float _maskThreshold;

		public Vector2 MaskSoftness
		{
			get
			{
				return _mask != null ? new Vector2(_mask.softness.x, _mask.softness.y) : Vector2.zero;
			}
		}

		public void SetMaskVertical(Vector2 leftBottomViewportPos, Vector2 rightTopViewportPos, Vector2 leftBottomContentPos, Vector2 rightTopContentPos)
		{
			if (_mask == null || _maskThreshold <= 0f)
			{
				return;
			}

			var halfSoftness = _mask.softness.y * 0.5f;
			var topRate = Mathf.Min(Mathf.Abs(rightTopViewportPos.y - rightTopContentPos.y), _maskThreshold) / _maskThreshold;
			var bottomRate = Mathf.Min(Mathf.Abs(leftBottomContentPos.y - leftBottomViewportPos.y), _maskThreshold) / _maskThreshold;
			_mask.padding = new Vector4(0f, halfSoftness * bottomRate - halfSoftness, 0f, halfSoftness * topRate - halfSoftness);
		}

		public void SetMaskHorizontal(Vector2 leftBottomViewportPos, Vector2 rightTopViewportPos, Vector2 leftBottomContentPos, Vector2 rightTopContentPos)
		{
			if (_mask == null || _maskThreshold <= 0f)
			{
				return;
			}

			var halfSoftness = _mask.softness.x * 0.5f;
			var leftRate = Mathf.Min(Mathf.Abs(leftBottomContentPos.x - leftBottomViewportPos.x), _maskThreshold) / _maskThreshold;
			var rightRate = Mathf.Min(Mathf.Abs(rightTopViewportPos.x - rightTopContentPos.x), _maskThreshold) / _maskThreshold;
			_mask.padding = new Vector4(halfSoftness * leftRate - halfSoftness, 0f, halfSoftness * rightRate - halfSoftness, 0f);
		}

		public void SetMaskEnabled(bool maskEnabled)
		{
			if (_mask != null)
			{
				_mask.enabled = maskEnabled;
			}
		}

		public void ResetMaskPadding(bool isVertical)
		{
			if (isVertical)
			{
				SetMaskVertical(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
			}
			else
			{
				SetMaskHorizontal(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
			}
		}

		public ScrollMask()
		{
		}
	}
}
