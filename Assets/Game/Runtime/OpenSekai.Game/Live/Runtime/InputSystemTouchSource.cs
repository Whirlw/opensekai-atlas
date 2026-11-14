using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Sekai.Core.Live
{
    internal sealed class InputSystemTouchSource : ILiveTouchSource
    {
        internal Camera Camera { get; set; }

        public int Count => NativeInput.ActiveTouches.Count;

        public LiveTouchSample this[int index]
        {
            get
            {
                EnhancedTouch touch = NativeInput.ActiveTouches[index];
                Vector2 position = touch.screenPosition;
                var screenPoint = new Vector3(position.x, position.y, 0f);
                Vector3 worldPosition = Camera != null ? Camera.ScreenToWorldPoint(screenPoint) : screenPoint;
                return new LiveTouchSample(touch.touchId, position, worldPosition,
                    touch.time, touch.phase, touch.delta, touch.finger != null ? touch.finger.index : touch.touchId);
            }
        }
    }
}

// Preserve NativeInput API while confining device-specific types to this file.
namespace Sekai
{
	public static partial class NativeInput
	{
		public static ReadOnlyArray<UnityEngine.InputSystem.EnhancedTouch.Touch> ActiveTouches
		{
			get
			{
				EnsureEnhancedTouchEnabled();
				return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
			}
		}

		public static ReadOnlyArray<Finger> Fingers
		{
			get
			{
				EnsureEnhancedTouchEnabled();
				return UnityEngine.InputSystem.EnhancedTouch.Touch.fingers;
			}
		}

		public static void Enable()
		{
			EnsureEnhancedTouchEnabled();
			foreach (InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
			{
				UnityEngine.InputSystem.InputSystem.ResetDevice(device, false);
			}
			_touchesDirty = true;
		}

		public static void Disable()
		{
			if (EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Disable();
			}
		}

		private static void EnsureEnhancedTouchEnabled()
		{
			if (!EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Enable();
			}
		}
	}
}
