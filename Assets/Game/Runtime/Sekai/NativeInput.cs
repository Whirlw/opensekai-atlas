using UnityEngine;
using UnityEngine.InputSystem;

namespace Sekai
{
	public static partial class NativeInput
	{
		private static readonly UnityEngine.Touch[] NoTouches;

		private static readonly UnityEngine.Touch[] PcTouches;
		private static UnityEngine.Touch[] _touches;
		private static bool _touchesDirty = true;

		public static bool BackPressedThisFrame => Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
		public static Vector3 PointerPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
		public static bool PointerPressedThisFrame => Mouse.current?.leftButton.wasPressedThisFrame ?? false;
		public static bool PointerReleasedThisFrame => Mouse.current?.leftButton.wasReleasedThisFrame ?? false;
		public static bool PointerPressed => Mouse.current?.leftButton.isPressed ?? false;
		public static int TouchCount => ActiveTouches.Count;

		public static UnityEngine.Touch[] Touches
		{
			get
			{
				var activeTouches = ActiveTouches;
				if (!_touchesDirty && _touches.Length == activeTouches.Count) return _touches;
				if (_touches.Length != activeTouches.Count) _touches = activeTouches.Count == 0 ? NoTouches : new UnityEngine.Touch[activeTouches.Count];
				for (int i = 0; i < activeTouches.Count; i++)
				{
					var touch = activeTouches[i];
					_touches[i] = new UnityEngine.Touch
					{
						fingerId = touch.touchId,
						position = touch.screenPosition,
						rawPosition = touch.screenPosition,
						deltaPosition = touch.delta,
						phase = touch.phase switch
						{
							UnityEngine.InputSystem.TouchPhase.Began => UnityEngine.TouchPhase.Began,
							UnityEngine.InputSystem.TouchPhase.Moved => UnityEngine.TouchPhase.Moved,
							UnityEngine.InputSystem.TouchPhase.Stationary => UnityEngine.TouchPhase.Stationary,
							UnityEngine.InputSystem.TouchPhase.Ended => UnityEngine.TouchPhase.Ended,
							_ => UnityEngine.TouchPhase.Canceled
						}
					};
				}
				_touchesDirty = false;
				return _touches;
			}
		}

		public static UnityEngine.Touch GetTouch(int index) => Touches[index];

		static NativeInput()
		{
			NoTouches = new UnityEngine.Touch[0];
			PcTouches = new UnityEngine.Touch[1];
			PcTouches[0].fingerId = 0;
			_touches = NoTouches;
			UnityEngine.InputSystem.InputSystem.onAfterUpdate += InvalidateTouches;
		}

		public static void ResetPointerButtons()
		{
			if (Mouse.current != null) UnityEngine.InputSystem.InputSystem.ResetDevice(Mouse.current, false);
			if (Keyboard.current != null) UnityEngine.InputSystem.InputSystem.ResetDevice(Keyboard.current, false);
		}

		public static void ConfigureEventSystem(UnityEngine.EventSystems.EventSystem eventSystem)
		{
			if (eventSystem == null) return;
			foreach (var legacy in eventSystem.GetComponents<UnityEngine.EventSystems.StandaloneInputModule>())
			{
				legacy.enabled = false;
				UnityEngine.Object.Destroy(legacy);
			}
			if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
			{
				var module = eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
				module.AssignDefaultActions();
			}
		}

		private static void InvalidateTouches() => _touchesDirty = true;

	}
}
