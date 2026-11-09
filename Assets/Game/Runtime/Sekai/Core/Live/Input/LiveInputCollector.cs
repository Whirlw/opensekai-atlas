using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Core.Live
{
	/// <summary>
	/// The per-finger bookkeeping the live loop needs before it can judge anything: what sample was
	/// seen last, where the finger was, and how far it moved since.
	///
	/// It is deliberately separate from <see cref="LiveLogic"/>: everything here answers "what did the
	/// finger do", nothing here answers "what does that mean for a note".
	/// </summary>
	internal sealed class LiveInputCollector
	{
		private readonly TouchDeduplication _deduplication;
		private readonly Dictionary<int, (double time, Vector2 position)> _positions;

		public LiveInputCollector(int fingerCapacity)
		{
			_deduplication = new TouchDeduplication(fingerCapacity);
			_positions = new Dictionary<int, (double time, Vector2 position)>(fingerCapacity);
		}

		/// <summary>Forgets every finger, for a fresh take of the same song.</summary>
		public void Reset()
		{
			_deduplication.Clear();
			_positions.Clear();
		}

		/// <summary>Seeds the seen-samples table from the fingers already down when the take starts.</summary>
		public void Seed(ILiveTouchSource source)
		{
			for (int i = 0; i < source.Count; i++)
			{
				LiveTouchSample touch = source[i];
				_deduplication.Commit(touch.touchId, touch.time, touch.phase);
			}
		}

		public bool ShouldProcess(in LiveTouchSample touch)
		{
			return _deduplication.ShouldProcess(in touch);
		}

		public void Commit(in LiveTouchSample touch)
		{
			_deduplication.Commit(in touch);
		}

		public void Commit(int touchId, double time, InputTouchPhase phase)
		{
			_deduplication.Commit(touchId, time, phase);
		}

		public bool IsRepeat(int touchId, double time)
		{
			return _deduplication.IsRepeat(touchId, time);
		}

		/// <summary>
		/// How far the finger travelled since the sample this frame is compared against. A press has no
		/// history, and a finger first seen mid-drag is measured against the screen origin, exactly as
		/// the original did.
		/// </summary>
		public Vector2 TakeDelta(in LiveTouchSample touch)
		{
			Vector2 screenPosition = touch.screenPosition;

			if (touch.phase == InputTouchPhase.Began)
			{
				_positions[touch.touchId] = (touch.time, screenPosition);
			}

			if (_positions.TryGetValue(touch.touchId, out var cachedPosition))
			{
				if (touch.time >= cachedPosition.time)
				{
					return screenPosition - cachedPosition.position;
				}

				return LiveUtility.Vector2Zero;
			}

			return screenPosition;
		}

		public bool TryGetPosition(int touchId, out (double time, Vector2 position) cached)
		{
			return _positions.TryGetValue(touchId, out cached);
		}

		public void Remember(int touchId, double time, Vector2 screenPosition)
		{
			_positions[touchId] = (time, screenPosition);
		}

		public void Forget(int touchId)
		{
			_positions.Remove(touchId);
			_deduplication.Forget(touchId);
		}

		/// <summary>Keeps the newest sample of a finger the judging pass has not consumed yet.</summary>
		public void CachePosition(in LiveTouchSample touch)
		{
			int touchId = touch.touchId;
			if (_positions.ContainsKey(touchId))
			{
				if (IsEndedOrCanceled(touch.phase))
				{
					_positions.Remove(touchId);
					return;
				}

				if (touch.time > _positions[touchId].time)
				{
					_positions[touchId] = (touch.time, touch.screenPosition);
				}

				return;
			}

			_positions.Add(touchId, (touch.time, touch.screenPosition));
		}

		private static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}
	}
}
