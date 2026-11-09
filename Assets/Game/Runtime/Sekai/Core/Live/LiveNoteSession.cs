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

		public NoteBase[] Notes { get; private set; } = Empty;

		public NoteBase[] HighSpeedNotes { get; private set; } = Empty;

		/// <summary>Cursor into <see cref="Notes"/>; everything before it is finished or skipped.</summary>
		public int NoteIndex { get; set; }

		/// <summary>Cursor into <see cref="HighSpeedNotes"/>.</summary>
		public int HighSpeedIndex { get; set; }

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
					_highSpeedScratch.Add(note);
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

		/// <summary>True when the note at the given index is the one the cursor is standing on.</summary>
		public bool IsCurrent(int noteIndex, int index) => noteIndex == index;
	}
}
