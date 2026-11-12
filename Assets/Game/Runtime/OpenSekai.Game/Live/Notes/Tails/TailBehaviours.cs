using Sekai;

namespace Sekai.Live.Tails
{
	/// <summary>
	/// Picks the behaviour once, when the bar is told which note ends it. Adding a new kind of end note
	/// means adding one class and one line here, instead of threading another boolean through the bar.
	/// </summary>
	public static class TailBehaviours
	{
		public static ITailBehaviour For(NoteBase tail)
		{
			if (tail is FlickNote)
			{
				return SwipeTail.Instance;
			}

			if (tail is FrictionFlickNote)
			{
				return SelfJudgedTail.Instance;
			}

			return ReleaseTail.Instance;
		}
	}
}
