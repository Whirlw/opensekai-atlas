using System;
using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;
using Sekai;
namespace OpenSekai.Live.Conversion
{
    internal static class RuntimeNoteGeometry
    {
		public static (float, float) CalcSkipNoteLane(NoteBase noteBase)
		{
			if (noteBase?.ParentNote is not NoteBase parentNote || parentNote.NoteList == null)
			{
				throw new ArgumentNullException(nameof(noteBase));
			}
			List<NoteBase> noteList = parentNote.NoteList;
			int index = noteList.FindIndex(note => ReferenceEquals(note, noteBase));
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(noteBase));
			}
			NoteBase prevNote = null;
			for (int i = index - 1; i >= 0; i--)
			{
				NoteBase candidate = noteList[i];
				if (candidate != null && !candidate.IsSkip && candidate.Category != NoteCategory.Combo)
				{
					prevNote = candidate;
					break;
				}
			}
			NoteBase nextNote = null;
			for (int i = index + 1; i < noteList.Count; i++)
			{
				NoteBase candidate = noteList[i];
				if (candidate != null && !candidate.IsSkip && candidate.Category != NoteCategory.Combo)
				{
					nextNote = candidate;
					break;
				}
			}
			if (prevNote == null || nextNote == null)
			{
				throw new ArgumentOutOfRangeException(nameof(noteBase));
			}
			float prevPosition = prevNote.MusicScoreInfo.bar + prevNote.MusicScoreInfo.barProgress;
			float nextPosition = nextNote.MusicScoreInfo.bar + nextNote.MusicScoreInfo.barProgress;
			float targetPosition = noteBase.MusicScoreInfo.bar + noteBase.MusicScoreInfo.barProgress;
			float rate = Mathf.Approximately(nextPosition, prevPosition) ? 0f : (targetPosition - prevPosition) / (nextPosition - prevPosition);
			rate = ApplyLineEase(rate, prevNote.LineType);
			return (
				prevNote.LaneStart + rate * (nextNote.LaneStart - prevNote.LaneStart),
				prevNote.LaneEnd + rate * (nextNote.LaneEnd - prevNote.LaneEnd));
		}

		private static float ApplyLineEase(float rate, NoteLineType lineType)
		{
			rate = Mathf.Clamp01(rate);
			return lineType switch
			{
				NoteLineType.Linear => rate,
				NoteLineType.EaseOut => 1f - (1f - rate) * (1f - rate),
				NoteLineType.EaseIn => rate * rate,
				_ => throw new ArgumentOutOfRangeException(nameof(lineType), lineType, null)
			};
		}
    }
}
