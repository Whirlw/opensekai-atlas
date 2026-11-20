using System;
using LitMotion;
using LitMotion.Extensions;
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

		private static readonly int _disableId;

		[UnityEngine.Serialization.FormerlySerializedAs("_spriteRenderer")]
		[SerializeField]
		private SpriteRenderer spriteRenderer;

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

		private MotionHandle _pressMotion;

		public void Setup(Action _onClick)
		{
			this._onClick = _onClick;
			_touchId = _disableId;
			_baseScale = transform.localScale;
		}

		public void CalculateBounds(Camera camera)
		{
			_isEnable = true;
			if (spriteRenderer == null)
			{
				spriteRenderer = GetComponent<SpriteRenderer>();
			}
			if (spriteRenderer == null || camera == null)
			{
				_isEnable = false;
				return;
			}
			Bounds rendererBounds = spriteRenderer.bounds;
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
			if (NativeInput.PointerPressedThisFrame && _touchId == _disableId && Contains(NativeInput.PointerPosition))
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
				_touchId = _disableId;
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
					if (_touchId == _disableId && Contains(position))
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
						_touchId = _disableId;
						return;
					}
				}
			}
		}

		private void ScaleDown()
		{
			PlayScale(_baseScale * pressScale);
		}

		private void ScaleUp()
		{
			PlayScale(_baseScale);
		}

		private void PlayScale(Vector3 scale)
		{
			if (!isActiveAndEnabled)
			{
				return;
			}
			_pressMotion.TryCancel();
			_pressMotion = LMotion.Create(transform.localScale, scale, _scaleTime)
				.WithEase(Ease.OutQuad)
				.WithScheduler(Live.LiveMotion.Scheduler())
				.BindToLocalScale(transform);
		}

		private void OnDisable()
		{
			if (_pressMotion.TryCancel() || _touchId != _disableId)
			{
				transform.localScale = _baseScale;
			}
			_pressMotion = default;
			_touchId = _disableId;
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
			_touchId = _disableId;
		}

		static LiveSpriteButton()
		{
			_disableId = int.MinValue;
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
