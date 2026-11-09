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
	}
}
