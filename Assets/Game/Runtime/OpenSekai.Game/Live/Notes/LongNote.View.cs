using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sekai.Live
{
	public partial class LongNote
	{
		// Bar geometry includes invisible endpoints; ViewNoteList contains only drawable keys.
		internal List<INote> LineNoteList { get; private set; } = new List<INote>();

		protected string _guideColor = null;

		public string guideColor
		{
			get => _guideColor ?? MusicScoreInfo.guideColor;
		}

		public void SetGuideColor(string color)
		{
			_guideColor = color;
		}

		public float LaneOffset
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public float JudgedLaneEnd
		{
			get
			{
				return LaneEndF + LaneOffset;
			}
		}

		public float JudgedLaneStart
		{
			get
			{
				return LaneStartF - LaneOffset;
			}
		}

		public override int LaneStart
		{
			get
			{
				return (int)Math.Round(LaneStartF, MidpointRounding.ToEven);
			}
			set
			{
				base.LaneStart = value;
				LaneStartF = value;
			}
		}

		public override int LaneEnd
		{
			get
			{
				return (int)Math.Round(LaneEndF, MidpointRounding.ToEven);
			}
			set
			{
				base.LaneEnd = value;
				LaneEndF = value;
			}
		}

		public override float LaneDistance(ref MusicScoreInfo currentFrameInfo, float inputLane)
		{
			var centerLane = (LaneStartF + LaneEndF) * 0.5f;
			var distance = centerLane - inputLane;
			return distance * distance;
		}

		private static class BarView
		{
			internal static void Recycle(LongNote bar)
			{
				bar.ConnectionNoteTerminate();
				bar.RemoveView();
			}

			internal static TailKeyPhase GetTailKeyPhase(LongNote bar, float currentTime)
			{
				// A bar whose end carries no judgement has no key phase at all: there is nothing to see
				// arrive, nothing to grade and nothing to retire.
				if (bar.childNote == null || !bar.childNote.HasJudgment) return TailKeyPhase.Hidden;
				float visibleFrom = bar.childNote.MusicScoreInfo.time - (bar.CalcTimeOffset?.Invoke(bar.childNote) ?? bar.offsetTime);
				return TailKeyLifetime.Of(bar.childNote.MusicScoreInfo.time, visibleFrom, currentTime,
					bar.childNote.Result, !bar.HasManagedTail && bar.State == NoteState.Done);
			}

			internal static void UpdateLane(LongNote bar, ref MusicScoreInfo currentFrameInfo)
			{
				INote currentNote = bar;
				INote nextNote = bar.childNote;
				if (bar.ViewNoteList != null)
				{
					for (var i = 1; i < bar.ViewNoteList.Count - 1; i++)
					{
						var viewNote = bar.ViewNoteList[i];
						// The tail is the final endpoint even if other markers follow it in the list.
						if (ReferenceEquals(viewNote, bar.childNote))
						{
							break;
						}

						if (viewNote == null || viewNote.IsSkip)
						{
							continue;
						}

						// 装饰note使用时间判断，普通note使用State判断
						// 装饰ConnectionNote永远不会被标记为Done，所以需要特殊处理
						bool isCompleted = viewNote.State == NoteState.Done;
						if (viewNote is NoteBase noteBase && noteBase.IsDecoration)
						{
							// 装饰note：当时间已过判定时间时视为"已完成"
							isCompleted = currentFrameInfo.time >= viewNote.MusicScoreInfo.time;
						}

						if (!isCompleted)
						{
							nextNote = viewNote;
							break;
						}

						currentNote = viewNote;
					}
				}

				LiveUtility.CalcExcuteNoteLane(bar, ref currentNote, ref nextNote, ref currentFrameInfo, bar.MusicScoreInfo, bar.childNote);
			}
		}
	}
}
