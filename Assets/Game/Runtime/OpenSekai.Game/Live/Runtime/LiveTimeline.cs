using System;
using Sekai.Live;

namespace Sekai.Core.Live
{
	/// <summary>Advances event and autoplay cursors against the session's current frame.</summary>
	internal sealed class LiveTimeline
	{
		private int _eventIndex;

		internal void Reset() => _eventIndex = 0;

		/// <summary>
		/// Runs every event that is due at or before the current frame. Events are ordered, so this
		/// is a cursor walk rather than a scan; an event that is not finished stays at the cursor and
		/// is retried next frame.
		/// </summary>
		internal void AdvanceEvents(MusicScoreInfo frame, EventBase[] events, Action<EventBase> onEventDone)
		{
			if (events == null)
			{
				return;
			}

			while (_eventIndex < events.Length)
			{
				EventBase eventBase = events[_eventIndex];
				if (eventBase == null || eventBase.State == EventState.Done)
				{
					_eventIndex++;
					continue;
				}

				if (eventBase.MusicScoreInfo.time > frame.time)
				{
					return;
				}

				eventBase.Update(frame, onEventDone);
				if (eventBase.State == EventState.Done)
				{
					_eventIndex++;
				}
			}
		}

		/// <summary>
		/// Auto-play: judges every note already at or before the current frame, on both the normal and
		/// the high speed list. The two lists are separate because a note's speed ratio decides which
		/// array it was filed into, and both cursors advance independently.
		/// </summary>
		internal void AdvanceAutoJudgment(MusicScoreInfo frame, LiveNoteSession session, bool skipDecoration = false)
		{
			if (session == null)
			{
				return;
			}

			// The note session advances past completed notes. Autoplay revisits a long note
			// through its interior ticks and tail while judging later due notes on the same frame.
			AdvanceList(frame, session.Notes, session.NoteIndex, skipDecoration);
			AdvanceList(frame, session.HighSpeedNotes, session.HighSpeedIndex, skipDecoration);
		}

		private static void AdvanceList(MusicScoreInfo frame, NoteBase[] notes, int index, bool skipDecoration)
		{
			if (notes == null)
			{
				return;
			}

			while (index < notes.Length)
			{
				NoteBase note = notes[index];
				if (note == null || note.State == NoteState.Done)
				{
					index++;
					continue;
				}

				if (note.MusicScoreInfo.time > frame.time)
				{
					return;
				}

				// A decorative note is not judged by autoplay at all: the frame walk plays its effect
				// and takes it off the board itself. Judging it here would silence that effect.
				if (skipDecoration && note.IsDecoration)
				{
					index++;
					continue;
				}

				note.AutoJudgment(frame);
				index++;
			}
		}
	}
}
