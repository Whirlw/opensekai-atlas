using System;
using System.Runtime.CompilerServices;
using Beebyte.Obfuscator;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	[AddComponentMenu("Custom UI/Custom Scroll Rect")]
	public class CustomScrollRect : ScrollRect
	{
		public class ReachedEdgeData
		{
			public bool Top
			{
				[CompilerGenerated]
				get
				{
					return _Top;
				}
				[CompilerGenerated]
				set
				{
					_Top = value;
				}
			}

			public bool Bottom
			{
				[CompilerGenerated]
				get
				{
					return _Bottom;
				}
				[CompilerGenerated]
				set
				{
					_Bottom = value;
				}
			}

			public bool Left
			{
				[CompilerGenerated]
				get
				{
					return _Left;
				}
				[CompilerGenerated]
				set
				{
					_Left = value;
				}
			}

			public bool Right
			{
				[CompilerGenerated]
				get
				{
					return _Right;
				}
				[CompilerGenerated]
				set
				{
					_Right = value;
				}
			}

			[CompilerGenerated]
			private bool _Top;

			[CompilerGenerated]
			private bool _Bottom;

			[CompilerGenerated]
			private bool _Left;

			[CompilerGenerated]
			private bool _Right;

			public ReachedEdgeData()
			{
			}
		}

		[SerializeField]
		private bool _absolutelyControl;

		[SerializeField]
		private bool _disableVerticalScrollByAuto;

		[SerializeField]
		private bool _disableHorizontalScrollByAuto;

		[SerializeField]
		private ScrollMask _mask;

				[SerializeField] private Action _OnBeginDragEvent;
		public Action OnBeginDragEvent
		{
		    get => _OnBeginDragEvent;
		    set => _OnBeginDragEvent = value;
		}

				[SerializeField] private Action _OnDragEvent;
		public Action OnDragEvent
		{
		    get => _OnDragEvent;
		    set => _OnDragEvent = value;
		}

				[SerializeField] private Action _OnEndDragEvent;
		public Action OnEndDragEvent
		{
		    get => _OnEndDragEvent;
		    set => _OnEndDragEvent = value;
		}

		public float Velocity
		{
			get
			{
				return velocity.magnitude;
			}
		}

		public bool Scrollable
		{
			get
			{
				return CheckScrollableNeed(vertical);
			}
		}

		public bool HasMask
		{
			get
			{
				return _mask != null;
			}
		}

		public Vector2 MaskSoftness
		{
			get
			{
				return _mask != null ? _mask.MaskSoftness : Vector2.zero;
			}
		}

		public override void LayoutComplete()
		{
			base.LayoutComplete();
			SetMask();
		}

		public override void SetLayoutHorizontal()
		{
			base.SetLayoutHorizontal();
			SetMask();
		}

		public override void SetLayoutVertical()
		{
			base.SetLayoutVertical();
			SetMask();
		}

		private void SetMask()
		{
			if (_mask == null)
			{
				return;
			}

			_mask.SetMaskEnabled(true);
			var isVertical = vertical || !horizontal;
			if (!CheckScrollableNeed(isVertical))
			{
				_mask.ResetMaskPadding(vertical);
			}

			SetMaskByScroll();
		}

		[Skip]
		public override void OnBeginDrag(PointerEventData eventData)
		{
			UpdateScrollableByAuto();
			// TODO(original): restore InputManager touch-control arbitration.
			base.OnBeginDrag(eventData);
			OnBeginDragEvent?.Invoke();
		}

		[Skip]
		public override void OnDrag(PointerEventData eventData)
		{
			base.OnDrag(eventData);
			OnDragEvent?.Invoke();
		}

		[Skip]
		public override void OnEndDrag(PointerEventData eventData)
		{
			base.OnEndDrag(eventData);
			OnEndDragEvent?.Invoke();
		}

		private bool CheckScrollableNeed(bool isVertical)
		{
			if (viewport == null || content == null)
			{
				return false;
			}

			return isVertical ? viewport.rect.height < content.rect.height : viewport.rect.width < content.rect.width;
		}

		private void UpdateScrollableByAuto()
		{
			if (_disableVerticalScrollByAuto)
			{
				vertical = CheckScrollableNeed(true);
			}

			if (_disableHorizontalScrollByAuto)
			{
				horizontal = CheckScrollableNeed(false);
			}
		}

		protected override void SetContentAnchoredPosition(Vector2 position)
		{
			base.SetContentAnchoredPosition(position);
			SetMask();
		}

		public ReachedEdgeData GetReachedEdge()
		{
			var result = new ReachedEdgeData();
			if (viewport == null || content == null)
			{
				return result;
			}

			var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content);
			var rect = viewport.rect;
			if (vertical)
			{
				result.Top = rect.yMax >= bounds.max.y - 0.1f;
				result.Bottom = rect.yMin <= bounds.min.y + 0.1f;
			}
			else if (horizontal)
			{
				result.Left = rect.xMin <= bounds.min.x + 0.1f;
				result.Right = rect.xMax >= bounds.max.x - 0.1f;
			}

			return result;
		}

		private void SetMaskByScroll()
		{
			if (_mask == null || viewport == null || content == null || !CheckScrollableNeed(vertical))
			{
				return;
			}

			var viewportCorners = new Vector3[4];
			var contentCorners = new Vector3[4];
			viewport.GetWorldCorners(viewportCorners);
			content.GetWorldCorners(contentCorners);

			var screenManager = ScreenManager.Instance;
			var leftBottomViewportPos = screenManager != null
				? screenManager.WorldToScreenPoint(viewportCorners[0])
				: RectTransformUtility.WorldToScreenPoint(null, viewportCorners[0]);
			var rightTopViewportPos = screenManager != null
				? screenManager.WorldToScreenPoint(viewportCorners[2])
				: RectTransformUtility.WorldToScreenPoint(null, viewportCorners[2]);
			var leftBottomContentPos = screenManager != null
				? screenManager.WorldToScreenPoint(contentCorners[0])
				: RectTransformUtility.WorldToScreenPoint(null, contentCorners[0]);
			var rightTopContentPos = screenManager != null
				? screenManager.WorldToScreenPoint(contentCorners[2])
				: RectTransformUtility.WorldToScreenPoint(null, contentCorners[2]);

			if (vertical)
			{
				_mask.SetMaskVertical(leftBottomViewportPos, rightTopViewportPos, leftBottomContentPos, rightTopContentPos);
			}
			else if (horizontal)
			{
				_mask.SetMaskHorizontal(leftBottomViewportPos, rightTopViewportPos, leftBottomContentPos, rightTopContentPos);
			}
		}

		public CustomScrollRect()
		{
		}
	}
}
