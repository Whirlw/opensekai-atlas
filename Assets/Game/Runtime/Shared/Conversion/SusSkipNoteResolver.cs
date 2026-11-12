using System.Collections.Generic;
using OpenSekai.Shared.Scoring;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// Places the skipped relay notes of a long or guide chain on their line.
	/// </summary>
	/// <remarks>
	/// A skip row is a point the authoring tool put on a chain to steer its shape, not a note the player
	/// answers. Its lane is interpolated between the nearest judged notes before and after it, eased by
	/// the line type of the note in front, and then widened into judge lanes. That is runtime-object
	/// work (it reads <see cref="NoteBase"/> and the live bundle config), so it stays in the game
	/// assembly while the pure stages live in Core.
	/// </remarks>
	public static class SusSkipNoteResolver
	{
		/// <summary>Rewrites the lanes of every skipped note reachable from <paramref name="roots"/>.</summary>
		public static void Resolve(IReadOnlyList<NoteBase> roots, LiveBundleBuildData config)
		{
			if (roots == null)
			{
				return;
			}
			foreach (NoteBase root in roots)
			{
				ResolveRoot(root, config);
			}
		}

		private static void ResolveRoot(NoteBase root, LiveBundleBuildData config)
		{
			if (root?.NoteList == null)
			{
				return;
			}
			foreach (NoteBase note in root.NoteList)
			{
				if (note == null || !note.IsSkip)
				{
					continue;
				}
				Interpolate(root, root.ChildNote, note, config);
			}
		}

		private static void Interpolate(INote startNote, INote endNote, NoteBase data, LiveBundleBuildData config)
		{
			if (data == null || data.ParentNote is not LongNote parent || parent.NoteList == null)
			{
				return;
			}
			int index = parent.NoteList.IndexOf(data);
			if (index < 0)
			{
				return;
			}

			NoteBase previous = null;
			for (int i = index - 1; i >= 0; i--)
			{
				NoteBase candidate = parent.NoteList[i];
				if (candidate != null && !candidate.IsSkip && candidate.Category != NoteCategory.Combo)
				{
					previous = candidate;
					break;
				}
			}
			NoteBase next = null;
			for (int i = index + 1; i < parent.NoteList.Count; i++)
			{
				NoteBase candidate = parent.NoteList[i];
				if (candidate != null && !candidate.IsSkip && candidate.Category != NoteCategory.Combo)
				{
					next = candidate;
					break;
				}
			}
			if (previous == null || next == null)
			{
				return;
			}

			float previousPosition = previous.MusicScoreInfo.bar + previous.MusicScoreInfo.barProgress;
			float nextPosition = next.MusicScoreInfo.bar + next.MusicScoreInfo.barProgress;
			float dataPosition = data.MusicScoreInfo.bar + data.MusicScoreInfo.barProgress;
			float rate = Mathf.Approximately(nextPosition, previousPosition) ? 0f : Mathf.Clamp01((dataPosition - previousPosition) / (nextPosition - previousPosition));
			rate = ApplyLineEase(rate, previous.LineType);
			float laneStart = Mathf.Lerp(previous.LaneStartF, next.LaneStartF, rate);
			float laneEnd = Mathf.Lerp(previous.LaneEndF, next.LaneEndF, rate);
			data.LaneStartF = laneStart;
			data.LaneEndF = laneEnd;
			data.LaneStart = Mathf.FloorToInt(laneStart);
			data.LaneEnd = Mathf.FloorToInt(laneEnd);
			float laneOffset = LiveUtility.GetLaneOffset(NoteCategory.Combo, config);
			JudgeTuning.WidenLaneRange(laneStart, laneEnd, laneOffset, out float judgeStart, out float judgeEnd);
			data.JudgeLaneStart = judgeStart;
			data.JudgeLaneEnd = judgeEnd;
		}

		private static float ApplyLineEase(float rate, NoteLineType lineType)
		{
			rate = Mathf.Clamp01(rate);
			return lineType switch
			{
				NoteLineType.EaseIn => rate * rate,
				NoteLineType.EaseOut => 1f - (1f - rate) * (1f - rate),
				_ => rate
			};
		}
	}
}
