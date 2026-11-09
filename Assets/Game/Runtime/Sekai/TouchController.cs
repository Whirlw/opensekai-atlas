using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sekai
{
	public class TouchController : MonoBehaviour
	{
				[SerializeField] private Action<Vector2> _OnTouch;
		public Action<Vector2> OnTouch
		{
		    get => _OnTouch;
		    set => _OnTouch = value;
		}

				[SerializeField] private Action<bool> _OnHit;
		public Action<bool> OnHit
		{
		    get => _OnHit;
		    set => _OnHit = value;
		}

		private GameObject _hitTarget;

		private bool _isCheckChild;

		private void Update()
		{
			CheckTouch();
		}

		private void CheckTouch()
		{
			if (NativeInput.TouchCount >= 1)
			{
				Touch touch = NativeInput.GetTouch(0);
				if (touch.phase == TouchPhase.Began)
				{
					OnTouch?.Invoke(touch.position);
					CheckHit(touch.position);
				}

				return;
			}

			if (NativeInput.PointerPressedThisFrame)
			{
				Vector2 position = NativeInput.PointerPosition;
				OnTouch?.Invoke(position);
				CheckHit(position);
			}
		}

		public void SetHitTarget(GameObject target, bool isCheckChild = false)
		{
			_hitTarget = target;
			_isCheckChild = isCheckChild;
		}

		private void CheckHit(Vector2 touchPoint)
		{
			if (EventSystem.current == null)
			{
				OnHit?.Invoke(false);
				return;
			}

			List<RaycastResult> raycastResults = new List<RaycastResult>();
			PointerEventData eventData = new PointerEventData(EventSystem.current)
			{
				position = touchPoint
			};
			EventSystem.current.RaycastAll(eventData, raycastResults);
			OnHit?.Invoke(IsHit(raycastResults));
		}

		private bool IsHit(List<RaycastResult> raycastResults)
		{
			if (_hitTarget == null || raycastResults == null)
			{
				return false;
			}

			foreach (RaycastResult result in raycastResults)
			{
				GameObject target = result.gameObject;
				if (target == null)
				{
					continue;
				}

				if (_isCheckChild)
				{
					if (target.transform.IsChildOf(_hitTarget.transform))
					{
						return true;
					}
				}
				else if (target == _hitTarget)
				{
					return true;
				}
			}

			return false;
		}

		public TouchController()
		{
		}
	}
}
