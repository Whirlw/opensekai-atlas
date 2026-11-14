using System;
using DG.Tweening;
using UnityEngine;

namespace Sekai
{
	public class LiveSpriteButton : MonoBehaviour
	{
		private struct ButtonBounds
		{
			public float left;

			public float bottom;

			public float right;

			public float top;
		}

		private static readonly int DisableId;

		[SerializeField]
		private SpriteRenderer _spriteRenderer;

		[SerializeField]
		protected float pressScale;

		[SerializeField]
		protected float raycastPadding;

		private float _scaleTime;

		private Action _onClick;

		private bool _isEnable;

		private int _touchId;

		private ButtonBounds _bounds;

		private Vector3 _baseScale;

		private Tweener _pressTween;

		public void Setup(Action _onClick)
		{
			this._onClick = _onClick;
			_touchId = DisableId;
			_baseScale = transform.localScale;
		}

		public void CalculateBounds(Camera camera)
		{
			_isEnable = true;
			if (_spriteRenderer == null)
			{
				_spriteRenderer = GetComponent<SpriteRenderer>();
			}
			if (_spriteRenderer == null || camera == null)
			{
				_isEnable = false;
				return;
			}
			Bounds rendererBounds = _spriteRenderer.bounds;
			Vector3 min = camera.WorldToScreenPoint(rendererBounds.min);
			Vector3 max = camera.WorldToScreenPoint(rendererBounds.max);
			_bounds.left = Mathf.Min(min.x, max.x) - raycastPadding;
			_bounds.right = Mathf.Max(min.x, max.x) + raycastPadding;
			_bounds.bottom = Mathf.Min(min.y, max.y) - raycastPadding;
			_bounds.top = Mathf.Max(min.y, max.y) + raycastPadding;
		}

		private void UpdateMouse()
		{
			if (!_isEnable)
			{
				return;
			}
			if (NativeInput.PointerPressedThisFrame && _touchId == DisableId && Contains(NativeInput.PointerPosition))
			{
				ScaleDown();
				_touchId = 0;
			}
			else if (NativeInput.PointerReleasedThisFrame && _touchId == 0)
			{
				if (Contains(NativeInput.PointerPosition))
				{
					OnClick();
					ScaleUp();
				}
				else
				{
					ScaleUp();
				}
				_touchId = DisableId;
			}
		}

		private void UpdateTouches()
		{
			if (!_isEnable)
			{
				return;
			}
			Touch[] touches = NativeInput.Touches;
			for (int i = 0; i < touches.Length; i++)
			{
				Touch touch = touches[i];
				Vector2 position = touch.position;
				if (touch.phase == TouchPhase.Began)
				{
					if (_touchId == DisableId && Contains(position))
					{
						ScaleDown();
						_touchId = touch.fingerId;
						return;
					}
				}
				else if (touch.phase == TouchPhase.Ended)
				{
					if (_touchId == touch.fingerId)
					{
						if (Contains(position))
						{
							OnClick();
						}
						ScaleUp();
						_touchId = DisableId;
						return;
					}
				}
			}
		}

		private void ScaleDown()
		{
			KillPressTween();
			_pressTween = transform.DOScale(_baseScale * pressScale, _scaleTime);
		}

		private void ScaleUp()
		{
			KillPressTween();
			_pressTween = transform.DOScale(_baseScale, _scaleTime);
		}

		private void KillPressTween()
		{
			if (_pressTween != null && _pressTween.IsActive())
			{
				_pressTween.Kill();
			}
		}

		private void LateUpdate()
		{
			UpdateTouches();
			if (NativeInput.TouchCount == 0)
			{
				UpdateMouse();
			}
		}

		public void OnClick()
		{
			_onClick?.Invoke();
		}

		public LiveSpriteButton()
		{
			_scaleTime = 0.125f;
			pressScale = 0.95f;
			raycastPadding = 24f;
			_touchId = DisableId;
		}

		static LiveSpriteButton()
		{
			DisableId = int.MinValue;
		}

		private bool Contains(Vector2 position)
		{
			return _bounds.left <= position.x
				&& _bounds.right >= position.x
				&& _bounds.bottom <= position.y
				&& _bounds.top >= position.y;
		}
	}
}
