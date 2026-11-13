using System.Collections.Generic;
using OpenSekai.Shared.Live;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>
	/// The views the live loop keeps an eye on after they are spawned: the key a bar ends with, and
	/// the keys that have been judged or that nobody judged and now slide away.
	///
	/// It owns only view bookkeeping. Whether a touch judged a note, and what the result was, is
	/// decided elsewhere; this class reads the verdict and decides when the key leaves the screen.
	/// </summary>
	internal sealed class LivePresentationBridge
	{
		private readonly HashSet<NoteBase> _tailKeys = new HashSet<NoteBase>();

		private readonly List<NoteBase> _retiring = new List<NoteBase>();

		/// <summary>Forgets every tracked key; the views themselves are reset by a new score.</summary>
		public void Reset()
		{
			_tailKeys.Clear();
			_retiring.Clear();
		}

		public int TailKeyCount => _tailKeys.Count;

		public int RetiringCount => _retiring.Count;

		/// <summary>
		/// Spawns the view for a note that just came due. A bar's ending key is additionally tracked
		/// so it can be asked to leave once it is judged or has slid past the line.
		/// </summary>
		public void Spawn(LiveViewBase[] views, NoteBase note, float now)
		{
			if (note == null)
			{
				return;
			}

			if (note.ParentRole == NoteParentRole.Tail)
			{
				LongNote bar = note.ParentNote as LongNote;
				// Every key a bar ends with is tracked, whatever phase it was in when its view was built.
				// Tracking only the ones that happened to be "Following" at spawn left the others with no
				// owner: a settled note never removes its own view (a tail's Done skips RemoveView, because
				// the bar used to own that key), so the sprite stayed on the judgement line for good - the
				// "friction tail sticks to the line after it is caught" report.
				if (bar != null && bar.ViewNoteList.Contains(note))
				{
					_tailKeys.Add(note);
				}
			}

			LiveViewExt.SpawnNote(views, note);
		}

		public void Unspawn(LiveViewBase[] views, NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			LiveViewExt.UnspawnNote(views, note);
		}

		/// <summary>Hands the views the pool sizes the score needs before the first note spawns.</summary>
		public void CreatePool(LiveViewBase[] views, Dictionary<(NoteCategory, NoteType), int> notePoolCount)
		{
			LiveViewExt.CreateNotePool(views, notePoolCount);
		}

		/// <summary>The bar of the score bar and rank plate start empty.</summary>
		public void SetupScore(LiveViewBase[] views, LiveScore score)
		{
			LiveViewExt.SetupScore(views, score);
		}

		/// <summary>A note was graded; its view plays the judgement effect.</summary>
		public void Judged(LiveViewBase[] views, NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			LiveViewExt.JudgmentNote(views, note);
		}

		/// <summary>A silent marker was graded; only the judgement text updates.</summary>
		public void JudgmentText(LiveViewBase[] views, NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			LiveViewExt.JudgmentText(views, note);
		}

		/// <summary>A finger touched the lane without picking any note up.</summary>
		public void Unpicked(LiveViewBase[] views, int lane, ref LiveTouch touch)
		{
			LiveViewExt.Unpicked(views, lane, ref touch);
		}

		public void UpdateCombo(LiveViewBase[] views, LiveScore score)
		{
			LiveViewExt.UpdateCombo(views, score);
		}

		/// <summary>The score went up by <paramref name="addScore"/>; the counter animates the delta.</summary>
		public void UpdateScore(LiveViewBase[] views, LiveScore score, int addScore)
		{
			LiveViewExt.UpdateScore(views, ref score, addScore);
		}

		/// <summary>Life moved; the gauge animates to the new value.</summary>
		public void UpdateLife(LiveViewBase[] views, LiveScore score)
		{
			LiveViewExt.UpdateLife(views, score);
		}

		/// <summary>
		/// A bar's ending key leaves when it has its own verdict, or when its own time arrives - whichever
		/// comes first. Letting a graded key keep riding down reads as a second, phantom key hanging under
		/// the line; and a key whose time has come has nothing left to show, so it is taken off rather than
		/// slid out (a friction, friction-flick or ordinary end has no verdict of its own when its bar ends,
		/// which is how such a key used to sit on the line for a few frames after the bar was gone).
		/// </summary>
		public void TickTailKeys(float now)
		{
			List<NoteBase> leaving = null;
			foreach (NoteBase tail in _tailKeys)
			{
				LongNote bar = tail.ParentNote as LongNote;
				if (bar == null || tail.Result != NoteResult.None || now >= tail.MusicScoreInfo.time)
				{
					(leaving ??= new List<NoteBase>()).Add(tail);
					continue;
				}

				TailKeyPhase phase = bar.GetTailKeyPhase(now);
				if ((phase is TailKeyPhase.SlidingOut or TailKeyPhase.Retired) && !_retiring.Contains(tail))
				{
					_retiring.Add(tail);
				}
			}

			if (leaving == null)
			{
				return;
			}

			for (int index = 0; index < leaving.Count; index++)
			{
				NoteBase tail = leaving[index];
				tail.RemoveView();
				_tailKeys.Remove(tail);
				_retiring.Remove(tail);
			}
		}

		/// <summary>
		/// SlidingOut and Retired keys keep moving past the line until Progress reaches 1.35.
		/// </summary>
		public void TickRetiring(MusicScoreInfo currentFrameInfo)
		{
			for (int index = _retiring.Count - 1; index >= 0; index--)
			{
				NoteBase retiring = _retiring[index];
				if (retiring == null)
				{
					_retiring.RemoveAt(index);
					continue;
				}

				retiring.TickRetire(currentFrameInfo.time);
				if (retiring.Progress >= 1.35f)
				{
					retiring.RemoveView();
					_tailKeys.Remove(retiring);
					_retiring.RemoveAt(index);
				}
			}
		}
	}
}
