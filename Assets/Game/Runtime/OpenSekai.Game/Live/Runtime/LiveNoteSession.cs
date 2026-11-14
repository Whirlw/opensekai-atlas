using System;
using System.Collections.Generic;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Owns "which notes are there and how far the walk has got" for one live session: the note
	/// arrays the frame loop scans, the split between normal and high speed notes, and the two
	/// cursors that only move forward.
	///
	/// The split is a data question, not a judging one: a note whose speed ratio is not 1 is drawn
	/// and scrolled by a different rule, so it is filed into its own array and walked by its own
	/// cursor. Keeping both arrays and both cursors here means no other code has to remember that.
	/// </summary>
	public sealed class LiveNoteSession
	{
		private static readonly NoteBase[] Empty = Array.Empty<NoteBase>();
		private readonly List<NoteBase> _highSpeedScratch = new List<NoteBase>();
		private int _noteIndex;
		private int _highSpeedIndex;

		public NoteBase[] Notes { get; private set; } = Empty;

		public NoteBase[] HighSpeedNotes { get; private set; } = Empty;

		/// <summary>Cursor into <see cref="Notes"/>; everything before it is finished or skipped.</summary>
		public int NoteIndex
		{
			get { return _noteIndex; }
			set { _noteIndex = value; }
		}

		/// <summary>Cursor into <see cref="HighSpeedNotes"/>.</summary>
		public int HighSpeedIndex
		{
			get { return _highSpeedIndex; }
			set { _highSpeedIndex = value; }
		}

		/// <summary>
		/// Takes the chart's note list and files the high speed notes out of it. Replaces whatever was
		/// bound before; call <see cref="Reset"/> when restarting the same chart.
		/// </summary>
		public void Bind(NoteBase[] notes)
		{
			Notes = notes ?? Empty;

			_highSpeedScratch.Clear();
			for (int i = 0; i < Notes.Length; i++)
			{
				NoteBase note = Notes[i];
				if (note != null && !UnityEngine.Mathf.Approximately(note.speedRatio, 1f))
				{
					note.IsHighSpeedNote = true;
					_highSpeedScratch.Add(note);
				}
				else if (note != null)
				{
					note.IsHighSpeedNote = false;
				}
			}

			HighSpeedNotes = _highSpeedScratch.Count == 0 ? Empty : _highSpeedScratch.ToArray();
			Reset();
		}

		public void Reset()
		{
			NoteIndex = 0;
			HighSpeedIndex = 0;
			_heads.Clear();
			// A new take starts with no open notes: carrying the previous take's lists over meant a retry
			// ticked notes it had never scanned, before their own bar was on screen at all.
			_tails.Clear();
		}

		/// <summary>
		/// Bars whose head has not been settled yet. A head answers to its own window, so it has to keep
		/// being ticked after the bar it belongs to is finished - the note walk steps over a Done note
		/// and would never give that head its last frame. This is the head's own tick list, not the bar's.
		/// </summary>
		private readonly List<LongNoteHead> _heads = new List<LongNoteHead>();

		/// <summary>
		/// Every head still waiting for its verdict, whatever its bar is doing. The frame asks this list to
		/// keep those heads registered as press candidates: the note walk only sees bars that are still being
		/// played, and a short bar is done milliseconds after its head - which used to take the head's own
		/// window away with it.
		/// </summary>
		internal List<LongNoteHead> PendingHeads => _heads;

		internal void RegisterHead(LongNoteHead head)
		{
			if (head != null && !head.IsSettled && !_heads.Contains(head))
			{
				_heads.Add(head);
			}
		}

		/// <summary>Gives every open head its own clock: a head whose window has passed settles itself.</summary>
		internal void TickHeads(float now)
		{
			for (int i = _heads.Count - 1; i >= 0; i--)
			{
				LongNoteHead head = _heads[i];
				if (head == null || head.IsSettled)
				{
					_heads.RemoveAt(i);
					continue;
				}

				head.SettleExpiredHead(now);
				if (head.IsSettled)
				{
					_heads.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// End notes that judge themselves - a rub, a rub flick, a hidden carrier. They are ticked here rather
		/// than by the bar's own note loop: the note's clock is what publishes its Miss, and a bar that ends
		/// first (it leaves on its own time, whatever its end does) used to take that clock away with it,
		/// leaving a friction-flick end that was never judged and never missed. Decoupling the tick is what
		/// lets the bar leave whenever it likes without changing the note's verdict.
		/// </summary>
		private readonly List<NoteBase> _tails = new List<NoteBase>();
		internal IReadOnlyList<NoteBase> PendingTails => _tails;

		/// <summary>Tails still waiting for their own clock, for tests and invariants.</summary>
		public int PendingTailCount => _tails.Count;

		internal void RegisterTail(NoteBase tail)
		{
			if (tail != null && tail.State != NoteState.Done && !_tails.Contains(tail))
			{
				_tails.Add(tail);
			}
		}

		/// <summary>Ticks every self-judging end note with its own display offset, and forgets the finished.</summary>
		internal void TickTails(System.Action<NoteBase> advance)
		{
			for (int i = _tails.Count - 1; i >= 0; i--)
			{
				NoteBase tail = _tails[i];
				if (tail == null || tail.State == NoteState.Done)
				{
					_tails.RemoveAt(i);
					continue;
				}

				advance(tail);
				if (tail.State == NoteState.Done)
				{
					_tails.RemoveAt(i);
				}
			}
		}

		/// <summary>What the walker should do with the note it was handed.</summary>
		public enum Scan
		{
			/// <summary>Not this note's frame yet - skip it but keep walking.</summary>
			Skip,

			/// <summary>The walker handled the note; move on.</summary>
			Handle,

			/// <summary>Everything from here on is in the future - stop walking this frame.</summary>
			Stop
		}

		public delegate Scan NoteVisitor(NoteBase note);

		/// <summary>
		/// Walks the normal notes from the cursor. A note that is gone or already finished is stepped
		/// over, and the cursor only advances while it stands on such a note: a hole further down the
		/// array must not pull the cursor forward, or entries before it would never be looked at again.
		/// </summary>
		public void WalkNotes(NoteVisitor visit)
		{
			Walk(Notes, ref _noteIndex, visit);
		}

		/// <summary>Walks the high speed notes the same way, on their own cursor.</summary>
		public void WalkHighSpeedNotes(NoteVisitor visit)
		{
			Walk(HighSpeedNotes, ref _highSpeedIndex, visit);
		}

		private static void Walk(NoteBase[] notes, ref int cursor, NoteVisitor visit)
		{
			for (int i = cursor; i < notes.Length; i++)
			{
				NoteBase note = notes[i];
				if (note == null || note.State == NoteState.Done)
				{
					if (i == cursor)
					{
						cursor++;
					}
					continue;
				}

				Scan outcome = visit(note);
				if (outcome == Scan.Stop)
				{
					return;
				}
				if (outcome == Scan.Skip)
				{
					if (i == cursor)
					{
						cursor++;
					}
					continue;
				}

				// Handled: the note may have finished during the visit, and only then does the
				// cursor move past it.
				if (note.State == NoteState.Done && i == cursor)
				{
					cursor++;
				}
			}
		}
	}
}
