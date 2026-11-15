namespace Sekai.Live.Tails
{
	/// <summary>
	/// One frame of a bar being held: everything a tail needs in order to decide whether this frame
	/// closed the bar. The bar fills it in, the tail reads it, and neither has to know the other's
	/// flags - the old shape had the caller branch on three booleans before it could even ask.
	/// </summary>
	public readonly struct TailFrame
	{
		public TailFrame(float now, bool releasedInside)
		{
			Now = now;
			ReleasedInside = releasedInside;
		}

		/// <summary>Music time of this frame.</summary>
		public float Now { get; }

		/// <summary>A finger was lifted inside the bar (holder, or a finger inside its judge lane).</summary>
		public bool ReleasedInside { get; }
	}
}
