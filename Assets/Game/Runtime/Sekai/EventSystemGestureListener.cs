using System;
using System.Collections.Generic;
using Beebyte.Obfuscator;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Sekai
{
	public class EventSystemGestureListener : MonoBehaviour
	{
		[Serializable]
		public class GestureEvent : UnityEvent<PointerGestureData>
		{
			public GestureEvent()
			{
			}
		}

		public struct DragInfo
		{
			public Vector2 StartPosition;

			public float StartTime;

			public DragInfo(Vector3 pos, float time)
			{
				StartPosition = pos;
				StartTime = time;
			}
		}

		[SerializeField]
		private List<EventSystemGestureHandler> _gestureHandlers;

		[SerializeField]
		private GestureEvent _onPinchScale;

		[SerializeField]
		private GestureEvent _onDrag;

		[SerializeField]
		private GestureEvent _onDragEnd;

		[SerializeField]
		private GestureEvent _onFlick;

		[SerializeField]
		private float _singleDragThreshold;

		[SerializeField]
		private float _flickThresholdTime;

		private List<PointerEventData> _pointerList;

		private PointerGestureData _gestureData;

		private float _screenDistance;

		private bool _isDragging;

		private bool _isPrevFrameDragging;

		private Dictionary<int, DragInfo> _dragInfo;

		private Action _onPinchScaleEnd;

		public int TouchCount
		{
			get
			{
				return _pointerList?.Count ?? 0;
			}
		}

		public GestureEvent OnPinchScale
		{
			get
			{
				return _onPinchScale;
			}
		}

		public GestureEvent OnSingleDrag
		{
			get
			{
				return _onDrag;
			}
		}

		public GestureEvent OnDragEndEvent
		{
			get
			{
				return _onDragEnd;
			}
		}

		public GestureEvent OnFlick
		{
			get
			{
				return _onFlick;
			}
		}

		public float FlickThresholdTime
		{
			get
			{
				return _flickThresholdTime;
			}
			set
			{
				_flickThresholdTime = value;
			}
		}

		private void Awake()
		{
			Initialize();
		}

		public void AddGestureHandler(EventSystemGestureHandler handler)
		{
			if (handler == null)
			{
				return;
			}

			_gestureHandlers ??= new List<EventSystemGestureHandler>();
			if (_gestureHandlers.Contains(handler))
			{
				return;
			}

			_gestureHandlers.Add(handler);
			RegisterCallBack(handler);
		}

		public void RemoveGestureHandler(EventSystemGestureHandler handler)
		{
			if (handler == null || _gestureHandlers == null)
			{
				return;
			}

			RemoveCallBack(handler);
			_gestureHandlers.Remove(handler);
		}

		public void CleanUpGestureHandler()
		{
			if (_gestureHandlers == null)
			{
				return;
			}

			for (int i = _gestureHandlers.Count - 1; i >= 0; i--)
			{
				if (_gestureHandlers[i] == null)
				{
					_gestureHandlers.RemoveAt(i);
				}
			}
		}

		public void ClearGestureHandler()
		{
			if (_gestureHandlers == null)
			{
				return;
			}

			foreach (EventSystemGestureHandler handler in _gestureHandlers)
			{
				RemoveCallBack(handler);
			}
			_gestureHandlers.Clear();
		}

		public void SetOnPinchScaleEnd(Action onPinchScaleEnd)
		{
			_onPinchScaleEnd = onPinchScaleEnd;
		}

		private void RegisterCallBack(EventSystemGestureHandler handler)
		{
			if (handler == null)
			{
				return;
			}

			RemoveCallBack(handler);
			handler.OnPointerDownEvent?.AddListener(OnPointerDown);
			handler.OnPointerUpEvent?.AddListener(OnPointerUp);
			handler.OnBeginDragEvent?.AddListener(OnBeginDrag);
			handler.OnDragEvent?.AddListener(OnDrag);
			handler.OnEndDragEvent?.AddListener(OnEndDrag);
		}

		private void RemoveCallBack(EventSystemGestureHandler handler)
		{
			if (handler == null)
			{
				return;
			}

			handler.OnPointerDownEvent?.RemoveListener(OnPointerDown);
			handler.OnPointerUpEvent?.RemoveListener(OnPointerUp);
			handler.OnBeginDragEvent?.RemoveListener(OnBeginDrag);
			handler.OnDragEvent?.RemoveListener(OnDrag);
			handler.OnEndDragEvent?.RemoveListener(OnEndDrag);
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			UpdatePointerInfo(eventData);
			if (eventData != null)
			{
				_dragInfo[eventData.pointerId] = new DragInfo(eventData.position, Time.realtimeSinceStartup);
			}
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			UpdatePointerInfo(eventData);
			if (eventData != null && _dragInfo.TryGetValue(eventData.pointerId, out DragInfo info))
			{
				Vector2 flickVector = eventData.position - info.StartPosition;
				float elapsed = Time.realtimeSinceStartup - info.StartTime;
				_gestureData.FlickVector = elapsed <= _flickThresholdTime ? flickVector : Vector2.zero;
				_gestureData.PointerEventData = eventData;
				if (_gestureData.IsFlickLeft || _gestureData.IsFlickRight || _gestureData.IsFlickTop || _gestureData.IsFlickBottom)
				{
					_onFlick?.Invoke(_gestureData);
				}
				_dragInfo.Remove(eventData.pointerId);
			}

			if (_pointerList.Count < 2)
			{
				_onPinchScaleEnd?.Invoke();
			}
		}

		[Skip]
		public void OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = true;
			UpdatePointerInfo(eventData);
		}

		[Skip]
		public void OnDrag(PointerEventData eventData)
		{
			UpdatePointerInfo(eventData);
			UpdateDragGestureData();
		}

		[Skip]
		public void OnEndDrag(PointerEventData eventData)
		{
			UpdatePointerInfo(eventData);
			UpdateDragGestureData();
			_onDragEnd?.Invoke(_gestureData);
			_isDragging = false;
		}

		private void UpdatePointerInfo(PointerEventData eventData)
		{
			if (eventData == null)
			{
				return;
			}

			int index = _pointerList.FindIndex(pointer => pointer != null && pointer.pointerId == eventData.pointerId);
			if (eventData.eligibleForClick || eventData.dragging || eventData.pointerPress != null)
			{
				if (index >= 0)
				{
					_pointerList[index] = eventData;
				}
				else
				{
					_pointerList.Add(eventData);
				}
			}
			else if (index >= 0)
			{
				_pointerList.RemoveAt(index);
			}
		}

		private void UpdateDragGestureData()
		{
			_gestureData.PrevPointerCount = _gestureData.PointerCount;
			_gestureData.PointerCount = _pointerList.Count;
			_gestureData.PointerEventData = _pointerList.Count > 0 ? _pointerList[0] : null;

			for (int i = 0; i < _gestureData.ScreenPosition.Length; i++)
			{
				if (i < _pointerList.Count && _pointerList[i] != null)
				{
					_gestureData.DragDelta[i] = _pointerList[i].delta;
					_gestureData.ScreenPosition[i] = _pointerList[i].position;
				}
				else
				{
					_gestureData.DragDelta[i] = Vector2.zero;
					_gestureData.ScreenPosition[i] = Vector2.zero;
				}
			}

			if (_pointerList.Count >= 2)
			{
				_gestureData.PrevDistance = _screenDistance;
				_screenDistance = Vector2.Distance(_pointerList[0].position, _pointerList[1].position);
				_gestureData.Distance = _screenDistance;
				_gestureData.DeltaScale = _gestureData.PrevDistance > 0f ? _screenDistance / _gestureData.PrevDistance : 1f;
				_gestureData.NormalizeDeltaScale = _gestureData.DeltaScale - 1f;
				_onPinchScale?.Invoke(_gestureData);
			}
			else if (_pointerList.Count == 1 && _pointerList[0].delta.sqrMagnitude >= _singleDragThreshold * _singleDragThreshold)
			{
				_onDrag?.Invoke(_gestureData);
			}
		}

		private void Update()
		{
			_isPrevFrameDragging = _isDragging;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (pauseStatus)
			{
				Initialize();
			}
		}

		public void Initialize()
		{
			_gestureHandlers ??= new List<EventSystemGestureHandler>();
			_onPinchScale ??= new GestureEvent();
			_onDrag ??= new GestureEvent();
			_onDragEnd ??= new GestureEvent();
			_onFlick ??= new GestureEvent();
			_pointerList ??= new List<PointerEventData>(2);
			_dragInfo ??= new Dictionary<int, DragInfo>();
			_pointerList.Clear();
			_dragInfo.Clear();
			_gestureData = new PointerGestureData();
			_screenDistance = 0f;
			_isDragging = false;
			_isPrevFrameDragging = false;

			foreach (EventSystemGestureHandler handler in _gestureHandlers.ToArray())
			{
				RegisterCallBack(handler);
			}
		}

		public EventSystemGestureListener()
		{
			_singleDragThreshold = 10f;
			_flickThresholdTime = 1f;
			_pointerList = new List<PointerEventData>(2);
			_dragInfo = new Dictionary<int, DragInfo>();
			_onPinchScale = new GestureEvent();
			_onDrag = new GestureEvent();
			_onDragEnd = new GestureEvent();
			_onFlick = new GestureEvent();
		}
	}
}
