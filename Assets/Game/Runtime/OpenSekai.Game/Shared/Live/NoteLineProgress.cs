using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
	/// <summary>
	/// Where a bar's body and its head key sit relative to the judgement line. The two are not the
	/// same rule: the body always ends on the line - that is what the line means - while the key is
	/// allowed to fall past it when nobody took the bar, so a miss reads as a miss.
	/// </summary>
	public static class NoteLineProgress
	{
		/// <summary>
		/// A sample of the body. Always clamped to the line: a bar's body is drawn from the judgement
		/// line to whatever part of it has already gone by, whatever kind of bar it is. Letting a sample
		/// past 1 here also inflates the split count until the tail end of the mesh no longer fits the
		/// buffer, which shows up as a missing piece just before the tail key.
		/// </summary>
		public static float Body(float offset, float now, float noteTime)
		{
			if (offset.Equals(0f))
			{
				return 1f;
			}

			return Mathf.Clamp01((offset + now - noteTime) / offset);
		}

		/// <summary>
		/// The bar's own key. Once the head has passed, a pinned bar holds the key on the line; an
		/// unpinned one keeps its value above 1 and travels below the line with the clock.
		/// </summary>
		public static float BarKey(float progress, bool pinsAtLine)
		{
			return pinsAtLine && progress >= 1f ? 1f : progress;
		}

		/// <summary>The curve the views use. It maps >1 to >1, so a travelling key really does move.</summary>
		public static float View(float progress)
		{
			return LiveConfig.GetNoteViewProgress(progress);
		}

		/// <summary>
		/// The offset one end of a body is drawn with. A note that has been executed carries the offset it
		/// was drawn with, and that is what the mesh uses - except when it is zero, which is not a speed but
		/// "never executed". A friction end or a hidden end reaches <see cref="NoteState.Input"/> when the
		/// player presses it, before any frame has executed it, and taking the zero made the whole bar draw
		/// no mesh at all until the next frame that executed it: measured on the device, a friction-hide bar
		/// was invisible for 18 frames (0.2 s) right after the press, which is the "整个消失几帧再出现"
		/// report. Such an end is measured instead.
		/// </summary>
		public static float SampleOffset(NoteBase note)
		{
			if (note == null)
			{
				return 0f;
			}

			if (note.State != NoteState.Wait && !Mathf.Approximately(note.offsetTime, 0f))
			{
				return note.offsetTime;
			}

			return note.CalcTimeOffset != null ? note.CalcTimeOffset(note) : note.offsetTime;
		}
	}
}
