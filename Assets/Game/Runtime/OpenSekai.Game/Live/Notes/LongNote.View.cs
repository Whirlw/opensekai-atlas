using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sekai.Live
{
	public partial class LongNote
	{
		// Bar geometry includes invisible endpoints; ViewNoteList contains only drawable keys.
		internal List<INote> LineNoteList { get; private set; } = new List<INote>();

		protected string _guideColor = null;

		internal NoteBase ConnectorHeadViewNote { get; private set; }
		private int _connectorHeadStyle;
		private NoteType _connectorHeadType;

		public void SetNextRushConnectorHeadViewNote(NoteBase note)
		{
			note.IsNextRushConnectorHeadVisual = true;
			ConnectorHeadViewNote = note;
			_connectorHeadStyle = note.NativeStyle;
			_connectorHeadType = note.Type;
		}

		internal bool UpdateConnectorHeadView(double now)
		{
			if (ConnectorHeadViewNote == null || !TryConnectorPresentation(now, out var segment, out float left, out float right))
				return false;
			ConnectorHeadViewNote.PlaceVisualHead(left, right);
			ConnectorHeadViewNote.NativeStyle = segment.HeadStyle(_connectorHeadStyle);
			ConnectorHeadViewNote.Type = segment.IsDamage || ConnectorHeadViewNote is NextRushDamageNote
				? _connectorHeadType
				: segment.IsCritical ? NoteType.Critical : NoteType.Default;
			return true;
		}

		internal bool TryConnectorPresentation(double now, out NextRushConnectorSegment segment, out float left, out float right)
		{
			segment = null;
			left = right = 0f;
			if (!TryNativeEdge(now, out var start, out var end, visualTime: true))
			{
				// A legacy fake damage head sits at the first endpoint during its lead-in.
				if (NativeConnector?.HasLegacyFakeDamageLeadIn != true
					|| now >= NativeTarget.Time || LineNoteList.Count < 2) return false;
				start = this;
				end = (NoteBase)LineNoteList[1];
				if (now >= end.NativeTarget.Time) return false;
			}
			segment = start.NativeConnector;
			if (segment.IsGuide || segment.FullScreen || segment.IsHidden(now, start.NativeScroll.SampleFrame(now).Hidden)) return false;
			double duration = end.NativeTarget.Time - start.NativeTarget.Time;
			float progress = now <= start.NativeTarget.Time ? 0f :
				segment.Interpolate((float)((now - start.NativeTarget.Time) / duration));
			left = Mathf.LerpUnclamped(start.NativeLeft, end.NativeLeft, progress);
			right = Mathf.LerpUnclamped(start.NativeRight, end.NativeRight, progress);
			return right - left > -1f;
		}

		internal bool TryNativeEdge(double time, out NoteBase start, out NoteBase end, bool visualTime = false)
		{
			start = end = null;
			int last = LineNoteList.Count - 1;
			if (last < 1 || time < TimeOf(this) || time >= TimeOf(LineNoteList[last])) return false;

			int low = 1, high = last;
			while (low < high)
			{
				int middle = (low + high) / 2;
				if (TimeOf(LineNoteList[middle]) <= time) low = middle + 1;
				else high = middle;
			}
			start = (NoteBase)LineNoteList[low - 1];
			end = (NoteBase)LineNoteList[low];
			return true;

			double TimeOf(INote point) => visualTime ? ((NoteBase)point).NativeTarget.Time : point.MusicScoreInfo.time;
		}

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
				// A bar whose end is not drawn has no key phase at all: there is nothing to see arrive and
				// nothing to retire.
				if (bar.childNote == null || !bar.childNote.IsVisible) return TailKeyPhase.Hidden;
				float visibleFrom = bar.childNote.MusicScoreInfo.time - (bar.CalcTimeOffset?.Invoke(bar.childNote) ?? bar.offsetTime);
				return TailKeyLifetime.Of(bar.childNote.MusicScoreInfo.time, visibleFrom, currentTime,
					bar.childNote.Result, !bar.HasManagedTail && !bar.HasIndependentTail && bar.State == NoteState.Done);
			}

			internal static void UpdateLane(LongNote bar, ref MusicScoreInfo currentFrameInfo)
			{
				if (bar.NativeConnector != null)
				{
					INote from = bar, to = bar.childNote;
					for (int i = 1; i < bar.LineNoteList.Count; i++)
					{
						to = bar.LineNoteList[i];
						if (currentFrameInfo.time < to.MusicScoreInfo.time || i == bar.LineNoteList.Count - 1) break;
						from = to;
					}
					LiveUtility.CalcExcuteNoteLane(bar, ref from, ref to, ref currentFrameInfo, bar.MusicScoreInfo, bar.childNote);
					return;
				}
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
