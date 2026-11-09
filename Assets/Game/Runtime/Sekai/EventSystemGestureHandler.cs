using System;
using Beebyte.Obfuscator;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Sekai
{
	public class EventSystemGestureHandler : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
	{
		[Serializable]
		public class GestureEvent : UnityEvent<PointerEventData>
		{
			public GestureEvent()
			{
			}
		}

		[SerializeField]
		private UnityEvent _onClick;

		[SerializeField]
		private GestureEvent _onClickDetail;

		[SerializeField]
		private GestureEvent _onPointerDown;

		[SerializeField]
		private GestureEvent _onPointerUp;

		[SerializeField]
		private GestureEvent _onBeginDrag;

		[SerializeField]
		private GestureEvent _onDrag;

		[SerializeField]
		private GestureEvent _onEndDrag;

		[SerializeField]
		private bool _enableDoubleClick;

		private float _pressTime;

		private bool _isDragging;

		public float PressTime
		{
			get
			{
				return _pressTime;
			}
		}

		public UnityEvent OnClickEvent
		{
			get
			{
				return _onClick;
			}
		}

		public GestureEvent OnClickDetailEvent
		{
			get
			{
				return _onClickDetail;
			}
		}

		public GestureEvent OnPointerDownEvent
		{
			get
			{
				return _onPointerDown;
			}
		}

		public GestureEvent OnPointerUpEvent
		{
			get
			{
				return _onPointerUp;
			}
		}

		public GestureEvent OnBeginDragEvent
		{
			get
			{
				return _onBeginDrag;
			}
		}

		public GestureEvent OnDragEvent
		{
			get
			{
				return _onDrag;
			}
		}

		public GestureEvent OnEndDragEvent
		{
			get
			{
				return _onEndDrag;
			}
		}

		private void Awake()
		{
		}

		[Skip]
		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData == null)
			{
				return;
			}

			if (eventData.dragging || _isDragging || Time.realtimeSinceStartup - _pressTime > 0.45f)
			{
				return;
			}

			if (!_enableDoubleClick && eventData.clickCount > 1)
			{
				return;
			}

			_onClick?.Invoke();
			_onClickDetail?.Invoke(eventData);
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			_pressTime = Time.realtimeSinceStartup;
			_isDragging = false;
			_onPointerDown?.Invoke(eventData);
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			_onPointerUp?.Invoke(eventData);
		}

		[Skip]
		public void OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = true;
			_onBeginDrag?.Invoke(eventData);
		}

		[Skip]
		public void OnDrag(PointerEventData eventData)
		{
			_onDrag?.Invoke(eventData);
		}

		[Skip]
		public void OnEndDrag(PointerEventData eventData)
		{
			_isDragging = false;
			_onEndDrag?.Invoke(eventData);
		}

		private void OnDestroy()
		{
			_onClick?.RemoveAllListeners();
			_onClickDetail?.RemoveAllListeners();
			_onPointerDown?.RemoveAllListeners();
			_onPointerUp?.RemoveAllListeners();
			_onBeginDrag?.RemoveAllListeners();
			_onDrag?.RemoveAllListeners();
			_onEndDrag?.RemoveAllListeners();
		}

		public EventSystemGestureHandler()
		{
			_onClick = new UnityEvent();
			_onClickDetail = new GestureEvent();
			_onPointerDown = new GestureEvent();
			_onPointerUp = new GestureEvent();
			_onBeginDrag = new GestureEvent();
			_onDrag = new GestureEvent();
			_onEndDrag = new GestureEvent();
		}
	}
}
