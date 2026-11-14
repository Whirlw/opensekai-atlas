using UnityEngine;

namespace Sekai.Live.Tails
{
	/// <summary>
	/// One frame of a bar being held: everything a tail needs in order to decide whether this frame
	/// closed the bar. The bar fills it in, the tail reads it, and neither has to know the other's
	/// flags - the old shape had the caller branch on three booleans before it could even ask.
	/// </summary>
	public readonly struct TailFrame
	{
		public TailFrame(float now, float deltaTime, Vector2 swipeDelta, bool releasedInside, bool covered,
			int? swipeTouchId = null, bool swipeFromTailLane = false)
		{
			Now = now;
			DeltaTime = deltaTime;
			SwipeDelta = swipeDelta;
			ReleasedInside = releasedInside;
			Covered = covered;
			SwipeTouchId = swipeTouchId;
			SwipeFromTailLane = swipeFromTailLane;
		}

		/// <summary>Music time of this frame.</summary>
		public float Now { get; }

		/// <summary>Frame duration, used for the per-frame flick speed.</summary>
		public float DeltaTime { get; }

		/// <summary>Net finger travel seen this frame inside the bar.</summary>
		public Vector2 SwipeDelta { get; }
		public int? SwipeTouchId { get; }

		/// <summary>
		/// The swipe started outside the bar but inside its tail's lane, before the tail time. It is not
		/// deferred like a swipe inside the bar: it is graded at once, and only when that grade is
		/// Perfect (<see cref="LongNote.AcceptsSwipeFromTailLane"/>).
		/// </summary>
		public bool SwipeFromTailLane { get; }

		/// <summary>A finger was lifted inside the bar (holder, or a finger inside its judge lane).</summary>
		public bool ReleasedInside { get; }

		/// <summary>The swipe's finger is still on the bar this frame.</summary>
		public bool Covered { get; }
	}
}
