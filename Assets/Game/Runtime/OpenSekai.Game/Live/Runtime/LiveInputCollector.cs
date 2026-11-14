using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Sekai.Live;
using OpenSekai.Shared.Live.Simulation;
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

		/// <summary>
		/// Forgets every finger, for a fresh take of the same song.
		/// </summary>
		public void Reset()
		{
			_deduplication.Clear();
			_positions.Clear();
		}

		/// <summary>Hands one finished touch to the judging side. It owns what the touch means.</summary>
		public delegate void TouchSink(in SimTouch touch, ref int activeInputCount);

		/// <summary>
		/// Turns this frame's raw samples into touches: drops the ones that repeat or arrive out of
		/// order, keeps the newest position per finger, and forwards the rest to <paramref name="sink"/>.
		/// The editor's mouse is fed through the same path so the two cannot drift apart.
		/// </summary>
		public void Collect(
			ILiveTouchSource source,
			Camera calcCamera,
			System.Func<double, float> resolveMusicTime,
			TouchSink sink,
			ref int activeInputCount)
		{
			// Read indexed samples directly; no per-frame array or collection copy.
			int touchCount = source.Count;

			for (int i = 0; i < touchCount; i++)
			{
				LiveTouchSample touch = source[i];
				// Raw sample trace: the one place that proves whether a press reached the process at all. A press
				// that never appears here was dropped by the platform, not by the judge.
				if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge))
				{
					OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Judge,
						string.Concat("rawTouch finger=", touch.fingerId, " touch=", touch.touchId,
							" phase=", touch.phase, " x=", touch.worldPosition.x.ToString("F1"),
							" y=", touch.worldPosition.y.ToString("F1"),
							" t=", touch.time.ToString("F3"),
							" process=", ShouldProcess(in touch)));
				}

				if (ShouldProcess(in touch))
				{
					TouchExecution(in touch, resolveMusicTime, sink, ref activeInputCount);
					Commit(in touch);
				}
			}

			for (int i = 0; i < touchCount; i++)
			{
				LiveTouchSample touch = source[i];
				CachePosition(in touch);
			}

#if UNITY_EDITOR
			ProcessMouseFallback(calcCamera, resolveMusicTime, sink, ref activeInputCount);
#endif
		}

		private void TouchExecution(
			in LiveTouchSample touch,
			System.Func<double, float> resolveMusicTime,
			TouchSink sink,
			ref int activeInputCount)
		{
			int touchId = touch.touchId;
			InputTouchPhase phase = touch.phase;
			double time = touch.time;
			if (IsRepeat(touchId, time))
			{
				if (IsEndedOrCanceled(phase) || phase == InputTouchPhase.None)
				{
					return;
				}

				if (phase == InputTouchPhase.Began || phase == InputTouchPhase.Stationary)
				{
					phase = InputTouchPhase.Moved;
				}
			}

			float musicTime = resolveMusicTime(time);
			Vector2 delta = TakeDelta(in touch);
			var simTouch = new SimTouch(touch.fingerId, touchId, delta, touch.worldPosition, phase, musicTime);
			sink(in simTouch, ref activeInputCount);
		}

#if UNITY_EDITOR
		private const int MouseFallbackTouchId = -1;
		private const int MouseFallbackFingerId = 0;

		private void ProcessMouseFallback(
			Camera calcCamera,
			System.Func<double, float> resolveMusicTime,
			TouchSink sink,
			ref int activeInputCount)
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
			{
				return;
			}

			InputTouchPhase phase;
			if (mouse.leftButton.wasPressedThisFrame)
			{
				phase = InputTouchPhase.Began;
			}
			else if (mouse.leftButton.wasReleasedThisFrame)
			{
				phase = InputTouchPhase.Ended;
			}
			else if (mouse.leftButton.isPressed)
			{
				Vector2 mouseDelta = mouse.delta.ReadValue();
				phase = mouseDelta.sqrMagnitude > 0f ? InputTouchPhase.Moved : InputTouchPhase.Stationary;
			}
			else
			{
				return;
			}

			Vector2 screenPosition = mouse.position.ReadValue();
			double time = Time.realtimeSinceStartupAsDouble;
			Vector2 delta = mouse.delta.ReadValue();
			if (phase == InputTouchPhase.Began)
			{
				Remember(MouseFallbackTouchId, time, screenPosition);
				delta = LiveUtility.Vector2Zero;
			}
			else if (TryGetPosition(MouseFallbackTouchId, out var cachedPosition))
			{
				delta = screenPosition - cachedPosition.position;
			}

			Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, 0f);
			Vector3 worldPosition = calcCamera != null
				? calcCamera.ScreenToWorldPoint(screenPoint)
				: new Vector3(screenPosition.x, screenPosition.y, 0f);
			float musicTime = resolveMusicTime(time);
			var simTouch = new SimTouch(MouseFallbackFingerId, MouseFallbackTouchId, delta, worldPosition, phase, musicTime);
			sink(in simTouch, ref activeInputCount);

			if (IsEndedOrCanceled(phase))
			{
				Forget(MouseFallbackTouchId);
			}
			else
			{
				Remember(MouseFallbackTouchId, time, screenPosition);
				Commit(MouseFallbackTouchId, time, phase);
			}
		}
#endif

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
		/// the legacy loop did.
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
