using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>
	/// One finger sample as the simulation sees it: a finger, a moment on the music clock, and
	/// where it is. The input layer produces these and nothing above it needs to know that they
	/// used to be <c>Sekai.Live.LiveTouch</c>, which carries the same six fields but lives in the
	/// gameplay namespace and drags the whole note model along with it.
	/// </summary>
	public readonly struct SimTouch
	{
		public readonly int FingerId;

		public readonly int TouchId;

		public readonly Vector2 Delta;

		public readonly Vector3 WorldPosition;

		public readonly TouchPhase Phase;

		public readonly float MusicTime;

		public SimTouch(int fingerId, int touchId, Vector2 delta, Vector3 worldPosition, TouchPhase phase, float musicTime)
		{
			FingerId = fingerId;
			TouchId = touchId;
			Delta = delta;
			WorldPosition = worldPosition;
			Phase = phase;
			MusicTime = musicTime;
		}
	}
}
