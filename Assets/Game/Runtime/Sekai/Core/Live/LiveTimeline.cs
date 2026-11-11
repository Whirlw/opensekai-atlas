using System;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	/// <summary>
	/// Owns "where the song is right now" for one live session: the frame snapshot every other
	/// subsystem reads, the wall clock it was taken against, and the two cursors that walk forward
	/// with it.
	///
	/// The point of the class is that only one place may ask the score for its current frame. Notes
	/// and views used to reach for a static frame on their own, which made "which clock is this"
	/// unanswerable; they now receive the frame from whoever advanced it.
	/// </summary>
	internal sealed class LiveTimeline
	{
		private double _gameTime;
		private int _eventIndex;

		/// <summary>The frame this session is in. Never null once <see cref="Update"/> has run.</summary>
		public MusicScoreInfo Frame { get; private set; }

		/// <summary>Wall clock reading the frame was taken against, used to place touches on the timeline.</summary>
		public double GameTime => _gameTime;

		public int EventIndex => _eventIndex;

		public void Reset()
		{
			_eventIndex = 0;
			_gameTime = 0d;
		}

		/// <summary>
		/// Advances the score to the given music time and takes a fresh frame. Passing a null score
		/// keeps the previous frame, which is what the editor-only preview paths expect.
		/// </summary>
		public void Update(MusicScore score, float musicScoreTime, double gameTime)
		{
			score?.Update(musicScoreTime);
			Frame = MusicScore.CurrentFrameInfo;
			_gameTime = gameTime;
		}

		/// <summary>
		/// Runs every event that is due at or before the current frame. Events are ordered, so this
		/// is a cursor walk rather than a scan; an event that is not finished stays at the cursor and
		/// is retried next frame.
		/// </summary>
		public void AdvanceEvents(EventBase[] events, Action<EventBase> onEventDone)
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

				if (eventBase.MusicScoreInfo.time > Frame.time)
				{
					return;
				}

				eventBase.Update(Frame, onEventDone);
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
		public void AdvanceAutoJudgment(LiveNoteSession session)
		{
			if (session == null)
			{
				return;
			}

			// The note session advances past completed notes. Autoplay revisits a long note
			// through its interior ticks and tail while judging later due notes on the same frame.
			AdvanceList(session.Notes, session.NoteIndex);
			AdvanceList(session.HighSpeedNotes, session.HighSpeedIndex);
		}

		private void AdvanceList(NoteBase[] notes, int index)
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

				if (note.MusicScoreInfo.time > Frame.time)
				{
					return;
				}

				note.AutoJudgment(Frame);
				index++;
			}
		}
	}
}
