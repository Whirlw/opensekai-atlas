using System;
using System.Collections.Generic;
using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public partial class LongNote
	{
		protected void SettleComboPointsAtEnd()
		{
			if (NoteList == null)
			{
				return;
			}

			for (int i = 0; i < NoteList.Count; i++)
			{
				if (NoteList[i] is LongHoldCombo point && point.ParentNote == this)
				{
					point.Terminate();
				}
			}
		}

		protected void ConnectionNoteTerminate()
		{
			if (NoteList == null)
			{
				return;
			}

			for (var i = 1; i < NoteList.Count - 1; i++)
			{
				var note = NoteList[i];
				if (note != null && note.State != NoteState.Done
					&& !((HasIndependentTail || HasManagedTail) && ReferenceEquals(note, TailNote)))
				{
					note.Terminate();
				}
			}
		}

		private void ConnectionNoteAutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			// A carrier never settles its head, so "the head was answered" cannot be the gate there:
			// the points it carries are exactly what it exists for.
			if (HasJudgment && Head.Result != NoteResult.Auto)
			{
				return;
			}

			foreach (NoteBase point in JudgedPoints)
			{
				point?.AutoJudgment(currentFrameInfo);
			}
		}

		private List<NoteBase> _judgedPoints;

		/// <summary>
		/// The interior points this bar really judges: the generated ones, the chart's preset ones, and
		/// the ones added without changing the shape. A hidden point only changes the shape, and two
		/// points on one position are one judgement for this bar (two bars at the same position still
		/// judge twice, each for itself).
		/// </summary>
		internal List<NoteBase> JudgedPoints => _judgedPoints ??= BuildJudgedPoints();

		private List<NoteBase> BuildJudgedPoints()
		{
			var points = new List<NoteBase>();
			if (NoteList == null)
			{
				return points;
			}

			// Native LevelData lists each scoring point explicitly, including ticks
			// sharing a timestamp with a separate visible point.
			var seen = NativeConnector == null ? new HashSet<int>() : null;
			int end = seen == null ? NoteList.Count : NoteList.Count - 1;
			for (int i = 1; i < end; i++)
			{
				NoteBase part = NoteList[i];
				if (part == null || !IsJudgedInteriorPoint(part)
					|| ((seen == null || HasIndependentTail || HasManagedTail) && ReferenceEquals(part, TailNote)))
				{
					continue;
				}

				if (seen != null && !seen.Add(JudgementPosition(part.MusicScoreInfo)))
				{
					continue;
				}

				points.Add(part);
			}

			return points;
		}

		/// <summary>
		/// Auto play generates a point without changing the bar's shape, so an undrawn one is always an
		/// auto point; a chart connection is judged only when it is drawn; a point added in the editor
		/// is a drawn <see cref="LongHoldCombo"/>. A hidden point is neither judged nor automatic.
		/// </summary>
		internal static bool IsJudgedInteriorPoint(NoteBase part)
		{
			return part != null && (part is LongHoldCombo || (part.HasJudgment && !part.IsSkip));
		}

		private static int JudgementPosition(MusicScoreInfo info)
		{
			return (int)Math.Round(info.time * 1000f);
		}
	}
}
