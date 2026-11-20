using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class InputSystemTouchSourceTests
    {
        [TestCase(TouchPhase.Moved)]
        [TestCase(TouchPhase.Ended)]
        public void SameUpdateSnapshotUsesOriginalPressTime(TouchPhase nextPhase)
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            EnhancedTouchSupport.Enable();
            try
            {
                const int TOUCH_ID = 12345;
                double pressTime = Time.realtimeSinceStartupAsDouble + 0.001d;
                double nextTime = pressTime + 0.012d;
                var pressPosition = new Vector2(100f, 150f);
                var nextPosition = new Vector2(200f, 150f);
                InputSystem.QueueStateEvent(screen, new TouchState
                {
                    touchId = TOUCH_ID, phase = TouchPhase.Began, position = pressPosition,
                }, pressTime);
                InputSystem.QueueStateEvent(screen, new TouchState
                {
                    touchId = TOUCH_ID, phase = nextPhase, position = nextPosition,
                }, nextTime);
                InputSystem.Update();

                var source = new InputSystemTouchSource();
                int index = FindTouch(source, TOUCH_ID);
                var snapshot = Sekai.NativeInput.ActiveTouches[index];
                Assert.That(snapshot.time, Is.EqualTo(nextTime).Within(0.00001d));

                LiveTouchSample down = source[index];
                Assert.That(down.phase, Is.EqualTo(TouchPhase.Began));
                Assert.That(down.time, Is.EqualTo(pressTime).Within(0.00001d));
                Assert.That(down.screenPosition, Is.EqualTo(pressPosition));

                InputSystem.Update();
                LiveTouchSample next = source[FindTouch(source, TOUCH_ID)];
                Assert.That(next.phase, Is.EqualTo(nextPhase));
                Assert.That(next.time, Is.EqualTo(nextTime).Within(0.00001d));
                Assert.That(next.screenPosition, Is.EqualTo(nextPosition));
            }
            finally
            {
                InputSystem.RemoveDevice(screen);
                EnhancedTouchSupport.Disable();
            }
        }

        private static int FindTouch(InputSystemTouchSource source, int touchId)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].touchId == touchId)
                    return i;
            }
            Assert.Fail($"Touch {touchId} was not in the EnhancedTouch snapshot.");
            return -1;
        }
    }
}
