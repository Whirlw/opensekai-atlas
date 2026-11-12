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

		public bool IsFinished => NoteIndex >= Notes.Length && HighSpeedIndex >= HighSpeedNotes.Length;

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

		private NoteBase[] _appearing = Empty;
		private float[] _appearTimes = System.Array.Empty<float>();
		private int _appearIndex;

		/// <summary>How many entries of the appearance table the cursor has walked past.</summary>
		public int AppearIndex => _appearIndex;

		public int AppearingCount => _appearing.Length;

		/// <summary>
		/// Installs the appearance table built at load: every note ordered by the moment it becomes visible,
		/// with that moment in the parallel time array.
		/// </summary>
		public void BindAppearTable(NoteBase[] order, float[] appearTimes)
		{
			_appearing = order ?? Empty;
			_appearTimes = appearTimes ?? System.Array.Empty<float>();
			_appearIndex = 0;
		}

		/// <summary>
		/// Visits every note whose appearance time has passed. The table is sorted by that time, so the walk
		/// stops at the first note that is not due yet and never has to ask how far ahead a note is - that
		/// question is what let one note with a collapsed lead park the scan and starve everything behind it.
		/// </summary>
		public void WalkAppearing(float now, NoteVisitor visit)
		{
			for (int i = _appearIndex; i < _appearing.Length; i++)
			{
				NoteBase note = _appearing[i];
				if (note == null || note.State == NoteState.Done)
				{
					if (i == _appearIndex)
					{
						_appearIndex++;
					}
					continue;
				}

				if (_appearTimes[i] > now)
				{
					return;
				}

				if (visit(note) == Scan.Stop)
				{
					return;
				}
			}
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