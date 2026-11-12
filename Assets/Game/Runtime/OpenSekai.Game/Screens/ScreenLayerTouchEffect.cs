using System.Collections.Generic;
using Sekai.Extensions;
using Sekai.UI;
using UnityEngine;
using Sekai.Live;

namespace Sekai
{
	public class ScreenLayerTouchEffect : ScreenLayer
	{
		private static readonly string DefaultLayerName = "UI";
		private static readonly string EffectOverlayLayerName = "EffectOverlay";

		[SerializeField]
		private TouchEffect[] _touchEffects;

		private Dictionary<int, TouchEffect> _touchEffectMap;
		private int _playTapEffectIndex;
		private Camera _uiCamera;
		private bool _mousePressed;
		private Vector3 _lastMousePosition;

		protected override void OnBoot(BootArgBase bootArg)
		{
			ScreenBootDone();
		}

		protected override void OnInitComponent()
		{
			_uiCamera = ScreenManager.Instance != null ? ScreenManager.Instance.GetUICamera() : Camera.main;
			_touchEffectMap = new Dictionary<int, TouchEffect>();
			if (_touchEffects == null || _touchEffects.Length == 0)
			{
				_touchEffects = GetComponentsInChildren<TouchEffect>(true);
			}
		}

		public override void OnWillExit()
		{
			ScreenWillExitDone();
		}

		private void Update()
		{
			if (_uiCamera == null)
			{
				_uiCamera = ScreenManager.Instance != null ? ScreenManager.Instance.GetUICamera() : Camera.main;
			}

			if (_uiCamera == null || _touchEffects == null || _touchEffects.Length == 0)
			{
				return;
			}

			var touchCount = NativeInput.TouchCount;
			if (touchCount > 0)
			{
				UpdateTouches(touchCount);
				return;
			}

			UpdateMouseFallback();
		}

		public void EndAllEffect()
		{
			EndAllTapEffect();
			EndAllFlollowEffect();
			EndAllLongTapEffect();
			_touchEffectMap?.Clear();
			_mousePressed = false;
		}

		public void SetEffectLayer()
		{
			var layer = LayerMask.NameToLayer(EffectOverlayLayerName);
			if (layer >= 0)
			{
				gameObject.SetLayer(layer);
			}
		}

		public void SetDefaultLayer()
		{
			var layer = LayerMask.NameToLayer(DefaultLayerName);
			if (layer >= 0)
			{
				gameObject.SetLayer(layer);
			}
		}

		private void UpdateTouches(int touchCount)
		{
			var count = Mathf.Min(touchCount, _touchEffects.Length);
			for (var i = 0; i < count; i++)
			{
				var touch = NativeInput.GetTouch(i);
				var fingerId = touch.fingerId;
				var screenPos = new Vector3(touch.position.x, touch.position.y, 0f);
				if (touch.phase == TouchPhase.Began)
				{
					PlayTapEffect(fingerId, screenPos);
				}
				else if (touch.phase == TouchPhase.Moved)
				{
					StartFollowEffect(fingerId, screenPos);
				}
				else
				{
					EndFollowEffect(fingerId);
				}

				if (touch.phase == TouchPhase.Stationary && CountLongTap(fingerId))
				{
					StartLongTapEffect(fingerId, screenPos);
				}
				else
				{
					EndLongTapEffect(fingerId);
				}

				if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
				{
					// Keep the tap particle alive after release so the click animation can finish.
					// Tap effects are stopped only when the layer is cleared or the effect slot is reused.
				}
			}
		}

		private void UpdateMouseFallback()
		{
			const int mouseFingerId = 0;
			var screenPos = NativeInput.PointerPosition;
			screenPos.z = 0f;

			if (NativeInput.PointerPressedThisFrame)
			{
				_mousePressed = true;
				_lastMousePosition = screenPos;
				PlayTapEffect(mouseFingerId, screenPos);
				return;
			}

			if (!_mousePressed)
			{
				return;
			}

			if (NativeInput.PointerPressed)
			{
				if (_touchEffectMap == null || !_touchEffectMap.ContainsKey(mouseFingerId))
				{
					PlayTapEffect(mouseFingerId, screenPos);
				}

				var isMoved = screenPos != _lastMousePosition;
				if (isMoved)
				{
					StartFollowEffect(mouseFingerId, screenPos);
				}

				if (!isMoved && CountLongTap(mouseFingerId))
				{
					StartLongTapEffect(mouseFingerId, screenPos);
				}
				else
				{
					EndLongTapEffect(mouseFingerId);
				}

				_lastMousePosition = screenPos;
				return;
			}

			EndFollowEffect(mouseFingerId);
			EndLongTapEffect(mouseFingerId);
			_mousePressed = false;
		}

		private void PlayTapEffect(int fingerId, Vector3 screenPos)
		{
			if (_touchEffects == null || _touchEffects.Length == 0 || _uiCamera == null)
			{
				return;
			}

			var touchEffect = _touchEffects[_playTapEffectIndex];
			if (touchEffect == null)
			{
				return;
			}

			touchEffect.PlayTapEffect(_uiCamera.ScreenToWorldPoint(screenPos));
			// The map is created in setup, but the editor mouse fallback can reach this before setup runs;
			// the other readers in this file already tolerate a missing map.
			_touchEffectMap ??= new Dictionary<int, TouchEffect>();
			if (_touchEffectMap.ContainsKey(fingerId))
			{
				_touchEffectMap[fingerId] = touchEffect;
			}
			else
			{
				_touchEffectMap.Add(fingerId, touchEffect);
			}

			_playTapEffectIndex = Wrap(_playTapEffectIndex + 1, 0, _touchEffects.Length);
		}

		private void EndTapEffect(int fingerId)
		{
			if (_touchEffectMap != null && _touchEffectMap.TryGetValue(fingerId, out var touchEffect))
			{
				touchEffect.StopTapEffect();
			}
		}

		private void EndAllTapEffect()
		{
			if (_touchEffects == null)
			{
				return;
			}

			foreach (var touchEffect in _touchEffects)
			{
				touchEffect?.StopTapEffect();
			}
		}

		private void StartFollowEffect(int fingerId, Vector3 screenPos)
		{
			if (_uiCamera != null && _touchEffectMap != null && _touchEffectMap.TryGetValue(fingerId, out var touchEffect))
			{
				touchEffect.StartFollowEffect(_uiCamera.ScreenToWorldPoint(screenPos));
			}
		}

		private void EndFollowEffect(int fingerId)
		{
			if (_touchEffectMap != null && _touchEffectMap.TryGetValue(fingerId, out var touchEffect))
			{
				touchEffect.EndFollowEffect();
			}
		}

		private void EndAllFlollowEffect()
		{
			if (_touchEffects == null)
			{
				return;
			}

			foreach (var touchEffect in _touchEffects)
			{
				touchEffect?.EndFollowEffect();
			}
		}

		private bool CountLongTap(int fingerId)
		{
			return _touchEffectMap != null &&
			       _touchEffectMap.TryGetValue(fingerId, out var touchEffect) &&
			       touchEffect.CountLongTap();
		}

		private void StartLongTapEffect(int fingerId, Vector3 screenPos)
		{
			if (_uiCamera != null && _touchEffectMap != null && _touchEffectMap.TryGetValue(fingerId, out var touchEffect))
			{
				touchEffect.StartLongTapEffect(_uiCamera.ScreenToWorldPoint(screenPos));
			}
		}

		private void EndLongTapEffect(int fingerId)
		{
			if (_touchEffectMap != null && _touchEffectMap.TryGetValue(fingerId, out var touchEffect))
			{
				touchEffect.EndLongTapEffect();
			}
		}

		private void EndAllLongTapEffect()
		{
			if (_touchEffects == null)
			{
				return;
			}

			foreach (var touchEffect in _touchEffects)
			{
				touchEffect?.EndLongTapEffect();
			}
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			EndAllEffect();
		}

		private static int Wrap(int value, int min, int max)
		{
			if (max <= min)
			{
				return min;
			}

			var range = max - min;
			return ((value - min) % range + range) % range + min;
		}
	}
}
