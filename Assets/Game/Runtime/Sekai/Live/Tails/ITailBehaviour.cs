namespace Sekai.Live.Tails
{
	/// <summary>
	/// How a bar is supposed to end. Every kind of end note answers the same questions, and the caller
	/// no longer has to ask "which note type ends this bar" before it can act: the answer lives behind
	/// <see cref="TryFinish"/>, once.
	/// </summary>
	public interface ITailBehaviour
	{
		/// <summary>
		/// One frame of the bar being held. Returns true when this frame settled the end. Whatever the
		/// behaviour does not settle is left to the bar's own timeout (<c>ExpireTail</c>), so an
		/// implementation may safely do nothing here.
		/// </summary>
		bool TryFinish(LongNote bar, in TailFrame frame);

		/// <summary>A finger leaving the screen inside the tail window scores this end.</summary>
		bool AcceptsRelease { get; }

		/// <summary>A swipe inside the bar scores this end (a flick at the tail).</summary>
		bool AcceptsSwipe { get; }

		/// <summary>The end note scores itself; the bar only detaches from it.</summary>
		bool JudgesItself { get; }
	}
}
