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

			// Everything that is not a flick or an ordinary end note is judged on its own, exactly like a
			// note that hangs on no bar at all: a rub, a rub flick, and the hidden carriers (which carry
			// no judgement of their own). The bar never grades them, only reads what they decided.
			if (tail is FrictionNote || tail is FrictionFlickNote || tail is HiddenConnectionNote || tail is FrictionHideNote)
			{
				return SelfJudgedTail.Instance;
			}

			return ReleaseTail.Instance;
		}
	}
}
