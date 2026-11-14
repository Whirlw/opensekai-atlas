using System.Collections.Generic;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Remembers the last sample seen per finger and decides what a new sample means.
	///
	/// Fingers report several samples per frame and the same timestamp more than once, so the raw
	/// stream cannot be judged directly: a repeat of the same timestamp is either noise to drop or a
	/// press that has not moved yet. Both rules live here, because both are about the sample stream
	/// rather than about judging.
	/// </summary>
	internal sealed class TouchDeduplication
	{
		private readonly Dictionary<int, (double time, InputTouchPhase phase)> _lastTouches;

		public TouchDeduplication(int capacity)
		{
			_lastTouches = new Dictionary<int, (double time, InputTouchPhase phase)>(capacity);
		}

		public void Clear()
		{
			_lastTouches.Clear();
		}

		/// <summary>True when this sample is new enough to act on.</summary>
		public bool ShouldProcess(in LiveTouchSample touch)
		{
			if (!_lastTouches.TryGetValue(touch.touchId, out var lastTouch))
			{
				return true;
			}

			if (touch.time < lastTouch.time)
			{
				return false;
			}

			return lastTouch.phase != InputTouchPhase.Ended;
		}

		public void Commit(in LiveTouchSample touch)
		{
			_lastTouches[touch.touchId] = (touch.time, touch.phase);
		}

		public void Commit(int touchId, double time, InputTouchPhase phase)
		{
			_lastTouches[touchId] = (time, phase);
		}

		public void Forget(int touchId)
		{
			_lastTouches.Remove(touchId);
		}

		/// <summary>
		/// A repeat of a timestamp cannot be a second press, so a still finger is promoted to Moved
		/// and an end is dropped by the caller.
		/// </summary>
		public bool IsRepeat(int touchId, double time)
		{
			return _lastTouches.TryGetValue(touchId, out var lastTouch) && time.Equals(lastTouch.time);
		}
	}
}
