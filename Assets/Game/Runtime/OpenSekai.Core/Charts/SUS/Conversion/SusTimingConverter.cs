using System;
using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using Sekai.SUS;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// Turns the timing rows of a parsed SUS chart — time signatures, BPM changes, <c>#TIL00</c> scroll
	/// speeds and <c>#VOLUME</c> SE levels — into the bar/progress/time frames the rest of the game
	/// indexes by <see cref="MusicScoreInfo"/>.
	/// </summary>
	/// <remarks>
	/// Pure: DTOs in, frames out. Nothing here knows about notes, Unity objects or scenes, which is what
	/// lets the timing of a chart be asserted without an editor.
	/// The merge rules are the importer's original ones. Rows sort by bar, then bar progress, then by a
	/// fixed kind priority (time signature, BPM, speed, volume); rows that land on the same bar position
	/// collapse into one frame whose values are the last row of each kind applied in that order. A frame
	/// at bar 0 progress 0 is always present, because everything downstream assumes a time origin.
	/// </remarks>
	public static class SusTimingConverter
	{
		private enum TimelineKind
		{
			TimeSignature = 0,
			Bpm = 1,
			HighSpeed = 2,
			Volume = 3
		}

		private readonly struct TimelineEntry
		{
			public TimelineEntry(int barIndex, float barProgress, TimelineKind kind, float value)
			{
				BarIndex = barIndex;
				BarProgress = barProgress;
				Kind = kind;
				Value = value;
			}

			public int BarIndex { get; }

			public float BarProgress { get; }

			public TimelineKind Kind { get; }

			public float Value { get; }
		}

		/// <summary>Builds the frame array for <paramref name="chart"/>. Never returns null or empty.</summary>
		public static MusicScoreInfo[] Convert(SusChartData chart)
		{
			if (chart == null)
			{
				throw new ArgumentNullException(nameof(chart));
			}

			List<TimelineEntry> entries = BuildTimeline(chart);
			var seed = new MusicScoreInfo(
				0, 0f, 0f,
				SusChartData.DefaultBpm,
				SusChartData.DefaultTimeSignature,
				SusChartData.DefaultSpeedRatio,
				SusChartData.DefaultSeVolume);
			return Merge(entries, seed);
		}

		/// <summary>
		/// Collects every timing row as a positioned entry, injecting the implicit 4/4 and 120 BPM
		/// defaults the format omits at the origin.
		/// </summary>
		private static List<TimelineEntry> BuildTimeline(SusChartData chart)
		{
			var entries = new List<TimelineEntry>();
			if (!chart.TimeSignatures.Exists(info => info.StartBarIndex == 0))
			{
				entries.Add(new TimelineEntry(0, 0f, TimelineKind.TimeSignature, SusChartData.DefaultTimeSignature));
			}
			foreach (TimeSignatureInfo info in chart.TimeSignatures)
			{
				entries.Add(new TimelineEntry(info.StartBarIndex, 0f, TimelineKind.TimeSignature, info.TimeSignature));
			}
			if (!chart.BpmInfos.Exists(info => info.bar == 0 && Approximately(info.barProgress, 0f)))
			{
				entries.Add(new TimelineEntry(0, 0f, TimelineKind.Bpm, SusChartData.DefaultBpm));
			}
			foreach (BpmInfo info in chart.BpmInfos)
			{
				entries.Add(new TimelineEntry(info.bar, Math.Max(0f, info.barProgress), TimelineKind.Bpm, info.bpm));
			}
			foreach (HighSpeedInfo info in chart.HighSpeedInfos)
			{
				entries.Add(new TimelineEntry(
					info.BarIndex,
					Math.Max(0f, GetTickOffsetBarProgress(chart, info.BarIndex, info.TickOffset)),
					TimelineKind.HighSpeed,
					info.SpeedRatio));
			}
			foreach (VolumeInfo info in chart.VolumeInfos)
			{
				entries.Add(new TimelineEntry(
					info.BarIndex,
					Math.Max(0f, GetTickOffsetBarProgress(chart, info.BarIndex, info.TickOffset)),
					TimelineKind.Volume,
					info.Volume));
			}
			return entries;
		}

		private static MusicScoreInfo[] Merge(List<TimelineEntry> entries, MusicScoreInfo seed)
		{
			if (entries.Count == 0)
			{
				return new[] { seed };
			}

			entries.Sort((left, right) =>
			{
				int compare = left.BarIndex.CompareTo(right.BarIndex);
				if (compare != 0)
				{
					return compare;
				}
				compare = left.BarProgress.CompareTo(right.BarProgress);
				return compare != 0 ? compare : ((int)left.Kind).CompareTo((int)right.Kind);
			});

			var scoreInfos = new List<MusicScoreInfo>();
			float bpm = seed.bpm;
			float timeSignature = seed.timeSignature;
			float speedRatio = seed.speedRatio;
			float seVolume = seed.seVolume;
			int index = 0;
			while (index < entries.Count)
			{
				int bar = entries[index].BarIndex;
				float progress = entries[index].BarProgress;
				while (index < entries.Count
					&& entries[index].BarIndex == bar
					&& Approximately(entries[index].BarProgress, progress))
				{
					TimelineEntry entry = entries[index];
					switch (entry.Kind)
					{
					case TimelineKind.TimeSignature:
						timeSignature = entry.Value;
						break;
					case TimelineKind.Bpm:
						bpm = entry.Value;
						break;
					case TimelineKind.HighSpeed:
						speedRatio = entry.Value;
						break;
					case TimelineKind.Volume:
						seVolume = entry.Value;
						break;
					}
					index++;
				}
				scoreInfos.Add(new MusicScoreInfo(bar, progress, 0f, bpm, timeSignature, speedRatio, seVolume));
			}

			if (scoreInfos.Count == 0 || scoreInfos[0].bar != 0 || !Approximately(scoreInfos[0].barProgress, 0f))
			{
				scoreInfos.Insert(0, seed);
			}

			CalcTime(scoreInfos);
			return scoreInfos.ToArray();
		}

		/// <summary>
		/// Walks the frames in bar order accumulating playing time. Each step is the bar delta times the
		/// previous frame's time signature, which is why the frame at a change carries the old values in
		/// its own position and the new ones from that position onward.
		/// </summary>
		private static void CalcTime(List<MusicScoreInfo> scoreInfoList)
		{
			if (scoreInfoList == null || scoreInfoList.Count == 0)
			{
				return;
			}

			scoreInfoList.Sort(ComparePosition);
			float time = 0f;
			MusicScoreInfo previous = scoreInfoList[0];
			previous.time = 0f;
			scoreInfoList[0] = previous;
			for (int i = 1; i < scoreInfoList.Count; i++)
			{
				MusicScoreInfo current = scoreInfoList[i];
				float deltaBars = current.bar - previous.bar + current.barProgress - previous.barProgress;
				float bpm = previous.bpm <= 0f ? SusChartData.DefaultBpm : previous.bpm;
				time += deltaBars * previous.timeSignature * 60f / bpm;
				current.time = time;
				scoreInfoList[i] = current;
				previous = current;
			}
		}

		private static int ComparePosition(MusicScoreInfo left, MusicScoreInfo right)
		{
			int compare = left.bar.CompareTo(right.bar);
			return compare != 0 ? compare : left.barProgress.CompareTo(right.barProgress);
		}

		/// <summary>
		/// Converts a tick offset inside a bar to a bar progress, using the time signature in force at
		/// that bar.
		/// </summary>
		private static float GetTickOffsetBarProgress(SusChartData chart, int barIndex, int tickOffset)
		{
			float timeSignature = SusChartData.DefaultTimeSignature;
			foreach (TimeSignatureInfo info in chart.TimeSignatures.OrderBy(value => value.StartBarIndex))
			{
				if (info.StartBarIndex > barIndex)
				{
					break;
				}
				timeSignature = info.TimeSignature;
			}
			float barTicks = timeSignature * chart.TicksPerBeat;
			return barTicks <= 0f ? 0f : tickOffset / barTicks;
		}

		/// <summary>
		/// <c>Mathf.Approximately</c> without the engine: same tolerance formula, so the frame grouping
		/// splits at exactly the same bar positions the importer did.
		/// </summary>
		private static bool Approximately(float a, float b)
		{
			return Math.Abs(b - a) < Math.Max(1e-06f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
		}
	}
}
